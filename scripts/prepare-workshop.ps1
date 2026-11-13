# Prepare local upload files and formatted text. Never logs in, uploads, deploys, or starts the game.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')
$version = Get-TaskVersion
$workspace = Join-Path $gameRoot 'WorkshopUpload'
$vdfPath = Join-Path $workspace 'upload.vdf'
$receiptPath = Join-Path $buildFolder 'validation.json'
if (!(Test-Path -LiteralPath $receiptPath)) { throw 'Run build.ps1 successfully before preparing the upload.' }
$receipt = Get-Content -LiteralPath $receiptPath -Raw -Encoding UTF8 | ConvertFrom-Json
if ($receipt.Version -ne $version) { throw 'Build validation version differs from VERSION.' }
$runtimeFiles = @('ChemAssistPlus.dll','manifest.xml','使用说明.txt')
foreach ($file in $runtimeFiles) {
    if ((Get-FileHash -LiteralPath (Join-Path $buildFolder $file) -Algorithm SHA256).Hash -ne $receipt.Files.$file) {
        throw "Build file changed after validation: $file"
    }
}

$changelog = [IO.File]::ReadAllText((Join-Path $docsRoot 'CHANGELOG.md'))
$workshopChanges = [IO.File]::ReadAllText((Join-Path $docsRoot 'WorkshopChangelog.md'))
function Convert-ChangesToBBCode([string]$Markdown) {
    $lines = New-Object 'System.Collections.Generic.List[string]'
    $inList = $false
    $started = $false
    foreach ($line in ($Markdown -split '\r?\n')) {
        if ($line -match '^## (.+)$') {
            if ($inList) { $lines.Add('[/list]'); $inList = $false }
            $lines.Add(''); $lines.Add('[h3]' + $Matches[1] + '[/h3]'); $started = $true
        } elseif ($started -and $line -match '^- (.+)$') {
            if (!$inList) { $lines.Add('[list]'); $inList = $true }
            $lines.Add('[*]' + $Matches[1])
        }
    }
    if ($inList) { $lines.Add('[/list]') }
    return ($lines -join "`r`n").Trim()
}
$versionPattern = '(?ms)^## ' + [regex]::Escape($version) + '(?:[ \t]+[^\r\n]*)?\r?\n(.*?)(?=^## |\z)'
$currentMatches = [regex]::Matches($changelog, $versionPattern)
$briefMatches = [regex]::Matches($workshopChanges, $versionPattern)
if ($currentMatches.Count -ne 1 -or $briefMatches.Count -ne 1) { throw 'CHANGELOG.md and WorkshopChangelog.md must each contain exactly one current version section.' }
$current = $currentMatches[0]
$currentBrief = Convert-ChangesToBBCode $briefMatches[0].Value
if ($currentBrief -notmatch '\[\*\]') { throw 'Current workshop changes must contain at least one bullet.' }
$note = Convert-ChangesToBBCode ("## $version`r`n" + $current.Groups[1].Value)
$template = [IO.File]::ReadAllText((Join-Path $docsRoot 'WorkshopDescription.template.txt'))
$description = $template.Replace('{{VERSION}}', $version).Replace('{{CURRENT_CHANGES}}', $currentBrief).Trim()
$description = [regex]::Replace($description, '\r?\n', "`r`n")
if ($description.Contains('{{')) { throw 'Unresolved workshop description placeholder.' }
$descriptionBytes = [Text.Encoding]::UTF8.GetByteCount($description)
if ($descriptionBytes -ge 8000) { throw "Workshop description is $descriptionBytes UTF-8 bytes; Steam requires fewer than 8000. Shorten the workshop copy, not the full local CHANGELOG.md." }

function Quote-Vdf([string]$Text) { return '"' + $Text.Replace('\','\\').Replace('"','\"') + '"' }
$raw = [IO.File]::ReadAllText($vdfPath)
foreach ($key in @('description','changenote')) {
    $pattern = '("' + $key + '"\s+)"(?:[^"\\]|\\.)*"'
    if ([regex]::Matches($raw, $pattern).Count -ne 1) { throw "Expected exactly one $key field in upload.vdf." }
    $value = if ($key -eq 'description') { $description } else { $note }
    $raw = [regex]::Replace($raw, $pattern, [Text.RegularExpressions.MatchEvaluator]{ param($m) $m.Groups[1].Value + (Quote-Vdf $value) })
}

# Preserve previous prepared content and metadata before replacing the local upload workspace.
$archive = Join-Path $workspace ("archive/before-prepare-{0}-{1}" -f $version,(Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Path $archive -Force | Out-Null
foreach ($name in @('content','upload.vdf','创意工坊简介.txt','本次更新说明.txt','CHANGELOG-source.md','content-sha256.txt')) {
    $previous = Join-Path $workspace $name
    if (Test-Path -LiteralPath $previous) { Copy-Item -LiteralPath $previous -Destination $archive -Recurse }
}
Write-TaskFile (Join-Path $workspace '创意工坊简介.txt') ($description + "`r`n")
Write-TaskFile (Join-Path $workspace '本次更新说明.txt') ($note + "`r`n")
Write-TaskFile (Join-Path $workspace 'CHANGELOG-source.md') $changelog
Write-TaskFile $vdfPath $raw
$content = Join-Path $workspace 'content'
New-Item -ItemType Directory -Path $content -Force | Out-Null
$hashLines = foreach ($file in $runtimeFiles) {
    Copy-Item -LiteralPath (Join-Path $buildFolder $file) -Destination $content
    $actual = (Get-FileHash -LiteralPath (Join-Path $content $file) -Algorithm SHA256).Hash
    if ($actual -ne $receipt.Files.$file) { throw "Prepared content hash mismatch: $file" }
    "$actual  $file"
}
Write-TaskFile (Join-Path $workspace 'content-sha256.txt') (($hashLines -join "`r`n") + "`r`n")
Write-Output "Prepared version $version; description includes current changes only; full history archived separately."
Write-Output "Backup: $archive"
& (Join-Path $workspace 'upload.ps1') -CheckOnly
if ($LASTEXITCODE -ne 0) { throw 'Workshop configuration preflight failed.' }
