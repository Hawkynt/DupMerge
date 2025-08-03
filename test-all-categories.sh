#!/bin/bash
echo "Running all test categories..."
echo

echo "=== UNIT TESTS ==="
dotnet test --filter "Category=Unit" --logger "console;verbosity=normal"
echo

echo "=== INTEGRATION TESTS ==="
dotnet test --filter "Category=Integration" --logger "console;verbosity=normal"
echo

echo "=== PERFORMANCE TESTS ==="
dotnet test --filter "Category=Performance" --logger "console;verbosity=normal"
echo

echo "=== END-TO-END TESTS ==="
dotnet test --filter "Category=EndToEnd" --logger "console;verbosity=normal"
echo

echo "=== ALL TESTS SUMMARY ==="
dotnet test --logger "console;verbosity=normal"