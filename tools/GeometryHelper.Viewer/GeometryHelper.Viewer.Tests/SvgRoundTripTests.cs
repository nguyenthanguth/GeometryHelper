using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Export;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Viewer.Tests
{
    /// <summary>
    /// What SvgWriter writes, read back by the viewer. An SVG prints its numbers to six places, so a coordinate comes back to within five ten-millionths
    /// of a unit, and an arc, whose centre and sweep are worked out from its end points and its radius, to about a millionth over its radius.
    /// </summary>
    public class SvgRoundTripTests
    {
        private const double Printed = 5.1E-7;

        private static Scene Read(SvgWriter writer) => SceneImport.ReadText(writer.ToString(), "svg");

        private static List<double> Flatten(IEnumerable<GeoPoint2> points) => points.SelectMany(Imp.P2).ToList();

        private static void Within(IReadOnlyList<double> expected, IReadOnlyList<double> actual, double allowed, string what = "")
        {
            Assert.True(expected.Count == actual.Count, what + ": " + expected.Count + " numbers expected, " + actual.Count + " found");

            for (int i = 0; i < expected.Count; i++)
            {
                Assert.True(Math.Abs(expected[i] - actual[i]) <= allowed, what + ": number " + i + " is " + actual[i].ToString("R") + " where " + expected[i].ToString("R") + " was drawn");
            }
        }

        [Fact]
        public void APoint_IsADot_ToSixPlaces()
        {
            var point = new GeoPoint2(12.3456789, -0.0000004);
            SceneItem item = Imp.Only(Read(new SvgWriter().Add(point, "red")));

            Assert.Equal("GeoPoint2", item.Kind);
            Assert.Equal("#FF0000", item.Color);
            Within(Imp.P2(point), Imp.Of(item), Printed);
            Assert.Equal(12.345679, Imp.Of(item)[0]);
        }

        [Fact]
        public void ASegment_IsALine_ToSixPlaces()
        {
            var line = (GeoLine2)SampleShapes.Make("GeoLine2", 2);
            SceneItem item = Imp.Only(Read(new SvgWriter().Add(line)));

            Assert.Equal("GeoLine2", item.Kind);
            Within(Imp.Of(line), Imp.Of(item), Printed);
        }

        [Fact]
        public void AChainAndAPolygon_AreAChainAndAPolygon()
        {
            var chain = (GeoPolyline2)SampleShapes.Make("GeoPolyline2", 4);
            var polygon = (GeoPolygon2)SampleShapes.Make("GeoPolygon2", 4);

            SceneItem a = Imp.Only(Read(new SvgWriter().Add(chain, "#3070c0")));
            SceneItem b = Imp.Only(Read(new SvgWriter().Add(polygon, "black", "#eeeeee")));

            Assert.Equal("GeoPolyline2", a.Kind);
            Assert.Equal("GeoPolygon2", b.Kind);
            Assert.Equal("#3070C0", a.Color);
            Assert.Equal("#000000", b.Color);
            Within(Imp.Of(chain), Imp.Of(a), Printed);
            Within(Imp.Of(polygon), Imp.Of(b), Printed);
        }

        [Fact]
        public void ARectangleATriangleAndTriangles_ArePolygons()
        {
            var rectangle = (GeoRectangle2)SampleShapes.Make("GeoRectangle2", 6);
            var triangle = (GeoTriangle2)SampleShapes.Make("GeoTriangle2", 6);

            Scene scene = Read(new SvgWriter().Add(rectangle).Add(triangle).Add(new[] { triangle, triangle }));

            Assert.Empty(scene.LoadWarnings);
            Assert.Equal(4, scene.Items.Count);
            Assert.All(scene.Items, i => Assert.Equal("GeoPolygon2", i.Kind));
            Within(Flatten(rectangle.GetVertices()), Imp.Of(scene.Items[0]), Printed);
            Within(Flatten(new[] { triangle.A, triangle.B, triangle.C }), Imp.Of(scene.Items[1]), Printed);
        }

        [Fact]
        public void AFaceWithAHole_IsAFace()
        {
            var face = (GeoFace2)SampleShapes.Make("GeoFace2", 8);
            SceneItem item = Imp.Only(Read(new SvgWriter().Add(face, "black", "#cccccc")));

            Assert.Equal("GeoFace2", item.Kind);
            Within(Imp.Of(face), Imp.Of(item), Printed);
        }

        [Fact]
        public void ACircle_IsACircle_AndAnEllipse_AnEllipseTurnedTheWayItWasDrawn()
        {
            var circle = new GeoCircle2(new GeoPoint2(12.5, -7.25), 33.125);
            var ellipse = new GeoEllipse2(new GeoPoint2(40.0, 25.0), new GeoVector2(Math.Cos(Math.PI / 6.0), Math.Sin(Math.PI / 6.0)), 30.0, 10.0);

            Scene scene = Read(new SvgWriter().Add(circle).Add(ellipse));

            Assert.Empty(scene.LoadWarnings);
            Assert.Equal(new[] { "GeoCircle2", "GeoEllipse2" }, scene.Items.Select(i => i.Kind).ToArray());
            Within(Imp.Of(circle), Imp.Of(scene.Items[0]), Printed);

            // The turn is printed in degrees to six places, which is ten nanoradians.
            Within(Imp.Of(ellipse), Imp.Of(scene.Items[1]), 1E-7);
        }

        [Fact]
        public void AnArcOfEitherSizeAndEitherWay_IsTheSameArc()
        {
            GeoArc2[] arcs =
            {
                new GeoArc2(new GeoPoint2(10.0, 20.0), 50.0, 0.3, 1.5),
                new GeoArc2(new GeoPoint2(-30.0, 5.5), 80.0, 0.3, 4.4),
                new GeoArc2(new GeoPoint2(10.0, 20.0), 50.0, 1.5, 0.3, true),
                new GeoArc2(new GeoPoint2(0.0, 0.0), 25.0, 4.0, 3.0, true),
            };

            foreach (GeoArc2 arc in arcs)
            {
                SceneItem item = Imp.Only(Read(new SvgWriter().Add(arc)));
                Assert.Equal("GeoArc2", item.Kind);

                List<double> d = Imp.Of(item);
                double read = d[2];
                var centre = new[] { d[0], d[1] };
                double start = d[3];
                double sweep = d[4];

                Assert.Equal(arc.SweptAngle < 0.0, sweep < 0.0);
                Within(new[] { arc.Center.X, arc.Center.Y, arc.Radius }, new[] { centre[0], centre[1], read }, 2E-5, "centre and radius");
                Within(Imp.P2(arc.StartPoint), new[] { centre[0] + (read * Math.Cos(start)), centre[1] + (read * Math.Sin(start)) }, 2E-5, "start");
                Within(Imp.P2(arc.EndPoint), new[] { centre[0] + (read * Math.Cos(start + sweep)), centre[1] + (read * Math.Sin(start + sweep)) }, 2E-5, "end");
            }
        }

        [Fact]
        public void AChainWithArcs_KeepsItsBulgesToAMillionth()
        {
            var chain = new GeoPolylineArc2(
                new[] { new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(200, 40), new GeoPoint2(300, 40) },
                new[] { 0.4, 0.0, -0.3, 0.0 });
            SceneItem item = Imp.Only(Read(new SvgWriter().Add(chain)));

            Assert.Equal("GeoPolylineArc2", item.Kind);
            Within(Imp.Of(chain), Imp.Of(item), 2E-5);
        }

        [Fact]
        public void ALoopWithArcs_KeepsItsClosingEdge_ArcAndAll()
        {
            var loop = new GeoPolygonArc2(
                new[] { new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100), new GeoPoint2(0, 100) },
                new[] { 0.0, 0.5, 0.0, 0.3 });
            SceneItem item = Imp.Only(Read(new SvgWriter().Add(loop)));

            Assert.Equal("GeoPolygonArc2", item.Kind);
            Within(Imp.Of(loop), Imp.Of(item), 2E-5);
        }

        [Fact]
        public void ASemicircleEdge_IsASemicircle_ThoughItsRadiusIsPrintedToSixPlaces()
        {
            // A radius and a chord that agree to a millionth cannot tell a half circle from a hair short of one; the half circle is the reading.
            var loop = new GeoPolygonArc2(new[] { new GeoPoint2(0.123457, 0.0), new GeoPoint2(33.333333, 0.0), new GeoPoint2(33.333333, 40.0) }, new[] { 1.0, 0.0, 0.0 });
            SceneItem item = Imp.Only(Read(new SvgWriter().Add(loop)));

            Assert.InRange(Imp.Of(item).Skip(6).First(), 1.0 - 1E-12, 1.0 + 1E-12);
        }

        [Fact]
        public void AnEdge_IsALineOrAnArc()
        {
            var straight = new GeoEdge2(new GeoPoint2(0.0, 0.0), new GeoPoint2(30.0, 40.0), 0.0);
            var bent = new GeoEdge2(new GeoPoint2(0.0, 0.0), new GeoPoint2(30.0, 40.0), 0.4);
            Scene scene = Read(new SvgWriter().Add(straight).Add(bent));

            Assert.Equal(new[] { "GeoLine2", "GeoArc2" }, scene.Items.Select(i => i.Kind).ToArray());
            Within(new[] { 0.0, 0.0, 30.0, 40.0 }, Imp.Of(scene.Items[0]), Printed);

            GeoArc2 arc = bent.ToArc();
            List<double> d = Imp.Of(scene.Items[1]);
            Within(new[] { arc.Center.X, arc.Center.Y, arc.Radius }, new[] { d[0], d[1], d[2] }, 2E-5);
        }

        [Fact]
        public void AMeshInThePlane_IsAPolygonToAFace()
        {
            var mesh = (Meshing.GeoMesh2)SampleShapes.Make("GeoMesh2", 1);
            Scene scene = Read(new SvgWriter().Add(mesh));

            Assert.Equal(mesh.FaceCount, scene.Items.Count);
            Assert.All(scene.Items, i => Assert.Equal("GeoPolygon2", i.Kind));
        }

        [Fact]
        public void ARay_IsASegmentFromItsOriginAlongItsDirection()
        {
            var ray = new GeoRay2(new GeoPoint2(10.0, 20.0), new GeoVector2(3.0, 4.0));
            Scene scene = Read(new SvgWriter().Add(new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(100, 100))).Add(ray));
            List<double> line = Imp.Of(scene.Items[1]);

            Assert.Equal("GeoLine2", scene.Items[1].Kind);
            Within(new[] { 10.0, 20.0 }, new[] { line[0], line[1] }, Printed);
            Assert.True(Math.Abs(((line[2] - line[0]) * ray.Direction.Y) - ((line[3] - line[1]) * ray.Direction.X)) < 1E-5);
        }

        [Fact]
        public void ALabel_IsText()
        {
            Scene scene = Read(new SvgWriter().Add(new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(100, 50))).Text(new GeoPoint2(20.5, 10.25), "a < b & c", "blue"));
            SceneItem item = scene.Items.Single(i => i.Kind == SceneKinds.Text);

            Assert.Equal("a < b & c", item.Data.GetProperty("text").GetString());
            Within(new[] { 20.5, 10.25, 0.0 }, Imp.Of(item).Take(3).ToList(), Printed);
            Assert.True(item.Data.GetProperty("height").GetDouble() > 0.0);
            Assert.Equal("#0000FF", item.Color);
        }

        [Fact]
        public void ANumberWithMoreThanSixPlaces_ComesBackRoundedToSix()
        {
            var polyline = new GeoPolyline2(new[] { new GeoPoint2(0.12345678901, 0.0), new GeoPoint2(1.0000004, 2.0) });
            SceneItem item = Imp.Only(Read(new SvgWriter().Add(polyline)));

            Assert.Equal("GeoLine2", item.Kind);
            Assert.Equal(0.123457, Imp.Of(item)[0]);
            Assert.Equal(1.0, Imp.Of(item)[2]);
        }

        [Fact]
        public void AWrittenPicture_IsAValidScene()
        {
            Scene scene = Read(new SvgWriter()
                .Add((GeoPolygon2)SampleShapes.Make("GeoPolygon2", 1), "red", "#ffcccc")
                .Add((GeoFace2)SampleShapes.Make("GeoFace2", 2))
                .Add(new GeoCircle2(new GeoPoint2(1.0, 2.0), 3.0))
                .Add(new GeoArc2(new GeoPoint2(0.0, 0.0), 10.0, 0.0, 2.0))
                .Add(new GeoPoint2(4.0, 5.0)));

            Assert.Empty(scene.LoadWarnings);
            Imp.IsAValidScene(scene);
        }
    }
}
