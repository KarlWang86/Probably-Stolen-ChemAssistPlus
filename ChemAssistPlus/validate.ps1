# 默认扫描 build；不写 Mods，不启动游戏。
param([string]$ModFolder)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'scripts/common.ps1')
if (!$ModFolder) { $ModFolder = $buildFolder }
$ModFolder = [IO.Path]::GetFullPath($ModFolder)
$receiptPath = Join-Path $ModFolder 'validation.json'
if (Test-Path -LiteralPath $receiptPath) { Remove-Item -LiteralPath $receiptPath }
$manifest = [xml](Get-Content -LiteralPath (Join-Path $ModFolder 'manifest.xml') -Raw -Encoding UTF8)
if ($manifest.Manifest.ID -ne 'chemical_workflow_assistant') { throw 'Incorrect independent mod ID.' }
if (@($manifest.Manifest.Assemblies.Assembly).Count -ne 1 -or $manifest.Manifest.Assemblies.Assembly -ne 'ChemAssistPlus.dll') { throw 'Unexpected packaged assembly.' }
if ($manifest.Manifest.Prerequisites.HasChildNodes -or $manifest.Manifest.Capabilities.HasChildNodes) { throw 'Unexpected prerequisites or capabilities.' }
$dll = Join-Path $ModFolder 'ChemAssistPlus.dll'
$version = Get-TaskVersion
if ($manifest.Manifest.ModVersion -ne $version -or [Reflection.AssemblyName]::GetAssemblyName($dll).Version.ToString(3) -ne $version) { throw 'Assembly / manifest / VERSION mismatch.' }
$toolOutput = Join-Path $buildFolder 'tools/NativeScan'
dotnet build (Join-Path $taskProject 'Tools/NativeScan/NativeScan.csproj') -c Release -o $toolOutput --ignore-failed-sources -p:NuGetAudit=false "-p:RestoreConfigFile=$taskProject/NuGet.Config"
if ($LASTEXITCODE -ne 0) { throw 'Native scanner build failed.' }
# 扫描器会递归检查载荷目录；工具二进制必须与运行载荷隔离。
$scanFolder = Join-Path $buildFolder 'scan-input'
New-Item -ItemType Directory -Path $scanFolder -Force | Out-Null
foreach ($file in @('ChemAssistPlus.dll','manifest.xml','使用说明.txt')) {
    Copy-Item -LiteralPath (Join-Path $ModFolder $file) -Destination $scanFolder
}
$scanOutput = & dotnet (Join-Path $toolOutput 'NativeScan.dll') $managed $scanFolder
if ($LASTEXITCODE -ne 0) { throw 'Native mod scan failed.' }
$hashes = [ordered]@{}
foreach ($file in @('ChemAssistPlus.dll','manifest.xml','使用说明.txt')) {
    $hashes[$file] = (Get-FileHash -LiteralPath (Join-Path $ModFolder $file) -Algorithm SHA256).Hash
}
foreach ($file in $hashes.Keys) {
    if ($hashes[$file] -ne (Get-FileHash -LiteralPath (Join-Path $scanFolder $file) -Algorithm SHA256).Hash) { throw "Payload changed during scan: $file" }
}
$receipt = [ordered]@{
    Version = $version
    ScannedAt = (Get-Date).ToString('o')
    GameAssemblySHA256 = (Get-FileHash -LiteralPath (Join-Path $managed 'Assembly-CSharp.dll') -Algorithm SHA256).Hash
    Files = $hashes
}
$receipt | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $receiptPath -Encoding UTF8
$report = @"
Target: $ModFolder
$($scanOutput -join "`n")
Version: $version
DLL SHA256: $($hashes['ChemAssistPlus.dll'])
Automated tests: NOT RUN
In-game validation: pending player feedback
"@
$report | Set-Content -LiteralPath (Join-Path $ModFolder '验证结果.txt') -Encoding UTF8
Write-Output $report
