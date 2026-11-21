using System;
using System.Collections.Generic;
using System.IO;
using GeometryHelper.Cad.Plugins;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The object snaps plugins add (<see cref="CadCustomSnap"/>) worked out by <see cref="ObjectSnap"/>: a circle of radius 10
    /// about the origin, handle A3, whose top is also its Quadrant point, a line A1 along the X axis, and finders that give
    /// the points the tests choose.
    /// </summary>
    public class CustomSnapGatheringTests
    {
        private static CadDocument Drawing()
        {
            return CadDocument.From(
                TestDrawings.Drawing(
                    null,
                    null,
                    TestDrawings.Circle(0, 0, 10, handle: "A3"),
                    TestDrawings.Line(-50, 0, -20, 0, handle: "A1")
                ),
                "custom.dxf"
            );
        }

        private static ObjectSnap Snaps(CadDocument document, CadSnapModes modes, params CadCustomSnap[] custom) =>
            new ObjectSnap(Tight)
            {
                Modes = modes,
                CustomSnaps = custom,
                EntityOf = document.FindEntity,
                Document = document,
            };

        private static CadCustomSnap At(string name, string keyword, params GeoPoint2[] points) =>
            new CadCustomSnap(name, keyword, request => points);

        [Fact]
        public void ACustomPointNearerThanTheQuadrant_Wins_AndOneAtTheSameDistanceLoses()
        {
            CadDocument document = Drawing();
            PickIndex index = document.GetPickIndex(document.ModelSpace);
            CadCustomSnap nearer = At("Near top", "NTOP", P(0.2, 10.0));
            CadCustomSnap same = At("Top", "TOPP", P(0.0, 10.0));
            GeoPoint2 cursor = P(0.2, 9.9);

            CadSnapResult won = Snaps(document, CadSnapModes.Quadrant, nearer).Snap(index, cursor, 2.0);
            CadSnapResult tied = Snaps(document, CadSnapModes.Quadrant, same).Snap(index, P(0.0, 9.9), 2.0);

            Assert.Equal(CadSnapKind.Custom, won.Kind);
            Assert.Same(nearer, won.CustomSnap);
            Assert.Equal("A3", won.Handle);
            Near(P(0.2, 10.0), won.Point);
            Assert.Equal(CadSnapKind.Quadrant, tied.Kind);
            Assert.Null(tied.CustomSnap);
        }

        [Theory]
        [InlineData(-0.3)]
        [InlineData(0.3)]
        public void ACustomPointAtTheQuadrantPoint_TiesWithItFromEitherSideOfTheTop(double x)
        {
            // Quadrant's 90 degree point comes from cos(pi/2), (6E-16, 10); the custom point is (0, 10). From the left of the
            // top the custom one is nearer by 1E-16, from the right farther: the built-in must win on both sides.
            CadDocument document = Drawing();
            PickIndex index = document.GetPickIndex(document.ModelSpace);
            CadCustomSnap top = At("Top", "TOPP", P(0.0, 10.0));

            CadSnapResult result = Snaps(document, CadSnapModes.Quadrant, top).Snap(index, P(x, 9.8), 2.0);

            Assert.Equal(CadSnapKind.Quadrant, result.Kind);
            Assert.Null(result.CustomSnap);
        }

        [Fact]
        public void ACustomSnapGivesItsPointsWhereNoBuiltInSnapIsOn()
        {
            CadDocument document = Drawing();
            PickIndex index = document.GetPickIndex(document.ModelSpace);
            CadCustomSnap snap = At("Top", "TOPP", P(0.0, 10.0));

            CadSnapResult result = Snaps(document, CadSnapModes.None, snap).Snap(index, P(0.3, 9.8), 2.0);

            Assert.Equal(CadSnapKind.Custom, result.Kind);
            Near(P(0.0, 10.0), result.Point);
        }

        [Fact]
        public void WithObjectSnapOff_ACustomSnapIsNotTaken_ButItsOverrideIs()
        {
            CadDocument document = Drawing();
            PickIndex index = document.GetPickIndex(document.ModelSpace);
            CadCustomSnap snap = At("Top", "TOPP", P(0.0, 10.0));
            ObjectSnap snaps = Snaps(document, CadSnapModes.None, snap);
            snaps.IsOn = false;

            Assert.Equal(CadSnapKind.None, snaps.Snap(index, P(0.3, 9.8), 2.0).Kind);

            snaps.CustomOverride = snap;
            Assert.Equal(CadSnapKind.Custom, snaps.Snap(index, P(0.3, 9.8), 2.0).Kind);
        }

        [Fact]
        public void TheOverrideOfACustomSnap_TakesOnlyThatSnap_EvenWhereAQuadrantIsNearer()
        {
            CadDocument document = Drawing();
            PickIndex index = document.GetPickIndex(document.ModelSpace);
            CadCustomSnap snap = At("Side", "SIDE", P(1.5, 9.9));
            ObjectSnap snaps = Snaps(document, CadSnapModes.Quadrant, snap);
            snaps.CustomOverride = snap;

            CadSnapResult result = snaps.Snap(index, P(0.0, 9.95), 2.0);

            Assert.Equal(CadSnapKind.Custom, result.Kind);
            Near(P(1.5, 9.9), result.Point);
        }

        [Fact]
        public void ABuiltInOverride_LeavesTheCustomSnapsOut()
        {
            CadDocument document = Drawing();
            PickIndex index = document.GetPickIndex(document.ModelSpace);
            CadCustomSnap snap = At("Near top", "NTOP", P(0.05, 10.0));
            ObjectSnap snaps = Snaps(document, CadSnapModes.Quadrant, snap);
            snaps.Override = CadSnapModes.Quadrant;

            CadSnapResult result = snaps.Snap(index, P(0.1, 9.9), 2.0);

            Assert.Equal(CadSnapKind.Quadrant, result.Kind);
        }

        [Fact]
        public void EntityTypes_KeepTheSnapToTheCirclesAndEmptyMeansEveryEntity()
        {
            CadDocument document = Drawing();
            PickIndex index = document.GetPickIndex(document.ModelSpace);
            var circlesOnly = new CadCustomSnap("Anywhere", "ANYW", request => new[] { request.Cursor });
            circlesOnly.EntityTypes.Add(typeof(DxfCircle));
            var every = new CadCustomSnap("Anywhere", "EVRY", request => new[] { request.Cursor });

            CadSnapResult onLine = Snaps(document, CadSnapModes.None, circlesOnly).Snap(index, P(-35, 0.3), 2.0);
            CadSnapResult onCircle = Snaps(document, CadSnapModes.None, circlesOnly).Snap(index, P(0.3, 10.2), 2.0);
            CadSnapResult any = Snaps(document, CadSnapModes.None, every).Snap(index, P(-35, 0.3), 2.0);

            Assert.Equal(CadSnapKind.None, onLine.Kind);
            Assert.Equal(CadSnapKind.Custom, onCircle.Kind);
            Assert.Equal(CadSnapKind.Custom, any.Kind);
            Assert.Equal("A1", any.Handle);
        }

        [Fact]
        public void TheFinderIsGivenTheEntityTheCursorTheApertureAndTheBasePoint()
        {
            CadDocument document = Drawing();
            PickIndex index = document.GetPickIndex(document.ModelSpace);
            CadSnapRequest seen = null;
            var snap = new CadCustomSnap("Spy", "SPYY", request =>
            {
                seen = request;
                return null;
            });

            Snaps(document, CadSnapModes.None, snap).Snap(index, P(0.3, 10.2), 2.0, P(5, 5));

            Assert.NotNull(seen);
            Assert.Same(document, seen.Document);
            Assert.IsType<DxfCircle>(seen.Entity);
            Assert.Equal("A3", seen.Handle);
            Near(P(0.3, 10.2), seen.Cursor);
            Assert.Equal(2.0, seen.Aperture, 12);
            Near(P(5, 5), seen.BasePoint.Value);
        }

        [Fact]
        public void PointsBeyondTheAperture_AreDropped()
        {
            CadDocument document = Drawing();
            PickIndex index = document.GetPickIndex(document.ModelSpace);
            CadCustomSnap far = At("Far", "FARR", P(0.0, 7.0), P(4.0, 10.0));
            CadCustomSnap near = At("Near", "NEAR", P(0.0, 7.0), P(1.2, 10.0));

            Assert.Equal(CadSnapKind.None, Snaps(document, CadSnapModes.None, far).Snap(index, P(0.0, 10.0), 2.0).Kind);

            CadSnapResult result = Snaps(document, CadSnapModes.None, near).Snap(index, P(0.0, 10.0), 2.0);
            Assert.Equal(CadSnapKind.Custom, result.Kind);
            Near(P(1.2, 10.0), result.Point);
        }

        [Fact]
        public void AFinderThatThrows_IsToldOnceSwitchedOffAndTheBuiltInSnapsStillAnswer()
        {
            CadDocument document = Drawing();
            PickIndex index = document.GetPickIndex(document.ModelSpace);
            int calls = 0;
            var snap = new CadCustomSnap("Bolt", "BOLT", request =>
            {
                calls++;
                throw new InvalidOperationException("no bolt");
            });
            ObjectSnap snaps = Snaps(document, CadSnapModes.Quadrant, snap);
            var failures = new List<string>();
            snaps.CustomFailed = (failed, exception) => failures.Add(failed.Keyword + ": " + exception.Message);

            CadSnapResult first = snaps.Snap(index, P(0.1, 9.9), 2.0);
            CadSnapResult second = snaps.Snap(index, P(0.1, 9.9), 2.0);

            Assert.Equal(CadSnapKind.Quadrant, first.Kind);
            Assert.Equal(CadSnapKind.Quadrant, second.Kind);
            Assert.Equal(1, calls);
            Assert.Equal(new[] { "BOLT: no bolt" }, failures);
        }

        [Fact]
        public void TwoCustomPointsAtTheSameDistance_GoToTheSnapLoadedFirstThenTheFirstPointGiven()
        {
            CadDocument document = Drawing();
            PickIndex index = document.GetPickIndex(document.ModelSpace);
            CadCustomSnap first = At("First", "FRST", P(1.0, 10.0), P(-1.0, 10.0));
            CadCustomSnap second = At("Second", "SCND", P(-1.0, 10.0));
            GeoPoint2 cursor = P(0.0, 10.0);

            CadSnapResult one = Snaps(document, CadSnapModes.None, second, first).Snap(index, cursor, 2.0);
            CadSnapResult two = Snaps(document, CadSnapModes.None, first, second).Snap(index, cursor, 2.0);

            Assert.Same(second, one.CustomSnap);
            Assert.Same(first, two.CustomSnap);
            Near(P(1.0, 10.0), two.Point);
        }

        [Fact]
        public void WithNoCustomSnap_TheBuiltInResultIsTheSame()
        {
            CadDocument document = Drawing();
            PickIndex index = document.GetPickIndex(document.ModelSpace);
            ObjectSnap plain = new ObjectSnap(Tight) { Modes = CadSnapModes.Quadrant };
            ObjectSnap withList = Snaps(document, CadSnapModes.Quadrant);

            CadSnapResult a = plain.Snap(index, P(0.1, 9.9), 2.0);
            CadSnapResult b = withList.Snap(index, P(0.1, 9.9), 2.0);

            Assert.Equal(a.Kind, b.Kind);
            Near(a.Point, b.Point, 0.0);
            Assert.Equal(a.Handle, b.Handle);
        }

        [Fact]
        public void ADefaultGlyph_IsASquareNineAcrossWithAFilledDot_AndAGivenGlyphIsKept()
        {
            CadCustomSnap plain = At("Plain", "PLAN");
            CadCustomSnap own = At("Own", "OWNN");
            own.Glyph = new[]
            {
                new CadMarkerStroke(new[] { P(-4.5, 4.5), P(4.5, 4.5), P(0.0, -4.5) }, true, filled: true),
            };

            IReadOnlyList<CadMarkerStroke> strokes = CadSnapMarker.StrokesOf(plain);

            Assert.Equal(2, strokes.Count);
            Assert.True(strokes[0].IsClosed);
            Assert.False(strokes[0].IsFilled);
            Assert.Equal(4, strokes[0].Points.Count);
            Assert.True(strokes[1].IsFilled);
            Assert.Same(own.Glyph, CadSnapMarker.StrokesOf(own));
        }

        [Fact]
        public void APluginSnapChoice_IsKeptInTheSettingsAndInACopyOfThem()
        {
            string path = Path.Combine(Path.GetTempPath(), "gh-plugin-snaps-" + Guid.NewGuid().ToString("N") + ".json");

            try
            {
                Assert.Empty(new GlobalSettings().PluginSnaps);
                var settings = new GlobalSettings
                {
                    PluginSnaps = new List<string> { "CTOP=1", "BOLT=0" },
                };
                GlobalSettings copy = settings.Clone();
                copy.PluginSnaps.Add("NUTT=1");
                settings.Save(path);

                GlobalSettings read = GlobalSettings.Load(path);

                Assert.Equal(new[] { "CTOP=1", "BOLT=0" }, read.PluginSnaps);
                Assert.Equal(new[] { "CTOP=1", "BOLT=0" }, settings.PluginSnaps);
                Assert.Equal(new[] { "CTOP=1", "BOLT=0", "NUTT=1" }, copy.PluginSnaps);

                settings.PluginSnaps = null;
                Assert.Empty(settings.PluginSnaps);
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
