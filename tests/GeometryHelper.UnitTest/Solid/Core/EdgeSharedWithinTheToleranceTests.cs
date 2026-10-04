using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid.Core
{
    /// <summary>
    /// A body two of whose faces share an edge only within the tolerance, as the booleans leave them, cut by a plane at a
    /// slant to that edge: each face put the crossing on its own copy of the edge, and the two crossings stood further
    /// apart than the tolerance, so the rim of the cut did not close.
    /// </summary>
    /// <remarks>
    /// The pieces came from booleans as they are, their copies of an edge a few thousandths apart, so they are cut within
    /// a hundredth, the default when they were found; within the default now, a thousandth, the copies are apart.
    /// </remarks>
    public class EdgeSharedWithinTheToleranceTests
    {
        private static readonly Tolerance Tolerance = new Tolerance(0.01, 0.01, Tolerance.DefaultEqualAngleRad, 0.01);

        private static GeoPoint3 P(double x, double y, double z) => new GeoPoint3(x, y, z);

        /// <summary>
        /// A piece of a box with a box taken out, turned: the edge its faces 0 and 4 share runs between corners 0.0045 apart
        /// at the top and 0.0063 at the foot.
        /// </summary>
        internal static GeoSolid3 Piece()
        {
            GeoPoint3[][] faces =
            {
                new[] { P(-707.27920926608408, 2167.5867391021034, 1683.8776336280507), P(-720.62157165854728, 2024.2108150848705, 1635.5258011787471), P(-720.62157165854717, 2024.2108150848703, 1385.7230946289947), P(-707.279209266084, 2167.5867391021034, 1434.0749270782983) },
                new[] { P(-736.848536321863, 2164.1588842500896, 1385.7177051066967), P(-720.62486722725453, 2024.2104330426562, 1385.7177051066967), P(-771.74830755304379, 2018.2838952948807, 1385.7177051066967), P(-875.86236502866507, 2027.9726034549935, 1385.7177051066967), P(-889.597135770488, 2146.45134471865, 1385.7177051066967) },
                new[] { P(-771.74830755304379, 2018.2838952948807, 1551.9139248695626), P(-875.86236502866507, 2027.9726034549935, 1385.7177051066967), P(-771.74830755304379, 2018.2838952948807, 1385.7177051066967) },
                new[] { P(-889.597135770488, 2146.45134471865, 1385.7177051066967), P(-875.86236502866507, 2027.9726034549935, 1385.7177051066967), P(-771.74830755304379, 2018.2838952948807, 1551.9139248695624), P(-720.62157165854728, 2024.2108150848703, 1635.5258011787471), P(-707.27920926608408, 2167.5867391021034, 1683.8776336280507) },
                new[] { P(-707.281541728038, 2167.5864687090366, 1434.0711126059487), P(-720.62486722725453, 2024.2104330426562, 1385.7177051066967), P(-736.848536321863, 2164.1588842500896, 1385.7177051066967) },
                new[] { P(-771.74830755304379, 2018.2838952948807, 1551.9139248695626), P(-771.74830755304379, 2018.2838952948807, 1385.7177051066967), P(-720.62157165854717, 2024.2108150848703, 1385.7230946289947), P(-720.62157165854728, 2024.2108150848703, 1635.5258011787471) },
                new[] { P(-707.281541728038, 2167.5864687090366, 1434.0711126059487), P(-736.848536321863, 2164.1588842500896, 1385.7177051066967), P(-889.597135770488, 2146.45134471865, 1385.7177051066967), P(-707.27920926608408, 2167.5867391021034, 1683.8776336280507) },
            };

            return new GeoSolid3(faces.Select(f => new GeoFace3(new GeoPolygon3(f, Tolerance))));
        }

        /// <summary>The plane the piece is cut by.</summary>
        internal static GeoPlane3 Plane() => new GeoPlane3(P(-1118.4862657678493, 1836.2587416680656, 1707.7401903945895), new GeoVector3(0.52072587539184489, 0.060365686211387375, 0.85158707518704468));

        [Fact]
        public void APlaneAtASlantToAnEdgeSharedWithinTheToleranceCutsTheBody()
        {
            // The plane meets the shared edge at 22 degrees, 1.077 below the top corner of one copy and 1.0727 below the
            // other's: the difference, 0.0045, put the two crossings 0.0112 apart along the edge. The rim is closed across
            // the gap.
            GeoSolid3 piece = Piece();
            GeoPlane3 plane = Plane();

            Assert.True(piece.IsClosed(Tolerance));
            Assert.True(piece.TrySplitBy(plane, out GeoSolid3 above, out GeoSolid3 below, Tolerance));
            Assert.True(above.IsClosed(Tolerance));
            Assert.True(below.IsClosed(Tolerance));
            // The piece holds its volume only to the crack between the two copies of the edge: measured from one corner or
            // another it holds 3 168 463 to 3 168 503, and the halves, each from a corner of its own, 31 more. Measured from
            // the same corner, the halves hold what the piece does.
            Assert.InRange((above.GetVolume() + below.GetVolume()) / piece.GetVolume(), 1 - 2E-5, 1 + 2E-5);

            foreach (GeoPoint3 corner in piece.Faces.SelectMany(f => f.Boundary.Vertices))
            {
                Assert.Equal(From(piece, corner), From(above, corner) + From(below, corner), 6);
            }

            Assert.Single(piece.Section(plane, Tolerance));
        }

        /// <summary>
        /// The piece of a thin L that a grid cell came from: where a face of the L runs between two cuts of the grid
        /// that meet it a hair apart, it is a strip narrowing down to nothing, and cut across where it was 0.0095 wide
        /// it was taken for no width. Its piece kept one corner of the narrow end and the cap the other, so its faces
        /// share their edge up the strip on copies 0.0095 apart at the foot and one at the top.
        /// </summary>
        internal static GeoSolid3 StripPiece()
        {
            GeoPoint3[][] faces =
            {
                new[] { P(240.00898068363858, 414.9654539401364, 1379.2314818669713), P(241.28303992713879, 413.64242540809033, 1368.8165523216296), P(241.28303992713887, 414.96545394013651, 1374.2225049631104) },
                new[] { P(241.28303992713873, 414.9654539401364, 1272.6915814575682), P(241.28303992713873, 414.94627776678988, 1272.6940174437516), P(241.26458899436818, 414.9654539401364, 1272.6893243533464) },
                new[] { P(241.27642639686508, 414.9654539401364, 1153.6077875893159), P(241.26458899436813, 414.9654539401364, 1272.6893243533464), P(241.2830399271387, 414.94627776678993, 1272.6940174437516) },
                new[] { P(241.28303992713876, 413.66465874139527, 1153.6077875893156), P(241.28303992713879, 413.64242540809033, 1368.8165523216296), P(240.00898068363858, 414.9654539401364, 1379.2314818669713), P(240.03140900086208, 414.9654539401364, 1153.6077875893156) },
                new[] { P(241.28303992713876, 414.95858062928562, 1153.6077875893159), P(241.28303992713873, 414.94627776678988, 1272.6940174437516), P(241.28303992713876, 414.9654539401364, 1272.6915814575682), P(241.28303992713887, 414.96545394013651, 1374.2225049631104), P(241.28303992713879, 413.64242540809033, 1368.8165523216296), P(241.28303992713879, 413.66465874139521, 1153.6077875893156) },
                new[] { P(240.03140900086208, 414.9654539401364, 1153.6077875893156), P(240.00898068363858, 414.9654539401364, 1379.2314818669713), P(241.28303992713887, 414.96545394013651, 1374.2225049631104), P(241.28303992713873, 414.9654539401364, 1272.6915814575682), P(241.26458899436813, 414.9654539401364, 1272.6893243533464), P(241.27642639686508, 414.9654539401364, 1153.6077875893159) },
                new[] { P(241.28303992713876, 413.66465874139527, 1153.6077875893156), P(240.03140900086208, 414.9654539401364, 1153.6077875893156), P(241.28303992713876, 414.95858062928562, 1153.6077875893159) },
            };

            // Built as the cut builds its pieces: the second face, 0.00018 of a square millimetre, is less than a polygon
            // takes at the tolerance itself.
            Tolerance pieces = GeometryHelper.Core.LoopAssembly.ForPieces(Tolerance);
            return new GeoSolid3(faces.Select(f => new GeoFace3(new GeoPolygon3(f, pieces), null, pieces)));
        }

        [Fact]
        public void ACutWhereTwoCopiesOfAnEdgeStandApartPastTheToleranceClosesTheSliverBetweenThem()
        {
            // Cut where the copies stand 0.0082 apart, the strip's two crossings 0.0036 apart were one point, the one on the
            // far copy: the rim was closed across the 0.0119 to the other copy, and the sliver above it, between the copies
            // up to where they meet, was left open, 102.8 long. A grid cell of the L came out so, not closed.
            GeoSolid3 piece = StripPiece();
            GeoPlane3 plane = new GeoPlane3(P(0, 0, 1169.85404376207), GeoVector3.ZAxis);

            Assert.True(piece.IsClosed(Tolerance));
            Assert.True(piece.TrySplitBy(plane, out GeoSolid3 above, out GeoSolid3 below, Tolerance));
            Assert.True(above.IsClosed(Tolerance), "the part above is open");
            Assert.True(below.IsClosed(Tolerance), "the part below is open");

            // The piece holds its volume only to the crack between the copies, a quarter of a percent of it measured from
            // its first corner; the sliver lies in its face through the corner where the copies meet, and measured from
            // there holds nothing, and the halves hold what the piece does.
            GeoPoint3 meet = P(241.28303992713873, 414.94627776678988, 1272.6940174437516);
            Assert.InRange((From(above, meet) + From(below, meet)) / From(piece, meet), 1 - 1E-6, 1 + 1E-6);
        }

        /// <summary>The volume a body's faces enclose, measured from a point by the triangles lying in them.</summary>
        private static double From(GeoSolid3 body, GeoPoint3 apex)
            => body.Faces
                .SelectMany(f => f.TriangulateSurface(Tolerance))
                .Sum(t => apex.GetVectorTo(t.A).DotProduct(apex.GetVectorTo(t.B).CrossProduct(apex.GetVectorTo(t.C))) / 6.0);
    }
}
