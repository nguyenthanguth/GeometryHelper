using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// The snaps AutoCAD has beyond those near the cursor (C16): Extension, Parallel, Apparent Intersection, Insertion and
    /// object snap tracking from points acquired by resting the cursor, with AutoCAD's OSMODE values.
    /// </summary>
    public class TrackingSnapTests
    {
        private static PickIndex Index(params string[] entities)
        {
            CadDocument document = CadDocument.From(TestDrawings.Drawing(null, null, entities), "tracking.dxf");
            return document.GetPickIndex(document.ModelSpace);
        }

        private static void Snapped(CadSnapResult result, CadSnapKind kind, double x, double y)
        {
            Assert.Equal(kind, result.Kind);
            Near(P(x, y), result.Point, 1E-9);
        }

        [Fact]
        public void TheSnapModesCarryAutoCADsOsmodeValues()
        {
            Assert.Equal(1, (int)CadSnapModes.Endpoint);
            Assert.Equal(2, (int)CadSnapModes.Midpoint);
            Assert.Equal(4, (int)CadSnapModes.Center);
            Assert.Equal(8, (int)CadSnapModes.Node);
            Assert.Equal(16, (int)CadSnapModes.Quadrant);
            Assert.Equal(32, (int)CadSnapModes.Intersection);
            Assert.Equal(64, (int)CadSnapModes.Insertion);
            Assert.Equal(128, (int)CadSnapModes.Perpendicular);
            Assert.Equal(256, (int)CadSnapModes.Tangent);
            Assert.Equal(512, (int)CadSnapModes.Nearest);
            Assert.Equal(1024, (int)CadSnapModes.GeometricCenter);
            Assert.Equal(2048, (int)CadSnapModes.ApparentIntersection);
            Assert.Equal(4096, (int)CadSnapModes.Extension);
            Assert.Equal(8192, (int)CadSnapModes.Parallel);
            Assert.Equal(
                CadSnapModes.Endpoint | CadSnapModes.Center | CadSnapModes.Intersection | CadSnapModes.Extension,
                (CadSnapModes)4133
            );
        }

        [Fact]
        public void ExtensionFollowsALineBeyondTheEndTheCursorRestedOn_AndARestThereAgainLetsItGo()
        {
            PickIndex index = Index(TestDrawings.Line(0, 0, 100, 0, handle: "A1"));
            var snap = new ObjectSnap(Tight) { Modes = CadSnapModes.Extension };

            Assert.Equal(CadSnapKind.None, snap.Snap(index, P(150, 0.4), 1.0).Kind);
            Assert.True(snap.Acquire(index, P(99.7, 0.3), 1.0));
            CadAcquiredPoint end = Assert.Single(snap.Acquired);
            Assert.Equal(CadSnapKind.Extension, end.Kind);
            Near(P(100, 0), end.Point, 1E-9);

            CadSnapResult along = snap.Snap(index, P(150, 0.4), 1.0);
            Snapped(along, CadSnapKind.Extension, 150, 0);
            Assert.Equal("A1", along.Handle);
            IReadOnlyList<GeoPoint2> track = Assert.Single(along.Tracks);
            Near(P(100, 0), track[0], 1E-9);
            Near(P(150, 0), track[track.Count - 1], 1E-9);

            // The run goes on beyond the end rested on only, not back past the other end.
            Assert.Equal(CadSnapKind.None, snap.Snap(index, P(-30, 0.4), 1.0).Kind);

            Assert.True(snap.Acquire(index, P(100.2, -0.2), 1.0));
            Assert.Empty(snap.Acquired);
            Assert.Equal(CadSnapKind.None, snap.Snap(index, P(150, 0.4), 1.0).Kind);
        }

        [Fact]
        public void ExtensionOfAnArcRunsRoundItsCircle_OffTheArcOnly()
        {
            // A quarter of radius 10 about the origin, 0 to 90 degrees; its end (0, 10) rested on.
            PickIndex index = Index(TestDrawings.Arc(0, 0, 10, 0, 90, handle: "A4"));
            var snap = new ObjectSnap(Tight) { Modes = CadSnapModes.Extension };
            Assert.True(snap.Acquire(index, P(0.2, 9.9), 1.0));

            double c = Math.Cos(0.75 * Math.PI);
            double s = Math.Sin(0.75 * Math.PI);
            CadSnapResult round = snap.Snap(index, P(10.3 * c, 10.3 * s), 1.0);

            Snapped(round, CadSnapKind.Extension, 10 * c, 10 * s);
            IReadOnlyList<GeoPoint2> track = Assert.Single(round.Tracks);
            Near(P(0, 10), track[0], 1E-9);
            Assert.True(track.Count >= 10);
            Assert.All(track, p => Assert.Equal(10.0, Math.Sqrt(p.X * p.X + p.Y * p.Y), 9));
            Assert.Equal(
                CadSnapKind.None,
                snap.Snap(index, P(10.3 * Math.Cos(0.25 * Math.PI), 10.3 * Math.Sin(0.25 * Math.PI)), 1.0).Kind
            );
        }

        [Fact]
        public void ParallelHoldsTheRubberBandAlongALineRestedOn_OnlyWithABasePoint()
        {
            PickIndex index = Index(TestDrawings.Line(0, 0, 100, 50, handle: "A1"));
            var snap = new ObjectSnap(Tight) { Modes = CadSnapModes.Parallel };
            GeoPoint2 from = P(200, 0);

            Assert.False(snap.Acquire(index, P(40, 20.3), 1.0));
            Assert.True(snap.Acquire(index, P(40, 20.3), 1.0, from));
            Assert.Equal(CadSnapKind.Parallel, Assert.Single(snap.Acquired).Kind);

            // The cursor at (260, 30.6) lies 0.537 off the line through (200, 0) along (2, 1): its foot is (260.24, 30.12).
            CadSnapResult parallel = snap.Snap(index, P(260, 30.6), 1.0, from);

            Snapped(parallel, CadSnapKind.Parallel, 260.24, 30.12);
            Near(from, Assert.Single(parallel.Tracks)[0], 1E-9);
            Assert.Equal(CadSnapKind.None, snap.Snap(index, P(260, 40), 1.0, from).Kind);
        }

        [Fact]
        public void TwoLinesThatStopShortOfACorner_GiveTheirApparentIntersection_NotAnIntersection()
        {
            PickIndex index = Index(
                TestDrawings.Line(0, 0, 98, 0, handle: "A1"),
                TestDrawings.Line(100, 2, 100, 50, handle: "A2")
            );

            CadSnapResult apparent = new ObjectSnap(Tight) { Modes = CadSnapModes.ApparentIntersection }.Snap(
                index,
                P(99.5, 0.6),
                2.5
            );
            CadSnapResult intersection = new ObjectSnap(Tight) { Modes = CadSnapModes.Intersection }.Snap(
                index,
                P(99.5, 0.6),
                2.5
            );

            Snapped(apparent, CadSnapKind.ApparentIntersection, 100, 0);
            Assert.Equal("A1", apparent.Handle);
            Assert.Equal(CadSnapKind.None, intersection.Kind);
        }

        [Fact]
        public void AnObjectClickedFirst_ThenAnotherUnderTheCursor_GiveWhereTheyWouldCross()
        {
            PickIndex index = Index(
                TestDrawings.Line(0, 0, 10, 0, handle: "A1"),
                TestDrawings.Line(20, 5, 20, 10, handle: "A2")
            );
            var snap = new ObjectSnap(Tight) { Modes = CadSnapModes.ApparentIntersection };

            CadSnapResult offered = snap.Snap(index, P(5, 0.3), 1.0);
            Snapped(offered, CadSnapKind.ExtendedApparentIntersection, 5, 0);
            Assert.Equal("A1", offered.Handle);
            Assert.False(snap.TakeFirstObject(snap.Snap(index, P(50, 50), 1.0)));
            Assert.True(snap.TakeFirstObject(offered));

            CadSnapResult crossing = snap.Snap(index, P(20.3, 7), 1.0);
            Snapped(crossing, CadSnapKind.ApparentIntersection, 20, 0);
            Assert.Equal("A2", crossing.Handle);

            snap.ClearAcquired();
            Snapped(snap.Snap(index, P(20.3, 7), 1.0), CadSnapKind.ExtendedApparentIntersection, 20, 7);
        }

        [Fact]
        public void TheInsertionPointOfABlockIsTakenFromAnyOfItsLines_AndOfAnAttributeAndATextNearThem()
        {
            // A frame 100 by 30 inserted at (20, 20) with the value BEAM B-12 of its attribute at (25, 38); a text NOTE at
            // (200, 0); a line L1, which has no insertion point.
            string block = TestDrawings.Block(
                "TB",
                TestDrawings.Line(0, 0, 100, 0),
                TestDrawings.Line(100, 0, 100, 30),
                TestDrawings.Entity(
                    "ATTDEF",
                    "0",
                    256,
                    null,
                    "10",
                    5.0,
                    "20",
                    18.0,
                    "30",
                    0.0,
                    "40",
                    5.0,
                    "1",
                    "TITLE?",
                    "3",
                    "Title",
                    "2",
                    "TITLE",
                    "70",
                    0
                )
            );
            string insert =
                "0\nINSERT\n5\nB1\n8\n0\n66\n1\n2\nTB\n10\n20\n20\n20\n30\n0\n41\n1\n42\n1\n43\n1\n"
                + TestDrawings.Entity(
                    "ATTRIB",
                    "0",
                    256,
                    "B2",
                    "10",
                    25.0,
                    "20",
                    38.0,
                    "30",
                    0.0,
                    "40",
                    5.0,
                    "1",
                    "BEAM B-12",
                    "2",
                    "TITLE",
                    "70",
                    0
                )
                + "0\nSEQEND\n5\nB5\n8\n0\n";
            CadDocument document = CadDocument.From(
                TestDrawings.Drawing(
                    null,
                    block,
                    insert,
                    TestDrawings.Text(200, 0, 2.5, "NOTE", handle: "T1"),
                    TestDrawings.Line(300, 0, 400, 0, handle: "L1")
                ),
                "insertion.dxf"
            );
            PickIndex index = document.GetPickIndex(document.ModelSpace);
            var snap = new ObjectSnap(Tight) { Modes = CadSnapModes.Insertion };

            CadSnapResult fromFrame = snap.Snap(index, P(120.3, 35), 1.0);
            Snapped(fromFrame, CadSnapKind.Insertion, 20, 20);
            Assert.Equal("B1", fromFrame.Handle);
            Snapped(snap.Snap(index, P(25.3, 38.2), 1.0), CadSnapKind.Insertion, 25, 38);
            Snapped(snap.Snap(index, P(200.4, 0.3), 1.0), CadSnapKind.Insertion, 200, 0);
            Assert.Equal(CadSnapKind.None, snap.Snap(index, P(350, 0.2), 1.0).Kind);
        }

        [Fact]
        public void TrackingFromAcquiredEndpointsHoldsTheCursorLevelOrUpright_AndWhereTheirLinesCross()
        {
            PickIndex index = Index(
                TestDrawings.Line(0, 0, 100, 0, handle: "L1"),
                TestDrawings.Line(200, 50, 200, 100, handle: "L2")
            );
            var snap = new ObjectSnap(Tight) { Modes = CadSnapModes.Endpoint, Tracking = true };

            Assert.True(snap.Acquire(index, P(100.3, 0.2), 1.0));
            Assert.True(snap.Acquire(index, P(200.2, 50.3), 1.0));
            Assert.Equal(new[] { CadSnapKind.Endpoint, CadSnapKind.Endpoint }, snap.Acquired.Select(a => a.Kind));

            Snapped(snap.Snap(index, P(150, 0.5), 1.0), CadSnapKind.Tracking, 150, 0);
            Snapped(snap.Snap(index, P(100.4, 30), 1.0), CadSnapKind.Tracking, 100, 30);
            CadSnapResult crossing = snap.Snap(index, P(100.3, 49.6), 1.0);
            Snapped(crossing, CadSnapKind.Tracking, 100, 50);
            Assert.Equal(2, crossing.Tracks.Count);

            snap.Tracking = false;
            Assert.Equal(CadSnapKind.None, snap.Snap(index, P(150, 0.5), 1.0).Kind);
        }

        [Fact]
        public void TrackingWithPolarOn_FollowsEveryPolarAngle_OnlyWithPolarmode2_AsAutoCad()
        {
            PickIndex index = Index(TestDrawings.Line(0, 0, 100, 0, handle: "L1"));
            var snap = new ObjectSnap(Tight) { Modes = CadSnapModes.Endpoint, Tracking = true };
            Assert.True(snap.Acquire(index, P(100.3, 0.2), 1.0));

            Assert.Equal(CadSnapKind.None, snap.Snap(index, P(130, 30.4), 1.0).Kind);
            snap.Polar = true;
            snap.PolarAngle = Math.PI / 4.0;

            // POLARMODE 0, AutoCAD's default, tracks orthogonally only; 2 tracks on all the polar angles.
            Assert.Equal(CadSnapKind.None, snap.Snap(index, P(130, 30.4), 1.0).Kind);
            snap.TracksPolarAngles = true;
            Snapped(snap.Snap(index, P(130, 30.4), 1.0), CadSnapKind.Tracking, 130.2, 30.2);
        }

        [Fact]
        public void SevenPointsAreAcquiredAtMost_TheOldestLetGoFirst()
        {
            var lines = Enumerable
                .Range(0, 8)
                .Select(i => TestDrawings.Line(i * 50, 0, i * 50 + 10, 20, handle: "L" + i))
                .ToArray();
            PickIndex index = Index(lines);
            var snap = new ObjectSnap(Tight) { Modes = CadSnapModes.Endpoint, Tracking = true };

            for (int i = 0; i < 8; i++)
            {
                Assert.True(snap.Acquire(index, P(i * 50 + 0.2, 0.2), 1.0));
            }

            Assert.Equal(7, snap.Acquired.Count);
            Assert.Equal("L1", snap.Acquired[0].Handle);
            Assert.Equal("L7", snap.Acquired[6].Handle);
        }

        [Fact]
        public void NothingIsAcquiredWithoutTrackingExtensionOrParallel_AndASnapNearTheCursorStillWins()
        {
            PickIndex index = Index(
                TestDrawings.Line(0, 0, 100, 0, handle: "L1"),
                TestDrawings.Line(150, 0, 150, 40, handle: "L2")
            );
            var snap = new ObjectSnap(Tight) { Modes = CadSnapModes.Endpoint };

            Assert.False(snap.Acquire(index, P(100.3, 0.2), 1.0));

            snap.Modes = CadSnapModes.Endpoint | CadSnapModes.Extension;
            Assert.True(snap.Acquire(index, P(100.3, 0.2), 1.0));

            // The end of L2 lies on the run of L1: the endpoint, near the cursor, wins over the extension.
            Snapped(snap.Snap(index, P(150.3, 0.4), 1.0), CadSnapKind.Endpoint, 150, 0);
        }

        [Fact]
        public void TheNewKindsHaveAutoCADsNames()
        {
            Assert.Equal(
                "Extended Apparent Intersection",
                CadSnapMarker.NameOf(CadSnapKind.ExtendedApparentIntersection)
            );
            Assert.Equal("Object Snap Tracking", CadSnapMarker.NameOf(CadSnapKind.Tracking));
            Assert.Equal(6, CadSnapMarker.Strokes(CadSnapKind.ExtendedApparentIntersection).Count);
            Assert.Equal(2, CadSnapMarker.Strokes(CadSnapKind.Tracking).Count);
        }
    }
}
