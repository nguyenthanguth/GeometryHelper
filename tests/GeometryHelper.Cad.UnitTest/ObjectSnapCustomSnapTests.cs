using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Cad.Plugins;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The object snaps plugins add (D517, D518): a point a plugin's finder gives competes with the built-in snaps by distance
    /// from the cursor, the built-in one winning a tie, then the snap loaded first, then the first point the finder gave. All
    /// on a circle A1 of radius 10 about the origin, whose Quadrant point at the top is (0, 10), and a Center at the origin.
    /// </summary>
    public class ObjectSnapCustomSnapTests
    {
        private const double Aperture = 2.0;

        /// <summary>The point of the circle 2 degrees short of its top: (0.349, 9.994), 0.35 from the quadrant point.</summary>
        private static readonly GeoPoint2 Near88 = new GeoPoint2(
            10.0 * Math.Cos(88.0 * Math.PI / 180.0),
            10.0 * Math.Sin(88.0 * Math.PI / 180.0)
        );

        private static CadCustomSnap Snap(string keyword, CadSnapFinder finder) =>
            new CadCustomSnap("Test " + keyword, keyword, finder);

        private static CadSnapFinder Gives(params GeoPoint2[] points) => request => points;

        private static ObjectSnap Scene(
            CadSnapModes modes,
            out PickIndex index,
            string[] entities,
            params CadCustomSnap[] snaps
        )
        {
            CadDocument document = CadDocument.From(TestDrawings.Drawing(null, null, entities), "custom.dxf");
            index = document.GetPickIndex(document.ModelSpace);
            return new ObjectSnap(Tight)
            {
                Modes = modes,
                CustomSnaps = snaps,
                EntityOf = document.FindEntity,
                Document = document,
            };
        }

        private static ObjectSnap Circle(CadSnapModes modes, out PickIndex index, params CadCustomSnap[] snaps) =>
            Scene(modes, out index, new[] { TestDrawings.Circle(0, 0, 10, handle: "A1") }, snaps);

        // ---- competing with the built-in snaps ----

        [Fact]
        public void ACustomSnapAtTheTopOfACircle_TiesWithQuadrantAndTheBuiltInSnapWinsWhenTheCursorIsOnTheAxis()
        {
            // The cursor (0, 9.5) is 0.5 from both (0, 10) and the quadrant point (6E-16, 10): equal to the bit, so Quadrant.
            CadCustomSnap top = Snap("CTOP", Gives(P(0, 10)));
            ObjectSnap snap = Circle(CadSnapModes.Quadrant, out PickIndex index, top);

            CadSnapResult result = snap.Snap(index, P(0, 9.5), Aperture);

            Assert.Equal(CadSnapKind.Quadrant, result.Kind);
            Assert.Null(result.CustomSnap);
            Near(P(0, 10), result.Point, 1E-9);
        }

        [Theory]
        [InlineData(-0.3)]
        [InlineData(0.3)]
        public void ACustomSnapAtTheTopOfACircle_TiesWithQuadrantFromEitherSideOfTheTopNotOnlyOnTheAxis(double x)
        {
            // Quadrant's 90 degree point is (6E-16, 10), the custom snap's (0, 10): 6E-16 apart, a tie by any tolerance. From
            // the left of the top the custom point is nearer by 1E-16 and from the right farther; D517 says the built-in wins
            // at equal distance, so both sides must give Quadrant, not whichever way the last bit falls.
            CadCustomSnap top = Snap("CTOP", Gives(P(0, 10)));
            ObjectSnap snap = Circle(CadSnapModes.Quadrant, out PickIndex index, top);

            CadSnapResult result = snap.Snap(index, P(x, 9.8), Aperture);

            Assert.Equal(CadSnapKind.Quadrant, result.Kind);
            Assert.Null(result.CustomSnap);
        }

        [Fact]
        public void ACustomPointNearerThanQuadrantWins_AndOneFartherLoses()
        {
            // Near88 is 0.35 from the quadrant point. From (0.4, 9.9) it is 0.107 away and Quadrant 0.412: the custom point
            // wins. From (0, 9.9) Quadrant is 0.1 away and Near88 0.361: Quadrant wins. Both are inside the aperture of 2.
            CadCustomSnap near = Snap("NEAR88", Gives(Near88));
            ObjectSnap snap = Circle(CadSnapModes.Quadrant, out PickIndex index, near);

            CadSnapResult custom = snap.Snap(index, P(0.4, 9.9), Aperture);
            CadSnapResult builtIn = snap.Snap(index, P(0.0, 9.9), Aperture);

            Assert.Equal(CadSnapKind.Custom, custom.Kind);
            Assert.Same(near, custom.CustomSnap);
            Assert.Equal("A1", custom.Handle);
            Near(Near88, custom.Point, 1E-9);
            Assert.Equal(CadSnapKind.Quadrant, builtIn.Kind);
            Assert.Null(builtIn.CustomSnap);
        }

        [Fact]
        public void ACustomSnapAnswersWithNoBuiltInSnapOnAndNotWhenObjectSnapIsOff()
        {
            CadCustomSnap near = Snap("NEAR88", Gives(Near88));
            ObjectSnap snap = Circle(CadSnapModes.None, out PickIndex index, near);

            CadSnapResult on = snap.Snap(index, P(0.4, 9.9), Aperture);
            snap.IsOn = false;
            CadSnapResult off = snap.Snap(index, P(0.4, 9.9), Aperture);

            Assert.Equal(CadSnapKind.Custom, on.Kind);
            Assert.Equal(CadSnapKind.None, off.Kind);
            Near(P(0.4, 9.9), off.Point, 1E-9);
        }

        [Fact]
        public void ABuiltInResultHasNoCustomSnapAndTheKindCustomIsTheLastOfTheKinds()
        {
            ObjectSnap snap = Circle(CadSnapModes.Center, out PickIndex index);

            CadSnapResult result = snap.Snap(index, P(0.2, 0.3), Aperture);

            Assert.Equal(CadSnapKind.Center, result.Kind);
            Assert.Null(result.CustomSnap);
            Assert.Equal(CadSnapKind.Custom, Enum.GetValues(typeof(CadSnapKind)).Cast<CadSnapKind>().Last());
        }

        // ---- the override ----

        [Fact]
        public void TheOverrideTakesTheCustomSnapAloneEvenWhereAQuadrantPointIsNearer()
        {
            // From (0, 9.9) Quadrant is 0.1 away and Near88 0.361; with the override only the custom snap is asked.
            CadCustomSnap near = Snap("NEAR88", Gives(Near88));
            ObjectSnap snap = Circle(CadSnapModes.Quadrant | CadSnapModes.Center, out PickIndex index, near);
            snap.CustomSnaps = new CadCustomSnap[0];
            snap.CustomOverride = near;

            CadSnapResult result = snap.Snap(index, P(0.0, 9.9), Aperture);

            Assert.Equal(CadSnapKind.Custom, result.Kind);
            Assert.Same(near, result.CustomSnap);
            Near(Near88, result.Point, 1E-9);
        }

        [Fact]
        public void TheOverrideAnswersWhileObjectSnapIsOffAndLeavesTheCursorWhereNoPointIsInTheAperture()
        {
            CadCustomSnap near = Snap("NEAR88", Gives(Near88));
            ObjectSnap snap = Circle(CadSnapModes.Quadrant, out PickIndex index);
            snap.IsOn = false;
            snap.CustomOverride = near;

            CadSnapResult inside = snap.Snap(index, P(0.0, 9.9), Aperture);
            // Aperture 0.2: Near88 is 0.361 away, outside it, and Quadrant's point, 0.1 away, is not asked.
            CadSnapResult outside = snap.Snap(index, P(0.0, 9.9), 0.2);

            Assert.Equal(CadSnapKind.Custom, inside.Kind);
            Assert.Equal(CadSnapKind.None, outside.Kind);
            Near(P(0.0, 9.9), outside.Point, 1E-9);
        }

        [Fact]
        public void WithTheOverrideClearedTheBuiltInSnapsAnswerAgain()
        {
            CadCustomSnap near = Snap("NEAR88", Gives(Near88));
            ObjectSnap snap = Circle(CadSnapModes.Quadrant, out PickIndex index);
            snap.CustomOverride = near;
            Assert.Equal(CadSnapKind.Custom, snap.Snap(index, P(0.0, 9.9), Aperture).Kind);

            snap.CustomOverride = null;

            Assert.Equal(CadSnapKind.Quadrant, snap.Snap(index, P(0.0, 9.9), Aperture).Kind);
        }

        // ---- which entities, and what the finder is given ----

        [Fact]
        public void EntityTypesLeaveOutALineAndNameCirclesBySubclassToo()
        {
            // A1 a circle about the origin, L1 a line along y = 30; the finder says which entity it was asked about.
            string[] entities =
            {
                TestDrawings.Circle(0, 0, 10, handle: "A1"),
                TestDrawings.Line(-50, 30, 50, 30, handle: "L1"),
            };
            var asked = new List<string>();
            CadSnapFinder finder = request =>
            {
                asked.Add(request.Handle);
                return new[] { request.Cursor };
            };
            var circles = Snap("CIRCLESONLY", finder);
            circles.EntityTypes.Add(typeof(DxfCircle));
            ObjectSnap snap = Scene(CadSnapModes.None, out PickIndex index, entities, circles);

            CadSnapResult online = snap.Snap(index, P(20, 30.1), Aperture);
            CadSnapResult oncircle = snap.Snap(index, P(0.0, 9.9), Aperture);

            Assert.Equal(CadSnapKind.None, online.Kind);
            Assert.Equal(new[] { "A1" }, asked);
            Assert.Equal(CadSnapKind.Custom, oncircle.Kind);

            // Any curve: DxfCircle derives from DxfCurve, DxfLine from it too.
            asked.Clear();
            circles.EntityTypes.Clear();
            circles.EntityTypes.Add(typeof(DxfCurve));
            Assert.Equal(CadSnapKind.Custom, snap.Snap(index, P(20, 30.1), Aperture).Kind);
            Assert.Equal(new[] { "L1" }, asked);

            // No type named: every entity.
            asked.Clear();
            circles.EntityTypes.Clear();
            Assert.Equal(CadSnapKind.Custom, snap.Snap(index, P(20, 30.1), Aperture).Kind);
            Assert.Equal(new[] { "L1" }, asked);

            // A line only: the circle is left out.
            asked.Clear();
            circles.EntityTypes.Clear();
            circles.EntityTypes.Add(typeof(DxfLine));
            Assert.Equal(CadSnapKind.None, snap.Snap(index, P(0.0, 9.9), Aperture).Kind);
            Assert.Empty(asked);
        }

        [Fact]
        public void TheFinderIsGivenTheDocumentTheEntityItsHandleTheCursorTheApertureAndTheBasePoint()
        {
            CadDocument document = CadDocument.From(
                TestDrawings.Drawing(null, null, TestDrawings.Circle(0, 0, 10, handle: "A1")),
                "request.dxf"
            );
            PickIndex index = document.GetPickIndex(document.ModelSpace);
            CadSnapRequest seen = null;
            var snap = new ObjectSnap(Tight)
            {
                Modes = CadSnapModes.None,
                EntityOf = document.FindEntity,
                Document = document,
                CustomSnaps = new[] { Snap("SEEN", request =>
                    {
                        seen = request;
                        return new GeoPoint2[0];
                    }) },
            };

            snap.Snap(index, P(0.0, 9.9), Aperture, P(50, 60));

            Assert.NotNull(seen);
            Assert.Same(document, seen.Document);
            Assert.IsType<DxfCircle>(seen.Entity);
            Assert.Equal("A1", seen.Handle);
            Near(P(0.0, 9.9), seen.Cursor, 1E-12);
            Assert.Equal(Aperture, seen.Aperture, 12);
            Near(P(50, 60), seen.BasePoint.Value, 1E-12);

            snap.Snap(index, P(0.0, 9.9), Aperture);
            Assert.Null(seen.BasePoint);
        }

        [Fact]
        public void TheFinderIsAskedOncePerEntityPerCallInOrdinalOrderOfTheHandlesWhateverOrderTheyAreDrawnIn()
        {
            // A2 is drawn before A1; both circles pass within the aperture of (0, 9.9). Every point the finder gives is the
            // same, so the tie falls to the order they were found in: A1 first.
            string[] entities =
            {
                TestDrawings.Circle(0, 0, 10.5, handle: "A2"),
                TestDrawings.Circle(0, 0, 10, handle: "A1"),
            };
            var asked = new List<string>();
            var any = Snap("ANYTHING", request =>
            {
                asked.Add(request.Handle);
                return new[] { P(0.5, 9.9) };
            });
            ObjectSnap snap = Scene(CadSnapModes.None, out PickIndex index, entities, any);

            CadSnapResult first = snap.Snap(index, P(0.0, 9.9), Aperture);
            CadSnapResult second = snap.Snap(index, P(0.0, 9.9), Aperture);

            Assert.Equal(new[] { "A1", "A2", "A1", "A2" }, asked);
            Assert.Equal("A1", first.Handle);
            Assert.Equal("A1", second.Handle);
            Near(first.Point, second.Point, 0.0);
        }

        [Fact]
        public void AnEntityOnAHiddenLayerIsNotAskedAndNeitherIsOneWithoutAnEntityLookup()
        {
            string[] entities = { TestDrawings.Circle(0, 0, 10, layer: "Hid", handle: "A1") };
            int asked = 0;
            var counting = Snap("COUNTING", request =>
            {
                asked++;
                return new[] { P(0.5, 9.9) };
            });
            ObjectSnap snap = Scene(CadSnapModes.None, out PickIndex index, entities, counting);
            snap.HiddenLayers = new HashSet<string> { "Hid" };

            Assert.Equal(CadSnapKind.None, snap.Snap(index, P(0.0, 9.9), Aperture).Kind);
            Assert.Equal(0, asked);

            snap.HiddenLayers = null;
            Assert.Equal(CadSnapKind.Custom, snap.Snap(index, P(0.0, 9.9), Aperture).Kind);
            Assert.Equal(1, asked);

            snap.EntityOf = null;
            Assert.Equal(CadSnapKind.None, snap.Snap(index, P(0.0, 9.9), Aperture).Kind);
            Assert.Equal(1, asked);
        }

        // ---- the aperture ----

        [Fact]
        public void APointBeyondTheApertureIsDroppedWhereOneInsideItIsKept()
        {
            // From (0, 9.9) the point (0, 13) is 3.1 away and (0, 10.9) is 1.0: aperture 2, neither near the boundary.
            CadCustomSnap far = Snap("FAR", Gives(P(0, 13)));
            CadCustomSnap both = Snap("BOTH", Gives(P(0, 13), P(0, 10.9)));
            ObjectSnap snap = Circle(CadSnapModes.None, out PickIndex index, far);

            CadSnapResult dropped = snap.Snap(index, P(0.0, 9.9), Aperture);
            snap.CustomSnaps = new[] { both };
            CadSnapResult kept = snap.Snap(index, P(0.0, 9.9), Aperture);

            Assert.Equal(CadSnapKind.None, dropped.Kind);
            Assert.Equal(CadSnapKind.Custom, kept.Kind);
            Near(P(0, 10.9), kept.Point, 1E-9);
        }

        // ---- ties between custom points ----

        [Fact]
        public void TwoPointsAsNearAsEachOtherFromOneSnapTheFirstGivenWinsAndSwappingThemSwapsTheWinner()
        {
            // (1, 9.9) and (-1, 9.9) are both exactly 1 from the cursor (0, 9.9).
            ObjectSnap snap = Circle(
                CadSnapModes.None,
                out PickIndex index,
                Snap("PAIR", Gives(P(1, 9.9), P(-1, 9.9)))
            );
            CadSnapResult rightFirst = snap.Snap(index, P(0.0, 9.9), Aperture);
            snap.CustomSnaps = new[] { Snap("PAIR", Gives(P(-1, 9.9), P(1, 9.9))) };
            CadSnapResult leftFirst = snap.Snap(index, P(0.0, 9.9), Aperture);

            Near(P(1, 9.9), rightFirst.Point, 1E-9);
            Near(P(-1, 9.9), leftFirst.Point, 1E-9);
        }

        [Fact]
        public void TwoSnapsGivingTheSamePointTheOneLoadedFirstWinsAndSwappingThemSwapsTheWinner()
        {
            CadCustomSnap a = Snap("AAA", Gives(P(0.5, 9.9)));
            CadCustomSnap b = Snap("BBB", Gives(P(0.5, 9.9)));
            ObjectSnap snap = Circle(CadSnapModes.None, out PickIndex index, a, b);

            CadSnapResult aFirst = snap.Snap(index, P(0.0, 9.9), Aperture);
            snap.CustomSnaps = new[] { b, a };
            CadSnapResult bFirst = snap.Snap(index, P(0.0, 9.9), Aperture);

            Assert.Same(a, aFirst.CustomSnap);
            Assert.Same(b, bFirst.CustomSnap);
        }

        [Fact]
        public void ACloserPointOfASnapLoadedLaterBeatsAFartherPointOfOneLoadedFirst()
        {
            CadCustomSnap far = Snap("FAR", Gives(P(1.5, 9.9)));
            CadCustomSnap close = Snap("CLOSE", Gives(P(0.5, 9.9)));
            ObjectSnap snap = Circle(CadSnapModes.None, out PickIndex index, far, close);

            CadSnapResult result = snap.Snap(index, P(0.0, 9.9), Aperture);

            Assert.Same(close, result.CustomSnap);
        }

        // ---- a finder that misbehaves ----

        [Fact]
        public void AFinderThatThrowsIsToldAboutOnceIsNotAskedAgainAndTheBuiltInSnapsStillAnswer()
        {
            int asked = 0;
            var failures = new List<KeyValuePair<CadCustomSnap, Exception>>();
            CadCustomSnap bolt = Snap("BOLT", request =>
            {
                asked++;
                throw new InvalidOperationException("boom");
            });
            ObjectSnap snap = Circle(CadSnapModes.Quadrant, out PickIndex index, bolt);
            snap.CustomFailed = (s, e) => failures.Add(new KeyValuePair<CadCustomSnap, Exception>(s, e));

            CadSnapResult first = snap.Snap(index, P(0.0, 9.9), Aperture);
            CadSnapResult second = snap.Snap(index, P(0.0, 9.9), Aperture);

            Assert.Equal(CadSnapKind.Quadrant, first.Kind);
            Assert.Equal(CadSnapKind.Quadrant, second.Kind);
            Assert.Equal(1, asked);
            KeyValuePair<CadCustomSnap, Exception> failure = Assert.Single(failures);
            Assert.Same(bolt, failure.Key);
            Assert.Equal("boom", failure.Value.Message);
        }

        [Fact]
        public void AFinderThatThrowsDoesNotSilenceTheOtherCustomSnaps()
        {
            CadCustomSnap bolt = Snap("BOLT", request => throw new InvalidOperationException("boom"));
            CadCustomSnap near = Snap("NEAR88", Gives(Near88));
            ObjectSnap snap = Circle(CadSnapModes.Quadrant, out PickIndex index, bolt, near);

            CadSnapResult first = snap.Snap(index, P(0.4, 9.9), Aperture);
            CadSnapResult second = snap.Snap(index, P(0.4, 9.9), Aperture);

            Assert.Same(near, first.CustomSnap);
            Assert.Same(near, second.CustomSnap);
        }

        [Fact]
        public void AFinderThatGivesNoPointsAtAllIsNotAnErrorAndLeavesTheBuiltInSnapsTheirTurn()
        {
            int asked = 0;
            CadCustomSnap nothing = Snap("NOTHING", request =>
            {
                asked++;
                return null;
            });
            ObjectSnap snap = Circle(CadSnapModes.Quadrant, out PickIndex index, nothing);

            CadSnapResult first = snap.Snap(index, P(0.0, 9.9), Aperture);
            CadSnapResult second = snap.Snap(index, P(0.0, 9.9), Aperture);

            Assert.Equal(CadSnapKind.Quadrant, first.Kind);
            Assert.Equal(CadSnapKind.Quadrant, second.Kind);
            Assert.Equal(2, asked);
        }

        [Fact]
        public void TheSameCallTwiceGivesTheSameResultAndAsksTheFinderOncePerCall()
        {
            int asked = 0;
            CadCustomSnap near = Snap("NEAR88", request =>
            {
                asked++;
                return new[] { Near88 };
            });
            ObjectSnap snap = Circle(CadSnapModes.Quadrant, out PickIndex index, near);

            CadSnapResult first = snap.Snap(index, P(0.4, 9.9), Aperture);
            CadSnapResult second = snap.Snap(index, P(0.4, 9.9), Aperture);

            Assert.Equal(2, asked);
            Assert.Equal(first.Kind, second.Kind);
            Assert.Same(first.CustomSnap, second.CustomSnap);
            Near(first.Point, second.Point, 0.0);
        }

        // ---- the marker ----

        [Fact]
        public void ASnapWithNoGlyphIsDrawnAsASquareNineAcrossAndAFilledDotOfRadiusOnePointTwo()
        {
            var snap = Snap("PLAIN", Gives());

            IReadOnlyList<CadMarkerStroke> strokes = CadSnapMarker.StrokesOf(snap);

            Assert.Equal(2, strokes.Count);
            Assert.True(strokes[0].IsClosed);
            Assert.False(strokes[0].IsFilled);
            Assert.Equal(CadSnapMarker.Size, strokes[0].Points.Max(p => p.X) - strokes[0].Points.Min(p => p.X), 9);
            Assert.Equal(CadSnapMarker.Size, strokes[0].Points.Max(p => p.Y) - strokes[0].Points.Min(p => p.Y), 9);
            Assert.True(strokes[1].IsFilled);
            Assert.All(strokes[1].Points, p => Assert.Equal(1.2, Math.Sqrt(p.X * p.X + p.Y * p.Y), 6));
        }

        [Fact]
        public void ASnapWithAGlyphIsDrawnWithThatGlyphAsItIs()
        {
            var triangle = new CadMarkerStroke(new[] { P(0, -4.5), P(4.5, 4.5), P(-4.5, 4.5) }, true, filled: true);
            var snap = Snap("TRI", Gives());
            snap.Glyph = new[] { triangle };

            IReadOnlyList<CadMarkerStroke> strokes = CadSnapMarker.StrokesOf(snap);

            Assert.Same(triangle, Assert.Single(strokes));
        }

        [Fact]
        public void AMarkerStrokeMadeByAPluginKeepsACopyOfItsPointsAndItsFlags()
        {
            var points = new List<GeoPoint2> { P(0, -4.5), P(4.5, 4.5), P(-4.5, 4.5) };

            var stroke = new CadMarkerStroke(points, true, true, true);
            points.Clear();
            var plain = new CadMarkerStroke(new[] { P(0, 0), P(1, 1) }, false);

            Assert.Equal(3, stroke.Points.Count);
            Assert.True(stroke.IsClosed);
            Assert.True(stroke.IsDotted);
            Assert.True(stroke.IsFilled);
            Assert.False(plain.IsClosed);
            Assert.False(plain.IsDotted);
            Assert.False(plain.IsFilled);
            Assert.Throws<ArgumentNullException>(() => new CadMarkerStroke(null, false));
        }
    }
}
