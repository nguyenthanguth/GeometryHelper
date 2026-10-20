using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    // Where the surface of a ball is crossed by a curve in space. A curve in a plane meets the surface where it meets the circle
    // the plane cuts from the surface, in the plane of the curve, and two curves in one plane cross by the library's own: the
    // crossings of an arc or a circle with a circle, of an ellipse with a circle. A chain is its edges, each a segment or an arc, and
    // a corner on the surface counts once. Every list runs along the curve, a circle or an ellipse from its zero parameter.
    static partial class Sphere3
    {
        internal static GeoPoint3[] Cross(GeoSphere3 sphere, GeoArc3 arc, Tolerance tolerance)
        {
            switch (CutByPlane(sphere, arc.GetPlane(), tolerance, out GeoCircle3 circle, out GeoPoint3 touch))
            {
                case Meeting.Circle:
                    return Along(Arc3.GetIntersections(arc, circle, tolerance), p => arc.GetParameterAtPoint(p, tolerance));

                case Meeting.Point:
                    return arc.DistanceTo(touch, tolerance) <= tolerance.EqualPoint ? new[] { touch } : new GeoPoint3[0];

                default:
                    return new GeoPoint3[0];
            }
        }

        internal static GeoPoint3[] Cross(GeoSphere3 sphere, GeoCircle3 rim, Tolerance tolerance)
        {
            switch (CutByPlane(sphere, rim.GetPlane(), tolerance, out GeoCircle3 circle, out GeoPoint3 touch))
            {
                case Meeting.Circle:
                    return Along(Arc3.GetIntersections(rim, circle, tolerance), p => rim.GetParameterAtPoint(p, tolerance));

                case Meeting.Point:
                    return Distance3.DistanceTo(touch, Projection3.ProjectToCircle(rim, touch, tolerance)) <= tolerance.EqualPoint ? new[] { touch } : new GeoPoint3[0];

                default:
                    return new GeoPoint3[0];
            }
        }

        internal static GeoPoint3[] Cross(GeoSphere3 sphere, GeoEllipse3 ellipse, Tolerance tolerance)
        {
            switch (CutByPlane(sphere, ellipse.GetPlane(), tolerance, out GeoCircle3 circle, out GeoPoint3 touch))
            {
                case Meeting.Circle:
                    return Along(Ellipse3.GetIntersections(ellipse, circle, tolerance), p => ellipse.GetParameterAtPoint(p, tolerance));

                case Meeting.Point:
                    return Distance3.DistanceTo(touch, Ellipse3.GetClosestPointOnBoundary(ellipse, touch)) <= tolerance.EqualPoint ? new[] { touch } : new GeoPoint3[0];

                default:
                    return new GeoPoint3[0];
            }
        }

        internal static GeoPoint3[] Cross(GeoSphere3 sphere, GeoEdge3 edge, Tolerance tolerance)
        {
            var found = new List<GeoPoint3>();

            Cross(found, sphere, edge, tolerance);

            return found.ToArray();
        }

        internal static GeoPoint3[] Cross(GeoSphere3 sphere, IEnumerable<GeoEdge3> edges, bool closed, Tolerance tolerance)
        {
            var found = new List<GeoPoint3>();

            foreach (GeoEdge3 edge in edges)
            {
                Cross(found, sphere, edge, tolerance);
            }

            return Finish(found, closed, tolerance);
        }

        private static void Cross(List<GeoPoint3> found, GeoSphere3 sphere, GeoEdge3 edge, Tolerance tolerance)
        {
            if (!edge.IsArc)
            {
                Cross(found, sphere, edge.ToLine(), tolerance);
                return;
            }

            foreach (GeoPoint3 point in Cross(sphere, edge.ToArc(), tolerance))
            {
                AddDistinct(found, point, tolerance.EqualPoint);
            }
        }

        // The points in order of a parameter along the curve, ties by coordinates, so that a list never depends on the order the
        // two curves' crossing gave them in.
        private static GeoPoint3[] Along(GeoPoint3[] points, Func<GeoPoint3, double> parameter)
        {
            return points
                .OrderBy(parameter)
                .ThenBy(p => p.X)
                .ThenBy(p => p.Y)
                .ThenBy(p => p.Z)
                .ToArray();
        }
    }
}
