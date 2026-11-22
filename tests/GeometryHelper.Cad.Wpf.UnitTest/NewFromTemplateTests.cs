using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// New from template: a button beside New on the start screen and Ctrl+Shift+N both run NEW with Select Template opened in
    /// the DrawingTemplates folder beside the program, where a drawing saved as a .dwt is listed and starts an unnamed drawing
    /// with its layers. Ctrl+N stays a blank drawing. The DrawingTemplates folder of the test output is used for the files, and
    /// each test removes what it put there.
    /// </summary>
    public sealed class NewFromTemplateTests : IDisposable
    {
        private const string Label = "New from template  ·  Ctrl+Shift+N";

        private readonly List<string> _made = new List<string>();

        private static string Shipped => CadSupportFolders.Default.FolderOf(CadSupportKind.DrawingTemplates);

        public void Dispose()
        {
            foreach (string file in _made)
            {
                try
                {
                    File.Delete(file);
                }
                catch (IOException) { }
            }
        }

        /// <summary>
        /// Puts a template in the DrawingTemplates folder, so that it is the folder the settings resolve to whatever the
        /// per-user Template folder of this machine holds; the file is removed when the test ends.
        /// </summary>
        private string Template(string name, params string[] layers)
        {
            DxfDatabase database = CadTemplate.CreateMetric();

            foreach (string layer in layers)
            {
                database.LayerTable.Add(new DxfLayerTableRecord { Name = layer });
            }

            Directory.CreateDirectory(Shipped);
            string path = Path.Combine(Shipped, name);
            _made.Add(path);
            CadDocument.From(database, name).SaveAsTemplate(path, "Test template", true, null);
            return path;
        }

        private static MainViewModel Model()
        {
            var model = new MainViewModel();
            model.ApplySettings(new GlobalSettings());
            return model;
        }

        private static void OnSta(Action test)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    _ = PackUriHelper.UriSchemePack;
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

        private static void Layout(CadWorkspace workspace)
        {
            workspace.Measure(new Size(1600, 960));
            workspace.Arrange(new Rect(0, 0, 1600, 960));
            workspace.UpdateLayout();
        }

        /// <summary>A workspace over the model on a thread of its own, laid out offscreen, its window messages in a host that is never shown.</summary>
        private static void OnWorkspace(MainViewModel model, Action<CadWorkspace, HwndSource> check)
        {
            OnSta(() =>
            {
                // The workspace sets the window's own dialogs on the model, Select Template among them: the test's one goes back.
                Func<SelectTemplateViewModel, bool> selectTemplate = model.ShowSelectTemplate;
                var workspace = new CadWorkspace { DataContext = model };
                model.ShowSelectTemplate = selectTemplate;

                using (
                    var source = new HwndSource(
                        new HwndSourceParameters("NewFromTemplateTests")
                        {
                            ParentWindow = new IntPtr(-3),
                            WindowStyle = 0,
                            Width = 1600,
                            Height = 960,
                        }
                    )
                    {
                        RootVisual = workspace,
                    }
                )
                {
                    try
                    {
                        Layout(workspace);
                        check(workspace, source);
                    }
                    finally
                    {
                        workspace.DrawingCanvas.Dispose();
                    }
                }
            });
        }

        private static Button ButtonLabelled(CadWorkspace workspace, string label) =>
            Descendants(workspace).OfType<Button>().Single(b => label.Equals(b.Content));

        // ---- The start screen

        [Fact]
        public void TheStartScreen_HasANewFromTemplateButton_RightOfNew_OfTheSameStyleAndSize()
        {
            OnWorkspace(Model(), (workspace, source) =>
            {
                Button blank = ButtonLabelled(workspace, "New  ·  Ctrl+N");
                Button fromTemplate = ButtonLabelled(workspace, Label);

                Assert.True(fromTemplate.IsEnabled);
                Assert.True(fromTemplate.IsVisible);
                Assert.Equal(blank.Padding, fromTemplate.Padding);
                Assert.Equal(blank.Margin, fromTemplate.Margin);
                Assert.Equal(blank.Height, fromTemplate.Height);
                Assert.Equal(blank.ActualHeight, fromTemplate.ActualHeight, 0.5);
                Assert.Equal(blank.FontSize, fromTemplate.FontSize);
                Assert.Equal(blank.Background, fromTemplate.Background);
                Assert.Equal(blank.BorderBrush, fromTemplate.BorderBrush);

                Point blankAt = blank.TranslatePoint(new Point(0, 0), workspace);
                Point fromTemplateAt = fromTemplate.TranslatePoint(new Point(0, 0), workspace);
                Assert.True(fromTemplateAt.X >= blankAt.X + blank.ActualWidth - 0.5, "It is not right of New.");
                Assert.Equal(blankAt.Y, fromTemplateAt.Y, 0.5);
                Assert.Contains("DrawingTemplates", fromTemplate.ToolTip.ToString());
            });
        }

        [Fact]
        public void TheNewFromTemplateButton_RunsNew_WithSelectTemplateInDrawingTemplates_ListingTheDwtThere()
        {
            Template("office.dwt", "Walls");
            MainViewModel model = Model();
            SelectTemplateViewModel shown = null;
            model.ShowSelectTemplate = dialog =>
            {
                shown = dialog;
                return false;
            };

            OnWorkspace(model, (workspace, source) =>
            {
                Button button = ButtonLabelled(workspace, Label);
                Assert.True(button.Command.CanExecute(null));
                button.Command.Execute(null);
            });

            Assert.Equal(Shipped, shown.Folder);
            Assert.Equal(new[] { "office.dwt" }, shown.Templates.Select(t => t.Name));
            Assert.False(model.HasDocument);
        }

        [Fact]
        public void TheNewFromTemplateCommand_StillShowsTheDialog_WithOneTemplateThere_AndWithNone()
        {
            MainViewModel model = Model();
            var shown = new List<SelectTemplateViewModel>();
            model.ShowSelectTemplate = dialog =>
            {
                shown.Add(dialog);
                return false;
            };

            Template("only.dwt");
            model.NewFromTemplateCommand.Execute(null);

            using (var empty = new TestFolder())
            {
                model.ApplySettings(new GlobalSettings { TemplateFolder = empty.FullPath });
                model.NewFromTemplateCommand.Execute(null);
                Assert.True(shown[1].IsEmpty);
                Assert.Equal(empty.FullPath, shown[1].Folder);
            }

            Assert.Equal(
                new[] { "only.dwt" },
                Assert.IsAssignableFrom<SelectTemplateViewModel>(shown[0]).Templates.Select(t => t.Name)
            );
            Assert.Equal(shown[0].Templates[0], shown[0].SelectedTemplate);
        }

        [Fact]
        public void TheNewFromTemplateButton_OpeningTheTemplate_StartsAnUnnamedDrawing_WithItsLayers()
        {
            Template("office.dwt", "Walls", "Doors");
            MainViewModel model = Model();
            model.ShowSelectTemplate = dialog =>
            {
                dialog.Open();
                return true;
            };

            OnWorkspace(model, (workspace, source) => ButtonLabelled(workspace, Label).Command.Execute(null));

            Assert.True(model.HasDocument);
            Assert.Equal("Drawing1.dwg", model.Title);
            Assert.False(model.Document.HasFile);
            Assert.Equal(new[] { "0", "Walls", "Doors" }, model.Document.Database.LayerTable.Select(l => l.Name));
            Assert.Contains("Started from the template office.dwt.", model.History);
        }

        [Fact]
        public void WhereNoFolderIsNamed_NewsDialogStartsInDrawingTemplates_AndATemplateNamedWithNoFolderIsFoundThere()
        {
            Template("office.dwt", "Walls");
            MainViewModel model = Model();
            SelectTemplateViewModel shown = null;
            model.ShowSelectTemplate = dialog =>
            {
                shown = dialog;
                return false;
            };

            model.NewDrawing();
            model.Execute("NEW");
            Assert.Equal(Shipped, shown.Folder);
            Assert.Equal(
                Path.Combine(Shipped, "office.dwt"),
                CadTemplate.Find("office", model.Settings.EffectiveTemplateFolder)
            );

            model.Execute("FILEDIA");
            model.Execute("0");
            model.Execute("NEW");
            model.Execute("office");

            Assert.Equal("Drawing2.dwg", model.Title);
            Assert.Contains("Walls", model.Document.Database.LayerTable.Select(l => l.Name));
        }

        // ---- The keys

        [DllImport("user32.dll")]
        private static extern bool GetKeyboardState(byte[] keys);

        [DllImport("user32.dll")]
        private static extern bool SetKeyboardState(byte[] keys);

        /// <summary>Presses the key down in the window with the modifiers held, as the thread's keyboard state says them.</summary>
        private static bool Press(CadWorkspace workspace, HwndSource source, Key key, ModifierKeys modifiers)
        {
            var state = new byte[256];
            Assert.True(GetKeyboardState(state));
            byte[] before = (byte[])state.Clone();

            try
            {
                // The generic and the left keys: what WPF reads as Control and Shift.
                byte control = (modifiers & ModifierKeys.Control) != 0 ? (byte)0x80 : (byte)0;
                byte shift = (modifiers & ModifierKeys.Shift) != 0 ? (byte)0x80 : (byte)0;
                state[0x11] = state[0xA2] = control;
                state[0x10] = state[0xA0] = shift;
                Assert.True(SetKeyboardState(state));
                Assert.Equal(modifiers, Keyboard.Modifiers);

                var args = new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, key)
                {
                    RoutedEvent = Keyboard.PreviewKeyDownEvent,
                };
                workspace.RaiseEvent(args);
                return args.Handled;
            }
            finally
            {
                SetKeyboardState(before);
            }
        }

        [Fact]
        public void CtrlShiftN_OnTheStartScreen_RunsNewWithSelectTemplateInDrawingTemplates()
        {
            Template("office.dwt", "Walls");
            MainViewModel model = Model();
            var shown = new List<SelectTemplateViewModel>();
            model.ShowSelectTemplate = dialog =>
            {
                shown.Add(dialog);
                dialog.Open();
                return true;
            };

            OnWorkspace(model, (workspace, source) =>
            {
                Assert.True(Press(workspace, source, Key.N, ModifierKeys.Control | ModifierKeys.Shift));
            });

            Assert.Equal(Shipped, Assert.Single(shown).Folder);
            Assert.Equal("Drawing1.dwg", model.Title);
            Assert.Contains("Walls", model.Document.Database.LayerTable.Select(l => l.Name));
        }

        [Fact]
        public void CtrlShiftN_WithADrawingOpen_DoesTheSame_AndCtrlNStaysABlankDrawing()
        {
            Template("office.dwt", "Walls");
            MainViewModel model = Model();
            model.NewDrawing();
            var shown = new List<SelectTemplateViewModel>();
            model.ShowSelectTemplate = dialog =>
            {
                shown.Add(dialog);
                dialog.Open();
                return true;
            };

            OnWorkspace(model, (workspace, source) =>
            {
                Assert.True(Press(workspace, source, Key.N, ModifierKeys.Control));
                Assert.Empty(shown);
                Assert.Equal("Drawing2.dwg", model.Title);
                Assert.DoesNotContain("Walls", model.Document.Database.LayerTable.Select(l => l.Name));

                Assert.True(Press(workspace, source, Key.N, ModifierKeys.Control | ModifierKeys.Shift));
                Assert.Equal(Shipped, Assert.Single(shown).Folder);
                Assert.Equal("Drawing3.dwg", model.Title);
                Assert.Contains("Walls", model.Document.Database.LayerTable.Select(l => l.Name));
            });
        }

        [Fact]
        public void CtrlShiftN_IsInNoDefaultShortcutOfTheTable_SoItClashesWithNone()
        {
            var settings = new GlobalSettings();

            Assert.DoesNotContain(settings.Keys.Keys, k =>
                string.Equals(k, "Ctrl+Shift+N", StringComparison.OrdinalIgnoreCase)
            );
        }

        // ---- Saving a template

        [Fact]
        public void ADrawingSavedAsADwtThroughSaveAs_IsInDrawingTemplates_AndNewStartsFromItUnnamed()
        {
            Template("marker.dwt");
            MainViewModel model = Model();
            model.NewDrawing();
            model.Document.Database.LayerTable.Add(new DxfLayerTableRecord { Name = "Walls" });
            model.Document.Database.LayerTable.Add(new DxfLayerTableRecord { Name = "Doors" });
            string[] layers = model.Document.Database.LayerTable.Select(l => l.Name).ToArray();
            CadExportJob offered = null;
            model.SaveAsRequested += (s, e) => offered = model.CreateSaveAsDialogJob();

            model.SaveAsTemplate();

            Assert.Equal(CadExportJob.Template, offered.FileType);
            Assert.Equal(Path.Combine(Shipped, "Drawing1.dwt"), offered.FileName);
            _made.Add(offered.FileName);
            Assert.True(model.SaveAs(offered.FileName, offered.FileType));
            Assert.Equal("AC1032", System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(offered.FileName), 0, 6));

            SelectTemplateViewModel shown = null;
            model.ShowSelectTemplate = dialog =>
            {
                shown = dialog;
                dialog.SelectedTemplate = dialog.Templates.Single(t => t.Name == "Drawing1.dwt");
                dialog.Open();
                return true;
            };
            model.Execute("NEW");

            Assert.Equal(new[] { "Drawing1.dwt", "marker.dwt" }, shown.Templates.Select(t => t.Name));
            Assert.Equal("Drawing2.dwg", model.Title);
            Assert.False(model.Document.HasFile);
            Assert.Equal(layers, model.Document.Database.LayerTable.Select(l => l.Name));
            Assert.Equal(new[] { "Walls", "Doors" }, layers.Skip(layers.Length - 2));
        }

        [Fact]
        public void ATemplateInInches_WithALayoutPlan_KeepsBothWhenNewStartsFromIt()
        {
            string path = Template("inches.dwt");
            DxfDatabase database = CadTemplate.CreateImperial();
            var plan = new DxfLayout { LayoutName = "Plan" };
            plan.AddToLayoutDictionary(database, null);
            plan.Initialize();
            CadDocument.From(database, "inches.dwg").SaveAsTemplate(path, "Inches", false, null);
            MainViewModel model = Model();
            model.ShowSelectTemplate = dialog =>
            {
                dialog.Open();
                return true;
            };

            model.Execute("NEW");

            Assert.Equal("Drawing1.dwg", model.Title);
            Assert.Equal(DxfMeasurementValue.English, model.Document.Database.Measurement);
            Assert.Contains("Plan", model.SpaceTabs.Select(t => t.Name));
            Assert.False(model.Document.HasFile);
        }

        // ---- The dialog

        [Fact]
        public void TheSelectTemplateWindowOfAnEmptyFolder_SaysHowToMakeATemplate()
        {
            using (var folder = new TestFolder())
            {
                OnSta(() =>
                {
                    var none = new SelectTemplateWindow(new SelectTemplateViewModel(folder.FullPath, new string[0]));
                    none.Body.Measure(new Size(520, 440));
                    none.Body.Arrange(new Rect(0, 0, 520, 440));
                    none.Body.UpdateLayout();

                    TextBlock hint = Descendants(none.Body)
                        .OfType<TextBlock>()
                        .Single(t => t.Text.StartsWith("No template in this folder", StringComparison.Ordinal));
                    Assert.Contains("SAVEAS", hint.Text);
                    Assert.Contains(".dwt", hint.Text);
                });
            }
        }
    }
}
