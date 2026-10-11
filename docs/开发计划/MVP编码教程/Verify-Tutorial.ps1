param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path,
    [string]$ExportPath,
    [string]$UnityEditorRoot = 'D:\Unity\Editor\2022.3.62f3c1\Editor',
    [switch]$CoreOnly
)

$ErrorActionPreference = 'Stop'
$utf8 = [System.Text.UTF8Encoding]::new($false, $true)
$outputRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('MvpTutorial-' + [guid]::NewGuid().ToString('N'))
[System.IO.Directory]::CreateDirectory($outputRoot) | Out-Null
$sources = @{}
$markdownFiles = @(Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.md' | Sort-Object Name)

foreach ($document in $markdownFiles) {
    $text = [System.IO.File]::ReadAllText($document.FullName, $utf8)
    if ($text.Contains([char]0xFFFD)) { throw "发现替换字符: $($document.Name)" }
    $fences = [regex]::Matches($text, '(?m)^```')
    if ($fences.Count % 2 -ne 0) { throw "代码围栏数量无效: $($document.Name)" }
    foreach ($match in [regex]::Matches($text, '(?ms)^```csharp file=(?<path>[^\r\n]+)\r?\n(?<code>.*?)^```\r?$')) {
        $relative = $match.Groups['path'].Value
        if ($relative -notmatch '^(Core|Unity|Tests)/[A-Za-z0-9.]+\.cs$') { throw "源文件路径无效: $relative" }
        if ($sources.ContainsKey($relative)) { throw "源文件定义重复: $relative" }
        $sources[$relative] = $match.Groups['code'].Value
    }
    foreach ($match in [regex]::Matches($text, '\[[^\]]+\]\((?<target>[^)]+)\)')) {
        $link = $match.Groups['target'].Value.Split('#')[0]
        if ($link -eq '' -or $link -match '^[a-zA-Z]+:') { continue }
        $resolved = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ([Uri]::UnescapeDataString($link))))
        if (-not (Test-Path -LiteralPath $resolved)) { throw "本地链接无效: $($document.Name) -> $link" }
    }
}
if ($sources.Count -ne 11) { throw "预期 11 个完整源文件，实际 $($sources.Count)" }

# 只提取教程中的完整代码，编译与阅读使用同一份接口。
foreach ($relative in $sources.Keys) {
    $target = Join-Path $outputRoot $relative
    [System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($target)) | Out-Null
    [System.IO.File]::WriteAllText($target, $sources[$relative], $utf8)
}

$sdkVersion = (& dotnet --version).Trim()
if ($LASTEXITCODE -ne 0) { throw '.NET SDK 查询失败' }
$sdkMajor = [int]$sdkVersion.Split('.')[0]
if ($sdkMajor -lt 6) { throw '核心测试使用 .NET SDK 6 或更高版本' }
$testProject = Join-Path $outputRoot 'Tests.csproj'
$projectXml = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net$sdkMajor.0</TargetFramework>
    <LangVersion>9.0</LangVersion>
    <ImplicitUsings>disable</ImplicitUsings>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="Core/*.cs" />
    <Compile Include="Tests/*.cs" />
  </ItemGroup>
</Project>
"@
[System.IO.File]::WriteAllText($testProject, $projectXml, $utf8)
& dotnet run --project $testProject --configuration Release
if ($LASTEXITCODE -ne 0) { throw '核心编译或测试失败' }

if (-not $CoreOnly) {
    $generatedProject = Join-Path $ProjectRoot 'Assembly-CSharp.csproj'
    if (-not (Test-Path -LiteralPath $generatedProject)) { throw '请在 Unity 中生成工程文件' }
    [xml]$unityProject = [System.IO.File]::ReadAllText($generatedProject, $utf8)
    $referencePaths = @($unityProject.SelectNodes("//*[local-name()='HintPath']") |
        ForEach-Object { [System.IO.Path]::GetFullPath($_.InnerText, $ProjectRoot) } |
        Where-Object { Test-Path -LiteralPath $_ })
    $referencePaths += @(
        (Join-Path $ProjectRoot 'Library\ScriptAssemblies\Mirror.dll'),
        (Join-Path $ProjectRoot 'Library\ScriptAssemblies\Unity.InputSystem.dll')
    )
    $referencePaths = @($referencePaths | Sort-Object -Unique)
    foreach ($required in @('Mirror.dll', 'Unity.InputSystem.dll', 'UnityEngine.CoreModule.dll', 'netstandard.dll')) {
        if (-not ($referencePaths | Where-Object { [System.IO.Path]::GetFileName($_) -eq $required })) {
            throw "编译依赖缺失: $required"
        }
    }
    if (-not (Test-Path -LiteralPath (Join-Path $UnityEditorRoot 'Data\Managed\UnityEngine\UnityEngine.CoreModule.dll'))) {
        throw 'Unity 编辑器路径无效'
    }
    $sdkEntry = @(& dotnet --list-sdks) | Where-Object { $_ -match "^$([regex]::Escape($sdkVersion)) " } | Select-Object -First 1
    if ($sdkEntry -notmatch '\[(?<directory>[^\]]+)\]') { throw 'Roslyn 编译器定位失败' }
    $compiler = Join-Path $Matches['directory'] "$sdkVersion\Roslyn\bincore\csc.dll"
    $arguments = @('-nologo', '-target:library', '-langversion:9.0', '-nostdlib+', '-debug:portable',
        "-out:$(Join-Path $outputRoot 'MvpTutorial.dll')")
    $arguments += @($referencePaths | ForEach-Object { "-reference:$_" })
    $arguments += @($sources.Keys | Where-Object { $_ -match '^(Core|Unity)/' } |
        Sort-Object | ForEach-Object { Join-Path $outputRoot $_ })
    & dotnet $compiler @arguments
    if ($LASTEXITCODE -ne 0) { throw 'Unity 与 Mirror 接口编译失败' }
    Write-Output 'PASS: Unity / Mirror / Input System 接口编译'

    $weaverRoot = Join-Path $outputRoot 'Weaver'
    [System.IO.Directory]::CreateDirectory($weaverRoot) | Out-Null
    [System.IO.File]::Copy((Join-Path $PSScriptRoot 'WeaverCheck.cs'), (Join-Path $weaverRoot 'Program.cs'))
    $weaverReferences = @(
        (Join-Path $ProjectRoot 'Library\ScriptAssemblies\Unity.Mirror.CodeGen.dll'),
        (Join-Path $ProjectRoot 'Library\ScriptAssemblies\Mirror.dll'),
        (Join-Path $ProjectRoot 'Assets\Plugins\Mirror\Plugins\Mono.Cecil\Mono.CecilX.dll'),
        (Join-Path $ProjectRoot 'Assets\Plugins\Mirror\Plugins\Mono.Cecil\Mono.CecilX.Rocks.dll'),
        (Join-Path $UnityEditorRoot 'Data\Managed\Unity.CompilationPipeline.Common.dll'),
        (Join-Path $UnityEditorRoot 'Data\Managed\UnityEngine\UnityEngine.CoreModule.dll'),
        (Join-Path $UnityEditorRoot 'Data\Managed\UnityEngine\UnityEditor.CoreModule.dll')
    )
    $weaverXml = [xml]"<Project Sdk='Microsoft.NET.Sdk'><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net$sdkMajor.0</TargetFramework><LangVersion>9.0</LangVersion></PropertyGroup><ItemGroup /></Project>"
    $items = $weaverXml.SelectSingleNode('/Project/ItemGroup')
    foreach ($reference in $weaverReferences) {
        if (-not (Test-Path -LiteralPath $reference)) { throw "Weaver 依赖缺失: $reference" }
        $node = $weaverXml.CreateElement('Reference')
        $node.SetAttribute('Include', [System.IO.Path]::GetFileNameWithoutExtension($reference))
        $hint = $weaverXml.CreateElement('HintPath')
        $hint.InnerText = $reference
        $node.AppendChild($hint) | Out-Null
        $items.AppendChild($node) | Out-Null
    }
    $weaverProject = Join-Path $weaverRoot 'Weaver.csproj'
    $weaverXml.Save($weaverProject)
    $referenceFile = Join-Path $outputRoot 'References.txt'
    [System.IO.File]::WriteAllLines($referenceFile, [string[]]$referencePaths, $utf8)
    & dotnet run --project $weaverProject --configuration Release -- (Join-Path $outputRoot 'MvpTutorial.dll') $referenceFile
    if ($LASTEXITCODE -ne 0) { throw 'Mirror Weaver 验证失败' }
}

if ($ExportPath) {
    $exportRoot = [System.IO.Path]::GetFullPath($ExportPath)
    # 导出前检查全部目标，保留学习目录内已经存在的不同内容。
    foreach ($relative in $sources.Keys) {
        $target = [System.IO.Path]::GetFullPath((Join-Path $exportRoot $relative))
        if (-not $target.StartsWith($exportRoot.TrimEnd('\') + '\', [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "导出路径越界: $target"
        }
        if ((Test-Path -LiteralPath $target) -and
            [System.IO.File]::ReadAllText($target, $utf8) -ne $sources[$relative]) {
            throw "目标已存在不同内容: $target"
        }
    }
    foreach ($relative in $sources.Keys) {
        $target = Join-Path $exportRoot $relative
        [System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($target)) | Out-Null
        [System.IO.File]::WriteAllText($target, $sources[$relative], $utf8)
    }
    Write-Output "导出目录: $exportRoot"
}
Write-Output "PASS: $($markdownFiles.Count) 篇文档 / $($sources.Count) 个源码文件 / UTF-8 与本地链接"
Write-Output "验证产物: $outputRoot"
