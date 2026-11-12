using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// XCLIP and the references it clips, as AutoCAD 2021's (probes of accoreconsole): the options of "Enter clipping option",
    /// the boundary it keeps in the SPATIAL_FILTER of the reference's extension dictionary, the clip drawn and picked, and each
    /// command one undo step.
    /// </summary>
    public class XClipTests
    {
        /// <summary>
        /// A block GHB of a square 100 by 100 and a circle of radius 20, its origin (10, 20), inserted at (1000, 500); the
        /// reference is the first of model space.
        /// </summary>
        private static CadDocument WithReference(
            out CadCommandSession session,
            out string handle,
            double scale = 1.0,
            double degrees = 0.0
        )
        {
            var database = new DxfDatabase("AC1027");
            var block = new DxfBlockTableRecord { Name = "GHB", Origin = new GeoPoint3(10, 20, 0) };
            database.BlockTable.Add(block);
            var square = new DxfPolyline();
            square.AddVertexAt(0, new GeoPoint2(0, 0), 0, 0, 0);
            square.AddVertexAt(1, new GeoPoint2(100, 0), 0, 0, 0);
            square.AddVertexAt(2, new GeoPoint2(100, 100), 0, 0, 0);
            square.AddVertexAt(3, new GeoPoint2(0, 100), 0, 0, 0);
            square.Closed = true;
            block.AppendEntity(square);
            block.AppendEntity(new DxfCircle(new GeoPoint3(50, 50, 0), new GeoVector3(0, 0, 1), 20.0));
            var insert = new DxfBlockReference(new GeoPoint3(1000, 500, 0), block)
            {
                ScaleFactors = new GeoVector3(scale, scale, scale),
                Rotation = degrees * Math.PI / 180.0,
            };
            database.ModelSpace.AppendEntity(insert);
            handle = insert.Handle;
            CadDocument document = CadDocument.From(database, "xclip.dxf");
            session = Session(document);
            return document;
        }

        private static DxfBlockReference Reference(CadDocument document, string handle) =>
            Entity<DxfBlockReference>(document, handle);

        /// <summary>XCLIP on a reference, New boundary, Rectangular, the two corners.</summary>
        private static void ClipToRectangle(CadCommandSession session, string handle, GeoPoint2 a, GeoPoint2 b) =>
            Run(session, "XCLIP", new[] { handle }, Enter, "New boundary", "Rectangular", a, b);

        [Fact]
        public void ARectangle_ClipsTheReference_AsAutoCadKeepsIt_InOneUndoStep()
        {
            CadDocument document = WithReference(out CadCommandSession session, out string handle);
            string before = document.Database.ToText();

            ClipToRectangle(session, handle, P(1010, 510), P(1090, 590));

            DxfSpatialFilter filter = Reference(document, handle).SpatialFilter;
            Assert.NotNull(filter);
            Assert.Equal(2, filter.Points.Count);
            Assert.Equal(1010.0, filter.Points[0].X, 9);
            Assert.Equal(590.0, filter.Points[1].Y, 9);
            Assert.True(filter.IsEnabled);
            Assert.Equal("XCLIP", document.NextUndo.Name);
            Assert.False(session.IsRunning);
            AssertReopens(document);

            Assert.True(document.Undo());
            Assert.Equal(before, document.Database.ToText());
            Assert.True(document.Redo());
            Assert.NotNull(Reference(document, handle).SpatialFilter);
        }

        [Fact]
        public void EnterAtTheOptionsAndTheBoundary_TakesNewBoundaryAndRectangular()
        {
            CadDocument document = WithReference(out CadCommandSession session, out string handle);

            Run(session, "XCLIP", new[] { handle }, Enter, Enter, Enter, P(1010, 510), P(1090, 590));

            Assert.Equal(2, Reference(document, handle).SpatialFilter.Points.Count);
        }

        [Fact]
        public void TheBoundaryIsKeptInTheUserCoordinateSystemThatIsCurrent()
        {
            CadDocument document = WithReference(out CadCommandSession session, out string handle);
            var ucs = CadUcs.InPlane(P(26.4634224397553, 73.48256441343794), 20.0 * Math.PI / 180.0);
            document.Ucs.SetCurrent(ucs);

            // Two corners in the system's own coordinates: (10, 10) and (90, 90) of it.
            GeoPoint2 a = ucs.ToWorld(P(10, 10));
            GeoPoint2 b = ucs.ToWorld(P(90, 90));
            ClipToRectangle(session, handle, a, b);

            DxfSpatialFilter filter = Reference(document, handle).SpatialFilter;
            Assert.Equal(10.0, filter.Points[0].X, 6);
            Assert.Equal(10.0, filter.Points[0].Y, 6);
            Assert.Equal(90.0, filter.Points[1].X, 6);
            Assert.Equal(0.9396926, filter.WorldToClip[0], 6);
            Assert.Equal(0.3420201, filter.WorldToClip[1], 6);
            Assert.Equal(-50.0, filter.WorldToClip[3], 4);
        }

        [Fact]
        public void APolygon_IsKeptByItsPoints_AndEnterEndsIt()
        {
            CadDocument document = WithReference(out CadCommandSession session, out string handle);

            Run(
                session,
                "XCLIP",
                new[] { handle },
                Enter,
                "New boundary",
                "Polygonal",
                P(1010, 510),
                P(1090, 510),
                P(1050, 590),
                Enter
            );

            DxfSpatialFilter filter = Reference(document, handle).SpatialFilter;
            Assert.Equal(3, filter.Points.Count);
            Assert.Equal(1050.0, filter.Points[2].X, 9);
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void APolygonPointThatCrossesASide_OrRepeatsOne_IsRefused_AsAutoCadSays()
        {
            CadDocument document = WithReference(out CadCommandSession session, out string handle);

            Run(session, "XCLIP", new[] { handle }, Enter, "New boundary", "Polygonal", P(1010, 510), P(1090, 510));
            Run(session, P(1090, 510));
            Assert.Contains("Invalid point, polygon segment is zero length.", session.History);

            Run(session, P(1050, 590), P(1050, 490));
            Assert.Contains("Invalid point, polygon segments cannot intersect.", session.History);

            Run(session, Enter);
            Assert.Equal(3, Reference(document, handle).SpatialFilter.Points.Count);
        }

        [Fact]
        public void APolygonOfTwoPoints_GivesNoClip_AsAutoCadSays()
        {
            CadDocument document = WithReference(out CadCommandSession session, out string handle);
            string before = document.Database.ToText();

            Run(
                session,
                "XCLIP",
                new[] { handle },
                Enter,
                "New boundary",
                "Polygonal",
                P(1010, 510),
                P(1090, 510),
                Enter
            );

            Assert.Equal("Boundaries with zero area are not acceptable.", session.History.Last());
            Assert.Equal(before, document.Database.ToText());
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void APolylinePicked_IsTheBoundary_ByItsVertices_AndARectangleOfFourIsTwoCorners()
        {
            CadDocument document = WithReference(out CadCommandSession session, out string handle);
            Run(session, "REC", P(1010, 510), P(1090, 590));
            string rectangle = All<DxfPolyline>(document.ModelSpace).Last().Handle;
            Run(session, "CIRCLE", P(1050, 550), "10");
            string circle = All<DxfCircle>(document.ModelSpace).Last().Handle;

            Run(session, "XCLIP", new[] { handle }, Enter, "New boundary", "Select polyline", new[] { circle });
            Assert.Equal("Selected object is not a polyline.", session.History.Last());
            Run(session, new object[] { new[] { rectangle } });

            DxfSpatialFilter filter = Reference(document, handle).SpatialFilter;
            Assert.Equal(2, filter.Points.Count);
            Assert.Equal(1010.0, filter.Points[0].X, 9);
            Assert.Equal(590.0, filter.Points[1].Y, 9);
        }

        [Fact]
        public void ALineSelected_IsIneligible_AndNothingIsFound_AsAutoCadSays()
        {
            CadDocument document = WithReference(out CadCommandSession session, out string handle);
            Run(session, "LINE", P(0, 0), P(100, 0), Enter);
            string line = All<DxfLine>(document.ModelSpace).Last().Handle;

            Run(session, "XCLIP", new[] { line }, Enter);

            Assert.Contains("1 was ineligible for clipping.", session.History);
            Assert.Equal("None found.", session.History.Last());
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void ALineAmongTheReferences_IsLeftOut_AndTheReferencesAreClipped()
        {
            CadDocument document = WithReference(out CadCommandSession session, out string handle);
            Run(session, "LINE", P(0, 0), P(100, 0), Enter);
            string line = All<DxfLine>(document.ModelSpace).Last().Handle;

            Run(
                session,
                "XCLIP",
                new[] { handle, line },
                Enter,
                "New boundary",
                "Rectangular",
                P(1010, 510),
                P(1090, 590)
            );

            Assert.Contains("1 was ineligible for clipping.", session.History);
            Assert.NotNull(Reference(document, handle).SpatialFilter);
        }

        [Fact]
        public void InvertClip_KeepsTheKeyholeAutoCadKept_ForTheRectangleItWrote()
        {
            // AutoCAD 2021 on GHB at (1000, 500) scaled by 2 and turned by 30 degrees, Invert clip, the rectangle (1020, 520) to
            // (1100, 600): eleven points, the rectangle with a slit down from its lower right corner to the box 5 per cent
            // beyond the block's extents, round it and up again.
            CadDocument document = WithReference(out CadCommandSession session, out string handle, 2.0, 30.0);

            Run(session, "XCLIP", new[] { handle }, Enter, "New boundary", "Invert clip");
            Assert.Equal("Inside mode - Objects inside boundary will be hidden.", session.History.Last());
            Run(session, "Rectangular", P(1020, 520), P(1100, 600));

            IReadOnlyList<GeoPoint2> points = Reference(document, handle).SpatialFilter.Points;
            double[][] expected =
            {
                new[] { 1020.0, 520.0 },
                new[] { 1099.99996, 520.0 },
                new[] { 1099.99996, 441.698729810778 },
                new[] { 889.0192378864668, 441.698729810778 },
                new[] { 889.0192378864668, 742.2243186433545 },
                new[] { 1189.544826719043, 742.2243186433545 },
                new[] { 1189.544826719043, 441.698729810778 },
                new[] { 1100.0, 441.698729810778 },
                new[] { 1100.0, 520.0 },
                new[] { 1100.0, 600.0 },
                new[] { 1020.0, 600.0 },
            };
            Assert.Equal(expected.Length, points.Count);

            for (int i = 0; i < expected.Length; i++)
            {
                Assert.Equal(expected[i][0], points[i].X, 4);
                Assert.Equal(expected[i][1], points[i].Y, 4);
            }
        }

        [Fact]
        public void InvertClip_OfAPolygon_SlitsToTheNearestEdge_AndGoesRoundAgainstTheirTurn()
        {
            // AutoCAD 2021's keyhole of the clockwise quadrilateral (1030, 600), (1060, 640), (1100, 610), (1070, 550): the
            // slit runs east from (1100, 610), the nearest edge of the box, and round it counter-clockwise.
            CadDocument document = WithReference(out CadCommandSession session, out string handle, 2.0, 30.0);

            Run(
                session,
                "XCLIP",
                new[] { handle },
                Enter,
                "New boundary",
                "Invert clip",
                "Polygonal",
                P(1030, 600),
                P(1060, 640),
                P(1100, 610),
                P(1070, 550),
                Enter
            );

            IReadOnlyList<GeoPoint2> points = Reference(document, handle).SpatialFilter.Points;
            Assert.Equal(11, points.Count);
            Assert.Equal(1100.0, points[2].X, 9);
            Assert.Equal(1189.544826719043, points[3].X, 4);
            Assert.Equal(610.0, points[3].Y, 4);
            Assert.Equal(742.2243186433545, points[4].Y, 4);
            Assert.Equal(889.0192378864668, points[5].X, 4);
            Assert.Equal(441.698729810778, points[6].Y, 4);
            Assert.Equal(1189.544826719043, points[7].X, 4);
            Assert.Equal(1070.0, points[10].X, 9);
        }

        [Fact]
        public void InvertClipTwice_IsOutsideModeAgain()
        {
            CadDocument document = WithReference(out CadCommandSession session, out string handle);

            Run(session, "XCLIP", new[] { handle }, Enter, "New boundary", "Invert clip", "Invert clip");

            Assert.Equal("Outside mode - Objects outside boundary will be hidden.", session.History.Last());
        }

        [Fact]
        public void OnAndOff_SwitchTheClip_Delete_TakesItOff_AndNoneSaysSo()
        {
            CadDocument document = WithReference(out CadCommandSession session, out string handle);

            Run(session, "XCLIP", new[] { handle }, Enter, "ON");
            Assert.Equal("No clip boundary found.", session.History.Last());
            Run(session, "OFF");
            Assert.Equal("No clip boundary found.", session.History.Last());
            Run(session, "Delete");
            Assert.Equal("No clip boundary found.", session.History.Last());
            Run(session, Esc);

            ClipToRectangle(session, handle, P(1010, 510), P(1090, 590));

            Run(session, "XCLIP", new[] { handle }, Enter, "OFF");
            Assert.False(Reference(document, handle).SpatialFilter.IsEnabled);
            Assert.Equal("XCLIP", document.NextUndo.Name);
            Run(session, "XCLIP", new[] { handle }, Enter, "ON");
            Assert.True(Reference(document, handle).SpatialFilter.IsEnabled);

            Run(session, "XCLIP", new[] { handle }, Enter, "Delete");
            Assert.Null(Reference(document, handle).SpatialFilter);
            Assert.NotNull(Reference(document, handle).ExtensionDictionary);
            Assert.True(document.Undo());
            Assert.NotNull(Reference(document, handle).SpatialFilter);
        }

        [Fact]
        public void NewBoundaryOverAnOldOne_AsksToDeleteIt_AndNoLeavesItBe()
        {
            CadDocument document = WithReference(out CadCommandSession session, out string handle);
            ClipToRectangle(session, handle, P(1010, 510), P(1090, 590));

            Run(session, "XCLIP", new[] { handle }, Enter, "New boundary");
            Assert.StartsWith("Delete old boundary(s)?", session.PromptText);
            Run(session, "No");

            Assert.False(session.IsRunning);
            Assert.Equal(1010.0, Reference(document, handle).SpatialFilter.Points[0].X, 9);

            Run(
                session,
                "XCLIP",
                new[] { handle },
                Enter,
                "New boundary",
                "Yes",
                "Rectangular",
                P(1020, 520),
                P(1080, 580)
            );
            Assert.Equal(1020.0, Reference(document, handle).SpatialFilter.Points[0].X, 9);
            Assert.Single(Reference(document, handle).ExtensionDictionary);
        }

        [Fact]
        public void Clipdepth_SetsThePlanes_ByDistance_RefusesAFrontBehindTheBack_AndRemoves()
        {
            CadDocument document = WithReference(out CadCommandSession session, out string handle);
            Run(session, "XCLIP", new[] { handle }, Enter, "Clipdepth");
            Assert.Equal("Xref must contain a clip boundary before specifying a clipdepth.", session.History.Last());
            Run(session, Esc);
            ClipToRectangle(session, handle, P(1010, 510), P(1090, 590));

            // AutoCAD 2021: a front plane at 10 and a back at 30 is "Front clipping plane behind back clipping plane."
            Run(session, "XCLIP", new[] { handle }, Enter, "Clipdepth", "Distance", "10", "Distance", "30");
            Assert.Equal(
                "Front clipping plane behind back clipping plane.  Clipdepth not applied.",
                session.History.Last()
            );
            Assert.Null(Reference(document, handle).SpatialFilter.FrontClip);

            Run(session, "XCLIP", new[] { handle }, Enter, "Clipdepth", "Distance", "5", "Distance", "-5");
            Assert.Equal(5.0, Reference(document, handle).SpatialFilter.FrontClip);
            Assert.Equal(-5.0, Reference(document, handle).SpatialFilter.BackClip);

            Run(session, "XCLIP", new[] { handle }, Enter, "Clipdepth", "Remove", "Remove");
            Assert.Null(Reference(document, handle).SpatialFilter.FrontClip);
            Assert.Null(Reference(document, handle).SpatialFilter.BackClip);
        }

        [Fact]
        public void GeneratePolyline_DrawsTheBoundary_OnTheCurrentLayer_InOneStep()
        {
            CadDocument document = WithReference(out CadCommandSession session, out string handle);
            ClipToRectangle(session, handle, P(1010, 510), P(1090, 590));
            int before = All<DxfPolyline>(document.ModelSpace).Count;

            Run(session, "XCLIP", new[] { handle }, Enter, "generate Polyline");

            List<DxfPolyline> polylines = All<DxfPolyline>(document.ModelSpace);
            Assert.Equal(before + 1, polylines.Count);
            DxfPolyline made = polylines.Last();
            Assert.True(made.Closed);
            Assert.Equal(4, made.NumberOfVertices);
            Near(P(1010, 510), made.GetPoint2dAt(0));
            Near(P(1090, 510), made.GetPoint2dAt(1));
            Near(P(1090, 590), made.GetPoint2dAt(2));
            Near(P(1010, 590), made.GetPoint2dAt(3));
            Assert.Equal("XCLIP", document.NextUndo.Name);
            Assert.True(document.Undo());
            Assert.Equal(before, All<DxfPolyline>(document.ModelSpace).Count);
        }

        [Fact]
        public void XC_IsTheAliasOfXclip()
        {
            CadDocument document = WithReference(out CadCommandSession session, out string handle);

            Run(session, "XC", new[] { handle });

            Assert.True(session.IsRunning);
            Assert.Equal("XCLIP", session.Current.Name);
        }

        [Fact]
        public void TheClipOfAReference_GoesWithItWhenItIsMoved_AndIsCopiedWithIt()
        {
            CadDocument document = WithReference(out CadCommandSession session, out string handle);
            ClipToRectangle(session, handle, P(1000, 500), P(1040, 530));
            IReadOnlyList<GeoPoint2> ring = Reference(document, handle).ClipOutline;

            Run(session, "MOVE", new[] { handle }, Enter, P(0, 0), P(70, 90));
            Run(session, "COPY", new[] { handle }, Enter, P(0, 0), P(300, 0), Enter);

            List<DxfBlockReference> all = All<DxfBlockReference>(document.ModelSpace);
            Assert.Equal(2, all.Count);

            foreach (DxfBlockReference reference in all)
            {
                IReadOnlyList<GeoPoint2> moved = reference.ClipOutline;
                Assert.NotNull(moved);
                Assert.Equal(ring.Count, moved.Count);

                for (int i = 0; i < ring.Count; i++)
                {
                    Near(ring[i], moved[i]);
                }
            }
        }

        [Fact]
        public void TheCopyOfAClippedReference_HasItsOwnFilterObjects()
        {
            CadDocument document = WithReference(out CadCommandSession session, out string handle);
            ClipToRectangle(session, handle, P(1000, 500), P(1040, 530));

            Run(session, "COPY", new[] { handle }, Enter, P(0, 0), P(300, 0), Enter);

            List<DxfBlockReference> all = All<DxfBlockReference>(document.ModelSpace);
            Assert.NotEqual(all[0].SpatialFilter.Handle, all[1].SpatialFilter.Handle);
            Assert.NotEqual(all[0].ExtensionDictionary.Handle, all[1].ExtensionDictionary.Handle);
            AssertReopens(document);
        }

        [Fact]
        public void ADrawingWithAClippedReference_ReadsBackToTheSameClip()
        {
            CadDocument document = WithReference(out CadCommandSession session, out string handle);
            ClipToRectangle(session, handle, P(1000, 500), P(1040, 530));

            DxfDatabase again = DxfDatabase.Parse(document.Database.ToText());

            DxfBlockReference reference = again.ModelSpace.OfType<DxfBlockReference>().Single();
            Assert.Equal(4, reference.ClipOutline.Count);
        }
    }
}
