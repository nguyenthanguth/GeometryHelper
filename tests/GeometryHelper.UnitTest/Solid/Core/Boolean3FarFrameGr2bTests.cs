using System;
using System.Linq;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using GeometryHelper.Meshing;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// The cut of a body's openings, its volume, area and centroid, a section of it and its cells, a hundred million and a
    /// thousand million out, are worked out as near the origin. There a trillionth of the coordinates, which the cut reads a
    /// corner on a plane within, came to a ten- or a hundred-thousandth and a thousandth, and walls of a body and its
    /// openings 0.0009 apart were cut in one plane. Each answer is held to the rounding of the coordinates, 2^-52 of them,
    /// times the area of the body, the most moving its corners can cost.
    /// </summary>
    public class Boolean3FarFrameGr2bTests
    {
        private static readonly Tolerance Thousandth = Tolerance.Default;

        private static readonly Tolerance Hundredth = new Tolerance(0.01, 1E-5, 0.017453292519943295, 0.01);

        private static GeoPoint3 At(double far, double x, double y, double z) => new GeoPoint3(far + x, far + y, far + z);

        private static GeoSolid3 Box(double far, double x0, double y0, double z0, double x1, double y1, double z1)
            => GeoSolid3.Extrude(
                new GeoPolygon3(At(far, x0, y0, z0), At(far, x1, y0, z0), At(far, x1, y1, z0), At(far, x0, y1, z0)), new GeoVector3(0, 0, z1 - z0), Thousandth);

        /// <summary>An L 100 by 80, its legs 20 wide, 50 high, its corner at (far, far, far).</summary>
        private static GeoSolid3 Body(double far)
            => GeoSolid3.Extrude(
                new GeoPolygon3(At(far, 0, 0, 0), At(far, 100, 0, 0), At(far, 100, 20, 0), At(far, 20, 20, 0), At(far, 20, 80, 0), At(far, 0, 80, 0)),
                new GeoVector3(0, 0, 50),
                Thousandth);

        /// <summary>
        /// The L with a blind opening 13 by 8.8271 up from its floor 22 deep, and one 15.5 by 6.528 down from its top 37 deep;
        /// the far walls of the two stand 0.0009 apart, the openings 27 apart along them.
        /// </summary>
        private static GeoSolid3 L(double far, bool turnedRound)
        {
            GeoSolid3 up = Box(far, 35, 4.6, -7, 48, 13.4271, 22);
            GeoSolid3 down = Box(far, 75, 6.9, 13, 90.5, 13.428, 57);

            return Body(far).WithOpenings(turnedRound ? new[] { down, up } : new[] { up, down });
        }

        /// <summary>The L less what the openings take of it.</summary>
        private const double Held = 160_000 - 13 * 8.8271 * 22 - 15.5 * 6.528 * 37;

        /// <summary>The L's faces, 24 400, and the walls of its openings: each opening's floor or ceiling takes the place of the hole it leaves.</summary>
        private const double Surface = 24_400 + 2 * (13 + 8.8271) * 22 + 2 * (15.5 + 6.528) * 37;

        public static TheoryData<double, bool, double> Cases()
        {
            var cases = new TheoryData<double, bool, double>();

            foreach (double far in new[] { 1E8, 1E9 })
            {
                foreach (bool turnedRound in new[] { false, true })
                {
                    cases.Add(far, turnedRound, 1E-3);
                    cases.Add(far, turnedRound, 1E-2);
                }
            }

            return cases;
        }

        private static Tolerance Within(double tolerance) => tolerance == 1E-3 ? Thousandth : Hundredth;

        /// <summary>What rounding the corners of the L far out can cost a volume: 2^-52 of far times its area and its openings'.</summary>
        private static double Rounding(double far) => far * Math.Pow(2, -52) * 2 * Surface;

        [Theory]
        [MemberData(nameof(Cases))]
        public void TheVolumeOfTheL_FarOut_KeepsEachWallInItsOwnPlane(double far, bool turnedRound, double tolerance)
        {
            GeoSolid3 l = L(far, turnedRound);

            Assert.Equal(Held, l.GetVolume(Within(tolerance)), Rounding(far));
            Assert.True(l.TryGetVolume(out double trusted, Within(tolerance)));
            Assert.Equal(Held, trusted, Rounding(far));
        }

        [Theory]
        [MemberData(nameof(Cases))]
        public void TheAreaOfTheL_FarOut_IsWhatItIsNearTheOrigin(double far, bool turnedRound, double tolerance)
        {
            // Its openings given down first, the L cut at the origin keeps faces 0.0009 wide between the planes of the two far
            // walls, under the opening down from the top, and measures 0.0234 over Surface; far out it is held to what it
            // measures there.
            double near = L(0, turnedRound).GetSurfaceArea(Within(tolerance));

            Assert.Equal(near, L(far, turnedRound).GetSurfaceArea(Within(tolerance)), Rounding(far));
            Assert.Equal(Surface, near, turnedRound ? 0.03 : 1E-9);
        }

        [Theory]
        [MemberData(nameof(Cases))]
        public void TheCentroidOfTheL_FarOut_IsWhereItIsNearTheOrigin(double far, bool turnedRound, double tolerance)
        {
            // The L's centre, less what each opening takes from it, worked out in the L's own frame.
            double x = (160_000 * 35.0 - 13 * 8.8271 * 22 * 41.5 - 15.5 * 6.528 * 37 * 82.75) / Held;
            double y = (160_000 * 25.0 - 13 * 8.8271 * 22 * (4.6 + 13.4271) / 2 - 15.5 * 6.528 * 37 * (6.9 + 13.428) / 2) / Held;
            double z = (160_000 * 25.0 - 13 * 8.8271 * 22 * 11 - 15.5 * 6.528 * 37 * 31.5) / Held;
            GeoPoint3 centroid = L(far, turnedRound).GetCentroid(Within(tolerance));

            Assert.Equal(x, centroid.X - far, 250 * Rounding(far) / Held + 2 * far * Math.Pow(2, -52));
            Assert.Equal(y, centroid.Y - far, 250 * Rounding(far) / Held + 2 * far * Math.Pow(2, -52));
            Assert.Equal(z, centroid.Z - far, 250 * Rounding(far) / Held + 2 * far * Math.Pow(2, -52));
        }

        [Theory]
        [MemberData(nameof(Cases))]
        public void TheOpeningsOfTheL_CutFarOut_KeepEachWallInItsOwnPlane(double far, bool turnedRound, double tolerance)
        {
            Assert.True(L(far, turnedRound).TryCutOpenings(out GeoSolid3 material, Within(tolerance)));

            Assert.Empty(material.Openings);
            Assert.Equal(Held, material.GetVolume(Within(tolerance)), Rounding(far));
            Assert.True(material.Validate(Within(tolerance)).IsValid);
        }

        [Theory]
        [InlineData(1E8)]
        [InlineData(1E9)]
        public void TheCornersOfTheLCutFarOut_AreTheCornersGiven(double far)
        {
            // Moved near the origin and back coordinate by coordinate, a corner of the L or of an opening that the cut keeps
            // comes back as it was given, to the bit.
            GeoSolid3 l = L(far, false);
            GeoPoint3[] given = l.Faces.Concat(l.Openings.SelectMany(o => o.Faces)).SelectMany(f => f.Boundary.Vertices).ToArray();

            Assert.True(l.TryCutOpenings(out GeoSolid3 material, Thousandth));

            int near = 0;

            foreach (GeoPoint3 corner in material.Faces.SelectMany(f => f.Boundary.Vertices.Concat(f.Holes.SelectMany(h => h.Vertices))))
            {
                GeoPoint3[] nearest = given.Where(g => g.DistanceTo(corner) < 1E-6).ToArray();

                if (nearest.Length > 0)
                {
                    near++;
                    Assert.Contains(nearest, g => g.Equals(corner));
                }
            }

            Assert.True(near >= 12);
        }

        /// <summary>What rounding the corners of the L far out can cost a section of it: 2^-52 of far times its rim.</summary>
        private static double SectionRounding(double far) => far * Math.Pow(2, -52) * 500;

        [Theory]
        [MemberData(nameof(Cases))]
        public void ASectionOfTheLAcrossTheOpeningUp_FarOut_KeepsItsFarWallInItsOwnPlane(double far, bool turnedRound, double tolerance)
        {
            // Across the foot at 41.5 along, through the opening up from the floor: the foot, 20 by 50, less that opening,
            // 8.8271 by 22.
            var plane = new GeoPlane3(At(far, 41.5, 10, 25), GeoVector3.XAxis);

            GeoFace3[] section = L(far, turnedRound).Section(plane, Within(tolerance));

            Assert.Equal(20 * 50 - 8.8271 * 22, section.Sum(f => f.Area), SectionRounding(far));
            Assert.All(section, f => Assert.All(f.Boundary.Vertices, p => Assert.True(Math.Abs(plane.SignedDistanceTo(p)) <= 1E-6)));
        }

        [Theory]
        [MemberData(nameof(Cases))]
        public void ASectionOfTheLAcrossBothOpenings_FarOut_KeepsEachWallInItsOwnPlane(double far, bool turnedRound, double tolerance)
        {
            // Across the L at 15 up, through both openings: the L, 3 200, less the two holes.
            var plane = new GeoPlane3(At(far, 50, 40, 15), GeoVector3.ZAxis);

            GeoFace3[] section = L(far, turnedRound).Section(plane, Within(tolerance));

            Assert.Equal(3_200 - 13 * 8.8271 - 15.5 * 6.528, section.Sum(f => f.Area), SectionRounding(far));
        }

        [Theory]
        [MemberData(nameof(Cases))]
        public void TheCellsOfTheL_FarOut_HoldWhatTheyHoldNearTheOrigin(double far, bool turnedRound, double tolerance)
        {
            // Cells 50 by 40 by 20 along the world's axes, from the L's first corner: each holds what the same cell of the L
            // at the origin holds, and stands where it stands, moved by far.
            CellOptions3 options = CellOptions3.Grid(50, 40, 20);
            GeoCellGrid3 cells = Mesh3.ToCells(L(far, turnedRound), options, MeshPlacement3.World, Within(tolerance));
            GeoCellGrid3 near = Mesh3.ToCells(L(0, turnedRound), options, MeshPlacement3.World, Within(tolerance));

            Assert.Equal(Held, cells.Volume, Rounding(far));
            Assert.Equal(near.CellCount, cells.CellCount);

            for (int i = 0; i < near.CellCount; i++)
            {
                GeoCell3 cell = cells.Cells[i];
                GeoCell3 there = near.Cells[i];

                Assert.Equal((there.I, there.J, there.K, there.Piece), (cell.I, cell.J, cell.K, cell.Piece));
                Assert.Equal(there.Volume, cell.Volume, Rounding(far));
                Assert.True(At(far, there.Box.Center.X, there.Box.Center.Y, there.Box.Center.Z).DistanceTo(cell.Box.Center) <= 4 * far * Math.Pow(2, -52));
            }
        }

        /// <summary>
        /// A plate 200 by 100 by 3 with two openings 50 by 70 up from its floor, 15 apart, one 1.5 deep and the other
        /// 1.50015: their ceilings stand 0.00015 apart, less than a trillionth of the coordinates a hundred million out.
        /// </summary>
        private static GeoSolid3 Plate(double far)
            => GeoSolid3.Extrude(new GeoPolygon3(At(far, 0, 0, 0), At(far, 200, 0, 0), At(far, 200, 100, 0), At(far, 0, 100, 0)), new GeoVector3(0, 0, 3), Thousandth)
                .WithOpenings(new[] { Box(far, 10, 15, -7, 60, 85, 1.5), Box(far, 75, 15, -7, 125, 85, 1.50015) });

        private const double PlateHeld = 60_000 - 50 * 70 * 1.5 - 50 * 70 * 1.50015;

        /// <summary>What rounding the corners of the plate far out can cost a volume: 2^-52 of far times its area and its openings'.</summary>
        private static double PlateRounding(double far) => far * Math.Pow(2, -52) * 2 * 42_520;

        [Theory]
        [InlineData(1E8)]
        [InlineData(1E9)]
        public void ThePlateWithCeilingsApart_FarOut_HoldsWhatItHoldsNearTheOrigin(double far)
        {
            // The two ceilings were cut in one plane: the plate held 0.525 too much, the second opening 0.00015 too shallow,
            // and its area lost the walls of the step.
            GeoSolid3 plate = Plate(far);

            Assert.Equal(PlateHeld, plate.GetVolume(Thousandth), PlateRounding(far));
            Assert.Equal(Plate(0).GetSurfaceArea(Thousandth), plate.GetSurfaceArea(Thousandth), PlateRounding(far));
            Assert.Equal(Plate(0).GetCentroid(Thousandth).Z, plate.GetCentroid(Thousandth).Z - far, 300 * PlateRounding(far) / PlateHeld + 2 * far * Math.Pow(2, -52));
            Assert.Equal(PlateHeld, Mesh3.ToCells(plate, CellOptions3.Grid(50, 40, 20), MeshPlacement3.World, Thousandth).Volume, PlateRounding(far));

            // Read as the triangles lying in its faces, the plate cut at the origin holds 0.33 too much, where read as fans it
            // holds what it holds; far out it is held to what it holds there.
            MassProperties3 mass = plate.GetMassProperties(7.85E-6, Thousandth);
            MassProperties3 near = Plate(0).GetMassProperties(7.85E-6, Thousandth);
            Assert.Equal(near.Volume, mass.Volume, PlateRounding(far));
            Assert.Equal(near.Centroid.Z, mass.Centroid.Z - far, 300 * PlateRounding(far) / PlateHeld + 2 * far * Math.Pow(2, -52));
        }

        [Theory]
        [InlineData(1E8)]
        [InlineData(1E9)]
        public void ASectionOfThePlateAcrossTheDeeperOpening_FarOut_KeepsItsCeilingInItsOwnPlane(double far)
        {
            // Across the plate at 100 along: 100 by 3 less the opening, 70 by 1.50015. It cut 195, 0.0105 too much, the
            // opening's ceiling cut in the plane of the other's.
            var plane = new GeoPlane3(At(far, 100, 50, 1), GeoVector3.XAxis);

            Assert.Equal(300 - 70 * 1.50015, Plate(far).Section(plane, Thousandth).Sum(f => f.Area), far * Math.Pow(2, -52) * 500);
        }
    }
}
