using System;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    // An infinite line in the plane asked of a face. The line is cut into the segment that reaches past the shape on
    // both sides (see XLine2) and asked as any segment is, with the tolerance passed through. Against a point, a ray
    // or another line nothing is cut.
    static partial class Face2
    {
        internal static GeoPoint2[] GetIntersections(GeoFace2 face, GeoXLine2 xline, Tolerance tolerance)
        {
            if (face == null) throw new ArgumentNullException(nameof(face));

            return GetIntersections(face, XLine2.CutPast(xline, face, tolerance), tolerance);
        }

        internal static bool CollidesWith(GeoFace2 face, GeoXLine2 xline, Tolerance tolerance)
        {
            if (face == null) throw new ArgumentNullException(nameof(face));

            return CollidesWith(face, XLine2.CutPast(xline, face, tolerance), tolerance);
        }

        internal static GeoLine2 GetShortestLineTo(GeoFace2 face, GeoXLine2 xline, Tolerance tolerance)
        {
            if (face == null) throw new ArgumentNullException(nameof(face));

            return GetShortestLineTo(face, XLine2.CutPast(xline, face, tolerance), tolerance);
        }

        internal static double DistanceTo(GeoFace2 face, GeoXLine2 xline, Tolerance tolerance)
        {
            if (face == null) throw new ArgumentNullException(nameof(face));

            return DistanceTo(face, XLine2.CutPast(xline, face, tolerance), tolerance);
        }
    }
}
