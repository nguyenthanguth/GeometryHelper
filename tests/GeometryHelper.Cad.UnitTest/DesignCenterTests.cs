using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// DesignCenter (ADCENTER, ADCCLOSE): what another drawing holds listed by kind, and copied into the drawing open (layers,
    /// linetypes, text, dimension, multileader and table styles, blocks, layouts), a thing the drawing has by the name kept as
    /// it is, each copy one undo step.
    /// </summary>
    public sealed class DesignCenterTests : IDisposable
    {
        private readonly string _folder = Path.Combine(
            Path.GetTempPath(),
            "gk",
            "layer3-tests",
            Guid.NewGuid().ToString("N")
        );

        public DesignCenterTests()
        {
            Directory.CreateDirectory(_folder);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_folder, true);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        /// <summary>
        /// A library drawing: layers ROAD (colour 3, linetype GAS, "Public roads") and WALL (colour 1); the linetype GAS of 12 on
        /// and 6 off; the text style ARCH in romans.shx; the block TREE (a circle on ROAD, comments "Tree 3 m"); the layout Plan
        /// with a line; the dimension style SHOP (text 5), the multileader style NOTE (text 7) and the table style SCHEDULE
        /// (data text 9).
        /// </summary>
        private static CadDocument Library()
        {
            var database = new DxfDatabase("AC1027");
            var gas = new DxfLinetypeTableRecord
            {
                Name = "GAS",
                AsciiDescription = "Gas ---- GAS ----",
                NumDashes = 2,
            };
            gas.SetDashLengthAt(0, 12.0);
            gas.SetDashLengthAt(1, -6.0);
            gas.PatternLength = 18.0;
            database.LinetypeTable.Add(gas);
            database.LayerTable.Add(new DxfLayerTableRecord { Name = "ROAD", ColorIndex = 3 });
            database.LayerTable["ROAD"].LinetypeName = "GAS";
            database.LayerTable["ROAD"].Description = "Public roads";
            database.LayerTable.Add(new DxfLayerTableRecord { Name = "WALL", ColorIndex = 1 });
            database.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = "ARCH", FileName = "romans.shx" });
            var tree = new DxfBlockTableRecord
            {
                Name = "TREE",
                Origin = new GeoPoint3(0, 0, 0),
                Comments = "Tree 3 m",
            };
            database.BlockTable.Add(tree);
            tree.AppendEntity(
                new DxfCircle(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), 1500.0) { Layer = "ROAD" }
            );
            DxfLayout plan = new DxfLayout { LayoutName = "Plan" };
            plan.AddToLayoutDictionary(database, null);
            plan.Initialize();
            plan.BlockTableRecord.AppendEntity(new DxfLine(new GeoPoint3(10, 10, 0), new GeoPoint3(100, 50, 0)));
            CadDocument document = CadDocument.From(database, "library.dxf");
            Assert.True(
                CadDimStyleEditor
                    .Add(document, "SHOP", "Standard", new Dictionary<string, object> { ["Dimtxt"] = 5.0 })
                    .Succeeded
            );
            Assert.True(CadMLeaderStyleEditor.Add(document, "NOTE", null).Succeeded);
            CadMLeaderStyleSettings note = CadMLeaderStyleSettings.Of(CadMLeaderStyleEditor.Find(database, "NOTE"));
            note.TextHeight = 7.0;
            Assert.True(CadMLeaderStyleEditor.Apply(document, "NOTE", note).Succeeded);
            Assert.True(CadTableStyleEditor.Add(document, "SCHEDULE", null).Succeeded);
            CadTableStyleSettings schedule = CadTableStyleSettings.Of(CadTableStyleEditor.Find(database, "SCHEDULE"));
            schedule.Data.TextHeight = 9.0;
            Assert.True(CadTableStyleEditor.Apply(document, "SCHEDULE", schedule).Succeeded);
            return document;
        }

        /// <summary>A drawing to copy into: layer WALL of colour 5, a line on it, and the layout Layout1.</summary>
        private static CadDocument Site()
        {
            var database = new DxfDatabase("AC1027");
            database.LayerTable.Add(new DxfLayerTableRecord { Name = "WALL", ColorIndex = 5 });
            database.ModelSpace.AppendEntity(
                new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)) { Layer = "WALL" }
            );
            return CadDocument.From(database, "site.dxf");
        }

        private CadDesignCenterDrawing LibraryFile(string name = "library.dxf")
        {
            string path = Path.Combine(_folder, name);
            Library().Database.Save(path);
            return CadDesignCenterDrawing.Open(path);
        }

        private static string[] Names(CadDesignCenterDrawing drawing, CadDesignCenterKind kind) =>
            drawing.Items(kind).Select(i => i.Name).ToArray();

        [Fact]
        public void ADrawingListsWhatItHoldsByKind_InOrdinalOrder_WithTheDescriptionsItShows()
        {
            CadDesignCenterDrawing library = LibraryFile();

            Assert.Equal("library.dxf", library.Name);
            Assert.Equal(new[] { "TREE" }, Names(library, CadDesignCenterKind.Blocks));
            Assert.Equal("Tree 3 m", library.Items(CadDesignCenterKind.Blocks)[0].Description);
            Assert.Contains("SHOP", Names(library, CadDesignCenterKind.Dimstyles));
            Assert.Equal(new[] { "0", "ROAD", "WALL" }, Names(library, CadDesignCenterKind.Layers));
            Assert.Equal("Public roads", library.Items(CadDesignCenterKind.Layers)[1].Description);
            Assert.Equal(new[] { "Layout1", "Plan" }, Names(library, CadDesignCenterKind.Layouts));
            Assert.Equal(new[] { "Continuous", "GAS" }, Names(library, CadDesignCenterKind.Linetypes));
            Assert.Equal("Gas ---- GAS ----", library.Items(CadDesignCenterKind.Linetypes)[1].Description);
            Assert.Contains("NOTE", Names(library, CadDesignCenterKind.Multileaderstyles));
            Assert.Contains("SCHEDULE", Names(library, CadDesignCenterKind.Tablestyles));
            Assert.Contains("ARCH", Names(library, CadDesignCenterKind.Textstyles));
            Assert.Empty(Names(library, CadDesignCenterKind.Xrefs));
        }

        [Fact]
        public void ADwgIsListedAsAFileOfDxfIs()
        {
            string path = Path.Combine(_folder, "library.dwg");
            CadDrawingFile.Save(Library().Database, path);
            Assert.True(CadDrawingFile.IsDwg(path));

            CadDesignCenterDrawing library = CadDesignCenterDrawing.Open(path);

            Assert.Equal(new[] { "0", "ROAD", "WALL" }, Names(library, CadDesignCenterKind.Layers));
            Assert.Equal(new[] { "TREE" }, Names(library, CadDesignCenterKind.Blocks));
        }

        [Fact]
        public void AnOpenDrawingIsListedToo_AndALayerOfItCopiedBringsItsLinetype()
        {
            CadDocument library = Library();
            CadDesignCenterDrawing open = CadDesignCenterDrawing.Of(library, "library.dxf");
            string before = library.Database.ToText();
            CadDocument site = Site();

            CadDesignCenterResult result = CadDesignCenter.Add(
                site,
                open,
                CadDesignCenterKind.Layers,
                new[] { "ROAD" }
            );

            Assert.True(result.Succeeded);
            Assert.Equal(new[] { "ROAD" }, result.Added);
            Assert.Equal(3, site.Database.LayerTable["ROAD"].Color.ColorIndex);
            Assert.Equal("GAS", site.Database.LayerTable["ROAD"].LinetypeName);
            Assert.Equal("Public roads", site.Database.LayerTable["ROAD"].Description);
            Assert.Equal(12.0, site.Database.LinetypeTable["GAS"].DashLengthAt(0), 6);
            Assert.Equal(before, library.Database.ToText());
        }

        [Fact]
        public void Layers_AreCopiedWithTheirSettings_ALayerTheDrawingHasIsKept_AndOneUndoTakesThemBack()
        {
            CadDocument site = Site();
            string start = site.Database.ToText();

            CadDesignCenterResult result = CadDesignCenter.Add(
                site,
                LibraryFile(),
                CadDesignCenterKind.Layers,
                new[] { "ROAD", "wall", "ROAD" }
            );

            Assert.Equal(new[] { "ROAD" }, result.Added);
            Assert.Equal(new[] { "wall" }, result.Kept);
            Assert.Equal(5, site.Database.LayerTable["WALL"].Color.ColorIndex);
            Assert.Equal(3, site.Database.LayerTable["ROAD"].Color.ColorIndex);
            Assert.Equal("ADCENTER", site.NextUndo.Name);
            Assert.True(site.Undo());
            Assert.Equal(start, site.Database.ToText());
        }

        [Fact]
        public void Linetypes_AreCopiedWithTheirDashes_AndTextStylesWithTheirFont()
        {
            CadDocument site = Site();
            CadDesignCenterDrawing library = LibraryFile();

            Assert.Equal(
                new[] { "GAS" },
                CadDesignCenter
                    .Add(site, library, CadDesignCenterKind.Linetypes, new[] { "GAS", "Continuous" })
                    .Added.ToArray()
            );
            Assert.Equal(
                new[] { "ARCH" },
                CadDesignCenter.Add(site, library, CadDesignCenterKind.Textstyles, new[] { "ARCH" }).Added.ToArray()
            );

            DxfLinetypeTableRecord gas = site.Database.LinetypeTable["GAS"];
            Assert.Equal(2, gas.NumDashes);
            Assert.Equal(-6.0, gas.DashLengthAt(1), 6);
            Assert.Equal("romans.shx", site.Database.TextStyleTable["ARCH"].FileName);
            CadDesignCenterResult again = CadDesignCenter.Add(
                site,
                library,
                CadDesignCenterKind.Linetypes,
                new[] { "GAS" }
            );
            Assert.Empty(again.Added);
            Assert.Equal(new[] { "GAS" }, again.Kept);
        }

        [Fact]
        public void ABlock_IsCopiedWithWhatItDrawsOn_NoInsertMade_AndTheLibraryDrawingLeftAlone()
        {
            CadDocument library = Library();
            CadDesignCenterDrawing open = CadDesignCenterDrawing.Of(library, "library.dxf");
            string before = library.Database.ToText();
            CadDocument site = Site();
            int entities = site.Database.ModelSpace.Count();

            CadDesignCenterResult result = CadDesignCenter.Add(
                site,
                open,
                CadDesignCenterKind.Blocks,
                new[] { "TREE" }
            );

            Assert.True(result.Succeeded, string.Concat(result.Failed));
            Assert.Equal(new[] { "TREE" }, result.Added);
            DxfBlockTableRecord tree = site.Database.BlockTable["TREE"];
            DxfCircle circle = Assert.IsType<DxfCircle>(Assert.Single(tree));
            Assert.Equal("ROAD", circle.Layer);
            Assert.True(site.Database.LayerTable.Has("ROAD"));
            Assert.Equal(entities, site.Database.ModelSpace.Count());
            Assert.Equal(before, library.Database.ToText());
        }

        [Fact]
        public void ALayout_IsCopiedWithItsObjects_AndTakesAnotherNameWhereTheDrawingHasIt()
        {
            CadDocument site = Site();
            CadDesignCenterDrawing library = LibraryFile();

            CadDesignCenterResult first = CadDesignCenter.Add(
                site,
                library,
                CadDesignCenterKind.Layouts,
                new[] { "Plan" }
            );
            CadDesignCenterResult second = CadDesignCenter.Add(
                site,
                library,
                CadDesignCenterKind.Layouts,
                new[] { "Plan" }
            );

            Assert.Equal(new[] { "Plan" }, first.Added);
            Assert.Equal(new[] { "Layout2-Plan" }, second.Added);
            Assert.Equal(new[] { "Model", "Layout1", "Plan", "Layout2-Plan" }, site.Spaces.Select(s => s.Name));
            Assert.IsType<DxfLine>(site.Spaces[2].Layout.BlockTableRecord.OfType<DxfLine>().Single());
        }

        [Fact]
        public void ADimensionStyle_IsCopiedWithItsVariables()
        {
            CadDocument site = Site();

            CadDesignCenterResult result = CadDesignCenter.Add(
                site,
                LibraryFile(),
                CadDesignCenterKind.Dimstyles,
                new[] { "SHOP" }
            );

            Assert.True(result.Succeeded, string.Concat(result.Failed));
            Assert.Equal(5.0, site.Database.DimStyleTable["SHOP"].Dimtxt, 6);
        }

        [Fact]
        public void AMultileaderStyle_AndATableStyle_AreCopiedWithTheirSettings()
        {
            CadDocument site = Site();
            CadDesignCenterDrawing library = LibraryFile();

            CadDesignCenterResult note = CadDesignCenter.Add(
                site,
                library,
                CadDesignCenterKind.Multileaderstyles,
                new[] { "NOTE" }
            );
            CadDesignCenterResult schedule = CadDesignCenter.Add(
                site,
                library,
                CadDesignCenterKind.Tablestyles,
                new[] { "SCHEDULE" }
            );

            Assert.True(note.Succeeded, string.Concat(note.Failed));
            Assert.True(schedule.Succeeded, string.Concat(schedule.Failed));
            Assert.Equal(
                7.0,
                CadMLeaderStyleSettings.Of(CadMLeaderStyleEditor.Find(site.Database, "NOTE")).TextHeight,
                6
            );
            Assert.Equal(
                9.0,
                CadTableStyleSettings.Of(CadTableStyleEditor.Find(site.Database, "SCHEDULE")).Data.TextHeight,
                6
            );
        }

        [Fact]
        public void ANameTheLibraryHasNot_IsSaidAsFailed_AndAnXrefIsNotCopied()
        {
            CadDocument site = Site();
            CadDesignCenterDrawing library = LibraryFile();

            CadDesignCenterResult missing = CadDesignCenter.Add(
                site,
                library,
                CadDesignCenterKind.Layers,
                new[] { "NOWHERE" }
            );
            CadDesignCenterResult xref = CadDesignCenter.Add(
                site,
                library,
                CadDesignCenterKind.Xrefs,
                new[] { "GRID" }
            );

            Assert.False(missing.Succeeded);
            Assert.Equal("The drawing library.dxf has no layer \"NOWHERE\".", missing.Failed[0]);
            Assert.False(xref.Succeeded);
            Assert.StartsWith("An external reference is attached", xref.Failed[0]);
            Assert.False(site.Database.LayerTable.Has("NOWHERE"));
        }

        [Fact]
        public void AFileThatCannotBeRead_IsRefused_WithTheReason()
        {
            string bad = Path.Combine(_folder, "broken.dwg");
            File.WriteAllText(bad, "not a drawing");

            Assert.ThrowsAny<Exception>(() => CadDesignCenterDrawing.Open(bad));
            Assert.Throws<FileNotFoundException>(() =>
                CadDesignCenterDrawing.Open(Path.Combine(_folder, "missing.dxf"))
            );
        }

        [Fact]
        public void AdcenterAndAdcclose_AskTheWindow_ThroughTheAliasesDcAndAdcToo()
        {
            var asked = new List<string>();
            CadDocument document = Site();
            CadCommandSession session = Session(document);
            session.UiRequested += (sender, e) =>
            {
                asked.Add(e.Name);
                e.Handled = true;
            };

            Run(session, "ADCENTER", "DC", "ADC", "DCENTER", "ADCCLOSE");

            Assert.Equal(new[] { "ADCENTER", "ADCENTER", "ADCENTER", "ADCENTER", "ADCCLOSE" }, asked);
        }

        [Fact]
        public void WithNoWindow_TheCommandsSayThereIsNoPalette()
        {
            CadCommandSession session = Run(Session(Site()), "ADCENTER");
            Assert.Equal("DesignCenter is a palette of the window; there is none to show.", session.History.Last());
            Run(session, "ADCCLOSE");
            Assert.Equal("DesignCenter is a palette of the window; there is none to close.", session.History.Last());
        }
    }
}
