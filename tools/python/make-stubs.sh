#!/bin/bash
# Writes the Python stubs of the library to src/python/GeometryHelper-stubs (git-ignored): runs the stub generator, built
# by the solution build, on the Release DLLs and XML docs of GeometryHelper, GeometryHelper.Cad and
# GeometryHelper.Cad.Remote. Build the solution in Release first:
#   dotnet build GeometryHelper.slnx -c Release -warnaserror --no-incremental
# The summary the generator prints (types, members, modules, what was left out) goes to standard output; the exit code
# is the generator's.
set -eu

root=$(cd "$(dirname "$0")/../.." && pwd)
tool="$root/tools/GeometryHelper.PyStubs/bin/Release/net8.0/GeometryHelper.PyStubs.dll"
lib="$root/src/GeometryHelper.Cad/bin/Release/netstandard2.0"

if [ ! -f "$tool" ]; then
    echo "The stub generator is not built: $tool" >&2
    echo "Run: dotnet build GeometryHelper.slnx -c Release -warnaserror --no-incremental" >&2
    exit 1
fi
if [ ! -f "$lib/GeometryHelper.dll" ] || [ ! -f "$lib/GeometryHelper.Cad.dll" ] || [ ! -f "$lib/GeometryHelper.Cad.Remote.dll" ]; then
    echo "The Release libraries are not built in $lib" >&2
    echo "Run: dotnet build GeometryHelper.slnx -c Release -warnaserror --no-incremental" >&2
    exit 1
fi

timeout 300 dotnet "$tool" --lib "$lib" --out "$root/src/python"
