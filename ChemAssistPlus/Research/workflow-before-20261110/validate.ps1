# 离线检查实际 Mods DLL、独立清单和原 Mod 哈希；不等价于游戏加载/完整制作测试。
# ModScanner 的游戏引用参数必须是程序集 BaseName，不能传完整文件路径。
$ErrorActionPreference = 'Stop'
$gameRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$managed = Join-Path $gameRoot 'Probably Stolen_Data/Managed'
$modFolder = Join-Path $gameRoot 'Mods/ChemicalWorkflowAssistant'
$scannerProject = Join-Path $PSScriptRoot 'Tools/NativeScan/NativeScan.csproj'
dotnet build $scannerProject -c Release --ignore-failed-sources -p:NuGetAudit=false
if ($LASTEXITCODE -ne 0) { throw 'Native scanner build failed.' }
$scanOutput = & dotnet (Join-Path $PSScriptRoot 'Tools/NativeScan/bin/Release/net8.0/NativeScan.dll') $managed $modFolder
if ($LASTEXITCODE -ne 0) { throw 'Native mod scan failed.' }
$manifest = [xml](Get-Content -LiteralPath (Join-Path $modFolder 'manifest.xml') -Raw -Encoding UTF8)
if ($manifest.Manifest.ID -ne 'chemical_workflow_assistant') { throw 'Incorrect independent mod ID.' }
if (@($manifest.Manifest.Assemblies.Assembly).Count -ne 1 -or $manifest.Manifest.Assemblies.Assembly -ne 'ChemAssistPlus.dll') { throw 'Unexpected packaged assembly.' }
if ($manifest.Manifest.Prerequisites.HasChildNodes) { throw 'Unexpected prerequisite.' }
$original = Join-Path $gameRoot '../../workshop/content/4348910/3830072589/OneClickCraft.dll'
$originalStatus = 'Not present'
if (Test-Path -LiteralPath $original) {
    $originalStatus = (Get-FileHash -LiteralPath $original -Algorithm SHA256).Hash
    if ($originalStatus -ne 'CB4623B78622F869AFBE16122C719C195DA696ABC9FC2708C8BF82905DE8E826') {
        Write-Warning 'The subscribed mod changed since the research snapshot (possibly a Workshop update).'
    }
}
$report = @"
Validation against the installed game, $(Get-Date -Format yyyy-MM-dd)
$($scanOutput -join "`n")
Independent manifest ID: chemical_workflow_assistant
Prerequisite mods: none
Packaged assemblies: ChemAssistPlus.dll only
Subscribed original DLL SHA256: $originalStatus
Automated tests: NOT RUN (user-requested log-driven workflow)
Game-save end-to-end test: NOT YET RUN
"@
$report | Set-Content -LiteralPath (Join-Path $PSScriptRoot '验证结果.txt') -Encoding utf8
Write-Output $report

