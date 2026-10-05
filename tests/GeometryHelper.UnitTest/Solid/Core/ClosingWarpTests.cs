using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid.Core
{
    /// <summary>
    /// Closing a solid whose hole stands out of flat: a loop further off flat than the planar tolerance, and no further
    /// than the options allow, filled by the triangles of least area across it on its own corners, where every way of
    /// filling it holds the same volume within the planar tolerance times its area, or as the strategy says where they do
    /// not; see <see cref="GeoSolid3.TryClose(out GeoSolid3, SolidClosingOptions, out SolidClosing3)"/>.
    /// </summary>
    /// <remarks>
    /// A loop of four corners with one lifted by h is filled one way across it or the other, and the two differ by a sixth
    /// of its area times h: within the planar tolerance times its area, a thousandth, they are taken as one, so a quad lifted
    /// by up to six tolerances is filled. A box 100 by 100 by 50 with its top left out and a corner lifted 0.005 holds
    /// between 500 008.3 and 500 016.7 filled, the two ways 8.3 apart within the bound of 10.
    /// </remarks>
    public class ClosingWarpTests
    {
        private static readonly Tolerance Fine = ClosingTestBodies.Fine;

        [Fact]
        public void ATopLiftedAtACornerByFiveThousandths_IsFilledByTwoTriangles()
        {
            // The rim of the top, 100 by 100, stands 0.00125 off the plane through its middle, more than the planar
            // tolerance: no one face, but two triangles across it, either way within the bound. Lifted 0.004 it would stand
            // off by the tolerance itself, and which step took it would be the rounding's.
            GeoSolid3 body = ClosingTestBodies.BoxWithoutItsTopLiftedAtACorner(100, 50, 0.005);

            SolidClosing3 report = AssertFilled(body, OffFlat(0.01), out GeoSolid3 closed);
            SolidRepair3 fill = Assert.Single(report.Repairs);
            Assert.Equal(SolidRepairKind.Fill, fill.Kind);
            Assert.InRange(fill.Size, 10000.0 - 1E-3, 10000.0 + 1E-3);
            Assert.InRange(report.AddedArea, 10000.0 - 1E-3, 10000.0 + 1E-3);
            GeoFace3[] added = closed.Faces.Where(face => !body.Faces.Contains(face)).ToArray();
            Assert.Equal(2, added.Length);
            Assert.All(added, face => Assert.Equal(3, face.Boundary.VertexCount));
            Assert.InRange(closed.GetVolume(Fine), 500000.0 + 10000.0 * 0.005 / 6.0 - 1E-6, 500000.0 + 10000.0 * 0.005 / 3.0 + 1E-6);
        }

        [Fact]
        public void ATopLiftedAtACornerByFiveHundredths_IsAHoleAmbiguous_WhereOnlyAnUnambiguousFillIsTaken()
        {
            // Lifted 0.05, the two ways across differ by 83.3, more than the bound of 10: which the body holds cannot be told.
            GeoPoint3 at = AssertRefused(ClosingTestBodies.BoxWithoutItsTopLiftedAtACorner(100, 50, 0.05), OffFlat(0.1), ClosingFailure.HoleAmbiguous);
            AssertOnTheTop(at, 100.0, 50.0, 0.05);
        }

        [Fact]
        public void ATopLiftedAtACornerByFiveHundredths_IsFilledByTheLeastArea_WhereThatIsTaken()
        {
            // The two ways across are as large to a ten-billionth, and whichever is taken, the body holds between 500 041.7
            // and 500 083.3.
            GeoSolid3 body = ClosingTestBodies.BoxWithoutItsTopLiftedAtACorner(100, 50, 0.05);

            SolidClosing3 report = AssertFilled(body, OffFlat(0.1, FillStrategy.MinArea), out GeoSolid3 closed);
            Assert.Equal(SolidRepairKind.Fill, Assert.Single(report.Repairs).Kind);
            GeoFace3[] added = closed.Faces.Where(face => !body.Faces.Contains(face)).ToArray();
            Assert.Equal(2, added.Length);
            Assert.All(added, face => Assert.Equal(3, face.Boundary.VertexCount));
            Assert.InRange(closed.GetVolume(Fine), 500000.0 + 10000.0 * 0.05 / 6.0 - 1E-6, 500000.0 + 10000.0 * 0.05 / 3.0 + 1E-6);
        }

        [Fact]
        public void AHoleFurtherOutOfFlatThanAllowed_IsAHoleOffFlat()
        {
            // Lifted 0.05, the rim stands 0.0125 off the plane through its middle, more than the hundredth allowed.
            GeoPoint3 at = AssertRefused(ClosingTestBodies.BoxWithoutItsTopLiftedAtACorner(100, 50, 0.05), OffFlat(0.01), ClosingFailure.HoleOffFlat);
            AssertOnTheTop(at, 100.0, 50.0, 0.05);
        }

        [Fact]
        public void ALongBoxMissingTwoFacesBesideEachOther_IsAHoleAmbiguous_WhereOnlyAnUnambiguousFillIsTaken()
        {
            // A box 10 by 1 by 1 with its top and front left out: one loop of six corners round both, 0.47 off the plane
            // through its middle. Of the ways across it lying on no face of the body, the two faces again hold 10, and two
            // others cutting across the box's corner, 20.05 of area, hold 8.33: 1.67 apart, past the bound of 0.014.
            GeoPoint3 at = AssertRefused(LongBoxMissingTopAndFront(10.0), OffFlat(1.0), ClosingFailure.HoleAmbiguous);
            Assert.InRange(at.X, -1E-6, 10.0 + 1E-6);
            Assert.InRange(at.Y, -1E-6, 1.0 + 1E-6);
            Assert.InRange(at.Z, -1E-6, 1.0 + 1E-6);
        }

        [Fact]
        public void ALongBoxMissingTwoFacesBesideEachOther_IsFilledByTheLeastAreaLyingOnNoFace()
        {
            // Across the loop the least area, 15.14, is the diagonal plane with a triangle at each end; but each end triangle
            // lies on the end face it stands in, back to back with it, and is no way to take. Of the ways lying on no face of
            // the body the least is the two faces again, 20, the others 20.05: the box whole, 10.
            SolidClosing3 report = AssertFilled(LongBoxMissingTopAndFront(10.0), OffFlat(1.0, FillStrategy.MinArea), out GeoSolid3 closed);
            SolidRepair3 fill = Assert.Single(report.Repairs);
            Assert.Equal(SolidRepairKind.Fill, fill.Kind);
            Assert.Equal(20.0, fill.Size, 9);
            Assert.Equal(20.0, report.AddedArea, 9);
            AssertVolume(10.0, closed);
        }

        [Fact]
        public void AShortBoxMissingTwoFacesBesideEachOther_IsFilledByTheTwoFacesAgain()
        {
            // A box 1 by 1 by 1: across the diagonal plane takes 2.41, the two faces again 2, and they are the least.
            SolidClosing3 report = AssertFilled(LongBoxMissingTopAndFront(1.0), OffFlat(1.0, FillStrategy.MinArea), out GeoSolid3 closed);
            SolidRepair3 fill = Assert.Single(report.Repairs);
            Assert.Equal(SolidRepairKind.Fill, fill.Kind);
            Assert.Equal(2.0, fill.Size, 9);
            Assert.Equal(2.0, report.AddedArea, 9);
            AssertVolume(1.0, closed);
        }

        [Fact]
        public void AHoleOf200CornersOutOfFlat_IsFilledByTriangles()
        {
            // A prism of 200 sides on a circle of radius 10, its top left out and the rim's corner on the x axis lifted
            // 0.0015: the ways across differ by no more than a third of the lift times the area, half the bound. Filled by
            // 198 triangles on its own corners.
            GeoSolid3 body = ClosingTestBodies.PrismWithoutItsTopLiftedAtACorner(200, 10, 10, 0.0015);
            double area = 0.5 * 200 * 100 * Math.Sin(2.0 * Math.PI / 200);

            SolidClosing3 report = AssertFilled(body, OffFlat(0.01), out GeoSolid3 closed);
            Assert.Equal(SolidRepairKind.Fill, Assert.Single(report.Repairs).Kind);
            GeoFace3[] added = closed.Faces.Where(face => !body.Faces.Contains(face)).ToArray();
            Assert.Equal(198, added.Length);
            Assert.All(added, face => Assert.Equal(3, face.Boundary.VertexCount));
            Assert.InRange(closed.GetVolume(Fine), area * 10.0 - 1E-6, area * 10.0 + area * 0.0015 / 3.0 + 1E-6);
        }

        [Fact]
        public void AHoleOfMoreCornersThanTheFillTakes_IsAHoleTooLarge()
        {
            // The same with 300 sides: the least area is found across loops of no more than 256 corners, and a loop out of
            // flat with more is too large to fill. The trouble is on its rim.
            GeoPoint3 at = AssertRefused(ClosingTestBodies.PrismWithoutItsTopLiftedAtACorner(300, 10, 10, 0.0015), OffFlat(0.01), ClosingFailure.HoleTooLarge);
            Assert.InRange(at.Z, 10.0 - 1E-6, 10.0015 + 1E-6);
            Assert.InRange(Math.Sqrt(at.X * at.X + at.Y * at.Y), 10.0 - 0.01, 10.0 + 1E-6);
        }

        [Theory]
        [InlineData(FillStrategy.WhenUnambiguous)]
        [InlineData(FillStrategy.MinArea)]
        public void AHoleOutOfFlatWithAHoleInIt_IsAHoleAmbiguous_WhateverTheStrategy(FillStrategy fill)
        {
            // The plate's top left out, its corner over (30, 30) lifted 0.005 and the hole's corner over (20, 20) as much: two
            // loops out of flat, the one inside the other. Triangles are found across a loop with nothing inside it, and the
            // least area is taken of those only.
            GeoPoint3 at = AssertRefused(ClosingTestBodies.PlateWithAHoleWithoutItsTopLifted(0.005), OffFlat(0.01, fill), ClosingFailure.HoleAmbiguous);
            AssertOnTheTop(at, 30.0, 10.0, 0.005);
        }

        // A box as long as given by 1 by 1, its top and its front left out.
        private static GeoSolid3 LongBoxMissingTopAndFront(double length)
            => ClosingTestBodies.WithoutFaces(new GeoSolid3(ClosingTestBodies.BoxFaces(ClosingTestBodies.Corners(0, 0, 0, length, 1, 1))), ClosingTestBodies.Top, ClosingTestBodies.Front);

        // Welds within five thousandths, fills a hole of any size out of flat by no more than given, as the strategy says.
        private static SolidClosingOptions OffFlat(double maxOffFlat, FillStrategy fill = FillStrategy.WhenUnambiguous)
            => new SolidClosingOptions(Fine, 0.005, double.PositiveInfinity, maxOffFlat, fill);

        // Filled: another body, valid, with no needle and no skin of no thickness, nothing gone wrong.
        private static SolidClosing3 AssertFilled(GeoSolid3 body, SolidClosingOptions options, out GeoSolid3 closed)
        {
            Assert.False(body.Validate(Fine).IsClosed);
            Assert.True(body.TryClose(out closed, options, out SolidClosing3 report), report.ToString());
            Assert.NotSame(body, closed);
            SolidValidation3 check = closed.Validate(Fine);
            Assert.True(check.IsValid, check.ToString());
            Assert.Equal(ClosingFailure.None, report.Failure);
            Assert.Null(report.FailureLocation);
            Assert.Equal(0, ClosingTestBodies.Needles(closed));
            Assert.Equal(0, ClosingTestBodies.BackToBack(closed));
            return report;
        }

        // Not closed: nothing back and nothing done, with why and where.
        private static GeoPoint3 AssertRefused(GeoSolid3 body, SolidClosingOptions options, ClosingFailure failure)
        {
            Assert.False(body.TryClose(out GeoSolid3 closed, options, out SolidClosing3 report));
            Assert.Equal(failure, report.Failure);
            Assert.Null(closed);
            Assert.Empty(report.Repairs);
            Assert.Equal(0.0, report.AddedArea);
            Assert.Equal(0.0, report.VolumeChange);
            Assert.True(report.FailureLocation.HasValue);
            return report.FailureLocation.Value;
        }

        // A point on the top of a box as wide as given each way, between its height and that and the lift.
        private static void AssertOnTheTop(GeoPoint3 at, double side, double height, double lift)
        {
            Assert.InRange(at.Z, height - 1E-6, height + lift + 1E-6);
            Assert.InRange(at.X, -1E-6, side + 1E-6);
            Assert.InRange(at.Y, -1E-6, side + 1E-6);
        }

        // Within a millionth of what it should hold.
        private static void AssertVolume(double expected, GeoSolid3 closed)
            => Assert.InRange(closed.GetVolume(Fine), expected * (1.0 - 1E-6), expected * (1.0 + 1E-6));
    }
}
