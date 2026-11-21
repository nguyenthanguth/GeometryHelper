# Sourced by build-wheel.sh and verify-python.sh. Sets GH_PYTHON to a 64-bit CPython 3.11 to 3.15, or exits 1.
# $GH_PYTHON when set; else the py launcher (PEP 514) for 3.15 down to 3.11, the first that runs (the launcher also
# lists installs that are gone from disk, so one that fails to run is passed over).
_gh_probe='import sys; ok = sys.implementation.name == "cpython" and (3, 11) <= sys.version_info[:2] <= (3, 15) and sys.maxsize > 2**32; print(sys.executable if ok else "")'
if [ -n "${GH_PYTHON:-}" ]; then
    _gh_found=$(timeout 60 "$GH_PYTHON" -c "$_gh_probe" 2>/dev/null || true)
    if [ -z "$_gh_found" ]; then
        echo "GH_PYTHON is '$GH_PYTHON': it must be a 64-bit CPython 3.11 to 3.15." >&2
        exit 1
    fi
else
    _gh_found=""
    for _gh_minor in 15 14 13 12 11; do
        _gh_found=$(timeout 60 py "-3.$_gh_minor" -c "$_gh_probe" 2>/dev/null || true)
        if [ -n "$_gh_found" ]; then break; fi
    done
    if [ -z "$_gh_found" ]; then
        echo "No CPython 3.11 to 3.15 found: install one or set GH_PYTHON." >&2
        exit 1
    fi
fi
GH_PYTHON=$_gh_found
export GH_PYTHON
