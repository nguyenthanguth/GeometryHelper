"""Build hooks of the geometryhelper wheel: the version, the platform tag, a check that the inputs exist."""

import re
from pathlib import Path
from typing import Any

from hatchling.builders.hooks.plugin.interface import BuildHookInterface
from hatchling.metadata.plugin.interface import MetadataHookInterface

_PROPS = Path("..") / ".." / "Directory.Build.props"

# What the build scripts leave in the project folder; the wheel is not built without them.
_INPUTS = (
    ("geometryhelper/lib/GeometryHelper.dll", "tools/python/build-wheel.sh"),
    ("GeometryHelper-stubs/__init__.pyi", "tools/python/make-stubs.sh"),
    ("LICENSE", "tools/python/build-wheel.sh"),
)


class VersionHook(MetadataHookInterface):
    """Takes the version from GeometryHelperVersion, so the NuGet packages and the wheel have one."""

    def update(self, metadata: dict[str, Any]) -> None:
        text = (Path(self.root) / _PROPS).read_text(encoding="utf-8")
        found = re.search(r"<GeometryHelperVersion>([^<]+)</GeometryHelperVersion>", text)
        if found is None:
            raise RuntimeError("No <GeometryHelperVersion> in " + str(Path(self.root) / _PROPS))
        metadata["version"] = found.group(1).strip()


class PlatformHook(BuildHookInterface):
    """Tags the wheel py3-none-win_amd64 and stops the build when an input is missing."""

    def initialize(self, version: str, build_data: dict[str, Any]) -> None:
        for name, script in _INPUTS:
            if not (Path(self.root) / name).is_file():
                raise RuntimeError(f"{name} is missing: run {script} first.")
        build_data["pure_python"] = False
        build_data["tag"] = "py3-none-win_amd64"
