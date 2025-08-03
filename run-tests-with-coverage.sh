#!/bin/bash
echo "Running tests with code coverage..."

# Clean previous coverage results
rm -rf TestResults

# Run tests with coverage
dotnet test --configuration Release --settings coverage.runsettings --collect:"XPlat Code Coverage" --results-directory:"TestResults"

# Find the coverage file and generate report
coverage_file=$(find TestResults -name "coverage.cobertura.xml" | head -1)

if [ -n "$coverage_file" ]; then
    echo "Found coverage file: $coverage_file"
    
    # Install reportgenerator if not already installed
    dotnet tool install -g dotnet-reportgenerator-globaltool 2>/dev/null || true
    
    # Generate HTML report
    reportgenerator -reports:"$coverage_file" -targetdir:"TestResults/CoverageReport" -reporttypes:Html
    
    echo "Coverage report generated in TestResults/CoverageReport/index.html"
else
    echo "No coverage file found!"
fi

echo ""
echo "To view coverage report, open: TestResults/CoverageReport/index.html"