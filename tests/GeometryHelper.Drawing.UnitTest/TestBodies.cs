using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Geometry;

namespace GeometryHelper.Drawing.UnitTest
{
    /// <summary>
    /// One object of a test body with the numbers worked out for it by hand: its volume, its edges, and the centres of the
    /// holes through it.
    /// </summary>
    internal sealed class TestPart
    {
        public TestPart(DrawingObject drawingObject, double volume, int edges, int smoothEdges, double holeDiameter, params GeoPoint3[] holeCentres)
        {
            Object = drawingObject;
            Volume = volume;
            Edges = edges;
            SmoothEdges = smoothEdges;
            HoleDiameter = holeDiameter;
            HoleCentres = holeCentres;
        }

        public DrawingObject Object { get; }

        /// <summary>The volume of the faceted body, worked out from its outline.</summary>
        public double Volume { get; }

        /// <summary>
        /// Every edge of the body, as many as the faces of the body read with coplanar neighbours merged and corners on a
        /// straight line dropped have: an extruded outline of n corners has 3n.
        /// </summary>
        public int Edges { get; }

        /// <summary>
        /// The edges between two facets of one round surface, the walls of a hole or a tube or the length of a bar,
        /// which a view draws only where they are a silhouette. The rings where a stirrup bends are not among them: they
        /// are where two straight lengths of bar meet, though at a bend of 90 degrees some of their facets meet at less
        /// than the angle between facets.
        /// </summary>
        public int SmoothEdges { get; }

        /// <summary>The diameter of the holes through the body; nought where it has none.</summary>
        public double HoleDiameter { get; }

        /// <summary>The centre of each hole on its axis, half way through the body.</summary>
        public IReadOnlyList<GeoPoint3> HoleCentres { get; }

        public TestPart TransformBy(GeoTransform3 transform)
        {
            DrawingObject o = Object;
            var moved = new DrawingObject(o.Id, o.Kind, o.Solid.TransformBy(transform), o.Frame.TransformBy(transform), o.Name, o.Mark, o.Profile, o.Material, o.ParentId, o.IsMain, o.Attributes);
            return new TestPart(moved, Volume, Edges, SmoothEdges, HoleDiameter, HoleCentres.Select(c => c.TransformBy(transform)).ToArray());
        }
    }

    /// <summary>A test body of 20-test-cases.md: the objects it is made of, each with its hand-worked numbers.</summary>
    internal sealed class TestBody
    {
        public TestBody(string name, IEnumerable<TestPart> parts, GeoCoordinateSystem3? frame = null)
        {
            Name = name;
            Parts = parts.OrderBy(p => p.Object.Id, StringComparer.Ordinal).ToArray();
            Frame = frame;
        }

        public string Name { get; }

        /// <summary>The objects, in the order of their ids.</summary>
        public IReadOnlyList<TestPart> Parts { get; }

        /// <summary>The frame the body is drawn in where it is not its main object's.</summary>
        public GeoCoordinateSystem3? Frame { get; }

        public TestPart this[string id] => Parts.Single(p => p.Object.Id == id);

        public double Volume => Parts.Sum(p => p.Volume);

        public DrawingSource ToSource()
        {
            IEnumerable<DrawingObject> objects = Parts.Select(p => p.Object);
            return Frame.HasValue ? new DrawingSource(objects, Frame.Value) : new DrawingSource(objects);
        }

        /// <summary>The body moved as a whole from the world's frame into a placement, the frames of its objects with it.</summary>
        public TestBody MovedTo(GeoCoordinateSystem3 placement)
        {
            GeoTransform3 transform = GeoTransform3.FromCoordinateSystem(placement);
            return new TestBody(Name, Parts.Select(p => p.TransformBy(transform)), Frame?.TransformBy(transform));
        }
    }

    /// <summary>
    /// The test bodies of 20-test-cases.md, built in code in millimetres, each closed and valid, with the numbers a
    /// draughtsman works out by hand. Members lie along the world's X with their Y up along the world's Z, as a Tekla
    /// part's frame has it; holes are cut with their corners on the circle, the first on the line of the frame's X.
    /// </summary>
    internal static class TestBodies
    {
        /// <summary>The tolerance the bodies are built and cut within.</summary>
        public static readonly Tolerance Build = Tolerance.Default;

        /// <summary>The sides of a faceted hole or bolt, until G0 reads how many Tekla Structures gives one.</summary>
        public const int HoleSegments = 24;

        /// <summary>
        /// TC1: a plate 300 by 200 by 20 with four holes of 22 at 50 from the edges. The plate lies in the world's XY,
        /// from the origin, 20 up along Z; its frame is the world's.
        /// </summary>
        public static TestBody TC1(int holeSegments = HoleSegments)
            => new TestBody("TC1", new[] { Plate1("TC1-plate", holeSegments) });

        /// <summary>
        /// TC2: an I-section 300 deep, its flanges 150 by 12 and its web 8, 3 000 long from the origin along X, and an
        /// end plate 200 by 340 by 20 on its far end with six holes of 22, two columns 100 apart and three rows 100 apart.
        /// The beam is the main part: its frame has X along it and Y up, so Z points along the world's -Y.
        /// </summary>
        public static TestBody TC2(int holeSegments = HoleSegments)
        {
            GeoCoordinateSystem3 beamFrame = MemberFrame(GeoPoint3.Origin);
            GeoSolid3 beam = GeoSolid3.Extrude(ISection(300, 150, 12, 8), SectionPlacement(GeoPoint3.Origin), 3000, Build);
            var beamPart = new TestPart(
                new DrawingObject("TC2-beam", DrawingObjectKind.Part, beam, beamFrame, "BEAM", "B1", "I300*150*12*8", "S355", "TC2", true),
                ISectionArea(300, 150, 12, 8) * 3000, 36, 0, 0.0);

            var centres = new List<GeoPoint2>();
            foreach (double z in new[] { -100.0, 0.0, 100.0 })
            {
                foreach (double y in new[] { -50.0, 50.0 })
                {
                    centres.Add(new GeoPoint2(y, z));
                }
            }

            // The plate is drawn across the beam's end: its X along the world's Y, its Y up, 20 thick along the world's X.
            GeoCoordinateSystem3 plateFrame = SectionPlacement(new GeoPoint3(3000, 0, 0));
            GeoSolid3 plate = GeoSolid3.Extrude(WithHoles(Rectangle(-100, -170, 100, 170), centres, 11, holeSegments), plateFrame, 20, Build);
            var platePart = new TestPart(
                new DrawingObject("TC2-endplate", DrawingObjectKind.Part, plate, plateFrame, "PLATE", "P1", "PL20*200", "S355", "TC2"),
                200 * 340 * 20 - 6 * 20 * PolygonArea(11, holeSegments), 12 + 18 * holeSegments, 6 * holeSegments, 22,
                centres.Select(c => new GeoPoint3(3010, c.X, c.Y)).ToArray());

            return new TestBody("TC2", new[] { beamPart, platePart });
        }

        /// <summary>TC3: a tube 168.3 by 6, 1 000 long from the origin along X, faceted.</summary>
        public static TestBody TC3(int segments = 32)
        {
            GeoFace2 ring = new GeoFace2(Circle(0, 0, 168.3 / 2, segments), new[] { Circle(0, 0, 168.3 / 2 - 6, segments) });
            GeoSolid3 tube = GeoSolid3.Extrude(ring, SectionPlacement(GeoPoint3.Origin), 1000, Build);
            double area = PolygonArea(168.3 / 2, segments) - PolygonArea(168.3 / 2 - 6, segments);
            return new TestBody("TC3", new[]
            {
                new TestPart(new DrawingObject("TC3-tube", DrawingObjectKind.Part, tube, MemberFrame(GeoPoint3.Origin), "TUBE", "T1", "PIPE168.3*6", "S355", isMain: true),
                    area * 1000, 6 * segments, 2 * segments, 0.0),
            });
        }

        /// <summary>
        /// TC4: an angle 100 by 100 by 10, 1 000 long from the origin along X, its horizontal leg along the world's Y and
        /// its upright one along Z. At the far end the upright leg is chamfered 50 by 50 (12 500 cut away); at the near
        /// end the horizontal leg is notched 30 along the angle by 40 across from its tip (12 000 cut away).
        /// </summary>
        public static TestBody TC4()
        {
            var section = new GeoPolygon2(P(0, 0), P(100, 0), P(100, 10), P(10, 10), P(10, 100), P(0, 100));
            GeoSolid3 angle = GeoSolid3.Extrude(section, SectionPlacement(GeoPoint3.Origin), 1000, Build);

            // The chamfer: the line x + z = 1 050 across the upright leg, cut by a prism standing out on both sides.
            var upright = new GeoCoordinateSystem3(new GeoPoint3(0, 15, 0), GeoVector3.XAxis, GeoVector3.ZAxis);
            GeoSolid3 chamfer = GeoSolid3.Extrude(new GeoPolygon2(P(940, 110), P(1010, 40), P(1010, 110)), upright, 20, Build);
            GeoSolid3 notch = GeoSolid3.Extrude(Rectangle(-5, 60, 30, 105), new GeoCoordinateSystem3(new GeoPoint3(0, 0, -5), GeoVector3.XAxis, GeoVector3.YAxis), 20, Build);

            GeoSolid3 cut = Subtract(Subtract(angle, chamfer), notch);
            return new TestBody("TC4", new[]
            {
                new TestPart(new DrawingObject("TC4-angle", DrawingObjectKind.Part, cut, MemberFrame(GeoPoint3.Origin), "ANGLE", "A1", "L100*100*10", "S355", isMain: true),
                    1900 * 1000 - 12500 - 12000, 27, 0, 0.0),
            });
        }

        /// <summary>
        /// TC5: a column 250 by 250 (flanges 14, web 9) 1 000 high standing on a base plate 400 by 400 by 25, with four
        /// triangular stiffeners 10 thick, 150 high, and four bolts of 20, 80 long, in holes of 22 at 150 from the middle.
        /// The base plate lies from the origin's level 25 up, centred on the Z axis; the column's flanges face the world's X.
        /// </summary>
        public static TestBody TC5(int holeSegments = HoleSegments)
            => new TestBody("TC5", ColumnBase("TC5", GeoVector3.Zero, holeSegments));

        /// <summary>
        /// TC6: the I-section of TC2 with its far end cut skew at 30 degrees through (2 000, 0, 0), the cut turned about
        /// the vertical, and its near end coped: the top flange and the web above 110 taken away for the first 100.
        /// </summary>
        public static TestBody TC6()
        {
            GeoSolid3 beam = GeoSolid3.Extrude(ISection(300, 150, 12, 8), SectionPlacement(GeoPoint3.Origin), 2100, Build);

            // The end left on the far side of the line x = 2 000 - y tan 30, the beam's section being symmetric across Y,
            // holds as much as a square end at 2 000 would.
            double run = 200 * Math.Tan(Math.PI / 6);
            var skew = new GeoPolygon2(P(2000 + run, -200), P(2300, -200), P(2300, 200), P(2000 - run, 200));
            GeoSolid3 skewTool = GeoSolid3.Extrude(skew, new GeoCoordinateSystem3(new GeoPoint3(0, 0, -200), GeoVector3.XAxis, GeoVector3.YAxis), 400, Build);
            GeoSolid3 copeTool = GeoSolid3.Extrude(Rectangle(-10, -100, 100, 100), new GeoCoordinateSystem3(new GeoPoint3(0, 0, 110), GeoVector3.XAxis, GeoVector3.YAxis), 90, Build);

            GeoSolid3 cut = Subtract(Subtract(beam, skewTool), copeTool);
            double cope = 100 * 150 * 12 + 100 * 8 * (138 - 110);
            return new TestBody("TC6", new[]
            {
                new TestPart(new DrawingObject("TC6-beam", DrawingObjectKind.Part, cut, MemberFrame(GeoPoint3.Origin), "BEAM", "B2", "I300*150*12*8", "S355", isMain: true),
                    ISectionArea(300, 150, 12, 8) * 2000 - cope, 42, 0, 0.0),
            });
        }

        /// <summary>
        /// TC7: a concrete beam 300 wide by 600 deep, 3 000 long from the origin along X, centred on the X axis; four bars
        /// of 20 at its corners, 30 to 2 970 along it; fifteen stirrups of 10 every 200 from 100, each an open loop round
        /// the bars with a gap of 40 in its top, 1 480 along its middle.
        /// </summary>
        public static TestBody TC7(int barSegments = 16, int stirrupSegments = 12)
        {
            var parts = new List<TestPart>();
            GeoSolid3 concrete = GeoSolid3.Extrude(Rectangle(-150, -300, 150, 300), SectionPlacement(GeoPoint3.Origin), 3000, Build);
            parts.Add(new TestPart(new DrawingObject("TC7-concrete", DrawingObjectKind.Part, concrete, MemberFrame(GeoPoint3.Origin), "BEAM", "C1", "300*600", "C30/37", "TC7", true),
                300.0 * 600 * 3000, 12, 0, 0.0));

            int bar = 0;
            foreach (double z in new[] { -249.0, 249.0 })
            {
                foreach (double y in new[] { -99.0, 99.0 })
                {
                    bar++;
                    var start = new GeoPoint3(30, y, z);
                    GeoSolid3 body = GeoSolid3.Cylinder(start, new GeoPoint3(2970, y, z), 10, barSegments, Build);
                    parts.Add(new TestPart(new DrawingObject("TC7-bar-" + bar, DrawingObjectKind.Rebar, body, MemberFrame(start), "BAR", "R1", "20", "B500B", "TC7"),
                        2940 * PolygonArea(10, barSegments), 3 * barSegments, barSegments, 0.0));
                }
            }

            for (int i = 0; i < 15; i++)
            {
                double x = 100 + 200 * i;
                var path = new GeoPolyline3(
                    new GeoPoint3(x, -75, 265), new GeoPoint3(x, -115, 265), new GeoPoint3(x, -115, -265),
                    new GeoPoint3(x, 115, -265), new GeoPoint3(x, 115, 265), new GeoPoint3(x, -35, 265));

                // A corner of the section lies in the plane of the loop, so that no facet faces straight out of it and
                // lies in the plane of a facet of the next length.
                GeoSolid3 body = GeoSolid3.Sweep(Circle(0, 0, 5, stirrupSegments), path, GeoVector3.XAxis, Build);
                var frame = new GeoCoordinateSystem3(new GeoPoint3(x, -75, 265), GeoVector3.YAxis.Negate(), GeoVector3.ZAxis);
                parts.Add(new TestPart(new DrawingObject("TC7-stirrup-" + (i + 1).ToString("00", CultureInfo.InvariantCulture), DrawingObjectKind.Rebar, body, frame, "STIRRUP", "R2", "10", "B500B", "TC7"),
                    1480 * PolygonArea(5, stirrupSegments), 11 * stirrupSegments, 5 * stirrupSegments, 0.0));
            }

            return new TestBody("TC7", parts);
        }

        /// <summary>
        /// TC8: three plates 10 thick. A, 200 by 100 from the origin; B, 150 by 100, lying on A from the same corner, face
        /// to face over 150 by 100 and sharing three edges of A's top; C, 200 by 100, beside A edge to edge along x = 200.
        /// </summary>
        public static TestBody TC8()
        {
            return new TestBody("TC8", new[]
            {
                Box("TC8-A", 0, 0, 0, 200, 100, 10),
                Box("TC8-B", 0, 0, 10, 150, 100, 20),
                Box("TC8-C", 200, 0, 0, 400, 100, 10),
            });
        }

        /// <summary>
        /// TC9: the plate of TC1 turned 30 degrees about the world's Z, so that its sides and hole walls are seen exactly
        /// edge-on from above and its top and bottom from the side; and a copy 500 along X turned on by a hair about the
        /// world's X, so that each of those faces is a hair off edge-on. Drawn in the world's frame.
        /// </summary>
        /// <param name="hair">The hair, in radians: 1E-5 moves a corner of the copy 0.003 over the plate's 300.</param>
        /// <param name="holeSegments">The sides of each hole.</param>
        public static TestBody TC9(double hair = 1E-5, int holeSegments = HoleSegments)
        {
            GeoTransform3 turn = GeoTransform3.RotationZ(Math.PI / 6);
            GeoTransform3 off = GeoTransform3.Translation(new GeoVector3(500, 0, 0)).Multiply(GeoTransform3.RotationX(hair)).Multiply(turn);
            return new TestBody("TC9", new[]
            {
                Plate1("TC9-exact", holeSegments).TransformBy(turn),
                Plate1("TC9-hair", holeSegments).TransformBy(off),
            }, GeoCoordinateSystem3.Global);
        }

        /// <summary>
        /// TC10: a frame of assemblies like TC5 on a grid 6 000 apart, the first at the origin, rows along Y and columns
        /// along X; 20 by 10 makes the 200 of the plan, 2 000 objects.
        /// </summary>
        public static TestBody TC10(int columns = 20, int rows = 10, int holeSegments = HoleSegments)
        {
            var parts = new List<TestPart>();

            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    string prefix = "TC10-" + row.ToString("000", CultureInfo.InvariantCulture) + "-" + column.ToString("000", CultureInfo.InvariantCulture);
                    parts.AddRange(ColumnBase(prefix, new GeoVector3(6000.0 * column, 6000.0 * row, 0), holeSegments));
                }
            }

            return new TestBody("TC10", parts, GeoCoordinateSystem3.Global);
        }

        /// <summary>The area of a regular polygon of n corners on a circle: n r² sin(2π / n) / 2.</summary>
        public static double PolygonArea(double radius, int corners) => 0.5 * corners * radius * radius * Math.Sin(2 * Math.PI / corners);

        /// <summary>The area of an I-section: two flanges and the web between them.</summary>
        public static double ISectionArea(double depth, double width, double flange, double web) => 2 * width * flange + (depth - 2 * flange) * web;

        private static TestPart Plate1(string id, int holeSegments)
        {
            GeoPoint2[] centres = { P(50, 50), P(250, 50), P(50, 150), P(250, 150) };
            GeoSolid3 plate = GeoSolid3.Extrude(WithHoles(Rectangle(0, 0, 300, 200), centres, 11, holeSegments), GeoCoordinateSystem3.Global, 20, Build);
            return new TestPart(
                new DrawingObject(id, DrawingObjectKind.Part, plate, GeoCoordinateSystem3.Global, "PLATE", "P1", "PL20*200", "S355", isMain: true),
                300 * 200 * 20 - 4 * 20 * PolygonArea(11, holeSegments), 12 + 12 * holeSegments, 4 * holeSegments, 22,
                centres.Select(c => new GeoPoint3(c.X, c.Y, 10)).ToArray());
        }

        private static IEnumerable<TestPart> ColumnBase(string prefix, GeoVector3 at, int holeSegments)
        {
            GeoTransform3 move = GeoTransform3.Translation(at);
            var parts = new List<TestPart>();
            GeoPoint2[] corners = { P(-150, -150), P(150, -150), P(-150, 150), P(150, 150) };

            GeoSolid3 basePlate = GeoSolid3.Extrude(WithHoles(Rectangle(-200, -200, 200, 200), corners, 11, holeSegments), GeoCoordinateSystem3.Global, 25, Build);
            parts.Add(new TestPart(new DrawingObject(prefix + "-base", DrawingObjectKind.Part, basePlate, GeoCoordinateSystem3.Global, "BASE PLATE", "P2", "PL25*400", "S355", prefix),
                400 * 400 * 25 - 4 * 25 * PolygonArea(11, holeSegments), 12 + 12 * holeSegments, 4 * holeSegments, 22,
                corners.Select(c => new GeoPoint3(c.X, c.Y, 12.5)).ToArray()));

            // The column's frame: X up its length, Y up its section towards the world's X, across the flanges.
            var columnFrame = new GeoCoordinateSystem3(new GeoPoint3(0, 0, 25), GeoVector3.ZAxis, GeoVector3.XAxis);
            var columnPlacement = new GeoCoordinateSystem3(new GeoPoint3(0, 0, 25), GeoVector3.YAxis.Negate(), GeoVector3.XAxis);
            GeoSolid3 column = GeoSolid3.Extrude(ISection(250, 250, 14, 9), columnPlacement, 1000, Build);
            parts.Add(new TestPart(new DrawingObject(prefix + "-column", DrawingObjectKind.Part, column, columnFrame, "COLUMN", "C1", "I250*250*14*9", "S355", prefix, true),
                ISectionArea(250, 250, 14, 9) * 1000, 36, 0, 0.0));

            // Two stiffeners on the outsides of the flanges, in the world's XZ, and two on the sides of the web, in YZ.
            var inXz = new GeoCoordinateSystem3(new GeoPoint3(0, 5, 0), GeoVector3.XAxis, GeoVector3.ZAxis);
            var inYz = new GeoCoordinateSystem3(new GeoPoint3(-5, 0, 0), GeoVector3.YAxis, GeoVector3.ZAxis);
            GeoPolygon2[] flangeSides = { new GeoPolygon2(P(125, 25), P(200, 25), P(125, 175)), new GeoPolygon2(P(-200, 25), P(-125, 25), P(-125, 175)) };
            GeoPolygon2[] webSides = { new GeoPolygon2(P(4.5, 25), P(200, 25), P(4.5, 175)), new GeoPolygon2(P(-200, 25), P(-4.5, 25), P(-4.5, 175)) };

            for (int i = 0; i < 2; i++)
            {
                parts.Add(Stiffener(prefix + "-stiffener-" + (i + 1), GeoSolid3.Extrude(flangeSides[i], inXz, 10, Build), inXz, prefix, 0.5 * 75 * 150 * 10));
                parts.Add(Stiffener(prefix + "-stiffener-" + (i + 3), GeoSolid3.Extrude(webSides[i], inYz, 10, Build), inYz, prefix, 0.5 * 195.5 * 150 * 10));
            }

            for (int i = 0; i < corners.Length; i++)
            {
                var start = new GeoPoint3(corners[i].X, corners[i].Y, -30);
                GeoSolid3 bolt = GeoSolid3.Cylinder(start, new GeoPoint3(corners[i].X, corners[i].Y, 50), 10, holeSegments, Build);
                parts.Add(new TestPart(new DrawingObject(prefix + "-bolt-" + (i + 1), DrawingObjectKind.Bolt, bolt, new GeoCoordinateSystem3(start, GeoVector3.ZAxis, GeoVector3.XAxis), "BOLT", "M20", "M20*80", "8.8", prefix),
                    80 * PolygonArea(10, holeSegments), 3 * holeSegments, holeSegments, 0.0));
            }

            return parts.Select(p => p.TransformBy(move));
        }

        private static TestPart Stiffener(string id, GeoSolid3 body, GeoCoordinateSystem3 frame, string parent, double volume)
            => new TestPart(new DrawingObject(id, DrawingObjectKind.Part, body, frame, "STIFFENER", "S1", "PL10", "S355", parent), volume, 9, 0, 0.0);

        private static TestPart Box(string id, double x0, double y0, double z0, double x1, double y1, double z1)
        {
            var frame = new GeoCoordinateSystem3(new GeoPoint3(x0, y0, z0), GeoVector3.XAxis, GeoVector3.YAxis);
            GeoSolid3 body = GeoSolid3.Extrude(Rectangle(0, 0, x1 - x0, y1 - y0), frame, z1 - z0, Build);
            return new TestPart(new DrawingObject(id, DrawingObjectKind.Part, body, frame, "PLATE", id.Substring(id.Length - 1), "PL" + (z1 - z0).ToString(CultureInfo.InvariantCulture), "S355", "TC8"),
                (x1 - x0) * (y1 - y0) * (z1 - z0), 12, 0, 0.0);
        }

        /// <summary>The frame of a member lying along the world's X from a point, its Y up along the world's Z.</summary>
        private static GeoCoordinateSystem3 MemberFrame(GeoPoint3 start) => new GeoCoordinateSystem3(start, GeoVector3.XAxis, GeoVector3.ZAxis);

        /// <summary>Where a section of a member along the world's X is drawn: across along the world's Y, up along Z.</summary>
        private static GeoCoordinateSystem3 SectionPlacement(GeoPoint3 origin) => new GeoCoordinateSystem3(origin, GeoVector3.YAxis, GeoVector3.ZAxis);

        private static GeoSolid3 Subtract(GeoSolid3 body, GeoSolid3 tool)
        {
            if (!body.TrySubtract(tool, out GeoSolid3 result, Build))
            {
                throw new InvalidOperationException("A cut of a test body failed.");
            }

            return result;
        }

        private static GeoPoint2 P(double x, double y) => new GeoPoint2(x, y);

        private static GeoPolygon2 Rectangle(double x0, double y0, double x1, double y1) => new GeoPolygon2(P(x0, y0), P(x1, y0), P(x1, y1), P(x0, y1));

        /// <summary>A circle of n corners, the first on the line of X from its centre, running anticlockwise.</summary>
        private static GeoPolygon2 Circle(double x, double y, double radius, int corners)
            => new GeoPolygon2(Enumerable.Range(0, corners).Select(i => P(x + radius * Math.Cos(2 * Math.PI * i / corners), y + radius * Math.Sin(2 * Math.PI * i / corners))));

        private static GeoFace2 WithHoles(GeoPolygon2 outline, IEnumerable<GeoPoint2> centres, double radius, int corners)
            => new GeoFace2(outline, centres.Select(c => Circle(c.X, c.Y, radius, corners)).ToArray());

        /// <summary>An I-section centred on the origin, drawn anticlockwise: across along X, up along Y.</summary>
        private static GeoPolygon2 ISection(double depth, double width, double flange, double web)
        {
            double w = width / 2, d = depth / 2, t = web / 2, f = d - flange;
            return new GeoPolygon2(P(-w, -d), P(w, -d), P(w, -f), P(t, -f), P(t, f), P(w, f), P(w, d), P(-w, d), P(-w, f), P(-t, f), P(-t, -f), P(-w, -f));
        }
    }
}
