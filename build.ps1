param(
    [string]$OutDir = (Join-Path $PSScriptRoot 'bin')
)

$ErrorActionPreference = 'Stop'
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { throw "csc.exe not found: $csc" }
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$refRoot = Split-Path $csc
$refs = @(
    ('/r:' + (Join-Path $refRoot 'System.Windows.Forms.dll')),
    ('/r:' + (Join-Path $refRoot 'System.Drawing.dll'))
)
$sources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | ForEach-Object { $_.FullName })
$baseArgs = @('/nologo', '/platform:x64', '/optimize+') + $refs

$consoleOut = Join-Path $OutDir 'BoogieBoardSync.exe'
$consoleArgs = $baseArgs + @('/target:exe', '/main:BoogieBoardSync.Program', ('/out:' + $consoleOut)) + $sources
& $csc $consoleArgs
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$trayOut = Join-Path $OutDir 'BoogieBoardSyncTray.exe'
$trayArgs = $baseArgs + @('/target:winexe', '/main:BoogieBoardSync.TrayProgram', ('/out:' + $trayOut)) + $sources
& $csc $trayArgs
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Output ("Built: " + $consoleOut)
Write-Output ("Built: " + $trayOut)
