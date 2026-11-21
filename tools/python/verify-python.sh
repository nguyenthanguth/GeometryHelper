#!/usr/bin/env bash
# Verifies the Python package: stubs, wheel, a fresh venv with the wheel installed, and tests/python on both runtimes.
# Run it after the solution build of CLAUDE.md ("Build and verify"). Exit 1 when any step or either test run fails.
set -eu

root=$(cd "$(dirname "$0")/../.." && { pwd -W 2>/dev/null || pwd; })
cd "$root"

# 1. Python.
. "$root/tools/python/find-python.sh"
echo "Python: $GH_PYTHON"

# 2. The stubs.
timeout 600 bash "$root/tools/python/make-stubs.sh"

# 3. The wheel.
timeout 1500 bash "$root/tools/python/build-wheel.sh"
wheel=$(ls "$root"/artifacts/python/geometryhelper-*.whl)

# 4. A fresh venv with the wheel and its numpy extra, and the test requirements.
venv=$root/artifacts/python/venv
rm -rf "$venv"
timeout 300 "$GH_PYTHON" -m venv "$venv"
timeout 900 "$venv/Scripts/python.exe" -m pip install --quiet --disable-pip-version-check "$wheel[numpy]"
timeout 900 "$venv/Scripts/python.exe" -m pip install --quiet --disable-pip-version-check -r "$root/tests/python/requirements.txt"

# 5. The tests, once per runtime.
failed=0
summary=""
for runtime in coreclr netfx; do
    echo "== pytest tests/python on $runtime =="
    log=$root/artifacts/python/pytest-$runtime.log
    if GEOMETRYHELPER_RUNTIME=$runtime timeout 1800 "$venv/Scripts/python.exe" -m pytest tests/python -q >"$log" 2>&1; then
        :
    else
        failed=1
    fi
    tail -n 25 "$log"
    summary="$summary$runtime: $(tail -n 1 "$log")"$'\n'
done
echo "== Python summary =="
printf '%s' "$summary"
exit "$failed"
