using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Geometry;
using GeometryHelper.Meshing;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// The ellipse in space cut into straight pieces and meshed (decisions 12 and 13): the 2D ellipse's polygon, chain
    /// and mesh laid out in its own frame and lifted, so that the counts, the symmetry and the sag are the 2D ones. The
    /// members are the circle in space's; the mesh mirrors Mesh3.ToMesh(GeoCircle3, ...) (Mesh3.cs:253-268), which lays
    /// the disc out, meshes it in 2D and lifts it.
    /// </summary>
    public class Ellipse3FlatteningTests
    {
        private static readonly Tolerance Tight = new Tolerance(1E-9, 1E-12);

        private static readonly Tolerance Tol = Tolerance.Default;

        private static void AssertLifted(Ellipse3Oracle.Specimen specimen, IReadOnlyList<GeoPoint2> flat, IReadOnlyList<GeoPoint3> lifted)
        {
            Assert.Equal(flat.Count, lifted.Count);

            for (int i = 0; i < flat.Count; i++)
            {
                Assert.True(lifted[i].IsEqualTo(specimen.Lift(flat[i]), Tight), $"vertex {i}: {lifted[i]}");
            }
        }

        [Fact]
        public void EachWayOfFlatteningIsTheEllipseLaidOutsLifted()
        {
            // The automatic chord tolerance, half a unit, a spacing of 25 and seven edges, as polygons and as chains.
            Ellipse3Oracle.Specimen specimen = Ellipse3Oracle.Tilted();
            GeoEllipse3 ellipse = specimen.Build();
            GeoEllipse2 flat = specimen.Flat();

            AssertLifted(specimen, flat.ToPolygon().Vertices, ellipse.ToPolygon().Vertices);
            AssertLifted(specimen, flat.ToPolygonByChordTolerance(0.5).Vertices, ellipse.ToPolygonByChordTolerance(0.5).Vertices);
            AssertLifted(specimen, flat.ToPolygonBySpacing(25).Vertices, ellipse.ToPolygonBySpacing(25).Vertices);
            AssertLifted(specimen, flat.ToPolygon(7).Vertices, ellipse.ToPolygon(7).Vertices);

            AssertLifted(specimen, flat.ToPolyline().Vertices, ellipse.ToPolyline().Vertices);
            AssertLifted(specimen, flat.ToPolylineByChordTolerance(0.5).Vertices, ellipse.ToPolylineByChordTolerance(0.5).Vertices);
            AssertLifted(specimen, flat.ToPolylineBySpacing(25).Vertices, ellipse.ToPolylineBySpacing(25).Vertices);
            AssertLifted(specimen, flat.ToPolyline(7).Vertices, ellipse.ToPolyline(7).Vertices);
        }

        [Fact]
        public void ThePolygonStartsAtTheEndOfTheMajorAxisAndRunsCounterClockwiseAboutTheNormal()
        {
            Ellipse3Oracle.Specimen specimen = Ellipse3Oracle.Tilted();
            GeoPolygon3 polygon = specimen.Build().ToPolygon();

            Assert.True(polygon.Vertices[0].IsEqualTo(specimen.At(300, 0, 0), Tight));
            Assert.True(polygon.Normal.IsEqualTo(specimen.N, Tight));

            // The chain closes on its first point.
            GeoPolyline3 chain = specimen.Build().ToPolyline();
            Assert.Equal(chain.Vertices[0], chain.Vertices[chain.VertexCount - 1]);
        }

        [Fact]
        public void ThePolygonIsSymmetricAboutBothAxes()
        {
            // 2D decision 10: built in the first quadrant and mirrored, so every corner read in the frame has its mirror
            // across each axis among the corners, and one sits at each end of each axis.
            Ellipse3Oracle.Specimen specimen = Ellipse3Oracle.Tilted(1000, 1);
            GeoPolygon3 polygon = specimen.Build().ToPolygonByChordTolerance(0.0);
            var corners = new List<GeoPoint3>(polygon.Vertices);

            Assert.Equal(0, corners.Count % 4);

            foreach (GeoPoint3 corner in polygon.Vertices)
            {
                specimen.Read(corner, out double u, out double v, out _);
                Assert.Contains(corners, c => c.IsEqualTo(specimen.At(u, -v, 0), Tight));
                Assert.Contains(corners, c => c.IsEqualTo(specimen.At(-u, v, 0), Tight));
            }

            Assert.Contains(corners, c => c.IsEqualTo(specimen.At(0, 1, 0), Tight));
            Assert.Contains(corners, c => c.IsEqualTo(specimen.At(-1000, 0, 0), Tight));
        }

        [Fact]
        public void AnEllipseThatIsACircleIsCutAsTheCircleIsOrUpToAMultipleOfFour()
        {
            // 2D decision 10 measured it: as many corners as GeoCircle3, or up to three more to make a multiple of four.
            var circle = new GeoCircle3(new GeoPoint3(100, 200, 300), new GeoVector3(1, -1, 2), 250);
            int circles = circle.ToPolygonByChordTolerance(0.0).VertexCount;
            int ellipses = GeoEllipse3.FromCircle(circle).ToPolygonByChordTolerance(0.0).VertexCount;

            Assert.Equal(GeoEllipse2.FromCircle(new GeoCircle2(new GeoPoint2(0, 0), 250)).ToPolygonByChordTolerance(0.0).VertexCount, ellipses);
            Assert.InRange(ellipses, circles, circles + 3);
            Assert.Equal(0, ellipses % 4);
        }

        [Fact]
        public void TheArgumentChecksAreTheCircles()
        {
            // Fewer than three edges, a chord tolerance negative or not a number, a spacing not positive.
            GeoEllipse3 ellipse = Ellipse3Oracle.Tilted().Build();

            Assert.Throws<ArgumentOutOfRangeException>(() => ellipse.ToPolygon(2));
            Assert.Throws<ArgumentOutOfRangeException>(() => ellipse.ToPolyline(2));
            Assert.Throws<ArgumentOutOfRangeException>(() => ellipse.ToPolygonByChordTolerance(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => ellipse.ToPolygonByChordTolerance(double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => ellipse.ToPolylineByChordTolerance(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => ellipse.ToPolygonBySpacing(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => ellipse.ToPolylineBySpacing(-5));
        }

        [Fact]
        public void AnEllipticalDiscInSpaceIsFannedFromItsCentre()
        {
            // As the circle's (Mesh3Tests.ADiscInSpaceIsFannedFromItsCenter): one triangle per edge of the automatic
            // polygon, the centre a vertex, the normal the ellipse's, the area the polygon's.
            Ellipse3Oracle.Specimen specimen = Ellipse3Oracle.Tilted(1500, 600);
            GeoEllipse3 ellipse = specimen.Build();
            GeoMesh3 fan = ellipse.ToMesh(MeshOptions.Triangles, Tol);
            GeoPolygon3 rim = ellipse.ToPolygonByChordTolerance(0.0);

            Assert.Equal(rim.VertexCount, fan.FaceCount);
            Assert.Contains(specimen.Center, fan.Vertices);
            Assert.True(fan.Normal.IsEqualTo(specimen.N, Tight));
            Assert.Equal(rim.Area, fan.Area, 1E-3);

            // Its triangles are the 2D ellipse's.
            Assert.Equal(specimen.Flat().ToMesh(MeshOptions.Triangles, Tol).FaceCount, fan.FaceCount);
        }

        [Fact]
        public void AGridOverAnEllipticalDiscKeepsToTheDiscAndItsPlane()
        {
            // Cells of 400 centred on the disc, its grid level: the area is the polygon's, and every vertex lies in the
            // plane and within the rim, read in the frame the test wrote down.
            Ellipse3Oracle.Specimen specimen = Ellipse3Oracle.Tilted(1500, 600);
            GeoEllipse3 ellipse = specimen.Build();
            GeoPolygon3 rim = ellipse.ToPolygonByChordTolerance(0.0);

            foreach (MeshPlacement3 placement in new[] { MeshPlacement3.World, MeshPlacement3.Own })
            {
                GeoMesh3 grid = ellipse.ToMesh(MeshOptions.Grid(400, 400, 0, GridAlignment.CenterCell, GridAlignment.CenterCell), placement, Tol);

                Assert.Equal(rim.Area, grid.Area, 1E-3);
                Assert.True(grid.FaceCount > 20);

                foreach (GeoPoint3 vertex in grid.Vertices)
                {
                    specimen.Read(vertex, out double u, out double v, out double h);
                    Assert.True((u / 1500) * (u / 1500) + (v / 600) * (v / 600) <= 1 + 1E-9, $"{vertex} lies outside the disc");
                    Assert.True(Math.Abs(h) < 1E-9, $"{vertex} lies off the disc's plane");
                }
            }
        }

        [Fact]
        public void ADiscOfNoWidthHasNoFaces()
        {
            // As the circle's (Mesh3Tests.ADiscOfNoSizeHasNoFaces): the default has no radii and no normal; one 300 by
            // 0.0005 is half the point tolerance wide (decision 13). One 300 by 5 has faces.
            Assert.Equal(0, default(GeoEllipse3).ToMesh(MeshOptions.Triangles, Tol).FaceCount);
            Assert.Equal(0, default(GeoEllipse3).ToMesh(MeshOptions.Grid(10, 10), MeshPlacement3.Own, Tol).FaceCount);
            Assert.Equal(0, Ellipse3Oracle.Tilted(300, 0.0005).Build().ToMesh(MeshOptions.Convex, Tol).FaceCount);
            Assert.Equal(0, Ellipse3Oracle.Tilted(300, 0.0005).Build().ToMesh(MeshOptions.Triangles, Tol).FaceCount);
            Assert.True(Ellipse3Oracle.Tilted(300, 5).Build().ToMesh(MeshOptions.Triangles, Tol).FaceCount > 0);
        }
    }
}
