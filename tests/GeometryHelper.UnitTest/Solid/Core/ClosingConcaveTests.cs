using System.Linq;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid.Core
{
    /// <summary>
    /// Closing a solid whose hole stands out of flat with a concave rim: the top of an L or a U left out and lifted at a
    /// corner. Of the ways of filling the rim by triangles on its own corners, those with a triangle turned back against the
    /// rim's normal fold over the outside of the rim and are no ways of filling it, and only the ways inside are weighed
    /// for ambiguity; see <see cref="GeoSolid3.TryClose(out GeoSolid3, SolidClosingOptions, out SolidClosing3)"/>.
    /// </summary>
    /// <remarks>
    /// With one corner of a flat rim lifted by h, a way of filling it holds h / 3 times the area, seen from above, of its
    /// triangles at that corner more than the flat top would, and two ways differ by h / 3 times the difference of those
    /// areas. They are taken as one within the bound, the rim's area times the planar tolerance: 0.3 for the L of 300, 0.5
    /// for the U of 500, 0.4 for a square of 400. The prisms stand 10 high, the L holding 3 000 flat, the U 5 000.
    /// </remarks>
    public class ClosingConcaveTests
    {
        private static readonly Tolerance Fine = ClosingTestBodies.Fine;

        [Fact]
        public void AnLShapedTopLiftedAtAConvexCorner_IsFilledByTriangles_WhereOnlyAnUnambiguousFillIsTaken()
        {
            // The top of the L left out and its corner at (20, 0) lifted 0.005: its rim stands 0.0017 off flat. Of the 14
            // ways of filling it, 9 lay a triangle turned back over the notch, and with them the volumes differ by 0.42,
            // more than the bound of 0.3. The 5 ways inside differ by 0.25 at most, within it. The least area is one of
            // those: the corner's triangles 50 and 100 seen from above, so the body holds 3 000 and 0.005 / 3 of 150.
            GeoSolid3 body = ClosingTestBodies.PrismWithoutItsTopLifted(ClosingTestBodies.LShapedPlan(), 10, 1, 0.005);

            SolidClosing3 report = AssertFilled(body, OffFlat(0.01), out GeoSolid3 closed);
            AssertOneFillOfTriangles(body, report, closed, 4, 300.0);
            Assert.InRange(closed.GetVolume(Fine), 3000.0 + (0.005 / 3.0 * 100.0) - 1E-6, 3000.0 + (0.005 / 3.0 * 250.0) + 1E-6);
            AssertVolume(3000.0 + (0.005 / 3.0 * 150.0), closed);
        }

        [Fact]
        public void AnLShapedTopLiftedAtAConvexCorner_IsFilledTheSameWay_WhereTheLeastAreaIsTaken()
        {
            // The least area is the same way whatever the others hold: the same four triangles, the same volume.
            GeoSolid3 body = ClosingTestBodies.PrismWithoutItsTopLifted(ClosingTestBodies.LShapedPlan(), 10, 1, 0.005);

            SolidClosing3 report = AssertFilled(body, OffFlat(0.01, FillStrategy.MinArea), out GeoSolid3 closed);
            AssertOneFillOfTriangles(body, report, closed, 4, 300.0);
            AssertVolume(3000.0 + (0.005 / 3.0 * 150.0), closed);
        }

        [Fact]
        public void ASquareTopLiftedAtACorner_IsFilledByTwoTriangles()
        {
            // A guard: a square 20 by 20 has no notch to fold over, and its two ways differ by 0.005 / 3 of 200, 0.33, within
            // the bound of 0.4. The least area lays the diagonal away from the lifted corner.
            GeoSolid3 body = ClosingTestBodies.PrismWithoutItsTopLifted(ClosingTestBodies.Rectangle(0, 0, 20, 20), 10, 2, 0.005);

            SolidClosing3 report = AssertFilled(body, OffFlat(0.01), out GeoSolid3 closed);
            AssertOneFillOfTriangles(body, report, closed, 2, 400.0);
            AssertVolume(4000.0 + (0.005 / 3.0 * 200.0), closed);
        }

        [Fact]
        public void AUShapedTopLiftedAtTheEndOfAnArm_IsFilledByTriangles_WhereOnlyAnUnambiguousFillIsTaken()
        {
            // The top of the U left out and the corner at the end of its arm, (30, 20), lifted 0.005. Of the 114 ways that
            // lay no needle, 106 fold back over the notch, and with them the volumes differ by 0.83, more than the bound of
            // 0.5; the 8 ways inside differ by 0.083. The least area takes the corner's triangles 50 and 100 seen from above.
            GeoSolid3 body = ClosingTestBodies.PrismWithoutItsTopLifted(ClosingTestBodies.UShapedPlan(), 10, 2, 0.005);

            SolidClosing3 report = AssertFilled(body, OffFlat(0.01), out GeoSolid3 closed);
            AssertOneFillOfTriangles(body, report, closed, 6, 500.0);
            Assert.InRange(closed.GetVolume(Fine), 5000.0 + (0.005 / 3.0 * 100.0) - 1E-6, 5000.0 + (0.005 / 3.0 * 150.0) + 1E-6);
            AssertVolume(5000.0 + (0.005 / 3.0 * 150.0), closed);
        }

        [Fact]
        public void AnLShapedTopLiftedFurther_IsAHoleAmbiguous_EvenAmongTheWaysInside()
        {
            // The L's corner at (20, 0) lifted 0.05: the 5 ways inside differ by 2.5 themselves, far more than the bound of
            // 0.3, and which the body holds cannot be told.
            GeoSolid3 body = ClosingTestBodies.PrismWithoutItsTopLifted(ClosingTestBodies.LShapedPlan(), 10, 1, 0.05);

            GeoPoint3 at = AssertRefused(body, OffFlat(0.1), ClosingFailure.HoleAmbiguous);
            Assert.InRange(at.Z, 10.0 - 1E-6, 10.05 + 1E-6);
            Assert.InRange(at.X, -1E-6, 20.0 + 1E-6);
            Assert.InRange(at.Y, -1E-6, 20.0 + 1E-6);
        }

        // Welds within five thousandths, fills a hole of any size out of flat by no more than given, as the strategy says.
        private static SolidClosingOptions OffFlat(double maxOffFlat, FillStrategy fill = FillStrategy.WhenUnambiguous)
            => new SolidClosingOptions(Fine, 0.005, double.PositiveInfinity, maxOffFlat, fill);

        // One Fill of a little more than the area given, made of as many triangles as given on the corners of the rim.
        private static void AssertOneFillOfTriangles(GeoSolid3 body, SolidClosing3 report, GeoSolid3 closed, int triangles, double area)
        {
            SolidRepair3 fill = Assert.Single(report.Repairs);
            Assert.Equal(SolidRepairKind.Fill, fill.Kind);
            Assert.InRange(fill.Size, area - 1E-6, area + 1E-3);
            Assert.InRange(report.AddedArea, area - 1E-6, area + 1E-3);
            GeoFace3[] added = closed.Faces.Where(face => !body.Faces.Contains(face)).ToArray();
            Assert.Equal(triangles, added.Length);
            Assert.All(added, face => Assert.Equal(3, face.Boundary.VertexCount));
            Assert.Equal(body.Faces.Count + triangles, closed.Faces.Count);
        }

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

        // Within a millionth of what it should hold.
        private static void AssertVolume(double expected, GeoSolid3 closed)
            => Assert.InRange(closed.GetVolume(Fine), expected * (1.0 - 1E-6), expected * (1.0 + 1E-6));
    }
}
