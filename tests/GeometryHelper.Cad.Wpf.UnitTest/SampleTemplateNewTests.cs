using System.Linq;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// NEW through Select Template with the settings of a fresh install: the dialog opens in the DrawingTemplates folder beside
    /// the program and lists the sample template GeometryHelper-mm.dwt that ships there with its description, and opening it
    /// starts an untitled Drawing1.dwg with the layers, styles and variables of the template and nothing drawn.
    /// </summary>
    public sealed class SampleTemplateNewTests
    {
        private static MainViewModel Model()
        {
            var model = new MainViewModel();
            model.ApplySettings(new GlobalSettings());
            return model;
        }

        [Fact]
        public void SelectTemplate_ListsTheSampleTemplate_WithItsDescription_InTheDrawingTemplatesFolder()
        {
            MainViewModel model = Model();
            SelectTemplateViewModel shown = null;
            model.ShowSelectTemplate = dialog =>
            {
                shown = dialog;
                return false;
            };

            model.NewFromTemplateCommand.Execute(null);

            Assert.Equal(CadSupportFolders.Default.FolderOf(CadSupportKind.DrawingTemplates), shown.Folder);
            Assert.Contains("GeometryHelper-mm.dwt", shown.Templates.Select(t => t.Name));
            Assert.False(shown.IsEmpty);
            SelectTemplateRow sample = shown.Templates.First(t => t.Name == "GeometryHelper-mm.dwt");
            shown.SelectedTemplate = sample;
            Assert.Equal(CadTemplate.SampleDescription, shown.SelectedDescription);
        }

        [Fact]
        public void OpeningTheSampleTemplate_StartsAnUntitledDrawing_WithItsLayersStylesAndVariables()
        {
            MainViewModel model = Model();
            model.ShowSelectTemplate = dialog =>
            {
                dialog.SelectedTemplate = dialog.Templates.First(t => t.Name == "GeometryHelper-mm.dwt");
                dialog.Open();
                return true;
            };

            CadDocument document = model.NewDrawingChoosingTemplate();

            Assert.NotNull(document);
            Assert.Equal("Drawing1.dwg", model.Title);
            Assert.False(document.HasFile);
            DxfDatabase database = document.Database;
            Assert.Equal(
                new[]
                {
                    "0",
                    "Axis",
                    "Outline",
                    "Thin",
                    "Hidden",
                    "Dimension",
                    "Text",
                    "Hatch",
                    "Rebar",
                    "Title block",
                    "Viewport",
                    "Defpoints",
                },
                database.LayerTable.Select(l => l.Name)
            );
            Assert.Equal(new[] { "Standard", "Notes", "Titles", "Arial" }, database.TextStyleTable.Select(t => t.Name));
            Assert.Contains(database.DimStyleTable, s => s.Name == "GH");
            Assert.Contains(database.DimStyleTable, s => s.Name == "GH-Annotative");
            Assert.Equal("GH", database.Dimstyle);
            Assert.Equal("0", database.Clayer);
            Assert.Equal(DxfMeasurementValue.Metric, database.Measurement);
            Assert.Equal(DxfUnits.Millimeters, database.Insunits);
            Assert.Equal(new[] { "Model", "A3" }, database.Layouts.Select(l => l.LayoutName));
            Assert.Empty(database.ModelSpace);
            Assert.Contains("Started from the template GeometryHelper-mm.dwt.", model.History);
        }
    }
}
