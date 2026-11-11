using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Drawing.Projection;
using GeometryHelper.Geometry;

namespace GeometryHelper.Benchmarks.Scenarios
{
    /// <summary>
    /// The hidden-line projection of a drawing's views (<see cref="ViewProjector"/>): the three views of an assembly of 50
    /// parts, and a frame of 1 000 members seen from above and from the front.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The assembly is five column bases side by side 6 000 apart, each a base plate 400 by 400 by 25 with four holes of 22,
    /// a column I 250 by 250 (flanges 14, web 9) 1 000 high, four stiffeners 10 thick and four bolts of 20, 80 long, every
    /// round faceted in 24: the TC5 of the drawing's test cases in a row, as TC10 lays them out. It is seen from above, from
    /// the front and from the right, the three views taken as one call. The frame is three storeys on a grid of 10 by 10 bays
    /// of 6 000: 300 columns HE 300, 540 beams IPE 400 and 160 floor plates 5 400 by 5 400 by 20. From the front its ten
    /// rows of columns and beams stand one behind another, every line of the first row falling on nine more behind it.
    /// </para>
    /// <para>
    /// Measured on the projection as agent projection left it, before it was made faster, the three views took 61 ms, the
    /// frame from above 220 and from the front 355; after, 28, 83 and 79, every view the same to the bit.
    /// </para>
    /// </remarks>
    internal static class DrawingProjectionScenarios
    {
        private const int HoleSegments = 24;

        private static readonly Tolerance Within = Tolerance.Default;

        private static readonly GeoCoordinateSystem3 Top = new GeoCoordinateSystem3(
            GeoPoint3.Origin,
            GeoVector3.XAxis,
            GeoVector3.YAxis
        );

        private static readonly GeoCoordinateSystem3 Front = new GeoCoordinateSystem3(
            GeoPoint3.Origin,
            GeoVector3.XAxis,
            GeoVector3.ZAxis
        );

        private static readonly GeoCoordinateSystem3 Right = new GeoCoordinateSystem3(
            GeoPoint3.Origin,
            GeoVector3.YAxis,
            GeoVector3.ZAxis
        );

        /// <summary>
        /// Every case of the projection.
        /// </summary>
        internal static IEnumerable<BenchmarkCase> All()
        {
            yield return new BenchmarkCase("Projection.FiftyParts", 50, "parts", false, FiftyParts);
            yield return new BenchmarkCase("Projection.Frame.Top", 1000, "members", false, () => Frame(Top));
            yield return new BenchmarkCase("Projection.Frame.Front", 1000, "members", false, () => Frame(Front));
        }

        /// <summary>
        /// The five column bases, seen from above, from the front and from the right.
        /// </summary>
        private static Fixture FiftyParts()
        {
            var parts = new List<ProjectionInput>();

            for (int column = 0; column < 5; column++)
            {
                parts.AddRange(
                    ColumnBase(
                        "TC10-000-" + column.ToString("000", CultureInfo.InvariantCulture),
                        new GeoVector3(6000.0 * column, 0.0, 0.0)
                    )
                );
            }

            ProjectionInput[] inputs = parts.ToArray();
            GeoCoordinateSystem3[] frames = { Top, Front, Right };

            return Fixture.Of(
                () => frames.Select(f => ViewProjector.Project(inputs, f, Within)).ToArray(),
                Sign,
                Describe,
                null
            );
        }

        /// <summary>
        /// The frame of 1 000 members, seen in one view.
        /// </summary>
        private static Fixture Frame(GeoCoordinateSystem3 frame)
        {
            ProjectionInput[] members = FrameMembers();

            return Fixture.Of(() => new[] { ViewProjector.Project(members, frame, Within) }, Sign, Describe, null);
        }

        /// <summary>
        /// A column base of TC5 moved along a vector: the base plate, the column, the four stiffeners and the four bolts.
        /// </summary>
        private static IEnumerable<ProjectionInput> ColumnBase(string prefix, GeoVector3 at)
        {
            GeoTransform3 move = GeoTransform3.Translation(at);
            var parts = new List<ProjectionInput>();
            GeoPoint2[] corners = { P(-150, -150), P(150, -150), P(-150, 150), P(150, 150) };
            var plate = new GeoFace2(
                Rectangle(-200, -200, 200, 200),
                corners.Select(c => Circle(c.X, c.Y, 11, HoleSegments)).ToArray()
            );
            parts.Add(
                new ProjectionInput(prefix + "-base", GeoSolid3.Extrude(plate, GeoCoordinateSystem3.Global, 25, Within))
            );

            var columnPlacement = new GeoCoordinateSystem3(
                new GeoPoint3(0, 0, 25),
                GeoVector3.YAxis.Negate(),
                GeoVector3.XAxis
            );
            parts.Add(
                new ProjectionInput(
                    prefix + "-column",
                    GeoSolid3.Extrude(ISection(250, 250, 14, 9), columnPlacement, 1000, Within)
                )
            );

            // Two stiffeners on the outsides of the flanges, in the world's XZ, and two on the sides of the web, in YZ.
            var inXz = new GeoCoordinateSystem3(new GeoPoint3(0, 5, 0), GeoVector3.XAxis, GeoVector3.ZAxis);
            var inYz = new GeoCoordinateSystem3(new GeoPoint3(-5, 0, 0), GeoVector3.YAxis, GeoVector3.ZAxis);
            GeoPolygon2[] flangeSides =
            {
                new GeoPolygon2(P(125, 25), P(200, 25), P(125, 175)),
                new GeoPolygon2(P(-200, 25), P(-125, 25), P(-125, 175)),
            };
            GeoPolygon2[] webSides =
            {
                new GeoPolygon2(P(4.5, 25), P(200, 25), P(4.5, 175)),
                new GeoPolygon2(P(-200, 25), P(-4.5, 25), P(-4.5, 175)),
            };

            for (int i = 0; i < 2; i++)
            {
                parts.Add(
                    new ProjectionInput(
                        prefix + "-stiffener-" + (i + 1),
                        GeoSolid3.Extrude(flangeSides[i], inXz, 10, Within)
                    )
                );
                parts.Add(
                    new ProjectionInput(
                        prefix + "-stiffener-" + (i + 3),
                        GeoSolid3.Extrude(webSides[i], inYz, 10, Within)
                    )
                );
            }

            for (int i = 0; i < corners.Length; i++)
            {
                var start = new GeoPoint3(corners[i].X, corners[i].Y, -30);
                parts.Add(
                    new ProjectionInput(
                        prefix + "-bolt-" + (i + 1),
                        GeoSolid3.Cylinder(
                            start,
                            new GeoPoint3(corners[i].X, corners[i].Y, 50),
                            10,
                            HoleSegments,
                            Within
                        )
                    )
                );
            }

            return parts.Select(p => new ProjectionInput(p.Id, p.Solid.TransformBy(move)));
        }

        /// <summary>
        /// The frame: per storey 100 columns HE 300, 3 600 high, 180 beams IPE 400, 5 700 long, and the floor plates.
        /// </summary>
        private static ProjectionInput[] FrameMembers()
        {
            var members = new List<ProjectionInput>();

            for (int level = 0; level < 3; level++)
            {
                double z0 = level * 4000.0;

                for (int i = 0; i < 10; i++)
                {
                    for (int j = 0; j < 10; j++)
                    {
                        double x = i * 6000.0,
                            y = j * 6000.0;
                        string at =
                            level.ToString(CultureInfo.InvariantCulture)
                            + "-"
                            + i.ToString("00", CultureInfo.InvariantCulture)
                            + "-"
                            + j.ToString("00", CultureInfo.InvariantCulture);
                        var up = new GeoCoordinateSystem3(new GeoPoint3(x, y, z0), GeoVector3.XAxis, GeoVector3.YAxis);
                        members.Add(
                            new ProjectionInput(
                                "C-" + at,
                                GeoSolid3.Extrude(ISection(300, 300, 19, 11), up, 3600, Within)
                            )
                        );

                        if (i < 9)
                        {
                            members.Add(
                                new ProjectionInput(
                                    "BX-" + at,
                                    Beam(new GeoPoint3(x + 150, y, z0 + 3800), GeoVector3.XAxis, 5700)
                                )
                            );
                        }

                        if (j < 9)
                        {
                            members.Add(
                                new ProjectionInput(
                                    "BY-" + at,
                                    Beam(new GeoPoint3(x, y + 150, z0 + 3800), GeoVector3.YAxis, 5700)
                                )
                            );
                        }
                    }
                }
            }

            int plates = 1000 - members.Count;

            for (int k = 0; k < plates; k++)
            {
                int i = k % 9,
                    j = (k / 9) % 9,
                    level = k / 81;
                double x = i * 6000.0 + 300,
                    y = j * 6000.0 + 300,
                    z = (level % 3) * 4000.0 + 4000;
                var corner = new GeoCoordinateSystem3(new GeoPoint3(x, y, z), GeoVector3.XAxis, GeoVector3.YAxis);
                members.Add(
                    new ProjectionInput(
                        "P-" + k.ToString("000", CultureInfo.InvariantCulture),
                        GeoSolid3.Extrude(Rectangle(0, 0, 5400, 5400), corner, 20, Within)
                    )
                );
            }

            return members.ToArray();
        }

        private static GeoSolid3 Beam(GeoPoint3 start, GeoVector3 along, double length)
        {
            GeoVector3 across = GeoVector3.ZAxis.CrossProduct(along);
            var placement = new GeoCoordinateSystem3(start, across, GeoVector3.ZAxis);
            return GeoSolid3.Extrude(ISection(400, 180, 13.5, 8.6), placement, length, Within);
        }

        /// <summary>An I-section centred on the origin: across along X, up along Y.</summary>
        private static GeoPolygon2 ISection(double depth, double width, double flange, double web)
        {
            double w = width / 2,
                d = depth / 2,
                t = web / 2,
                f = d - flange;
            return new GeoPolygon2(
                P(-w, -d),
                P(w, -d),
                P(w, -f),
                P(t, -f),
                P(t, f),
                P(w, f),
                P(w, d),
                P(-w, d),
                P(-w, f),
                P(-t, f),
                P(-t, -f),
                P(-w, -f)
            );
        }

        /// <summary>A circle of n corners, the first on the line of X from its centre, anticlockwise.</summary>
        private static GeoPolygon2 Circle(double x, double y, double radius, int corners) =>
            new GeoPolygon2(
                Enumerable
                    .Range(0, corners)
                    .Select(i =>
                        P(
                            x + radius * Math.Cos(2 * Math.PI * i / corners),
                            y + radius * Math.Sin(2 * Math.PI * i / corners)
                        )
                    )
            );

        private static GeoPolygon2 Rectangle(double x0, double y0, double x1, double y1) =>
            new GeoPolygon2(P(x0, y0), P(x1, y0), P(x1, y1), P(x0, y1));

        private static GeoPoint2 P(double x, double y) => new GeoPoint2(x, y);

        /// <summary>
        /// Signs views: how many, then of each the counts of its seen lines, hidden lines and section faces, and every line in
        /// order, its visibility, kind and objects and its ends, or its arc, to the last bit.
        /// </summary>
        private static void Sign(ResultSignature signature, ProjectedView2[] views)
        {
            signature.Add(views.Length);

            foreach (ProjectedView2 view in views)
            {
                signature.Add(view.VisibleEdges.Count);
                signature.Add(view.HiddenEdges.Count);
                signature.Add(view.SectionFaces.Count);

                foreach (ProjectedEdge2 edge in view.VisibleEdges.Concat(view.HiddenEdges))
                {
                    signature.Add((int)edge.Visibility);
                    signature.Add((int)edge.Kind);
                    signature.Add(edge.ObjectIds.Count);

                    foreach (string id in edge.ObjectIds)
                    {
                        signature.Add(id);
                    }

                    signature.Add(edge.IsArc);

                    if (edge.IsArc)
                    {
                        signature.Add(edge.Arc.Center);
                        signature.Add(edge.Arc.Radius);
                        signature.Add(edge.Arc.StartAngle);
                        signature.Add(edge.Arc.SweptAngle);
                    }
                    else
                    {
                        signature.Add(edge.Line.StartPoint);
                        signature.Add(edge.Line.EndPoint);
                    }
                }
            }
        }

        private static string Describe(ProjectedView2[] views) =>
            Report.Count(views.Sum(v => (long)v.VisibleEdges.Count))
            + " seen, "
            + Report.Count(views.Sum(v => (long)v.HiddenEdges.Count))
            + " hidden";
    }
}
