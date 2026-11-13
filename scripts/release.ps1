# 仅显式留档/发布时打包；不部署、不上传、不更新 WorkshopUpload。
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')
& (Join-Path $PSScriptRoot 'build.ps1')
$version = Get-TaskVersion
$releaseFolder = Join-Path $repoRoot ("releases/{0}-{1}" -f $version,(Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
$install = Join-Path $releaseFolder 'install/ChemicalWorkflowAssistant'
$sourceRoot = Join-Path $releaseFolder 'source'
$source = Join-Path $sourceRoot 'ChemAssistPlus'
New-Item -ItemType Directory -Path $install,$source -Force | Out-Null
foreach ($file in @('ChemAssistPlus.dll','manifest.xml','使用说明.txt')) {
    Copy-Item -LiteralPath (Join-Path $buildFolder $file) -Destination $install
}
# 白名单：不包含游戏/参考 Mod DLL、研究 IL、旧构建、历史测试、旧归档。
Get-ChildItem -LiteralPath $taskProject -File | Where-Object {
    $_.Extension -in @('.cs','.csproj') -or $_.Name -in @('VERSION','manifest.xml')
} | Copy-Item -Destination $source
foreach ($name in @('AGENTS.md','README.md','LICENSE','Directory.Build.props','NuGet.Config','.gitignore')) {
    Copy-Item -LiteralPath (Join-Path $repoRoot $name) -Destination $sourceRoot
}
foreach ($dir in @('docs','scripts','Screenshots','Tools/NativeScan','Tools/AssemblyQuery','Tools/WorkshopPublisher')) {
    $target = Join-Path $sourceRoot $dir
    New-Item -ItemType Directory -Path $target -Force | Out-Null
    Get-ChildItem -LiteralPath (Join-Path $repoRoot $dir) -File | Where-Object {
        $_.Extension -in @('.cs','.csproj','.ps1','.md','.txt','.png','.jpg','.jpeg')
    } | Copy-Item -Destination $target
}
Compress-Archive -LiteralPath (Join-Path $releaseFolder 'install/ChemicalWorkflowAssistant') -DestinationPath (Join-Path $releaseFolder "ChemAssistPlus-$version-install.zip")
Compress-Archive -Path (Join-Path $sourceRoot '*') -DestinationPath (Join-Path $releaseFolder "ChemAssistPlus-$version-source.zip")
Write-Output "Release archives: $releaseFolder"
