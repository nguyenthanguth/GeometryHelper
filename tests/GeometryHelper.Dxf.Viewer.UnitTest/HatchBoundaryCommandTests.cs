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
    /// The commands that work on the boundaries of a hatch made: HATCHGENERATEBOUNDARY, HATCHSETBOUNDARY and the typed
    /// -HATCHEDIT, as AutoCAD 2021 asks them (probed in its console: the prompts, the polyline and the CIRCLE of a hatch's two
    /// loops on the current layer, the loops replaced, "Hatch boundary associativity removed."), and what the Hatch Creation
    /// tab keeps running in HATCH: HPDLGMODE, islands off, separate hatches, retained boundaries.
    /// </summary>
    public sealed class HatchBoundaryCommandTests
    {
        /// <summary>Four lines round a rectangle from (x0, y0) to (x1, y1), in the order bottom, right, top, left.</summary>
        private static List<DxfLine> Rectangle(DxfDatabase database, double x0, double y0, double x1, double y1)
        {
            GeoPoint3[] corners =
            {
                new GeoPoint3(x0, y0, 0),
                new GeoPoint3(x1, y0, 0),
                new GeoPoint3(x1, y1, 0),
                new GeoPoint3(x0, y1, 0),
            };
            var lines = new List<DxfLine>();

            for (int i = 0; i < 4; i++)
            {
                var line = new DxfLine(corners[i], corners[(i + 1) % 4]);
                database.ModelSpace.AppendEntity(line);
                lines.Add(line);
            }

            return lines;
        }

        /// <summary>
        /// A drawing of R2013 with layers A and B (B current), a plate 100 by 60 of four lines with a circle of radius 10 in its
        /// middle, and a second rectangle 100 by 60 further along; <paramref name="hatched"/> hatches the plate from a point
        /// between the rectangle and the circle on layer A (two loops, associative), 6000 less the circle's area.
        /// </summary>
        private static CadDocument Plate(out DxfHatch hatch, out List<DxfLine> second, bool hatched = true)
        {
            var database = new DxfDatabase("AC1027");
            database.LayerTable.Add(new DxfLayerTableRecord { Name = "A" });
            database.LayerTable.Add(new DxfLayerTableRecord { Name = "B" });
            Rectangle(database, 0, 0, 100, 60);
            database.ModelSpace.AppendEntity(new DxfCircle(new GeoPoint3(50, 30, 0), new GeoVector3(0, 0, 1), 10));
            second = Rectangle(database, 200, 0, 300, 60);

            // A closed polyline 100 by 60 to the right of both, on layer 0, for HATCHSETBOUNDARY to take.
            var outline = new DxfPolyline();
            outline.AddVertexAt(0, new GeoPoint2(400, 0), 0.0, 0.0, 0.0);
            outline.AddVertexAt(1, new GeoPoint2(500, 0), 0.0, 0.0, 0.0);
            outline.AddVertexAt(2, new GeoPoint2(500, 60), 0.0, 0.0, 0.0);
            outline.AddVertexAt(3, new GeoPoint2(400, 60), 0.0, 0.0, 0.0);
            outline.Closed = true;
            database.ModelSpace.AppendEntity(outline);
            CadDocument document = CadDocument.From(database, "plate.dxf");
            document.HatchSettings.Layer = "A";
            hatch = null;

            if (hatched)
            {
                CadCommandSession session = Session(document);
                session.Tolerance = Tolerance.Default;
                Run(session, "HATCH", P(5, 5), Enter);
                hatch = Assert.Single(document.ModelSpace.Block.OfType<DxfHatch>());
            }

            database.Clayer = "B";
            return document;
        }

        private static CadCommandSession Fresh(CadDocument document)
        {
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            return session;
        }

        /// <summary>The polylines the commands made, on layer B; the closed polyline the plate has on layer 0 is not one.</summary>
        private static List<DxfPolyline> Made(CadDocument document) =>
            document.ModelSpace.Block.OfType<DxfPolyline>().Where(p => p.Layer == "B").ToList();

        private static List<DxfHatch> Hatches(CadDocument document) =>
            document.ModelSpace.Block.OfType<DxfHatch>().Where(h => !h.IsErased).ToList();

        #region HATCHGENERATEBOUNDARY

        [Fact]
        public void HatchGenerateBoundary_MakesAPolylineRoundTheOuterLoopAndACircleRoundTheIsland_OnTheCurrentLayer()
        {
            CadDocument document = Plate(out DxfHatch hatch, out _);
            CadCommandSession session = Fresh(document);
            Run(session, "HATCHGENERATEBOUNDARY");
            Assert.Equal("Select hatch objects:", session.PromptText);
            Run(session, new[] { hatch.Handle }, Enter);

            Assert.False(session.IsRunning);
            DxfPolyline polyline = Assert.Single(Made(document));
            Assert.True(polyline.Closed);
            Assert.Equal(4, polyline.NumberOfVertices);
            Assert.Equal("B", polyline.Layer);
            DxfCircle made = Assert.Single(document.ModelSpace.Block.OfType<DxfCircle>(), c => c.Layer == "B");
            Assert.Equal(10.0, made.Radius, 9);
            Assert.Equal(50.0, made.Center.X, 9);
            Assert.Equal(30.0, made.Center.Y, 9);

            // The hatch is left as it was and follows nothing new.
            Assert.Equal(2, hatch.NumberOfLoops);
            Assert.True(hatch.Associative);
            Assert.Equal("HATCHGENERATEBOUNDARY", document.NextUndo.Name);
            document.Undo();
            Assert.Empty(Made(document));
        }

        [Fact]
        public void HatchGenerateBoundary_TakesTheHatchSelectedBeforeIt_AndAsksNothing()
        {
            CadDocument document = Plate(out DxfHatch hatch, out _);
            document.Selection.Set(new[] { hatch.Handle });
            CadCommandSession session = Fresh(document);
            Run(session, "HATCHGENERATEBOUNDARY");

            Assert.False(session.IsRunning);
            Assert.Single(Made(document));
        }

        [Fact]
        public void HatchGenerateBoundary_LeavesOutWhatIsNotAHatch()
        {
            CadDocument document = Plate(out _, out List<DxfLine> second);
            CadCommandSession session = Fresh(document);
            Run(session, "HATCHGENERATEBOUNDARY", new[] { second[0].Handle }, Enter);

            Assert.False(session.IsRunning);
            Assert.Empty(Made(document));
        }

        #endregion

        #region HATCHSETBOUNDARY

        /// <summary>The closed polyline 100 by 60 on layer 0 that <see cref="Plate"/> draws.</summary>
        private static DxfPolyline Outline(CadDocument document) =>
            document.ModelSpace.Block.OfType<DxfPolyline>().Single(p => p.Layer == "0");

        [Fact]
        public void HatchSetBoundary_TakesTheOldLoopsOut_AndFollowsTheObjectsSelected()
        {
            CadDocument document = Plate(out DxfHatch hatch, out _);
            DxfPolyline outline = Outline(document);
            CadCommandSession session = Fresh(document);
            Run(session, "HATCHSETBOUNDARY");
            Assert.Equal("Select hatch object:", session.PromptText);
            Run(session, (object)new[] { hatch.Handle });
            Assert.Equal("Select objects to be used for the new boundary:", session.PromptText);
            Run(session, new[] { outline.Handle }, Enter);
            Assert.True(
                session.PromptText == "Erase selected linework? [Yes/No] <N>:",
                string.Join(" | ", session.History)
            );
            Run(session, "N");

            Assert.False(session.IsRunning);
            Assert.Equal(1, hatch.NumberOfLoops);
            Assert.Equal(6000.0, hatch.Area, 6);
            Assert.True(hatch.Associative);
            Assert.Same(outline, Assert.Single(hatch.GetAssociatedObjectsAt(0)));
            Assert.False(outline.IsErased);
            Assert.Equal("HATCHSETBOUNDARY", document.NextUndo.Name);

            document.Undo();
            Assert.Equal(2, Assert.Single(Hatches(document)).NumberOfLoops);
        }

        [Fact]
        public void HatchSetBoundary_AnsweredYes_ErasesTheObjects_AndTheHatchFollowsNothing()
        {
            CadDocument document = Plate(out DxfHatch hatch, out _);
            DxfPolyline outline = Outline(document);
            CadCommandSession session = Fresh(document);
            Run(session, "HATCHSETBOUNDARY", new[] { hatch.Handle }, new[] { outline.Handle }, Enter, "Y");

            Assert.True(outline.IsErased);
            Assert.Equal(1, hatch.NumberOfLoops);
            Assert.Equal(6000.0, hatch.Area, 6);
            Assert.False(hatch.Associative);
        }

        [Fact]
        public void HatchSetBoundary_OfObjectsThatAreNotClosed_SaysSo_AndChangesNothing()
        {
            CadDocument document = Plate(out DxfHatch hatch, out List<DxfLine> second);
            CadCommandSession session = Fresh(document);
            Run(session, "HATCHSETBOUNDARY", new[] { hatch.Handle }, new[] { second[0].Handle }, Enter);

            Assert.False(session.IsRunning);
            Assert.Contains("1 object is not closed and cannot be a hatch boundary.", session.History);
            Assert.Contains("Valid hatch boundary not found.", session.History);
            Assert.Equal(2, hatch.NumberOfLoops);
        }

        [Fact]
        public void HatchSetBoundary_OfAnObjectThatIsNoHatch_SaysSo()
        {
            CadDocument document = Plate(out _, out List<DxfLine> second);
            CadCommandSession session = Fresh(document);
            Run(session, "HATCHSETBOUNDARY", new[] { second[0].Handle });

            Assert.False(session.IsRunning);
            Assert.Contains("Selected object is not a hatch.", session.History);
        }

        #endregion

        #region -HATCHEDIT

        private static CadCommandSession Edit(CadDocument document, DxfHatch hatch, params object[] more)
        {
            CadCommandSession session = Fresh(document);
            Run(session, "-HATCHEDIT", new[] { hatch.Handle }, Enter);
            return Run(session, more);
        }

        [Fact]
        public void HatchEditTyped_AsksTheOptionsAutoCadAsks_AndTheOptionAloneIsTheProperties()
        {
            CadDocument document = Plate(out DxfHatch hatch, out _);
            CadCommandSession session = Fresh(document);
            Run(session, "-HATCHEDIT");
            Assert.Equal("Select hatch objects:", session.PromptText);
            Run(session, new[] { hatch.Handle }, Enter);

            Assert.Equal(
                "Enter hatch option [DIsassociate/Style/Properties/DRaw order/ADd boundaries/Remove boundaries/recreate Boundary/ASsociate/separate Hatches/Origin/ANnotative/hatch COlor/LAyer/Transparency] <Properties>:",
                session.PromptText
            );
            Run(session, Enter);
            Assert.Equal("Enter a pattern name or [?/Solid/User defined/Gradient] <ANSI31>:", session.PromptText);
        }

        [Fact]
        public void HatchEditTyped_Disassociate_SaysSo_AndTheHatchFollowsNothing()
        {
            CadDocument document = Plate(out DxfHatch hatch, out _);
            CadCommandSession session = Edit(document, hatch, "DI");

            Assert.False(session.IsRunning);
            Assert.Contains("Hatch boundary associativity removed.", session.History);
            Assert.False(hatch.Associative);
            Assert.Equal(2, hatch.NumberOfLoops);
            Assert.Equal("-HATCHEDIT", document.NextUndo.Name);
        }

        [Fact]
        public void HatchEditTyped_Style_ChangesTheIslandStyle()
        {
            CadDocument document = Plate(out DxfHatch hatch, out _);
            Edit(document, hatch, "S", "Ignore");

            Assert.Equal(DxfHatchStyle.Ignore, hatch.HatchStyle);
        }

        [Fact]
        public void HatchEditTyped_Properties_LaysAnotherPatternOnTheHatch_KeepingItsOrigin()
        {
            CadDocument document = Plate(out DxfHatch hatch, out _);
            CadHatchOrigin.MoveTo(hatch, new GeoPoint2(7, 3));
            Edit(document, hatch, "P", "ANSI32", "2", "30");

            Assert.Equal("ANSI32", hatch.PatternName);
            Assert.Equal(2.0, hatch.PatternScale, 9);
            Assert.Equal(30.0, hatch.PatternAngle * 180.0 / Math.PI, 9);
            Assert.Equal(7.0, CadHatchOrigin.Of(hatch).X, 6);
            Assert.Equal(3.0, CadHatchOrigin.Of(hatch).Y, 6);
            Assert.True(hatch.Associative);
        }

        [Fact]
        public void HatchEditTyped_RecreateBoundary_MakesPolylinesOnTheCurrentLayer_AndReassociatesOnYes()
        {
            CadDocument document = Plate(out DxfHatch hatch, out _);
            CadCommandSession session = Edit(document, hatch, "B");
            Assert.Equal("Enter type of boundary object [Region/Polyline] <Polyline>:", session.PromptText);
            Run(session, Enter);
            Assert.Equal("Reassociate hatch with new boundary? [Yes/No] <N>:", session.PromptText);
            Run(session, "Y");

            Assert.False(session.IsRunning);
            List<DxfPolyline> made = Made(document).ToList();

            // One round the plate and one, of three arcs, round the circle that was its island (HATCH cut it where it was found).
            Assert.Equal(2, made.Count);
            Assert.All(made, p => Assert.Equal("B", p.Layer));
            Assert.All(made, p => Assert.True(p.Closed));
            Assert.Equal(new[] { 3, 4 }, made.Select(p => p.NumberOfVertices).OrderBy(n => n).ToArray());
            Assert.True(hatch.Associative);
            Assert.Same(made[0], Assert.Single(hatch.GetAssociatedObjectsAt(0)));
            Assert.Same(made[1], Assert.Single(hatch.GetAssociatedObjectsAt(1)));
        }

        [Fact]
        public void HatchEditTyped_RecreateBoundary_AnsweredNo_LeavesTheHatchFollowingWhatItFollowed()
        {
            CadDocument document = Plate(out DxfHatch hatch, out _);
            Edit(document, hatch, "B", "P", "N");

            Assert.Equal(2, Made(document).Count());
            Assert.True(hatch.Associative);
            Assert.All(hatch.GetAssociatedObjects(), e => Assert.IsNotType<DxfPolyline>(e));
        }

        [Fact]
        public void HatchEditTyped_RecreateBoundary_OfTypeRegion_MakesRegions()
        {
            CadDocument document = Plate(out DxfHatch hatch, out _);
            CadCommandSession session = Edit(document, hatch, "B", "R", "N");

            Assert.Equal(2, document.ModelSpace.Block.OfType<DxfUnknownEntity>().Count(e => e.DxfName == "REGION"));
            Assert.Empty(Made(document));
            Assert.Contains("Created 2 regions.", session.History);
        }

        [Fact]
        public void HatchEditTyped_AddBoundaries_TakesAnotherAreaIn_AndTheHatchFollowsItsObjectsToo()
        {
            CadDocument document = Plate(out DxfHatch hatch, out List<DxfLine> second);
            CadCommandSession session = Edit(document, hatch, "AD");
            Assert.Equal("Specify internal point or [Select objects]:", session.PromptText);
            Run(session, P(205, 5), Enter);

            Assert.False(session.IsRunning);
            Assert.Equal(3, hatch.NumberOfLoops);
            Assert.True(hatch.Associative);
            Assert.Equal(6000.0 - Math.PI * 100.0 + 6000.0, hatch.Area, 3);
            Assert.Equal(4, hatch.GetAssociatedObjectsAt(2).Count);
            Assert.Contains(second[0], hatch.GetAssociatedObjectsAt(2));
        }

        [Fact]
        public void HatchEditTyped_RemoveBoundaries_TakesTheLoopOfTheObjectSelectedOut()
        {
            CadDocument document = Plate(out DxfHatch hatch, out List<DxfLine> second, false);
            CadCommandSession first = Fresh(document);
            Run(first, "HATCH", P(5, 5), P(205, 5), Enter);
            hatch = Assert.Single(Hatches(document));
            Assert.Equal(3, hatch.NumberOfLoops);

            CadCommandSession session = Edit(document, hatch, "R", new[] { second[0].Handle }, Enter);

            Assert.False(session.IsRunning);
            Assert.Equal(2, hatch.NumberOfLoops);
            Assert.True(hatch.Associative);
            Assert.DoesNotContain(second[0], hatch.GetAssociatedObjects());
        }

        [Fact]
        public void HatchEditTyped_RemoveBoundaries_OfAnOuterLoop_TakesItsIslandsWithIt()
        {
            CadDocument document = Plate(out DxfHatch hatch, out _);
            DxfLine bottom = document.ModelSpace.Block.OfType<DxfLine>().First();
            CadCommandSession session = Edit(document, hatch, "R", new[] { bottom.Handle }, Enter);

            Assert.Equal(0, hatch.NumberOfLoops);
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void HatchEditTyped_Associate_FollowsTheObjectsThatMakeTheAreaFound()
        {
            CadDocument document = Plate(out DxfHatch hatch, out _);
            Edit(document, hatch, "DI");
            Assert.False(hatch.Associative);

            CadCommandSession session = Edit(document, hatch, "AS", P(5, 5), Enter);

            Assert.False(session.IsRunning);
            Assert.True(hatch.Associative);
            Assert.Equal(4, hatch.GetAssociatedObjectsAt(0).Count);
        }

        [Fact]
        public void HatchEditTyped_Associate_OfAnAreaThatIsNotTheHatchs_SaysSo()
        {
            CadDocument document = Plate(out DxfHatch hatch, out _);
            Edit(document, hatch, "DI");
            CadCommandSession session = Edit(document, hatch, "AS", P(205, 5), Enter);

            Assert.Contains("Valid hatch boundary not found.", session.History);
            Assert.False(hatch.Associative);
        }

        [Fact]
        public void HatchEditTyped_SeparateHatches_MakesOneHatchOfEachArea()
        {
            CadDocument document = Plate(out DxfHatch hatch, out _, false);
            Run(Fresh(document), "HATCH", P(5, 5), P(205, 5), Enter);
            hatch = Assert.Single(Hatches(document));
            Edit(document, hatch, "H");

            List<DxfHatch> all = Hatches(document);
            Assert.Equal(2, all.Count);
            Assert.Equal(new[] { 1, 2 }, all.Select(h => h.NumberOfLoops).OrderBy(n => n).ToArray());
            Assert.All(all, h => Assert.True(h.Associative));
        }

        [Fact]
        public void HatchEditTyped_Origin_SetNewOrigin_LaysThePatternFromThePoint()
        {
            CadDocument document = Plate(out DxfHatch hatch, out _);
            Edit(document, hatch, "O", "Set", P(10, 5));

            Assert.Equal(10.0, CadHatchOrigin.Of(hatch).X, 6);
            Assert.Equal(5.0, CadHatchOrigin.Of(hatch).Y, 6);
        }

        [Fact]
        public void HatchEditTyped_Origin_DefaultToBoundaryExtents_TakesTheCornerOfTheBoxRoundTheOuterLoop()
        {
            CadDocument document = Plate(out DxfHatch hatch, out _);
            Edit(document, hatch, "O", "Default", "bottom R");

            Assert.Equal(100.0, CadHatchOrigin.Of(hatch).X, 6);
            Assert.Equal(0.0, CadHatchOrigin.Of(hatch).Y, 6);
        }

        [Fact]
        public void HatchEditTyped_ColorLayerAndTransparency_ChangeTheHatchAlone_NotTheNextHatchsSettings()
        {
            CadDocument document = Plate(out DxfHatch hatch, out _);
            CadHatchSettings next = document.HatchSettings;
            Edit(document, hatch, "CO", "1", ".");
            Assert.Equal(1, hatch.Color.ColorIndex);
            Assert.Same(next, document.HatchSettings);
            Assert.Null(next.Color);

            Edit(document, hatch, "LA", "B");
            Assert.Equal("B", hatch.Layer);

            Edit(document, hatch, "T", "50");
            Assert.Equal(127, hatch.Transparency.Alpha);
            Assert.Equal("A", document.HatchSettings.Layer);
        }

        [Fact]
        public void HatchEditTyped_DrawOrder_SendsTheHatchToTheBack()
        {
            CadDocument document = Plate(out DxfHatch hatch, out _);
            Edit(document, hatch, "DR", "send to Back");

            Assert.Same(hatch, document.ModelSpace.Block.DrawOrderTable.GetFullDrawOrder().First());
        }

        [Fact]
        public void HatchEditTyped_Annotative_MakesTheHatchAnnotative()
        {
            CadDocument document = Plate(out DxfHatch hatch, out _);
            Edit(document, hatch, "AN", "Y");

            Assert.Equal(DxfAnnotativeStates.True, hatch.Annotative);
        }

        [Fact]
        public void HatchEditTyped_OnEveryHatchSelected_ChangesThemAll()
        {
            CadDocument document = Plate(out DxfHatch first, out _);
            Run(Fresh(document), "HATCH", P(205, 5), Enter);
            DxfHatch other = Hatches(document).Single(h => h != first);
            CadCommandSession session = Fresh(document);
            Run(session, "-HATCHEDIT", new[] { first.Handle, other.Handle }, Enter, "S", "Outer");

            Assert.Equal(DxfHatchStyle.Outer, first.HatchStyle);
            Assert.Equal(DxfHatchStyle.Outer, other.HatchStyle);
            Assert.Equal("-HATCHEDIT", document.NextUndo.Name);
        }

        #endregion

        #region HATCH, as the Hatch Creation tab keeps it running

        [Fact]
        public void Hpdlgmode_StartsAsAutoCadStartsIt_AndAsksTheHatchDialogFirstOnlyAtOne()
        {
            CadDocument document = Plate(out _, out _, false);
            CadCommandSession session = Fresh(document);
            var asked = new List<string>();
            session.UiRequested += (sender, e) =>
            {
                asked.Add(e.Name);
                e.Handled = true;
            };

            Run(session, "HPDLGMODE");
            Assert.Equal("Enter new value for HPDLGMODE <2>:", session.PromptText);
            Run(session, Esc);

            Run(session, "HATCH");
            Assert.Empty(asked);
            Run(session, Esc);

            Run(session, "HPDLGMODE", "1", "HATCH");
            Assert.Equal(new[] { "HATCHSETTINGS" }, asked);
            Assert.Equal("Pick internal point or [Select objects/Undo/seTtings]:", session.PromptText);
        }

        [Fact]
        public void Hpdlgmode_AtOneWithNoWindow_AsksNothingAtTheStart()
        {
            CadDocument document = Plate(out _, out _, false);
            CadCommandSession session = Fresh(document);
            Run(session, "HPDLGMODE", "1", "HATCH");

            Assert.Equal("Pick internal point or [Select objects/Undo/seTtings]:", session.PromptText);
        }

        [Theory]
        [InlineData("3")]
        [InlineData("-1")]
        public void Hpdlgmode_RefusesWhatIsNotNoughtOneOrTwo(string value)
        {
            CadDocument document = Plate(out _, out _, false);
            CadCommandSession session = Fresh(document);
            Run(session, "HPDLGMODE", value);

            Assert.Contains("Requires an integer between 0 and 2.", session.History);
        }

        [Fact]
        public void Hatch_SelectObjects_GoesBackToThePointPrompt_AndEnterMakesTheHatch()
        {
            CadDocument document = Plate(out _, out List<DxfLine> second, false);
            DxfPolyline outline = Outline(document);
            CadCommandSession session = Fresh(document);
            Run(session, "HATCH", "S", new[] { outline.Handle }, Enter);

            Assert.True(session.IsRunning);
            Assert.Equal("Pick internal point or [Select objects/Undo/seTtings]:", session.PromptText);
            Run(session, P(205, 5), Enter);

            Assert.False(session.IsRunning);
            Assert.Equal(2, Assert.Single(Hatches(document)).NumberOfLoops);
            Assert.NotEmpty(second);
        }

        [Fact]
        public void Hatch_RemoveBoundaries_IsTakenWhereItIsNotShown_AndTakesTheAreaOutOfTheHatch()
        {
            CadDocument document = Plate(out _, out List<DxfLine> second, false);
            CadCommandSession session = Fresh(document);
            Run(session, "HATCH", P(5, 5), P(205, 5), "remove Boundaries");
            Assert.Equal("Select objects or [Add boundaries]:", session.PromptText);
            Run(session, new[] { second[0].Handle }, Enter);
            Assert.Equal("Select objects or [Add boundaries/Undo]:", session.PromptText);
            Run(session, Enter);
            Assert.Equal("Pick internal point or [Select objects/Undo/seTtings]:", session.PromptText);
            Run(session, Enter);

            Assert.Equal(2, Assert.Single(Hatches(document)).NumberOfLoops);
        }

        [Fact]
        public void Hatch_WithIslandDetectionOff_KeepsTheOuterLoopAlone()
        {
            CadDocument document = Plate(out _, out _, false);
            CadCommandSession session = Fresh(document);
            session.Remembered["hatch.islands"] = 0.0;
            Run(session, "HATCH", P(5, 5), Enter);

            Assert.Equal(1, Assert.Single(Hatches(document)).NumberOfLoops);
        }

        [Fact]
        public void Hatch_WithSeparateHatchesOn_MakesOneHatchOfEachArea()
        {
            CadDocument document = Plate(out _, out _, false);
            CadCommandSession session = Fresh(document);
            session.Remembered["hatch.separate"] = 1.0;
            Run(session, "HATCH", P(5, 5), P(205, 5), Enter);

            Assert.Equal(2, Hatches(document).Count);
        }

        [Fact]
        public void Hatch_WithHpboundretain_KeepsAPolylineOfEachBoundaryFound()
        {
            CadDocument document = Plate(out _, out _, false);
            CadCommandSession session = Fresh(document);
            Run(session, "HPBOUNDRETAIN", "1", "HATCH", P(5, 5), Enter);

            Assert.Equal(2, Made(document).Count());
            Assert.Equal(1, session.Remembered["hatch.retain"]);
        }

        [Fact]
        public void Hatch_WithADrawOrderOfBackOrFront_PutsTheHatchThere()
        {
            CadDocument document = Plate(out _, out _, false);
            CadCommandSession session = Fresh(document);
            session.Remembered["hatch.order"] = 2.0;
            Run(session, "HATCH", P(5, 5), Enter);

            DxfHatch hatch = Assert.Single(Hatches(document));
            Assert.Same(hatch, document.ModelSpace.Block.DrawOrderTable.GetFullDrawOrder().Last());
        }

        #endregion
    }
}
