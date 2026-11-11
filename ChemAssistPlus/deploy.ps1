# 专项游戏验证时显式运行；必须说明已核对的同类工坊状态。
param(
    [Parameter(Mandatory=$true)]
    [ValidateSet('Disabled','SameIdLocalPriorityConfirmed')]
    [string]$WorkshopState
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'scripts/common.ps1')
# 重新扫描并核对 build，杜绝复制未通过扫描或被修改的产物。
& (Join-Path $taskProject 'validate.ps1')
$destination = Join-Path $gameRoot 'Mods/ChemicalWorkflowAssistant'
New-Item -ItemType Directory -Path $destination -Force | Out-Null
foreach ($file in @('ChemAssistPlus.dll','manifest.xml','使用说明.txt')) {
    Copy-Item -LiteralPath (Join-Path $buildFolder $file) -Destination $destination -ErrorAction Stop
    if ((Get-FileHash -LiteralPath (Join-Path $buildFolder $file)).Hash -ne (Get-FileHash -LiteralPath (Join-Path $destination $file)).Hash) { throw "Deployed hash mismatch: $file" }
}
Write-Output "Deployed: $destination; Workshop check: $WorkshopState"
