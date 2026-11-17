using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The Properties of a parameter or an action picked in the Block Editor, and the rest of the authoring commands' prompts:
    /// the rows of each kind, a row set as one undo step with the graph written in place, the refusals in AutoCAD's words, the
    /// requests BTESTBLOCK, BAUTHORPALETTE and BVSTATE hand the window, a drawing too old for a dynamic block.
    /// </summary>
    public class BlockAuthoringPropertiesTests
    {
        private static CadDocument Panel(out string[] lines, string version = "AC1032")
        {
            var db = new DxfDatabase(version);
            var block = new DxfBlockTableRecord { Name = "PANEL", Origin = new GeoPoint3(0, 0, 0) };
            db.BlockTable.Add(block);
            DxfLine[] made =
            {
                new DxfLine(new GeoPoint3(100, 0, 0), new GeoPoint3(100, 50, 0)),
                new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)),
                new DxfLine(new GeoPoint3(0, 50, 0), new GeoPoint3(100, 50, 0)),
                new DxfLine(new GeoPoint3(0, 50, 0), new GeoPoint3(0, 0, 0)),
            };

            foreach (DxfLine line in made)
            {
                block.AppendEntity(line);
            }

            lines = made.Select(l => l.Handle).ToArray();
            db.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(1000, 500, 0), block));
            return CadDocument.From(db, "panel.dxf");
        }

        private static CadCommandSession Start(CadDocument document, params object[] inputs)
        {
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            return Run(session, inputs);
        }

        private static CadAuthoringElement Of(CadDocument document, int index) => document.BlockEditor.Elements[index];

        private static CadSelectionInfo Describe(CadDocument document, int index) =>
            CadBlockAuthoring.Describe(Of(document, index));

        private static string Row(CadSelectionInfo info, string label) =>
            info.Groups.SelectMany(g => g.Rows).Single(r => r.Label == label).Value;

        private static CadEditResult Set(CadDocument document, int index, string label, string text) =>
            CadBlockAuthoring.SetProperty(document, Of(document, index).Handle, label, text);

        private static CadCommandSession WithWidth(out CadDocument document, out string[] lines)
        {
            document = Panel(out lines);
            CadCommandSession session = Start(document, "-BEDIT", "PANEL");
            Run(session, "BPARAMETER", "Linear", P(0, 0), P(100, 0), P(50, -20));
            Run(
                session,
                "BACTION",
                "sTretch",
                P(50, 0),
                Enter,
                P(90, -10),
                P(110, 60),
                lines.Take(3).ToArray(),
                Enter,
                Enter
            );
            return session;
        }

        [Fact]
        public void TheRowsOfALinearParameter_AreMiscGeometryLabelsAndValueSet_ReadOnlyWhereTheyAreMeasured()
        {
            WithWidth(out CadDocument document, out _);

            CadSelectionInfo info = Describe(document, 0);

            Assert.Equal("Linear parameter (1)", info.Label);
            Assert.Equal(
                new[] { "Misc", "Geometry", "Property labels", "Value set" },
                info.Groups.Select(g => g.Title)
            );
            Assert.Equal("Linear", Row(info, "Name"));
            Assert.Equal("Yes", Row(info, "Show properties"));
            Assert.Equal("No", Row(info, "Chain actions"));
            Assert.Equal("1", Row(info, "Number of grips"));
            Assert.Equal("100.0000", Row(info, "Distance"));
            Assert.Equal("Distance1", Row(info, "Distance name"));
            Assert.Equal("None", Row(info, "Dist type"));
            Assert.All(info.Groups.Single(g => g.Title == "Geometry").Rows, r => Assert.True(r.IsReadOnly));
            Assert.True(info.Groups[0].Rows.Single(r => r.Label == "Number of grips").IsReadOnly);
            Assert.False(info.Groups[0].Rows.Single(r => r.Label == "Name").IsReadOnly);
        }

        [Fact]
        public void ARowSet_IsOneUndoStepOfItsOwn_AndAReadOnlyRowAndATakenNameAreRefused()
        {
            CadCommandSession session = WithWidth(out CadDocument document, out _);

            Assert.True(Set(document, 0, "Name", "Span").Succeeded);
            Assert.Equal("Span", Of(document, 0).Name);
            Assert.Equal("Properties", document.NextUndo.Name);

            Assert.False(Set(document, 0, "Distance", "10").Succeeded);
            Assert.False(Set(document, 1, "Name", "span").Succeeded);
            Assert.Equal("Span", Of(document, 0).Name);

            Assert.True(document.Undo());
            Assert.Equal("Linear", Of(document, 0).Name);
            Assert.NotNull(session);
        }

        [Fact]
        public void TheValueSetRows_ChangeTheTypeAndTheNumbersInPlace_AnAngleInDegrees()
        {
            WithWidth(out CadDocument document, out _);

            Assert.True(Set(document, 0, "Dist type", "Increment").Succeeded);
            CadSelectionInfo steps = Describe(document, 0);
            Assert.Equal("Increment", Row(steps, "Dist type"));
            Assert.Equal("0.0000", Row(steps, "Dist minimum"));
            Assert.Equal("200.0000", Row(steps, "Dist maximum"));
            Assert.Equal("10.0000", Row(steps, "Dist increment"));

            Assert.True(Set(document, 0, "Dist minimum", "50").Succeeded);
            Assert.True(Set(document, 0, "Dist maximum", "300").Succeeded);
            Assert.True(Set(document, 0, "Dist increment", "25").Succeeded);
            Assert.False(Set(document, 0, "Dist maximum", "40").Succeeded);
            Assert.Equal("300.0000", Row(Describe(document, 0), "Dist maximum"));

            Assert.True(Set(document, 0, "Dist type", "List").Succeeded);
            Assert.True(Set(document, 0, "Dist value list", "100, 150,200").Succeeded);
            Assert.Equal("100.0000,150.0000,200.0000", Row(Describe(document, 0), "Dist value list"));
            Assert.True(Set(document, 0, "Dist type", "None").Succeeded);
            Assert.DoesNotContain(Describe(document, 0).Groups.SelectMany(g => g.Rows), r =>
                r.Label == "Dist value list"
            );

            CadCommandSession session = Start(document, "BPARAMETER", "Rotation", P(0, 0), "20", "0", P(0, 30));
            CadAuthoringElement rotation = document.BlockEditor.Elements.Single(e =>
                e.TypeName == "Rotation parameter"
            );
            Assert.True(CadBlockAuthoring.SetProperty(document, rotation.Handle, "Angle type", "Increment").Succeeded);
            CadSelectionInfo angle = CadBlockAuthoring.Describe(document.BlockEditor.ElementOf(rotation.Handle));
            Assert.Equal("360.0000", Row(angle, "Angle maximum"));
            Assert.Equal("15.0000", Row(angle, "Angle increment"));
            Assert.NotNull(session);
        }

        [Fact]
        public void AnActionsRows_AreItsObjectsMultiplierOffsetAndForAnArrayItsSpacings()
        {
            CadCommandSession session = WithWidth(out CadDocument document, out string[] lines);
            CadSelectionInfo stretch = Describe(document, 1);

            Assert.Equal("Stretch action (1)", stretch.Label);
            Assert.Equal("3", Row(stretch, "Number of objects"));
            Assert.Equal("1.0000", Row(stretch, "Distance multiplier"));
            Assert.Equal("0.0000", Row(stretch, "Angle offset"));

            Assert.True(Set(document, 1, "Distance multiplier", "0.5").Succeeded);
            Assert.True(Set(document, 1, "Angle offset", "90").Succeeded);
            Assert.False(Set(document, 1, "Distance multiplier", "0").Succeeded);
            stretch = Describe(document, 1);
            Assert.Equal("0.5000", Row(stretch, "Distance multiplier"));
            Assert.Equal("90.0000", Row(stretch, "Angle offset"));

            Run(session, "BPARAMETER", "Linear", P(0, 0), P(0, 50), P(-20, 25));
            Run(session, "BACTION", "Array", P(-20, 25), Enter, new[] { lines[3] }, Enter, "20");
            CadAuthoringElement array = document.BlockEditor.Elements.Single(e => e.TypeName == "Array action");
            CadSelectionInfo rows = CadBlockAuthoring.Describe(array);
            Assert.Equal("20.0000", Row(rows, "Column spacing"));
            Assert.True(CadBlockAuthoring.SetProperty(document, array.Handle, "Column spacing", "35").Succeeded);
            Assert.Equal(
                "35.0000",
                Row(CadBlockAuthoring.Describe(document.BlockEditor.ElementOf(array.Handle)), "Column spacing")
            );
        }

        [Fact]
        public void AnXyAndAPolarParameter_ListTheirTwoPropertiesAndTheirOwnGeometry()
        {
            CadDocument document = Panel(out _);
            CadCommandSession session = Start(document, "-BEDIT", "PANEL");
            Run(session, "BPARAMETER", "Xy", P(0, 0), P(100, 50), P(100, 60));
            Run(session, "BPARAMETER", "Polar", P(0, 0), P(0, 100), P(10, 50));

            CadSelectionInfo xy = Describe(document, 0);
            Assert.Equal("100.0000", Row(xy, "Horizontal distance"));
            Assert.Equal("50.0000", Row(xy, "Vertical distance"));
            Assert.Equal("Horizontal1", Row(xy, "Horizontal name"));
            Assert.Equal("Vertical1", Row(xy, "Vertical name"));
            Assert.Equal("None", Row(xy, "Horz type"));
            Assert.Equal("None", Row(xy, "Vert type"));

            CadSelectionInfo polar = Describe(document, 1);
            Assert.Equal("Polar parameter (1)", polar.Label);
            Assert.Equal("100.0000", Row(polar, "Distance"));
            Assert.Equal("90.0000", Row(polar, "Angle"));
            Assert.Equal("Angle1", Row(polar, "Angle name"));
            Assert.Equal("None", Row(polar, "Angle type"));
        }

        [Fact]
        public void AStartPointPickedForAnAction_IsRefusedForNow_AndTheSecondPointTakenWithEnterOrAPickNearIt()
        {
            CadDocument document = Panel(out string[] lines);
            CadCommandSession session = Start(document, "-BEDIT", "PANEL");
            Run(session, "BPARAMETER", "Linear", P(0, 0), P(100, 0), P(50, -20));

            Run(session, "BACTION", "Move", P(50, 0));
            Assert.Equal(
                "Specify parameter point to associate with action or enter [sTart point/Second point] <Second point>:",
                session.PromptText
            );

            Run(session, P(5, 0));
            Assert.Contains(
                "Only the second point of a parameter can be associated with an action in this version.",
                session.History
            );
            Run(session, "sTart");
            Assert.Equal(2, session.History.Count(h => h.StartsWith("Only the second point")));

            Run(session, P(95, 0), new[] { lines[0] }, Enter, Enter);
            Assert.Equal("Move action", Of(document, 1).TypeName);
        }

        [Fact]
        public void AValueSetOptionWithAListOfTextOrAWrongRange_IsSaidAndTheParameterMadeWithoutIt()
        {
            CadDocument document = Panel(out _);
            CadCommandSession session = Start(document, "-BEDIT", "PANEL");

            Run(session, "BPARAMETER", "Linear", "Value set", "List", "100, x");
            Assert.Contains("Requires numeric nonzero value.", session.History);
            Run(session, P(0, 0), P(100, 0), P(50, -20));

            CadAuthoringElement width = Of(document, 0);
            Assert.Equal("None", Row(CadBlockAuthoring.Describe(width), "Dist type"));

            Run(session, "BPARAMETER", "Linear", "Value set", "Increment", "10", "200", "50");
            Assert.Contains("Requires numeric nonzero value.", session.History.Skip(1));
        }

        [Fact]
        public void ANameOrLabelAnotherElementHas_IsSaidAtTheOption_AndNotTaken()
        {
            CadDocument document = Panel(out _);
            CadCommandSession session = Start(document, "-BEDIT", "PANEL");
            Run(session, "BPARAMETER", "Linear", P(0, 0), P(100, 0), P(50, -20));

            Run(session, "BPARAMETER", "Linear", "Name", "linear");
            Assert.Contains("A parameter with that name already exists in the block", session.History);
            Run(session, "Label", "distance1");
            Assert.Contains("A property with that label already exists in the block", session.History);
            Run(session, P(0, 0), P(0, 50), P(-20, 25));

            Assert.Equal(new[] { "Linear", "Linear1" }, document.BlockEditor.Elements.Select(e => e.Name));
            Assert.Equal(new[] { "Distance1", "Distance2" }, document.BlockEditor.Elements.Select(e => e.Labels[0]));
        }

        [Fact]
        public void ADrawingOlderThanR2010_RefusesAParameter_InTheLibrarysWords_AndWritesNothing()
        {
            CadDocument document = Panel(out _, "AC1015");
            CadCommandSession session = Start(document, "-BEDIT", "PANEL");

            Run(session, "BPARAMETER", "Linear", P(0, 0), P(100, 0), P(50, -20));

            Assert.Empty(document.BlockEditor.Elements);
            Assert.Contains(session.History, h => h.Contains("R2010"));
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void ALookupParameterAndAction_AreRefusedWithTheirReason_AsTheirTableIsNotInThisVersion()
        {
            CadDocument document = Panel(out _);
            CadCommandSession session = Start(document, "-BEDIT", "PANEL");

            Run(session, "BPARAMETER", "looKup");
            Run(session, "BACTION", "Lookup");

            Assert.Equal(
                2,
                session.History.Count(h =>
                    h.Contains("lookup table, which the Block Editor of this version does not make")
                )
            );
            Assert.Empty(document.BlockEditor.Elements);
        }

        [Fact]
        public void BtestblockHandsTheWindowACopyOfTheDrawing_WithTheBlockAsItStandsInsertedAlone()
        {
            CadDocument document = Panel(out _);
            CadCommandSession session = Start(document, "-BEDIT", "PANEL");
            Run(session, "BPARAMETER", "Linear", P(0, 0), P(100, 0), P(50, -20));
            CadTestBlockRequest asked = null;
            session.UiRequested += (sender, e) =>
            {
                if (e.Name == "BTESTBLOCK")
                {
                    asked = (CadTestBlockRequest)e.Payload;
                    e.Handled = true;
                }
            };

            Run(session, "BTESTBLOCK");

            Assert.NotNull(asked);
            Assert.Equal("Test Block Window - PANEL", asked.Title);
            DxfBlockReference only = Assert.Single(asked.Database.ModelSpace.OfType<DxfBlockReference>(), r =>
                !r.IsErased
            );
            Assert.True(only.IsDynamicBlock);
            Assert.Equal("PANEL", only.Name);
            Assert.Single(document.Database.ModelSpace.OfType<DxfBlockReference>(), r => !r.IsErased);
            Assert.Equal(
                new GeoPoint3(1000, 500, 0),
                document.Database.ModelSpace.OfType<DxfBlockReference>().First().Position
            );
        }

        [Fact]
        public void BauthorpaletteAndBvstateAskTheWindow_AndWithNoneSayWhatTheyDoOnTheCommandLine()
        {
            CadDocument document = Panel(out _);
            CadCommandSession session = Start(document, "-BEDIT", "PANEL", "BPARAMETER", "Visibility", P(20, -10));
            var asked = new List<string>();
            session.UiRequested += (sender, e) =>
            {
                if (e.Name == "BAUTHORPALETTE" || e.Name == "BVSTATE")
                {
                    asked.Add(e.Name + ":" + e.Payload.GetType().Name);
                    e.Handled = true;
                }
            };

            Run(session, "BAUTHORPALETTE", "BAUTHORPALETTECLOSE", "BVSTATE");

            Assert.Equal(
                new[]
                {
                    "BAUTHORPALETTE:CadAuthoringPaletteRequest",
                    "BAUTHORPALETTE:CadAuthoringPaletteRequest",
                    "BVSTATE:CadVisibilityStatesRequest",
                },
                asked
            );
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void AnElementClickedAtASelectionPrompt_IsTakenLikeAnObject_AndMoveLeavesItBe()
        {
            CadCommandSession session = WithWidth(out CadDocument document, out _);
            string handle = Of(document, 0).Handle;
            document.Selection.Set(new[] { handle });

            Run(session, "MOVE");

            Assert.False(session.IsRunning);
            Assert.Equal(2, document.BlockEditor.Elements.Count);
        }
    }
}
