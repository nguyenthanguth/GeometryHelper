using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Drawing.Projection;
using GeometryHelper.Dxf;
using GeometryHelper.Dxf.Viewer;
using GeometryHelper.Dxf.Viewer.Wpf;
using GeometryHelper.Geometry;
using SkiaSharp;
using Xunit;

namespace GeometryHelper.Drawing.UnitTest.Projection
{
    /// <summary>
    /// Bodies, view frames and checks for the projection tests, built in code; and a PNG of a view to look at.
    /// </summary>
    internal static class ProjectionKit
    {
        public static readonly Tolerance Tol = Tolerance.Default;

        /// <summary>Looking down: X to the right, Y up the sheet, the viewer above.</summary>
        public static readonly GeoCoordinateSystem3 Top = new GeoCoordinateSystem3(
            GeoPoint3.Origin,
            GeoVector3.XAxis,
            GeoVector3.YAxis
        );

        /// <summary>Looking along +Y from in front: X to the right, Z up the sheet, the viewer at -Y.</summary>
        public static readonly GeoCoordinateSystem3 Front = new GeoCoordinateSystem3(
            GeoPoint3.Origin,
            GeoVector3.XAxis,
            GeoVector3.ZAxis
        );

        /// <summary>Looking along -X from the right: Y to the right, Z up the sheet, the viewer at +X.</summary>
        public static readonly GeoCoordinateSystem3 Right = new GeoCoordinateSystem3(
            GeoPoint3.Origin,
            GeoVector3.YAxis,
            GeoVector3.ZAxis
        );

        /// <summary>Looking from (1, 1, 1) towards the origin, Z up the sheet.</summary>
        public static readonly GeoCoordinateSystem3 Iso = new GeoCoordinateSystem3(
            GeoPoint3.Origin,
            new GeoVector3(-1, 1, 0),
            new GeoVector3(-1, -1, 2)
        );

        public static GeoPoint2 P(double x, double y) => new GeoPoint2(x, y);

        public static GeoPolygon2 Rectangle(double x0, double y0, double x1, double y1) =>
            new GeoPolygon2(P(x0, y0), P(x1, y0), P(x1, y1), P(x0, y1));

        /// <summary>A circle of n corners, the first on the line of X from its centre, anticlockwise.</summary>
        public static GeoPolygon2 Circle(double x, double y, double radius, int corners) =>
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

        /// <summary>A box from one corner to the other, square to the world's axes.</summary>
        public static GeoSolid3 Box(double x0, double y0, double z0, double x1, double y1, double z1) =>
            GeoSolid3.Extrude(
                Rectangle(0, 0, x1 - x0, y1 - y0),
                new GeoCoordinateSystem3(new GeoPoint3(x0, y0, z0), GeoVector3.XAxis, GeoVector3.YAxis),
                z1 - z0,
                Tol
            );

        /// <summary>A plate lying in the world's XY from the origin, its holes of a diameter through it.</summary>
        public static GeoSolid3 PlateWithHoles(
            double width,
            double height,
            double thickness,
            double diameter,
            int corners,
            params GeoPoint2[] centres
        ) =>
            GeoSolid3.Extrude(
                new GeoFace2(
                    Rectangle(0, 0, width, height),
                    centres.Select(c => Circle(c.X, c.Y, diameter / 2, corners)).ToArray()
                ),
                GeoCoordinateSystem3.Global,
                thickness,
                Tol
            );

        /// <summary>An angle along X from the origin, its legs along Y and Z, both of a size and a thickness.</summary>
        public static GeoSolid3 Angle(double leg, double thickness, double length) =>
            GeoSolid3.Extrude(
                new GeoPolygon2(
                    P(0, 0),
                    P(leg, 0),
                    P(leg, thickness),
                    P(thickness, thickness),
                    P(thickness, leg),
                    P(0, leg)
                ),
                new GeoCoordinateSystem3(GeoPoint3.Origin, GeoVector3.YAxis, GeoVector3.ZAxis),
                length,
                Tol
            );

        /// <summary>A tube along X from the origin, its section a ring of n corners.</summary>
        public static GeoSolid3 Tube(double diameter, double wall, double length, int corners) =>
            GeoSolid3.Extrude(
                new GeoFace2(Circle(0, 0, diameter / 2, corners), new[] { Circle(0, 0, diameter / 2 - wall, corners) }),
                new GeoCoordinateSystem3(GeoPoint3.Origin, GeoVector3.YAxis, GeoVector3.ZAxis),
                length,
                Tol
            );

        /// <summary>An I-section along X from a point, centred on the X axis: across along Y, up along Z.</summary>
        public static GeoSolid3 ISection(double x, double depth, double width, double flange, double web, double length)
        {
            double w = width / 2,
                d = depth / 2,
                t = web / 2,
                f = d - flange;
            var section = new GeoPolygon2(
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
            return GeoSolid3.Extrude(
                section,
                new GeoCoordinateSystem3(new GeoPoint3(x, 0, 0), GeoVector3.YAxis, GeoVector3.ZAxis),
                length,
                Tol
            );
        }

        /// <summary>
        /// A beam I 200 by 100 (flanges 10, web 6) 1 000 along X from the origin, with an end plate 20 by 120 by 220 against
        /// each end.
        /// </summary>
        public static ProjectionInput[] BeamWithEndPlates() =>
            new[]
            {
                new ProjectionInput("beam", ISection(0, 200, 100, 10, 6, 1000)),
                new ProjectionInput("plate-1", Box(-20, -60, -110, 0, 60, 110)),
                new ProjectionInput("plate-2", Box(1000, -60, -110, 1020, 60, 110)),
            };

        public static ProjectedView2 Project(GeoCoordinateSystem3 frame, params ProjectionInput[] inputs) =>
            ViewProjector.Project(inputs, frame, Tol);

        public static ProjectedView2 Project(GeoCoordinateSystem3 frame, GeoSolid3 solid) =>
            ViewProjector.Project(new[] { new ProjectionInput("A", solid) }, frame, Tol);

        /// <summary>Asserts that a straight line runs between two points, either way round, to the point tolerance.</summary>
        public static void HasLine(IEnumerable<ProjectedEdge2> edges, double x0, double y0, double x1, double y1)
        {
            Assert.Contains(edges, e =>
                !e.IsArc
                && (
                    Near(e.StartPoint, x0, y0) && Near(e.EndPoint, x1, y1)
                    || Near(e.StartPoint, x1, y1) && Near(e.EndPoint, x0, y0)
                )
            );
        }

        /// <summary>Asserts that a whole circle is drawn about a point at a radius, to the point tolerance.</summary>
        public static void HasCircle(IEnumerable<ProjectedEdge2> edges, double x, double y, double radius)
        {
            Assert.Contains(edges, e =>
                e.IsCircle && Near(e.Arc.Center, x, y) && Math.Abs(e.Arc.Radius - radius) <= Tol.EqualPoint
            );
        }

        public static bool Near(GeoPoint2 p, double x, double y) =>
            Math.Abs(p.X - x) <= Tol.EqualPoint && Math.Abs(p.Y - y) <= Tol.EqualPoint;

        /// <summary>
        /// The pairs of straight lines that lie on one line and overlap by more than twice the tolerance: a line drawn twice,
        /// or seen and hidden at once.
        /// </summary>
        public static List<string> Overlaps(IReadOnlyList<ProjectedEdge2> a, IReadOnlyList<ProjectedEdge2> b, bool same)
        {
            var found = new List<string>();
            double tol = Tol.EqualPoint;

            for (int i = 0; i < a.Count; i++)
            {
                for (int j = same ? i + 1 : 0; j < b.Count; j++)
                {
                    if (a[i].IsArc || b[j].IsArc)
                    {
                        continue;
                    }

                    GeoLine2 p = a[i].Line;
                    GeoLine2 q = b[j].Line;
                    GeoLine2 longer = p.Length >= q.Length ? p : q;
                    GeoLine2 shorter = p.Length >= q.Length ? q : p;
                    double length = longer.Length;
                    double ux = longer.Direction.X / length,
                        uy = longer.Direction.Y / length;
                    double ax = shorter.StartPoint.X - longer.StartPoint.X,
                        ay = shorter.StartPoint.Y - longer.StartPoint.Y;
                    double bx = shorter.EndPoint.X - longer.StartPoint.X,
                        by = shorter.EndPoint.Y - longer.StartPoint.Y;

                    if (Math.Abs(ux * ay - uy * ax) > tol || Math.Abs(ux * by - uy * bx) > tol)
                    {
                        continue;
                    }

                    double s0 = ux * ax + uy * ay,
                        s1 = ux * bx + uy * by;
                    double overlap = Math.Min(length, Math.Max(s0, s1)) - Math.Max(0.0, Math.Min(s0, s1));

                    if (overlap > 2 * tol)
                    {
                        found.Add(a[i] + " / " + b[j]);
                    }
                }
            }

            return found;
        }

        /// <summary>The view as text, every number to the bit, for comparing two views.</summary>
        public static string Signature(ProjectedView2 view)
        {
            var text = new System.Text.StringBuilder();

            foreach (ProjectedEdge2 e in view.VisibleEdges.Concat(view.HiddenEdges))
            {
                text.Append(e.Visibility)
                    .Append(' ')
                    .Append(e.Kind)
                    .Append(' ')
                    .Append(string.Join(",", e.ObjectIds))
                    .Append(' ');

                if (e.IsArc)
                {
                    text.Append(Bits(e.Arc.Center.X))
                        .Append(Bits(e.Arc.Center.Y))
                        .Append(Bits(e.Arc.Radius))
                        .Append(Bits(e.Arc.StartAngle))
                        .Append(Bits(e.Arc.SweptAngle));
                }
                else
                {
                    text.Append(Bits(e.Line.StartPoint.X))
                        .Append(Bits(e.Line.StartPoint.Y))
                        .Append(Bits(e.Line.EndPoint.X))
                        .Append(Bits(e.Line.EndPoint.Y));
                }

                text.Append('\n');
            }

            foreach (ProjectedFace2 f in view.SectionFaces)
            {
                text.Append(f.ObjectId).Append(' ').Append(Bits(f.Face.Area));

                foreach (GeoPoint2 p in f.Face.Boundary.Vertices)
                {
                    text.Append(Bits(p.X)).Append(Bits(p.Y));
                }

                text.Append('\n');
            }

            return text.ToString();
        }

        private static string Bits(double value) => BitConverter.DoubleToInt64Bits(value).ToString("X16") + ";";

        /// <summary>
        /// The folder proj\patterns of the run (<see cref="TestRun"/>) holding acadiso.pat with the one pattern ANSI31, its one line at 45 degrees
        /// 3.175 apart, so that a drawing given it as its pattern search path holds the lines of the hatching it is given.
        /// </summary>
        public static string PatternFolder()
        {
            string folder = TestRun.Folder("proj", "patterns");
            File.WriteAllText(
                Path.Combine(folder, "acadiso.pat"),
                "*ANSI31, ANSI Iron, Brick, Stone masonry\n45, 0,0, 0,3.175\n"
            );
            return folder;
        }

        /// <summary>
        /// Writes a view into a new drawing and draws the drawing, as the presenter shows it, into a PNG under
        /// the folder proj of the run; gives the path. The drawing finds ANSI31 in <see cref="PatternFolder"/>, so that the
        /// hatching of a section is drawn.
        /// </summary>
        public static string SavePicture(ProjectedView2 view, string name, double hatchScale = 1.0)
        {
            var database = new DxfDatabase("AC1027") { PatternSearchPath = PatternFolder() };
            view.WriteTo(database, hatchScale);
            CadDocument document = CadDocument.From(database, name + ".dxf");
            DisplayBox box = document.GetDisplay(document.ModelSpace).Bounds;
            const int width = 1200,
                height = 900;
            var cad = new CadView(width, height);

            if (!box.IsEmpty)
            {
                cad.ZoomTo(box, 0.08);
            }

            string folder = TestRun.Folder("proj");
            string path = Path.Combine(folder, name + ".png");

            using (var bitmap = new SKBitmap(width, height))
            using (var canvas = new SKCanvas(bitmap))
            using (var renderer = new SkiaCanvasRenderer())
            {
                renderer.Render(
                    canvas,
                    document,
                    document.ModelSpace,
                    cad,
                    new CanvasOptions { Background = 0xFFFFFF }
                );
                canvas.Flush();

                using (SKImage image = SKImage.FromBitmap(bitmap))
                using (SKData data = image.Encode(SKEncodedImageFormat.Png, 100))
                using (FileStream file = File.Create(path))
                {
                    data.SaveTo(file);
                }
            }

            database.Save(Path.Combine(folder, name + ".dxf"));
            return path;
        }
    }
}
