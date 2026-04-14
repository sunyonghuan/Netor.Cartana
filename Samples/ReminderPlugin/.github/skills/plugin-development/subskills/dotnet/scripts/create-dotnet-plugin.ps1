param(
    [Parameter(ValueFromRemainingArguments = $true)][object[]]$Args
)

$scriptPath = Join-Path $PSScriptRoot '..\..\..\scripts\create-dotnet-plugin.ps1'
& $scriptPath @Args
exit $LASTEXITCODE