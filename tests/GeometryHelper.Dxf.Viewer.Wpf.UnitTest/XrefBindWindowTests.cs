using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The palette's Bind… and the Bind Xrefs/DGN Underlays dialog, to the design (XrefBindMenu, XrefBind, XrefBindRefused):
    /// offered for a loaded drawing, greyed for one unloaded or not found and for an image; the bind type started from
    /// BINDTYPE and written back on OK; every row selected bound as -XREF Bind binds it, in one undo step; a refusal shown in
    /// a box with the lines -XREF Bind writes on the command line. Driven with no window, then each window laid out on a
    /// thread of its own.
    /// </summary>
    public class XrefBindWindowTests
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

        /// <summary>A PNG of 4 by 3 pixels, saved as logo.png.</summary>
        private static string Logo(TestFolder folder)
        {
            string path = Path.Combine(folder.FullPath, "logo.png");
            var bitmap = System.Windows.Media.Imaging.BitmapSource.Create(
                4,
                3,
                96,
                96,
                PixelFormats.Gray8,
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

        /// <summary>The window's view model over plan-level2.dxf, each drawing given attached at the origin.</summary>
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

        private static double BindType(MainViewModel model) =>
            model.Session.Remembered.TryGetValue("bindtype", out double value) ? value : 0.0;

        [Fact]
        public void Bind_IsOfferedForALoadedDrawing_AndGreyedForOneUnloadedOrNotFound_AndForAnImage()
        {
            using (var folder = new TestFolder())
            {
                string grid = Drawing(folder, "grid");
                MainViewModel model = Plan(folder, grid);
                string logo = Logo(folder);

                foreach (string answer in new[] { "-IMAGE", "A", logo, "10,20", string.Empty, string.Empty })
                {
                    model.Execute(answer);
                }

                ExternalReferencesViewModel palette = Palette(model);

                palette.Selected = Row(palette, "grid");
                Assert.True(palette.BindCommand.CanExecute(null));
                palette.Selected = Row(palette, "logo");
                Assert.False(palette.BindCommand.CanExecute(null));
                palette.Selected = Row(palette, "plan-level2");
                Assert.False(palette.BindCommand.CanExecute(null));

                // A selection holding one loaded drawing is offered Bind…, though an image is selected with it.
                palette.Selected = Row(palette, "logo");
                palette.SelectedRows = new[] { Row(palette, "grid"), Row(palette, "logo") };
                Assert.True(palette.BindCommand.CanExecute(null));

                palette.Selected = Row(palette, "grid");
                palette.UnloadCommand.Execute(null);
                Assert.Equal("Unloaded", Row(palette, "grid").StatusText);
                Assert.False(palette.BindCommand.CanExecute(null));

                palette.ReloadCommand.Execute(null);
                File.Delete(grid);
                palette.Refresh();
                Assert.Equal("Not Found", Row(palette, "grid").StatusText);
                Assert.False(palette.BindCommand.CanExecute(null));
            }
        }

        [Fact]
        public void Ok_StartsFromBindType_WritesTheChoiceBack_AndBindsAsXrefBind_InOneUndoStep()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Plan(folder, Drawing(folder, "grid"));
                string attached = model.Document.Database.ToText();
                ExternalReferencesViewModel palette = Palette(model);
                palette.Selected = Row(palette, "grid");
                XrefBindViewModel shown = null;
                model.ShowXrefBind = dialog =>
                {
                    shown = dialog;
                    Assert.True(dialog.IsBind);
                    Assert.False(dialog.IsInsert);
                    Assert.Equal("Binding: grid", dialog.NamesText);
                    dialog.IsInsert = true;
                    dialog.Close(CadDialogAction.Ok);
                };
                model.ShowXrefBindRefused = refused => Assert.Fail("Nothing should be refused.");

                palette.BindCommand.Execute(null);

                Assert.NotNull(shown);
                Assert.Equal(1.0, BindType(model));
                DxfBlockTableRecord block = model.Document.Database.BlockTable["grid"];
                Assert.False(block.IsFromExternalReference);
                Assert.True(model.Document.Database.LayerTable.Has("GRID"));
                Assert.DoesNotContain("grid$0$", model.Document.Database.ToText(), StringComparison.Ordinal);
                Assert.Contains("Enter xref name(s) to bind: grid", model.History);
                Assert.DoesNotContain(palette.Rows, r => r.Name == "grid");

                Assert.Equal("-XREF", model.Document.NextUndo.Name);
                Assert.True(model.Document.Undo());
                Assert.Equal(attached, model.Document.Database.ToText());

                // The next time, the dialog starts from Insert, the choice kept by BINDTYPE.
                palette.Refresh();
                palette.Selected = Row(palette, "grid");
                model.ShowXrefBind = dialog =>
                {
                    Assert.True(dialog.IsInsert);
                    dialog.IsBind = true;
                    dialog.Close(CadDialogAction.Ok);
                };
                palette.BindCommand.Execute(null);
                Assert.Equal(0.0, BindType(model));
                Assert.True(model.Document.Database.LayerTable.Has("grid$0$GRID"));
            }
        }

        [Fact]
        public void Cancel_ChangesNothing_NeitherTheDrawingNorBindType()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Plan(folder, Drawing(folder, "grid"));
                string attached = model.Document.Database.ToText();
                ExternalReferencesViewModel palette = Palette(model);
                palette.Selected = Row(palette, "grid");
                model.ShowXrefBind = dialog =>
                {
                    dialog.IsInsert = true;
                    dialog.Close(CadDialogAction.Cancel);
                };

                palette.BindCommand.Execute(null);

                Assert.Equal(0.0, BindType(model));
                Assert.True(model.Document.Database.BlockTable["grid"].IsFromExternalReference);
                Assert.Equal(attached, model.Document.Database.ToText());
                Assert.DoesNotContain("Enter xref name(s) to bind: grid", model.History);
            }
        }

        [Fact]
        public void TwoRowsSelected_AreBoundByOneDialog_InOneUndoStep()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Plan(folder, Drawing(folder, "grid"), Drawing(folder, "axes"));
                string attached = model.Document.Database.ToText();
                ExternalReferencesViewModel palette = Palette(model);
                palette.Selected = Row(palette, "grid");
                palette.SelectedRows = new[] { Row(palette, "grid"), Row(palette, "axes") };
                int dialogs = 0;
                model.ShowXrefBind = dialog =>
                {
                    dialogs++;
                    Assert.Equal(new[] { "axes", "grid" }, dialog.Names.ToArray());
                    Assert.Equal("Binding: axes, grid", dialog.NamesText);
                    dialog.Close(CadDialogAction.Ok);
                };

                palette.BindCommand.Execute(null);

                Assert.Equal(1, dialogs);
                Assert.Contains("Enter xref name(s) to bind: axes,grid", model.History);
                Assert.False(model.Document.Database.BlockTable["grid"].IsFromExternalReference);
                Assert.False(model.Document.Database.BlockTable["axes"].IsFromExternalReference);
                Assert.True(model.Document.Database.LayerTable.Has("grid$0$GRID"));
                Assert.True(model.Document.Database.LayerTable.Has("axes$0$GRID"));
                Assert.Equal(new[] { "plan-level2" }, palette.Rows.Select(r => r.Name).ToArray());

                Assert.True(model.Document.Undo());
                Assert.Equal(attached, model.Document.Database.ToText());
            }
        }

        [Fact]
        public void ANestedRow_IsOfferedBind_AndOkRefusesIt_InTheBoxAndOnTheCommandLine()
        {
            using (var folder = new TestFolder())
            {
                Drawing(folder, "column");
                string gridBase = Drawing(folder, "grid-base", d =>
                    d.ModelSpace.AppendEntity(
                        new DxfBlockReference(new GeoPoint3(0, 5000, 0), d.AttachXref("column.dxf", "column"))
                    )
                );
                MainViewModel model = Plan(folder, gridBase);
                string attached = model.Document.Database.ToText();
                ExternalReferencesViewModel palette = Palette(model);
                palette.Selected = Row(palette, "column");
                Assert.True(palette.BindCommand.CanExecute(null));
                XrefBindRefusedViewModel box = null;
                model.ShowXrefBind = dialog => dialog.Close(CadDialogAction.Ok);
                model.ShowXrefBindRefused = refused => box = refused;

                palette.BindCommand.Execute(null);

                Assert.NotNull(box);
                Assert.Equal(new[] { "Xref \"column\" is nested and cannot be bound." }, box.Lines.ToArray());
                Assert.Equal(
                    "Select its parent, grid-base, and bind that: column is bound with it, as grid-base$0$… and column$0$…. The same lines are on the command line.",
                    box.Hint
                );
                Assert.Contains("Xref \"column\" is nested and cannot be bound.", model.History);
                Assert.Equal(attached, model.Document.Database.ToText());
            }
        }

        [Fact]
        public void AnUnloadedRowSelectedWithALoadedOne_RefusesBoth_InAutoCadsWords()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Plan(folder, Drawing(folder, "grid"), Drawing(folder, "annex"));
                ExternalReferencesViewModel palette = Palette(model);
                palette.Selected = Row(palette, "annex");
                palette.UnloadCommand.Execute(null);
                string unloaded = model.Document.Database.ToText();
                palette.Selected = Row(palette, "grid");
                palette.SelectedRows = new[] { Row(palette, "grid"), Row(palette, "annex") };
                Assert.True(palette.BindCommand.CanExecute(null));
                XrefBindRefusedViewModel box = null;
                model.ShowXrefBind = dialog => dialog.Close(CadDialogAction.Ok);
                model.ShowXrefBindRefused = refused => box = refused;

                palette.BindCommand.Execute(null);

                Assert.Equal(
                    new[] { "Cannot bind unloaded xref(s).  Reload them first:", "Xref \"annex\" is unloaded." },
                    box.Lines.ToArray()
                );
                Assert.Equal(
                    "Reload annex, then bind again. No reference of the selection was bound. The same lines are on the command line.",
                    box.Hint
                );
                Assert.Contains("Cannot bind unloaded xref(s).  Reload them first:", model.History);
                Assert.Equal(unloaded, model.Document.Database.ToText());
            }
        }

        [Fact]
        public void ADrawingWhoseNestedFileIsNotFound_IsRefused_TheNestedOneNamed()
        {
            using (var folder = new TestFolder())
            {
                string detail = Drawing(folder, "detail-B");
                string sheet = Drawing(folder, "sheet", d =>
                    d.ModelSpace.AppendEntity(
                        new DxfBlockReference(new GeoPoint3(0, 5000, 0), d.AttachXref("detail-B.dxf", "detail-B"))
                    )
                );
                MainViewModel model = Plan(folder, sheet);
                File.Delete(detail);
                string attached = model.Document.Database.ToText();
                ExternalReferencesViewModel palette = Palette(model);
                palette.Selected = Row(palette, "sheet");
                XrefBindRefusedViewModel box = null;
                model.ShowXrefBind = dialog => dialog.Close(CadDialogAction.Ok);
                model.ShowXrefBindRefused = refused => box = refused;

                palette.BindCommand.Execute(null);

                Assert.Equal(
                    new[] { "xref \"detail-B\" is currently not found. Reload or detach before binding." },
                    box.Lines.ToArray()
                );
                Assert.Equal(
                    "The file of detail-B, or of a reference it places, is not found. Select New Path, Reload or Detach it, then bind again. The same lines are on the command line.",
                    box.Hint
                );
                Assert.Equal(attached, model.Document.Database.ToText());
            }
        }

        [Fact]
        public void InsertWithABlockTheDrawingHas_SaysItOnTheCommandLineOnly_AndShowsNoBox()
        {
            using (var folder = new TestFolder())
            {
                string grid = Drawing(folder, "grid", d =>
                {
                    var tag = new DxfBlockTableRecord { Name = "TAG" };
                    d.BlockTable.Add(tag);
                    tag.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(0, 10, 0)));
                    d.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(500, 0, 0), tag));
                });
                var model = new MainViewModel();
                DxfDatabase host = CadTemplate.CreateMetric();
                host.BlockTable.Add(new DxfBlockTableRecord { Name = "TAG" });
                Assert.True(model.Open(folder.Write("plan-level2.dxf", host)));

                foreach (string answer in new[] { "-XREF", "A", grid, "0,0", string.Empty, string.Empty, string.Empty })
                {
                    model.Execute(answer);
                }

                ExternalReferencesViewModel palette = Palette(model);
                palette.Selected = Row(palette, "grid");
                model.ShowXrefBind = dialog =>
                {
                    dialog.IsInsert = true;
                    dialog.Close(CadDialogAction.Ok);
                };
                model.ShowXrefBindRefused = refused => Assert.Fail("A duplicate block is no refusal.");

                palette.BindCommand.Execute(null);

                Assert.Contains("Duplicate Block \"TAG\" ignored.", model.History);
                Assert.False(model.Document.Database.BlockTable["grid"].IsFromExternalReference);
            }
        }

        [Fact]
        public void TheTextAndFontsPage_SaysWhatHiddenMessagesKeep_AndOffersTheThreeAnswers()
        {
            GlobalSettingsViewModel dialog = new MainViewModel().CreateGlobalSettings("Text and fonts");

            Assert.Equal(
                "The font of the app, what draws a font of a drawing not found (FONTALT, the font mapping file and the substitutions), and the answers of hidden messages (AutoCAD’s Hidden Message Settings): set one back to Ask, or Reset to defaults, to be asked again. A font not found is drawn in GeometryHelper Simplex (ghsimplex), the presenter's own single-stroke font, by default.",
                dialog.Intro
            );
            GlobalSettingSection hidden = Assert.Single(dialog.Sections, s => s.Title == "Hidden messages");
            GlobalSettingRow row = Assert.Single(hidden.Rows);
            Assert.Equal("Esc in the MTEXT editor with changes", row.Label);
            Assert.Equal(new[] { "Ask", "Yes, save the changes", "No, discard the changes" }, row.Options.ToArray());
            Assert.Equal("Ask", row.Value);
        }

        [Fact]
        public void TheDialogTheRefusalAndTheMenu_AreDrawnAsTheDesign()
        {
            OnSta(() =>
            {
                _ = PackUriHelper.UriSchemePack;

                var dialog = new XrefBindWindow(new XrefBindViewModel(new[] { "grid-base" }, 0));
                var refusal = new XrefBindRefusedWindow(
                    new XrefBindRefusedViewModel(
                        new[] { "Cannot bind unloaded xref(s).  Reload them first:", "Xref \"annex\" is unloaded." },
                        "Reload annex, then bind again."
                    )
                );

                foreach (
                    (Window window, FrameworkElement body) in new (Window, FrameworkElement)[]
                    {
                        (dialog, dialog.Body),
                        (refusal, refusal.Body),
                    }
                )
                {
                    Style title = (Style)window.FindResource("GhTitleCloseButton");
                    object context = window.DataContext;
                    window.Content = null;
                    body.DataContext = context;
                    Layout(body);
                    Button cross = Assert.Single(Descendants(body).OfType<Button>(), b => b.Style == title);
                    Assert.Equal(
                        Color.FromRgb(0xF2, 0x6B, 0x3A),
                        ((SolidColorBrush)Descendants(cross).OfType<IconView>().Single().Stroke).Color
                    );
                    Assert.Contains(
                        "Bind Xrefs/DGN Underlays",
                        Descendants(body).OfType<TextBlock>().Select(t => t.Text)
                    );
                    window.Close();
                }

                List<string> texts = Descendants(dialog.Body).OfType<TextBlock>().Select(t => t.Text).ToList();
                Assert.Contains("Bind Type", texts);
                Assert.Contains("Binding: grid-base", texts);
                Assert.Contains(
                    "Names renamed after the reference: layer WALL of grid-base becomes grid-base$0$WALL.",
                    texts
                );
                Assert.Contains(
                    "Merged by name, as if detached and inserted: WALL stays WALL, the drawing's own kept.",
                    texts
                );
                List<RadioButton> radios = Descendants(dialog.Body).OfType<RadioButton>().ToList();
                Assert.Equal(new[] { "Bind", "Insert" }, radios.Select(r => (string)r.Content).ToArray());
                Assert.Equal(new bool?[] { true, false }, radios.Select(r => r.IsChecked).ToArray());
                Assert.Equal(
                    new[] { "Cancel", "OK" },
                    Descendants(dialog.Body)
                        .OfType<Button>()
                        .Where(b => b.Content is string)
                        .Select(b => (string)b.Content)
                        .ToArray()
                );

                List<string> refusalTexts = Descendants(refusal.Body).OfType<TextBlock>().Select(t => t.Text).ToList();
                Assert.Contains("Nothing was bound", refusalTexts);
                Assert.Contains("Cannot bind unloaded xref(s).  Reload them first:", refusalTexts);
                Assert.Contains("Xref \"annex\" is unloaded.", refusalTexts);
                Assert.Contains("Reload annex, then bind again.", refusalTexts);
                Assert.Equal(
                    new[] { "OK" },
                    Descendants(refusal.Body)
                        .OfType<Button>()
                        .Where(b => b.Content is string)
                        .Select(b => (string)b.Content)
                        .ToArray()
                );

                using (var folder = new TestFolder())
                {
                    MainViewModel model = Plan(folder, Drawing(folder, "grid"));
                    var palette = new ExternalReferencesWindow(new ExternalReferencesViewModel(model));
                    var rowStyle = (Style)palette.References.ItemContainerStyle;
                    var rowMenu = (ContextMenu)
                        rowStyle
                            .Setters.OfType<Setter>()
                            .Single(s => s.Property == FrameworkElement.ContextMenuProperty)
                            .Value;
                    MenuItem bind = rowMenu.Items.OfType<MenuItem>().Last();
                    Assert.Equal("Bind…", bind.Header);
                    Assert.Equal(SelectionMode.Extended, palette.References.SelectionMode);
                    Assert.Equal(
                        "BindCommand",
                        System.Windows.Data.BindingOperations.GetBinding(bind, MenuItem.CommandProperty).Path.Path
                    );
                    palette.Close();
                }
            });
        }

        private static void Layout(FrameworkElement element)
        {
            element.Measure(new Size(1000, 800));
            element.Arrange(new Rect(0, 0, 1000, 800));
            element.UpdateLayout();
        }

        private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                yield return child;

                foreach (DependencyObject below in Descendants(child))
                {
                    yield return below;
                }
            }
        }

        private static void OnSta(Action test)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    test();
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            Assert.Null(failure);
        }
    }
}
