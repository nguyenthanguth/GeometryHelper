using System;
using System.Collections.Generic;
using GeometryHelper.SolidGeometry.Core;
using GeometryHelper.SolidGeometry.Geometry;
using Xunit;

namespace GeometryHelper.SolidGeometry.UnitTest.Core
{
    /// <summary>
    /// Covers what a cutting plane meets that is not a plain crossing: a vertex resting on it, an edge
    /// running along it, the rim of a hole lying in it.
    /// <para>
    /// The crossings along the cut line are paired off in order on the understanding that they alternate
    /// entering and leaving the material. Anything that puts a vertex on the plane without the boundary
    /// passing through to the other side breaks that alternation, and the pieces then come back
    /// overlapping — a body cut that way gains volume out of nothing while both halves still report
    /// themselves closed, so nothing downstream catches it. Every test here is an accounting check: what
    /// the two sides add up to must be what went in.
    /// </para>
    /// </summary>
    public class CutPlaneTouchTests
    {
        /// <summary>The plane y = 0, so that "above" is y &gt; 0.</summary>
        private static GeoPlane3 CutAtY(double y) => new GeoPlane3(new GeoPoint3(0, y, 0), GeoVector3.YAxis);

        private static double AreaOf(GeoPolygon3[] pieces)
        {
            double total = 0.0;
            foreach (GeoPolygon3 p in pieces) { total += p.Area; }
            return total;
        }

        private static double AreaOf(GeoFace3[] pieces)
        {
            double total = 0.0;
            foreach (GeoFace3 f in pieces) { total += f.Area; }
            return total;
        }

        private static GeoSolid3 Prism(GeoPoint3[] profile, double height)
        {
            List<GeoFace3> faces = new List<GeoFace3>();
            GeoPoint3[] top = new GeoPoint3[profile.Length];
            GeoPoint3[] bottomReversed = new GeoPoint3[profile.Length];

            for (int i = 0; i < profile.Length; i++)
            {
                top[i] = new GeoPoint3(profile[i].X, profile[i].Y, profile[i].Z + height);
                bottomReversed[i] = profile[profile.Length - 1 - i];
            }

            faces.Add(new GeoFace3(new GeoPolygon3(bottomReversed)));
            faces.Add(new GeoFace3(new GeoPolygon3(top)));

            for (int i = 0; i < profile.Length; i++)
            {
                int j = (i + 1) % profile.Length;
                faces.Add(new GeoFace3(new GeoPolygon3(profile[i], profile[j], top[j], top[i])));
            }

            return new GeoSolid3(faces);
        }

        /// <summary>
        /// A rectangle straddling the cut line, with a spur to the right whose tip rests exactly on it at
        /// (1, 0) and turns back up. The tip is on the plane but the outline never passes through it.
        /// </summary>
        private static GeoPoint3[] TangentialVertexProfile() => new[]
        {
            new GeoPoint3(-5, 4, 0), new GeoPoint3(-5, -4, 0),
            new GeoPoint3(-1, -4, 0), new GeoPoint3(-1, 4, 0),
            new GeoPoint3(1, 0, 0), new GeoPoint3(3, 4, 0)
        };

        #region A vertex resting on the plane

        [Fact]
        public void APolygonWithATangentialVertex_IsNotTornIntoOverlappingPieces()
        {
            GeoPolygon3 poly = new GeoPolygon3(TangentialVertexProfile());

            Assert.Equal(40.0, poly.Area, 9);
            Assert.True(poly.TrySplitBy(CutAtY(0), out GeoPolygon3[] above, out GeoPolygon3[] below));

            // The tip only grazes the cut, so it does not divide the upper part in two.
            Assert.Single(above);
            Assert.Single(below);

            Assert.Equal(24.0, AreaOf(above), 9);
            Assert.Equal(16.0, AreaOf(below), 9);
            Assert.Equal(poly.Area, AreaOf(above) + AreaOf(below), 9);
        }

        [Fact]
        public void ASolidWithATangentialEdge_ConservesItsVolume()
        {
            GeoSolid3 body = Prism(TangentialVertexProfile(), 3.0);

            Assert.Equal(120.0, body.Volume, 9);
            Assert.True(body.TrySplitBy(CutAtY(0), out GeoSolid3 above, out GeoSolid3 below));

            Assert.Equal(72.0, above.Volume, 9);
            Assert.Equal(48.0, below.Volume, 9);
            Assert.Equal(body.Volume, above.Volume + below.Volume, 9);

            Assert.True(above.IsClosed());
            Assert.True(below.IsClosed());
        }

        [Fact]
        public void SubtractingAcrossATangentialEdge_ConservesVolume()
        {
            GeoSolid3 body = Prism(TangentialVertexProfile(), 3.0);

            // A tool filling everything below the cut, its top face exactly on the plane the tip rests on.
            GeoSolid3 tool = Prism(new[]
            {
                new GeoPoint3(-6, -6, -1), new GeoPoint3(6, -6, -1),
                new GeoPoint3(6, 0, -1), new GeoPoint3(-6, 0, -1)
            }, 5.0);

            Assert.True(Boolean3.TrySubtract(body, tool, out GeoSolid3 rest));

            Assert.Equal(72.0, rest.Volume, 9);
            Assert.True(rest.IsClosed());
        }

        [Fact]
        public void APlaneThroughAVertexTheOutlineReallyCrossesAt_StillCuts()
        {
            // The control for the case above: the cut line passes through two vertices, but the outline
            // genuinely passes through to the other side at each of them.
            GeoPolygon3 poly = new GeoPolygon3(
                new GeoPoint3(-5, 4, 0), new GeoPoint3(-5, 0, 0), new GeoPoint3(-5, -4, 0),
                new GeoPoint3(-1, -4, 0), new GeoPoint3(-1, 0, 0), new GeoPoint3(-1, 4, 0));

            Assert.True(poly.TrySplitBy(CutAtY(0), out GeoPolygon3[] above, out GeoPolygon3[] below));

            Assert.Equal(16.0, AreaOf(above), 9);
            Assert.Equal(16.0, AreaOf(below), 9);
        }

        #endregion

        #region An edge running along the plane

        [Fact]
        public void AnOutlineEdgeLyingAlongTheCut_ConservesArea()
        {
            GeoPolygon3 poly = new GeoPolygon3(
                new GeoPoint3(-5, 4, 0), new GeoPoint3(-5, -4, 0),
                new GeoPoint3(-1, -4, 0), new GeoPoint3(-1, 4, 0),
                new GeoPoint3(1, 0, 0), new GeoPoint3(3, 0, 0), new GeoPoint3(4, 4, 0));

            Assert.True(poly.TrySplitBy(CutAtY(0), out GeoPolygon3[] above, out GeoPolygon3[] below));

            Assert.Equal(poly.Area, AreaOf(above) + AreaOf(below), 9);
            Assert.Equal(16.0, AreaOf(below), 9);
        }

        [Fact]
        public void AnLCutAlongThePlaneOfItsOwnNotch_GivesEachHalfItsOwnReach()
        {
            // The two halves meet the plane over different stretches: the foot reaches x = 10 along it,
            // the upright only x = 4. Taking one reach for both would leave the other half with a spur of
            // zero width.
            GeoSolid3 body = Prism(new[]
            {
                new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(10, 4, 0),
                new GeoPoint3(4, 4, 0), new GeoPoint3(4, 10, 0), new GeoPoint3(0, 10, 0)
            }, 6.0);

            Assert.Equal(384.0, body.Volume, 9);
            Assert.True(body.TrySplitBy(CutAtY(4), out GeoSolid3 above, out GeoSolid3 below));

            Assert.Equal(144.0, above.Volume, 9);
            Assert.Equal(240.0, below.Volume, 9);
            Assert.Equal(body.Volume, above.Volume + below.Volume, 9);

            Assert.True(above.IsClosed());
            Assert.True(below.IsClosed());

            // The upright does not reach past x = 4, so nothing of it may stick out to x = 10.
            Assert.True(above.GetAabb().Max.X <= 4.0 + 1E-6);
        }

        #endregion

        #region The rim of a hole lying in the plane

        private static GeoFace3 SquareWithHole(double z)
        {
            GeoPolygon3 outer = new GeoPolygon3(
                new GeoPoint3(0, 0, z), new GeoPoint3(10, 0, z),
                new GeoPoint3(10, 10, z), new GeoPoint3(0, 10, z));

            GeoPolygon3 hole = new GeoPolygon3(
                new GeoPoint3(1, 1, z), new GeoPoint3(3, 1, z),
                new GeoPoint3(3, 3, z), new GeoPoint3(1, 3, z));

            return new GeoFace3(outer, new[] { hole });
        }

        [Fact]
        public void AHoleWhoseRimLiesOnTheCut_MergesIntoTheBoundary()
        {
            GeoFace3 face = SquareWithHole(0.0);

            Assert.Equal(96.0, face.Area, 9);
            Assert.True(Splition3.TrySplitBy(face, CutAtY(1), out GeoFace3[] above, out GeoFace3[] below));

            Assert.Equal(96.0, AreaOf(above) + AreaOf(below), 9);
            Assert.Equal(86.0, AreaOf(above), 9);
            Assert.Equal(10.0, AreaOf(below), 9);

            // The rim runs along the cut, so the piece above it has an outline that goes round the hole
            // rather than a separate hole ring touching its own boundary.
            Assert.Single(above);
            Assert.Empty(above[0].Holes);
            Assert.Equal(8, above[0].Boundary.VertexCount);
        }

        [Fact]
        public void AHoleClearOfTheCut_StaysAHole()
        {
            GeoFace3 face = SquareWithHole(0.0);

            Assert.True(Splition3.TrySplitBy(face, CutAtY(5), out GeoFace3[] above, out GeoFace3[] below));

            Assert.Equal(96.0, AreaOf(above) + AreaOf(below), 9);
            Assert.Single(below);
            Assert.Single(below[0].Holes);
        }

        [Fact]
        public void ABodyCutAlongTheWallOfItsOwnCavity_ConservesVolume()
        {
            GeoSolid3 slab = Prism(new[]
            {
                new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0),
                new GeoPoint3(10, 10, 0), new GeoPoint3(0, 10, 0)
            }, 4.0);

            GeoSolid3 cavity = Prism(new[]
            {
                new GeoPoint3(1, 1, 1), new GeoPoint3(3, 1, 1),
                new GeoPoint3(3, 3, 1), new GeoPoint3(1, 3, 1)
            }, 2.0);

            Assert.True(Boolean3.TrySubtract(slab, cavity, out GeoSolid3 hollow));
            Assert.Equal(392.0, hollow.Volume, 9);

            // y = 1 holds one wall of the cavity, so the cut runs along it.
            Assert.True(hollow.TrySplitBy(CutAtY(1), out GeoSolid3 above, out GeoSolid3 below));

            Assert.Equal(hollow.Volume, above.Volume + below.Volume, 9);
            Assert.True(above.IsClosed());
            Assert.True(below.IsClosed());
        }

        [Fact]
        public void TakingASecondBodyOutOfOneThatAlreadyHasACavity()
        {
            GeoSolid3 slab = Prism(new[]
            {
                new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0),
                new GeoPoint3(10, 10, 0), new GeoPoint3(0, 10, 0)
            }, 4.0);

            GeoSolid3 first = Prism(new[]
            {
                new GeoPoint3(1, 1, 1), new GeoPoint3(3, 1, 1),
                new GeoPoint3(3, 3, 1), new GeoPoint3(1, 3, 1)
            }, 2.0);

            GeoSolid3 second = Prism(new[]
            {
                new GeoPoint3(5, 5, 0.5), new GeoPoint3(7, 5, 0.5),
                new GeoPoint3(7, 7, 0.5), new GeoPoint3(5, 7, 0.5)
            }, 3.0);

            Assert.True(Boolean3.TrySubtract(slab, first, out GeoSolid3 once));
            Assert.Equal(400.0 - 8.0, once.Volume, 9);

            Assert.True(Boolean3.TrySubtract(once, second, out GeoSolid3 twice));
            Assert.Equal(400.0 - 8.0 - 12.0, twice.Volume, 9);
            Assert.True(twice.IsClosed());
        }

        #endregion
    }
}
