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
from GeometryHelper import GeometryHelperLog, SolidBooleanOptions, Tolerance
from GeometryHelper.Cad import CadCommandSession, CadDocument, CadSystemVariable, CadTrueTypeFonts
from GeometryHelper.Core import Boolean3, Intersection2, Splition2
from GeometryHelper.Dxf import DxfBlockLookupRow, DxfBlockReference, DxfBlockTableRecord, DxfDatabase, DxfEraseFlags
from GeometryHelper.Dxf import DxfReadOptions, DxfWriter
from GeometryHelper.Enums import BooleanOutcome, GeometryHelperLogLevel
from GeometryHelper.Extension import GeoPointEnumerableExtension
from GeometryHelper.Geometry import GeoLine2, GeoObb3, GeoPoint2, GeoPoint3, GeoPolygon2, GeoPolygon3, GeoRectangle2
from GeometryHelper.Geometry import GeoSolid3, GeoXLine2
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


# Out parameters (stub rule R8): a trailing one is left out of the call, one with a parameter after it is left out only
# when that parameter is given by keyword, and the stubs write the parameters after it keyword-only


def _plate_and_cutter() -> tuple[GeoSolid3, GeoSolid3]:
    # a plate 100 by 100 by 10 and a cutter 20 by 20 by 30 through its middle
    return GeoObb3(GeoPoint3(0, 0, 0), 100, 100, 10).ToSolid(), GeoObb3(GeoPoint3(0, 0, 0), 20, 20, 30).ToSolid()


def test_a_trailing_out_parameter_is_left_out_and_comes_back_in_the_tuple() -> None:
    plate, cutter = _plate_and_cutter()
    made, left = plate.TrySubtract(cutter)
    assert made
    assert left.GetVolume(TOLERANCE) == pytest.approx(96000.0)  # 100 * 100 * 10 less 20 * 20 * 10


def test_an_out_parameter_before_the_tolerance_is_left_out_when_the_tolerance_is_a_keyword() -> None:
    plate, cutter = _plate_and_cutter()
    made, left = plate.TrySubtract(cutter, tolerance=TOLERANCE)
    assert made
    assert left.GetVolume(TOLERANCE) == pytest.approx(96000.0)
    # a static one, and one after the arguments in front of it
    assert Boolean3.TrySubtract(plate, cutter, tolerance=TOLERANCE)[0]
    line, other = GeoLine2(GeoPoint2(0, 0), GeoPoint2(10, 10)), GeoLine2(GeoPoint2(0, 10), GeoPoint2(10, 0))
    found, crossing = Intersection2.TryIntersectWith(line, other, tolerance=TOLERANCE)
    assert found
    assert crossing.X == pytest.approx(5.0)
    assert crossing.Y == pytest.approx(5.0)


def test_an_out_parameter_before_the_tolerance_is_refused_when_the_tolerance_is_positional() -> None:
    plate, cutter = _plate_and_cutter()
    with pytest.raises(TypeError):
        plate.TrySubtract(cutter, TOLERANCE)
    with pytest.raises(TypeError):
        plate.TryGetVolume(TOLERANCE)


def test_every_out_parameter_before_the_tolerance_is_left_out_with_the_keyword_too() -> None:
    # Splition2.TrySplitBy(line, point, out GeoLine2, out GeoLine2, tolerance)
    line = GeoLine2(GeoPoint2(0, 0), GeoPoint2(10, 10))
    split, first, second = Splition2.TrySplitBy(line, GeoPoint2(5, 5), tolerance=TOLERANCE)
    assert split
    assert first.Length == pytest.approx(second.Length)


def test_an_out_parameter_of_a_value_type_is_left_out_with_the_keyword_too() -> None:
    plate, _ = _plate_and_cutter()
    assert plate.TryGetVolume(tolerance=TOLERANCE) == (True, 100000.0)  # a double comes back in the tuple


def test_an_out_parameter_followed_by_a_parameter_takes_a_placeholder_where_the_keyword_is_not_used() -> None:
    # what the stubs do not promise but pythonnet allows: None for a class, a value of the type for a struct or a double
    plate, cutter = _plate_and_cutter()
    assert plate.TrySubtract(cutter, None, TOLERANCE)[0]
    line, other = GeoLine2(GeoPoint2(0, 0), GeoPoint2(10, 10)), GeoLine2(GeoPoint2(0, 10), GeoPoint2(10, 0))
    assert Intersection2.TryIntersectWith(line, other, GeoPoint2(), TOLERANCE)[0]
    assert plate.TryGetVolume(0.0, TOLERANCE)[0]
    with pytest.raises(TypeError):
        Intersection2.TryIntersectWith(line, other, None, TOLERANCE)
    with pytest.raises(TypeError):
        plate.TryGetVolume(None, TOLERANCE)


def test_the_overload_with_one_more_out_parameter_is_reached_with_the_tolerance_and_the_options_by_keyword() -> None:
    # TrySubtract(tool, out result, SolidBooleanOptions, out outcome) comes back as a three-item tuple
    plate, cutter = _plate_and_cutter()
    made, left, outcome = plate.TrySubtract(cutter, options=SolidBooleanOptions.Default)
    assert made
    assert outcome == BooleanOutcome.Made
    assert left.GetVolume(TOLERANCE) == pytest.approx(96000.0)
