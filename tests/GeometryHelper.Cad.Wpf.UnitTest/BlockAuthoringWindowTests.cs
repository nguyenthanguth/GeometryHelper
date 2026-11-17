using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// The authoring side of the Block Editor in the window: the Parameter and Action menus of the green tab, the Visibility States
    /// dialog and the Block Authoring Palettes, and the Properties of a parameter or an action picked by its glyph.
    /// </summary>
    public sealed class BlockAuthoringWindowTests
    {
        /// <summary>A drawing with the block PANEL, a rectangle 100 by 50 of four lines, placed once.</summary>
        private static CadDocument Plan(out string[] lines)
        {
            DxfDatabase database = CadTemplate.CreateMetric();
            var panel = new DxfBlockTableRecord { Name = "PANEL", Origin = new GeoPoint3(0, 0, 0) };
            database.BlockTable.Add(panel);
            DxfLine[] made =
            {
                new DxfLine(new GeoPoint3(100, 0, 0), new GeoPoint3(100, 50, 0)),
                new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)),
                new DxfLine(new GeoPoint3(0, 50, 0), new GeoPoint3(100, 50, 0)),
                new DxfLine(new GeoPoint3(0, 50, 0), new GeoPoint3(0, 0, 0)),
            };

            foreach (DxfLine line in made)
            {
                panel.AppendEntity(line);
            }

            lines = made.Select(l => l.Handle).ToArray();
            database.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(1000, 0, 0), panel));
            return CadDocument.From(database, "panel.dxf");
        }

        private static MainViewModel Edit(CadDocument document)
        {
            var model = new MainViewModel();
            model.OpenDocument(document);
            model.Execute("-BEDIT");
            model.Execute("PANEL");
            return model;
        }

        private static BlockEditorRibbonViewModel Ribbon(MainViewModel model) =>
            (BlockEditorRibbonViewModel)model.BlockEditorTab.TabContent;

        private static void Width(MainViewModel model)
        {
            model.StartCommand("BPARAMETER");
            model.Execute("Linear");
            model.Execute("0,0");
            model.Execute("100,0");
            model.Execute("50,-20");
        }

        [Fact]
        public void TheParameterMenu_ListsEveryKind_LookupGrey_AndAnItemStartsBparameterWithItsKindAnswered()
        {
            MainViewModel model = Edit(Plan(out _));
            BlockEditorRibbonViewModel ribbon = Ribbon(model);

            Assert.Equal(
                new[]
                {
                    "Point",
                    "Linear",
                    "Polar",
                    "XY",
                    "Rotation",
                    "Alignment",
                    "Flip",
                    "Visibility",
                    "Lookup",
                    "Base Point",
                },
                ribbon.ParameterItems.Select(i => i.Header)
            );
            Assert.False(ribbon.ParameterItems.Single(i => i.Header == "Lookup").IsEnabled);
            Assert.Equal("Point ▾", ribbon.ParameterButton);

            ribbon.ParameterItems.Single(i => i.Header == "Linear").Command.Execute(null);

            Assert.Equal("Linear ▾", ribbon.ParameterButton);
            Assert.True(model.IsCommandRunning);
            Assert.StartsWith("Specify start point or [Name/Label/Chain/Description/Palette/Value set]", model.Prompt);
        }

        [Fact]
        public void TheActionMenu_ListsEveryKind_AndAnItemStartsBactionWithItsKindAnswered()
        {
            MainViewModel model = Edit(Plan(out _));
            BlockEditorRibbonViewModel ribbon = Ribbon(model);

            Assert.Equal(
                new[] { "Move", "Scale", "Stretch", "Polar Stretch", "Rotate", "Flip", "Array", "Lookup" },
                ribbon.ActionItems.Select(i => i.Header)
            );

            ribbon.ActionItems.Single(i => i.Header == "Stretch").Command.Execute(null);

            Assert.Equal("Stretch ▾", ribbon.ActionButton);
            Assert.Equal("Select parameter:", model.Prompt);

            // A kind of two words is answered by its first: Polar Stretch is BACTION's Polar stretch.
            model.Session.Escape();
            ribbon.ActionItems.Single(i => i.Header == "Polar Stretch").Command.Execute(null);
            Assert.Equal("Polar Stretch ▾", ribbon.ActionButton);
            Assert.Equal("Select parameter:", model.Prompt);
        }

        [Fact]
        public void AParameterPickedByItsGlyph_ShowsItsPropertiesInThePalette_AndARowTypedIsOneUndoStep()
        {
            CadDocument document = Plan(out _);
            MainViewModel model = Edit(document);
            Width(model);
            string handle = document.BlockEditor.Elements[0].Handle;

            document.Selection.Set(new[] { handle });

            Assert.Equal("Linear parameter (1)", model.SelectionLabel);
            Assert.Equal(
                new[] { "Misc", "Geometry", "Property labels", "Value set" },
                model.PropertyGroups.Select(g => g.Title)
            );
            PropertyGroupViewModel labels = model.PropertyGroups.Single(g => g.Title == "Property labels");
            Assert.Equal("Distance1", labels.Rows.Single(r => r.Label == "Distance name").Value);
            Assert.Equal(
                "100.0000",
                model.PropertyGroups.Single(g => g.Title == "Geometry").Rows.Single(r => r.Label == "Distance").Value
            );

            labels.Rows.Single(r => r.Label == "Distance name").Value = "Width";

            Assert.Equal("Width", document.BlockEditor.Elements[0].Labels[0]);
            Assert.Equal("Properties", document.NextUndo.Name);
            Assert.True(document.Undo());
            Assert.Equal("Distance1", document.BlockEditor.Elements[0].Labels[0]);
        }

        [Fact]
        public void AValueSetTypedInThePalette_IsWrittenInPlace_AndAnInsertSnapsToIt()
        {
            CadDocument document = Plan(out string[] lines);
            MainViewModel model = Edit(document);
            Width(model);
            model.StartCommand("BACTION");
            model.Execute("sTretch");
            model.Execute("50,0");
            model.Execute(string.Empty);
            model.Execute("90,-10");
            model.Execute("110,60");
            model.Session.FeedSelection(lines.Take(3).ToArray());
            model.Execute(string.Empty);
            model.Execute(string.Empty);
            document.Selection.Set(new[] { document.BlockEditor.Elements[0].Handle });

            model.PropertyGroups.Single(g => g.Title == "Value set").Rows.Single(r => r.Label == "Dist type").Value =
                "Increment";
            PropertyGroupViewModel set = model.PropertyGroups.Single(g => g.Title == "Value set");
            set.Rows.Single(r => r.Label == "Dist increment").Value = "10";
            set.Rows.Single(r => r.Label == "Dist minimum").Value = "50";
            model.PropertyGroups.Single(g => g.Title == "Value set").Rows.Single(r => r.Label == "Dist maximum").Value =
                "300";
            model.Execute("BSAVE");
            model.Execute("BCLOSE");

            DxfBlockReference insert = document.Database.ModelSpace.OfType<DxfBlockReference>().First();
            DxfDynamicBlockReferenceProperty property = insert.DynamicBlockReferencePropertyCollection.First(p =>
                p.PropertyName == "Distance1"
            );
            property.Value = 77.0;
            Assert.Equal(80.0, (double)property.Value, 9);
        }

        [Fact]
        public void AnActionSelected_ShowsItsMultiplierAndOffset_AndTheMultiplierSet_IsKept()
        {
            CadDocument document = Plan(out string[] lines);
            MainViewModel model = Edit(document);
            Width(model);
            model.StartCommand("BACTION");
            model.Execute("Move");
            model.Execute("50,0");
            model.Execute(string.Empty);
            model.Session.FeedSelection(new[] { lines[0] });
            model.Execute(string.Empty);
            model.Execute(string.Empty);

            document.Selection.Set(new[] { document.BlockEditor.Elements[1].Handle });

            Assert.Equal("Move action (1)", model.SelectionLabel);
            PropertyGroupViewModel action = model.PropertyGroups.Single(g => g.Title == "Action");
            Assert.Equal("1.0000", action.Rows.Single(r => r.Label == "Distance multiplier").Value);
            Assert.Equal(
                "1",
                model
                    .PropertyGroups.Single(g => g.Title == "Misc")
                    .Rows.Single(r => r.Label == "Number of objects")
                    .Value
            );

            action.Rows.Single(r => r.Label == "Distance multiplier").Value = "0.5";

            Assert.Equal(
                "0.5000",
                model
                    .PropertyGroups.Single(g => g.Title == "Action")
                    .Rows.Single(r => r.Label == "Distance multiplier")
                    .Value
            );
        }

        [Fact]
        public void TheVisibilityStatesDialog_ListsTheStates_AndNewRenameMoveSetCurrentAndDeleteChangeThem()
        {
            CadDocument document = Plan(out _);
            MainViewModel model = Edit(document);
            model.StartCommand("BPARAMETER");
            model.Execute("Visibility");
            model.Execute("50,-20");
            VisibilityStatesViewModel dialog = null;
            model.ShowVisibilityStates = shown => dialog = shown;

            model.StartCommand("BVSTATE");

            Assert.NotNull(dialog);
            Assert.Equal(new[] { "VisibilityState0" }, dialog.States);
            Assert.Equal("VisibilityState0", dialog.Current);

            dialog.Name = "Open";
            dialog.How = VisibilityStatesViewModel.HideAll;
            dialog.NewCommand.Execute(null);
            Assert.Equal(new[] { "VisibilityState0", "Open" }, dialog.States);
            Assert.Equal("Open", dialog.Selected);

            dialog.MoveUpCommand.Execute(null);
            Assert.Equal(new[] { "Open", "VisibilityState0" }, dialog.States);

            dialog.Name = "Closed";
            dialog.RenameCommand.Execute(null);
            Assert.Equal(new[] { "Closed", "VisibilityState0" }, dialog.States);

            // The first state is the one shown until another is set, so the deleted one is the second.
            dialog.Selected = "VisibilityState0";
            dialog.SetCurrentCommand.Execute(null);
            Assert.Equal("VisibilityState0", dialog.Current);
            dialog.Selected = "Closed";
            dialog.DeleteCommand.Execute(null);
            Assert.Equal(new[] { "VisibilityState0" }, dialog.States);

            dialog.DeleteCommand.Execute(null);
            Assert.Equal("Cannot delete the current visibility state.", dialog.Message);
        }

        [Fact]
        public void TheAuthoringPalettes_ListTheParametersAndActions_AndAToolStartsItsCommand()
        {
            MainViewModel model = Edit(Plan(out _));
            AuthoringPaletteViewModel palette = null;
            model.ShowAuthoringPalette = shown => palette = shown;

            Ribbon(model).AuthoringPalettesCommand.Execute(null);

            Assert.NotNull(palette);
            Assert.Equal(10, palette.Parameters.Count);
            Assert.Equal(8, palette.Actions.Count);
            Assert.False(palette.Parameters.Single(p => p.Name == "Lookup").IsEnabled);

            palette.Parameters.Single(p => p.Name == "Polar").Command.Execute(null);

            Assert.True(model.IsCommandRunning);
            Assert.StartsWith("Specify start point or", model.Prompt);
        }

        [Fact]
        public void AClickOnAGlyph_SelectsTheParameterOrAction_WhereNoObjectLies_AndAClickAwayLetsItGo()
        {
            CadDocument document = Plan(out string[] lines);
            MainViewModel model = Edit(document);
            Width(model);
            model.StartCommand("BACTION");
            model.Execute("Move");
            model.Execute("50,0");
            model.Execute(string.Empty);
            model.Session.FeedSelection(new[] { lines[0] });
            model.Execute(string.Empty);
            model.Execute("70,-30");
            CadSpace space = document.CurrentSpace;

            // The label of the parameter stands clear of every line, as does the action's bolt.
            string picked = CadPicking.Pick(document, space, 50.0, -20.0, 1.0, false);

            Assert.Equal(document.BlockEditor.Elements[0].Handle, picked);
            Assert.Equal(new[] { picked }, document.Selection.Handles);
            Assert.Equal("Linear parameter (1)", model.SelectionLabel);
            document.Selection.Clear();

            Assert.Equal(
                document.BlockEditor.Elements[1].Handle,
                CadPicking.Pick(document, space, 70.0, -30.0, 1.0, false)
            );
            Assert.Equal("Move action (1)", model.SelectionLabel);

            Assert.Null(CadPicking.Pick(document, space, 300.0, 300.0, 1.0, false));
            Assert.Equal(0, document.Selection.Count);
        }

        [Fact]
        public void ZoomExtentsInTheBlockEditor_TakesInTheGlyphsAndTheirLabels()
        {
            CadDocument document = Plan(out _);
            MainViewModel model = Edit(document);
            DisplayBox before = CadPicking.Extents(document, document.CurrentSpace);
            Width(model);

            DisplayBox after = CadPicking.Extents(document, document.CurrentSpace);

            Assert.True(before.MinY >= 0.0);
            Assert.True(after.MinY <= -20.0);
            Assert.True(after.MaxY >= before.MaxY);
        }

        [Fact]
        public void TestBlock_OpensTheBlockAsItStandsInADrawingOfItsOwn_WithNothingElseInModelSpace()
        {
            CadDocument document = Plan(out _);
            MainViewModel model = Edit(document);
            Width(model);
            int inserts = document.Database.ModelSpace.OfType<DxfBlockReference>().Count();

            Ribbon(model).TestBlockCommand.Execute(null);

            Assert.NotSame(document, model.Document);
            Assert.Equal("Test Block Window - PANEL", model.Document.Title);
            DxfBlockReference test = Assert.Single(model.Document.Database.ModelSpace.OfType<DxfBlockReference>());
            Assert.True(test.IsDynamicBlock);
            Assert.Equal(1, inserts);
        }
    }
}
