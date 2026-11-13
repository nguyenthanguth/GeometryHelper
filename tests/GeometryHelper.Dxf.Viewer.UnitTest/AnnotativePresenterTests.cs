using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// The annotative objects in the presenter: the Annotative rows of Properties, -HATCH ANnotative, -OBJECTSCALE, AIOBJECTSCALEADD
    /// and AIOBJECTSCALEREMOVE in the words of AutoCAD 2021's console (probed), ANNOALLVISIBLE, text drawn under an annotative style,
    /// and annotative objects drawn at CANNOSCALE and at a viewport's scale.
    /// </summary>
    public class AnnotativePresenterTests
    {
        private static CadDocument Scaled()
        {
            CadDocument document = Blank();
            DxfObjectContextCollection scales = document.Database.ObjectContextManager.GetContextCollection(
                "ACDB_ANNOTATIONSCALES"
            );

            foreach (double d in new[] { 1.0, 50.0, 100.0 })
            {
                string name = "1:" + d.ToString("0");
                scales.AddContext(
                    new DxfAnnotationScale
                    {
                        Name = name,
                        PaperUnits = 1,
                        DrawingUnits = d,
                    }
                );
            }

            return document;
        }

        private static DxfAnnotationScale Scale(CadDocument document, string name) =>
            (DxfAnnotationScale)
                document.Database.ObjectContextManager.GetContextCollection("ACDB_ANNOTATIONSCALES").GetContext(name);

        private static DxfDBText Text(CadDocument document)
        {
            var text = new DxfDBText
            {
                Position = new GeoPoint3(10, 10, 0),
                TextString = "ABC",
                Height = 2.5,
            };
            document.Database.ModelSpace.AppendEntity(text);
            text.Annotative = DxfAnnotativeStates.True;
            return text;
        }

        private static string[] Names(DxfEntity entity) => entity.GetContexts().Select(c => c.Name).ToArray();

        private static CadCommandSession Start(CadDocument document, params object[] inputs) =>
            Run(Session(document), inputs);

        [Fact]
        public void ObjectScale_AddsAScale_AsAutoCADsConsoleDoes_AndAsksTheNextUntilEnter()
        {
            CadDocument document = Scaled();
            DxfDBText text = Text(document);
            CadCommandSession session = Start(document, "-OBJECTSCALE");
            Assert.Equal("Select annotative objects:", session.PromptText);

            Run(session, new[] { text.Handle }, Enter);
            Assert.Equal("Enter an option [Add/Delete/?] <Add>:", session.PromptText);
            Run(session, "A");
            Assert.Equal("Enter named scale to add or [?] <1:1>:", session.PromptText);

            Run(session, "1:50");
            Assert.Equal(
                "1 object updated to support annotation scale <1:50>.",
                session.History[session.History.Count - 1]
            );
            Assert.Equal("Enter named scale to add or [?]:", session.PromptText);
            Assert.Equal(new[] { "1:1", "1:50" }, Names(text));

            Run(session, "1:7");
            Assert.Contains("Scale 1:7 not found.", session.History);
            Assert.Equal("Enter named scale to add or [?]:", session.PromptText);
            Run(session, Enter);
            Assert.False(session.IsRunning);

            // The whole command is one undo step.
            Assert.Equal("-OBJECTSCALE", document.NextUndo.Name);
            Assert.True(document.Undo());
            Assert.Equal(new[] { "1:1" }, Names(Entity<DxfDBText>(document, text.Handle)));
        }

        [Fact]
        public void ObjectScale_DeletesAScale_AndRefusesTheLastAndOneTheObjectIsNotShownAt()
        {
            CadDocument document = Scaled();
            DxfDBText text = Text(document);
            text.AddContext(Scale(document, "1:50"));
            CadCommandSession session = Start(document, "-OBJECTSCALE", new[] { text.Handle }, Enter, "D");
            Assert.Equal("Enter named scale to delete or [?] <1:1>:", session.PromptText);

            Run(session, "1:50");
            Assert.Equal("1 object scale removed.", session.History[session.History.Count - 1]);
            Assert.Equal(new[] { "1:1" }, Names(text));

            Run(session, "1:100");
            Assert.Contains("0 objects scale removed.", session.History);
            Assert.Contains("1 object doesn't support annotation scale <1:100>.", session.History);
            Assert.Contains("1 objects cannot remove scale.", session.History);
            Run(session, Enter);
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void AnObjectNotAnnotative_IsNotOfferedToObjectScale()
        {
            CadDocument document = Scaled();
            var line = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0));
            document.Database.ModelSpace.AppendEntity(line);
            CadCommandSession session = Start(document, "-OBJECTSCALE", new[] { line.Handle }, Enter);

            Assert.False(session.IsRunning);
            Assert.Contains("No annotative objects found.", session.History);
        }

        [Fact]
        public void TheCurrentScaleAddedAndRemoved_ByAiObjectScaleAddAndRemove_InAutoCADsWords()
        {
            CadDocument document = Scaled();
            DxfDBText text = Text(document);
            document.Database.Cannoscale = "1:100";

            CadCommandSession session = Start(document, "AIOBJECTSCALEADD", new[] { text.Handle }, Enter);
            Assert.Equal(
                "1 object updated to support annotation scale <1:100>.",
                session.History[session.History.Count - 1]
            );
            Assert.Equal(new[] { "1:1", "1:100" }, Names(text));

            session = Start(document, "AIOBJECTSCALEREMOVE", new[] { text.Handle }, Enter);
            Assert.Equal("1 object scale removed.", session.History[session.History.Count - 1]);
            Assert.Equal(new[] { "1:1" }, Names(text));

            session = Start(document, "AIOBJECTSCALEREMOVE", new[] { text.Handle }, Enter);
            Assert.Contains("0 objects scale removed.", session.History);
            Assert.Contains("1 object doesn't support annotation scale <1:100>.", session.History);
            Assert.Contains("1 objects cannot remove scale.", session.History);
        }

        [Fact]
        public void HatchAnnotative_Yes_MakesTheHatchAnnotativeAtTheCurrentScale_AndAsksItsDefaultNext()
        {
            CadDocument document = Scaled();
            AddSquare(document);
            document.Database.Cannoscale = "1:50";

            CadCommandSession session = Start(document, "-HATCH", "AN");
            Assert.Equal("Create annotative hatch [Yes/No] <N>:", session.PromptText);
            Run(session, "Y");
            Run(session, "AN");
            Assert.Equal("Create annotative hatch [Yes/No] <Y>:", session.PromptText);
            Run(session, Enter, P(50, 50), Enter);

            DxfHatch hatch = Assert.Single(All<DxfHatch>(document.ModelSpace));
            Assert.Equal(DxfAnnotativeStates.True, hatch.Annotative);
            Assert.Equal(new[] { "1:50" }, Names(hatch));
            Assert.True(hatch.HasContext(Scale(document, "1:50")));

            // The same hatch made plain has lines 50 times closer.
            document = Scaled();
            AddSquare(document);
            document.Database.Cannoscale = "1:50";
            Start(document, "-HATCH", P(50, 50), Enter);
            DxfHatch plain = Assert.Single(All<DxfHatch>(document.ModelSpace));
            Assert.Equal(DxfAnnotativeStates.False, plain.Annotative);
            Assert.Equal(plain.GetPatternDefinitionAt(0).OffsetY * 50.0, hatch.GetPatternDefinitionAt(0).OffsetY, 6);
            AssertReopens(document);
        }

        private static void AddSquare(CadDocument document)
        {
            GeoPoint3[] corners =
            {
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(100, 0, 0),
                new GeoPoint3(100, 100, 0),
                new GeoPoint3(0, 100, 0),
            };

            for (int i = 0; i < 4; i++)
            {
                document.Database.ModelSpace.AppendEntity(new DxfLine(corners[i], corners[(i + 1) % 4]));
            }
        }

        [Fact]
        public void TheAnnotativeRow_ShowsYesOrNo_AndMakesATextAnnotativeAtTheCurrentScale()
        {
            CadDocument document = Scaled();
            document.Database.Cannoscale = "1:50";
            var text = new DxfDBText
            {
                Position = new GeoPoint3(1, 1, 0),
                TextString = "T",
                Height = 2.5,
            };
            document.Database.ModelSpace.AppendEntity(text);

            CadPropertyRow Row(string label) =>
                CadProperties
                    .Describe(document, null, new[] { text.Handle })
                    .Groups.SelectMany(g => g.Rows)
                    .FirstOrDefault(r => r.Label == label);

            Assert.Equal("No", Row("Annotative").Value);
            Assert.Null(Row("Annotation scale"));

            CadEditResult result = CadPropertyEditor.Set(document, new[] { text.Handle }, "Annotative", "Yes");
            Assert.True(result.Succeeded, result.Message);
            Assert.Equal("Yes", Row("Annotative").Value);
            Assert.Equal("1:50", Row("Annotation scale").Value);
            Assert.True(Row("Annotation scale").IsReadOnly);
            Assert.Equal(DxfAnnotativeStates.True, Entity<DxfDBText>(document, text.Handle).Annotative);

            Assert.True(document.Undo());
            Assert.Equal(DxfAnnotativeStates.False, Entity<DxfDBText>(document, text.Handle).Annotative);
        }

        [Fact]
        public void ATextDrawnUnderAnAnnotativeStyle_IsAsHighAsTheSheetsPaperOverTheScale_AndAnnotative()
        {
            CadDocument document = Scaled();
            var style = new DxfTextStyleTableRecord { Name = "Anno", FileName = "arial.ttf" };
            document.Database.TextStyleTable.Add(style);
            style.Annotative = DxfAnnotativeStates.True;
            document.Database.Textstyle = "Anno";
            document.Database.Cannoscale = "1:50";

            Start(document, "TEXT", P(10, 10), "2.5", "0", "ABC", Enter);
            DxfDBText text = Assert.Single(All<DxfDBText>(document.ModelSpace));
            Assert.Equal(DxfAnnotativeStates.True, text.Annotative);
            Assert.Equal(new[] { "1:50" }, Names(text));
            Assert.Equal(125.0, text.Height, 9);
        }

        [Fact]
        public void AnnotativeText_IsDrawnAtTheCurrentScale_AndNotDrawnAtOneItHasNot_UnlessAllAreVisible()
        {
            CadDocument document = Scaled();
            DxfDBText text = Text(document);
            text.AddContext(Scale(document, "1:50"));
            DxfDatabase db = document.Database;

            double Height()
            {
                DisplayList2 list = new DisplayBuilder(db).Build(db.ModelSpace, true);
                return list.Items.OfType<DisplayText2>().SingleOrDefault()?.Height ?? -1.0;
            }

            db.Cannoscale = "1:1";
            Assert.Equal(2.5, Height(), 9);
            db.Cannoscale = "1:50";
            Assert.Equal(125.0, Height(), 9);

            // At 1:100 the text has no context: shown as it is read where every annotative object is visible, else not drawn.
            db.Cannoscale = "1:100";
            db.AnnoAllVisible = true;
            Assert.Equal(2.5, Height(), 9);
            db.AnnoAllVisible = false;
            Assert.Equal(-1.0, Height(), 9);
        }

        [Fact]
        public void AnnotativeMText_IsDrawnWithItsHeightScaledAtTheCurrentScale()
        {
            CadDocument document = Scaled();
            var words = new DxfMText
            {
                Location = new GeoPoint3(0, 0, 0),
                TextHeight = 2.5,
                Contents = "MT",
            };
            document.Database.ModelSpace.AppendEntity(words);
            words.Annotative = DxfAnnotativeStates.True;
            words.AddContext(Scale(document, "1:50"));
            DxfDatabase db = document.Database;

            db.Cannoscale = "1:1";
            DisplayList2 list = new DisplayBuilder(db).Build(db.ModelSpace, true);
            double one = list.Items.OfType<DisplayText2>().Single().Height;
            db.Cannoscale = "1:50";
            list = new DisplayBuilder(db).Build(db.ModelSpace, true);
            double fifty = list.Items.OfType<DisplayText2>().Single().Height;
            Assert.Equal(2.5, one, 9);
            Assert.Equal(125.0, fifty, 9);
        }

        [Fact]
        public void TheScaleOfAViewport_DrawsAnnotativeTextOfModelSpaceInIt_AtThatScale()
        {
            CadDocument document = Scaled();
            DxfDBText text = Text(document);
            text.AddContext(Scale(document, "1:50"));
            DxfDatabase db = document.Database;
            db.Cannoscale = "1:1";
            var layout = new DxfLayout { LayoutName = "Plan" };
            layout.AddToLayoutDictionary(db, null);
            layout.Initialize();
            var viewport = new DxfViewport
            {
                CenterPoint = new GeoPoint3(100, 100, 0),
                Width = 100,
                Height = 100,
            };
            layout.BlockTableRecord.AppendEntity(viewport);
            viewport.ViewCenter = new GeoPoint2(10, 10);
            viewport.ViewHeight = 5000;
            viewport.On = true;

            // The viewport shows model space at 1:50, whatever CANNOSCALE is: its own drawing of the text, 125 high.
            var builder = new DisplayBuilder(db);
            DisplayList2 model = builder.Build(db.ModelSpace, true);
            Assert.Equal(2.5, model.Items.OfType<DisplayText2>().Single().Height, 9);
            DisplayList2 sheet = builder.Build(layout.BlockTableRecord, false);
            DisplayViewport2 seen = sheet.Items.OfType<DisplayViewport2>().Single();
            Assert.Equal(0.02, seen.Scale, 9);
            Assert.Equal(125.0, seen.Model.Items.OfType<DisplayText2>().Single().Height, 9);
        }

        [Fact]
        public void AnnoAllVisible_IsAVariableOfTheDrawing_ZeroOrOne()
        {
            CadDocument document = Scaled();
            CadCommandSession session = Start(document, "ANNOALLVISIBLE", "0");
            Assert.False(document.Database.AnnoAllVisible);
            Run(session, "ANNOALLVISIBLE", "1");
            Assert.True(document.Database.AnnoAllVisible);
        }
    }
}
