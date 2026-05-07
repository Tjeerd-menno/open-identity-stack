param(
    [Parameter(Mandatory = $true)]
    [string]$BaseRef,

    [string]$OasdiffImage = "tufin/oasdiff:v1.15.0",

    [switch]$AllowExternalRefs
)

$ErrorActionPreference = "Stop"

function Get-OpenApiVersion {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $inInfo = $false
    foreach ($line in Get-Content -Path $Path) {
        if ($line -match "^info:\s*$") {
            $inInfo = $true
            continue
        }

        if ($inInfo -and $line -match "^\S") {
            break
        }

        if ($inInfo -and $line -match "^\s+version:\s*['""]([^'""]+)['""]\s*(?:#.*)?$") {
            return $Matches[1].Trim()
        }

        if ($inInfo -and $line -match "^\s+version:\s*([^#\s]+)\s*(?:#.*)?$") {
            return $Matches[1].Trim()
        }
    }

    throw "OpenAPI document '$Path' is missing info.version."
}

function Test-OpenApiDocument {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    return (Select-String -Path $Path -Pattern "^openapi:\s*" -Quiet)
}

$repoRoot = (git rev-parse --show-toplevel).Trim()
Set-Location $repoRoot

$allowExternalRefsValue = $AllowExternalRefs.IsPresent.ToString().ToLowerInvariant()
$baseTempRoot = Join-Path ([System.IO.Path]::GetTempPath()) "openapi-base-$([System.Guid]::NewGuid())"
New-Item -ItemType Directory -Path $baseTempRoot | Out-Null

try {
    $currentSpecs = Get-ChildItem -Path (Join-Path $repoRoot "specs") -Recurse -File -Include *.yaml, *.yml -ErrorAction SilentlyContinue |
        ForEach-Object { [System.IO.Path]::GetRelativePath($repoRoot, $_.FullName).Replace("\", "/") } |
        Where-Object { $_ -match "^specs/.+/contracts/.+\.ya?ml$" -and (Test-OpenApiDocument -Path (Join-Path $repoRoot $_)) }

    $baseTreeEntries = git ls-tree -r --name-only $BaseRef specs
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to list OpenAPI specs from base ref '$BaseRef'. Ensure the workflow fetched the base branch before running this script."
    }

    $baseCandidates = $baseTreeEntries |
        Where-Object { $_ -match "^specs/.+/contracts/.+\.ya?ml$" }

    $baseSpecs = @()
    foreach ($path in $baseCandidates) {
        $basePath = Join-Path $baseTempRoot $path
        New-Item -ItemType Directory -Path (Split-Path -Path $basePath -Parent) -Force | Out-Null
        $baseContent = git show "${BaseRef}:$path" 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "Unable to read OpenAPI spec '$path' from base ref '$BaseRef': $baseContent"
        }

        $baseContentText = if ($baseContent -is [System.Array]) {
            [string]::Join("`n", $baseContent)
        }
        else {
            [string]$baseContent
        }

        [System.IO.File]::WriteAllText(
            $basePath,
            $baseContentText,
            [System.Text.UTF8Encoding]::new($false))
        if (Test-OpenApiDocument -Path $basePath) {
            $baseSpecs += $path
        }
    }

    $allSpecs = @($currentSpecs + $baseSpecs) | Sort-Object -Unique
    if ($allSpecs.Count -eq 0) {
        Write-Host "No OpenAPI contract specs found under specs/**/contracts."
        exit 0
    }

    $hasFailures = $false
    foreach ($specPath in $allSpecs) {
        $currentPath = Join-Path $repoRoot $specPath
        $basePath = Join-Path $baseTempRoot $specPath
        $currentExists = Test-Path -Path $currentPath
        $baseExists = Test-Path -Path $basePath

        if (-not $baseExists) {
            Write-Host "Skipping new OpenAPI spec '$specPath'; no base version exists on $BaseRef."
            continue
        }

        if (-not $currentExists) {
            Write-Host "::error file=$specPath::Breaking change: OpenAPI spec '$specPath' exists on $BaseRef but was removed in this branch. Spec removal is treated as a breaking API contract change."
            $hasFailures = $true
            continue
        }

        $baseVersion = Get-OpenApiVersion -Path $basePath
        $currentVersion = Get-OpenApiVersion -Path $currentPath

        if ($baseVersion -ne $currentVersion) {
            Write-Host "Skipping '$specPath'; API version changed from '$baseVersion' to '$currentVersion'."
            continue
        }

        Write-Host "Checking '$specPath' for breaking changes at API version '$currentVersion'."
        $specsRoot = Join-Path $repoRoot "specs"
        docker run --rm `
            -v "${baseTempRoot}:/base:ro" `
            -v "${specsRoot}:/workspace/specs:ro" `
            $OasdiffImage `
            breaking --fail-on ERR --format githubactions "--allow-external-refs=$allowExternalRefsValue" "/base/$specPath" "/workspace/$specPath"

        if ($LASTEXITCODE -ne 0) {
            $hasFailures = $true
        }
    }

    if ($hasFailures) {
        exit 1
    }
}
finally {
    Remove-Item -Path $baseTempRoot -Recurse -Force -ErrorAction SilentlyContinue
}
