#!/usr/bin/env bash

set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
tool_project="$repo_root/src/DacpacTool/DacpacTool.csproj"
input_sql="$repo_root/test/TestProject/Tables/MyTable.sql"
work_dir="$(mktemp -d)"
base_dacpac="$work_dir/base.dacpac"
consumer_dacpac="$work_dir/consumer.dacpac"
input_list="$work_dir/inputfiles.txt"

cleanup() {
  rm -rf "$work_dir"
}
trap cleanup EXIT

printf '%s\n' "$input_sql" > "$input_list"

echo "Building base dacpac used as the reference..."
dotnet run --project "$tool_project" -- build \
  --name BasePackage \
  --version 1.0.0.0 \
  --output "$base_dacpac" \
  --inputfile "$input_list"

echo
echo "Reproducing the single-part --reference bug..."
echo "This should be a valid invocation, but currently fails because Program.BuildDacpac"
echo "uses 'if (...)' followed by 'if (...) else' instead of 'if / else if / else'."
echo

dotnet run --project "$tool_project" -- build \
  --name ConsumerPackage \
  --version 1.0.0.0 \
  --output "$consumer_dacpac" \
  --inputfile "$input_list" \
  --reference "$base_dacpac"
