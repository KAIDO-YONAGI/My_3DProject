# Build-GrassWindMap.ps1
#
# 用途：从 material-map.json 里筛出草地材质，生成 Tools\ToonMigration\grass-wind-map.json。
#       该文件供 Unity 编辑器脚本 UrpAssetSetup.cs 消费：
#       草地原来是废弃的 Nature/SpeedTree 着色器，自带风动画；迁到 URP 后那个着色器不可用，
#       需要为每块草地单独建一个开启 _UseGrassWind 的 ToonLit 材质，再替换 Terrain 上的引用。
#
# 只读脚本，不修改任何材质。

param(
    [string]$ProjectRoot = (Get-Location).Path
)

$ErrorActionPreference = 'Stop'

$mapPath = Join-Path $ProjectRoot 'Tools\ToonMigration\material-map.json'
if (-not (Test-Path $mapPath)) { throw "找不到 $mapPath，请先运行 Build-MaterialMap.ps1" }
$map = Get-Content $mapPath -Raw | ConvertFrom-Json

$toonGuid = $map.toonShader.guid

$grass = $map.materials | Where-Object { $_.isGrass }

$items = foreach ($g in $grass) {
    # Assets\Materials\ForestMaterials\grass\Materials\grass_01.mat
    #   -> Assets\Materials\ForestMaterials\grass\Toon\grass_01_Toon.mat
    $dir = [System.IO.Path]::GetDirectoryName($g.path)
    $stem = [System.IO.Path]::GetFileNameWithoutExtension($g.path)
    $toonDir = Join-Path $dir 'Toon'
    $newPath = (Join-Path $toonDir "$($stem)_Toon.mat").Replace('\', '/')

    [pscustomobject]@{
        oldPath    = $g.path
        newPath    = $newPath
        name       = "$($stem)_Toon"
        oldShader  = $g.srcShader
        baseColor  = $g.baseColor
        baseMapST  = $g.baseMapST
        cutoff     = $g.cutoff
        alphaClipping = $g.alphaClipping
    }
}

$out = [pscustomobject]@{
    generatedAt            = (Get-Date).ToString('s')
    toonShaderName         = 'My_3DProject/Toon/ToonLit (NiloCat URP)'
    toonShaderGuid         = $toonGuid
    grassWindStrength      = 0.08
    grassWindSpeed         = 1.5
    grassWindFrequency     = 1.0
    grassCount             = @($items).Count
    grasses                = @($items)
}

$outPath = Join-Path $ProjectRoot 'Tools\ToonMigration\grass-wind-map.json'
$out | ConvertTo-Json -Depth 6 | Set-Content -Path $outPath -Encoding utf8

Write-Host "已写出草地风动画映射：$outPath"
Write-Host "草地材质数量：$($out.grassCount)"
$items | ForEach-Object { "  $($_.oldPath)`n    -> $($_.newPath)" }
