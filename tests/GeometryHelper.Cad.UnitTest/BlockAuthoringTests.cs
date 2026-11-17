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
    /// The authoring side of the Block Editor, as AutoCAD 2021's: BPARAMETER and BACTION write the parameters and actions of a
    /// dynamic block through the library, each a step of undo; the INSERTs of the block saved work as AutoCAD's do; ERASE takes an
    /// element out; BVSTATE, BVHIDE and BVSHOW manage the visibility states.
    /// </summary>
    public class BlockAuthoringTests
    {
        /// <summary>A drawing of R2018 with the block PANEL, a rectangle 100 by 50 of four lines, and an INSERT of it at (1 000, 500).</summary>
        private static CadDocument Panel(out string[] lines)
        {
            var db = new DxfDatabase("AC1032");
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

        /// <summary>A drawing with the block THING holding the entities given, and an INSERT of it at the origin.</summary>
        private static CadDocument Thing(out string[] handles, params DxfEntity[] entities)
        {
            var db = new DxfDatabase("AC1032");
            var block = new DxfBlockTableRecord { Name = "THING", Origin = new GeoPoint3(0, 0, 0) };
            db.BlockTable.Add(block);

            foreach (DxfEntity entity in entities)
            {
                block.AppendEntity(entity);
            }

            handles = entities.Select(e => e.Handle).ToArray();
            db.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(0, 0, 0), block));
            return CadDocument.From(db, "thing.dxf");
        }

        private static CadCommandSession Start(CadDocument document, params object[] inputs)
        {
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            return Run(session, inputs);
        }

        private static IReadOnlyList<CadAuthoringElement> Elements(CadDocument document) =>
            document.BlockEditor.Elements;

        private static void SaveAndClose(CadCommandSession session)
        {
            Run(session, "BSAVE", "BCLOSE");
            Assert.Null(session.Document.BlockEditor);
        }

        private static DxfBlockReference First(CadDocument document) => All<DxfBlockReference>(document.ModelSpace)[0];

        private static DxfDynamicBlockReferenceProperty Property(CadDocument document, string name) =>
            First(document).DynamicBlockReferencePropertyCollection.First(p => p.PropertyName == name);

        private static IEnumerable<DxfLine> Drawn(CadDocument document) =>
            First(document).BlockTableRecord.OfType<DxfLine>().Where(l => !l.IsErased);

        private static bool Has(IEnumerable<DxfLine> lines, GeoPoint3 a, GeoPoint3 b) =>
            lines.Any(l => l.StartPoint.IsEqualTo(a, Tight) && l.EndPoint.IsEqualTo(b, Tight));

        [Fact]
        public void ALinearParameterAndAStretchAction_AreWrittenByTheCommands_AndAnInsertOfTheSavedBlockStretches()
        {
            CadDocument document = Panel(out string[] lines);
            CadCommandSession session = Start(document, "-BEDIT", "PANEL");

            Run(session, "BPARAMETER", "Linear", P(0, 0), P(100, 0), P(50, -20));

            CadAuthoringElement width = Assert.Single(Elements(document));
            Assert.Equal("Linear parameter", width.TypeName);
            Assert.Equal("Distance1", Assert.Single(width.Labels));
            Assert.Equal("Linear", width.Name);
            Assert.True(document.BlockEditor.HasUnsavedChanges);
            Assert.False(session.IsRunning);

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

            Assert.Equal(2, Elements(document).Count);
            Assert.Equal("Stretch action", Elements(document)[1].TypeName);
            Assert.Equal("Stretch", Elements(document)[1].Name);

            SaveAndClose(session);
            AssertReopens(document);

            Assert.Equal(100.0, (double)Property(document, "Distance1").Value, 9);
            Property(document, "Distance1").Value = 200.0;
            Assert.Equal(200.0, (double)Property(document, "Distance1").Value, 9);
            Assert.True(First(document).BlockTableRecord.IsAnonymous);
            Assert.True(Has(Drawn(document), new GeoPoint3(200, 0, 0), new GeoPoint3(200, 50, 0)));
            Assert.True(Has(Drawn(document), new GeoPoint3(0, 50, 0), new GeoPoint3(0, 0, 0)));
        }

        [Fact]
        public void AParameterAndEachActionOfAnotherSession_AreOneUndoStepEach_AndUndoTakesThemOutOfTheBlock()
        {
            CadDocument document = Panel(out string[] lines);
            CadCommandSession session = Start(document, "-BEDIT", "PANEL");

            Run(session, "BPARAMETER", "Linear", P(0, 0), P(100, 0), P(50, -20));
            Run(session, "BACTION", "Move", P(50, 0), Enter, new[] { lines[0] }, Enter, Enter);

            Assert.Equal(2, Elements(document).Count);
            Assert.Equal("BACTION", document.NextUndo.Name);
            Assert.True(document.Undo());
            Assert.Equal("Linear parameter", Assert.Single(Elements(document)).TypeName);
            Assert.True(document.Undo());
            Assert.Empty(Elements(document));
            Assert.False(document.Database.BlockTable["PANEL"].IsDynamicBlock);
            Assert.True(document.Redo());
            Assert.Single(Elements(document));
        }

        [Fact]
        public void APointParameterAndAMoveAction_MoveTheCircleWithThePositionOfAnInsert()
        {
            var circle = new DxfCircle(new GeoPoint3(80, 20, 0), GeoVector3.ZAxis, 5);
            CadDocument document = Thing(out string[] handles, circle);
            CadCommandSession session = Start(document, "-BEDIT", "THING");

            Run(session, "BPARAMETER", "pOint", P(80, 20), P(80, 35));
            Run(session, "BACTION", "Move", P(80, 20), handles, Enter, Enter);

            Assert.Equal("Point parameter", Elements(document)[0].TypeName);
            Assert.Equal("Move action", Elements(document)[1].TypeName);
            SaveAndClose(session);

            Assert.Equal(80.0, (double)Property(document, "Position1 X").Value, 9);
            Property(document, "Position1 X").Value = 120.0;
            Property(document, "Position1 Y").Value = 35.0;
            DxfCircle moved = First(document).BlockTableRecord.OfType<DxfCircle>().Single();
            Assert.True(moved.Center.IsEqualTo(new GeoPoint3(120, 35, 0), Tight));
        }

        [Fact]
        public void ARotationParameterAndARotateAction_TurnTheLineAboutTheBasePoint()
        {
            var arrow = new DxfLine(new GeoPoint3(50, 25, 0), new GeoPoint3(80, 25, 0));
            CadDocument document = Thing(out string[] handles, arrow);
            CadCommandSession session = Start(document, "-BEDIT", "THING");

            Run(session, "BPARAMETER", "Rotation", P(50, 25), "30", "0", P(50, 60));
            Run(session, "BACTION", "Rotate", P(80, 25), handles, Enter, Enter);
            SaveAndClose(session);

            Property(document, "Angle1").Value = Math.PI / 2;
            Assert.True(Has(Drawn(document), new GeoPoint3(50, 25, 0), new GeoPoint3(50, 55, 0)));
        }

        [Fact]
        public void AFlipParameterAndAFlipAction_MirrorTheLeafAboutTheReflectionLine()
        {
            var leaf = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(90, 0, 0));
            CadDocument document = Thing(out string[] handles, leaf);
            CadCommandSession session = Start(document, "-BEDIT", "THING");

            Run(session, "BPARAMETER", "Flip", P(0, 0), P(0, 50), P(10, 50));
            Run(session, "BACTION", "Flip", P(0, 50), handles, Enter, Enter);
            SaveAndClose(session);

            Property(document, "Flip state1").Value = (short)1;
            Assert.True(Has(Drawn(document), new GeoPoint3(0, 0, 0), new GeoPoint3(-90, 0, 0)));
        }

        [Fact]
        public void AnXyParameterAndItsStretch_StretchTheCornerBothWays()
        {
            CadDocument document = Panel(out string[] lines);
            CadCommandSession session = Start(document, "-BEDIT", "PANEL");

            Run(session, "BPARAMETER", "Xy", P(0, 0), P(100, 50), P(100, 60));
            Run(
                session,
                "BACTION",
                "sTretch",
                P(100, 50),
                Enter,
                P(90, 40),
                P(110, 60),
                new[] { lines[0], lines[2] },
                Enter,
                Enter
            );
            Assert.Equal(2, Elements(document).Count);
            SaveAndClose(session);

            Property(document, "Horizontal1").Value = 160.0;
            Property(document, "Vertical1").Value = 80.0;
            Assert.True(
                Has(Drawn(document), new GeoPoint3(100, 0, 0), new GeoPoint3(160, 80, 0)),
                string.Join(" ; ", Drawn(document).Select(l => l.StartPoint + "-" + l.EndPoint))
            );
            Assert.True(Has(Drawn(document), new GeoPoint3(0, 50, 0), new GeoPoint3(160, 80, 0)));
        }

        [Fact]
        public void APolarParameterAndAPolarStretch_TurnAndStretchTheBar()
        {
            var bar = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0));
            CadDocument document = Thing(out string[] handles, bar);
            CadCommandSession session = Start(document, "-BEDIT", "THING");

            Run(session, "BPARAMETER", "Polar", P(0, 0), P(100, 0), P(50, -10));
            Run(session, "BACTION", "Polar stretch", P(50, 0), P(90, -10), P(110, 10), handles, Enter, Enter, Enter);
            SaveAndClose(session);

            Property(document, "Angle1").Value = Math.PI / 2;
            Assert.True(Has(Drawn(document), new GeoPoint3(0, 0, 0), new GeoPoint3(0, 100, 0)));
        }

        [Fact]
        public void AnArrayActionOfALinearParameter_CopiesTheEntityAlongTheDistance()
        {
            var post = new DxfCircle(new GeoPoint3(0, 0, 0), GeoVector3.ZAxis, 5);
            CadDocument document = Thing(out string[] handles, post);
            CadCommandSession session = Start(document, "-BEDIT", "THING");

            Run(session, "BPARAMETER", "Linear", P(0, 0), P(100, 0), P(50, -20));
            Run(session, "BACTION", "Array", P(50, 0), Enter, handles, Enter, "60");
            SaveAndClose(session);

            Property(document, "Distance1").Value = 250.0;
            Assert.Equal(4, First(document).BlockTableRecord.OfType<DxfCircle>().Count(c => !c.IsErased));
        }

        [Fact]
        public void AScaleActionOfALinearParameter_ScalesTheCircle()
        {
            var circle = new DxfCircle(new GeoPoint3(50, 25, 0), GeoVector3.ZAxis, 25);
            CadDocument document = Thing(out string[] handles, circle);
            CadCommandSession session = Start(document, "-BEDIT", "THING");

            Run(session, "BPARAMETER", "Linear", P(0, 0), P(100, 0), P(50, -40));
            Run(session, "BACTION", "Scale", P(50, 0), handles, Enter, Enter);
            SaveAndClose(session);

            Property(document, "Distance1").Value = 200.0;
            DxfCircle scaled = First(document).BlockTableRecord.OfType<DxfCircle>().Single();
            Assert.Equal(50.0, scaled.Radius, 9);
            Assert.True(scaled.Center.IsEqualTo(new GeoPoint3(100, 50, 0), Tight));
        }

        [Fact]
        public void AnAlignmentAndABasePointParameter_AreWrittenAsOneNodeEach_AndTheSecondOfAKindIsRefused()
        {
            CadDocument document = Panel(out _);
            CadCommandSession session = Start(document, "-BEDIT", "PANEL");

            Run(session, "BPARAMETER", "Alignment", P(0, 0), P(50, 0));
            Run(session, "BPARAMETER", "Base", P(10, 10));
            Run(session, "BPARAMETER", "Base");

            Assert.Equal(
                new[] { "Alignment parameter", "Base point parameter" },
                Elements(document).Select(e => e.TypeName)
            );
            Assert.Contains("** Base point parameter already exists in block definition. **", session.History);
            Assert.True(document.Database.BlockTable["PANEL"].IsDynamicBlock);
        }

        [Fact]
        public void TheOptionsOfTheFirstPrompt_GiveTheParameterItsNameLabelDescriptionAndValueSet()
        {
            CadDocument document = Panel(out string[] lines);
            CadCommandSession session = Start(document, "-BEDIT", "PANEL");

            Run(session, "BPARAMETER", "Linear", "Name", "Width", "Label", "Width", "Description", "Overall width");
            Run(session, "Value set", "Increment", "10", "50", "300");
            Run(session, P(0, 0), P(100, 0), P(50, -20));

            CadAuthoringElement width = Assert.Single(Elements(document));
            Assert.Equal("Width", width.Name);
            Assert.Equal("Width", Assert.Single(width.Labels));

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
            SaveAndClose(session);

            Property(document, "Width").Value = 75.0;
            Assert.Equal(70.0, (double)Property(document, "Width").Value, 9);
            Assert.Equal("Overall width", Property(document, "Width").Description);
        }

        [Fact]
        public void AnActionPickedOnAParameterItCannotFollow_IsRefusedInAutoCadsWords_AndAsksAgain()
        {
            CadDocument document = Panel(out _);
            CadCommandSession session = Start(document, "-BEDIT", "PANEL");
            Run(session, "BPARAMETER", "Linear", P(0, 0), P(100, 0), P(50, -20));

            Run(session, "BACTION", "Rotate", P(50, 0));

            Assert.Contains("Invalid selection.  Action requires a Rotation parameter.", session.History);
            Assert.Equal("Select parameter:", session.PromptText);

            Run(session, P(300, 300));
            Assert.Contains("Object selected is not a parameter object", session.History);
            Run(session, Esc);
            Assert.Single(Elements(document));
        }

        [Fact]
        public void ACommandOutsideTheBlockEditor_SaysSo()
        {
            CadDocument document = Panel(out _);
            CadCommandSession session = Start(document, "BPARAMETER");

            Assert.Contains("** BPARAMETER command only allowed in Block Editor. **", session.History);
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void AParameterSelectedAndErased_GoesWithItsGripsAndActions_AndTheBlockIsNoDynamicBlockAfterwards()
        {
            CadDocument document = Panel(out string[] lines);
            CadCommandSession session = Start(document, "-BEDIT", "PANEL");
            Run(session, "BPARAMETER", "Linear", P(0, 0), P(100, 0), P(50, -20));
            Run(session, "BACTION", "Move", P(50, 0), Enter, new[] { lines[0] }, Enter, Enter);
            string parameter = Elements(document)[0].Handle;

            Run(session, "ERASE", P(50, -20), Enter);

            Assert.True(Elements(document).Count == 0, string.Join(" | ", session.History));
            Assert.False(document.Database.BlockTable["PANEL"].IsDynamicBlock);
            Assert.Equal("ERASE", document.NextUndo.Name);
            Assert.True(document.Undo());
            Assert.Equal(2, Elements(document).Count);
            Assert.Equal(parameter, Elements(document)[0].Handle);
        }

        [Fact]
        public void TheGlyphOfAParameter_IsPickedNearItsLine_AndAnActionNearItsBolt()
        {
            CadDocument document = Panel(out string[] lines);
            CadCommandSession session = Start(document, "-BEDIT", "PANEL");
            Run(session, "BPARAMETER", "Linear", P(0, 0), P(100, 0), P(50, -20));
            Run(session, "BACTION", "Move", P(50, 0), Enter, new[] { lines[0] }, Enter, P(70, -30));

            CadBlockEditor editor = document.BlockEditor;
            Assert.Equal("Linear", CadBlockAuthoring.ElementAt(editor, P(30, 0.4), 1.0)?.Name);
            Assert.Equal("Move", CadBlockAuthoring.ElementAt(editor, P(70.2, -30.2), 1.0)?.Name);
            Assert.Null(CadBlockAuthoring.ElementAt(editor, P(30, 20), 1.0));
        }
    }
}
