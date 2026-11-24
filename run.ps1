param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$ProgramArgs
)
$exe = Join-Path $PSScriptRoot 'bin\BoogieBoardSync.exe'
if (-not (Test-Path $exe)) {
    & (Join-Path $PSScriptRoot 'build.ps1')
}
& $exe @ProgramArgs
