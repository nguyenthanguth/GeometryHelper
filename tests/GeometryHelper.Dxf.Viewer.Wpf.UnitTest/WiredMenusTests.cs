using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The menu items, buttons and dialogs that said "not built" and are wired now: Convert PS to MS of the Default menu
    /// (CHSPACE), the hatch's Set Boundary and Generate Boundary, the dimension menu's Edit Constraint, Dimension Name Format
    /// and Dimension Style, the title bar's search box, and the % buttons of the attribute dialogs; what stays grey says why.
    /// </summary>
    public sealed class WiredMenusTests
    {
        private static CadMenuItemViewModel Item(IEnumerable<CadMenuItemViewModel> items, string header) =>
            items.Single(i => i.Header == header);

        private static MainViewModel Open(DxfDatabase database = null)
        {
            var model = new MainViewModel();
            model.OpenDocument(CadDocument.From(database ?? new DxfDatabase("AC1032"), "wired.dxf"));
            return model;
        }

        /// <summary>A model with a line from (0, 0) to (10, 1) given a horizontal constraint, its dimension on the hidden layer; the dimension's handle.</summary>
        private static string ConstrainedLine(out MainViewModel model)
        {
            model = Open();
            model.Execute("LINE");
            model.Execute("0,0");
            model.Execute("10,1");
            model.Execute(string.Empty);
            model.Execute("DCHORIZONTAL");
            model.Execute("0,0");
            model.Execute("10,1");
            model.Execute("5,4");
            model.Execute(string.Empty);
            return model.Document.ModelSpace.Block.OfType<DxfDimension>().Single().Handle;
        }

        [Fact]
        public void ConvertPsToMs_IsLive_AndStartsChspace_WhichRefusesTheModelTab()
        {
            MainViewModel model = Open();

            CadMenuItemViewModel convert = Item(model.CanvasMenu(), "Convert PS to MS");
            Assert.True(convert.IsEnabled);
            convert.Command.Execute(null);

            Assert.Contains("** Command not allowed in Model Tab **", model.Session.History);
        }

        [Fact]
        public void TheHatchMenu_SetBoundaryAndGenerateBoundary_StartTheirCommandsOnTheHatch()
        {
            var database = new DxfDatabase("AC1027");

            foreach (var (a, b) in new[] { (0, 1), (1, 2), (2, 3), (3, 0) })
            {
                GeoPoint3[] corners =
                {
                    new GeoPoint3(0, 0, 0),
                    new GeoPoint3(100, 0, 0),
                    new GeoPoint3(100, 60, 0),
                    new GeoPoint3(0, 60, 0),
                };
                database.ModelSpace.AppendEntity(new DxfLine(corners[a], corners[b]));
            }

            MainViewModel model = Open(database);
            model.Execute("HATCH");
            model.Execute("5,5");
            model.Execute(string.Empty);
            DxfHatch hatch = model.Document.ModelSpace.Block.OfType<DxfHatch>().Single();
            model.Document.Selection.Set(new[] { hatch.Handle });

            CadMenuItemViewModel set = Item(model.CanvasMenu(), "Set Boundary");
            CadMenuItemViewModel generate = Item(model.CanvasMenu(), "Generate Boundary");
            Assert.True(set.IsEnabled);
            Assert.True(generate.IsEnabled);

            set.Command.Execute(null);
            Assert.Equal("HATCHSETBOUNDARY", model.Session.Current.Name);
            Assert.Equal("Select objects to be used for the new boundary:", model.Session.PromptText);
            model.Escape();

            int polylines = model.Document.ModelSpace.Block.OfType<DxfPolyline>().Count();
            generate.Command.Execute(null);

            Assert.False(model.IsCommandRunning);
            Assert.Equal(polylines + 1, model.Document.ModelSpace.Block.OfType<DxfPolyline>().Count());
        }

        [Fact]
        public void ADimensionThatIsNoConstraint_HasItsConstraintItemsGrey_WithTheReason()
        {
            MainViewModel model = Open();
            model.Execute("DIMLINEAR");
            model.Execute("0,0");
            model.Execute("1000,0");
            model.Execute("500,-300");
            model.Document.Selection.Set(
                new[] { model.Document.ModelSpace.Block.OfType<DxfDimension>().Single().Handle }
            );

            IReadOnlyList<CadMenuItemViewModel> items = model.CanvasMenu();
            CadMenuItemViewModel edit = Item(items, "Edit Constraint");
            CadMenuItemViewModel format = Item(items, "Dimension Name Format");

            Assert.False(edit.IsEnabled);
            Assert.Contains("DCCONVERT", edit.ToolTip, StringComparison.Ordinal);
            Assert.False(format.IsEnabled);
            Assert.Equal(
                new[] { "Value", "Name", "Name and Expression" },
                format.Items.Select(i => i.Header).ToArray()
            );
        }

        [Fact]
        public void EditConstraint_StartsParametersEditOnTheName_AndTheExpressionTypedMovesTheLine()
        {
            string dimension = ConstrainedLine(out MainViewModel model);
            model.Document.Selection.Set(new[] { dimension });

            CadMenuItemViewModel edit = Item(model.CanvasMenu(), "Edit Constraint");
            Assert.True(edit.IsEnabled);
            edit.Command.Execute(null);

            Assert.Equal("-PARAMETERS", model.Session.Current.Name);
            Assert.Equal("Enter expression:", model.Session.PromptText);

            model.Execute("25");

            DxfLine line = model.Document.ModelSpace.Block.OfType<DxfLine>().Single();
            Assert.Equal(25.0, line.EndPoint.X - line.StartPoint.X, 6);
        }

        [Fact]
        public void DimensionNameFormat_GivesTheConstraintsWordsTheNameTheValueOrBoth_AndKeepsThemWhenTheValueChanges()
        {
            string dimension = ConstrainedLine(out MainViewModel model);
            model.Document.Selection.Set(new[] { dimension });
            string Words() => ((DxfDimension)model.Document.FindEntity(dimension)).DimensionText;

            // The words as the number they hold, whatever the decimals the dimension reads: "30.00" is 30.
            double Number(string words) => double.Parse(words, System.Globalization.CultureInfo.InvariantCulture);
            Assert.StartsWith("d1=10", Words(), StringComparison.Ordinal);

            CadMenuItemViewModel format = Item(model.CanvasMenu(), "Dimension Name Format");
            Assert.True(format.IsEnabled);

            Item(format.Items, "Name").Command.Execute(null);
            Assert.Equal("d1", Words());

            Item(Item(model.CanvasMenu(), "Dimension Name Format").Items, "Value").Command.Execute(null);
            Assert.Equal(10.0, Number(Words()), 9);

            // A new value is shown in the format chosen, and the format is read back from the words of a file saved and opened again.
            Assert.True(CadParameters.Edit(model.Document, "d1", "30").Succeeded);
            Assert.Equal(30.0, Number(Words()), 9);
            var again = CadDocument.From(DxfDatabase.Parse(model.Document.Database.ToText()), "again.dxf");
            Assert.True(CadParameters.Edit(again, "d1", "40").Succeeded);
            Assert.Equal(40.0, Number(again.ModelSpace.Block.OfType<DxfDimension>().Single().DimensionText), 9);

            Item(Item(model.CanvasMenu(), "Dimension Name Format").Items, "Name and Expression").Command.Execute(null);
            Assert.StartsWith("d1=30", Words(), StringComparison.Ordinal);

            // One undo step each.
            Assert.True(model.Document.Undo());
            Assert.Equal(30.0, Number(Words()), 9);
        }

        [Fact]
        public void SaveAsNewStyle_OfOneDimension_AsksTheNameOnTheCommandLine_AndKeepsTheStyleWithItsOverrides()
        {
            MainViewModel model = Open();
            model.Execute("DIMLINEAR");
            model.Execute("0,0");
            model.Execute("1000,0");
            model.Execute("500,-300");
            string handle = model.Document.ModelSpace.Block.OfType<DxfDimension>().Single().Handle;
            model.Document.Selection.Set(new[] { handle });

            CadMenuItemViewModel menu = Item(model.CanvasMenu(), "Dimension Style");
            CadMenuItemViewModel save = Item(menu.Items, "Save as New Style...");
            Assert.True(save.IsEnabled);
            save.Command.Execute(null);

            Assert.Equal("-DIMSTYLE", model.Session.Current.Name);
            Assert.StartsWith("Enter name for new dimension style", model.Session.PromptText);

            model.Execute("Mine");

            Assert.True(model.Document.Database.DimStyleTable.Has("Mine"));
            Assert.Equal("Mine", CadDimStyleEditor.Current(model.Document.Database));
            Assert.Equal("Other...", menu.Items.Last().Header);
        }

        [Fact]
        public void TheTitleBarSearch_ListsTheCommandsForTheWords_AndEnterStartsTheFirstAndEmptiesTheBox()
        {
            MainViewModel model = Open();
            Assert.False(model.IsTitleSearching);
            Assert.Empty(model.TitleSearchResults);

            model.TitleSearchText = " circle ";

            Assert.True(model.IsTitleSearching);
            Assert.Equal("circle", model.TitleSearchText);
            Assert.Contains(model.TitleSearchResults, r => r.Command == "CIRCLE");

            CommandSuggestionViewModel row = model.TitleSearchResults.First(r => !r.IsHeader);
            Assert.True(model.RunTitleSearchResult());

            Assert.Equal(row.Command, model.Session.Current.Name);
            Assert.False(model.IsTitleSearching);
            Assert.Empty(model.TitleSearchResults);
            Assert.False(model.RunTitleSearchResult());
        }

        [Fact]
        public void TheTitleBarSearch_FindsNothing_WhereNoDrawingIsOpen()
        {
            var model = new MainViewModel { TitleSearchText = "circle" };

            Assert.True(model.IsTitleSearching);
            Assert.Empty(model.TitleSearchResults);
        }

        [Fact]
        public void InsertFieldOfTheAttributeDefinitionDialog_PutsTheFieldInTheDefault_AndTheDefinitionHoldsIt()
        {
            MainViewModel model = Open(CadTemplate.CreateMetric());
            model.ShowField = dialog =>
            {
                dialog.Close(CadFieldAction.Ok);
                return true;
            };
            string code = null;
            model.ShowAttributeDefinition = dialog =>
            {
                Assert.True(dialog.CanInsertField);
                code = dialog.AskField();
                dialog.Request.Tag = "DATED";
                dialog.Default = code;
                dialog.SpecifyOnScreen = false;
                dialog.X = "10";
                dialog.Y = "20";
                dialog.Close(CadDialogAction.Ok);
                return true;
            };

            model.Execute("ATTDEF");

            Assert.StartsWith("%<\\AcVar Date", code, StringComparison.Ordinal);
            DxfAttributeDefinition definition = model
                .Document.ModelSpace.Block.OfType<DxfAttributeDefinition>()
                .Single();
            Assert.True(CadFields.Has(definition));
            Assert.Equal(code, CadFields.CodesOf(definition));
            Assert.DoesNotContain("%<", definition.TextString, StringComparison.Ordinal);
        }

        [Fact]
        public void TheAttributeDialogs_HaveNoInsertFieldWhereTheWindowShowsNoFieldDialog()
        {
            MainViewModel model = Open(CadTemplate.CreateMetric());
            bool can = true;
            model.ShowAttributeDefinition = dialog =>
            {
                can = dialog.CanInsertField;
                dialog.Close(CadDialogAction.Cancel);
                return true;
            };

            model.Execute("ATTDEF");

            Assert.False(can);
        }

        [Fact]
        public void TheBlockEditorsConstraintButtons_StartTheirCommands_AndBlockTableAndConstructionAreTheOnesGrey()
        {
            DxfDatabase database = CadTemplate.CreateMetric();
            var plate = new DxfBlockTableRecord { Name = "PLATE", Origin = new GeoPoint3(0, 0, 0) };
            database.BlockTable.Add(plate);
            plate.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 10, 0)));
            database.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(0, 0, 0), plate));
            MainViewModel model = Open(database);
            model.Execute("-BEDIT");
            model.Execute("PLATE");
            var ribbon = (BlockEditorRibbonViewModel)model.BlockEditorTab.TabContent;

            ribbon.AutoConstrainCommand.Execute(null);
            Assert.Equal("AUTOCONSTRAIN", model.Session.Current.Name);
            model.Escape();

            ribbon.LinearConstraintCommand.Execute(null);
            Assert.Equal("DCLINEAR", model.Session.Current.Name);
            model.Escape();

            ribbon.DeleteConstraintsCommand.Execute(null);
            Assert.Equal("DELCONSTRAINT", model.Session.Current.Name);
            model.Escape();

            ribbon.ParameterManagerCommand.Execute(null);
            Assert.False(model.IsCommandRunning);
        }
    }
}
