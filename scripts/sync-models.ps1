<#
.SYNOPSIS
    Checks the local OpenAI model catalog against the gateway's costing catalog
    and reports any drift (models present in one but missing in the other).

.DESCRIPTION
    Parses the gateway's src/lib/costing.ts to extract the model IDs from the
    pricingCatalog object, then compares against config/providers/openai.models.json.

    Snapshot aliases (model IDs containing a date like -2025-08-07) are excluded
    from drift checks since the local catalog uses stable names only.

    Reports:
    - Models in gateway but missing from local catalog
    - Models in local catalog but missing from gateway
    - Models present in both

.PARAMETER GatewayPath
    Path to the openai-api-service repo root. Defaults to sibling directory.
#>
param(
    [string]$GatewayPath = (Join-Path $PSScriptRoot "..\..\openai-api-service")
)

$ErrorActionPreference = "Stop"

$costingFile = Join-Path $GatewayPath "src/lib/costing.ts"
$modelsFile = Join-Path $PSScriptRoot "..\config\providers\openai.models.json"

if (-not (Test-Path $costingFile)) {
    Write-Error "Gateway costing file not found at: $costingFile"
    exit 1
}
if (-not (Test-Path $modelsFile)) {
    Write-Error "Local models file not found at: $modelsFile"
    exit 1
}

# Extract model IDs from costing.ts pricingCatalog keys
$costingContent = Get-Content $costingFile -Raw
$gatewayModelsAll = [System.Collections.Generic.List[string]]::new()
$matches = [regex]::Matches($costingContent, '"([^"]+)":\s*\{\s*inputPerMillion')
foreach ($m in $matches) {
    $gatewayModelsAll.Add($m.Groups[1].Value)
}

if ($gatewayModelsAll.Count -eq 0) {
    Write-Error "No models found in gateway costing file. Check the regex or file format."
    exit 1
}

# Filter out snapshot aliases (contain a date pattern like -YYYY-MM-DD)
$snapshotPattern = '-\d{4}-\d{2}-\d{2}'
$gatewayModels = $gatewayModelsAll | Where-Object { $_ -notmatch $snapshotPattern }
$skippedSnapshots = $gatewayModelsAll | Where-Object { $_ -match $snapshotPattern }

if ($gatewayModels.Count -eq 0) {
    Write-Error "No models found in gateway costing file. Check the regex or file format."
    exit 1
}

# Extract model IDs from openai.models.json
$modelsJson = Get-Content $modelsFile -Raw | ConvertFrom-Json
$localModels = [System.Collections.Generic.List[string]]::new()
foreach ($model in $modelsJson.models) {
    $localModels.Add($model.modelId)
}

# Compare
$gatewaySet = [System.Collections.Generic.HashSet[string]]::new([string[]]@($gatewayModels))
$localSet = [System.Collections.Generic.HashSet[string]]::new([string[]]@($localModels))

$missingFromLocal = $gatewayModels | Where-Object { -not $localSet.Contains($_) }
$missingFromGateway = $localModels | Where-Object { -not $gatewaySet.Contains($_) }
$inBoth = $localModels | Where-Object { $gatewaySet.Contains($_) }

Write-Host ""
Write-Host "=== Model Catalog Drift Report ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "Gateway models ($($gatewayModels.Count) stable, $($skippedSnapshots.Count) snapshot aliases skipped):" -ForegroundColor White
$gatewayModels | ForEach-Object { Write-Host "  - $_" }
if ($skippedSnapshots.Count -gt 0) {
    $skippedSnapshots | ForEach-Object { Write-Host "  - $_ (snapshot, skipped)" -ForegroundColor DarkGray }
}

Write-Host ""
Write-Host "Local models ($($localModels.Count)):" -ForegroundColor White
$localModels | ForEach-Object { Write-Host "  - $_" }

Write-Host ""

if ($inBoth.Count -gt 0) {
    Write-Host "In sync ($($inBoth.Count)):" -ForegroundColor Green
    $inBoth | ForEach-Object { Write-Host "  - $_" -ForegroundColor Green }
}

$driftFound = $false

if ($missingFromLocal.Count -gt 0) {
    Write-Host ""
    Write-Host "Missing from local catalog ($($missingFromLocal.Count)):" -ForegroundColor Yellow
    $missingFromLocal | ForEach-Object { Write-Host "  - $_" -ForegroundColor Yellow }
    $driftFound = $true
}

if ($missingFromGateway.Count -gt 0) {
    Write-Host ""
    Write-Host "In local catalog but not in gateway ($($missingFromGateway.Count)):" -ForegroundColor Yellow
    $missingFromGateway | ForEach-Object { Write-Host "  - $_" -ForegroundColor Yellow }
    $driftFound = $true
}

Write-Host ""
if ($driftFound) {
    Write-Host "DRIFT DETECTED — review and update config/providers/openai.models.json" -ForegroundColor Red
    exit 1
} else {
    Write-Host "No drift detected." -ForegroundColor Green
    exit 0
}
