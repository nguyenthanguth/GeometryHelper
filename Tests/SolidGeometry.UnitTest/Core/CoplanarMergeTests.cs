using System;
using System.Collections.Generic;
using SolidGeometry;
using SolidGeometry.Core;
using SolidGeometry.Geometry;
using Xunit;

namespace SolidGeometry.UnitTest.Core
{
    /// <summary>
    /// Covers putting a surface back into as few faces as describe it, which is what keeps repeated
    /// cutting from making the face count grow without the shape changing.
    /// </summary>
    public class CoplanarMergeTests
    {
        private static GeoSolid3 MakeBoxSolid(GeoPoint3 min, GeoPoint3 max) =>
            new GeoAabb3(min, max).ToObb().ToSolid();

        [Fact]
        public void TwoHalvesOfASquareBecomeTheSquareAgain()
        {
            GeoFace3 left = new GeoFace3(new GeoPolygon3(
                new GeoPoint3(0, 0, 0), new GeoPoint3(5, 0, 0),
                new GeoPoint3(5, 10, 0), new GeoPoint3(0, 10, 0)));

            GeoFace3 right = new GeoFace3(new GeoPolygon3(
                new GeoPoint3(5, 0, 0), new GeoPoint3(10, 0, 0),
                new GeoPoint3(10, 10, 0), new GeoPoint3(5, 10, 0)));

            GeoFace3[] merged = Merge3.CoplanarFaces(new[] { left, right });

            Assert.Single(merged);
            Assert.Equal(100.0, merged[0].Area, 6);
            Assert.True(merged[0].Normal.IsEqualTo(left.Normal));

            // The two vertices where the old shared edge met the outline are kept even though they sit in
            // the middle of a straight run: dropping them would leave this face meeting its neighbours at
            // a T-junction wherever they still have the vertex.
            Assert.Equal(6, merged[0].Boundary.VertexCount);
        }

        [Fact]
        public void FacesThatShareAPlaneButDoNotTouchStaySeparate()
        {
            GeoFace3 near = new GeoFace3(new GeoPolygon3(
                new GeoPoint3(0, 0, 0), new GeoPoint3(1, 0, 0),
                new GeoPoint3(1, 1, 0), new GeoPoint3(0, 1, 0)));

            GeoFace3 far = new GeoFace3(new GeoPolygon3(
                new GeoPoint3(50, 0, 0), new GeoPoint3(51, 0, 0),
                new GeoPoint3(51, 1, 0), new GeoPoint3(50, 1, 0)));

            GeoFace3[] merged = Merge3.CoplanarFaces(new[] { near, far });

            Assert.Equal(2, merged.Length);
            Assert.Equal(2.0, merged[0].Area + merged[1].Area, 6);
        }

        [Fact]
        public void FacesOnDifferentPlanesAreNotTouched()
        {
            GeoFace3 flat = new GeoFace3(new GeoPolygon3(
                new GeoPoint3(0, 0, 0), new GeoPoint3(1, 0, 0),
                new GeoPoint3(1, 1, 0), new GeoPoint3(0, 1, 0)));

            GeoFace3 upright = new GeoFace3(new GeoPolygon3(
                new GeoPoint3(0, 0, 0), new GeoPoint3(1, 0, 0),
                new GeoPoint3(1, 0, 1), new GeoPoint3(0, 0, 1)));

            Assert.Equal(2, Merge3.CoplanarFaces(new[] { flat, upright }).Length);
        }

        [Fact]
        public void AFaceAndOneFacingTheOtherWayAreNotMerged()
        {
            GeoFace3 up = new GeoFace3(new GeoPolygon3(
                new GeoPoint3(0, 0, 0), new GeoPoint3(1, 0, 0),
                new GeoPoint3(1, 1, 0), new GeoPoint3(0, 1, 0)));

            // The same flat place, looked at from the other side: a different surface.
            Assert.Equal(2, Merge3.CoplanarFaces(new[] { up, up.Flip() }).Length);
        }

        [Fact]
        public void ARingOfFacesKeepsTheHoleInTheMiddle()
        {
            // A three by three grid of squares with the middle one missing. Tiled edge to edge, so every
            // shared edge cancels against exactly one other; a coarser tiling would meet at T-junctions and
            // nothing would cancel.
            List<GeoFace3> ring = new List<GeoFace3>();

            for (int ix = 0; ix < 3; ix++)
            {
                for (int iy = 0; iy < 3; iy++)
                {
                    if (ix == 1 && iy == 1)
                    {
                        continue;
                    }

                    double x = ix * 3.0;
                    double y = iy * 3.0;

                    ring.Add(new GeoFace3(new GeoPolygon3(
                        new GeoPoint3(x, y, 0),
                        new GeoPoint3(x + 3, y, 0),
                        new GeoPoint3(x + 3, y + 3, 0),
                        new GeoPoint3(x, y + 3, 0))));
                }
            }

            GeoFace3[] merged = Merge3.CoplanarFaces(ring);

            Assert.Single(merged);
            Assert.Single(merged[0].Holes);

            // Nine by nine outer, three by three hole.
            Assert.Equal(81.0 - 9.0, merged[0].Area, 6);
        }

        [Fact]
        public void ATJunctionOnlyStopsTheFacesItAffects()
        {
            // One long strip against two short ones. The two short ones share a whole edge and join; the
            // long edge below them matches neither of their bottom edges, so nothing cancels there and that
            // last join does not happen. Merging under-joins rather than guessing, which keeps the area
            // right and the geometry honest.
            GeoFace3 wide = new GeoFace3(new GeoPolygon3(
                new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0),
                new GeoPoint3(10, 5, 0), new GeoPoint3(0, 5, 0)));

            GeoFace3 leftAbove = new GeoFace3(new GeoPolygon3(
                new GeoPoint3(0, 5, 0), new GeoPoint3(5, 5, 0),
                new GeoPoint3(5, 10, 0), new GeoPoint3(0, 10, 0)));

            GeoFace3 rightAbove = new GeoFace3(new GeoPolygon3(
                new GeoPoint3(5, 5, 0), new GeoPoint3(10, 5, 0),
                new GeoPoint3(10, 10, 0), new GeoPoint3(5, 10, 0)));

            GeoFace3[] merged = Merge3.CoplanarFaces(new[] { wide, leftAbove, rightAbove });

            Assert.Equal(2, merged.Length);

            double total = 0.0;
            foreach (GeoFace3 face in merged)
            {
                total += face.Area;
            }

            Assert.Equal(100.0, total, 6);
        }

        [Fact]
        public void MergingKeepsTheTotalArea()
        {
            GeoFace3 left = new GeoFace3(new GeoPolygon3(
                new GeoPoint3(0, 0, 0), new GeoPoint3(5, 0, 0),
                new GeoPoint3(5, 10, 0), new GeoPoint3(0, 10, 0)));

            GeoFace3 right = new GeoFace3(new GeoPolygon3(
                new GeoPoint3(5, 0, 0), new GeoPoint3(10, 0, 0),
                new GeoPoint3(10, 10, 0), new GeoPoint3(5, 10, 0)));

            GeoFace3[] merged = Merge3.CoplanarFaces(new[] { left, right });

            double before = left.Area + right.Area;
            double after = 0.0;
            foreach (GeoFace3 face in merged)
            {
                after += face.Area;
            }

            Assert.Equal(before, after, 6);
        }

        [Fact]
        public void ASurfaceSubdividedByACutGoesBackToSixFaces()
        {
            // Cutting a box in two and gluing the halves back together, cap excluded, rebuilds the box with
            // four of its six faces split in two. That is the surface a round of cutting leaves behind, and
            // what merging is for. A single cut on its own never leaves two coplanar faces on the same
            // side, so there would be nothing to merge in one half alone.
            GeoSolid3 box = MakeBoxSolid(GeoPoint3.Origin, new GeoPoint3(10, 10, 10));
            GeoPlane3 cutter = new GeoPlane3(new GeoPoint3(5, 0, 0), GeoVector3.XAxis);

            Assert.True(box.TrySplitBy(cutter, out GeoSolid3 upper, out GeoSolid3 lower));

            List<GeoFace3> glued = new List<GeoFace3>();

            foreach (GeoFace3 face in upper.Faces)
            {
                if (!cutter.ContainsAll(face.Boundary.Vertices))
                {
                    glued.Add(face);
                }
            }

            foreach (GeoFace3 face in lower.Faces)
            {
                if (!cutter.ContainsAll(face.Boundary.Vertices))
                {
                    glued.Add(face);
                }
            }

            GeoSolid3 subdivided = new GeoSolid3(glued);

            Assert.Equal(10, subdivided.Faces.Count);
            Assert.True(subdivided.IsClosed());
            Assert.Equal(1000.0, subdivided.Volume, 6);

            GeoSolid3 tidied = Merge3.CoplanarFaces(subdivided);

            Assert.Equal(6, tidied.Faces.Count);
            Assert.Equal(subdivided.Volume, tidied.Volume, 6);
            Assert.Equal(subdivided.SurfaceArea, tidied.SurfaceArea, 6);
            Assert.True(tidied.IsClosed());
        }

        [Fact]
        public void MergingAConcaveCutSolidKeepsItsVolume()
        {
            GeoSolid3 lShape = MakePrism(new[]
            {
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(10, 0, 0),
                new GeoPoint3(10, 4, 0),
                new GeoPoint3(4, 4, 0),
                new GeoPoint3(4, 10, 0),
                new GeoPoint3(0, 10, 0)
            }, 6.0);

            Assert.True(lShape.TrySplitBy(GeoPlane3.XY.Offset(3), out GeoSolid3 upper, out _));

            GeoSolid3 tidied = Merge3.CoplanarFaces(upper);

            Assert.True(tidied.Faces.Count <= upper.Faces.Count);
            Assert.Equal(upper.Volume, tidied.Volume, 6);
            Assert.True(tidied.IsClosed());
        }

        [Fact]
        public void AnAlreadyMinimalSolidIsLeftAlone()
        {
            GeoSolid3 cube = MakeBoxSolid(GeoPoint3.Origin, new GeoPoint3(10, 10, 10));
            GeoSolid3 tidied = Merge3.CoplanarFaces(cube);

            Assert.Equal(6, tidied.Faces.Count);
            Assert.Equal(cube.Volume, tidied.Volume, 6);
        }

        [Fact]
        public void NullArgumentsAreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => Merge3.CoplanarFaces((IEnumerable<GeoFace3>)null));
            Assert.Throws<ArgumentNullException>(() => Merge3.CoplanarFaces((GeoSolid3)null));
        }

        /// <summary>
        /// Builds a closed prism from a profile walked counter-clockwise seen from +Z.
        /// </summary>
        private static GeoSolid3 MakePrism(GeoPoint3[] profile, double height)
        {
            GeoPoint3[] top = new GeoPoint3[profile.Length];
            for (int i = 0; i < profile.Length; i++)
            {
                top[i] = profile[i].Add(new GeoVector3(0, 0, height));
            }

            List<GeoFace3> faces = new List<GeoFace3>();

            GeoPoint3[] bottomReversed = new GeoPoint3[profile.Length];
            for (int i = 0; i < profile.Length; i++)
            {
                bottomReversed[i] = profile[profile.Length - 1 - i];
            }

            faces.Add(new GeoFace3(new GeoPolygon3(bottomReversed)));
            faces.Add(new GeoFace3(new GeoPolygon3(top)));

            for (int i = 0; i < profile.Length; i++)
            {
                int next = (i + 1) % profile.Length;
                faces.Add(new GeoFace3(new GeoPolygon3(profile[i], profile[next], top[next], top[i])));
            }

            return new GeoSolid3(faces);
        }
    }
}
