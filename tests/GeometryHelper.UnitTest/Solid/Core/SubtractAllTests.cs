using System;
using System.Collections;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid.Core
{
    /// <summary>
    /// Taking bodies out of a solid one after another, by either overload: the arguments each checks, an empty list of
    /// tools, a tool that does not reach the body, a list that can be gone through once only, a body carrying openings, a
    /// tool taking all a closed cut left, and a closed cut refused for holding less than a cut can leave; see
    /// <see cref="GeoSolid3.TrySubtractAll(IEnumerable{GeoSolid3}, out GeoSolid3, SolidBooleanOptions, out SubtractReport)"/>
    /// and
    /// <see cref="GeoSolid3.TrySubtractAll(IEnumerable{GeoSolid3}, out GeoSolid3, SolidBooleanOptions, SolidClosingOptions, out SubtractReport)"/>.
    /// </summary>
    /// <remarks>
    /// The body is the box of <see cref="ClosingTestBodies"/>, 30 by 20 by 10, 6 000; the post of
    /// <see cref="ClosingTestBodies.PostThrough"/> holds 480 and takes 240 out of it.
    /// </remarks>
    public class SubtractAllTests
    {
        private static readonly Tolerance Fine = Tolerance.Default;

        // Parts drawn against each other cut as touching within a hundredth, and no fallback.
        private static readonly SolidBooleanOptions Plain = new SolidBooleanOptions(Fine, 0.01);

        // Welds within five thousandths, and fills nothing.
        private static readonly SolidClosingOptions Welding = new SolidClosingOptions(Fine, 0.005);

        [Fact]
        public void NullArguments_Throw_NamingTheArgument_InEitherOverload()
        {
            GeoSolid3 body = ClosingTestBodies.Box();
            GeoSolid3[] tools = { ClosingTestBodies.PostThrough() };

            AssertNull("subject", () => Boolean3.TrySubtractAll(null, tools, out _, Plain, out _));
            AssertNull("tools", () => Boolean3.TrySubtractAll(body, null, out _, Plain, out _));
            AssertNull("options", () => Boolean3.TrySubtractAll(body, tools, out _, null, out _));
            AssertNull("tools", () => body.TrySubtractAll(null, out _, Plain, out _));
            AssertNull("options", () => body.TrySubtractAll(tools, out _, null, out _));

            AssertNull("subject", () => Boolean3.TrySubtractAll(null, tools, out _, Plain, Welding, out _));
            AssertNull("tools", () => Boolean3.TrySubtractAll(body, null, out _, Plain, Welding, out _));
            AssertNull("options", () => Boolean3.TrySubtractAll(body, tools, out _, null, Welding, out _));
            AssertNull("tools", () => body.TrySubtractAll(null, out _, Plain, Welding, out _));
            AssertNull("options", () => body.TrySubtractAll(tools, out _, null, Welding, out _));
        }

        [Fact]
        public void ANullTool_Throws_WithClosingToo()
        {
            GeoSolid3 body = ClosingTestBodies.Box();
            GeoSolid3[] tools = { ClosingTestBodies.PostThrough(), null };

            ArgumentException thrown = Assert.Throws<ArgumentException>(() => body.TrySubtractAll(tools, out _, Plain, Welding, out _));
            Assert.Equal("tools", thrown.ParamName);
            thrown = Assert.Throws<ArgumentException>(() => Boolean3.TrySubtractAll(body, tools, out _, Plain, Welding, out _));
            Assert.Equal("tools", thrown.ParamName);
        }

        [Fact]
        public void NoTools_GiveTheVeryBodyBack_AndAnEmptyReport()
        {
            GeoSolid3 body = ClosingTestBodies.Box();

            Assert.True(body.TrySubtractAll(new GeoSolid3[0], out GeoSolid3 plain, Plain, out SubtractReport a));
            Assert.True(body.TrySubtractAll(new GeoSolid3[0], out GeoSolid3 closing, Plain, Welding, out SubtractReport b));

            foreach ((GeoSolid3 left, SubtractReport report) in new[] { (plain, a), (closing, b) })
            {
                Assert.Same(body, left);
                Assert.Equal(0, report.Tools);
                Assert.Equal(0, report.Taken);
                AssertEmpty(report);
            }
        }

        [Fact]
        public void AToolThatDoesNotReachTheBody_IsTaken_AndTheVeryBodyIsLeft()
        {
            // A block 70 clear of the box along x takes nothing, and the cut gives the box itself back.
            GeoSolid3 body = ClosingTestBodies.Box();
            GeoSolid3[] tools = { new GeoSolid3(ClosingTestBodies.BoxFaces(ClosingTestBodies.Corners(100, 0, 0, 110, 10, 10))) };

            Assert.True(body.TrySubtractAll(tools, out GeoSolid3 plain, Plain, out SubtractReport a));
            Assert.True(body.TrySubtractAll(tools, out GeoSolid3 closing, Plain, Welding, out SubtractReport b));

            foreach ((GeoSolid3 left, SubtractReport report) in new[] { (plain, a), (closing, b) })
            {
                Assert.Same(body, left);
                Assert.Equal(1, report.Tools);
                Assert.Equal(1, report.Taken);
                AssertEmpty(report);
            }
        }

        [Fact]
        public void AToolThatDoesNotReachABodyNotValid_IsSkipped_OrWithClosingClosesTheBody()
        {
            // As it is: the box cracked 0.005 through its front is not valid, and the cut by a tool far off gives it back
            // as it is, still not valid. Without closing that cut is skipped; with closing it is closed, the crack welded,
            // though the tool took nothing: the body comes back changed by a cut that never met it.
            GeoSolid3 body = ClosingTestBodies.BoxCrackedThroughTheFront(0.005);
            GeoSolid3[] tools = { new GeoSolid3(ClosingTestBodies.BoxFaces(ClosingTestBodies.Corners(100, 0, 0, 110, 10, 10))) };

            Assert.True(body.TrySubtractAll(tools, out GeoSolid3 plain, Plain, out SubtractReport a));
            Assert.Equal(new[] { 0 }, a.Skipped);
            Assert.Same(body, plain);

            Assert.True(body.TrySubtractAll(tools, out GeoSolid3 closing, Plain, Welding, out SubtractReport b));
            Assert.Equal(new[] { 0 }, b.Closed);
            Assert.Empty(b.Skipped);
            Assert.True(closing.Validate(Fine).IsValid, closing.Validate(Fine).ToString());
            AssertVolume(6000.0, closing);
        }

        [Fact]
        public void ToolsThatCanBeGoneThroughOnce_AreGoneThroughOnce_ByEitherOverload()
        {
            GeoSolid3 body = ClosingTestBodies.Box();

            Assert.True(body.TrySubtractAll(new Once(ClosingTestBodies.PostThrough(), ClosingTestBodies.NotchInTheTop()), out GeoSolid3 plain, Plain, out SubtractReport a));
            Assert.True(body.TrySubtractAll(new Once(ClosingTestBodies.PostThrough(), ClosingTestBodies.NotchInTheTop()), out GeoSolid3 closing, Plain, Welding, out SubtractReport b));
            Assert.Equal(2, a.Taken);
            Assert.Equal(2, b.Taken);
            AssertVolume(5680.0, plain);
            AssertVolume(5680.0, closing);
        }

        [Fact]
        public void ABodyCarryingAnOpening_IsCutAsItsMaterial_TheOpeningCarriedOut()
        {
            // An opening 10 by 6 by 6 in the box, clear of the post: the booleans cut the opening out first, and what the cut
            // leaves is material with no openings of its own, 6 000 less 360 and 240.
            var opening = new GeoSolid3(ClosingTestBodies.BoxFaces(ClosingTestBodies.Corners(1, 1, 2, 11, 7, 8)));
            var body = new GeoSolid3(ClosingTestBodies.Box().Faces, new[] { opening });
            Assert.Equal(5640.0, body.GetVolume(Fine), 6);
            GeoSolid3[] tools = { ClosingTestBodies.PostThrough() };

            Assert.True(body.TrySubtractAll(tools, out GeoSolid3 plain, Plain, out SubtractReport a));
            Assert.True(body.TrySubtractAll(tools, out GeoSolid3 closing, Plain, Welding, out SubtractReport b));

            foreach ((GeoSolid3 left, SubtractReport report) in new[] { (plain, a), (closing, b) })
            {
                Assert.Equal(1, report.Taken);
                AssertEmpty(report);
                Assert.Empty(left.Openings);
                Assert.True(left.Validate(Fine).IsValid, left.Validate(Fine).ToString());
                AssertVolume(5400.0, left);
            }

            Assert.Equal(plain, closing);
        }

        [Fact]
        public void ABodyCarryingAnOpening_IsNotClosed_ItsCutSkippedAsWithoutClosing()
        {
            // The box cracked 0.005 through its front, with the opening of the test before: its material reads 5 400, where
            // the box whole with the opening holds 5 640, and cut by the post the material is 5 160, open by the crack. The
            // 240 short come from cutting the opening out of a body not closed, before any closing, and no bound from below
            // holds a body with openings: closed, two welds, it would be taken holding 5 160 where the box whole with the
            // opening, cut by the post, holds 5 400. So a cut of a body with openings is not closed: it is skipped, as
            // without closing, the body kept as it was.
            var opening = new GeoSolid3(ClosingTestBodies.BoxFaces(ClosingTestBodies.Corners(1, 1, 2, 11, 7, 8)));
            var body = new GeoSolid3(ClosingTestBodies.BoxCrackedThroughTheFront(0.005).Faces, new[] { opening });
            GeoSolid3[] tools = { ClosingTestBodies.PostThrough() };

            Assert.True(body.TrySubtractAll(tools, out GeoSolid3 plain, Plain, out SubtractReport a));
            Assert.Equal(new[] { 0 }, a.Skipped);
            Assert.Same(body, plain);

            Assert.True(body.TrySubtractAll(tools, out GeoSolid3 left, Plain, Welding, out SubtractReport b));
            Assert.Equal(new[] { 0 }, b.Skipped);
            Assert.Empty(b.Closed);
            Assert.Empty(b.Closings);
            Assert.Empty(b.RefusedByVolume);
            Assert.Same(body, left);
        }

        [Fact]
        public void AToolCarryingAnOpening_IsNotClosed_ItsCutSkippedAsWithoutClosing()
        {
            // The same crack cut by the post carrying an opening of its own: the body before holds no opening, but the tool
            // as put onto it does, and its material is read as the subject's is. Its cut is not closed either.
            var hollow = new GeoSolid3(ClosingTestBodies.BoxFaces(ClosingTestBodies.Corners(13.5, 8.5, 3, 16.5, 11.5, 7)));
            var tool = new GeoSolid3(ClosingTestBodies.PostThrough().Faces, new[] { hollow });
            GeoSolid3 body = ClosingTestBodies.BoxCrackedThroughTheFront(0.005);

            Assert.True(body.TrySubtractAll(new[] { tool }, out GeoSolid3 left, Plain, Welding, out SubtractReport report));
            Assert.Empty(report.Closed);
            Assert.Equal(new[] { 0 }, report.Skipped);
            Assert.Same(body, left);
        }

        [Fact]
        public void ACutClosed_ThenAToolTakingAllThatIsLeft_LeavesNothing_AndTheReportListsTheClosedCut()
        {
            // The post's cut, open by the crack, is closed; then a block round the whole box takes all of it.
            GeoSolid3 body = ClosingTestBodies.BoxCrackedThroughTheFront(0.005);
            GeoSolid3[] tools = { ClosingTestBodies.PostThrough(), new GeoSolid3(ClosingTestBodies.BoxFaces(ClosingTestBodies.Corners(-5, -5, -5, 35, 25, 15))) };

            Assert.False(body.TrySubtractAll(tools, out GeoSolid3 left, Plain, Welding, out SubtractReport report));
            Assert.Null(left);
            Assert.Equal(2, report.Tools);
            Assert.Equal(2, report.Taken);
            Assert.Empty(report.Skipped);
            Assert.Equal(new[] { 0 }, report.Closed);
            Assert.Single(report.Closings);
        }

        [Fact]
        public void AClosedCutHoldingLessThanTheBodyBeforeLessTheTool_IsRefused_AndTheCutSkipped()
        {
            // The box with its top given twice, the same way round, reads 8 000: the copy holds 2 000 more, measured as the
            // faces are. Cut by the post, the result keeps the copy; closed, the copy dropped, it holds 5 760, which is less
            // than 8 000 less the whole post, 7 520, by far more than the slack: refused, the cut skipped, and the body kept
            // as it was. (5 760 is what the box less the post holds: the bound reads the body before by its faces.)
            GeoSolid3 body = ClosingTestBodies.BoxWithFaceTwice(ClosingTestBodies.Top);
            GeoSolid3 post = ClosingTestBodies.PostThrough();
            Assert.Equal(8000.0, body.GetVolume(Fine), 6);
            Assert.True(body.TrySubtract(post, out GeoSolid3 within, Plain, out _));
            Assert.True(within.TryClose(out GeoSolid3 closed, Welding, out _));
            Assert.InRange(closed.GetVolume(Fine), 0.0, 8000.0 - 480.0 - 100.0);

            Assert.True(body.TrySubtractAll(new[] { post }, out GeoSolid3 left, Plain, Welding, out SubtractReport report));
            Assert.Equal(new[] { 0 }, report.RefusedByVolume);
            Assert.Equal(new[] { 0 }, report.Skipped);
            Assert.Empty(report.Closed);
            Assert.Equal(0, report.Taken);
            Assert.Same(body, left);
        }

        [Fact]
        public void AClosedCutHoldingMoreThanTheBodyBefore_IsListedAsRefusedByVolume()
        {
            // The box with its top turned over reads 2 000, and its cut by the post closes to 6 000: refused from above.
            GeoSolid3 body = ClosingTestBodies.BoxWithFaceFlipped(ClosingTestBodies.Top);

            Assert.True(body.TrySubtractAll(new[] { ClosingTestBodies.PostThrough() }, out GeoSolid3 left, Plain, Welding, out SubtractReport report));
            Assert.Equal(new[] { 0 }, report.RefusedByVolume);
            Assert.Equal(new[] { 0 }, report.Skipped);
            Assert.Same(body, left);
        }

        [Fact]
        public void TheOverloadWithoutClosing_RefusesNothingByVolume()
        {
            GeoSolid3 body = ClosingTestBodies.BoxWithFaceTwice(ClosingTestBodies.Top);

            Assert.True(body.TrySubtractAll(new[] { ClosingTestBodies.PostThrough() }, out _, Plain, out SubtractReport report));
            Assert.Equal(new[] { 0 }, report.Skipped);
            Assert.Empty(report.RefusedByVolume);
        }

        private static void AssertNull(string name, Action call)
        {
            ArgumentNullException thrown = Assert.Throws<ArgumentNullException>(call);
            Assert.Equal(name, thrown.ParamName);
        }

        // Nothing skipped, within the fallback, closed or refused.
        private static void AssertEmpty(SubtractReport report)
        {
            Assert.Empty(report.Skipped);
            Assert.Empty(report.WithinFallback);
            Assert.Empty(report.Closed);
            Assert.Empty(report.Closings);
            Assert.Empty(report.RefusedByVolume);
        }

        // Within a millionth of what it should hold.
        private static void AssertVolume(double expected, GeoSolid3 body)
            => Assert.InRange(body.GetVolume(Fine), expected * (1.0 - 1E-6), expected * (1.0 + 1E-6));

        // Bodies that can be gone through once: a second time throws.
        private sealed class Once : IEnumerable<GeoSolid3>
        {
            private readonly GeoSolid3[] _bodies;
            private bool _gone;

            internal Once(params GeoSolid3[] bodies)
            {
                _bodies = bodies;
            }

            public IEnumerator<GeoSolid3> GetEnumerator()
            {
                if (_gone)
                {
                    throw new InvalidOperationException("These bodies can be gone through once only.");
                }

                _gone = true;
                return ((IEnumerable<GeoSolid3>)_bodies).GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
