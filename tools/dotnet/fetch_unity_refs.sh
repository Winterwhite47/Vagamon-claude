#!/usr/bin/env bash
# Downloads Unity 6000.0 engine reference assemblies (NuGet: Digitalroot.References.Unity 6000.0.75, publicized
# reference DLLs published for modding) into $1 and prints the directory to pass as UNITY_REFS.
# Compile-time only: nothing from the package is executed or shipped.
set -euo pipefail
dest="${1:?usage: fetch_unity_refs.sh <dir>}"
mkdir -p "$dest"
pkg="$dest/refs.nupkg"
curl -sSL -f -o "$pkg" https://api.nuget.org/v3-flatcontainer/digitalroot.references.unity/6000.0.75/digitalroot.references.unity.6000.0.75.nupkg
python3 -I -c "import sys, zipfile; zipfile.ZipFile(sys.argv[1]).extractall(sys.argv[2])" "$pkg" "$dest/x"
echo "$dest/x/ref/net48"
