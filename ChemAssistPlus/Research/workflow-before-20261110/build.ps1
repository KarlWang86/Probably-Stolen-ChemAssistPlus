# 开发→编译→复制到 Mods 运行目录→原生安全扫描。按用户要求不自动运行测试。
# 注意：此脚本会更新运行 DLL；不会创建安装 zip，也不会启动游戏或修改存档。
$ErrorActionPreference = 'Stop'
$taskProject = $PSScriptRoot
$gameRoot = [IO.Path]::GetFullPath((Join-Path $taskProject '../..'))
$destination = Join-Path $gameRoot 'Mods/ChemicalWorkflowAssistant'
dotnet build (Join-Path $taskProject 'ChemAssistPlus.csproj') -c Release --ignore-failed-sources -p:NuGetAudit=false
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
New-Item -ItemType Directory -Path $destination -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $taskProject 'bin/Release/netstandard2.1/ChemAssistPlus.dll') -Destination $destination
Copy-Item -LiteralPath (Join-Path $taskProject 'manifest.xml') -Destination $destination
Copy-Item -LiteralPath (Join-Path $taskProject '使用说明.txt') -Destination $destination
& (Join-Path $taskProject 'validate.ps1')
Write-Output "Installed: $destination"

