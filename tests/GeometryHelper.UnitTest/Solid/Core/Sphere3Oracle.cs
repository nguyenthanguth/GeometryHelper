using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// What a ball does to a shape in space, worked out in closed form or by dense sampling from the shape's own numbers and sharing
    /// no code with the library's ball: the distance from the centre to the shape's material, and where the surface of the ball meets the
    /// shape's pieces.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The distance is that of the centre to the shape read as the library reads it: a point, a curve, a plane, a flat region and a disc
    /// as filled, a body as its material. A ball is a point grown by its radius, so the gap is that distance less the radius, and the
    /// shortest segment runs from the surface to the point of the shape nearest the centre. The pieces come from
    /// <see cref="XLineSpecimen"/>, whose distances from a point are closed forms or dense samples refined by golden section; a disc is
    /// the plane's height and the planar distance to the rim, a body is nought where a ray from the point crosses its faces an odd
    /// number of times.
    /// </para>
    /// <para>
    /// The surface of a ball meets a segment, a ray or a line where a quadratic has its roots, and a curve where the distance from the
    /// centre less the radius changes sign, found on 20 000 samples along it and bisected. A pair is <i>delicate</i> where the
    /// tolerance, not the geometry, decides what is met: a line that grazes the surface, a curve that dips to within four thousandths of
    /// it without crossing, a crossing within four thousandths of an end or of another, an end within four thousandths of the surface.
    /// Its crossings are not judged.
    /// </para>
    /// </remarks>
    internal static class Sphere3Oracle
    {
        /// <summary>The band round the point tolerance that the answer may not be in for the tolerance not to decide it.</summary>
        internal const double Band = 4E-3;

        /// <summary>One ring of a flat region or a body, its corners as the oracle holds them.</summary>
        private sealed class Face
        {
            internal Vec[] Outline;
            internal Vec[][] Holes;
            internal Vec Normal;
            internal int Drop;

            internal Face(IReadOnlyList<GeoPoint3> outline, IEnumerable<IReadOnlyList<GeoPoint3>> holes)
            {
                Outline = outline.Select(Vec.Of).ToArray();
                Holes = (holes ?? Enumerable.Empty<IReadOnlyList<GeoPoint3>>()).Select(h => h.Select(Vec.Of).ToArray()).ToArray();

                Vec sum = new Vec(0, 0, 0);

                for (int i = 0; i < Outline.Length; i++)
                {
                    Vec a = Outline[i];
                    Vec b = Outline[(i + 1) % Outline.Length];

                    sum = sum + new Vec((a.Y - b.Y) * (a.Z + b.Z), (a.Z - b.Z) * (a.X + b.X), (a.X - b.X) * (a.Y + b.Y));
                }

                Normal = sum.Unit();
                Drop = Math.Abs(Normal.X) >= Math.Abs(Normal.Y) && Math.Abs(Normal.X) >= Math.Abs(Normal.Z) ? 0 : Math.Abs(Normal.Y) >= Math.Abs(Normal.Z) ? 1 : 2;
            }

            private double U(Vec p) => Drop == 0 ? p.Y : Drop == 1 ? p.Z : p.X;

            private double W(Vec p) => Drop == 0 ? p.Z : Drop == 1 ? p.X : p.Y;

            private bool InRing(Vec[] ring, Vec p)
            {
                bool inside = false;

                for (int i = 0, j = ring.Length - 1; i < ring.Length; j = i++)
                {
                    double ui = U(ring[i]), wi = W(ring[i]), uj = U(ring[j]), wj = W(ring[j]);

                    if ((wi > W(p)) != (wj > W(p)) && U(p) < (uj - ui) * (W(p) - wi) / (wj - wi) + ui)
                    {
                        inside = !inside;
                    }
                }

                return inside;
            }

            internal bool Holds(Vec p) => InRing(Outline, p) && !Holds2(p);

            private bool Holds2(Vec p) => Holes.Any(h => InRing(h, p));

            /// <summary>The distance along a direction from a point at which it meets the plane of the face inside its outline, or null.</summary>
            internal double? Hit(Vec from, Vec direction)
            {
                double den = Normal.Dot(direction);

                if (Math.Abs(den) < 1E-12)
                {
                    return null;
                }

                double t = Normal.Dot(Outline[0] - from) / den;

                return t > 0 && Holds(from + direction * t) ? t : (double?)null;
            }
        }

        /// <summary>Whether a point is inside a body, by the parity of the crossings of a ray from it with the faces of the body.</summary>
        private static bool Inside(Vec p, IEnumerable<GeoFace3> faces)
        {
            Face[] ours = faces.Select(f => new Face(f.Boundary.Vertices, f.Holes.Select(h => h.Vertices))).ToArray();

            // Three rays in directions no shape is drawn along; the body is taken as holding the point where two of the three say so.
            Vec[] rays = { new Vec(0.5773, 0.3217, 0.7501).Unit(), new Vec(-0.2371, 0.8123, -0.5301).Unit(), new Vec(0.1117, -0.4431, 0.8891).Unit() };
            int votes = 0;

            foreach (Vec ray in rays)
            {
                int hits = 0;

                foreach (Face face in ours)
                {
                    if (face.Hit(p, ray).HasValue)
                    {
                        hits++;
                    }
                }

                votes += hits % 2;
            }

            return votes >= 2;
        }

        /// <summary>
        /// Whether a drawn shape is one the library promises to handle: the generator draws a face with a hole about the centre of a
        /// star-shaped outline, and an outline of three corners may not hold that centre, so that the hole pokes out of the face.
        /// </summary>
        internal static bool IsSound(XLineSpecimen specimen)
        {
            if (!(specimen.Shape is GeoFace3 face))
            {
                return true;
            }

            var outline = new Face(face.Boundary.Vertices, null);

            return face.Holes.All(h => h.Vertices.All(v => outline.Holds(Vec.Of(v))));
        }

        /// <summary>The distance from a point to a disc: the height over its plane, and the planar distance to the rim where the foot is outside.</summary>
        private static double ToDisc(Vec p, Vec center, Vec normal, Func<Vec, bool> holds, Func<Vec, double> toRim)
        {
            double height = normal.Dot(p - center);
            Vec foot = p - normal * height;

            return holds(foot) ? Math.Abs(height) : Math.Sqrt(height * height + toRim(foot) * toRim(foot));
        }

        /// <summary>The distance from the centre of a ball to the shape of a specimen, the shape read as filled where the library reads it so.</summary>
        internal static double Reading(XLineSpecimen specimen, Vec center)
        {
            switch (specimen.Kind)
            {
                case "Circle3":
                {
                    var circle = (GeoCircle3)specimen.Shape;
                    Vec c = Vec.Of(circle.Center);
                    Vec n = Vec.Of(circle.Normal).Unit();

                    return ToDisc(center, c, n, f => (f - c).Length <= circle.Radius, f => Math.Abs((f - c).Length - circle.Radius));
                }

                case "Ellipse3":
                {
                    var ellipse = (GeoEllipse3)specimen.Shape;
                    Vec c = Vec.Of(ellipse.Center);
                    Vec n = Vec.Of(ellipse.Normal).Unit();
                    Vec m = Vec.Of(ellipse.MajorAxis).Unit();
                    Vec w = n.Cross(m);

                    return ToDisc(
                        center,
                        c,
                        n,
                        f => { double x = (f - c).Dot(m) / ellipse.MajorRadius; double y = (f - c).Dot(w) / ellipse.MinorRadius; return x * x + y * y <= 1.0; },
                        f => specimen.Pieces[0].GapFromPoint(f));
                }

                case "Aabb3":
                {
                    var box = (GeoAabb3)specimen.Shape;
                    Vec low = Vec.Of(box.Min);
                    Vec high = Vec.Of(box.Max);
                    GeoPoint3 middle = ((low + high) * 0.5).ToPoint();

                    return Inside(center, new GeoObb3(middle, high.X - low.X, high.Y - low.Y, high.Z - low.Z).ToSolid().Faces) ? 0.0 : specimen.DistanceFrom(center);
                }

                case "Obb3":
                    return Inside(center, ((GeoObb3)specimen.Shape).ToSolid().Faces) ? 0.0 : specimen.DistanceFrom(center);

                case "Solid3":
                    return Inside(center, ((GeoSolid3)specimen.Shape).Faces) ? 0.0 : specimen.DistanceFrom(center);

                default:
                    return specimen.DistanceFrom(center);
            }
        }

        /// <summary>One piece of a shape's curve or boundary that a surface can cross.</summary>
        internal abstract class Element
        {
            /// <summary>The points where the sphere about a centre meets the element, in no order.</summary>
            internal abstract List<Vec> Meetings(Vec center, double radius, ref bool delicate);
        }

        /// <summary>A segment, a ray or a line, given by a point, a unit direction and the range of distances along it.</summary>
        internal sealed class Straight : Element
        {
            private readonly Vec _o;
            private readonly Vec _d;
            private readonly double _from;
            private readonly double _to;

            internal Straight(Vec a, Vec b)
            {
                _o = a;
                _d = (b - a).Unit();
                _from = 0.0;
                _to = (b - a).Length;
            }

            internal Straight(Vec origin, Vec direction, double from, double to)
            {
                _o = origin;
                _d = direction.Unit();
                _from = from;
                _to = to;
            }

            internal override List<Vec> Meetings(Vec center, double radius, ref bool delicate)
            {
                var found = new List<Vec>();
                Vec w = _o - center;
                double b = w.Dot(_d);
                Vec perpendicular = w - _d * b;
                double rho = perpendicular.Length;

                if (Math.Abs(rho - radius) <= Band)
                {
                    delicate = true;
                }

                if (rho > radius)
                {
                    return found;
                }

                double half = Math.Sqrt(radius * radius - rho * rho);

                foreach (double t in new[] { -b - half, -b + half })
                {
                    if (!double.IsInfinity(_from) && Math.Abs(t - _from) <= Band || !double.IsInfinity(_to) && Math.Abs(t - _to) <= Band)
                    {
                        delicate = true;
                    }

                    if (t >= _from && t <= _to)
                    {
                        found.Add(_o + _d * t);
                    }
                }

                foreach (double end in new[] { _from, _to })
                {
                    if (!double.IsInfinity(end) && Math.Abs((_o + _d * end - center).Length - radius) <= Band)
                    {
                        delicate = true;
                    }
                }

                return found;
            }
        }

        /// <summary>A curve given by a point at a parameter between 0 and 1, closed or not.</summary>
        internal sealed class Parametric : Element
        {
            private readonly Func<double, Vec> _at;
            private readonly bool _closed;

            internal Parametric(Func<double, Vec> at, bool closed)
            {
                _at = at;
                _closed = closed;
            }

            internal override List<Vec> Meetings(Vec center, double radius, ref bool delicate)
            {
                const int N = 20000;
                var found = new List<Vec>();
                var g = new double[N + 1];
                var points = new Vec[N + 1];
                double length = 0.0;

                for (int i = 0; i <= N; i++)
                {
                    points[i] = _at((double)i / N);
                    g[i] = (points[i] - center).Length - radius;

                    if (i > 0)
                    {
                        length += (points[i] - points[i - 1]).Length;
                    }
                }

                var roots = new List<double>();

                for (int i = 0; i < N; i++)
                {
                    if (g[i] == 0.0 || (g[i] < 0.0) != (g[i + 1] < 0.0))
                    {
                        double lo = (double)i / N;
                        double hi = (double)(i + 1) / N;
                        double glo = g[i];

                        for (int k = 0; k < 80; k++)
                        {
                            double mid = 0.5 * (lo + hi);
                            double gm = (_at(mid) - center).Length - radius;

                            if ((gm < 0.0) == (glo < 0.0))
                            {
                                lo = mid;
                                glo = gm;
                            }
                            else
                            {
                                hi = mid;
                            }
                        }

                        roots.Add(0.5 * (lo + hi));
                    }
                }

                // Near tangent: the distance from the surface has a local least that is within the band and does not change sign.
                for (int i = 1; i < N; i++)
                {
                    double a = Math.Abs(g[i]);

                    if (a <= Band && a <= Math.Abs(g[i - 1]) && a <= Math.Abs(g[i + 1]) && (g[i - 1] < 0.0) == (g[i] < 0.0) && (g[i + 1] < 0.0) == (g[i] < 0.0))
                    {
                        delicate = true;
                    }
                }

                if (!_closed)
                {
                    foreach (int end in new[] { 0, N })
                    {
                        if (Math.Abs(g[end]) <= Band)
                        {
                            delicate = true;
                        }
                    }

                    foreach (double root in roots)
                    {
                        if (Math.Min(root, 1.0 - root) * length <= 2.0 * Band)
                        {
                            delicate = true;
                        }
                    }
                }

                for (int i = 0; i + 1 < roots.Count; i++)
                {
                    if ((roots[i + 1] - roots[i]) * length <= 4.0 * Band)
                    {
                        delicate = true;
                    }
                }

                if (_closed && roots.Count > 1 && (roots[0] + 1.0 - roots[roots.Count - 1]) * length <= 4.0 * Band)
                {
                    delicate = true;
                }

                // A closed curve's start is not an end, but a root there is found twice.
                foreach (double root in roots)
                {
                    Vec point = _at(root);

                    if (!found.Any(f => f.DistanceTo(point) <= 1E-9))
                    {
                        found.Add(point);
                    }
                }

                return found;
            }
        }

        private static IEnumerable<Element> Segments(IReadOnlyList<GeoPoint3> ring, bool closed)
        {
            int edges = closed ? ring.Count : ring.Count - 1;

            for (int i = 0; i < edges; i++)
            {
                yield return new Straight(Vec.Of(ring[i]), Vec.Of(ring[(i + 1) % ring.Count]));
            }
        }

        /// <summary>An arc from its centre, normal, start point and signed sweep, as the oracle's own pieces turn: counter-clockwise about the normal.</summary>
        private static Element OfArc(GeoArc3 arc)
        {
            Vec c = Vec.Of(arc.Center);
            Vec n = Vec.Of(arc.Normal).Unit();
            Vec s = Vec.Of(arc.StartPoint) - c;
            Vec t = n.Cross(s);
            double sweep = arc.SweptAngle;

            return new Parametric(u => c + s * Math.Cos(u * sweep) + t * Math.Sin(u * sweep), false);
        }

        private static IEnumerable<Element> OfEdge(GeoEdge3 edge)
        {
            yield return edge.IsArc ? OfArc(edge.ToArc()) : new Straight(Vec.Of(edge.StartPoint), Vec.Of(edge.EndPoint));
        }

        /// <summary>The elements of a shape whose surface crossings are asked, or null where none are: a point, a plane, a box and a body.</summary>
        internal static List<Element> Elements(XLineSpecimen specimen)
        {
            switch (specimen.Shape)
            {
                case GeoLine3 line:
                    return new List<Element> { new Straight(Vec.Of(line.StartPoint), Vec.Of(line.EndPoint)) };
                case GeoRay3 ray:
                    return new List<Element> { new Straight(Vec.Of(ray.Origin), Vec.Of(ray.Direction), 0.0, double.PositiveInfinity) };
                case GeoXLine3 xline:
                    return new List<Element> { new Straight(Vec.Of(xline.Origin), Vec.Of(xline.Direction), double.NegativeInfinity, double.PositiveInfinity) };
                case GeoArc3 arc:
                    return new List<Element> { OfArc(arc) };
                case GeoCircle3 circle:
                {
                    Vec c = Vec.Of(circle.Center);
                    Vec s = Vec.Of(circle.GetPointAtParameter(0.0)) - c;
                    Vec t = Vec.Of(circle.Normal).Unit().Cross(s);

                    return new List<Element> { new Parametric(u => c + s * Math.Cos(2.0 * Math.PI * u) + t * Math.Sin(2.0 * Math.PI * u), true) };
                }

                case GeoEllipse3 ellipse:
                {
                    Vec c = Vec.Of(ellipse.Center);
                    Vec major = Vec.Of(ellipse.MajorAxis).Unit();
                    Vec minor = Vec.Of(ellipse.Normal).Unit().Cross(major);

                    return new List<Element>
                    {
                        new Parametric(u => c + major * (ellipse.MajorRadius * Math.Cos(2.0 * Math.PI * u)) + minor * (ellipse.MinorRadius * Math.Sin(2.0 * Math.PI * u)), true)
                    };
                }

                case GeoEdge3 edge:
                    return OfEdge(edge).ToList();
                case GeoPolyline3 polyline:
                    return Segments(polyline.Vertices, false).ToList();
                case GeoPolygon3 polygon:
                    return Segments(polygon.Vertices, true).ToList();
                case GeoTriangle3 triangle:
                    return Segments(new[] { triangle.A, triangle.B, triangle.C }, true).ToList();
                case GeoFace3 face:
                    return Segments(face.Boundary.Vertices, true).Concat(face.Holes.SelectMany(h => Segments(h.Vertices, true))).ToList();
                case GeoPolylineArc3 chain:
                    return chain.GetEdges().SelectMany(OfEdge).ToList();
                case GeoPolygonArc3 loop:
                    return loop.GetEdges().SelectMany(OfEdge).ToList();
                default:
                    return null;
            }
        }

        /// <summary>The points where the surface of a ball meets a shape, each once, and whether the tolerance rather than the geometry decides them.</summary>
        internal static Vec[] Crossings(List<Element> elements, Vec center, double radius, out bool delicate)
        {
            delicate = false;
            var found = new List<Vec>();

            foreach (Element element in elements)
            {
                foreach (Vec point in element.Meetings(center, radius, ref delicate))
                {
                    if (!found.Any(f => f.DistanceTo(point) <= 1E-6))
                    {
                        found.Add(point);
                    }
                }
            }

            // Two crossings within the point tolerance of each other are one to the library.
            for (int i = 0; i < found.Count; i++)
            {
                for (int j = i + 1; j < found.Count; j++)
                {
                    if (found[i].DistanceTo(found[j]) <= 2.0 * Band)
                    {
                        delicate = true;
                    }
                }
            }

            return found.ToArray();
        }
    }
}
