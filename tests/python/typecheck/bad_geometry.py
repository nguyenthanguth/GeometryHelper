"""Type-checks with exactly the errors marked by an expect comment on their lines (a `|` lists rules that are alike)."""

from GeometryHelper.Geometry import GeoPoint3

p = GeoPoint3(1, 2, 3)
q = GeoPoint3(4, 5, 6)
text: str = p.DistanceTo(q)  # expect: reportAssignmentType
bad_constructor = GeoPoint3("a", 2, 3)  # expect: reportCallIssue|reportArgumentType
missing_member = p.NoSuchMember  # expect: reportAttributeAccessIssue
two_points_added = p + q  # expect: reportOperatorIssue
