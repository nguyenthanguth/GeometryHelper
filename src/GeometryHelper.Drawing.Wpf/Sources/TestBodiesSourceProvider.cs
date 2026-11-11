using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Drawing.Wpf
{
    /// <summary>
    /// A source of objects built in code, in millimetres, to try the model drawing without a model: three assemblies side by
    /// side along the world's Y, each closed and valid.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>B1, a beam: an I-section 290 deep and 300 wide (flanges 14, web 8.5), 3 000 long along the world's X from the
    /// origin, with an end plate 300 by 340 by 20 on each end, each with four holes of 22 on a grid of 200 by 240. The beam is
    /// the main part.</item>
    /// <item>C1, a column base 3 000 along Y: a column 250 by 250 (flanges 14, web 9) 1 500 high on a base plate 400 by 400 by
    /// 25 with four holes of 22 at 150 from its middle, four triangular stiffeners 10 thick and four bolts M20 80 long.</item>
    /// <item>F1, a small frame 6 000 along Y: two columns 200 by 200, 3 000 high and 4 000 apart, and a beam 300 by 150 on
    /// top, 4 200 long, the main part.</item>
    /// </list>
    /// The changed model (<c>changed: true</c>) has the beam B1 3 500 long, its far end plate moved with it, and a stiffener
    /// 10 thick added in its web half way along: an update from it changes the beam and the far plate and adds the stiffener.
    /// </remarks>
    public sealed class TestBodiesSourceProvider : IDrawingSourceProvider
    {
        private const int HoleSegments = 16;
        private readonly bool _changed;
        private readonly Tolerance _tolerance;

        /// <summary>Makes the source of the test bodies.</summary>
        /// <param name="tolerance">The tolerance the bodies are built within.</param>
        /// <param name="changed">Whether to give the model as changed: the beam 3 500 long with a stiffener added.</param>
        public TestBodiesSourceProvider(Tolerance tolerance, bool changed = false)
        {
            _tolerance = tolerance;
            _changed = changed;
        }

        /// <inheritdoc/>
        public string Name => _changed ? "Test bodies, changed" : "Test bodies";

        /// <inheritdoc/>
        public string Description =>
            _changed
                ? "The test bodies with the beam B1 made 3 500 long and a stiffener added, to try Update from model"
                : "Objects built in code: a beam with end plates, a column base with bolts, a small frame";

        /// <summary>Gets true: the bodies are built in code.</summary>
        public bool IsAvailable => true;

        /// <inheritdoc/>
        public string AvailabilityText =>
            _changed ? "Built in · 3 assemblies, 17 objects" : "Built in · 3 assemblies, 16 objects";

        /// <summary>Gives the objects of the three assemblies, built anew.</summary>
        /// <returns>The objects in the order of their ids.</returns>
        public IReadOnlyList<DrawingObject> CreateObjects()
        {
            var objects = new List<DrawingObject>();
            Beam(objects);
            ColumnBase(objects, new GeoVector3(0.0, 3000.0, 0.0));
            Frame(objects, new GeoVector3(0.0, 6000.0, 0.0));
            objects.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            return objects;
        }

        private void Beam(List<DrawingObject> objects)
        {
            double length = _changed ? 3500.0 : 3000.0;
            GeoSolid3 beam = GeoSolid3.Extrude(
                ISection(290, 300, 14, 8.5),
                Section(GeoPoint3.Origin),
                length,
                _tolerance
            );
            objects.Add(
                new DrawingObject(
                    "B1-beam",
                    DrawingObjectKind.Part,
                    beam,
                    Member(GeoPoint3.Origin),
                    "BEAM",
                    "B1",
                    "HEA300",
                    "S355",
                    "B1",
                    true
                )
            );

            GeoPoint2[] holes = { P(-100, -120), P(100, -120), P(-100, 120), P(100, 120) };
            GeoFace2 plate = new GeoFace2(Rectangle(-150, -170, 150, 170), Circles(holes, 11));

            foreach (double x in new[] { -20.0, length })
            {
                GeoCoordinateSystem3 frame = Section(new GeoPoint3(x, 0, 0));
                string id = "B1-plate-" + (x < 0 ? "1" : "2");
                objects.Add(
                    new DrawingObject(
                        id,
                        DrawingObjectKind.Part,
                        GeoSolid3.Extrude(plate, frame, 20, _tolerance),
                        frame,
                        "END PLATE",
                        "P1",
                        "PL20*300",
                        "S355",
                        "B1"
                    )
                );
            }

            if (_changed)
            {
                // Between the flanges on the near side of the web: 4.25 off the beam's axis to the flange's edge at 150.
                GeoCoordinateSystem3 frame = Section(new GeoPoint3(length / 2.0 - 5.0, 0, 0));
                GeoSolid3 stiffener = GeoSolid3.Extrude(Rectangle(4.25, -131, 150, 131), frame, 10, _tolerance);
                objects.Add(
                    new DrawingObject(
                        "B1-stiffener-1",
                        DrawingObjectKind.Part,
                        stiffener,
                        frame,
                        "STIFFENER",
                        "S1",
                        "PL10*146",
                        "S355",
                        "B1"
                    )
                );
            }
        }

        private void ColumnBase(List<DrawingObject> objects, GeoVector3 at)
        {
            GeoTransform3 move = GeoTransform3.Translation(at);
            GeoPoint2[] corners = { P(-150, -150), P(150, -150), P(-150, 150), P(150, 150) };

            GeoFace2 basePlate = new GeoFace2(Rectangle(-200, -200, 200, 200), Circles(corners, 11));
            Add(
                objects,
                move,
                "C1-base",
                DrawingObjectKind.Part,
                GeoSolid3.Extrude(basePlate, GeoCoordinateSystem3.Global, 25, _tolerance),
                GeoCoordinateSystem3.Global,
                "BASE PLATE",
                "P2",
                "PL25*400",
                "S355",
                "C1"
            );

            // The column's frame: X up its length, Y across the flanges towards the world's X.
            var columnFrame = new GeoCoordinateSystem3(new GeoPoint3(0, 0, 25), GeoVector3.ZAxis, GeoVector3.XAxis);
            var placement = new GeoCoordinateSystem3(
                new GeoPoint3(0, 0, 25),
                GeoVector3.YAxis.Negate(),
                GeoVector3.XAxis
            );
            Add(
                objects,
                move,
                "C1-column",
                DrawingObjectKind.Part,
                GeoSolid3.Extrude(ISection(250, 250, 14, 9), placement, 1500, _tolerance),
                columnFrame,
                "COLUMN",
                "C1",
                "HEB250",
                "S355",
                "C1",
                true
            );

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
                Add(
                    objects,
                    move,
                    "C1-stiffener-" + (i + 1),
                    DrawingObjectKind.Part,
                    GeoSolid3.Extrude(flangeSides[i], inXz, 10, _tolerance),
                    inXz,
                    "STIFFENER",
                    "S2",
                    "PL10",
                    "S355",
                    "C1"
                );
                Add(
                    objects,
                    move,
                    "C1-stiffener-" + (i + 3),
                    DrawingObjectKind.Part,
                    GeoSolid3.Extrude(webSides[i], inYz, 10, _tolerance),
                    inYz,
                    "STIFFENER",
                    "S3",
                    "PL10",
                    "S355",
                    "C1"
                );
            }

            for (int i = 0; i < corners.Length; i++)
            {
                var start = new GeoPoint3(corners[i].X, corners[i].Y, -30);
                GeoSolid3 bolt = GeoSolid3.Cylinder(
                    start,
                    new GeoPoint3(corners[i].X, corners[i].Y, 50),
                    10,
                    HoleSegments,
                    _tolerance
                );
                Add(
                    objects,
                    move,
                    "C1-bolt-" + (i + 1),
                    DrawingObjectKind.Bolt,
                    bolt,
                    new GeoCoordinateSystem3(start, GeoVector3.ZAxis, GeoVector3.XAxis),
                    "BOLT",
                    "M20",
                    "M20*80",
                    "8.8",
                    "C1"
                );
            }
        }

        private void Frame(List<DrawingObject> objects, GeoVector3 at)
        {
            GeoTransform3 move = GeoTransform3.Translation(at);

            for (int i = 0; i < 2; i++)
            {
                var foot = new GeoPoint3(4000.0 * i, 0, 0);
                var frame = new GeoCoordinateSystem3(foot, GeoVector3.ZAxis, GeoVector3.XAxis);
                var placement = new GeoCoordinateSystem3(foot, GeoVector3.YAxis.Negate(), GeoVector3.XAxis);
                Add(
                    objects,
                    move,
                    "F1-column-" + (i + 1),
                    DrawingObjectKind.Part,
                    GeoSolid3.Extrude(ISection(200, 200, 10, 6.5), placement, 3000, _tolerance),
                    frame,
                    "COLUMN",
                    "C2",
                    "HEB200",
                    "S355",
                    "F1"
                );
            }

            var start = new GeoPoint3(-100, 0, 3150);
            Add(
                objects,
                move,
                "F1-beam",
                DrawingObjectKind.Part,
                GeoSolid3.Extrude(ISection(300, 150, 10.7, 7.1), Section(start), 4200, _tolerance),
                Member(start),
                "BEAM",
                "B2",
                "IPE300",
                "S355",
                "F1",
                true
            );
        }

        private static void Add(
            List<DrawingObject> objects,
            GeoTransform3 move,
            string id,
            DrawingObjectKind kind,
            GeoSolid3 body,
            GeoCoordinateSystem3 frame,
            string name,
            string mark,
            string profile,
            string material,
            string parent,
            bool isMain = false
        ) =>
            objects.Add(
                new DrawingObject(
                    id,
                    kind,
                    body.TransformBy(move),
                    frame.TransformBy(move),
                    name,
                    mark,
                    profile,
                    material,
                    parent,
                    isMain
                )
            );

        /// <summary>The frame of a member lying along the world's X from a point, its Y up along the world's Z.</summary>
        private static GeoCoordinateSystem3 Member(GeoPoint3 start) =>
            new GeoCoordinateSystem3(start, GeoVector3.XAxis, GeoVector3.ZAxis);

        /// <summary>Where a section of a member along the world's X is drawn: across along the world's Y, up along Z.</summary>
        private static GeoCoordinateSystem3 Section(GeoPoint3 origin) =>
            new GeoCoordinateSystem3(origin, GeoVector3.YAxis, GeoVector3.ZAxis);

        private static GeoPoint2 P(double x, double y) => new GeoPoint2(x, y);

        private static GeoPolygon2 Rectangle(double x0, double y0, double x1, double y1) =>
            new GeoPolygon2(P(x0, y0), P(x1, y0), P(x1, y1), P(x0, y1));

        /// <summary>Holes of a radius round centres, each a polygon of <see cref="HoleSegments"/> corners running anticlockwise, the first on the line of X.</summary>
        private static GeoPolygon2[] Circles(GeoPoint2[] centres, double radius)
        {
            var circles = new GeoPolygon2[centres.Length];

            for (int c = 0; c < centres.Length; c++)
            {
                var corners = new GeoPoint2[HoleSegments];

                for (int i = 0; i < HoleSegments; i++)
                {
                    double angle = 2.0 * Math.PI * i / HoleSegments;
                    corners[i] = P(centres[c].X + radius * Math.Cos(angle), centres[c].Y + radius * Math.Sin(angle));
                }

                circles[c] = new GeoPolygon2(corners);
            }

            return circles;
        }

        /// <summary>An I-section centred on the origin, drawn anticlockwise: across along X, up along Y.</summary>
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

        /// <inheritdoc/>
        public override string ToString() => Name + " (" + AvailabilityText + ")";
    }
}
