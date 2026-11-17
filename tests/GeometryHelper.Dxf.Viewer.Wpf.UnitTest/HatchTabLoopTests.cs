using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO.Packaging;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The contextual tabs of the ribbon over a view model they write to and read from: a value the user chooses in a control
    /// runs one edit and returns. A stack overflow killed the app when the pattern of a hatch was chosen in the Hatch Editor tab:
    /// the tab told every property changed after the edit, the box's list was a new list at each read, so the box chose its
    /// pattern again, which ran the edit again, and so on until the stack ended. The tabs are put in a window of Windows that is
    /// only for messages (never shown), a control is driven as the user's keys drive it, and a loop is cut by the depth of the
    /// stack at each change the drawing or the tab tells, never run to its end.
    /// </summary>
    public sealed class HatchTabLoopTests
    {
        /// <summary>The most frames a change may be nested in: a loop reaches it, one edit with its binding does not.</summary>
        private const int FrameLimit = 400;

        /// <summary>A tab in a workspace in a window of messages, counting the edits of the drawing and the depth of the stack.</summary>
        private sealed class Rig : IDisposable
        {
            private readonly Func<object> _tab;
            private object _watched;

            public Rig(MainViewModel model, Func<object> tab)
            {
                Model = model;
                _tab = tab;

                // Listened to before the workspace's bindings are, so that this is the first to hear of a change.
                Watch();
                Workspace = new CadWorkspace { DataContext = model };

                // The workspace hooks every dialog of the window to the view model; a test never shows a window, so each is let go
                // (a box that offers a dialog then only says it is shown by the window).
                foreach (
                    System.Reflection.PropertyInfo hook in typeof(MainViewModel)
                        .GetProperties()
                        .Where(p => p.CanWrite && typeof(Delegate).IsAssignableFrom(p.PropertyType))
                )
                {
                    hook.SetValue(model, null);
                }

                Source = new HwndSource(
                    new HwndSourceParameters("HatchTabLoopTests")
                    {
                        ParentWindow = new IntPtr(-3),
                        WindowStyle = 0,
                        Width = 2200,
                        Height = 960,
                    }
                )
                {
                    RootVisual = Workspace,
                };
                Layout();
                model.Document.Changed += (_, e) =>
                {
                    Frames("the drawing");

                    if (e.Kind == CadChangeKind.Edit)
                    {
                        Edits++;
                    }
                };
            }

            public MainViewModel Model { get; }

            public CadWorkspace Workspace { get; }

            public HwndSource Source { get; }

            public int Edits { get; private set; }

            public int Changes { get; private set; }

            public int MaxFrames { get; private set; }

            public void Dispose() => Source.Dispose();

            public void Layout()
            {
                Workspace.Measure(new Size(2200, 960));
                Workspace.Arrange(new Rect(0, 0, 2200, 960));
                Workspace.UpdateLayout();
            }

            /// <summary>Runs what the user does once; a loop is cut where it is too deep, and the counts are kept.</summary>
            public void Act(Action action)
            {
                Edits = 0;
                Changes = 0;

                try
                {
                    action();
                    Watch();
                    Layout();
                }
                catch (InvalidOperationException e) when (e.Message.StartsWith("A loop", StringComparison.Ordinal)) { }
            }

            /// <summary>Finds the one control of a kind by its tool tip, or by the property it is bound to where <paramref name="where"/> starts with "=".</summary>
            public T Find<T>(string where)
                where T : FrameworkElement
            {
                IEnumerable<T> all = Descendants(Workspace).OfType<T>();
                return where.StartsWith("=", StringComparison.Ordinal)
                    ? all.Single(e => BoundTo(e) == where.Substring(1))
                    : all.Single(e => where.Equals(e.ToolTip));
            }

            private static string BoundTo(FrameworkElement element)
            {
                DependencyProperty property =
                    element is Slider ? System.Windows.Controls.Primitives.RangeBase.ValueProperty
                    : element is TextBox ? TextBox.TextProperty
                    : element is CheckBox ? System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty
                    : System.Windows.Controls.Primitives.Selector.SelectedItemProperty;
                return BindingOperations.GetBinding(element, property)?.Path?.Path;
            }

            /// <summary>Chooses another item in a box with the Down key, or the Up key where it is on the last: the box's own selection, as a click in its list makes.</summary>
            public void Choose(string tip, Key first = Key.Down)
            {
                ComboBox box = Find<ComboBox>(tip);
                int before = box.SelectedIndex;
                Act(() => Press(box, first));

                if (box.SelectedIndex == before)
                {
                    Act(() => Press(box, first == Key.Down ? Key.Up : Key.Down));
                }
            }

            public void Check(string tip) =>
                Act(() =>
                {
                    CheckBox box = Find<CheckBox>(tip);
                    box.IsChecked = box.IsChecked != true;
                });

            public void Type(string tip, string text) =>
                Act(() =>
                {
                    TextBox box = Find<TextBox>(tip);
                    box.Text = text;
                    BindingOperations.GetBindingExpression(box, TextBox.TextProperty).UpdateSource();
                });

            public void Slide(string tip, double value) =>
                Act(() =>
                {
                    Slider slider = Find<Slider>(tip);
                    slider.Value = value;
                    BindingOperations
                        .GetBindingExpression(slider, System.Windows.Controls.Primitives.RangeBase.ValueProperty)
                        .UpdateSource();
                });

            /// <summary>Checks that one edit was made at most, and that nothing ran deeper than a change and its answer.</summary>
            public void Done(int edits)
            {
                Assert.True(MaxFrames <= FrameLimit, "a loop: " + MaxFrames + " frames, " + Edits + " edits");
                Assert.Equal(edits, Edits);
                Assert.True(Changes > 0 || edits > 0, "the control changed nothing");
            }

            private void Press(UIElement element, Key key) =>
                element.RaiseEvent(
                    new KeyEventArgs(Keyboard.PrimaryDevice, Source, 0, key) { RoutedEvent = Keyboard.KeyDownEvent }
                );

            /// <summary>Listens to the tab now shown, where it is another from the one listened to before.</summary>
            private void Watch()
            {
                object now = _tab();

                if (ReferenceEquals(now, _watched))
                {
                    return;
                }

                if (_watched is INotifyPropertyChanged old)
                {
                    old.PropertyChanged -= OnChanged;
                }

                _watched = now;

                if (now is INotifyPropertyChanged fresh)
                {
                    fresh.PropertyChanged += OnChanged;
                }
            }

            private void OnChanged(object sender, PropertyChangedEventArgs e)
            {
                Changes++;
                Frames("the tab");
            }

            private void Frames(string where)
            {
                MaxFrames = Math.Max(MaxFrames, new StackTrace().FrameCount);

                if (MaxFrames > FrameLimit)
                {
                    // The loop is cut here, and stays cut (the controls are let go of the view model, so that nothing answers
                    // again), so that the host lives to report it.
                    Workspace.DataContext = null;
                    throw new InvalidOperationException("A loop: " + MaxFrames + " frames deep in " + where);
                }
            }
        }

        private static MainViewModel Plate(bool hatched)
        {
            var database = new DxfDatabase("AC1027");
            database.LayerTable.Add(new DxfLayerTableRecord { Name = "A" });
            database.LayerTable.Add(new DxfLayerTableRecord { Name = "B" });
            GeoPoint3[] corners = { new(0, 0, 0), new(100, 0, 0), new(100, 60, 0), new(0, 60, 0) };

            for (int i = 0; i < 4; i++)
            {
                database.ModelSpace.AppendEntity(new DxfLine(corners[i], corners[(i + 1) % 4]));
            }

            var model = new MainViewModel();
            model.OpenDocument(CadDocument.From(database, "plate.dxf"));
            model.Document.HatchSettings.Layer = "A";

            if (hatched)
            {
                model.Execute("HATCH");
                model.Execute("5,5");
                model.Execute(string.Empty);
                DxfHatch hatch = model.Document.ModelSpace.Block.OfType<DxfHatch>().Single();
                model.Document.Selection.Set(new[] { hatch.Handle });
            }
            else
            {
                model.Execute("HATCH");
            }

            return model;
        }

        #region The Hatch Editor tab and the Hatch Creation tab

        private static void Hatch(Rig r, string control)
        {
            switch (control)
            {
                case "Pattern":
                    r.Choose("The pattern, or the gradient where the type is Gradient");
                    break;
                case "Type":
                    r.Choose("Pattern, User defined, Solid or Gradient");
                    break;
                case "Layer":
                    r.Choose("The layer of the hatch; Use current for the layer the drawing is on");
                    break;
                case "Transparency":
                    r.Choose("0 to 90 per cent, ByLayer or ByBlock");
                    break;
                case "Angle":
                    r.Type("The angle of the pattern or the gradient, in degrees", "30");
                    break;
                case "Scale":
                    r.Type("2 draws the lines of the pattern twice as far apart", "2");
                    break;
                case "Spacing":
                    r.Type("The distance between the lines made by the user", "4");
                    break;
                case "IsoPenWidth":
                    r.Choose("The pen an ISO pattern is drawn for: its width in millimetres is the scale");
                    break;
                case "Double":
                    r.Check("Cross the lines made by the user with as many square to them");
                    break;
                case "Associative":
                    r.Check("The hatch follows the objects its loops are made of");
                    break;
                case "Annotative":
                    r.Check("The pattern is drawn at the scale of the view");
                    break;
                case "GapTolerance":
                    r.Type("The widest gap in a boundary that is taken as closed, 0 to 5000 (HPGAPTOL)", "3");
                    break;
                case "IslandDetection":
                    r.Choose("Which areas inside the boundary are hatched");
                    break;
                case "DrawOrder":
                    r.Choose("Where the hatch goes in the draw order");
                    break;
                case "SeparateHatches":
                    r.Check("A hatch of each area picked, not one of them all");
                    break;
                case "RetainBoundaries":
                    r.Check("Keep a polyline of each boundary HATCH finds (HPBOUNDRETAIN)");
                    break;
                case "StoreAsDefaultOrigin":
                    r.Check("Keep the origin picked for the next hatches (HPORIGIN)");
                    break;
                default:
                    throw new ArgumentException(control);
            }
        }

        public static TheoryData<string, int> EditorControls =>
            new TheoryData<string, int>
            {
                { "Pattern", 1 },
                { "Type", 1 },
                { "Layer", 1 },
                { "Transparency", 1 },
                { "Angle", 1 },
                { "Scale", 1 },
                { "Spacing", 1 },
                { "IsoPenWidth", 1 },
                { "Double", 1 },
                { "Associative", 1 },
                { "Annotative", 1 },
                { "GapTolerance", 0 },
                { "IslandDetection", 1 },
                { "DrawOrder", 1 },
                { "SeparateHatches", 0 },
                { "RetainBoundaries", 0 },
            };

        public static TheoryData<string> CreationControls =>
            new TheoryData<string>
            {
                "Pattern",
                "Type",
                "Layer",
                "Transparency",
                "Angle",
                "Scale",
                "Spacing",
                "IsoPenWidth",
                "Double",
                "Associative",
                "Annotative",
                "GapTolerance",
                "IslandDetection",
                "DrawOrder",
                "SeparateHatches",
                "RetainBoundaries",
                "StoreAsDefaultOrigin",
            };

        [Theory]
        [MemberData(nameof(EditorControls))]
        public void AControlOfTheHatchEditorTab_ChosenOnce_RunsOneEditAndReturns(string control, int edits) =>
            OnSta(() =>
            {
                MainViewModel model = Plate(true);
                using var rig = new Rig(model, () => model.HatchTab);

                Hatch(rig, control);

                rig.Done(edits);
            });

        [Theory]
        [MemberData(nameof(CreationControls))]
        public void AControlOfTheHatchCreationTab_ChosenOnce_ChangesTheSettingsAndReturns(string control) =>
            OnSta(() =>
            {
                MainViewModel model = Plate(false);
                using var rig = new Rig(model, () => model.HatchTab);

                Hatch(rig, control);

                rig.Done(0);
            });

        [Fact]
        public void ThePatternChosenInTheHatchEditorTab_IsTheHatchsAndTheBoxShowsIt()
        {
            OnSta(() =>
            {
                MainViewModel model = Plate(true);
                using var rig = new Rig(model, () => model.HatchTab);

                ComboBox box = rig.Find<ComboBox>("The pattern, or the gradient where the type is Gradient");

                rig.Choose("The pattern, or the gradient where the type is Gradient");

                rig.Done(1);
                Assert.Equal("ANSI32", model.Document.ModelSpace.Block.OfType<DxfHatch>().Single().PatternName);
                Assert.Equal("ANSI32", box.SelectedItem);
                model.Document.Undo();
                Assert.Equal("ANSI31", model.Document.ModelSpace.Block.OfType<DxfHatch>().Single().PatternName);
            });
        }

        #endregion

        #region The Image tab, the PDF Underlay tab, the Array tab and the Table Cell tab

        private static MainViewModel WithImage()
        {
            var database = new DxfDatabase("AC1027");
            DxfDBDictionary images = DxfRasterImageDef.CreateImageDictionary(database);
            var definition = new DxfRasterImageDef("picture.bmp", new GeoVector2(8, 8));
            images.SetAt("picture", definition);
            var image = new DxfRasterImage(definition);
            image.SetOrientation(new GeoPoint3(0, 0, 0), new GeoVector3(40, 0, 0), new GeoVector3(0, 40, 0));
            database.ModelSpace.AppendEntity(image);
            var model = new MainViewModel();
            model.OpenDocument(CadDocument.From(database, "images.dxf"));
            model.Document.Selection.Set(new[] { image.Handle });
            return model;
        }

        private static MainViewModel WithPdf()
        {
            var database = new DxfDatabase("AC1032");
            DxfDBDictionary underlays = DxfPdfDefinition.CreatePdfDictionary(database);
            var definition = new DxfPdfDefinition("page.pdf", "1");
            underlays.SetAt("page - Page_1", definition);
            var underlay = new DxfPdfUnderlay(definition);
            database.ModelSpace.AppendEntity(underlay);
            underlay.ScaleFactors = new GeoVector3(25.4, 25.4, 25.4);
            var model = new MainViewModel();
            model.OpenDocument(CadDocument.From(database, "host.dxf"));
            model.Document.Selection.Set(new[] { underlay.Handle });
            return model;
        }

        private static MainViewModel WithArray()
        {
            var database = new DxfDatabase("AC1027");
            database.ModelSpace.AppendEntity(new DxfCircle(new GeoPoint3(10, 10, 0), new GeoVector3(0, 0, 1), 5));
            var model = new MainViewModel();
            model.OpenDocument(CadDocument.From(database, "array.dxf"));
            model.Document.Selection.Set(new[] { model.Document.ModelSpace.Block.OfType<DxfCircle>().Single().Handle });

            foreach (string input in new[] { "ARRAYRECT", "AS", "Y", "COU", "3", "4", "S", "20", "30", "X" })
            {
                model.Execute(input);
            }

            model.Document.Selection.Set(
                new[] { model.Document.ModelSpace.Block.OfType<DxfBlockReference>().Single().Handle }
            );
            return model;
        }

        /// <summary>A table of three columns 40 wide, a title, a header and two rows of data, its cell B3 selected.</summary>
        private static MainViewModel WithTableCell()
        {
            var model = new MainViewModel();
            model.OpenDocument(CadDocument.From(CadTemplate.CreateMetric(), "tables.dxf"));
            model.ShowInsertTable = dialog =>
            {
                dialog.Columns = "3";
                dialog.ColumnWidth = "40";
                dialog.DataRows = "2";
                dialog.Ok();
                return true;
            };
            model.Execute("TABLE");
            model.Execute("0,0");
            model.Escape();
            Assert.True(model.SelectTableCellAt(new GeoPoint2(60, -25), false));
            return model;
        }

        [Theory]
        [InlineData("Brightness", "slide", 1)]
        [InlineData("Contrast", "slide", 1)]
        [InlineData("Fade", "slide", 1)]
        [InlineData("Brightness", "type", 1)]
        [InlineData("Contrast", "type", 1)]
        [InlineData("Fade", "type", 1)]
        [InlineData("Off, the image is shown as its frame alone", "check", 1)]
        [InlineData("Frame: IMAGEFRAME, whether the frames of the images are shown and plotted", "choose", 1)]
        public void AControlOfTheImageTab_ChosenOnce_RunsOneEditAndReturns(string control, string how, int edits) =>
            OnSta(() =>
            {
                MainViewModel model = WithImage();
                using var rig = new Rig(model, () => model.ImageEditor);

                Drive(rig, control, how);

                rig.Done(edits);
            });

        [Theory]
        [InlineData("Fade", "slide", 1)]
        [InlineData("Contrast", "slide", 1)]
        [InlineData("Fade", "type", 1)]
        [InlineData("Contrast", "type", 1)]
        [InlineData("PDFADJUST, Monochrome: the underlay in shades of grey", "check", 1)]
        [InlineData(
            "The colours of the PDF turned to show on the background: a dark one shows its black as white",
            "check",
            1
        )]
        [InlineData("Off, the underlay is shown as its frame alone", "check", 1)]
        // PDFFRAME is a variable of the drawing, not a step of undo: no edit, and the tab answers once.
        [InlineData("Frame: PDFFRAME, whether the frames of the PDF underlays are shown and plotted", "choose", 0)]
        public void AControlOfThePdfUnderlayTab_ChosenOnce_RunsOneEditAndReturns(
            string control,
            string how,
            int edits
        ) =>
            OnSta(() =>
            {
                MainViewModel model = WithPdf();
                using var rig = new Rig(model, () => model.PdfEditor);

                Drive(rig, control, how);

                rig.Done(edits);
            });

        [Theory]
        [InlineData("Items", "type", "5")]
        [InlineData("Between", "type", "25")]
        [InlineData("Total", "type", "60")]
        [InlineData("Rows", "type", "5")]
        [InlineData("RowBetween", "type", "35")]
        [InlineData("RowTotal", "type", "120")]
        [InlineData("RowElevation", "type", "2")]
        [InlineData("Levels", "type", "2")]
        [InlineData("LevelBetween", "type", "15")]
        [InlineData("LevelTotal", "type", "30")]
        [InlineData("RotateItems", "check", "")]
        [InlineData("Clockwise", "check", "")]
        [InlineData("AlignItems", "check", "")]
        public void AControlOfTheArrayTab_ChosenOnce_RunsOneEditAndReturns(string control, string how, string text) =>
            OnSta(() =>
            {
                MainViewModel model = WithArray();
                using var rig = new Rig(model, () => model.ArrayEditor);

                Drive(rig, control, how, text);

                rig.Done(1);
            });

        [Theory]
        [InlineData("The cell style of the cells selected: Title, Header, Data or one made with Create New Cell Style")]
        [InlineData("Background fill of the cells selected")]
        public void AControlOfTheTableCellTab_ChosenOnce_RunsOneEditAndReturns(string tip) =>
            OnSta(() =>
            {
                MainViewModel model = WithTableCell();
                using var rig = new Rig(model, () => model.TableCell);

                // B3 is a Data cell, the last of the cell styles before the two entries that open dialogs: Up chooses Header.
                rig.Choose(tip, tip.StartsWith("The cell style", StringComparison.Ordinal) ? Key.Up : Key.Down);

                rig.Done(1);
            });

        /// <summary>Drives a control found by the property it is bound to (a tool tip where there is none to find it by).</summary>
        private static void Drive(Rig rig, string control, string how, string text = "40")
        {
            string where = control.Contains(" ") ? control : "=" + control;

            switch (how)
            {
                case "slide":
                    rig.Slide(where, 70);
                    break;
                case "type":
                    rig.Type(where, text);
                    break;
                case "check":
                    rig.Check(where);
                    break;
                default:
                    rig.Choose(where);
                    break;
            }
        }

        #endregion

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
    }
}
