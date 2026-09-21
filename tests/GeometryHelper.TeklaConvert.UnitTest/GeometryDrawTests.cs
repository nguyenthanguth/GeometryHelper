using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GeometryHelper.Geometry;
using GeometryHelper.TeklaConvert;
using Tekla.Structures.Model;
using TSG = Tekla.Structures.Geometry3d;
using Xunit;

namespace GeometryHelper.TeklaConvert.UnitTest
{
    /// <summary>
    /// What <see cref="GeometryDraw"/> hands to Tekla. Inserting a control polycurve needs a running Tekla; building
    /// the curve it is drawn with does not.
    /// </summary>
    public class GeometryDrawTests
    {
        private static TSG.Point P(double x, double y, double z = 0.0) => new TSG.Point(x, y, z);

        private static void AssertPoints(IEnumerable<TSG.Point> expected, IEnumerable<TSG.Point> actual)
        {
            Assert.NotNull(actual);
            Assert.Equal(expected.Select(p => (p.X, p.Y, p.Z)), actual.Select(p => (p.X, p.Y, p.Z)));
        }

        [Fact]
        public void Outline_ClosedRing_RepeatsTheFirstPointSoTheClosingEdgeIsDrawn()
        {
            TSG.Point[] square = { P(0, 0), P(1000, 0), P(1000, 1000), P(0, 1000) };

            AssertPoints(square.Concat(new[] { P(0, 0) }), GeometryDraw.Outline(square, true));
        }

        [Fact]
        public void Outline_ClosedRingThatAlreadyRepeatsItsStart_IsNotClosedTwice()
        {
            TSG.Point[] ring = { P(0, 0), P(1000, 0), P(1000, 1000), P(0, 0) };

            AssertPoints(ring, GeometryDraw.Outline(ring, true));
        }

        [Fact]
        public void Outline_OpenPolyline_KeepsThePointsAsGiven()
        {
            TSG.Point[] points = { P(0, 0), P(1000, 0), P(1000, 1000) };

            AssertPoints(points, GeometryDraw.Outline(points, false));
        }

        [Fact]
        public void Outline_LeavesOutRepeatedAndNullPoints()
        {
            TSG.Point[] points = { P(0, 0), P(0, 0), null, P(1000, 0), P(1000, 0.0000001), P(1000, 1000) };

            AssertPoints(new[] { P(0, 0), P(1000, 0), P(1000, 1000) }, GeometryDraw.Outline(points, false));
        }

        [Fact]
        public void Outline_ClosedWithTwoPoints_IsDrawnAsOneSegment()
        {
            TSG.Point[] points = { P(0, 0), P(1000, 0) };

            AssertPoints(points, GeometryDraw.Outline(points, true));
        }

        [Fact]
        public void Outline_FewerThanTwoDistinctPoints_IsNothingToDraw()
        {
            Assert.Null(GeometryDraw.Outline(new TSG.Point[0], false));
            Assert.Null(GeometryDraw.Outline(new[] { P(5, 5) }, true));
            Assert.Null(GeometryDraw.Outline(new[] { P(5, 5), P(5, 5), P(5, 5) }, true));
        }

        [Fact]
        public void Outline_CopiesThePoints()
        {
            TSG.Point first = P(0, 0);
            List<TSG.Point> outline = GeometryDraw.Outline(new[] { first, P(1000, 0), P(1000, 1000) }, true);

            first.X = 999;

            Assert.Equal(0.0, outline[0].X);
            Assert.Equal(0.0, outline[3].X);
        }

        [Fact]
        public void ToPolycurve_ClosedSquare_HasAllFourEdges()
        {
            TSG.Point[] square = { P(0, 0), P(1000, 0), P(1000, 1000), P(0, 1000) };

            TSG.Polycurve curve = GeometryDraw.ToPolycurve(square, true);

            Assert.NotNull(curve);
            Assert.Equal(4, curve.Count());
        }

        [Fact]
        public void ToPolycurve_NothingToDraw_IsNull()
        {
            Assert.Null(GeometryDraw.ToPolycurve(new[] { P(5, 5) }, true));
        }

        [Fact]
        public void FaceRings_TheBoundaryFirstThenEveryHole_EachInItsColour()
        {
            GeoFace3 face = SquareWithTwoHoles();

            var rings = GeometryDraw.FaceRings(face, ControlObjectColorEnum.RED, ControlObjectColorEnum.WHITE).ToList();

            Assert.Equal(3, rings.Count);
            Assert.Same(face.Boundary, rings[0].Ring);
            Assert.Equal(ControlObjectColorEnum.RED, rings[0].Color);
            Assert.Equal(face.Holes, rings.Skip(1).Select(r => r.Ring));
            Assert.All(rings.Skip(1), r => Assert.Equal(ControlObjectColorEnum.WHITE, r.Color));
        }

        [Fact]
        public void FaceRings_OtherColours_ArePassedThrough()
        {
            var rings = GeometryDraw.FaceRings(SquareWithTwoHoles(), ControlObjectColorEnum.BLUE, ControlObjectColorEnum.YELLOW).ToList();

            Assert.Equal(
                new[] { ControlObjectColorEnum.BLUE, ControlObjectColorEnum.YELLOW, ControlObjectColorEnum.YELLOW },
                rings.Select(r => r.Color));
        }

        [Fact]
        public void FaceRings_AFaceWithoutHoles_IsItsBoundaryAlone()
        {
            GeoFace3 face = new GeoFace3(Square(0, 0, 1000, false));

            var rings = GeometryDraw.FaceRings(face, ControlObjectColorEnum.RED, ControlObjectColorEnum.WHITE).ToList();

            Assert.Single(rings);
            Assert.Same(face.Boundary, rings[0].Ring);
        }

        [Theory]
        [InlineData(typeof(GeoFace3))]
        [InlineData(typeof(GeoSolid3))]
        [InlineData(typeof(IEnumerable<GeoSolid3>))]
        public void DrawToTekla_FacesAndSolids_DrawBoundariesRedAndHolesWhiteUnlessTold(Type target)
        {
            MethodInfo method = typeof(GeometryDraw).GetMethod(
                nameof(GeometryDraw.DrawToTekla),
                new[] { target, typeof(ControlObjectColorEnum), typeof(ControlObjectColorEnum), typeof(ControlObjectLineType) });

            Assert.NotNull(method);
            ParameterInfo[] parameters = method.GetParameters();
            Assert.Equal("boundaryColor", parameters[1].Name);
            Assert.Equal((int)ControlObjectColorEnum.RED, Convert.ToInt32(parameters[1].DefaultValue));
            Assert.Equal("holeColor", parameters[2].Name);
            Assert.Equal((int)ControlObjectColorEnum.WHITE, Convert.ToInt32(parameters[2].DefaultValue));
            Assert.Equal((int)ControlObjectLineType.SolidLine, Convert.ToInt32(parameters[3].DefaultValue));
        }

        [Fact]
        public void TheDrawingMethods_AreDrawPolylineAndDrawToTekla()
        {
            string[] names = typeof(GeometryDraw).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Select(m => m.Name)
                .Distinct()
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(new[] { "DrawPolyline", "DrawToTekla" }, names);
        }

        private static GeoPolygon3 Square(double x, double y, double size, bool clockwise)
        {
            GeoPoint3[] corners =
            {
                new GeoPoint3(x, y, 0), new GeoPoint3(x + size, y, 0), new GeoPoint3(x + size, y + size, 0), new GeoPoint3(x, y + size, 0)
            };

            return new GeoPolygon3(clockwise ? corners.Reverse() : corners);
        }

        // A 1000 mm square with two 100 mm holes.
        private static GeoFace3 SquareWithTwoHoles()
        {
            return new GeoFace3(Square(0, 0, 1000, false), new[] { Square(100, 100, 100, true), Square(500, 500, 100, true) });
        }
    }
}
