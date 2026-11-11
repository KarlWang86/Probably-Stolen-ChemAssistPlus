# 仅显式留档/发布时打包；不部署、不上传、不更新 WorkshopUpload。
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'scripts/common.ps1')
& (Join-Path $taskProject 'build.ps1')
$version = Get-TaskVersion
$releaseFolder = Join-Path $taskProject ("releases/{0}-{1}" -f $version,(Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
$install = Join-Path $releaseFolder 'install/ChemicalWorkflowAssistant'
$source = Join-Path $releaseFolder 'source/ChemAssistPlus'
New-Item -ItemType Directory -Path $install,$source -Force | Out-Null
foreach ($file in @('ChemAssistPlus.dll','manifest.xml','使用说明.txt')) {
    Copy-Item -LiteralPath (Join-Path $buildFolder $file) -Destination $install
}
# 白名单：不包含游戏/参考 Mod DLL、研究 IL、旧构建、历史测试、旧归档。
Get-ChildItem -LiteralPath $taskProject -File | Where-Object {
    $_.Extension -in @('.cs','.csproj','.props','.ps1','.md') -or $_.Name -in @('VERSION','NuGet.Config','manifest.xml','使用说明.txt')
} | Copy-Item -Destination $source
foreach ($dir in @('scripts','Tools/NativeScan','Tools/AssemblyQuery')) {
    $target = Join-Path $source $dir
    New-Item -ItemType Directory -Path $target -Force | Out-Null
    Get-ChildItem -LiteralPath (Join-Path $taskProject $dir) -File | Where-Object { $_.Extension -in @('.cs','.csproj','.ps1') } | Copy-Item -Destination $target
}
Compress-Archive -LiteralPath (Join-Path $releaseFolder 'install/ChemicalWorkflowAssistant') -DestinationPath (Join-Path $releaseFolder "ChemAssistPlus-$version-install.zip")
Compress-Archive -LiteralPath (Join-Path $releaseFolder 'source/ChemAssistPlus') -DestinationPath (Join-Path $releaseFolder "ChemAssistPlus-$version-source.zip")
Write-Output "Release archives: $releaseFolder"
