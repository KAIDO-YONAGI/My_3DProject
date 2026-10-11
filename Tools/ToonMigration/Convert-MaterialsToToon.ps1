# Convert-MaterialsToToon.ps1
#
# 用途：按 Tools\ToonMigration\material-map.json 的方案，把项目自有材质的 shader
#       从内置 Standard / Nature/SpeedTree 换成 My_3DProject/Toon/ToonLit (NiloCat URP)。
#
# 默认只做预览（-WhatIf）。确认无误后再加 -Apply 真正落盘。
#
# 安全措施：
# 1. 先跑 Build-MaterialMap.ps1 生成映射方案，脚本只消费该方案，不重新猜属性。
# 2. -Apply 时先把原材质备份到 .toon-migration-backup/，便于逐个回退。
# 3. 贴图按 guid 原样搬运；_MainTex 的 Scale/Offset 写进 _BaseMap_ST，不丢不错。
# 4. 以 LF + UTF-8 无 BOM 写回，与仓库现有材质风格一致。

param(
    [string]$ProjectRoot = (Get-Location).Path,
    [switch]$Apply,
    [switch]$Force,          # -Apply 时若备份目录已存在也继续
    [string]$ShowYamlFor     # 只打印指定材质名生成的 YAML，不写任何文件，用于落盘前人工核对
)

$ErrorActionPreference = 'Stop'

$mapPath = Join-Path $ProjectRoot 'Tools\ToonMigration\material-map.json'
if (-not (Test-Path $mapPath)) { throw "找不到映射方案，请先运行 Build-MaterialMap.ps1：$mapPath" }
$map = Get-Content $mapPath -Raw | ConvertFrom-Json

$toonGuid = $map.toonShader.guid
$toonFileId = $map.toonShader.fileID

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)

function Fmt([double]$v) {
    # 紧凑输出，避免出现 0.5000000000000001 这种噪声
    $s = $v.ToString('0.###############', [System.Globalization.CultureInfo]::InvariantCulture)
    if ($s -eq '-0') { $s = '0' }
    return $s
}
function FmtVec([object[]]$a) { return ($a | ForEach-Object { Fmt ([double]$_) }) -join ',' }

function New-ToonMaterialYaml {
    param($Entry)

    $sb = New-Object System.Text.StringBuilder

    [void]$sb.AppendLine('%YAML 1.1')
    [void]$sb.AppendLine('%TAG !u! tag:unity3d.com,2011:')
    [void]$sb.AppendLine('--- !u!21 &2100000')
    [void]$sb.AppendLine('Material:')
    [void]$sb.AppendLine('  serializedVersion: 8')
    [void]$sb.AppendLine('  m_ObjectHideFlags: 0')
    [void]$sb.AppendLine('  m_CorrespondingSourceObject: {fileID: 0}')
    [void]$sb.AppendLine('  m_PrefabInstance: {fileID: 0}')
    [void]$sb.AppendLine('  m_PrefabAsset: {fileID: 0}')
    [void]$sb.AppendLine("  m_Name: $($Entry.name)")
    [void]$sb.AppendLine("  m_Shader: {fileID: $toonFileId, guid: $toonGuid, type: 3}")
    [void]$sb.AppendLine('  m_Parent: {fileID: 0}')
    [void]$sb.AppendLine('  m_ModifiedSerializedProperties: 0')

    if ($Entry.alphaClipping) {
        [void]$sb.AppendLine('  m_ValidKeywords:')
        [void]$sb.AppendLine('  - _UseAlphaClipping')
    }
    else {
        [void]$sb.AppendLine('  m_ValidKeywords: []')
    }
    [void]$sb.AppendLine('  m_InvalidKeywords: []')
    [void]$sb.AppendLine('  m_LightmapFlags: 4')
    [void]$sb.AppendLine('  m_EnableInstancingVariants: 0')
    [void]$sb.AppendLine('  m_DoubleSidedGI: 0')

    if ($Entry.isTransparent) {
        [void]$sb.AppendLine("  m_CustomRenderQueue: $($Entry.customQueue)")
        [void]$sb.AppendLine('  stringTagMap:')
        [void]$sb.AppendLine('    RenderType: Transparent')
    }
    else {
        [void]$sb.AppendLine('  m_CustomRenderQueue: -1')
        [void]$sb.AppendLine('  stringTagMap: {}')
    }

    [void]$sb.AppendLine('  disabledShaderPasses: []')
    [void]$sb.AppendLine('  m_LockedProperties: ')
    [void]$sb.AppendLine('  m_SavedProperties:')
    [void]$sb.AppendLine('    serializedVersion: 3')
    [void]$sb.AppendLine('    m_TexEnvs:')

    # 主贴图：guid 原样搬运
    [void]$sb.AppendLine('    - _BaseMap:')
    [void]$sb.AppendLine("        m_Texture: $($Entry.baseMap)")
    [void]$sb.AppendLine('        m_Scale: {x: 1, y: 1}')
    [void]$sb.AppendLine('        m_Offset: {x: 0, y: 0}')

    $st = $Entry.baseMapST
    [void]$sb.AppendLine('    - _BaseMap_ST:')
    [void]$sb.AppendLine('        m_Texture: {fileID: 0}')
    [void]$sb.AppendLine("        m_Scale: {x: $(Fmt ([double]$st[0])), y: $(Fmt ([double]$st[1]))}")
    [void]$sb.AppendLine("        m_Offset: {x: $(Fmt ([double]$st[2])), y: $(Fmt ([double]$st[3]))}")

    [void]$sb.AppendLine('    - _EmissionMap:')
    [void]$sb.AppendLine("        m_Texture: $($Entry.emissionMap)")
    [void]$sb.AppendLine('        m_Scale: {x: 1, y: 1}')
    [void]$sb.AppendLine('        m_Offset: {x: 0, y: 0}')

    [void]$sb.AppendLine('    - _OcclusionMap:')
    [void]$sb.AppendLine("        m_Texture: $($Entry.occlusionMap)")
    [void]$sb.AppendLine('        m_Scale: {x: 1, y: 1}')
    [void]$sb.AppendLine('        m_Offset: {x: 0, y: 0}')

    [void]$sb.AppendLine('    - _OutlineZOffsetMaskTex:')
    [void]$sb.AppendLine('        m_Texture: {fileID: 0}')
    [void]$sb.AppendLine('        m_Scale: {x: 1, y: 1}')
    [void]$sb.AppendLine('        m_Offset: {x: 0, y: 0}')

    [void]$sb.AppendLine('    m_Ints: []')
    [void]$sb.AppendLine('    m_Floats:')

    $floatRows = [ordered]@{
        '_IsFace'                       = 0
        '_UseAlphaClipping'             = if ($Entry.alphaClipping) { 1 } else { 0 }
        '_Cutoff'                       = [double]$Entry.cutoff
        '_UseEmission'                  = 0
        '_EmissionMulByBaseColor'       = 0
        '_EmissionMapChannelMask'       = $null   # Vector，走 m_Colors 之外的写法，见下
        '_UseOcclusion'                 = 0
        '_OcclusionStrength'            = 1
        '_OcclusionRemapStart'          = 0
        '_OcclusionRemapEnd'            = 1
        '_IndirectLightMultiplier'      = 1
        '_DirectLightMultiplier'        = 1
        '_CelShadeMidPoint'             = 0
        '_CelShadeSoftness'             = 0.05
        '_MainLightIgnoreCelShade'      = 0
        '_AdditionalLightIgnoreCelShade' = 0.9
        '_ReceiveShadowMappingAmount'   = 0.65
        '_ReceiveShadowMappingPosOffset' = 0
        '_OutlineWidth'                 = 1
        '_OutlineZOffset'               = 0.0001
        '_OutlineZOffsetMaskRemapStart' = 0
        '_OutlineZOffsetMaskRemapEnd'   = 1
        '_UseGrassWind'                 = if ($Entry.isGrass) { 1 } else { 0 }
        '_GrassWindStrength'            = 0.08
        '_GrassWindSpeed'               = 1.5
        '_GrassWindFrequency'           = 1
        '_SrcBlend'                     = [double]$Entry.srcBlend
        '_DstBlend'                     = [double]$Entry.dstBlend
    }

    foreach ($k in $floatRows.Keys) {
        $v = $floatRows[$k]
        if ($null -eq $v) { continue }
        [void]$sb.AppendLine("    - ${k}: $(Fmt ([double]$v))")
    }

    [void]$sb.AppendLine('    m_Colors:')
    [void]$sb.AppendLine("    - _BaseColor: {r: $(Fmt ([double]$Entry.baseColor[0])), g: $(Fmt ([double]$Entry.baseColor[1])), b: $(Fmt ([double]$Entry.baseColor[2])), a: $(Fmt ([double]$Entry.baseColor[3]))}")
    [void]$sb.AppendLine("    - _EmissionColor: {r: $(Fmt ([double]$Entry.emissionColor[0])), g: $(Fmt ([double]$Entry.emissionColor[1])), b: $(Fmt ([double]$Entry.emissionColor[2])), a: $(Fmt ([double]$Entry.emissionColor[3]))}")
    [void]$sb.AppendLine('    - _IndirectLightMinColor: {r: 0.1, g: 0.1, b: 0.1, a: 1}')
    [void]$sb.AppendLine('    - _ShadowMapColor: {r: 1, g: 0.825, b: 0.78, a: 1}')
    [void]$sb.AppendLine('    - _OutlineColor: {r: 0.5, g: 0.5, b: 0.5, a: 1}')
    # Vector 属性在 Unity 的材质序列化里同样落在 m_Colors 段
    [void]$sb.AppendLine('    - _EmissionMapChannelMask: {r: 1, g: 1, b: 1, a: 0}')
    [void]$sb.AppendLine('    - _OcclusionMapChannelMask: {r: 1, g: 0, b: 0, a: 0}')
    [void]$sb.AppendLine('    - _GrassWindDirection: {r: 1, g: 0, b: 0, a: 1}')
    [void]$sb.AppendLine('  m_BuildTextureStacks: []')

    return $sb.ToString()
}

$backupRoot = Join-Path $ProjectRoot '.toon-migration-backup'

if ($ShowYamlFor) {
    $hit = $map.materials | Where-Object { $_.name -eq $ShowYamlFor }
    if (-not $hit) { throw "映射方案里没有名为 '$ShowYamlFor' 的材质。" }
    foreach ($h in $hit) {
        Write-Host "======== $($h.path) ========"
        Write-Host (New-ToonMaterialYaml -Entry $h)
    }
    exit 0
}

if ($Apply) {
    if ((Test-Path $backupRoot) -and -not $Force) {
        throw "备份目录已存在：$backupRoot`n如需覆盖请显式加 -Force。"
    }
    New-Item -ItemType Directory -Force -Path $backupRoot | Out-Null
}

$converted = 0
$skipped   = 0

foreach ($entry in $map.materials) {
    $srcPath = Join-Path $ProjectRoot $entry.path
    if (-not (Test-Path $srcPath)) {
        Write-Warning "找不到材质，跳过：$($entry.path)"
        $skipped++
        continue
    }

    $newYaml = New-ToonMaterialYaml -Entry $entry

    if ($Apply) {
        # 备份：把相对路径里的目录分隔符换成 __，便于扁平存放
        $backupName = ($entry.path -replace '[\\/]', '__')
        $backupPath = Join-Path $backupRoot $backupName
        Copy-Item -Path $srcPath -Destination $backupPath -Force

        [System.IO.File]::WriteAllText($srcPath, $newYaml, $utf8NoBom)
        Write-Host "已转换: $($entry.path)"
    }
    else {
        $tag = @()
        if ($entry.alphaClipping) { $tag += '裁切' }
        if ($entry.isTransparent) { $tag += '半透明' }
        if ($entry.isGrass)       { $tag += '风动画' }
        $tagText = if ($tag.Count) { " [$($tag -join '/')]" } else { '' }
        Write-Host "预览: $($entry.path)$tagText"
    }
    $converted++
}

Write-Host ''
if ($Apply) {
    Write-Host "已转换 $converted 个材质，跳过 $skipped 个。"
    Write-Host "原材质备份在：$backupRoot"
    Write-Host ''
    Write-Host '下一步：'
    Write-Host '  1. 打开 Unity，等待导入与着色器编译完成'
    Write-Host '  2. 确认 URP Asset 已挂到 GraphicsSettings 与 QualitySettings'
    Write-Host '  3. 检查控制台是否有着色器编译错误'
}
else {
    Write-Host "预览完成：将转换 $converted 个材质，跳过 $skipped 个。"
    Write-Host '确认无误后加 -Apply 真正落盘。'
}
