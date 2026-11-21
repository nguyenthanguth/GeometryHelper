"""Type-checks with exactly the errors marked by an expect comment on their lines: what pythonnet refuses at run time
(measured, T2 and T3) the stubs refuse too."""

from GeometryHelper.Geometry import GeoPoint2, GeoPolygon2, GeoRectangle2
from GeometryHelper.Takeoff import VolumeTakeoff

# a Python list is not an IReadOnlyList<VolumeItem> for pythonnet
results = VolumeTakeoff.Run([])  # expect: reportArgumentType|reportCallIssue

# an IReadOnlyList<T> as pythonnet wraps it has Count, not len()
polygon = GeoPolygon2(GeoPoint2(0, 0), GeoPoint2(4, 0), GeoPoint2(4, 3))
size = len(polygon.Vertices)  # expect: reportArgumentType

# GeoRectangle2.Fit takes IEnumerable<GeoPoint2> and no array: a Python list is refused there too
corners = [GeoPoint2(0, 0), GeoPoint2(4, 0), GeoPoint2(4, 2)]
fitted = GeoRectangle2.Fit(corners)  # expect: reportArgumentType|reportCallIssue
