using System;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    // An infinite line in space asked of a curved chain and of a curved loop. The line is cut into the segment that reaches past
    // the shape on both sides (see XLine3) and asked as any segment is, with the tolerance passed through.
    static partial class ArcChain3
    {
        internal static bool CollidesWith(GeoPolylineArc3 chain, GeoXLine3 xline, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));

            return CollidesWith(chain, XLine3.CutPast(xline, chain, tolerance), tolerance);
        }

        internal static GeoPoint3[] GetIntersections(GeoPolylineArc3 chain, GeoXLine3 xline, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));

            return GetIntersections(chain, XLine3.CutPast(xline, chain, tolerance), tolerance);
        }

        internal static bool TryIntersectWith(GeoPolylineArc3 chain, GeoXLine3 xline, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));

            return TryIntersectWith(chain, XLine3.CutPast(xline, chain, tolerance), out intersections, tolerance);
        }

        internal static bool CollidesWith(GeoPolygonArc3 loop, GeoXLine3 xline, Tolerance tolerance)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));

            return CollidesWith(loop, XLine3.CutPast(xline, loop, tolerance), tolerance);
        }

        internal static GeoPoint3[] GetIntersections(GeoPolygonArc3 loop, GeoXLine3 xline, Tolerance tolerance)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));

            return GetIntersections(loop, XLine3.CutPast(xline, loop, tolerance), tolerance);
        }

        internal static bool TryIntersectWith(GeoPolygonArc3 loop, GeoXLine3 xline, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));

            return TryIntersectWith(loop, XLine3.CutPast(xline, loop, tolerance), out intersections, tolerance);
        }
    }
}
