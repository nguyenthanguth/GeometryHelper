#!/usr/bin/env bash
# Builds the geometryhelper wheel into artifacts/python. Needs the Release build of src/GeometryHelper.Cad (the
# solution build) and src/python/GeometryHelper-stubs (make-stubs.sh). Python: see find-python.sh.
set -eu

root=$(cd "$(dirname "$0")/../.." && { pwd -W 2>/dev/null || pwd; })
. "$root/tools/python/find-python.sh"

built=$root/src/GeometryHelper.Cad/bin/Release/netstandard2.0
project=$root/src/python
lib=$project/geometryhelper/lib
out=$root/artifacts/python

for name in GeometryHelper GeometryHelper.Cad GeometryHelper.Cad.Remote; do
    if [ ! -f "$built/$name.dll" ]; then
        echo "$built/$name.dll is missing: build GeometryHelper.slnx in Release first." >&2
        exit 1
    fi
done

# Only the DLLs and the three support folders: no XML docs and no PDBs.
rm -rf "$lib"
mkdir -p "$lib"
for name in GeometryHelper GeometryHelper.Cad GeometryHelper.Cad.Remote; do
    cp "$built/$name.dll" "$lib/"
done
for folder in Fonts Hatches Linetypes; do
    cp -r "$built/$folder" "$lib/$folder"
done
cp "$root/LICENSE" "$project/LICENSE"

# `build` lives in its own venv, made once and kept; hatchling is fetched by the isolated build.
mkdir -p "$out"
rm -f "$out"/geometryhelper-*.whl
if [ ! -x "$out/venv-build/Scripts/python.exe" ]; then
    rm -rf "$out/venv-build"
    timeout 300 "$GH_PYTHON" -m venv "$out/venv-build"
    timeout 600 "$out/venv-build/Scripts/python.exe" -m pip install --quiet --disable-pip-version-check "build==1.6.1"
fi
(cd "$project" && timeout 600 "$out/venv-build/Scripts/python.exe" -m build --wheel --outdir "$out")

wheel=$(ls "$out"/geometryhelper-*.whl)
echo "Built $wheel ($(stat -c %s "$wheel") bytes)"
