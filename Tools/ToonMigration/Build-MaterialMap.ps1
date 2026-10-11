# Build-MaterialMap.ps1
#
# 用途：为项目内的材质批量生成"旧 shader 属性 -> ToonLit 属性"的映射方案（只读，不改任何文件）。
# 输出：Tools\ToonMigration\material-map.json，供 Convert-MaterialsToToon.ps1 消费。
#
# 设计要点：
# 1. 用正则逐段切分材质 YAML，不依赖 ConvertFrom-Yaml（本机 PowerShell 7 默认没有该命令）。
# 2. 贴图按 guid 引用原样搬运，改 shader 后贴图不丢、不串。
# 3. 记录 _MainTex 的 m_Scale/m_Offset，写入新材质的 _BaseMap、_BaseMap_ST，
#    否则 eye_touming 的 x=0.5、pine 的 y=-1 这类非默认 ST 会导致贴图整体错位。

param(
    [string]$ProjectRoot = (Get-Location).Path,
    [string[]]$SkipPathPatterns = @('\\Plugins\\')
)

$ErrorActionPreference = 'Stop'

$toonShaderMeta = Join-Path $ProjectRoot 'Assets\Core\Shaders\Toon\ToonLit.shader.meta'
if (-not (Test-Path $toonShaderMeta)) { throw "找不到 ToonLit.shader.meta：$toonShaderMeta" }
$toonGuid = ([regex]::Match((Get-Content $toonShaderMeta -Raw), 'guid:\s*([0-9a-f]{32})')).Groups[1].Value
if (-not $toonGuid) { throw "无法从 $toonShaderMeta 解析 guid" }

# ---- 已知内置贴图 guid -> 名字（内置资源的 guid 以全 0 开头再跟 fileID）----
$builtinTextureGuid = @{
    '0000000000000000f000000000000000' = '__builtin__'
}
# Unity 内置默认贴图直接以文件名引用即可，无需 guid。
$builtinTextureNames = @{
    10300 = 'white'      # Default-White
    10301 = 'black'      # Default-Black
    10302 = 'grey'       # Default-Grey
    10303 = 'bump'       # Default-Bump
    10304 = 'red'        # Default-Red
    10305 = 'linearGray' # Default-LinearGrey
    10800 = 'white'      # Default-Particle
}

function Get-SectionBlocks {
    param([string]$Text, [string]$Header)

    $start = $Text.IndexOf($Header)
    if ($start -lt 0) { return @() }
    $bodyStart = $start + $Header.Length

    # 段落到下一个同级键为止（同级键的缩进为 4 个空格）。
    # 注意必须用 [A-Za-z_] 而不是 \S —— 列表项的 "    - _BumpMap:" 里 "-" 也是非空白字符，
    # 用 \S 会把第一个列表项当成同级键，段落直接截断成空。
    $rest = $Text.Substring($bodyStart)
    $endMatch = [regex]::Match($rest, '(?m)^    [A-Za-z_]')
    $body = if ($endMatch.Success) { $rest.Substring(0, $endMatch.Index) } else { $rest }

    $blocks = @()
    # 两种写法都要兼容：
    #   "- _Cutoff: 0.5"                 值写在同一行（Standard 材质）
    #   "- _Cutoff:`n        0.47000003" 值写在下一行（部分导入器产生的材质）
    $matches = [regex]::Matches($body, '(?ms)^    - (\w+):[ \t]*(.*?)(?=^    - \w+:|\z)')
    foreach ($m in $matches) {
        $blocks += [pscustomobject]@{
            Prop  = $m.Groups[1].Value
            Lines = $m.Groups[2].Value
        }
    }
    return $blocks
}

function Convert-TextureRef {
    param([string]$Line, [string]$PropName)

    $m = [regex]::Match($Line, 'm_Texture:\s*\{fileID:\s*(-?\d+)(?:,\s*guid:\s*([0-9a-f]{32}))?(?:,\s*type:\s*(\d+))?\}')
    if (-not $m.Success) {
        return [pscustomobject]@{ Texture = 'white'; Note = 'no-texture-object' }
    }

    $fileId = [int]$m.Groups[1].Value
    $guid = $m.Groups[2].Value

    # 无贴图：fileID 为 0 且无 guid；或纯内置默认贴图（guid 全 0）
    if ($fileId -eq 0 -and [string]::IsNullOrEmpty($guid)) {
        return [pscustomobject]@{ Texture = 'white'; Note = 'null-texture' }
    }
    if ($guid -eq '0000000000000000f000000000000000') {
        $name = if ($builtinTextureNames.ContainsKey($fileId)) { $builtinTextureNames[$fileId] } else { 'white' }
        return [pscustomobject]@{ Texture = $name; Note = "builtin-$fileId" }
    }

    # 真实资源贴图：保留 guid 与 type
    $type = if ($m.Groups[3].Success) { $m.Groups[3].Value } else { '3' }
    return [pscustomobject]@{ Texture = "{fileID: $fileId, guid: $guid, type: $type}"; Note = 'asset' }
}

$allMats = Get-ChildItem -Path (Join-Path $ProjectRoot 'Assets') -Recurse -Filter *.mat -File
$report = @()
$skipped = @()

foreach ($mat in $allMats) {
    $rel = $mat.FullName.Substring($ProjectRoot.Length).TrimStart('\')

    $skip = $false
    foreach ($p in $SkipPathPatterns) { if ($rel -match $p) { $skip = $true; break } }
    if ($skip) { $skipped += $rel; continue }

    $text = Get-Content $mat.FullName -Raw

    # ---- 当前 shader ----
    $shaderMatch = [regex]::Match($text, 'm_Shader:\s*\{fileID:\s*(-?\d+),\s*guid:\s*([0-9a-f]{32})')
    $srcFileId = if ($shaderMatch.Success) { [int]$shaderMatch.Groups[1].Value } else { 0 }
    $srcGuid = if ($shaderMatch.Success) { $shaderMatch.Groups[2].Value } else { '' }

    # ---- 关键字 ----
    $kwMatch = [regex]::Match($text, '(?ms)m_ValidKeywords:(.*?)(?=m_InvalidKeywords:)')
    $keywords = if ($kwMatch.Success) {
        [regex]::Matches($kwMatch.Groups[1].Value, '- (\S+)') | ForEach-Object { $_.Groups[1].Value }
    } else { @() }

    # ---- 贴图 ----
    $textures = @{}
    $stByProp = @{}
    foreach ($b in (Get-SectionBlocks -Text $text -Header 'm_TexEnvs:')) {
        $ref = Convert-TextureRef -Line $b.Lines -PropName $b.Prop
        $textures[$b.Prop] = $ref

        $scaleM = [regex]::Match($b.Lines, 'm_Scale:\s*\{x:\s*(-?[\d.eE+-]+),\s*y:\s*(-?[\d.eE+-]+)\}')
        $offM = [regex]::Match($b.Lines, 'm_Offset:\s*\{x:\s*(-?[\d.eE+-]+),\s*y:\s*(-?[\d.eE+-]+)\}')
        if ($scaleM.Success -and $offM.Success) {
            $stByProp[$b.Prop] = @(
                [double]$scaleM.Groups[1].Value, [double]$scaleM.Groups[2].Value,
                [double]$offM.Groups[1].Value, [double]$offM.Groups[2].Value
            )
        }
    }

    # ---- 浮点 ----
    $floats = @{}
    foreach ($b in (Get-SectionBlocks -Text $text -Header 'm_Floats:')) {
        $v = [regex]::Match($b.Lines, '(-?[\d.eE+-]+)')
        if ($v.Success) { $floats[$b.Prop] = [double]$v.Groups[1].Value }
    }

    # ---- 颜色 ----
    $colors = @{}
    foreach ($b in (Get-SectionBlocks -Text $text -Header 'm_Colors:')) {
        $c = [regex]::Match($b.Lines, 'r:\s*(-?[\d.eE+-]+),\s*g:\s*(-?[\d.eE+-]+),\s*b:\s*(-?[\d.eE+-]+),\s*a:\s*(-?[\d.eE+-]+)')
        if ($c.Success) {
            $colors[$b.Prop] = @(
                [double]$c.Groups[1].Value, [double]$c.Groups[2].Value,
                [double]$c.Groups[3].Value, [double]$c.Groups[4].Value
            )
        }
    }

    # ---- 渲染状态 ----
    $customQueue = [int]([regex]::Match($text, 'm_CustomRenderQueue:\s*(-?\d+)').Groups[1].Value)
    $renderType = [regex]::Match($text, 'RenderType:\s*(\S+)').Groups[1].Value
    $isTransparent = ($keywords -contains '_ALPHABLEND_ON') -or
                     ($keywords -contains '_ALPHAPREMULTIPLY_ON') -or
                     ($renderType -eq 'Transparent') -or
                     ($customQueue -ge 3000)

    # ---- 透明度模式 ----
    $mode = if ($floats.ContainsKey('_Mode')) { [int]$floats['_Mode'] } else { 0 }
    $hasCutoff = $floats.ContainsKey('_Cutoff')
    $cutoff = if ($hasCutoff) { $floats['_Cutoff'] } else { 0.5 }

    $alphaClipping = ($keywords -contains '_ALPHATEST_ON') -or ($mode -eq 1)
    # 部分材质的 _Cutoff 是显式写进材质里的（Standard 的 Cutout 模式、SpeedTree 的裁切，
    # 典型值 0.47 / 0.5 / 0.6 / 0.62），这类也要开裁切。
    # 关键：必须先用 $hasCutoff 判断材质"真的声明了"这个属性，
    # 否则兜底默认值 0.5 会反过来把所有材质都误判成需要裁切。
    if (-not $alphaClipping -and $hasCutoff -and $cutoff -lt 0.999) {
        $alphaClipping = $true
    }

    # ---- 主贴图与 ST ----
    $mainTexProp = if ($textures.ContainsKey('_MainTex')) { '_MainTex' }
                   elseif ($textures.ContainsKey('_BaseMap')) { '_BaseMap' }
                   else { $null }
    $mainTex = if ($mainTexProp) { $textures[$mainTexProp].Texture } else { 'white' }
    $mainSt = if ($mainTexProp -and $stByProp.ContainsKey($mainTexProp)) { $stByProp[$mainTexProp] } else { @(1, 1, 0, 0) }

    $baseColor = if ($colors.ContainsKey('_Color')) { $colors['_Color'] }
                 elseif ($colors.ContainsKey('_BaseColor')) { $colors['_BaseColor'] }
                 else { @(1, 1, 1, 1) }

    # ---- 是否为植被（决定是否开风动画）----
    $isGrass = ($rel -match '\\grass\\') -and ($rel -match '\.mat$')

    $blend = if ($isTransparent) { @(5, 10) } else { @(1, 0) }   # SrcAlpha/OneMinusSrcAlpha 或 One/Zero

    $report += [pscustomobject]@{
        path            = $rel
        name            = $mat.BaseName
        srcShaderFileID = $srcFileId
        srcShaderGuid   = $srcGuid
        srcShader       = switch ($srcFileId) {
            46    { 'Standard' }
            51    { 'Nature/SpeedTree(legacy)' }
            10509 { 'Standard(speedtree)' }
            10511 { 'Nature/SpeedTree8' }
            default { "fileID=$srcFileId" }
        }
        isTransparent   = $isTransparent
        alphaClipping   = $alphaClipping
        isGrass         = $isGrass
        keywords        = $keywords
        baseMap         = $mainTex
        baseMapST       = $mainSt
        baseColor       = $baseColor
        cutoff          = $cutoff
        srcBlend        = $blend[0]
        dstBlend        = $blend[1]
        customQueue     = $customQueue
        renderType      = $renderType
        emissionMap     = if ($textures.ContainsKey('_EmissionMap')) { $textures['_EmissionMap'].Texture } else { 'white' }
        occlusionMap    = if ($textures.ContainsKey('_OcclusionMap')) { $textures['_OcclusionMap'].Texture } else { 'white' }
        emissionColor   = if ($colors.ContainsKey('_EmissionColor')) { $colors['_EmissionColor'] } else { @(0, 0, 0, 1) }
        metallic        = if ($floats.ContainsKey('_Metallic')) { $floats['_Metallic'] } else { 0 }
        glossiness      = if ($floats.ContainsKey('_Glossiness')) { $floats['_Glossiness'] } else { 0.5 }
    }
}

$out = [pscustomobject]@{
    generatedAt  = (Get-Date).ToString('s')
    projectRoot  = $ProjectRoot
    toonShader   = @{ guid = $toonGuid; fileID = 4800000 }
    materialCount = $report.Count
    skippedCount = $skipped.Count
    skipped      = $skipped
    materials    = $report
}

$outPath = Join-Path $ProjectRoot 'Tools\ToonMigration\material-map.json'
$out | ConvertTo-Json -Depth 8 | Set-Content -Path $outPath -Encoding utf8

Write-Host "已写出映射方案：$outPath"
Write-Host "待转换材质：$($report.Count) 个；跳过（第三方插件）：$($skipped.Count) 个"
Write-Host ""
Write-Host "---- 待转换材质概览 ----"
$report | Group-Object srcShader | ForEach-Object { "  {0,-26} {1} 个" -f $_.Name, $_.Count }
Write-Host ""
$report | ForEach-Object {
    "  {0,-42} {1,-24} clip={2,-5} transp={3,-5} grass={4,-5} cutoff={5}" -f `
        $_.name, $_.srcShader, $_.alphaClipping, $_.isTransparent, $_.isGrass, $_.cutoff
}
