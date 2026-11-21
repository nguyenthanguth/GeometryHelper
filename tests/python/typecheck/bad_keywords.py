"""Type-checks with exactly the errors marked by an expect comment on their lines."""

from GeometryHelper.Geometry import GeoPoint2, GeoVector2, GeoXLine2

line = GeoXLine2(GeoPoint2(0, 0), GeoVector2(1, 0))
# `from` is not a Python name: the stubs call it from_ and make it positional only, as pythonnet takes it
by_keyword = line.ToLine(from_=0.0, to=10.0)  # expect: reportCallIssue
