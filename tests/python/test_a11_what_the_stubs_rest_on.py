"""A11: what pythonnet does with our types, which the stubs promise a user (measured as T2 to T6, 2026-11-21).

The package depends on ``pythonnet>=3.2.1,<4``, so a newer pythonnet can change any of these without a change of ours.
Each test pins one behaviour a stub rule rests on: if one fails after an upgrade, the stub rule named in its comment
needs a second look, not the test a quick edit.
"""

import pytest
from System import Action, Array, Func
from System import Exception as NetException
from System.Collections.Generic import List

import geometryhelper  # noqa: F401  (boots .NET and loads the assemblies)
from GeometryHelper import GeometryHelperLog, Tolerance
from GeometryHelper.Cad import CadCommandSession, CadDocument, CadSystemVariable, CadTrueTypeFonts
from GeometryHelper.Dxf import DxfBlockLookupRow, DxfBlockReference, DxfBlockTableRecord, DxfDatabase, DxfEraseFlags
from GeometryHelper.Dxf import DxfReadOptions, DxfWriter
from GeometryHelper.Enums import GeometryHelperLogLevel
from GeometryHelper.Extension import GeoPointEnumerableExtension
from GeometryHelper.Geometry import GeoPoint2, GeoPoint3, GeoPolygon2, GeoPolygon3, GeoRectangle2, GeoXLine2
from GeometryHelper.Geometry import GeoVector2
from GeometryHelper.Takeoff import VolumeItem, VolumeTakeoff

TOLERANCE = Tolerance.Default


# T2: lists (stub rule of D612: a Python list is typed only where it is accepted: an array, never an IEnumerable)


def test_a_python_list_is_accepted_for_an_array_parameter_and_refused_for_an_ienumerable_one() -> None:
    # GeoPolygon3 has a params GeoPoint3[] constructor and one for IEnumerable<GeoPoint3>: the list goes to the array.
    points = [GeoPoint3(0, 0, 0), GeoPoint3(10, 0, 0), GeoPoint3(10, 5, 0), GeoPoint3(0, 5, 0)]
    assert GeoPolygon3(points).Area == 50.0  # a rectangle 10 by 5
    assert GeoPolygon3(tuple(points)).Area == 50.0
    # GeoRectangle2.Fit takes IEnumerable<GeoPoint2> and no array: the same list is refused, a .NET array is taken.
    corners = [GeoPoint2(0, 0), GeoPoint2(4, 0), GeoPoint2(4, 2), GeoPoint2(0, 2)]
    with pytest.raises(TypeError):
        GeoRectangle2.Fit(corners)
    with pytest.raises(TypeError):
        GeoRectangle2.Fit(tuple(corners))
    assert GeoRectangle2.Fit(Array[GeoPoint2](corners)).Width > 0


def test_a_generator_is_refused_for_an_array_parameter_too() -> None:
    with pytest.raises(TypeError):
        GeoPolygon3(p for p in [GeoPoint3(0, 0, 0), GeoPoint3(1, 0, 0), GeoPoint3(1, 1, 0)])


def test_a_dotnet_list_is_not_built_from_a_python_list() -> None:
    with pytest.raises(TypeError):
        List[float]([1.0, 2.0])


def test_a_python_list_is_refused_for_ireadonlylist_and_the_dotnet_collections_are_accepted() -> None:
    with pytest.raises(TypeError):
        VolumeTakeoff.Run([])
    assert len(VolumeTakeoff.Run(Array[VolumeItem]([]))) == 0
    assert len(VolumeTakeoff.Run(List[VolumeItem]())) == 0


def test_a_python_list_is_refused_for_a_list_parameter_and_a_dotnet_list_is_accepted() -> None:
    near = [GeoPoint2(0, 0), GeoPoint2(1, 0), GeoPoint2(1, 0.0001), GeoPoint2(2, 0)]
    with pytest.raises(TypeError):
        GeoPointEnumerableExtension.RemoveConsecutiveNearPoints(near, TOLERANCE)
    with pytest.raises(TypeError):
        GeoPointEnumerableExtension.RemoveConsecutiveNearPoints(Array[GeoPoint2](near), TOLERANCE)
    points = List[GeoPoint2]()
    for point in near:
        points.Add(point)
    # (1, 0) and (1, 0.0001) are 1E-4 apart, a tenth of the 1E-3 point tolerance: one of them goes.
    assert len(GeoPointEnumerableExtension.RemoveConsecutiveNearPoints(points, TOLERANCE)) == 3


def test_a_python_list_is_refused_for_an_ilist_property_and_a_dotnet_list_or_array_is_accepted() -> None:
    options = DxfReadOptions()
    with pytest.raises(TypeError):
        options.SupportFolders = ["a", "b"]
    options.SupportFolders = Array[str](["a", "b"])
    options.SupportFolders = List[str]()


# T3: len() (stub rule R12: __len__ only where it works)


def test_len_works_on_a_list_and_an_array_but_not_on_an_interface_typed_return() -> None:
    points = List[GeoPoint2]()
    points.Add(GeoPoint2(1, 2))
    assert len(points) == 1
    assert len(Array[GeoPoint2]([GeoPoint2(1, 2), GeoPoint2(3, 4)])) == 2
    vertices = GeoPolygon2([GeoPoint2(0, 0), GeoPoint2(4, 0), GeoPoint2(4, 3)]).Vertices
    assert vertices.Count == 3
    with pytest.raises(TypeError):
        len(vertices)


def test_len_works_on_the_one_class_of_ours_that_is_a_read_only_collection() -> None:
    database = DxfDatabase.Parse(DxfWriter().ToString())
    block = DxfBlockTableRecord()
    block.Name = "B1"
    database.BlockTable.Add(block)
    attributes = DxfBlockReference(GeoPoint3(0, 0, 0), block).AttributeCollection
    assert len(attributes) == 0
    assert attributes.Count == 0


def test_len_is_missing_on_the_classes_of_ours_that_are_only_enumerable() -> None:
    database = DxfDatabase.Parse(DxfWriter().ToString())
    with pytest.raises(TypeError):
        len(database.LayerTable)
    with pytest.raises(TypeError):
        len(database.ModelSpace)


def test_a_negative_index_wraps_on_an_array_and_raises_on_a_list() -> None:
    array = Array[GeoPoint2]([GeoPoint2(1, 2), GeoPoint2(3, 4)])
    assert (array[-1].X, array[-1].Y) == (3.0, 4.0)
    points = List[GeoPoint2]()
    points.Add(GeoPoint2(1, 2))
    with pytest.raises(Exception, match="out of range"):
        points[-1]


# T4: flags (stub rule R2)


def test_flags_combine_with_each_other_and_not_with_a_number() -> None:
    a, b = DxfEraseFlags.ZeroLengthCurve, DxfEraseFlags.EmptyText
    assert (int(a | b), int(a & b), int(a ^ b)) == (3, 0, 3)
    assert type(a | b) is DxfEraseFlags
    assert int(~a) == -2
    assert ((a | b) & a) == a
    with pytest.raises(TypeError):
        a | 1
    with pytest.raises(TypeError):
        1 | a


def test_a_zero_flags_value_is_still_true_and_is_tested_against_the_zero_value() -> None:
    nothing = DxfEraseFlags.ZeroLengthCurve & DxfEraseFlags.EmptyText
    assert bool(nothing)  # a trap the guide names: `if flags & x` is always true
    assert int(nothing) == 0
    assert nothing == DxfEraseFlags(0)


# T5: params arrays (stub rule R9: an extra overload with *name)


def test_a_params_array_takes_separate_arguments_none_or_a_list() -> None:
    a, b, c = GeoPoint2(0, 0), GeoPoint2(10, 0), GeoPoint2(10, 5)
    assert GeoPolygon2(a, b, c).Area == 25.0
    assert GeoPolygon2([a, b, c]).Area == 25.0
    rectangle = GeoRectangle2(GeoPoint2(0, 0), 4.0, 2.0, 0.0)  # centre (0, 0), width 4, height 2
    assert (rectangle.Combine(a, b, c).Width, rectangle.Combine(a, b, c).Height) == (12.0, 6.0)
    assert rectangle.Combine().Width == 4.0
    assert DxfBlockLookupRow("r", 1.0, 2.0, 3.0) is not None
    assert DxfBlockLookupRow("r") is not None


# T6: static properties and delegates (D612 revision proposed in measure.md)


def test_a_static_property_is_assigned_and_read_back() -> None:
    # Tolerance.Global is not used here: it is process-wide, and new tests never set it (CLAUDE.md).
    before = GeometryHelperLog.Enable
    try:
        GeometryHelperLog.Enable = not before
        assert GeometryHelperLog.Enable == (not before)
    finally:
        GeometryHelperLog.Enable = before
    assert GeometryHelperLog.Enable == before


def test_a_property_without_a_setter_refuses_assignment() -> None:
    with pytest.raises(TypeError, match="read-only"):
        Tolerance.Default = Tolerance(1, 1)


def test_a_python_function_is_refused_for_a_delegate_and_a_wrapped_one_is_accepted() -> None:
    seen: list[str] = []
    writer = lambda level, message, exception: seen.append(f"{level}: {message}")  # noqa: E731
    try:
        with pytest.raises(TypeError):
            GeometryHelperLog.Writer = writer
        GeometryHelperLog.Writer = Action[GeometryHelperLogLevel, str, NetException](writer)
        GeometryHelperLog.Enable = True
        GeometryHelperLog.Warn("hello", None)
    finally:
        GeometryHelperLog.Writer = None
        GeometryHelperLog.Enable = False
    assert seen == ["Warn: hello"]


def test_a_python_function_is_refused_for_a_delegate_parameter_and_a_wrapped_one_is_accepted() -> None:
    value = [1.5]
    with pytest.raises(TypeError):
        CadSystemVariable.Real("X", 10.0, lambda: value[0], lambda x: value.__setitem__(0, x))
    variable = CadSystemVariable.Real(
        "X", 10.0, Func[float](lambda: value[0]), Action[float](lambda x: value.__setitem__(0, x))
    )
    assert variable is not None
    CadTrueTypeFonts.MeasureWidth = Func[str, bool, bool, str, float](lambda text, bold, italic, font: 1.0)
    CadTrueTypeFonts.MeasureWidth = None


def test_a_python_function_subscribes_to_an_event_with_plus_equals() -> None:
    document = CadDocument.From(DxfDatabase.Parse(DxfWriter().ToString()), "events.dxf")
    heard: list[object] = []

    def on_changed(sender: object, args: object) -> None:
        heard.append(args)

    document.Changed += on_changed
    document.BeginGroup("noise")
    session = CadCommandSession(document)
    for answer in ("CIRCLE", "0,0", "5"):
        session.Feed(answer)
    document.EndGroup()
    document.Changed -= on_changed
    assert len(heard) >= 1


# R10: the parameters named like Python keywords


def test_a_parameter_named_from_is_given_by_position_or_through_a_dictionary() -> None:
    line = GeoXLine2(GeoPoint2(0, 0), GeoVector2(1, 0))
    expected = line.ToLine(0.0, 10.0)
    assert str(line.ToLine(0.0, to=10.0)) == str(expected)
    assert str(line.ToLine(**{"from": 0.0, "to": 10.0})) == str(expected)
