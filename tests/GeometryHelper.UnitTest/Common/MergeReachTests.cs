using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Extension;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Common
{
    /// <summary>
    /// Merging was the one thing `Core` could do that nothing else could reach: joining a bag of segments
    /// into chains was written twice over, in <see cref="Merge2"/> and <see cref="Merge3"/>, and a caller had
    /// to name the core class to get at it.
    /// </summary>
    /// <remarks>
    /// <see cref="Merge2"/>'s four methods were also the only public API in the library that demanded an
    /// explicit tolerance, so the default-tolerance twins are held here too.
    /// </remarks>
    public class MergeReachTests
    {
        [Fact]
        public void ABagOfSegmentsBecomesChains()
        {
            // Two runs, their segments given out of order and pointing both ways.
            var scattered = new List<GeoLine2>
            {
                new GeoLine2(new GeoPoint2(10, 0), new GeoPoint2(20, 0)),
                new GeoLine2(new GeoPoint2(100, 0), new GeoPoint2(110, 0)),
                new GeoLine2(new GeoPoint2(10, 0), new GeoPoint2(0, 0)),
                new GeoLine2(new GeoPoint2(120, 0), new GeoPoint2(110, 0)),
            };

            GeoPolyline2[] chains = scattered.Join();

            Assert.Equal(2, chains.Length);
            Assert.Equal(Merge2.Join(scattered).Length, chains.Length);

            // The first run goes 0 to 20 and the second 100 to 120, whichever order they come back in.
            double[] lengths = chains.Select(chain => chain.Length).OrderBy(length => length).ToArray();

            Assert.Equal(20.0, lengths[0], 6);
            Assert.Equal(20.0, lengths[1], 6);

            var apart = new List<GeoLine2>
            {
                new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(10, 0)),
                new GeoLine2(new GeoPoint2(500, 0), new GeoPoint2(510, 0)),
            };

            Assert.Equal(2, apart.Join().Length);
        }

        [Fact]
        public void TheSameWorksInSpaceAndForChainsAsWellAsSegments()
        {
            var scattered = new List<GeoLine3>
            {
                new GeoLine3(new GeoPoint3(10, 0, 0), new GeoPoint3(10, 10, 0)),
                new GeoLine3(new GeoPoint3(10, 0, 0), new GeoPoint3(0, 0, 0)),
            };

            GeoPolyline3[] chains = scattered.Join();

            Assert.Single(chains);
            Assert.Equal(20.0, chains[0].Length, 6);
            Assert.Equal(Merge3.Join(scattered).Length, chains.Length);

            var pieces = new List<GeoPolyline3>
            {
                new GeoPolyline3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0)),
                new GeoPolyline3(new GeoPoint3(10, 0, 0), new GeoPoint3(10, 10, 0)),
            };

            Assert.Single(pieces.Join());
            Assert.Equal(20.0, pieces.MergeIntoOne().Length, 6);
            Assert.Equal(20.0, pieces.MergeConsecutive()[0].Length, 6);
        }

        [Fact]
        public void PiecesAlreadyInOrderAreRejoinedWithoutSearching()
        {
            // What splitting hands back: in order, and only touching pieces should run together.
            var run = new List<GeoLine2>
            {
                new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(10, 0)),
                new GeoLine2(new GeoPoint2(10, 0), new GeoPoint2(20, 0)),
                new GeoLine2(new GeoPoint2(50, 0), new GeoPoint2(60, 0)),
            };

            GeoLine2[] stretches = run.MergeConsecutive();

            Assert.Equal(2, stretches.Length);
            Assert.Equal(20.0, stretches[0].Length, 6);
            Assert.Equal(10.0, stretches[1].Length, 6);

            var chains = new List<GeoPolyline2>
            {
                new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(10, 0)),
                new GeoPolyline2(new GeoPoint2(10, 0), new GeoPoint2(10, 10)),
                new GeoPolyline2(new GeoPoint2(50, 0), new GeoPoint2(60, 0)),
            };

            Assert.Equal(2, chains.MergeConsecutive().Length);
        }

        [Fact]
        public void TwoChainsJoinOnlyWhereOneEndsAtTheOtherStart()
        {
            var first = new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(10, 0));
            var touching = new GeoPolyline2(new GeoPoint2(10, 0), new GeoPoint2(10, 10));
            var apart = new GeoPolyline2(new GeoPoint2(50, 0), new GeoPoint2(60, 0));

            GeoPolyline2 joined = first.MergeWith(touching);

            Assert.NotNull(joined);
            Assert.Equal(20.0, joined.Length, 6);

            // Not meeting is the ordinary case, and the answer is null rather than an exception.
            Assert.Null(first.MergeWith(apart));
        }

        [Fact]
        public void ATriangulatedSkinGoesBackToTheFlatPanelsItStandsFor()
        {
            GeoSolid3 cube = new GeoAabb3(GeoPoint3.Origin, new GeoPoint3(100, 100, 100)).ToObb().ToSolid();

            // Triangulating and rebuilding gives twelve triangles standing for six panels.
            GeoTriangle3[] mesh = cube.Triangulate();

            Assert.Equal(12, mesh.Length);

            var skin = new GeoSolid3(
                mesh.Select(triangle => new GeoFace3(new GeoPolygon3(triangle.A, triangle.B, triangle.C))).ToArray());

            Assert.Equal(12, skin.Faces.Count);
            Assert.Equal(6, skin.MergeCoplanarFaces().Faces.Count);
            Assert.Equal(cube.Volume, skin.MergeCoplanarFaces().Volume, 6);

            // The collection form answers the same about the faces on their own.
            Assert.Equal(6, skin.Faces.MergeCoplanar().Length);
        }

        [Fact]
        public void EveryMergeTakesATolerance()
        {
            Tolerance global = Tolerance.Global;

            var segments = new List<GeoLine2>
            {
                new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(10, 0)),
                new GeoLine2(new GeoPoint2(10, 0), new GeoPoint2(20, 0)),
            };
            var chains = new List<GeoPolyline2>
            {
                new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(10, 0)),
                new GeoPolyline2(new GeoPoint2(10, 0), new GeoPoint2(10, 10)),
            };

            // Merge2's four were the only public methods in the library with no default-tolerance twin.
            Assert.Equal(Merge2.Join(segments, global).Length, Merge2.Join(segments).Length);
            Assert.Equal(Merge2.ConsecutiveLines(segments, global).Length, Merge2.ConsecutiveLines(segments).Length);
            Assert.Equal(Merge2.ConsecutivePolylines(chains, global).Length, Merge2.ConsecutivePolylines(chains).Length);
            Assert.Equal(
                Merge2.Polylines(chains[0], chains[1], global).Length,
                Merge2.Polylines(chains[0], chains[1]).Length, 9);

            Assert.Equal(segments.Join(global).Length, segments.Join().Length);
            Assert.Equal(segments.MergeConsecutive(global).Length, segments.MergeConsecutive().Length);
            Assert.Equal(chains.MergeConsecutive(global).Length, chains.MergeConsecutive().Length);
            Assert.Equal(chains[0].MergeWith(chains[1], global).Length, chains[0].MergeWith(chains[1]).Length, 9);
        }

        [Fact]
        public void NothingIsAskedOfNothing()
        {
            Assert.Throws<ArgumentNullException>(() => ((IEnumerable<GeoLine2>)null).Join());
            Assert.Throws<ArgumentNullException>(() => ((IEnumerable<GeoLine3>)null).Join());
            Assert.Throws<ArgumentNullException>(() => ((IEnumerable<GeoLine2>)null).MergeConsecutive());
            Assert.Throws<ArgumentNullException>(() => ((IEnumerable<GeoFace3>)null).MergeCoplanar());
            Assert.Throws<ArgumentNullException>(() => ((IEnumerable<GeoPolyline3>)null).MergeIntoOne());
        }
    }
}
