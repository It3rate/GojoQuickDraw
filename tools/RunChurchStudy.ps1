param(
    [Parameter(Mandatory=$true)][string]$Output,
    [string]$InputFile='D:/_quickDraw/quickdraw/church.ndjson',
    [int]$Limit=1024,
    [int]$Workers=8,
    [int]$Passes=6,
    [int]$Seed=291043
)
$ErrorActionPreference='Stop'
$studyRepository=Split-Path -Parent $PSScriptRoot
Push-Location -LiteralPath $studyRepository
try {
    & python -m gojo_quickdraw.church_study run --input $InputFile --output $Output --limit $Limit --workers $Workers --passes $Passes --steps 1 --candidates 48 --seed $Seed
    if($LASTEXITCODE -ne 0){throw 'Church computation stopped; retained checkpoints can be resumed with the same command.'}
    & dotnet run --project tools/church-audit/ChurchAudit.csproj -c Release -- seal $Output 10000 $Workers
    if($LASTEXITCODE -ne 0){throw 'Native boundary import stopped; completed numerical results are retained.'}
    Write-Output (Join-Path $Output 'review.html')
}
finally {Pop-Location}
