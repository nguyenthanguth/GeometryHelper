"""A7: ``geometryhelper.arrays``: the four block copies between numpy and .NET arrays of doubles, GeoPoint2 and
GeoPoint3 (D607). The checks read the .NET side one property at a time, which is what the helpers exist to avoid."""

import importlib
import time
from collections.abc import Callable
from subprocess import CompletedProcess
from types import ModuleType
from typing import Any

import clr
import numpy as np
import pytest
from System import Array, Double
from System.Collections.Generic import List
from System.Runtime.InteropServices import Marshal

import geometryhelper  # noqa: F401  (boots .NET and loads the assemblies)
from GeometryHelper.Geometry import GeoPoint2, GeoPoint3, GeoPolygon2

Python = Callable[..., CompletedProcess[str]]


@pytest.fixture
def arrays() -> ModuleType:
    return importlib.import_module("geometryhelper.arrays")


def _random(shape: tuple[int, ...]) -> np.ndarray[Any, np.dtype[np.float64]]:
    # 1 000 values of the 300 000 mm to either side that a model reaches; the seed makes every run the same.
    return np.random.default_rng(7).uniform(-3e5, 3e5, shape)


def test_the_layout_the_copies_rely_on_is_two_and_three_doubles() -> None:
    # The memmove of D607 is right only while GeoPoint2 and GeoPoint3 are structs of exactly 2 and 3 doubles.
    assert Marshal.SizeOf(clr.GetClrType(GeoPoint2)) == 16
    assert Marshal.SizeOf(clr.GetClrType(GeoPoint3)) == 24


@pytest.mark.parametrize(
    "function, shape, element",
    [("to_doubles", (1000,), "Double"), ("to_points2", (1000, 2), "GeoPoint2"), ("to_points3", (1000, 3), "GeoPoint3")],
)
def test_numpy_to_dotnet_and_back_is_bit_for_bit(
    arrays: ModuleType, function: str, shape: tuple[int, ...], element: str
) -> None:
    source = _random(shape)
    converted = getattr(arrays, function)(source)
    assert converted.Length == 1000
    assert type(converted).__name__ == f"{element}[]"
    result = arrays.to_numpy(converted)
    assert result.dtype == np.float64
    assert result.shape == shape
    assert np.array_equal(result, source)


def test_a_point_array_equals_the_properties_of_its_points(arrays: ModuleType) -> None:
    source = _random((50, 3))
    points = arrays.to_points3(source)
    by_properties = np.array([[p.X, p.Y, p.Z] for p in points])
    assert np.array_equal(by_properties, source)
    assert np.array_equal(arrays.to_numpy(points), by_properties)


def test_the_array_made_from_numpy_keeps_the_order_of_x_y_and_z(arrays: ModuleType) -> None:
    points = arrays.to_points3(np.array([[1.0, 2.0, 3.0], [4.0, 5.0, 6.0]]))
    assert [(p.X, p.Y, p.Z) for p in points] == [(1.0, 2.0, 3.0), (4.0, 5.0, 6.0)]
    flat = arrays.to_points2(np.array([[7.0, 8.0]]))
    assert [(p.X, p.Y) for p in flat] == [(7.0, 8.0)]


def test_the_result_is_a_new_array_the_caller_owns(arrays: ModuleType) -> None:
    source = _random((10, 3))
    kept = source.copy()
    points = arrays.to_points3(source)
    result = arrays.to_numpy(points)
    assert result.flags.writeable and not np.shares_memory(result, source)
    points[0] = GeoPoint3(9, 9, 9)  # a change on the .NET side does not reach the copy already taken
    result[1, 1] = -1.0  # nor a change of the copy reach the .NET array or the source
    assert np.array_equal(source, kept)
    assert (points[1].X, points[1].Y, points[1].Z) == tuple(kept[1])
    assert tuple(result[0]) == tuple(kept[0])


@pytest.mark.parametrize("function, shape", [("to_doubles", (0,)), ("to_points2", (0, 2)), ("to_points3", (0, 3))])
def test_an_empty_input_gives_an_empty_array_and_back(
    arrays: ModuleType, function: str, shape: tuple[int, ...]
) -> None:
    converted = getattr(arrays, function)(np.empty(shape))
    assert converted.Length == 0
    assert arrays.to_numpy(converted).shape == shape


@pytest.mark.parametrize(
    "function, shape, expected",
    [
        ("to_points3", (5, 2), "(n, 3)"),
        ("to_points3", (5,), "(n, 3)"),
        ("to_points3", (2, 3, 3), "(n, 3)"),
        ("to_points2", (5, 3), "(n, 2)"),
        ("to_doubles", (3, 2), "(n,)"),
    ],
)
def test_a_wrong_shape_is_refused_naming_the_shape_expected_and_the_one_given(
    arrays: ModuleType, function: str, shape: tuple[int, ...], expected: str
) -> None:
    with pytest.raises(ValueError) as refused:
        getattr(arrays, function)(np.zeros(shape))
    assert expected in str(refused.value)
    assert str(shape) in str(refused.value)


@pytest.mark.parametrize(
    "source",
    [
        np.arange(12).reshape(4, 3),  # integers
        np.arange(12, dtype=np.float32).reshape(4, 3),  # single precision
        np.arange(12.0).reshape(3, 4).T,  # not contiguous (a transpose)
        np.asfortranarray(np.arange(12.0).reshape(4, 3)),  # column-major
        [[0, 1, 2], [3, 4, 5], [6, 7, 8], [9, 10, 11]],  # a list of lists
    ],
    ids=["int64", "float32", "transposed", "fortran", "list of lists"],
)
def test_any_array_like_is_converted_to_float64_in_row_order(arrays: ModuleType, source: Any) -> None:
    expected = np.asarray(source, dtype=np.float64)  # numpy's own reading of the source: shape (4, 3), rows as written
    assert np.array_equal(arrays.to_numpy(arrays.to_points3(source)), expected)


def test_a_read_only_input_is_accepted_and_left_as_it_was(arrays: ModuleType) -> None:
    source = _random((20, 3))
    source.flags.writeable = False
    kept = source.copy()
    assert np.array_equal(arrays.to_numpy(arrays.to_points3(source)), kept)
    assert np.array_equal(source, kept)


def test_nan_and_infinities_are_copied_as_they_are(arrays: ModuleType) -> None:
    source = np.array([[np.nan, np.inf, -np.inf], [1.0, 2.0, 3.0]])
    points = arrays.to_points3(source)
    assert np.isnan(points[0].X) and points[0].Y == np.inf and points[0].Z == -np.inf
    assert np.array_equal(arrays.to_numpy(points), source, equal_nan=True)


def test_negative_zero_keeps_its_sign(arrays: ModuleType) -> None:
    result = arrays.to_numpy(arrays.to_doubles(np.array([-0.0, 0.0])))
    assert np.signbit(result).tolist() == [True, False]


def test_the_vertices_of_a_polygon_an_ireadonlylist_are_converted(arrays: ModuleType) -> None:
    # GeoPolygon2.Vertices is an IReadOnlyList<GeoPoint2>, not an array: it goes through Enumerable.ToArray first.
    square = arrays.to_points2(np.array([[0.0, 0.0], [10.0, 0.0], [10.0, 5.0], [0.0, 5.0]]))
    vertices = GeoPolygon2(square).Vertices
    by_properties = np.array([[v.X, v.Y] for v in vertices])
    assert by_properties.shape == (4, 2)
    assert np.array_equal(arrays.to_numpy(vertices), by_properties)


def test_a_dotnet_list_of_points_and_of_doubles_is_converted(arrays: ModuleType) -> None:
    points = List[GeoPoint3]()
    points.Add(GeoPoint3(1, 2, 3))
    points.Add(GeoPoint3(4, 5, 6))
    assert np.array_equal(arrays.to_numpy(points), [[1.0, 2.0, 3.0], [4.0, 5.0, 6.0]])
    doubles = List[Double]()
    doubles.Add(0.5)
    doubles.Add(-2.0)
    assert np.array_equal(arrays.to_numpy(doubles), [0.5, -2.0])


@pytest.mark.parametrize(
    "refused",
    [[1.0, 2.0], (GeoPoint3(0, 0, 0),), [], "text", 5, None],
    ids=["list of floats", "tuple of points", "empty list", "string", "int", "None"],
)
def test_to_numpy_refuses_what_is_not_a_dotnet_collection_of_the_accepted_kinds(
    arrays: ModuleType, refused: Any
) -> None:
    with pytest.raises(TypeError) as error:
        arrays.to_numpy(refused)
    for kind in ("Double", "GeoPoint2", "GeoPoint3"):
        assert kind in str(error.value)


def test_to_numpy_refuses_a_dotnet_array_of_another_element_type(arrays: ModuleType) -> None:
    with pytest.raises(TypeError):
        arrays.to_numpy(Array[int]([1, 2, 3]))
    with pytest.raises(TypeError):
        arrays.to_numpy(Array[str](["a"]))


def test_a_hundred_thousand_points_copy_in_milliseconds_not_in_a_loop(arrays: ModuleType) -> None:
    # Measured 0.4 to 4.3 ms each way; reading X, Y and Z of every point takes 202 to 283 ms. The limit of 100 ms is 25
    # times the slowest copy and half the fastest loop, and the best of three runs is taken.
    source = _random((100_000, 3))
    arrays.to_numpy(arrays.to_points3(source))  # a warm-up call, so one-off costs are not timed
    best = float("inf")
    for _ in range(3):
        start = time.perf_counter()
        result = arrays.to_numpy(arrays.to_points3(source))
        best = min(best, time.perf_counter() - start)
    assert np.array_equal(result, source)
    assert best < 0.1


def test_the_package_imports_without_numpy(python: Python) -> None:
    process = python("import sys\nsys.modules['numpy'] = None\nimport geometryhelper\n")
    assert process.returncode == 0, process.stderr


def test_the_arrays_module_without_numpy_fails_with_the_documented_text(python: Python) -> None:
    process = python("import sys\nsys.modules['numpy'] = None\nimport geometryhelper.arrays\n")
    assert process.returncode == 1
    assert (
        "ImportError: geometryhelper.arrays needs numpy: pip install geometryhelper[numpy]"
        in process.stderr.splitlines()
    )
