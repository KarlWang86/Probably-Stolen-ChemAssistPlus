# Explicitly publish prepared content and synchronize english/schinese. Never starts the game.
param([switch]$CheckOnly, [switch]$SchineseOnly, [string[]]$AdditionalPreview = @())
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'scripts/common.ps1')
if ($AdditionalPreview.Count -gt 0 -and ($CheckOnly -or $SchineseOnly)) { throw 'Additional previews require a full publish.' }
$previewPaths = @($AdditionalPreview | ForEach-Object { (Resolve-Path -LiteralPath $_ -ErrorAction Stop).ProviderPath })
$workspace = Join-Path $gameRoot 'WorkshopUpload'
$version = Get-TaskVersion
$receipt = Get-Content -LiteralPath (Join-Path $buildFolder 'validation.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if ($receipt.Version -ne $version) { throw 'Build validation version differs from VERSION.' }
foreach ($file in @('ChemAssistPlus.dll','manifest.xml','使用说明.txt')) {
    $prepared = (Get-FileHash -LiteralPath (Join-Path $workspace "content/$file") -Algorithm SHA256).Hash
    $built = (Get-FileHash -LiteralPath (Join-Path $buildFolder $file) -Algorithm SHA256).Hash
    if ($prepared -ne $receipt.Files.$file -or $built -ne $prepared) { throw "Prepared/build/validated content differs: $file" }
}
& (Join-Path $workspace 'upload.ps1') -CheckOnly
if ($LASTEXITCODE -ne 0) { throw 'Workshop preflight failed.' }
$toolOutput = Join-Path $buildFolder 'tools/WorkshopPublisher'
& dotnet build (Join-Path $taskProject 'Tools/WorkshopPublisher/WorkshopPublisher.csproj') -c Release -o $toolOutput --ignore-failed-sources -p:NuGetAudit=false "-p:RestoreConfigFile=$taskProject/NuGet.Config"
if ($LASTEXITCODE -ne 0) { throw 'WorkshopPublisher build failed.' }
$mode = if ($CheckOnly) { '--check' } elseif ($SchineseOnly) { '--schinese-only' } else { '--publish' }
& dotnet (Join-Path $toolOutput 'WorkshopPublisher.dll') $workspace $mode @previewPaths
if ($LASTEXITCODE -ne 0) { throw 'Workshop publish/check incomplete; inspect language receipt before retrying.' }
