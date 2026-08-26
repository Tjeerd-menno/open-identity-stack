# Defaults mirror the values the CI workflows pass explicitly. Keep the two in step: a default
# that advertises a higher bar than CI enforces makes the gate look stricter than it is.
param(
    [Parameter(Mandatory = $false)]
    [string]$ResultsDir = "TestResults",

    [Parameter(Mandatory = $false)]
    [double]$MinimumLineRate = 0.10,

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

    if ($lineRate -lt $MinimumLineRate) {
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
