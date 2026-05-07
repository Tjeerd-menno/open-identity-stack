# Code Coverage Script for OpenIdentityStack (PowerShell)
# Runs tests with coverage and generates HTML reports

$ErrorActionPreference = "Continue"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Split-Path -Parent $ScriptDir
$CoverageDir = Join-Path $RepoRoot "coverage-report"
$CoverageSettings = Join-Path $RepoRoot "coverage.settings.xml"

Write-Host "========================================" -ForegroundColor Green
Write-Host "OpenIdentityStack Code Coverage Analysis" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""

# Clean previous coverage data
Write-Host "Cleaning previous coverage data..." -ForegroundColor Yellow
if (Test-Path $CoverageDir) {
    Remove-Item -Path $CoverageDir -Recurse -Force
}
Get-ChildItem -Path "$RepoRoot/tests" -Filter "coverage.cobertura.xml" -Recurse | Remove-Item -Force
Get-ChildItem -Path "$RepoRoot/tests" -Filter "TestResults" -Directory -Recurse | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

# Run tests with coverage
Write-Host "Running tests with code coverage..." -ForegroundColor Yellow
Set-Location $RepoRoot

# Check if reportgenerator is installed
$reportGenInstalled = dotnet tool list --global | Select-String "dotnet-reportgenerator-globaltool"
if (-not $reportGenInstalled) {
    Write-Host "Installing ReportGenerator..." -ForegroundColor Yellow
    dotnet tool install --global dotnet-reportgenerator-globaltool
}

# Run tests
dotnet test `
    --coverage `
    --coverage-output-format cobertura `
    --coverage-output coverage.cobertura.xml `
    --coverage-settings "$CoverageSettings" `
    --verbosity quiet

$TestResult = $LASTEXITCODE

if ($TestResult -ne 0 -and $TestResult -ne 2) {
    Write-Host "Tests failed with exit code $TestResult" -ForegroundColor Red
    # Continue to generate report even if tests fail (exit code 2 means test failures but coverage was generated)
}

# Generate coverage report
Write-Host "Generating coverage report..." -ForegroundColor Yellow
reportgenerator `
    -reports:"$RepoRoot/tests/**/TestResults/coverage.cobertura.xml" `
    -targetdir:"$CoverageDir" `
    -reporttypes:"Html;TextSummary;MarkdownSummary;Badges" `
    -verbosity:"Warning"

# Display summary
Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "Coverage Summary" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Get-Content "$CoverageDir/Summary.txt" | Select-Object -First 20

# Generate badge files
Write-Host ""
Write-Host "Badge files generated:" -ForegroundColor Green
Get-ChildItem "$CoverageDir/*.svg" -ErrorAction SilentlyContinue | ForEach-Object { Write-Host "  $($_.Name)" }

# Open report
Write-Host ""
Write-Host "Coverage report generated at:" -ForegroundColor Green
Write-Host "  $CoverageDir/index.html" -ForegroundColor Yellow
Write-Host ""

Write-Host "Opening coverage report in browser..." -ForegroundColor Yellow
$reportPath = Join-Path $CoverageDir "index.html"
Start-Process $reportPath

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "Analysis complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green

exit 0
