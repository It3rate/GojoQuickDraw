param([string]$Dataset = '')
$ErrorActionPreference = 'Stop'
$quickDrawProject = Join-Path $PSScriptRoot 'desktop/GojoQuickDraw.Desktop.csproj'
& dotnet build $quickDrawProject --configuration Release --verbosity quiet
if ($LASTEXITCODE -ne 0) {
    Read-Host 'The desktop app could not be built. Press Enter to close'
    exit $LASTEXITCODE
}
$quickDrawApp = Join-Path $PSScriptRoot 'desktop/bin/Release/net10.0/GojoQuickDraw.Desktop.exe'
$quickDrawArguments = @()
if ($Dataset) { $quickDrawArguments = @('--dataset', ('"' + [IO.Path]::GetFullPath($Dataset) + '"')) }
if ($quickDrawArguments.Count) {
    Start-Process -FilePath $quickDrawApp -WorkingDirectory $PSScriptRoot -ArgumentList $quickDrawArguments
} else {
    Start-Process -FilePath $quickDrawApp -WorkingDirectory $PSScriptRoot
}
