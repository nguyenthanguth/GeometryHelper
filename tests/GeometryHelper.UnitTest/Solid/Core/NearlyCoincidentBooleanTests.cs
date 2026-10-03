using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Booleans on bodies that meet the way slabs drawn side by side in a model do: faces within a few hundredths of a
    /// millimetre of each other, turned a fraction of a microradian, so that every plane of one cuts the other along a
    /// sliver thinner than the tolerance. Concrete volumes taken from such slabs were thousands of cubic millimetres off,
    /// a slab touching another came out smaller or larger for having the other taken from it, and what a difference took
    /// was not what the two had in common.
    /// </summary>
    public class NearlyCoincidentBooleanTests
    {
        private static readonly Tolerance Tolerance = Tolerance.Default;

        [Fact]
        public void ACapNarrowerThanItsTiltKeepsTheSideItFaces()
        {
            // A plate three hundredths thick with a corner of its edge two thousandths out, cut across where that corner is
            // three thousandths short of the plane: the cap runs from the corner, on the plane within the tolerance, to the
            // far face, and stands five degrees off the plane over its three hundredths. A piece more than the angle
            // tolerance off the way it was asked to face was turned over, so each half had its cap facing into itself.
            GeoSolid3 plate = Prism(new[] { P(0, 0), P(0.03, 0), P(0.03, 200), P(0, 200), P(-0.002, 100) }, 0, 300);
            var plane = new GeoPlane3(new GeoPoint3(0, 100.0028, 0), GeoVector3.YAxis);

            Assert.True(Splition3.TrySplitBy(plate, plane, out GeoSolid3 above, out GeoSolid3 below, Tolerance));

            foreach (GeoSolid3 half in new[] { above, below })
            {
                Assert.True(half.GetSignedVolume() > 0.0);
                Assert.Equal(VolumeFrom(half, new GeoPoint3(-500, -500, -500)), VolumeFrom(half, new GeoPoint3(500, 700, 900)), 6);
            }

            Assert.Equal(plate.GetVolume(), above.GetVolume() + below.GetVolume(), 6);
        }

        [Fact]
        public void ASliverAlongTheSubjectsFaceIsNotTakenWithTheTool()
        {
            // A slab, and an L-shaped one beside it whose notch lies 0.0165 inside the first's west face and whose long edge
            // 0.025 inside its north face. The planes of the second cut slivers 0.0165 thick off the first along its whole
            // west face, too thin for any point of them to be further than the tolerance from their skin; taken for empty,
            // they went with the difference, seventy-four thousand cubic millimetres of slab nowhere near the other one.
            GeoSolid3 slab = Prism(new[] { P(0, 0), P(4200, 0), P(4200, 4200), P(0, 4200) }, 0, 1100);
            GeoSolid3 beside = Prism(new[] { P(0.0165, 4199.975), P(6000, 4199.975), P(6000, 5000), P(-1000, 5000), P(-1000, 4100), P(0.0165, 4100) }, 0, 1100);

            Assert.True(slab.TrySubtract(beside, out GeoSolid3 rest, Tolerance));
            Assert.True(slab.TryIntersect(beside, out GeoSolid3 shared, Tolerance));
            double taken = slab.GetVolume() - rest.GetVolume();

            // What the two have in common is the strip 0.025 deep along the north face, and the corner 0.0165 deep at its
            // west end; the corner is thinner than twice the tolerance, and either answer is right for it.
            double strip = (4200 - 0.0165) * 0.025 * 1100;
            double corner = 0.0165 * 100 * 1100;
            Assert.InRange(taken, strip - 1, strip + corner + 1);
            Assert.Equal(shared.GetVolume(), taken, 0);
            Assert.True(rest.IsClosed(Tolerance));
        }

        [Fact]
        public void AFaceCutAlongANotchAHairOffThePlaneLosesNothingBetweenItsPieces()
        {
            // An L 60 m across, cut by a plane turned 0.42 microradians off its notch edge: the notch corner lies a ten
            // thousandth to one side of the plane, the far end of the edge eight ten-thousandths to the other, and the
            // plane leaves the face 73 m away. The piece the notch edge borders stepped across it from the far end, the other from the
            // corner, and between the two lines lay a sliver of the face that went to neither.
            GeoFace3 face = new GeoFace3(new GeoPolygon3(new[]
            {
                P(-20000, 0), P(40000, 0), P(40000, 73725), P(0, 73725), P(0, 71550), P(-20000, 71550),
            }, Tolerance), null, Tolerance);
            GeoPlane3 plane = AlongTheNotch();

            Assert.True(Splition3.TrySplitBy(face, plane, out GeoFace3[] above, out GeoFace3[] below, Tolerance));
            Assert.Equal(face.Area, above.Sum(piece => piece.Area) + below.Sum(piece => piece.Area), 3);
        }

        [Fact]
        public void ASlabTouchingItsNeighbourWithinTheToleranceComesBackAsItWas()
        {
            // The L above as a slab, and a slab filling its notch: the two meet along the notch edge eight ten-thousandths
            // apart at most and turned as the plane above is, and a ten-thousandth apart along the notch's foot. Nothing of
            // one lies in the other. Cut by the planes of the second and glued back together, the first came out larger,
            // by what the cutting rounded along seventy metres.
            GeoSolid3 slab = Prism(new[] { P(-20000, 0), P(40000, 0), P(40000, 73725), P(0, 73725), P(0, 71550), P(-20000, 71550) }, 0, 300);
            GeoPlane3 plane = AlongTheNotch();
            GeoSolid3 notch = Prism(new[] { P(-20000, 71550.0001), P(OnPlane(plane, 71550.0001), 71550.0001), P(OnPlane(plane, 73725), 73725), P(-20000, 73725) }, 0, 300);

            Assert.True(slab.TrySubtract(notch, out GeoSolid3 rest, Tolerance));
            Assert.False(slab.TryIntersect(notch, out _, Tolerance));
            Assert.Same(slab, rest);
        }

        [Fact]
        public void AWedgeTooThinToCutDoesNotSpoilTheCut()
        {
            // A slab with a row of ten pits, and an L-shaped one round two sides of it, a hair into it along its north face:
            // a hundredth of a millimetre at the west end, nothing three metres on, and the L ends before that. The end of
            // the L crosses the wedge where it is two thousandths across, which cuts nothing, a piece that thin being no
            // polygon. Counted as a cut that failed, it threw the cut away, the pits made cutting the L the other way too
            // dear to try, and both were cut by every plane of both.
            var pits = Enumerable.Range(0, 10).Select(k => new[] { P(200 + 220 * k, 400), P(300 + 220 * k, 400), P(300 + 220 * k, 500), P(200 + 220 * k, 500) }).ToArray();
            GeoSolid3 slab = Plate(new[] { P(0, 0), P(3000, 0), P(3000, 1000), P(0, 1000) }, pits, 0, 1100);
            GeoSolid3 beside = Prism(new[] { P(-600, -500), P(-100, -500), P(-100, Edge(-100)), P(2492.5, Edge(2492.5)), P(2492.5, 1500), P(-600, 1500) }, 0, 1100);

            // The wedge is never twice the tolerance thick: the two touch, and neither takes anything from the other.
            Assert.True(slab.TrySubtract(beside, out GeoSolid3 rest, Tolerance));
            Assert.Same(slab, rest);
            Assert.True(beside.TrySubtract(slab, out rest, Tolerance));
            Assert.Same(beside, rest);
            Assert.False(slab.TryIntersect(beside, out _, Tolerance));
        }

        [Fact]
        public void AShellThinnerThanTheToleranceIsAPieceOfItsOwn()
        {
            // A block, and apart from it a plate five thousandths thick: thinner on average than the tolerance, so no sheet
            // of no thickness either. Nothing holds it, and it was dropped, and the slivers a plane leaves along the face of
            // a slab went with it, seventy thousand cubic millimetres of one.
            var fine = new Tolerance(1E-4, 1E-8, Tolerance.DefaultEqualAngleRad, 1E-4);
            var faces = new List<GeoFace3>(Prism(new[] { P(0, 0), P(100, 0), P(100, 100), P(0, 100) }, 0, 100, fine).Faces);
            faces.AddRange(Prism(new[] { P(200, 0), P(1200, 0), P(1200, 0.005), P(200, 0.005) }, 0, 300, fine).Faces);
            var body = new GeoSolid3(faces);

            GeoSolid3[] pieces = Boolean3.SplitShells(body, Tolerance);

            Assert.Equal(2, pieces.Length);
            Assert.Equal(body.GetVolume(), pieces.Sum(piece => piece.GetVolume()), 6);
        }

        [Fact]
        public void ASlabWrappingTheCornerOfAnotherTakesTheWedgeTheyShareEitherWay()
        {
            // Taken from the slab that wraps the other's corner, the other took nothing: cut by the other's few planes it
            // could not be cut through the corner, and the planes of both, cut by instead, held the other's long side for
            // the face of the wedge, the two through one point and turned 0.32 milliradians, 5.8 apart at the far end.
            WrappedCorner(out GeoSolid3 first, out GeoSolid3 second, out double wedge);

            Assert.True(Boolean3.TrySubtract(first, second, out GeoSolid3 firstRest, Tolerance));
            Assert.True(Boolean3.TrySubtract(second, first, out GeoSolid3 secondRest, Tolerance));
            AssertNear(wedge, first.GetVolume() - firstRest.GetVolume(), "the first less the second");
            AssertNear(wedge, second.GetVolume() - secondRest.GetVolume(), "the second less the first");
            Assert.True(firstRest.IsClosed(Tolerance));
            Assert.True(secondRest.IsClosed(Tolerance));

            // The first with the second as an opening is the same material.
            AssertNear(wedge, first.GetVolume() - first.WithOpenings(new[] { second }).GetVolume(Tolerance), "the second cut in as an opening");
        }

        [Fact]
        public void ASlabWrappingTheCornerOfAnotherSharesTheWholeWedgeWithIt()
        {
            // The common part lost the wedge's tip, 6.4 m of it narrower than 2 mm: the slab wrapping the corner, cut by the
            // other's planes, kept the tip and the part of it beyond the other's end in one cell the other's end could not
            // cut, and one point of that cell, beyond the end, said the cell was outside the other.
            WrappedCorner(out GeoSolid3 first, out GeoSolid3 second, out double wedge);

            Assert.True(Boolean3.TryIntersect(first, second, out GeoSolid3 shared, Tolerance));
            AssertNear(wedge, shared.GetVolume(), "the common part");
            Assert.True(shared.IsClosed(Tolerance));
            AssertNear(wedge, Boolean3.Intersect(first, second, Tolerance).Sum(piece => piece.GetVolume()), "the pieces shared");

            Assert.True(Boolean3.TryUnion(first, second, out GeoSolid3 both, Tolerance));
            AssertNear(first.GetVolume() + second.GetVolume() - wedge, both.GetVolume(), "the two together");
        }

        /// <summary>
        /// Two slabs as drawn side by side, the second wrapping a corner of the first: along the first's end a hair off it,
        /// and from the corner, a hair outside it, along its long side at a slant, 5.8 into it 18 m on, so that the two
        /// share a wedge 300 thick from nothing at the corner. An island of the second in the first's notch has square holes
        /// in it, so that more of the second's planes come near the first than cutting the first by them was let cost.
        /// </summary>
        private static void WrappedCorner(out GeoSolid3 first, out GeoSolid3 second, out double wedge)
        {
            first = Slab(new[] { (0.0, 0.0), (18000.0, 0.0), (18000.0, 3000.0), (4000.0, 3000.0), (4000.0, 9000.0), (0.0, 9000.0) }, 6600, 7500, null);

            double rise = 5.8 / 18000.0;
            var wraps = Slab(new[] { (-3000.0, -5000.0), (21000.0, -5000.0), (21000.0, rise * 21000.0 - 0.00005), (-0.0003, -0.00005), (-0.0002, 9000.0), (-3000.0, 9000.0) }, 7200, 7500, null);
            var holes = Enumerable.Range(0, 12).Select(k => new[] { (5300.0 + k * 700, 4300.0 + k * 230), (5700.0 + k * 700, 4300.0 + k * 230), (5700.0 + k * 700, 4700.0 + k * 230), (5300.0 + k * 700, 4700.0 + k * 230) });
            var island = Slab(new[] { (5000.0, 4000.0), (17000.0, 4000.0), (17000.0, 8500.0), (5000.0, 8500.0) }, 7200, 7500, holes);
            second = new GeoSolid3(wraps.Faces.Concat(island.Faces));
            wedge = 0.5 * 5.8 * 18000.0 * 300.0;
        }

        /// <summary>A slab of an outline wound counter-clockwise in XY, holes and all, made as GeoSolid3.Extrude makes it.</summary>
        private static GeoSolid3 Slab(IEnumerable<(double X, double Y)> outline, double bottom, double top, IEnumerable<IEnumerable<(double X, double Y)>> holes)
        {
            var boundary = new GeoPolygon3(outline.Select(p => new GeoPoint3(p.X, p.Y, bottom)), Tolerance);
            var face = new GeoFace3(boundary, holes?.Select(h => new GeoPolygon3(h.Select(p => new GeoPoint3(p.X, p.Y, bottom)), Tolerance)), Tolerance);
            return GeoSolid3.Extrude(face, new GeoVector3(0, 0, top - bottom), Tolerance);
        }

        /// <summary>
        /// Holds a volume to what it should be within a ten-thousandth: the corner of the wedge, a hair outside the long side,
        /// takes 270 of its 15.66 million, and the cuts round a hundred more.
        /// </summary>
        private static void AssertNear(double expected, double actual, string what)
            => Assert.True(Math.Abs(actual - expected) <= 1E-4 * Math.Abs(expected), $"{what}: {actual:N1} for {expected:N1}");

        /// <summary>
        /// The plane eight ten-thousandths west of the L's notch edge at its north end and turned 0.424 microradians, so that
        /// it is a ten-thousandth east of the notch corner and three hundredths east of the edge's line at the far side.
        /// </summary>
        private static GeoPlane3 AlongTheNotch() => new GeoPlane3(new GeoPoint3(-0.0008, 73725, 0), new GeoVector3(1, 4.24E-7, 0).Normalize());

        /// <summary>
        /// Where a plane standing square to XY crosses the line y = <paramref name="y"/>.
        /// </summary>
        private static double OnPlane(GeoPlane3 plane, double y)
            => plane.Origin.X - plane.Normal.Y / plane.Normal.X * (y - plane.Origin.Y);

        /// <summary>
        /// The south edge of the slab beside: 0.0119 inside the north face of the other at x = 0, meeting it at x = 2930.
        /// </summary>
        private static double Edge(double x) => 1000 - 0.0119 + 0.0119 * x / 2930;

        private static GeoPoint3 P(double x, double y) => new GeoPoint3(x, y, 0.0);

        private static GeoSolid3 Prism(GeoPoint3[] outline, double bottom, double top) => Prism(outline, bottom, top, Tolerance);

        /// <summary>
        /// A prism standing on an outline wound counter-clockwise in XY, from one height to another.
        /// </summary>
        private static GeoSolid3 Prism(GeoPoint3[] outline, double bottom, double top, Tolerance tolerance)
        {
            GeoPoint3[] low = outline.Select(p => new GeoPoint3(p.X, p.Y, bottom)).ToArray();
            GeoPoint3[] high = outline.Select(p => new GeoPoint3(p.X, p.Y, top)).ToArray();
            var faces = new List<GeoFace3>
            {
                new GeoFace3(new GeoPolygon3(low.Reverse(), tolerance), null, tolerance),
                new GeoFace3(new GeoPolygon3(high, tolerance), null, tolerance),
            };

            for (int i = 0; i < outline.Length; i++)
            {
                int j = (i + 1) % outline.Length;
                faces.Add(new GeoFace3(new GeoPolygon3(new[] { low[i], low[j], high[j], high[i] }, tolerance), null, tolerance));
            }

            return new GeoSolid3(faces);
        }

        /// <summary>
        /// A plate standing on an outline wound counter-clockwise in XY, with square pits through it.
        /// </summary>
        private static GeoSolid3 Plate(GeoPoint3[] outline, GeoPoint3[][] pits, double bottom, double top)
        {
            GeoPoint3[] At(GeoPoint3[] ring, double z) => ring.Select(p => new GeoPoint3(p.X, p.Y, z)).ToArray();

            var faces = new List<GeoFace3>
            {
                new GeoFace3(new GeoPolygon3(At(outline, bottom).Reverse(), Tolerance), pits.Select(pit => new GeoPolygon3(At(pit, bottom).Reverse(), Tolerance)), Tolerance),
                new GeoFace3(new GeoPolygon3(At(outline, top), Tolerance), pits.Select(pit => new GeoPolygon3(At(pit, top), Tolerance)), Tolerance),
            };

            faces.AddRange(Sides(At(outline, bottom), At(outline, top), false));

            foreach (GeoPoint3[] pit in pits)
            {
                faces.AddRange(Sides(At(pit, bottom), At(pit, top), true));
            }

            return new GeoSolid3(faces);
        }

        /// <summary>
        /// The faces standing on each edge of a ring, facing out of it, or into it for the wall of a pit.
        /// </summary>
        private static IEnumerable<GeoFace3> Sides(GeoPoint3[] low, GeoPoint3[] high, bool inward)
        {
            for (int i = 0; i < low.Length; i++)
            {
                int j = (i + 1) % low.Length;
                GeoPoint3[] corners = inward ? new[] { low[j], low[i], high[i], high[j] } : new[] { low[i], low[j], high[j], high[i] };
                yield return new GeoFace3(new GeoPolygon3(corners, Tolerance), null, Tolerance);
            }
        }

        /// <summary>
        /// The volume a surface encloses, measured from a given point: the same from every point when the surface closes and
        /// is wound one way throughout, and different where it does not.
        /// </summary>
        private static double VolumeFrom(GeoSolid3 solid, GeoPoint3 apex)
        {
            double total = 0.0;

            foreach (GeoFace3 face in solid.Faces)
            {
                foreach (GeoTriangle3 triangle in face.Triangulate())
                {
                    total += apex.GetVectorTo(triangle.A).TripleProduct(apex.GetVectorTo(triangle.B), apex.GetVectorTo(triangle.C)) / 6.0;
                }

                foreach (GeoPolygon3 hole in face.Holes)
                {
                    foreach (GeoTriangle3 triangle in hole.Triangulate())
                    {
                        total -= apex.GetVectorTo(triangle.A).TripleProduct(apex.GetVectorTo(triangle.B), apex.GetVectorTo(triangle.C)) / 6.0;
                    }
                }
            }

            return total;
        }
    }
}
