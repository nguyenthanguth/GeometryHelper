"""F2: the picture of a dimension on a file (D1675). A dimension added to a drawing of ``new_file`` comes with the picture
the app draws (lines, arrows and words with its measurement), and one moved by ``modify`` is drawn again.
"""

from typing import Any

from GeometryHelper.Dxf import DxfMText, DxfRotatedDimension
from GeometryHelper.Geometry import GeoPoint3

from geometryhelper.cad import new_file


def _a_dimension_of_100(far: float) -> Any:
    return DxfRotatedDimension(0.0, GeoPoint3(0, 0, 0), GeoPoint3(far, 0, 0), GeoPoint3(far, 20, 0), "", None)


def _words(dimension: Any) -> list[str]:
    return [str(m.Contents) for m in dimension.DimBlock if isinstance(m, DxfMText) and not m.IsErased]


def test_a_dimension_added_to_a_file_has_the_picture_of_its_measurement() -> None:
    drawing = new_file()

    drawing.add(_a_dimension_of_100(100))
    drawing.commit()

    made = drawing.entities("DIMENSION")[0].net
    assert round(made.Measurement, 6) == 100.0
    assert sum(1 for e in made.DimBlock if not e.IsErased) >= 4
    assert any("100" in words for words in _words(made))


def test_a_dimension_moved_by_modify_is_drawn_again_for_its_new_measurement() -> None:
    drawing = new_file()
    drawing.add(_a_dimension_of_100(100))
    drawing.commit()

    entity = drawing.entities("DIMENSION")[0]
    entity.net.XLine2Point = GeoPoint3(150, 0, 0)
    drawing.modify(entity)
    drawing.commit()

    moved = drawing.entities("DIMENSION")[0].net
    assert round(moved.Measurement, 6) == 150.0
    assert any("150" in words for words in _words(moved))
