# 默认只返回匹配类型/方法的签名；-IL 保存指定方法的完整 IL。
param(
    [Parameter(Mandatory=$true)][string]$Type,
    [Parameter(Mandatory=$true)][string]$Method,
    [string]$Assembly,
    [switch]$IL
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'common.ps1')
if (!$Assembly) { $Assembly = Join-Path $managed 'Assembly-CSharp.dll' }
$toolOutput = Join-Path $buildFolder 'tools/AssemblyQuery'
$buildLog = & dotnet build (Join-Path $toolsRoot 'AssemblyQuery/AssemblyQuery.csproj') -c Release -o $toolOutput --ignore-failed-sources -p:NuGetAudit=false "-p:RestoreConfigFile=$repoRoot/NuGet.Config"
if ($LASTEXITCODE -ne 0) { throw ($buildLog -join "`n") }
$queryArgs = @($Assembly,$Type,$Method)
if ($IL) { $queryArgs += '--il' }
$result = & dotnet (Join-Path $toolOutput 'AssemblyQuery.dll') @queryArgs
if ($LASTEXITCODE -ne 0) { throw 'Assembly query failed.' }
$research = Join-Path $repoRoot 'Research'
New-Item -ItemType Directory -Path $research -Force | Out-Null
$kind = if ($IL) { 'il' } else { 'api' }
$safe = ("{0}-{1}-{2}" -f [IO.Path]::GetFileNameWithoutExtension($Assembly),$Type,$Method) -replace '[^\w.-]','_'
$output = Join-Path $research "$safe-$kind.txt"
$result | Set-Content -LiteralPath $output -Encoding UTF8
if ($IL) { Write-Output "IL saved: $output" } else { Write-Output $result; Write-Output "Saved: $output" }
