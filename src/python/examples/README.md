# Python examples

Thirteen scripts for the three places Python works with GeometryHelper: outside the app on the libraries, inside
GeometryHelper CAD, and outside it driving a running app. Each does one common task, and a test of the repository runs
each against a drawing it builds, on .NET 8 and on .NET Framework 4.8, so they keep working. They are not part of the
wheel. The guide, [Python examples](../../GeometryHelper/docs/python-examples.md), shows what each prints.

| File | Place | Task | How to run |
|---|---|---|---|
| [library/list_layers.py](library/list_layers.py) | outside the app | the objects and the closed area on each layer of a drawing | `python list_layers.py plan.dxf` |
| [library/coordinates_numpy.py](library/coordinates_numpy.py) | outside the app | the vertices of every polyline as numpy arrays, and their extents | `python coordinates_numpy.py plan.dxf` |
| [library/make_drawing.py](library/make_drawing.py) | outside the app | a drawing made in code, with a layer, a rectangle and a circle, saved as DXF or DWG | `python make_drawing.py out.dwg` |
| [library/regions.py](library/regions.py) | outside the app | union, difference, intersection and offset of two squares | `python regions.py` |
| [library/solids.py](library/solids.py) | outside the app | a box with a hole cut through it: volume, surface area and centroid | `python solids.py` |
| [library/batch_folder.py](library/batch_folder.py) | outside the app | a CSV report of every DXF and DWG file of a folder | `python batch_folder.py folder report.csv` |
| [library/convert.py](library/convert.py) | outside the app | a DXF or DWG written as a DXF or DWG, in a version | `python convert.py in.dxf out.dwg --version AC1027` |
| [in_app/number_points.py](in_app/number_points.py) | PYRUN | a number written at each point you pick | PYRUN, then the file |
| [in_app/drawing_tools.py](in_app/drawing_tools.py) | PYLOAD | the commands RECTC and LAYERCOUNT | PYLOAD, then the file |
| [in_app/selection_lengths.py](in_app/selection_lengths.py) | PYRUN | the length and closed area of the selected objects | PYRUN, then the file |
| [remote/draw_and_read.py](remote/draw_and_read.py) | a script driving the app | a grid drawn in a new drawing and read back | `python draw_and_read.py [out.dxf]` |
| [remote/pick_and_mark.py](remote/pick_and_mark.py) | a script driving the app | a circle drawn where the user picks | `python pick_and_mark.py` |
| [remote/open_documents.py](remote/open_documents.py) | a script driving the app | the open drawings and what is in each | `python open_documents.py` |

The library scripts run in the virtual environment the wheel is installed in (`pip install geometryhelper[numpy]`, then
`python <file>`). The scripts in `in_app` are run from the command line of GeometryHelper CAD, with PYRUN or PYLOAD. The
scripts in `remote` need GeometryHelper CAD running, with "Allow other programs to connect" on in Global settings.
