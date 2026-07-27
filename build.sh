#!/usr/bin/env bash
# Build script for the EfCoreMcp solution.
# This script is invoked by the automated build system.

set -euo pipefail

# Ensure we are in the repository root.
# If the script is executed from a subdirectory, change to the script's directory.
cd "$(dirname "$0")"

# Restore NuGet packages and build the solution.
dotnet restore
dotnet build --configuration Release --no-restore
