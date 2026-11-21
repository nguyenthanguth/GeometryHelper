"""Type-checks clean: the helpers of geometryhelper and geometryhelper.arrays."""

from typing import Literal

import numpy as np

import geometryhelper
from geometryhelper import undo_group
from geometryhelper.arrays import to_doubles, to_numpy, to_points2, to_points3
from GeometryHelper.Cad import CadDocument
from GeometryHelper.Geometry import GeoPoint2, GeoPolygon2

version: str = geometryhelper.__version__
runtime: Literal["coreclr", "netfx"] = geometryhelper.runtime()


def edit(document: CadDocument) -> int:
    with undo_group(document, "one step") as target:
        return target.EntityCount


doubles = to_doubles(np.array([1.0, 2.0]))
points3 = to_points3(np.zeros((4, 3)))
points2 = to_points2([[0.0, 1.0], [2.0, 3.0]])  # any array-like
table: np.ndarray = to_numpy(points3)
vector: np.ndarray = to_numpy(doubles)
vertices: np.ndarray = to_numpy(GeoPolygon2(GeoPoint2(0, 0), GeoPoint2(4, 0), GeoPoint2(4, 3)).Vertices)
