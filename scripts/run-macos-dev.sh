#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

if ! command -v dotnet >/dev/null 2>&1; then
  echo "Toren IDE requires a .NET 10 SDK. 'dotnet' was not found on PATH." >&2
  exit 1
fi

echo "Starting Toren IDE from source..."
exec dotnet run --project src/Toren.App/Toren.App.csproj
