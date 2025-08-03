@echo off
echo Running tests with code coverage...

rem Clean previous coverage results
if exist "TestResults" rmdir /s /q "TestResults"

rem Run tests with coverage
dotnet test --configuration Release --settings coverage.runsettings --collect:"XPlat Code Coverage" --results-directory:"TestResults"

rem Find the coverage file and generate report
for /r "TestResults" %%f in (coverage.cobertura.xml) do (
    echo Found coverage file: %%f
    
    rem Install reportgenerator if not already installed
    dotnet tool install -g dotnet-reportgenerator-globaltool 2>nul
    
    rem Generate HTML report
    reportgenerator -reports:"%%f" -targetdir:"TestResults\CoverageReport" -reporttypes:Html
    
    echo Coverage report generated in TestResults\CoverageReport\index.html
    goto :found
)

:found
echo.
echo To view coverage report, open: TestResults\CoverageReport\index.html
pause