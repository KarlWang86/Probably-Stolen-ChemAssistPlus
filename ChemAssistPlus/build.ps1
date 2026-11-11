# 日常构建：同步版本、编译到 build、原生扫描；不部署、不打包。
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'scripts/common.ps1')
Sync-TaskVersion
New-Item -ItemType Directory -Path $buildFolder -Force | Out-Null
# 先撤销旧验证凭据，失败构建不能沿用上次的 PASS。
$receipt = Join-Path $buildFolder 'validation.json'
if (Test-Path -LiteralPath $receipt) { Remove-Item -LiteralPath $receipt }
dotnet build (Join-Path $taskProject 'ChemAssistPlus.csproj') -c Release -o $buildFolder --ignore-failed-sources -p:NuGetAudit=false "-p:RestoreConfigFile=$taskProject/NuGet.Config"
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
foreach ($file in @('manifest.xml','使用说明.txt')) {
    Copy-Item -LiteralPath (Join-Path $taskProject $file) -Destination $buildFolder
}
& (Join-Path $taskProject 'validate.ps1')
Write-Output "Built and scanned: $buildFolder"
