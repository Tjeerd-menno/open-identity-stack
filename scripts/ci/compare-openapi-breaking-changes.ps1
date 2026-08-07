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

function Get-OpenApiContractKey {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if ($Path -match "^specs/(.+)/contracts/(.+\.ya?ml)$") {
        return "$($Matches[1])/$($Matches[2])"
    }

    if ($Path -match "^contracts/openapi/(.+\.ya?ml)$") {
        return $Matches[1]
    }

    return $null
}

function Get-RepositoryRelativePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Root,

        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $normalizedRoot = [System.IO.Path]::GetFullPath($Root)
    $normalizedPath = [System.IO.Path]::GetFullPath($Path)
    $rootWithSeparator = $normalizedRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    if (-not $normalizedPath.StartsWith($rootWithSeparator, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Path '$Path' is not under repository root '$Root'."
    }

    return $normalizedPath.Substring($rootWithSeparator.Length).Replace("\", "/")
}

$repoRoot = (git rev-parse --show-toplevel).Trim()
Set-Location $repoRoot

$allowExternalRefsValue = $AllowExternalRefs.IsPresent.ToString().ToLowerInvariant()
$baseTempRoot = Join-Path ([System.IO.Path]::GetTempPath()) "openapi-base-$([System.Guid]::NewGuid())"
New-Item -ItemType Directory -Path $baseTempRoot | Out-Null

try {
    $currentSpecsByKey = @{}
    Get-ChildItem -Path (Join-Path $repoRoot "specs"), (Join-Path $repoRoot "contracts/openapi") -Recurse -File -Include *.yaml, *.yml -ErrorAction SilentlyContinue |
        ForEach-Object {
            $relativePath = Get-RepositoryRelativePath -Root $repoRoot -Path $_.FullName
            $key = Get-OpenApiContractKey -Path $relativePath
            if ($null -ne $key -and (Test-OpenApiDocument -Path $_.FullName)) {
                $currentSpecsByKey[$key] = $relativePath
            }
        }

    $baseTreeEntries = git ls-tree -r --name-only $BaseRef specs contracts/openapi 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to list OpenAPI specs from base ref '$BaseRef'. Ensure the workflow fetched the base branch before running this script."
    }

    $baseSpecsByKey = @{}
    foreach ($path in $baseTreeEntries) {
        $key = Get-OpenApiContractKey -Path $path
        if ($null -eq $key) {
            continue
        }

        $basePath = Join-Path $baseTempRoot $key
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
            $baseSpecsByKey[$key] = $path
        }
    }

    $allSpecs = @($currentSpecsByKey.Keys + $baseSpecsByKey.Keys) | Sort-Object -Unique
    if ($allSpecs.Count -eq 0) {
        Write-Host "No OpenAPI contract specs found under contracts/openapi or specs/**/contracts."
        exit 0
    }

    $hasFailures = $false
    foreach ($specKey in $allSpecs) {
        $currentSpecPath = if ($currentSpecsByKey.ContainsKey($specKey)) { $currentSpecsByKey[$specKey] } else { $null }
        $baseSpecPath = if ($baseSpecsByKey.ContainsKey($specKey)) { $baseSpecsByKey[$specKey] } else { $null }
        $currentPath = if ($null -ne $currentSpecPath) { Join-Path $repoRoot $currentSpecPath } else { $null }
        $basePath = Join-Path $baseTempRoot $specKey
        $currentExists = $null -ne $currentPath -and (Test-Path -Path $currentPath)
        $baseExists = Test-Path -Path $basePath

        if (-not $baseExists) {
            Write-Host "Skipping new OpenAPI spec '$currentSpecPath'; no base version exists on $BaseRef."
            continue
        }

        if (-not $currentExists) {
            Write-Host "::error file=$baseSpecPath::Breaking change: OpenAPI spec '$baseSpecPath' exists on $BaseRef but was removed in this branch. Spec removal is treated as a breaking API contract change."
            $hasFailures = $true
            continue
        }

        $baseVersion = Get-OpenApiVersion -Path $basePath
        $currentVersion = Get-OpenApiVersion -Path $currentPath

        if ($baseVersion -ne $currentVersion) {
            Write-Host "Skipping '$currentSpecPath'; API version changed from '$baseVersion' to '$currentVersion'."
            continue
        }

        Write-Host "Checking '$currentSpecPath' for breaking changes at API version '$currentVersion'."
        docker run --rm `
            -v "${baseTempRoot}:/base:ro" `
            -v "${repoRoot}:/workspace:ro" `
            $OasdiffImage `
            breaking --fail-on ERR --format githubactions "--allow-external-refs=$allowExternalRefsValue" "/base/$specKey" "/workspace/$currentSpecPath"

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
