# geometryhelper

GeometryHelper for Python: 2D and 3D geometry, DXF and DWG read and write, and the CAD presenter without a window
(a `CadDocument` edited through its command session and saved). The library is the .NET one, run on
[pythonnet](https://pythonnet.github.io/); the .NET names stay as they are, and the wheel carries stubs so an editor
(Pylance, pyright) completes them.

## Install

Windows, 64-bit, Python 3.11 to 3.15, and a .NET 8 (or later) runtime or .NET Framework 4.8:

```
pip install geometryhelper[numpy]
```

The `numpy` extra is for `geometryhelper.arrays`, which copies point arrays to and from numpy in one block.

## Example

```python
import geometryhelper
from GeometryHelper.Cad import CadCommandSession, CadDocument
from GeometryHelper.Dxf import DxfDatabase, DxfWriter

document = CadDocument.From(DxfDatabase.Parse(DxfWriter().ToString()))
session = CadCommandSession(document)
with geometryhelper.undo_group(document, "A circle"):  # one undo step, or none if the block raises
    for line in ("CIRCLE", "0,0", "25"):
        session.Feed(line)
document.SaveAs("drawing.dwg")
print(document.EntityCount, geometryhelper.__version__, geometryhelper.runtime())
```

The runtime is chosen by the environment variable `GEOMETRYHELPER_RUNTIME`: `auto` (the default: .NET 8 or later when
it is installed, else .NET Framework), `coreclr` or `netfx`.

The guide, with the runtime choice, the numpy helpers and the stubs, is at
<https://nguyenthanguth.github.io/GeometryHelper/>.
