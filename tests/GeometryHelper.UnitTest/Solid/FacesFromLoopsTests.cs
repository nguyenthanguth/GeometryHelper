using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;
using GeometryHelper.UnitTest.Common;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Faces made from loops of corners as a modeller gives them: one face where the loops lie flat, and triangles on
    /// their own corners where they do not, so that a body whose faces come a little out of flat stays closed.
    /// </summary>
    public class FacesFromLoopsTests
    {
        private static readonly Tolerance Default = Tolerance.Default;

        private static readonly GeoPoint3[] Square = { P(0, 0, 0), P(100, 0, 0), P(100, 100, 0), P(0, 100, 0) };

        [Fact]
        public void FlatLoopsGiveOneFaceAsTheConstructorsWould()
        {
            // The hole wound against the boundary, as IFC writes one.
            GeoPoint3[] hole = { P(40, 40, 0), P(40, 60, 0), P(60, 60, 0), P(60, 40, 0) };

            GeoFace3 face = Assert.Single(GeoFace3.FromLoops(Square, new[] { hole }, Default));

            Assert.Equal(Square, face.Boundary.Vertices);
            GeoPolygon3 kept = Assert.Single(face.Holes);
            Assert.True(kept.Normal.IsCodirectionalTo(face.Normal, Default));
            Assert.Equal(100.0 * 100.0 - 20.0 * 20.0, face.Area, 9);
            Assert.True(face.Normal.IsCodirectionalTo(GeoVector3.ZAxis, Default));
        }

        [Fact]
        public void ALoopOutOfFlatGivesTrianglesOnItsOwnCorners()
        {
            // One corner a millimetre up: the loop strays half a millimetre from its plane, ten times what the default
            // lets a face stray.
            GeoPoint3[] quad = { P(0, 0, 0), P(100, 0, 0), P(100, 100, 1), P(0, 100, 0) };

            GeoFace3[] faces = GeoFace3.FromLoops(quad, null, Default);

            Assert.Equal(2, faces.Length);
            Assert.All(faces, face =>
            {
                Assert.Equal(3, face.Boundary.Vertices.Count);
                Assert.All(face.Boundary.Vertices, corner => Assert.Contains(corner, quad));
                Assert.Empty(face.Holes);
                Assert.True(face.Normal.Z > 0.99, "wound as the loop is");
            });
            Assert.InRange(faces.Sum(face => face.Area), 10000.0 * 0.99999, 10000.0 * 1.001);
        }

        [Fact]
        public void TrianglesOfALoopWoundTheOtherWayFaceTheOtherWay()
        {
            GeoPoint3[] quad = { P(0, 0, 0), P(0, 100, 0), P(100, 100, 1), P(100, 0, 0) };

            GeoFace3[] faces = GeoFace3.FromLoops(quad, null, Default);

            Assert.Equal(2, faces.Length);
            Assert.All(faces, face => Assert.True(face.Normal.Z < -0.99));
        }

        [Fact]
        public void AnLOutOfFlatIsCoveredWithoutItsNotch()
        {
            // 100 x 100 less the quarter at the top right, its inner corner a millimetre up. A fan from any corner but
            // the inner one reaches across the notch.
            GeoPoint3[] l = { P(0, 0, 0), P(100, 0, 0), P(100, 50, 0), P(50, 50, 1), P(50, 100, 0), P(0, 100, 0) };

            GeoFace3[] faces = GeoFace3.FromLoops(l, null, Default);

            Assert.Equal(4, faces.Length);
            Assert.InRange(faces.Sum(face => face.Area), 7500.0 * 0.99999, 7500.0 * 1.001);
            Assert.DoesNotContain(faces, face => CoversFromAbove(face, 75, 75));
            Assert.All(faces, face => Assert.True(face.Normal.Z > 0.99));
        }

        [Fact]
        public void AHoleOffThePlaneOfItsBoundaryIsKept()
        {
            // The boundary flat, the hole with one corner a millimetre up: off the boundary's plane, and out of flat.
            GeoPoint3[] hole = { P(40, 40, 0), P(60, 40, 0), P(60, 60, 1), P(40, 60, 0) };

            GeoFace3[] faces = GeoFace3.FromLoops(Square, new[] { hole }, Default);

            Assert.True(faces.Length > 1);
            Assert.All(faces, face => Assert.Empty(face.Holes));
            Assert.All(faces.SelectMany(face => face.Boundary.Vertices), corner => Assert.True(Square.Contains(corner) || hole.Contains(corner)));
            Assert.InRange(faces.Sum(face => face.Area), 9600.0 * 0.99999, 9600.0 * 1.001);
            Assert.DoesNotContain(faces, face => CoversFromAbove(face, 50, 50));
        }

        [Fact]
        public void ACubeWithACornerPushedOutStaysClosed()
        {
            // A cube of 100 with the corner at the top, right and back pushed out a millimetre each way: the three
            // faces that meet there are out of flat.
            GeoPoint3[] corners =
            {
                P(0, 0, 0), P(100, 0, 0), P(100, 100, 0), P(0, 100, 0),
                P(0, 0, 100), P(100, 0, 100), P(101, 101, 101), P(0, 100, 100),
            };
            int[][] loops =
            {
                new[] { 0, 3, 2, 1 }, new[] { 4, 5, 6, 7 }, new[] { 0, 1, 5, 4 },
                new[] { 1, 2, 6, 5 }, new[] { 2, 3, 7, 6 }, new[] { 3, 0, 4, 7 },
            };

            var body = new GeoSolid3(loops.SelectMany(loop => GeoFace3.FromLoops(loop.Select(i => corners[i]), null, Default)));

            Assert.Equal(3 + 3 * 2, body.Faces.Count);
            Assert.True(body.IsClosed(Default));
            Assert.InRange(body.Volume, 1E6, 1E6 + 12000.0);
        }

        [Fact]
        public void TheNotchedBeamClosesWithNoToleranceAtAll()
        {
            // new Tolerance() has every threshold at 0: read into polygons so, five of the beam's 26 faces are refused as
            // not flat, the face 0.04 mm out and four more out by rounding alone.
            var none = new Tolerance();

            var beam = new GeoSolid3(DefaultToleranceTests.Loops().SelectMany(loops => GeoFace3.FromLoops(loops[0], loops.Skip(1), none)));

            Assert.True(beam.Faces.Count > 26);
            Assert.True(beam.IsClosed(Default));
            double concrete = 30000.0 * 3000.0 * 400.0 - 5 * 600.0 * 800.0 * 400.0;
            Assert.InRange(beam.Volume, concrete * 0.99999, concrete * 1.00001);

            GeoFace3 cut = Assert.Single(beam.Section(new GeoPlane3(DefaultToleranceTests.Start, GeoVector3.YAxis), Default));
            Assert.Equal(3, cut.Holes.Count);
            double web = 30000.0 * 3000.0 - 5 * 600.0 * 800.0;
            Assert.InRange(cut.Area, web * 0.99999, web * 1.00001);
        }

        [Fact]
        public void TheNotchedBeamAtTheDefaultToleranceIsItsTwentySixFacesWhole()
        {
            List<GeoFace3> faces = DefaultToleranceTests.Loops().SelectMany(loops => GeoFace3.FromLoops(loops[0], loops.Skip(1), Default)).ToList();

            Assert.Equal(26, faces.Count);
            // The three openings run through the web, so each side face has all three as holes.
            Assert.Equal(6, faces.Sum(face => face.Holes.Count));
            Assert.True(new GeoSolid3(faces).IsClosed(Default));
        }

        [Fact]
        public void LoopsWithNoAreaGiveNoFaces()
        {
            Assert.Empty(GeoFace3.FromLoops(new[] { P(0, 0, 0), P(50, 0, 0), P(100, 0, 0) }, null, Default));
            Assert.Empty(GeoFace3.FromLoops(new[] { P(0, 0, 0), P(100, 0, 0), P(100, 0, 0), P(0, 0, 0) }, null, Default));
            Assert.Empty(GeoFace3.FromLoops(new GeoPoint3[0], null, Default));
        }

        [Fact]
        public void AHoleWithNoAreaIsLeftOut()
        {
            GeoPoint3[] sliver = { P(40, 40, 0), P(60, 40, 0), P(60, 40, 0) };

            GeoFace3 face = Assert.Single(GeoFace3.FromLoops(Square, new[] { sliver }, Default));

            Assert.Empty(face.Holes);
            Assert.Equal(10000.0, face.Area, 9);
        }

        [Fact]
        public void MissingLoopsAreRefused()
        {
            Assert.Throws<ArgumentNullException>(() => GeoFace3.FromLoops(null, null, Default));
            Assert.Throws<ArgumentException>(() => GeoFace3.FromLoops(Square, new GeoPoint3[][] { null }, Default));
        }

        [Fact]
        public void WithoutAToleranceTheOneInUseDecidesWhatIsFlat()
        {
            // A corner 0.2 up: a tenth off the plane, flat within half a millimetre and not within five hundredths.
            GeoPoint3[] quad = { P(0, 0, 0), P(100, 0, 0), P(100, 100, 0.2), P(0, 100, 0) };

            using (Tolerance.Use(new Tolerance(0.01, 0.01, Tolerance.DefaultEqualAngleRad, 0.5)))
            {
                Assert.Single(GeoFace3.FromLoops(quad, null));
            }

            using (Tolerance.Use(Default))
            {
                Assert.Equal(2, GeoFace3.FromLoops(quad, null).Length);
            }
        }

        private static GeoPoint3 P(double x, double y, double z) => new GeoPoint3(x, y, z);

        /// <summary>
        /// Whether a triangle, seen from above, covers a point in the plan.
        /// </summary>
        private static bool CoversFromAbove(GeoFace3 triangle, double x, double y)
        {
            IReadOnlyList<GeoPoint3> v = triangle.Boundary.Vertices;
            double d1 = Cross(v[0], v[1], x, y);
            double d2 = Cross(v[1], v[2], x, y);
            double d3 = Cross(v[2], v[0], x, y);

            return (d1 > 0 && d2 > 0 && d3 > 0) || (d1 < 0 && d2 < 0 && d3 < 0);
        }

        private static double Cross(GeoPoint3 a, GeoPoint3 b, double x, double y) => (b.X - a.X) * (y - a.Y) - (b.Y - a.Y) * (x - a.X);
    }
}
