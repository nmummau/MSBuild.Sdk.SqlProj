#!/usr/bin/env bash

set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
results_dir="$repo_root/TestResults"
report_dir="$repo_root/coverage-report"
test_project="$repo_root/test/DacpacTool.Tests/DacpacTool.Tests.csproj"

rm -rf "$results_dir" "$report_dir"

dotnet test "$test_project" -c Release --collect:"XPlat Code Coverage" --results-directory "$results_dir" "$@"

if ! command -v reportgenerator >/dev/null 2>&1; then
  dotnet tool install -g dotnet-reportgenerator-globaltool
fi

reportgenerator \
  -reports:"$results_dir/**/coverage.cobertura.xml" \
  -targetdir:"$report_dir" \
  -filefilters:"-*.g.cs" \
  -reporttypes:"Html"

echo "Coverage report: $report_dir/index.html"

if command -v xdg-open >/dev/null 2>&1; then
  xdg-open "$report_dir/index.html" >/dev/null 2>&1 || true
fi
