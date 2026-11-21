# Python API

Every type of the Python package `geometryhelper`, as a script sees it: 806 types in 18 namespaces, 18 561 members. One
page a type, with the import line, each member as Python reads it (every overload, `self`, Python types) and its
summary. The pages are written from the same stubs an editor reads, so the text is what hovering a member in Pylance
shows, and the stubs are written from the XML documentation of the libraries.

Each member has a link to its page in the .NET reference. The remarks and the examples are there: the stubs carry the
summary only. The .NET reference is the full text, this reference is the shape of the call from Python.

## Reading a signature

- `self` is the object. A method is called without it: `p.DistanceTo(q)`.
- A .NET `out` parameter is not a parameter. The call returns a tuple of its result and the out values, and the signature
  says `-> tuple[bool, GeoPoint3]`. A parameter that comes after an out parameter is keyword-only, after a `*`:
  `plate.TrySubtract(cutter, tolerance=tolerance)`. See [out parameters come back in a tuple](../../src/GeometryHelper/docs/python.md#out-parameters-come-back-in-a-tuple).
- `Sequence[T] | System.Array[T]` takes a Python list or tuple. A parameter written `System.Array[T]` alone, or a .NET
  interface such as `IReadOnlyList[T]`, does not: build an `Array[T]` or a `List[T]` first. See
  [lists: only for arrays](../../src/GeometryHelper/docs/python.md#lists-only-for-arrays).
- A `params` array is an overload with a list and an overload with `*name`: `GeoPolygon2(a, b, c)` and
  `GeoPolygon2([a, b, c])` both work.
- A parameter named as a Python keyword (`from`, `with`, `except`) is written `from_` and takes its argument by position
  only. The 33 members named `None`, `True` or `False` are left out of the pages, as they are of the stubs: read them
  with `getattr(DxfEndCap, "None")`. See
  [members named None, True or False](../../src/GeometryHelper/docs/python.md#members-named-none-true-or-false).
- A property is read and set without parentheses. A property shown with a setter can be assigned.
- The names are the .NET names. See [the .NET names stay as they are](../../src/GeometryHelper/docs/python.md#the-net-names-stay-as-they-are).

How the stubs are made and what an editor needs to find them is in
[Stubs and an editor](../../src/GeometryHelper/docs/python.md#stubs-and-an-editor).
