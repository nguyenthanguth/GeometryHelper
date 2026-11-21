"""Type-checks clean: a script that drives a running app, as the guide writes it: ``with`` on the connection, a
``System.Type`` set from a Python class, a .NET array for ``IEnumerable<string>`` and the pick's exception.

The file is never run: it would connect to the app that is open on the machine. Pyright only reads it."""

from System import Array, IDisposable, String, TimeSpan

from GeometryHelper.Cad.Remote import CadApplication, CadObjectFilter, CadRemoteException
from GeometryHelper.Dxf import DxfLine

with CadApplication() as app:
    doc = app.ActiveDocument
    print(app.Running.ProcessId)

    # Kind takes the Python class of a .NET type (pythonnet passes it as the System.Type)
    only_lines = CadObjectFilter()
    only_lines.Kind = DxfLine
    only_lines.Layer = "Steel"
    for found in doc.GetObjects(only_lines):
        print(doc.HandleOf(found))

    # IEnumerable<string> takes a .NET array, not a Python list
    doc.Select(Array[String](["1A", "1B"]))
    for selected in doc.GetSelectedObjects():
        print(selected.Handle)

    try:
        corner = doc.PickPoint("Pick the corner", timeout=TimeSpan.FromSeconds(5))
        print(corner.X, corner.Y)
    except CadRemoteException as error:
        cancelled: bool = error.IsCancelled

    for handle in doc.CommitChanges():
        print(handle)


def hold(resource: IDisposable) -> None:
    """A value typed as the interface is a context manager as well."""
    with resource:
        print("held")
