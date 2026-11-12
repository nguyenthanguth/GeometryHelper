using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The External References palette's Unload, Reload and Detach on every row selected, each action one undo step, a row
    /// the action refuses told in AutoCAD 2021's words (probes xrefs3-1 and xrefs3-2); and a name with a wildcard character
    /// (a#1, a@b, a.b, ~t, [x], a-b) acted on whole, as AutoCAD's backquote escape makes -XREF take it.
    /// </summary>
    public class XrefSelectionActionTests
    {
        /// <summary>A drawing of a line on layer GRID from (0, 0) to (1 000, 0), saved as xref\{name}.dxf.</summary>
        private static string Drawing(TestFolder folder, string name, Action<DxfDatabase> more = null)
        {
            DxfDatabase database = CadTemplate.CreateMetric();
            database.LayerTable.Add(new DxfLayerTableRecord { Name = "GRID" });
            database.ModelSpace.AppendEntity(
                new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(1000, 0, 0)) { Layer = "GRID" }
            );
            more?.Invoke(database);
            Directory.CreateDirectory(Path.Combine(folder.FullPath, "xref"));
            return folder.Write(Path.Combine("xref", name + ".dxf"), database);
        }

        /// <summary>A PNG of 4 by 3 pixels, saved as {name}.png.</summary>
        private static string Logo(TestFolder folder, string name = "logo")
        {
            string path = Path.Combine(folder.FullPath, name + ".png");
            var bitmap = System.Windows.Media.Imaging.BitmapSource.Create(
                4,
                3,
                96,
                96,
                System.Windows.Media.PixelFormats.Gray8,
                null,
                new byte[12],
                4
            );
            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));

            using (FileStream stream = File.Create(path))
            {
                encoder.Save(stream);
            }

            return path;
        }

        /// <summary>The window's view model over plan-level2.dxf, each file given attached at the origin under the name before its =.</summary>
        private static MainViewModel Plan(TestFolder folder, params string[] attached)
        {
            var model = new MainViewModel();
            Assert.True(model.Open(folder.Write("plan-level2.dxf", CadTemplate.CreateMetric())));

            foreach (string path in attached)
            {
                foreach (string answer in new[] { "-XREF", "A", path, "0,0", string.Empty, string.Empty, string.Empty })
                {
                    model.Execute(answer);
                }
            }

            return model;
        }

        private static ExternalReferencesViewModel Palette(MainViewModel model)
        {
            ExternalReferencesViewModel palette = null;
            model.ShowExternalReferences = shown => palette = shown;
            model.Execute("ER");
            Assert.NotNull(palette);
            return palette;
        }

        private static ReferenceRowViewModel Row(ExternalReferencesViewModel palette, string name) =>
            palette.Rows.Single(r => r.Name == name);

        /// <summary>Selects the rows of the names, as Ctrl+click does, the first the one shown.</summary>
        private static void Select(ExternalReferencesViewModel palette, params string[] names)
        {
            palette.Selected = Row(palette, names[0]);
            palette.SelectedRows = names.Select(n => Row(palette, n)).ToList();
        }

        private static string[] StatusOf(ExternalReferencesViewModel palette, params string[] names) =>
            names.Select(n => Row(palette, n).StatusText).ToArray();

        [Fact]
        public void Unload_OnTwoRowsSelected_UnloadsBoth_InOneUndoStep()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Plan(folder, Drawing(folder, "grid"), Drawing(folder, "axes"));
                string attached = model.Document.Database.ToText();
                ExternalReferencesViewModel palette = Palette(model);
                Select(palette, "grid", "axes");

                Assert.True(palette.UnloadCommand.CanExecute(null));
                palette.UnloadCommand.Execute(null);

                Assert.Equal(new[] { "Unloaded", "Unloaded" }, StatusOf(palette, "grid", "axes"));
                Assert.Contains("Enter xref name(s) to unload: axes,grid", model.History);
                Assert.Equal("-XREF", model.Document.NextUndo.Name);
                Assert.True(model.Document.Undo());
                Assert.Equal(attached, model.Document.Database.ToText());
            }
        }

        [Fact]
        public void Reload_OnTwoRowsSelected_ReloadsBoth_InOneUndoStep()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Plan(folder, Drawing(folder, "grid"), Drawing(folder, "axes"));
                ExternalReferencesViewModel palette = Palette(model);
                Select(palette, "grid", "axes");
                palette.UnloadCommand.Execute(null);
                string unloaded = model.Document.Database.ToText();
                Assert.Equal(new[] { "Unloaded", "Unloaded" }, StatusOf(palette, "grid", "axes"));
                Select(palette, "grid", "axes");

                palette.ReloadCommand.Execute(null);

                Assert.Equal(new[] { "Loaded", "Loaded" }, StatusOf(palette, "grid", "axes"));
                Assert.Contains("Enter xref name(s) to reload: axes,grid", model.History);
                Assert.True(model.Document.Undo());
                Assert.Equal(unloaded, model.Document.Database.ToText());
            }
        }

        [Fact]
        public void Detach_OnTwoRowsSelected_DetachesBoth_InOneUndoStep()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Plan(
                    folder,
                    Drawing(folder, "grid"),
                    Drawing(folder, "axes"),
                    Drawing(folder, "keep")
                );
                string attached = model.Document.Database.ToText();
                ExternalReferencesViewModel palette = Palette(model);
                Select(palette, "grid", "axes");

                palette.DetachCommand.Execute(null);

                Assert.Equal(new[] { "plan-level2", "keep" }, palette.Rows.Select(r => r.Name).ToArray());
                Assert.False(model.Document.Database.BlockTable.Has("grid"));
                Assert.False(model.Document.Database.BlockTable.Has("axes"));
                Assert.Equal("-XREF", model.Document.NextUndo.Name);
                Assert.True(model.Document.Undo());
                Assert.Equal(attached, model.Document.Database.ToText());
            }
        }

        [Fact]
        public void Detach_OnAnXrefAndAnImageSelected_IsOneUndoStep()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Plan(folder, Drawing(folder, "grid"));

                foreach (string answer in new[] { "-IMAGE", "A", Logo(folder), "10,20", string.Empty, string.Empty })
                {
                    model.Execute(answer);
                }

                string attached = model.Document.Database.ToText();
                ExternalReferencesViewModel palette = Palette(model);
                Select(palette, "grid", "logo");

                palette.DetachCommand.Execute(null);

                Assert.Equal(new[] { "plan-level2" }, palette.Rows.Select(r => r.Name).ToArray());
                Assert.True(model.Document.Undo());
                Assert.Equal(attached, model.Document.Database.ToText());
                palette.Refresh();
                Assert.Equal(new[] { "plan-level2", "grid", "logo" }, palette.Rows.Select(r => r.Name).ToArray());
            }
        }

        [Fact]
        public void Unload_OnAnImageAndAnXrefSelected_UnloadsBoth()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Plan(folder, Drawing(folder, "grid"));

                foreach (string answer in new[] { "-IMAGE", "A", Logo(folder), "10,20", string.Empty, string.Empty })
                {
                    model.Execute(answer);
                }

                ExternalReferencesViewModel palette = Palette(model);
                Select(palette, "grid", "logo");

                palette.UnloadCommand.Execute(null);

                Assert.Equal(new[] { "Unloaded", "Unloaded" }, StatusOf(palette, "grid", "logo"));
            }
        }

        [Fact]
        public void AnUnloadedRowSelectedWithALoadedOne_IsLeftAlone_WhenUnloaded()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Plan(folder, Drawing(folder, "grid"), Drawing(folder, "axes"));
                ExternalReferencesViewModel palette = Palette(model);
                Select(palette, "axes");
                palette.UnloadCommand.Execute(null);
                int said = model.History.Count;
                Select(palette, "grid", "axes");

                palette.UnloadCommand.Execute(null);

                Assert.Equal(new[] { "Unloaded", "Unloaded" }, StatusOf(palette, "grid", "axes"));
                Assert.DoesNotContain(model.History.Skip(said), l =>
                    l.StartsWith("Xref \"axes\"", StringComparison.Ordinal)
                );
                Assert.Contains(model.History.Skip(said), l => l == "\"grid\" is unloaded.");
            }
        }

        [Fact]
        public void ARowAlreadyUnloaded_SelectedAlone_IsAnsweredAsAutoCadAnswers()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Plan(folder, Drawing(folder, "grid"));
                ExternalReferencesViewModel palette = Palette(model);
                Select(palette, "grid");
                palette.UnloadCommand.Execute(null);
                int said = model.History.Count;

                palette.UnloadCommand.Execute(null);

                Assert.Contains("No matching and loaded xref names found.", model.History.Skip(said));
            }
        }

        /// <summary>A plan drawing placing the nested column, attached as plan next to grid; both in the host.</summary>
        private static MainViewModel Nested(TestFolder folder)
        {
            string column = Drawing(folder, "column");
            Assert.True(File.Exists(column));
            string plan = Drawing(folder, "plan", d =>
                d.ModelSpace.AppendEntity(
                    new DxfBlockReference(new GeoPoint3(0, 5000, 0), d.AttachXref("column.dxf", "column"))
                )
            );
            return Plan(folder, Drawing(folder, "grid"), plan);
        }

        [Fact]
        public void ANestedRowSelectedWithAnOwnOne_IsRefusedOnDetach_InAutoCadsWords_AndSkippedOnUnload()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Nested(folder);
                ExternalReferencesViewModel palette = Palette(model);
                Assert.Equal(
                    new[] { "plan-level2", "grid", "plan", "column" },
                    palette.Rows.Select(r => r.Name).ToArray()
                );

                Select(palette, "column");
                Assert.False(palette.DetachCommand.CanExecute(null));
                Assert.False(palette.UnloadCommand.CanExecute(null));
                Assert.False(palette.ReloadCommand.CanExecute(null));

                Select(palette, "grid", "column");
                Assert.True(palette.UnloadCommand.CanExecute(null));
                palette.UnloadCommand.Execute(null);
                Assert.Equal(new[] { "Unloaded", "Loaded" }, StatusOf(palette, "grid", "column"));

                Select(palette, "grid", "column");
                palette.DetachCommand.Execute(null);

                Assert.Equal(new[] { "plan-level2", "plan", "column" }, palette.Rows.Select(r => r.Name).ToArray());
                Assert.Contains("Xref column has multiple references. Not detached.", model.History);
                Assert.Contains("\"plan\"", model.History);
                Assert.False(model.Document.Database.BlockTable.Has("grid"));
            }
        }

        /// <summary>Names AutoCAD reads as wildcards: a digit, a letter, any other character, anything but, one of, a range.</summary>
        private static readonly string[] Wild = { "a#1", "a@b", "a.b", "~t", "[x]", "a-b", "ab" };

        private static MainViewModel Wildcards(TestFolder folder)
        {
            string grid = Drawing(folder, "grid");
            var model = new MainViewModel();
            Assert.True(model.Open(folder.Write("plan-level2.dxf", CadTemplate.CreateMetric())));

            foreach (string name in Wild)
            {
                foreach (
                    string answer in new[]
                    {
                        "-XREF",
                        "A",
                        name + "=" + grid,
                        "0,0",
                        string.Empty,
                        string.Empty,
                        string.Empty,
                    }
                )
                {
                    model.Execute(answer);
                }
            }

            return model;
        }

        [Fact]
        public void ANameWithAWildcardCharacter_IsUnloadedAndReloadedAlone()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Wildcards(folder);
                ExternalReferencesViewModel palette = Palette(model);
                Assert.Equal(Wild.Length, palette.Rows.Count(r => !r.IsCurrent));

                foreach (string name in Wild)
                {
                    Select(palette, name);
                    palette.UnloadCommand.Execute(null);
                    Assert.Equal(
                        Wild.Select(n => n == name ? "Unloaded" : "Loaded").ToArray(),
                        StatusOf(palette, Wild)
                    );

                    Select(palette, name);
                    palette.ReloadCommand.Execute(null);
                    Assert.Equal(Wild.Select(n => "Loaded").ToArray(), StatusOf(palette, Wild));
                }
            }
        }

        [Fact]
        public void ANameWithAWildcardCharacter_HasItsPathTypeChangedAndItsPathSetAlone()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Wildcards(folder);
                ExternalReferencesViewModel palette = Palette(model);
                string other = Drawing(folder, "other");

                string full = Path.GetFullPath(Path.Combine(folder.FullPath, "xref", "grid.dxf"));
                string[] Paths() => Wild.Select(n => model.Document.Database.BlockTable[n].PathName).ToArray();

                foreach (string name in Wild)
                {
                    string[] before = Paths();
                    Select(palette, name);
                    palette.MakeAbsoluteCommand.Execute(null);
                    Assert.Equal(Wild.Select((n, i) => n == name ? full : before[i]).ToArray(), Paths());

                    before = Paths();
                    Select(palette, name);
                    palette.SetPath(other);
                    Assert.Equal(Wild.Select((n, i) => n == name ? other : before[i]).ToArray(), Paths());
                }
            }
        }

        [Fact]
        public void TwoNamesWithWildcardCharacters_AreDetachedAndBoundAlone()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Wildcards(folder);
                ExternalReferencesViewModel palette = Palette(model);
                model.ShowXrefBind = dialog => dialog.Close(CadDialogAction.Ok);
                Select(palette, "a#1", "~t");

                palette.BindCommand.Execute(null);

                Assert.Equal(
                    new[] { "plan-level2", "[x]", "a-b", "a.b", "a@b", "ab" }.OrderBy(n => n, StringComparer.Ordinal),
                    palette.Rows.Select(r => r.Name).OrderBy(n => n, StringComparer.Ordinal)
                );
                Assert.False(model.Document.Database.BlockTable["a#1"].IsFromExternalReference);
                Assert.False(model.Document.Database.BlockTable["~t"].IsFromExternalReference);

                Select(palette, "a.b", "[x]");
                palette.DetachCommand.Execute(null);

                Assert.False(model.Document.Database.BlockTable.Has("a.b"));
                Assert.False(model.Document.Database.BlockTable.Has("[x]"));
                Assert.True(model.Document.Database.BlockTable["a-b"].IsFromExternalReference);
                Assert.True(model.Document.Database.BlockTable["ab"].IsFromExternalReference);
                Assert.True(model.Document.Database.BlockTable["a@b"].IsFromExternalReference);
            }
        }

        [Fact]
        public void AnImageWithAWildcardCharacter_IsUnloadedAlone()
        {
            using (var folder = new TestFolder())
            {
                var model = new MainViewModel();
                Assert.True(model.Open(folder.Write("plan-level2.dxf", CadTemplate.CreateMetric())));
                string logo = Logo(folder);

                foreach (string name in new[] { "i#1", "i1" })
                {
                    foreach (
                        string answer in new[] { "-IMAGE", "A", name + "=" + logo, "10,20", string.Empty, string.Empty }
                    )
                    {
                        model.Execute(answer);
                    }
                }

                ExternalReferencesViewModel palette = Palette(model);
                Select(palette, "i#1");

                palette.UnloadCommand.Execute(null);

                Assert.Equal(new[] { "Unloaded", "Loaded" }, StatusOf(palette, "i#1", "i1"));
            }
        }
    }
}
