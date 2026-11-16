using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>BLEND in AutoCAD 2021's words: a spline between the ends of two open curves, with Tangent or Smooth continuity.</summary>
    internal static partial class CadCommands
    {
        /// <summary>
        /// BLEND: "Continuity = Tangent", "Select first object or [CONtinuity]:" and "Select second object:", each pick taking the
        /// end of an open line, arc, elliptical arc, polyline or spline nearest the point picked (a circle, a closed curve or
        /// anything else is "Cannot BLEND this object." and asked again), and the spline from the one end to the other made on the
        /// current layer. CONtinuity asks "Enter continuity [Tangent/Smooth] &lt;Tangent&gt;:" and keeps the answer for the next BLEND.
        /// Tangent makes the cubic that leaves the first end along the first curve's direction and arrives along the second's,
        /// a third of the distance between the ends from each end to its handle, as AutoCAD's does to the digit (the lines
        /// (0, 0) to (30, 0) and (50, 20) to (80, 20), picked at their near ends, give the cubic from (30, 0) by (39.4281, 0) and
        /// (40.5719, 20) to (50, 20)). Smooth makes the degree 5 spline that also takes the curvature of each curve at its end
        /// (a line's nought, an arc's one over its radius), its control points the ones along the curve a fifth of the distance
        /// apart; AutoCAD's own Smooth spline has other control points, running back along the first curve before it turns. The
        /// same curve twice is "Cannot blend an entity with itself at the specified location.".
        /// </summary>
        private static IEnumerable<CadPrompt> Blend(CadCommandContext c)
        {
            BlendEnd first = null;
            bool again = false;

            while (first == null)
            {
                if (!again)
                {
                    c.Write("Continuity = " + (BlendSmooth(c) ? "Smooth" : "Tangent"));
                }

                again = true;
                yield return CadPrompt.ForEntity("Select first object", new[] { "CONtinuity" });

                if (c.Input.Is("CONtinuity"))
                {
                    yield return CadPrompt.ForKeyword(
                        "Enter continuity",
                        new[] { "Tangent", "Smooth" },
                        BlendSmooth(c) ? "Smooth" : "Tangent"
                    );

                    if (c.Input.Is("Tangent") || c.Input.Is("Smooth"))
                    {
                        c.Remember("blend.smooth", c.Input.Is("Smooth") ? 1.0 : 0.0);
                    }

                    continue;
                }

                if (c.Input.Kind != CadInputKind.Selection || c.Input.Handles.Count == 0)
                {
                    yield break;
                }

                first = BlendPicked(c, c.Input.Handles[0], c.Input.Point);

                if (first == null)
                {
                    c.Write("Cannot BLEND this object.");
                }
            }

            BlendEnd second = null;

            while (second == null)
            {
                BlendEnd from = first;
                yield return CadPrompt
                    .ForEntity("Select second object")
                    .Previewing(
                        CadPreview.MakingOne(c, input =>
                            input.Kind == CadInputKind.Selection
                            && input.Handles.Count > 0
                            && BlendPicked(c, input.Handles[0], input.Point) is BlendEnd other
                            && !string.Equals(other.Handle, from.Handle, StringComparison.OrdinalIgnoreCase)
                                ? BlendSpline(c, from, other, BlendSmooth(c))
                                : null
                        )
                    );

                if (c.Input.Kind != CadInputKind.Selection || c.Input.Handles.Count == 0)
                {
                    yield break;
                }

                second = BlendPicked(c, c.Input.Handles[0], c.Input.Point);

                if (second == null)
                {
                    c.Write("Cannot BLEND this object.");
                }
                else if (string.Equals(second.Handle, first.Handle, StringComparison.OrdinalIgnoreCase))
                {
                    c.Write("Cannot blend an entity with itself at the specified location.");
                    second = null;
                }
            }

            DxfSpline blend = BlendSpline(c, first, second, BlendSmooth(c));

            if (blend == null)
            {
                c.Write("*Invalid* The ends of the two objects are one point.");
                yield break;
            }

            c.Apply(null, s => s.Append(c.Space, blend));
        }

        private static bool BlendSmooth(CadCommandContext c) => c.Remembered("blend.smooth", 0.0) != 0.0;

        /// <summary>The end of a curve a blend runs from: the point, the direction leaving the curve there, and its curvature vector.</summary>
        private sealed class BlendEnd
        {
            public string Handle;
            public GeoPoint3 Point;
            public GeoVector3 Outward;
            public GeoVector3 Bend;
        }

        /// <summary>
        /// The end of the object picked that lies nearest the point picked (the last one where they are as near), or null where
        /// the object is not an open line, arc, ellipse, polyline or spline that has a direction there.
        /// </summary>
        private static BlendEnd BlendPicked(CadCommandContext c, string handle, GeoPoint2 pick)
        {
            DxfEntity entity = c.Document.FindEntity(handle);

            if (
                !(
                    entity is DxfLine
                    || entity is DxfArc
                    || entity is DxfEllipse
                    || entity is DxfPolyline
                    || entity is DxfPolyline2d
                    || entity is DxfPolyline3d
                    || entity is DxfSpline
                ) || !(entity is DxfCurve curve)
            )
            {
                return null;
            }

            try
            {
                if (curve.Closed)
                {
                    return null;
                }

                GeoPoint3 at = P3(pick);
                bool atStart = curve.StartPoint.DistanceTo(at) < curve.EndPoint.DistanceTo(at);
                double parameter = atStart ? curve.StartParam : curve.EndParam;
                GeoVector3 speed = curve.GetFirstDerivative(parameter);

                if (!(speed.Length > 0.0))
                {
                    return null;
                }

                GeoVector3 along = speed / speed.Length;
                GeoVector3 bend = new GeoVector3(0.0, 0.0, 0.0);

                try
                {
                    GeoVector3 acceleration = curve.GetSecondDerivative(parameter);
                    GeoVector3 across = acceleration - along * acceleration.DotProduct(along);
                    bend = across / speed.LengthSquared;
                }
                catch (NotSupportedException) { }

                return new BlendEnd
                {
                    Handle = handle,
                    Point = atStart ? curve.StartPoint : curve.EndPoint,
                    Outward = atStart ? -along : along,
                    Bend = bend,
                };
            }
            catch (Exception e)
                when (e is NotSupportedException || e is InvalidOperationException || e is ArgumentException)
            {
                return null;
            }
        }

        /// <summary>
        /// The spline from one end to the other (see <see cref="Blend"/>): the cubic Bézier with knots 0 to the distance between
        /// the ends, or the quintic one over 0 to 1 with the curvature of each end; null where the ends are one point.
        /// </summary>
        private static DxfSpline BlendSpline(CadCommandContext c, BlendEnd from, BlendEnd to, bool smooth)
        {
            GeoVector3 way = from.Point.GetVectorTo(to.Point);
            double length = way.Length;

            if (!(length > c.Session.Tolerance.EqualPoint))
            {
                return null;
            }

            var points = new List<GeoPoint3>();
            var knots = new List<double>();
            int degree;

            if (smooth)
            {
                double step = length / 5.0;
                double bend = length * length / 20.0;
                GeoPoint3 p1 = from.Point + from.Outward * step;
                GeoPoint3 p4 = to.Point + to.Outward * step;
                points.Add(from.Point);
                points.Add(p1);
                points.Add(p1 + from.Outward * step + from.Bend * bend);
                points.Add(p4 + to.Outward * step + to.Bend * bend);
                points.Add(p4);
                points.Add(to.Point);
                degree = 5;
                knots.AddRange(new[] { 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0 });
            }
            else
            {
                double handle = length / 3.0;
                points.Add(from.Point);
                points.Add(from.Point + from.Outward * handle);
                points.Add(to.Point + to.Outward * handle);
                points.Add(to.Point);
                degree = 3;
                knots.AddRange(new[] { 0.0, 0.0, 0.0, 0.0, length, length, length, length });
            }

            return Current(c, new DxfSpline(degree, false, false, false, points, knots, null, 1E-10, 1E-10));
        }
    }
}
