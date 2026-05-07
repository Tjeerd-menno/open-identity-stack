#!/bin/bash
set -e

# Code Coverage Script for OpenIdentityStack
# Runs tests with coverage and generates HTML reports

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
COVERAGE_DIR="$REPO_ROOT/coverage-report"
COVERAGE_SETTINGS="$REPO_ROOT/coverage.settings.xml"

# Colors
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}OpenIdentityStack Code Coverage Analysis${NC}"
echo -e "${GREEN}========================================${NC}"
echo ""

# Clean previous coverage data
echo -e "${YELLOW}Cleaning previous coverage data...${NC}"
rm -rf "$COVERAGE_DIR"
find "$REPO_ROOT/tests" -name "coverage.cobertura.xml" -delete
find "$REPO_ROOT/tests" -type d -name "TestResults" -exec rm -rf {} + 2>/dev/null || true

# Run tests with coverage
echo -e "${YELLOW}Running tests with code coverage...${NC}"
cd "$REPO_ROOT"

# Check if reportgenerator is installed
if ! command -v reportgenerator &> /dev/null; then
    echo -e "${YELLOW}Installing ReportGenerator...${NC}"
    dotnet tool install --global dotnet-reportgenerator-globaltool
fi

# Run tests
dotnet test \
    --coverage \
    --coverage-output-format cobertura \
    --coverage-output coverage.cobertura.xml \
    --coverage-settings "$COVERAGE_SETTINGS" \
    --verbosity quiet

TEST_RESULT=$?

if [ $TEST_RESULT -ne 0 ] && [ $TEST_RESULT -ne 2 ]; then
    echo -e "${RED}Tests failed with exit code $TEST_RESULT${NC}"
    # Continue to generate report even if tests fail (exit code 2 means test failures but coverage was generated)
fi

# Generate coverage report
echo -e "${YELLOW}Generating coverage report...${NC}"
reportgenerator \
    -reports:"$REPO_ROOT/tests/**/TestResults/coverage.cobertura.xml" \
    -targetdir:"$COVERAGE_DIR" \
    -reporttypes:"Html;TextSummary;MarkdownSummary;Badges" \
    -verbosity:"Warning"

# Display summary
echo ""
echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}Coverage Summary${NC}"
echo -e "${GREEN}========================================${NC}"
cat "$COVERAGE_DIR/Summary.txt" | head -20

# Generate badge files
echo ""
echo -e "${GREEN}Badge files generated:${NC}"
ls -lh "$COVERAGE_DIR"/*.svg 2>/dev/null || echo "No badge files generated"

# Open report if possible
echo ""
echo -e "${GREEN}Coverage report generated at:${NC}"
echo -e "  ${YELLOW}$COVERAGE_DIR/index.html${NC}"
echo ""

# Try to open the report
if command -v xdg-open &> /dev/null; then
    echo -e "${YELLOW}Opening coverage report in browser...${NC}"
    xdg-open "$COVERAGE_DIR/index.html" &
elif command -v open &> /dev/null; then
    echo -e "${YELLOW}Opening coverage report in browser...${NC}"
    open "$COVERAGE_DIR/index.html"
elif command -v start &> /dev/null; then
    echo -e "${YELLOW}Opening coverage report in browser...${NC}"
    start "$COVERAGE_DIR/index.html"
else
    echo -e "${YELLOW}Please open the following file in your browser:${NC}"
    echo -e "  file://$COVERAGE_DIR/index.html"
fi

echo ""
echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}Analysis complete!${NC}"
echo -e "${GREEN}========================================${NC}"

exit 0
