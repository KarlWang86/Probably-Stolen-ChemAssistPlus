# 日常构建：同步版本、编译到 build、原生扫描；不部署、不打包。
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')
Sync-TaskVersion
New-Item -ItemType Directory -Path $buildFolder -Force | Out-Null
# 先撤销旧验证凭据，失败构建不能沿用上次的 PASS。
$receipt = Join-Path $buildFolder 'validation.json'
if (Test-Path -LiteralPath $receipt) { Remove-Item -LiteralPath $receipt }
dotnet build (Join-Path $taskProject 'ChemAssistPlus.csproj') -c Release -o $buildFolder --ignore-failed-sources -p:NuGetAudit=false "-p:RestoreConfigFile=$repoRoot/NuGet.Config"
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Copy-Item -LiteralPath (Join-Path $taskProject 'manifest.xml') -Destination $buildFolder
Copy-Item -LiteralPath (Join-Path $docsRoot '使用说明.txt') -Destination $buildFolder
& (Join-Path $PSScriptRoot 'validate.ps1')
Write-Output "Built and scanned: $buildFolder"
