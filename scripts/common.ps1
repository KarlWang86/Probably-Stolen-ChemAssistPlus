$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskProject = Join-Path $repoRoot 'ChemAssistPlus'
$docsRoot = Join-Path $repoRoot 'docs'
$toolsRoot = Join-Path $repoRoot 'Tools'
$gameRoot = $env:PROBABLY_STOLEN_GAME_ROOT
$localGameRoot = Join-Path $repoRoot 'GameRoot.local.txt'
if (!$gameRoot -and (Test-Path -LiteralPath $localGameRoot)) {
    $gameRoot = [IO.File]::ReadAllText($localGameRoot).Trim()
}
if (!$gameRoot) { $gameRoot = Join-Path $repoRoot '..' }
$gameRoot = [IO.Path]::GetFullPath($gameRoot)
if (!(Test-Path -LiteralPath (Join-Path $gameRoot 'Probably Stolen_Data/Managed/Assembly-CSharp.dll'))) {
    throw 'Game not found. Set PROBABLY_STOLEN_GAME_ROOT or write the game directory to GameRoot.local.txt.'
}
$buildFolder = Join-Path $repoRoot 'build'
$managed = Join-Path $gameRoot 'Probably Stolen_Data/Managed'
$utf8Bom = New-Object System.Text.UTF8Encoding($true)
function Write-TaskFile([string]$Path, [string]$Text) {
    if (!(Test-Path -LiteralPath $Path) -or [IO.File]::ReadAllText($Path) -cne $Text) {
        [IO.File]::WriteAllText($Path, $Text, $utf8Bom)
    }
}
function Get-TaskVersion {
    $v = [IO.File]::ReadAllText((Join-Path $taskProject 'VERSION')).Trim()
    if ($v -notmatch '^\d+\.\d+\.\d+$') { throw 'VERSION must contain major.minor.patch.' }
    return $v
}
function Sync-TaskVersion {
    $v = Get-TaskVersion
    $path = Join-Path $taskProject 'manifest.xml'
    $text = [IO.File]::ReadAllText($path)
    $text = [regex]::Replace($text, '<ModVersion>[^<]+</ModVersion>', "<ModVersion>$v</ModVersion>")
    $text = [regex]::Replace($text, '(化学流程助手)\d+\.\d+\.\d+', ('${1}' + $v))
    Write-TaskFile $path $text
    $path = Join-Path $docsRoot '使用说明.txt'
    $text = [IO.File]::ReadAllText($path)
    $text = [regex]::Replace($text, '(化学流程助手 )\d+\.\d+\.\d+', ('${1}' + $v))
    $text = [regex]::Replace($text, '(标题应为)\d+\.\d+\.\d+', ('${1}' + $v))
    Write-TaskFile $path $text
}
