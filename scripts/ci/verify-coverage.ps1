param(
    [Parameter(Mandatory = $false)]
    [string]$ResultsDir = "TestResults",

    [Parameter(Mandatory = $false)]
    [double]$MinimumLineRate = 0.80,

    [Parameter(Mandatory = $false)]
    [double]$MinimumOverallLineRate = 0.80
)

$coverageFiles = Get-ChildItem -Path $ResultsDir -Filter "*.cobertura.xml" -Recurse -ErrorAction SilentlyContinue
if (-not $coverageFiles) {
    throw "No coverage reports found in '$ResultsDir'."
}

$failed = @()
$uniqueLineCoveredByKey = @{}

foreach ($file in $coverageFiles) {
    [xml]$xml = Get-Content -Path $file.FullName
    $lineRate = [double]$xml.coverage.'line-rate'

    foreach ($class in $xml.coverage.packages.package.classes.class) {
        $sourceFileIdentifier = if ($class.filename) { $class.filename } else { $class.name }

        if ($class.lines -and $class.lines.line) {
            foreach ($line in $class.lines.line) {
                $key = "$sourceFileIdentifier|$($line.number)"
                $isCovered = ([int]$line.hits) -gt 0

                if (-not $uniqueLineCoveredByKey.ContainsKey($key)) {
                    $uniqueLineCoveredByKey[$key] = $isCovered
                } elseif ($isCovered) {
                    $uniqueLineCoveredByKey[$key] = $true
                }
            }
        }
    }

    $percent = [math]::Round($lineRate * 100, 2)
    Write-Host "$($file.Name): $percent%"

    # Check for a per-project threshold override.
    # Convention: a '.coverage-threshold' file in the test project's root directory
    # (search upward from the coverage file's directory until the root of $ResultsDir)
    # overrides the global MinimumLineRate for that project.
    $effectiveThreshold = $MinimumLineRate
    $searchDir = $file.Directory
    $resultsDirInfo = Get-Item $ResultsDir
    while ($null -ne $searchDir -and $searchDir.FullName -ne $resultsDirInfo.FullName -and $searchDir.FullName.StartsWith($resultsDirInfo.FullName)) {
        $overrideFile = Join-Path $searchDir.FullName ".coverage-threshold"
        if (Test-Path $overrideFile) {
            $overrideValue = Get-Content $overrideFile -Raw
            $parsedOverride = 0.0
            if ([double]::TryParse($overrideValue.Trim(), [ref]$parsedOverride)) {
                $effectiveThreshold = $parsedOverride
                Write-Host "  (using per-project threshold: $([math]::Round($effectiveThreshold * 100, 2))%)"
            }
            break
        }
        $searchDir = $searchDir.Parent
    }

    if ($lineRate -lt $effectiveThreshold) {
        $failed += "$($file.Name) ($percent%)"
    }
}

$totalLinesValid = $uniqueLineCoveredByKey.Count
$totalLinesCovered = ($uniqueLineCoveredByKey.Values | Where-Object { $_ } | Measure-Object).Count
$overall = if ($totalLinesValid -gt 0) { $totalLinesCovered / $totalLinesValid } else { 0 }
$overallPercent = [math]::Round($overall * 100, 2)
Write-Host "Overall line coverage: $overallPercent%"

if ($overall -lt $MinimumOverallLineRate) {
    throw "Overall line coverage $overallPercent% is below $([math]::Round($MinimumOverallLineRate * 100, 2))%."
}

if ($failed.Count -gt 0) {
    throw "Coverage below threshold ($([math]::Round($MinimumLineRate * 100, 2))%): $($failed -join ', ')"
}
