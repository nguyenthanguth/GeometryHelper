"""Type-checks with exactly the errors marked by an expect comment on their lines."""

import numpy as np

import geometryhelper
from geometryhelper import undo_group
from geometryhelper.arrays import to_numpy, to_points3
from GeometryHelper.Cad import CadDocument
from GeometryHelper.Geometry import GeoPoint3

not_a_document = undo_group("a name", "x")  # expect: reportArgumentType
no_name = undo_group(CadDocument.Open("a.dxf"))  # expect: reportCallIssue
wrong_kind: int = geometryhelper.runtime()  # expect: reportAssignmentType
from_a_point = to_points3(GeoPoint3(0, 0, 0))  # expect: reportArgumentType|reportCallIssue
from_a_list = to_numpy([1.0, 2.0])  # expect: reportArgumentType|reportCallIssue
fine = to_numpy(to_points3(np.zeros((1, 3))))
