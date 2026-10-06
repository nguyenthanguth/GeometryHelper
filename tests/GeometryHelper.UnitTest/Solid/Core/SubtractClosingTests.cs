using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid.Core
{
    /// <summary>
    /// Taking bodies out of a solid one after another, a cut that would be skipped closed by
    /// <see cref="GeoSolid3.TryClose(out GeoSolid3, SolidClosingOptions, out SolidClosing3)"/>: the result within the
    /// tolerance first, then the one within the fallback, each taken only where it closes, is valid within the booleans'
    /// tolerance, and holds no more than the body before it and no less than that less the tool; every cut taken today is
    /// taken as it is; see
    /// <see cref="GeoSolid3.TrySubtractAll(System.Collections.Generic.IEnumerable{GeoSolid3}, out GeoSolid3, SolidBooleanOptions, SolidClosingOptions, out SubtractReport)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The body is the box of <see cref="ClosingTestBodies"/>, 30 by 20 by 10, 6 000, its front cracked through where told;
    /// the post of <see cref="ClosingTestBodies.PostThrough"/> takes 240 out of it, well clear of the crack. A cut keeps the
    /// crack in what it leaves: open, so the cut is skipped today, within a thousandth, and within a hundredth too where the
    /// crack is wider than the hundredth's cut closes.
    /// </para>
    /// <para>
    /// Measured: within a thousandth the cut leaves a crack 0.005 wide open, and within a hundredth closes it; a crack 0.05
    /// wide it leaves open within both.
    /// </para>
    /// </remarks>
    public class SubtractClosingTests
    {
        private static readonly Tolerance Fine = Tolerance.Default;

        private static readonly Tolerance Hundredth = new Tolerance(0.01, 0.01, Tolerance.DefaultEqualAngleRad, 0.01);

        // Parts drawn against each other cut as touching within a hundredth, and no fallback.
        private static readonly SolidBooleanOptions Plain = new SolidBooleanOptions(Fine, 0.01);

        // The same, and a cut not valid within a thousandth worked out again within a hundredth.
        private static readonly SolidBooleanOptions WithFallback = new SolidBooleanOptions(Fine, 0.01, Hundredth);

        // Welds within five thousandths, and fills nothing.
        private static readonly SolidClosingOptions Welding = new SolidClosingOptions(Fine, 0.005);

        [Fact]
        public void ACutLeftOpenByACrackAHairWide_IsClosedByWelds_WhereTodayItIsSkipped()
        {
            // The crack, 0.005 wide, is kept open by the cut within a thousandth: skipped today, and with closing welded shut,
            // two welds, the box less the post.
            GeoSolid3 body = ClosingTestBodies.BoxCrackedThroughTheFront(0.005);
            GeoSolid3[] tools = { ClosingTestBodies.PostThrough() };

            Assert.True(body.TrySubtractAll(tools, out GeoSolid3 today, Plain, out SubtractReport before));
            Assert.Equal(new[] { 0 }, before.Skipped);
            Assert.Same(body, today);

            Assert.True(body.TrySubtractAll(tools, out GeoSolid3 left, Plain, Welding, out SubtractReport report));
            Assert.Equal(1, report.Tools);
            Assert.Equal(1, report.Taken);
            Assert.Empty(report.Skipped);
            Assert.Empty(report.WithinFallback);
            Assert.Equal(new[] { 0 }, report.Closed);
            SolidClosing3 closing = Assert.Single(report.Closings);
            Assert.Equal(2, closing.Repairs.Count);
            Assert.All(closing.Repairs, repair => Assert.Equal(SolidRepairKind.Weld, repair.Kind));
            Assert.True(left.Validate(Fine).IsValid, left.Validate(Fine).ToString());
            AssertVolume(5760.0, left);
        }

        [Fact]
        public void ACutLeftOpenWiderThanTheWeldsReach_IsStillSkipped_TheBodyKeptAsItWas()
        {
            // A crack 0.05 wide, open within the tolerance and within the fallback, is ten times too wide for the welds: the
            // cut is skipped as today, and the body is the one given.
            GeoSolid3 body = ClosingTestBodies.BoxCrackedThroughTheFront(0.05);
            GeoSolid3[] tools = { ClosingTestBodies.PostThrough() };

            foreach (SolidBooleanOptions options in new[] { Plain, WithFallback })
            {
                Assert.True(body.TrySubtractAll(tools, out GeoSolid3 left, options, Welding, out SubtractReport report));
                Assert.Equal(new[] { 0 }, report.Skipped);
                Assert.Equal(0, report.Taken);
                Assert.Empty(report.Closed);
                Assert.Empty(report.Closings);
                Assert.Same(body, left);
            }
        }

        [Fact]
        public void ACutTakenToday_IsTakenAsToday_NothingClosed()
        {
            // The box whole, and the post: valid within the tolerance, the cut is the one the overload without closing takes.
            GeoSolid3 body = ClosingTestBodies.Box();
            GeoSolid3[] tools = { ClosingTestBodies.PostThrough(), ClosingTestBodies.NotchInTheTop() };

            Assert.True(body.TrySubtractAll(tools, out GeoSolid3 today, Plain, out SubtractReport before));
            Assert.True(body.TrySubtractAll(tools, out GeoSolid3 left, Plain, Welding, out SubtractReport report));
            Assert.Equal(today, left);
            Assert.Equal(before.Taken, report.Taken);
            Assert.Empty(report.Skipped);
            Assert.Empty(report.Closed);
            Assert.Empty(report.Closings);
            AssertVolume(5680.0, left);
        }

        [Fact]
        public void ACutTakenTodayWithinTheFallback_IsTakenAsToday_NotClosed()
        {
            // The crack 0.005 wide, open within a thousandth, is closed by the cut within a hundredth: taken today within the
            // fallback, and so taken still, as it is, though the welds could close the cut within a thousandth.
            GeoSolid3 body = ClosingTestBodies.BoxCrackedThroughTheFront(0.005);
            GeoSolid3[] tools = { ClosingTestBodies.PostThrough() };

            Assert.True(body.TrySubtractAll(tools, out GeoSolid3 today, WithFallback, out SubtractReport before));
            Assert.Equal(new[] { 0 }, before.WithinFallback);

            Assert.True(body.TrySubtractAll(tools, out GeoSolid3 left, WithFallback, Welding, out SubtractReport report));
            Assert.Equal(today, left);
            Assert.Equal(new[] { 0 }, report.WithinFallback);
            Assert.Empty(report.Skipped);
            Assert.Empty(report.Closed);
            Assert.Empty(report.Closings);
        }

        [Fact]
        public void WhereTheCutWithinTheToleranceDoesNotClose_TheCutWithinTheFallbackIsClosed()
        {
            // The front cracked 0.008 wide, and the top corner over (30, 20) cut off 0.2 along each edge, the triangle left
            // out.
            // Within a thousandth the cut keeps both: the crack is too wide for the welds, and as a hole, 0.08, larger than
            // may be filled. Within a hundredth the crack closes and the corner stays open: not valid, skipped today, and
            // closed by filling the corner, 0.035, no weld.
            GeoSolid3 body = ClosingTestBodies.WithCornerCutOffOpen(ClosingTestBodies.BoxCrackedThroughTheFront(0.008), new GeoPoint3(30, 20, 10), 0.2);
            GeoSolid3[] tools = { ClosingTestBodies.PostThrough() };
            var filling = new SolidClosingOptions(Fine, 0.005, 0.05, 0.0, FillStrategy.WhenUnambiguous);

            Assert.True(body.TrySubtractAll(tools, out _, WithFallback, out SubtractReport before));
            Assert.Equal(new[] { 0 }, before.Skipped);
            Assert.True(body.TrySubtract(tools[0], out GeoSolid3 within, Plain, out _));
            Assert.False(within.TryClose(out _, filling, out _));

            Assert.True(body.TrySubtractAll(tools, out GeoSolid3 left, WithFallback, filling, out SubtractReport report));
            Assert.Equal(new[] { 0 }, report.Closed);
            Assert.Empty(report.Skipped);
            SolidRepair3 fill = Assert.Single(Assert.Single(report.Closings).Repairs);
            Assert.Equal(SolidRepairKind.Fill, fill.Kind);
            Assert.InRange(fill.Size, 0.0346, 0.0347);
            Assert.True(left.Validate(Fine).IsValid, left.Validate(Fine).ToString());
            AssertVolume(5760.0 - (0.2 * 0.2 * 0.2 / 6.0), left);

            // With no fallback, the cut within the tolerance is all there is, and it does not close.
            Assert.True(body.TrySubtractAll(tools, out GeoSolid3 kept, Plain, filling, out SubtractReport plain));
            Assert.Equal(new[] { 0 }, plain.Skipped);
            Assert.Empty(plain.Closed);
            Assert.Same(body, kept);
        }

        [Fact]
        public void AClosedCutHoldingMoreThanTheBodyBefore_IsRefused_AndTheCutSkipped()
        {
            // The box with its top turned over holds 2 000 as it is read. Cut by the post, the result is closed by turning the
            // top back, and holds 6 000: the post's hole is gone, and a difference never grows the body. Refused within the
            // tolerance and within the fallback, the cut is skipped and the body kept as it was.
            GeoSolid3 body = ClosingTestBodies.BoxWithFaceFlipped(ClosingTestBodies.Top);
            GeoSolid3[] tools = { ClosingTestBodies.PostThrough() };
            Assert.Equal(2000.0, body.GetVolume(Fine), 6);
            Assert.True(body.TrySubtract(tools[0], out GeoSolid3 within, Plain, out _));
            Assert.True(within.TryClose(out GeoSolid3 grown, Welding, out _));
            Assert.Equal(6000.0, grown.GetVolume(Fine), 6);

            foreach (SolidBooleanOptions options in new[] { Plain, WithFallback })
            {
                Assert.True(body.TrySubtractAll(tools, out GeoSolid3 left, options, Welding, out SubtractReport report));
                Assert.Equal(new[] { 0 }, report.Skipped);
                Assert.Empty(report.Closed);
                Assert.Same(body, left);
            }
        }

        [Fact]
        public void AClosingWithinACoarserTolerance_LeavingTheCutOpenWithinTheBooleansTolerance_IsNotTaken()
        {
            // Within a hundredth, a crack 0.005 wide reads closed, and closing takes the cut as it is; within the booleans'
            // thousandth it is still open, and every cut taken must be valid within that.
            GeoSolid3 body = ClosingTestBodies.BoxCrackedThroughTheFront(0.005);
            GeoSolid3[] tools = { ClosingTestBodies.PostThrough() };

            Assert.True(body.TrySubtractAll(tools, out GeoSolid3 left, Plain, new SolidClosingOptions(Hundredth, 0.005), out SubtractReport report));
            Assert.Equal(new[] { 0 }, report.Skipped);
            Assert.Empty(report.Closed);
            Assert.Same(body, left);
        }

        [Fact]
        public void TheReport_ListsTheCutsClosed_InTheOrderGiven()
        {
            // The first tool's cut cannot be worked out, and with no body to close it is skipped; the second's is closed, and
            // the box is whole again for the third, taken as it is. Without closing, all three are skipped: the crack stays.
            GeoSolid3 body = ClosingTestBodies.BoxCrackedThroughTheFront(0.005);
            GeoSolid3[] tools = { ClosingTestBodies.PostThroughWithItsTopTurned(), ClosingTestBodies.PostThrough(), ClosingTestBodies.NotchInTheTop() };

            Assert.True(body.TrySubtractAll(tools, out _, Plain, out SubtractReport before));
            Assert.Equal(new[] { 0, 1, 2 }, before.Skipped);
            Assert.Empty(before.Closed);
            Assert.Empty(before.Closings);

            Assert.True(body.TrySubtractAll(tools, out GeoSolid3 left, Plain, Welding, out SubtractReport report));
            Assert.Equal(3, report.Tools);
            Assert.Equal(2, report.Taken);
            Assert.Equal(new[] { 0 }, report.Skipped);
            Assert.Equal(new[] { 1 }, report.Closed);
            Assert.Single(report.Closings);
            Assert.Contains("Closed: 1", report.ToString(), StringComparison.Ordinal);
            AssertVolume(5680.0, left);
        }

        [Fact]
        public void TheSameCall_GivesTheSameReport_AndTheSameBody()
        {
            GeoSolid3 body = ClosingTestBodies.BoxCrackedThroughTheFront(0.005);
            GeoSolid3[] tools = { ClosingTestBodies.PostThroughWithItsTopTurned(), ClosingTestBodies.PostThrough(), ClosingTestBodies.NotchInTheTop() };

            Assert.True(body.TrySubtractAll(tools, out GeoSolid3 first, Plain, Welding, out SubtractReport one));
            Assert.True(body.TrySubtractAll(tools, out GeoSolid3 second, Plain, Welding, out SubtractReport two));
            Assert.Equal(first, second);
            Assert.Equal(one.ToString(), two.ToString());
            Assert.Equal(one.Skipped, two.Skipped);
            Assert.Equal(one.Closed, two.Closed);
            Assert.Equal(one.Closings.Select(closing => closing.ToString()), two.Closings.Select(closing => closing.ToString()));
        }

        [Fact]
        public void TheStaticAndTheInstanceCalls_Agree()
        {
            GeoSolid3 body = ClosingTestBodies.BoxCrackedThroughTheFront(0.005);
            GeoSolid3[] tools = { ClosingTestBodies.PostThrough() };

            Assert.True(Boolean3.TrySubtractAll(body, tools, out GeoSolid3 a, Plain, Welding, out SubtractReport ra));
            Assert.True(body.TrySubtractAll(tools, out GeoSolid3 b, Plain, Welding, out SubtractReport rb));
            Assert.Equal(a, b);
            Assert.Equal(ra.ToString(), rb.ToString());
        }

        [Fact]
        public void ClosingOptionsThatAreNull_Throw()
        {
            // Callers wanting no closing call the overload without it.
            GeoSolid3 body = ClosingTestBodies.Box();
            GeoSolid3[] tools = { ClosingTestBodies.PostThrough() };

            ArgumentNullException thrown = Assert.Throws<ArgumentNullException>(() => body.TrySubtractAll(tools, out _, Plain, null, out _));
            Assert.Equal("closing", thrown.ParamName);
            thrown = Assert.Throws<ArgumentNullException>(() => Boolean3.TrySubtractAll(body, tools, out _, Plain, null, out _));
            Assert.Equal("closing", thrown.ParamName);
        }

        // Within a millionth of what it should hold.
        private static void AssertVolume(double expected, GeoSolid3 body)
            => Assert.InRange(body.GetVolume(Fine), expected * (1.0 - 1E-6), expected * (1.0 + 1E-6));
    }
}
