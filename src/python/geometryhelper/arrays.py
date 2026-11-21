"""Copies between numpy arrays and .NET arrays of doubles and points, one block at a time.

Reading a point array one property at a time (``p.X``, ``p.Y``, ``p.Z`` of each element) costs about 280 ms for
100 000 GeoPoint3 on .NET 8. The functions here pin the .NET array and copy it with one memmove: about 1 ms. They rely
on GeoPoint2 and GeoPoint3 being structs of exactly two and three doubles.

Needs numpy: ``pip install geometryhelper[numpy]``.
"""

from __future__ import annotations

import ctypes
import importlib
from collections.abc import Sequence
from typing import TYPE_CHECKING, Any

from geometryhelper import _DISTRIBUTION  # pyright: ignore[reportPrivateUsage]

if TYPE_CHECKING:
    from GeometryHelper.Geometry import GeoPoint2, GeoPoint3
    from System import Array
    from System.Collections.Generic import IEnumerable

try:
    import numpy as np
except ImportError as error:
    raise ImportError(f"geometryhelper.arrays needs numpy: pip install {_DISTRIBUTION}[numpy]") from error
from numpy.typing import NDArray

_system: Any = importlib.import_module("System")
_interop: Any = importlib.import_module("System.Runtime.InteropServices")
_linq: Any = importlib.import_module("System.Linq")
_geometry: Any = importlib.import_module("GeometryHelper.Geometry")

# The element types the functions handle, by their .NET full name: the Python class and the doubles per element.
_KINDS: dict[str, tuple[Any, int]] = {
    "System.Double": (_system.Double, 1),
    "GeometryHelper.Geometry.GeoPoint2": (_geometry.GeoPoint2, 2),
    "GeometryHelper.Geometry.GeoPoint3": (_geometry.GeoPoint3, 3),
}
_ENUMERABLE = "System.Collections.Generic.IEnumerable`1"
# numpy.typing.ArrayLike is not used: with Python 3.11 pyright reads part of it as Unknown, which strict mode refuses.
_Numbers = NDArray[Any] | Sequence[float] | Sequence[Sequence[float]]
_ACCEPTED = "a .NET array or IEnumerable of Double, GeoPoint2 or GeoPoint3"


def _kind_of(net_type: Any) -> str | None:
    """Return the full name of the element type of a .NET array or IEnumerable<T>, or None when it is not handled."""
    if net_type.IsArray:
        name: str = net_type.GetElementType().FullName
        return name if net_type.GetArrayRank() == 1 and name in _KINDS else None
    for interface in net_type.GetInterfaces():
        if interface.IsGenericType and interface.GetGenericTypeDefinition().FullName == _ENUMERABLE:
            name = interface.GetGenericArguments()[0].FullName
            if name in _KINDS:
                return name
    return None


def to_numpy(values: IEnumerable[float] | IEnumerable[GeoPoint2] | IEnumerable[GeoPoint3]) -> NDArray[np.float64]:
    """Copy a .NET array of Double, GeoPoint2 or GeoPoint3 into a new numpy array that the caller owns.

    Double gives shape ``(n,)``, GeoPoint2 ``(n, 2)`` and GeoPoint3 ``(n, 3)``, always float64. A .NET array is
    pinned and copied by one memmove. Any other IEnumerable of those (an IReadOnlyList such as GeoPolygon2.Vertices, a
    List) first goes through one ``Enumerable.ToArray`` call. Another element type, or a Python list, raises a
    TypeError.
    """
    # The parameter type is for the caller's type checker; a Python list still reaches here at run time.
    net_type: Any = values.GetType() if hasattr(values, "GetType") else None
    kind = _kind_of(net_type) if hasattr(values, "GetType") else None
    if kind is None:
        raise TypeError(f"to_numpy takes {_ACCEPTED}, not {type(values).__name__}.")
    element, width = _KINDS[kind]
    net_array = values if net_type.IsArray else _linq.Enumerable.ToArray[element](values)
    count = _linq.Enumerable.Count[element](net_array)
    result = np.empty(count if width == 1 else (count, width), dtype=np.float64)
    if count:
        handle = _interop.GCHandle.Alloc(net_array, _interop.GCHandleType.Pinned)
        try:
            ctypes.memmove(result.ctypes.data, handle.AddrOfPinnedObject().ToInt64(), result.nbytes)
        finally:
            handle.Free()
    return result


def _to_net(array: _Numbers, element: Any, width: int, function: str) -> Any:
    """Copy a numpy array of shape (n,) or (n, width) into a new .NET array of ``element``."""
    block = np.ascontiguousarray(array, dtype=np.float64)
    expected = "(n,)" if width == 1 else f"(n, {width})"
    if block.ndim != (1 if width == 1 else 2) or (width > 1 and block.shape[1] != width):
        raise ValueError(f"{function} expects shape {expected}, got {block.shape}.")
    count = block.shape[0]
    net_array = _system.Array[element](count)
    if count:
        handle = _interop.GCHandle.Alloc(net_array, _interop.GCHandleType.Pinned)
        try:
            ctypes.memmove(handle.AddrOfPinnedObject().ToInt64(), block.ctypes.data, block.nbytes)
        finally:
            handle.Free()
    return net_array


def to_doubles(array: _Numbers) -> Array[float]:
    """Copy a 1-D numpy array (any length, 0 included) into a new ``Array[Double]``.

    The values go through ``numpy.ascontiguousarray(array, dtype=float64)``, so a list of numbers works too. Another
    shape raises a ValueError. NaN and infinities are copied as they are.
    """
    return _to_net(array, _system.Double, 1, "to_doubles")


def to_points2(array: _Numbers) -> Array[GeoPoint2]:
    """Copy a numpy array of shape ``(n, 2)`` into a new ``Array[GeoPoint2]``.

    The values go through ``numpy.ascontiguousarray(array, dtype=float64)``. Another shape raises a ValueError naming
    the shape expected and the one given. NaN and infinities are copied as they are.
    """
    return _to_net(array, _geometry.GeoPoint2, 2, "to_points2")


def to_points3(array: _Numbers) -> Array[GeoPoint3]:
    """Copy a numpy array of shape ``(n, 3)`` into a new ``Array[GeoPoint3]``.

    The values go through ``numpy.ascontiguousarray(array, dtype=float64)``. Another shape raises a ValueError naming
    the shape expected and the one given. NaN and infinities are copied as they are.
    """
    return _to_net(array, _geometry.GeoPoint3, 3, "to_points3")
