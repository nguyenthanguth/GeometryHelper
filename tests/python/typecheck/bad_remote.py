"""Type-checks with exactly the errors marked by an expect comment on their lines: what pythonnet refuses at run time
when a script drives a running app (measured, P4) the stubs refuse too. The file is never run: pyright only reads it."""

from GeometryHelper.Cad.Remote import CadApplication, CadObjectFilter

with CadApplication() as app:
    doc = app.ActiveDocument

    # a Python list is not an IEnumerable<string> for pythonnet: it takes a .NET array
    doc.Select(["1A"])  # expect: reportArgumentType|reportCallIssue

    # no keyword initialisers for a .NET object: the properties are set after the constructor
    by_layer = CadObjectFilter(Layer="0")  # expect: reportCallIssue

    # Kind is a System.Type or the Python class of a .NET type, not a number
    by_kind = CadObjectFilter()
    by_kind.Kind = 5  # expect: reportAttributeAccessIssue
