#!/usr/bin/env bash

set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
tool_project="$repo_root/src/DacpacTool/DacpacTool.csproj"
input_sql="$repo_root/test/TestProject/Tables/MyTable.sql"
work_dir="$(mktemp -d)"
input_list="$work_dir/inputfiles.txt"
output_dacpac="$work_dir/out.dacpac"

cleanup() {
  rm -rf "$work_dir"
}
trap cleanup EXIT

printf '%s\n' "$input_sql" > "$input_list"

echo "Reproducing malformed --buildproperty handling..."
echo "Expected: a clean validation error about NAME=VALUE format."
echo "Actual: the current code can crash with an index error because it blindly accesses keyValuePair[1]."
echo

set +e
dotnet run --project "$tool_project" -- build \
  --name MyPackage \
  --version 1.0.0.0 \
  --output "$output_dacpac" \
  --inputfile "$input_list" \
  --buildproperty InvalidPropertyWithoutEquals
build_status=$?
set -e

echo
echo "Build command exit code: $build_status"
echo
echo "Reproducing malformed --sqlcmdvar handling..."
echo "Expected: a clean validation error about NAME=VALUE format."
echo "Actual: the current code can crash with an index error because it blindly accesses keyValuePair[1]."
echo

set +e
dotnet run --project "$tool_project" -- deploy \
  --input "$output_dacpac" \
  --targetServerName localhost \
  --targetDatabaseName MyDb \
  --sqlcmdvar InvalidSqlCmdVar
deploy_status=$?
set -e

echo
echo "Deploy command exit code: $deploy_status"
