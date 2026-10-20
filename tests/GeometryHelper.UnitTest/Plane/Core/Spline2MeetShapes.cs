using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// Random shapes of each of the fourteen kinds a spline is asked about, drawn about a box on a seeded generator, with the library's
    /// object and the oracle's reading of the same numbers side by side.
    /// </summary>
    internal static class Spline2MeetShapes
    {
        internal static readonly string[] Kinds =
        {
            "segment", "ray", "xline", "polyline", "polygon", "rectangle", "triangle", "face", "arc", "circle", "ellipse", "edge", "chain", "loop"
        };

        private static double Between(Random r, double lo, double hi) => lo + (hi - lo) * r.NextDouble();

        private static GeoPoint2 P(double x, double y) => new GeoPoint2(x, y);

        /// <summary>
        /// Draws a star-shaped ring of corners about a centre: the corners at rising angles, each between a share of the radius and the radius.
        /// </summary>
        private static GeoPoint2[] Star(Random r, double cx, double cy, double radius, double inner, int corners)
        {
            var ring = new GeoPoint2[corners];

            for (int i = 0; i < corners; i++)
            {
                double angle = 2.0 * Math.PI * (i + Between(r, 0.0, 0.6)) / corners;
                double reach = radius * Between(r, inner, 1.0);
                ring[i] = P(Math.Round(cx + reach * Math.Cos(angle), 4), Math.Round(cy + reach * Math.Sin(angle), 4));
            }

            return ring;
        }

        private static void AddEdges(MeetShape shape, IReadOnlyList<GeoPoint2> ring, bool closed)
        {
            for (int i = 0; i + 1 < ring.Count; i++)
            {
                shape.Parts.Add(MeetSegment.Between(ring[i], ring[i + 1]));
            }

            if (closed)
            {
                shape.Parts.Add(MeetSegment.Between(ring[ring.Count - 1], ring[0]));
            }
        }

        /// <summary>
        /// Draws a shape of a kind about a centre, of a size.
        /// </summary>
        internal static MeetShape Draw(Random r, string kind, double cx, double cy, double size)
        {
            var shape = new MeetShape { Kind = kind };
            double ox = cx + Between(r, -size, size);
            double oy = cy + Between(r, -size, size);
            double angle = Between(r, 0.0, 2.0 * Math.PI);
            double radius = Between(r, 0.15, 1.0) * size;

            switch (kind)
            {
                case "segment":
                {
                    double length = Between(r, 0.1, 2.0) * size;
                    var a = P(ox, oy);
                    var b = P(ox + length * Math.Cos(angle), oy + length * Math.Sin(angle));
                    shape.Shape = new GeoLine2(a, b);
                    shape.Parts.Add(MeetSegment.Between(a, b));
                    break;
                }

                case "ray":
                    shape.Shape = new GeoRay2(P(ox, oy), new GeoVector2(Math.Cos(angle), Math.Sin(angle)));
                    shape.Parts.Add(new MeetSegment(ox, oy, Math.Cos(angle), Math.Sin(angle), 0.0, true, false));
                    break;

                case "xline":
                    shape.Shape = new GeoXLine2(P(ox, oy), new GeoVector2(Math.Cos(angle), Math.Sin(angle)));
                    shape.Parts.Add(new MeetSegment(ox, oy, Math.Cos(angle), Math.Sin(angle), 0.0, false, true));
                    break;

                case "polyline":
                {
                    GeoPoint2[] ring = Star(r, ox, oy, radius, 0.2, 2 + r.Next(5));
                    shape.Shape = new GeoPolyline2(ring);
                    AddEdges(shape, ((GeoPolyline2)shape.Shape).Vertices, false);
                    break;
                }

                case "polygon":
                {
                    GeoPoint2[] ring = Star(r, ox, oy, radius, 0.4, 3 + r.Next(5));
                    var polygon = new GeoPolygon2(ring);
                    shape.Shape = polygon;
                    AddEdges(shape, polygon.Vertices, true);
                    shape.Inside = (x, y) => Spline2MeetOracle.InRing(polygon.Vertices, x, y);
                    break;
                }

                case "rectangle":
                {
                    double w = Between(r, 0.3, 1.2) * size;
                    double h = Between(r, 0.3, 1.2) * size;
                    var rect = new GeoRectangle2(P(ox, oy), w, h, angle);
                    shape.Shape = rect;
                    double c = Math.Cos(angle);
                    double s = Math.Sin(angle);
                    var corners = new[]
                    {
                        P(ox + (-w / 2) * c - (-h / 2) * s, oy + (-w / 2) * s + (-h / 2) * c),
                        P(ox + (w / 2) * c - (-h / 2) * s, oy + (w / 2) * s + (-h / 2) * c),
                        P(ox + (w / 2) * c - (h / 2) * s, oy + (w / 2) * s + (h / 2) * c),
                        P(ox + (-w / 2) * c - (h / 2) * s, oy + (-w / 2) * s + (h / 2) * c)
                    };
                    AddEdges(shape, corners, true);
                    shape.Inside = (x, y) => Spline2MeetOracle.InRing(corners, x, y);
                    break;
                }

                case "triangle":
                {
                    GeoPoint2[] ring = Star(r, ox, oy, radius, 0.5, 3);
                    var triangle = new GeoTriangle2(ring[0], ring[1], ring[2]);
                    shape.Shape = triangle;
                    AddEdges(shape, ring, true);
                    shape.Inside = (x, y) => Spline2MeetOracle.InRing(ring, x, y);
                    break;
                }

                case "face":
                {
                    GeoPoint2[] outer = Star(r, ox, oy, radius, 0.5, 3 + r.Next(4));
                    GeoPoint2[] hole = Star(r, ox, oy, radius * 0.3, 0.5, 3 + r.Next(3));
                    var face = new GeoFace2(new GeoPolygon2(outer), new[] { new GeoPolygon2(hole) });
                    shape.Shape = face;
                    AddEdges(shape, face.Boundary.Vertices, true);
                    AddEdges(shape, face.Holes[0].Vertices, true);
                    shape.Inside = (x, y) => Spline2MeetOracle.InRing(face.Boundary.Vertices, x, y) && !Spline2MeetOracle.InRing(face.Holes[0].Vertices, x, y);
                    break;
                }

                case "arc":
                {
                    double sweep = Between(r, 0.3, 2.0 * Math.PI - 0.3) * (r.Next(2) == 0 ? 1.0 : -1.0);
                    var arc = new GeoArc2(P(ox, oy), radius, angle, angle + sweep, sweep < 0.0);
                    shape.Shape = arc;
                    shape.Parts.Add(new MeetRound(ox, oy, radius, arc.StartAngle, arc.SweptAngle, false));
                    break;
                }

                case "circle":
                    shape.Shape = new GeoCircle2(P(ox, oy), radius);
                    shape.Parts.Add(new MeetRound(ox, oy, radius, 0.0, 2.0 * Math.PI, true));
                    shape.Inside = (x, y) => Math.Sqrt((x - ox) * (x - ox) + (y - oy) * (y - oy)) <= radius;
                    break;

                case "ellipse":
                {
                    double b = radius * Between(r, 0.1, 1.0);
                    double ux = Math.Cos(angle);
                    double uy = Math.Sin(angle);
                    shape.Shape = new GeoEllipse2(P(ox, oy), new GeoVector2(ux, uy), radius, b);
                    shape.Parts.Add(new MeetEllipse(ox, oy, ux, uy, radius, b));
                    shape.Inside = (x, y) =>
                    {
                        double along = ((x - ox) * ux + (y - oy) * uy) / radius;
                        double across = ((y - oy) * ux - (x - ox) * uy) / b;

                        return along * along + across * across <= 1.0;
                    };
                    break;
                }

                case "edge":
                {
                    var a = P(ox, oy);
                    var b = P(ox + radius * Math.Cos(angle), oy + radius * Math.Sin(angle));
                    double bulge = r.Next(2) == 0 ? 0.0 : Between(r, -1.5, 1.5);

                    if (Math.Abs(bulge) < 0.05)
                    {
                        bulge = 0.0;
                    }

                    shape.Shape = new GeoEdge2(a, b, bulge);
                    shape.Parts.Add(bulge == 0.0 ? (MeetPart)MeetSegment.Between(a, b) : MeetRound.OfBulge(a, b, bulge));
                    break;
                }

                case "chain":
                {
                    GeoPoint2[] ring = Star(r, ox, oy, radius, 0.3, 2 + r.Next(4));
                    double[] bulges = Enumerable.Range(0, ring.Length - 1).Select(i => r.Next(3) == 0 ? 0.0 : Between(r, -1.2, 1.2)).ToArray();
                    shape.Shape = new GeoPolylineArc2(ring, bulges);
                    AddCurved(shape, ring, bulges, false);
                    break;
                }

                default:
                {
                    GeoPoint2[] ring = Star(r, ox, oy, radius, 0.5, 3 + r.Next(4));
                    double[] bulges = Enumerable.Range(0, ring.Length).Select(i => r.Next(3) == 0 ? 0.0 : Between(r, -0.4, 0.4)).ToArray();
                    var loop = new GeoPolygonArc2(ring, bulges);
                    shape.Shape = loop;
                    AddCurved(shape, ring, bulges, true);

                    // The material of a loop: the polygon of the corners, with each bulge's arc cut finely.
                    var outline = new List<GeoPoint2>();

                    for (int i = 0; i < ring.Length; i++)
                    {
                        GeoPoint2 a = ring[i];
                        GeoPoint2 b = ring[(i + 1) % ring.Length];
                        outline.Add(a);

                        if (bulges[i] != 0.0)
                        {
                            MeetRound round = MeetRound.OfBulge(a, b, bulges[i]);

                            for (int k = 1; k < 300; k++)
                            {
                                double angleAt = 4.0 * Math.Atan(bulges[i]) * k / 300.0;
                                outline.Add(Along(a, b, bulges[i], angleAt));
                            }

                            _ = round;
                        }
                    }

                    shape.Inside = (x, y) => Spline2MeetOracle.InRing(outline, x, y);
                    break;
                }
            }

            return shape;
        }

        /// <summary>
        /// Gets the point of the arc of a bulge between two points that has swept an angle from the first.
        /// </summary>
        private static GeoPoint2 Along(GeoPoint2 a, GeoPoint2 b, double bulge, double swept)
        {
            double angle = 4.0 * Math.Atan(bulge);
            double chord = Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
            double radius = chord / (2.0 * Math.Sin(Math.Abs(angle) / 2.0));
            double nx = -(b.Y - a.Y) / chord;
            double ny = (b.X - a.X) / chord;
            double offset = radius * Math.Cos(Math.Abs(angle) / 2.0) * (bulge > 0.0 ? 1.0 : -1.0);
            double cx = 0.5 * (a.X + b.X) + nx * offset;
            double cy = 0.5 * (a.Y + b.Y) + ny * offset;
            double start = Math.Atan2(a.Y - cy, a.X - cx);

            return P(cx + radius * Math.Cos(start + swept), cy + radius * Math.Sin(start + swept));
        }

        private static void AddCurved(MeetShape shape, GeoPoint2[] ring, double[] bulges, bool closed)
        {
            int edges = closed ? ring.Length : ring.Length - 1;

            for (int i = 0; i < edges; i++)
            {
                GeoPoint2 a = ring[i];
                GeoPoint2 b = ring[(i + 1) % ring.Length];
                shape.Parts.Add(Math.Abs(bulges[i]) < 1E-12 ? (MeetPart)MeetSegment.Between(a, b) : MeetRound.OfBulge(a, b, bulges[i]));
            }
        }
    }
}
