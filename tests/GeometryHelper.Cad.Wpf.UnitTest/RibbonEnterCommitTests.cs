using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// The boxes of the ribbon commit at Enter, as AutoCAD 2021's do: typing 10 in the Scale box of the Hatch Editor tab and
    /// pressing Enter changes the hatch at once, with the keyboard still in the box, one undo step, and Esc puts the old value
    /// back. A scan keeps every box of the ribbon that holds a value under the same rule.
    /// </summary>
    public sealed class RibbonEnterCommitTests : IDisposable
    {
        private readonly string _folder = Path.Combine(
            Path.GetTempPath(),
            "gk",
            "ribbonenter-" + Guid.NewGuid().ToString("N")
        );

        public RibbonEnterCommitTests()
        {
            Directory.CreateDirectory(_folder);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_folder, true);
            }
            catch (IOException)
            {
                // A file still held is left for the system to clear.
            }
        }

        /// <summary>A workspace over a model, laid out in a window of messages (never shown), with every dialog hook let go.</summary>
        private sealed class Rig : IDisposable
        {
            public Rig(MainViewModel model)
            {
                Model = model;
                Workspace = new CadWorkspace { DataContext = model };

                foreach (
                    System.Reflection.PropertyInfo hook in typeof(MainViewModel)
                        .GetProperties()
                        .Where(p => p.CanWrite && typeof(Delegate).IsAssignableFrom(p.PropertyType))
                )
                {
                    hook.SetValue(model, null);
                }

                Source = new HwndSource(
                    new HwndSourceParameters("RibbonEnterCommitTests")
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
            }

            public MainViewModel Model { get; }

            public CadWorkspace Workspace { get; }

            public HwndSource Source { get; }

            public void Dispose() => Source.Dispose();

            public void Layout()
            {
                Workspace.Measure(new Size(2200, 960));
                Workspace.Arrange(new Rect(0, 0, 2200, 960));
                Workspace.UpdateLayout();
            }

            public TextBox Box(DependencyObject scope, string name) =>
                Descendants(scope)
                    .OfType<TextBox>()
                    .Single(b => name.Equals(b.ToolTip) || name.Equals(AutomationProperties.GetName(b)));

            /// <summary>Types a value in a box: the box holds the keyboard, its text changed, its source not yet written.</summary>
            public void Type(TextBox box, string text)
            {
                box.Focus();
                Keyboard.Focus(box);
                box.Text = text;
            }

            public void Press(UIElement element, Key key) =>
                element.RaiseEvent(
                    new KeyEventArgs(Keyboard.PrimaryDevice, Source, 0, key)
                    {
                        RoutedEvent = Keyboard.PreviewKeyDownEvent,
                    }
                );
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

        /// <summary>The number of undo steps named <paramref name="name"/> on top of the document's, undone as they are counted.</summary>
        private static int Steps(CadDocument document, string name)
        {
            int count = 0;

            while (document.CanUndo && name.Equals(document.NextUndo.Name))
            {
                Assert.True(document.Undo());
                count++;
            }

            return count;
        }

        /// <summary>A plate of four lines 100 by 60, hatched with ANSI31 at scale 1, the hatch selected.</summary>
        private static MainViewModel HatchedPlate(out DxfHatch hatch)
        {
            var database = new DxfDatabase("AC1027");
            GeoPoint3[] corners =
            {
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(100, 0, 0),
                new GeoPoint3(100, 60, 0),
                new GeoPoint3(0, 60, 0),
            };

            for (int i = 0; i < 4; i++)
            {
                database.ModelSpace.AppendEntity(new DxfLine(corners[i], corners[(i + 1) % 4]));
            }

            var model = new MainViewModel();
            model.OpenDocument(CadDocument.From(database, "plate.dxf"));
            model.Execute("HATCH");
            model.Execute("5,5");
            model.Execute(string.Empty);
            hatch = model.Document.ModelSpace.Block.OfType<DxfHatch>().Single();
            model.Document.Selection.Set(new[] { hatch.Handle });
            return model;
        }

        private const string ScaleTip = "2 draws the lines of the pattern twice as far apart";
        private const string AngleTip = "The angle of the pattern or the gradient, in degrees";

        [Fact]
        public void TheScaleOfTheHatchEditorTyped10AndEntered_ChangesTheHatchAtOnce_WithTheKeyboardStillInTheBox()
        {
            OnSta(() =>
            {
                MainViewModel model = HatchedPlate(out DxfHatch hatch);
                using (var rig = new Rig(model))
                {
                    TextBox box = rig.Box(Descendants(rig.Workspace).OfType<HatchRibbon>().Single(), ScaleTip);
                    int changes = 0;
                    model.Document.Changed += (_, _) => changes++;
                    rig.Type(box, "10");
                    Assert.Equal(1.0, hatch.PatternScale, 9);
                    IInputElement focused = Keyboard.FocusedElement;

                    rig.Press(box, Key.Enter);

                    Assert.Equal(10.0, hatch.PatternScale, 9);
                    Assert.True(changes > 0, "the drawing was not told");
                    Assert.Equal("10", model.HatchTab.Scale);
                    Assert.Same(focused, Keyboard.FocusedElement);
                    Assert.Equal("10", box.Text);

                    // Leaving the box afterwards adds no step: one Enter is one undo step.
                    Keyboard.ClearFocus();
                    Assert.Equal(1, Steps(model.Document, "HATCHEDIT"));
                    Assert.Equal(1.0, ((DxfHatch)model.Document.FindEntity(hatch.Handle)).PatternScale, 9);
                }
            });
        }

        [Fact]
        public void TheAngleOfTheHatchEditorTyped30AndEntered_ChangesTheHatch_AndEscPutsTheOldAngleBack()
        {
            OnSta(() =>
            {
                MainViewModel model = HatchedPlate(out DxfHatch hatch);
                using (var rig = new Rig(model))
                {
                    TextBox box = rig.Box(Descendants(rig.Workspace).OfType<HatchRibbon>().Single(), AngleTip);
                    rig.Type(box, "30");
                    rig.Press(box, Key.Enter);
                    Assert.Equal(30.0, hatch.PatternAngle * 180.0 / Math.PI, 9);

                    rig.Type(box, "75");
                    rig.Press(box, Key.Escape);

                    Assert.Equal("30", box.Text);
                    Assert.Equal(30.0, hatch.PatternAngle * 180.0 / Math.PI, 9);
                    Assert.Equal("30", model.HatchTab.Angle);
                }
            });
        }

        [Fact]
        public void TheScaleOfTheHatchCreationTypedAndEntered_IsTheScaleOfTheNextHatch()
        {
            OnSta(() =>
            {
                MainViewModel model = HatchedPlate(out _);
                model.Document.Selection.Clear();
                model.Execute("HATCH");
                using (var rig = new Rig(model))
                {
                    TextBox box = rig.Box(Descendants(rig.Workspace).OfType<HatchRibbon>().Single(), ScaleTip);
                    rig.Type(box, "2.5");
                    rig.Press(box, Key.Enter);

                    Assert.Equal(2.5, model.Document.HatchSettings.Scale, 9);
                }
            });
        }

        /// <summary>A drawing of R2013 with a rectangular associative array of a circle: 3 columns 20 apart, 4 rows 30 apart.</summary>
        private static MainViewModel WithArray(out string insert)
        {
            var database = new DxfDatabase("AC1027");
            var circle = new DxfCircle(new GeoPoint3(10, 10, 0), new GeoVector3(0, 0, 1), 5);
            database.ModelSpace.AppendEntity(circle);
            var model = new MainViewModel();
            model.OpenDocument(CadDocument.From(database, "array.dxf"));
            model.Document.Selection.Set(new[] { circle.Handle });

            foreach (string input in new[] { "ARRAYRECT", "AS", "Y", "COU", "3", "4", "S", "20", "30", "X" })
            {
                model.Execute(input);
            }

            insert = model.Document.ModelSpace.Block.OfType<DxfBlockReference>().Single().Handle;
            model.Document.Selection.Set(new[] { insert });
            return model;
        }

        [Fact]
        public void TheColumnsOfTheArrayTabTyped5AndEntered_MakeTheArrayAgain_OneUndoStep()
        {
            OnSta(() =>
            {
                MainViewModel model = WithArray(out string insert);
                using (var rig = new Rig(model))
                {
                    TextBox box = rig.Box(
                        Descendants(rig.Workspace).OfType<ArrayRibbon>().Single(),
                        "The number of columns, or of items round the centre or along the path"
                    );
                    rig.Type(box, "5");

                    rig.Press(box, Key.Enter);

                    Assert.Equal(5, model.ArrayEditor.Items);
                    Assert.Equal(
                        20,
                        ((DxfBlockReference)model.Document.FindEntity(insert))
                            .BlockTableRecord.OfType<DxfBlockReference>()
                            .Count()
                    );
                    Keyboard.ClearFocus();
                    Assert.Equal(1, Steps(model.Document, "ARRAYEDIT"));
                }
            });
        }

        [Fact]
        public void TheBrightnessOfTheImageTabTyped70AndEntered_ChangesTheImage_OneUndoStep()
        {
            OnSta(() =>
            {
                var database = new DxfDatabase("AC1027");
                DxfRasterImage image = Place(database, Bitmap("colour.bmp"));
                var model = new MainViewModel();
                model.OpenDocument(CadDocument.From(database, Path.Combine(_folder, "image.dxf")));
                model.Document.Selection.Set(new[] { image.Handle });
                using (var rig = new Rig(model))
                {
                    TextBox box = rig.Box(
                        Descendants(rig.Workspace).OfType<ImageRibbon>().Single(),
                        "Brightness value"
                    );
                    rig.Type(box, "70");

                    rig.Press(box, Key.Enter);

                    var changed = (DxfRasterImage)model.Document.FindEntity(image.Handle);
                    Assert.Equal(70, changed.Brightness);
                    Keyboard.ClearFocus();
                    Assert.Equal(1, Steps(model.Document, "IMAGEADJUST"));
                    Assert.Equal(50, ((DxfRasterImage)model.Document.FindEntity(image.Handle)).Brightness);
                }
            });
        }

        [Fact]
        public void TheHeightOfTheTextEditorTab_Typed7AndEntered_IsSet_AndEscPutsTheOldHeightBack()
        {
            OnSta(() =>
            {
                var database = new DxfDatabase("AC1027");
                var mtext = new DxfMText
                {
                    Location = new GeoPoint3(0, 0, 0),
                    TextHeight = 2.5,
                    Contents = "PLATE",
                };
                database.ModelSpace.AppendEntity(mtext);
                var model = new MainViewModel();
                model.OpenDocument(CadDocument.From(database, "text.dxf"), "text.dxf");
                Assert.True(model.EditTextAt(mtext.Handle));
                using (var rig = new Rig(model))
                {
                    TextBox box = rig.Box(rig.Workspace, "Text height");
                    Assert.Equal("2.5", model.TextEditor.HeightText);
                    rig.Type(box, "7");
                    Assert.Equal("2.5", model.TextEditor.HeightText);

                    rig.Press(box, Key.Enter);

                    Assert.Equal("7", model.TextEditor.HeightText);

                    rig.Type(box, "9");
                    rig.Press(box, Key.Escape);

                    Assert.Equal("7", box.Text);
                    Assert.Equal("7", model.TextEditor.HeightText);
                }
            });
        }

        [Fact]
        public void TheTextHeightBoxOfTheAnnotateTab_TypedAndEntered_SetsTheHeightOfTheNextText()
        {
            OnSta(() =>
            {
                var model = new MainViewModel();
                model.OpenDocument(CadDocument.From(new DxfDatabase("AC1027"), "empty.dxf"));
                model.SelectedTab = model.AnnotateTab;
                using (var rig = new Rig(model))
                {
                    TextBox box = rig.Box(rig.Workspace, "Text height of the next Single Line text (TEXT, MTEXT)");
                    string before = model.TextHeight;
                    rig.Type(box, "12.5");
                    Assert.Equal(before, model.TextHeight);

                    rig.Press(box, Key.Enter);

                    Assert.Equal("12.5", model.TextHeight);
                }
            });
        }

        [Fact]
        public void ARadiusTypedInTheProperties_AndEntered_ChangesTheCircleAtOnce_AsItAlwaysDid()
        {
            OnSta(() =>
            {
                var database = new DxfDatabase("AC1027");
                var circle = new DxfCircle(new GeoPoint3(0, 0, 0), GeoVector3.ZAxis, 25);
                database.ModelSpace.AppendEntity(circle);
                var model = new MainViewModel();
                model.OpenDocument(CadDocument.From(database, "circle.dxf"));
                model.Document.Selection.Set(new[] { circle.Handle });
                using (var rig = new Rig(model))
                {
                    TextBox box = Descendants(rig.Workspace)
                        .OfType<TextBox>()
                        .Single(b =>
                            "Editor".Equals(b.Name) && b.DataContext is PropertyRowViewModel { Label: "Radius" }
                        );
                    rig.Type(box, "40");

                    box.RaiseEvent(
                        new KeyEventArgs(Keyboard.PrimaryDevice, rig.Source, 0, Key.Enter)
                        {
                            RoutedEvent = Keyboard.KeyDownEvent,
                        }
                    );

                    Assert.Equal(40.0, ((DxfCircle)model.Document.FindEntity(circle.Handle)).Radius, 9);
                }
            });
        }

        /// <summary>
        /// Every box of the ribbon that holds a value must commit at Enter: a box whose text is bound to its source at the
        /// loss of the keyboard and does not say so (<see cref="CommitOnEnter"/>) is found here, so that a new box cannot
        /// bring the fault back. A box that writes at each keystroke (a search box, the command line) and the box of the text
        /// edited in place are the ones that need no more, and a read-only box (the history of the command line) holds no value.
        /// </summary>
        [Fact]
        public void EveryEditableBoxOfTheRibbonAndItsTabs_CommitsAtEnter()
        {
            OnSta(() =>
            {
                var found = new HashSet<FrameworkElement>();
                var saying = new HashSet<FrameworkElement>();
                var failures = new List<string>();

                void Scan(FrameworkElement root)
                {
                    foreach (DependencyObject d in Descendants(root).Prepend(root))
                    {
                        // The box inside a combo's template is the combo's own: the combo is judged, by IsEditable.
                        if (d is TextBox box && !"PART_EditableTextBox".Equals(box.Name))
                        {
                            found.Add(box);

                            if (CommitOnEnter.GetEnabled(box))
                            {
                                saying.Add(box);
                            }

                            if (!CommitsAtEnter(box))
                            {
                                failures.Add(Describe(box));
                            }
                        }
                        else if (d is ComboBox combo && combo.IsEditable)
                        {
                            failures.Add(Describe(combo));
                        }
                    }
                }

                foreach (
                    FrameworkElement ribbon in new FrameworkElement[]
                    {
                        new HatchRibbon(),
                        new ArrayRibbon(),
                        new ImageRibbon(),
                        new PdfRibbon(),
                        new TableCellRibbon(),
                        new BlockEditorRibbon(),
                    }
                )
                {
                    ribbon.Measure(new Size(2200, 400));
                    ribbon.Arrange(new Rect(0, 0, 2200, 400));
                    ribbon.UpdateLayout();
                    Scan(ribbon);
                }

                var database = new DxfDatabase("AC1027");
                var mtext = new DxfMText
                {
                    Location = new GeoPoint3(0, 0, 0),
                    TextHeight = 2.5,
                    Contents = "PLATE",
                };
                database.ModelSpace.AppendEntity(mtext);
                var model = new MainViewModel();
                model.OpenDocument(CadDocument.From(database, "text.dxf"), "text.dxf");
                Assert.True(model.EditTextAt(mtext.Handle));
                using (var rig = new Rig(model))
                {
                    foreach (
                        CadWorkspaceExtension tab in new[]
                        {
                            model.TextEditorTab,
                            model.InsertTab,
                            model.AnnotateTab,
                            model.ViewTab,
                            null,
                        }
                    )
                    {
                        model.SelectedTab = tab;
                        rig.Layout();
                        Scan(rig.Workspace);
                    }
                }

                // The scan saw the boxes it is meant to see: the 19 of the four ribbons, the 4 of the Text Editor tab.
                Assert.True(
                    saying.Count >= 19 + 4 + 1,
                    "only " + saying.Count + " of " + found.Count + " boxes say so"
                );
                Assert.Equal(string.Empty, string.Join("\n", failures.Distinct()));
            });
        }

        /// <summary>Whether a box sets its value at Enter: it says so, writes at each keystroke, or is the box of text edited in place.</summary>
        private static bool CommitsAtEnter(TextBox box)
        {
            if (
                CommitOnEnter.GetEnabled(box)
                || box.AcceptsReturn
                || box.IsReadOnly
                || "InPlaceTextBox".Equals(box.Name)
            )
            {
                return true;
            }

            // The Value box of a row of the Properties palette handles Enter and Esc itself (OnValueKeyDown).
            if ("Editor".Equals(box.Name))
            {
                return true;
            }

            BindingExpression binding = box.GetBindingExpression(TextBox.TextProperty);
            return binding != null && binding.ParentBinding.UpdateSourceTrigger == UpdateSourceTrigger.PropertyChanged;
        }

        private static string Describe(FrameworkElement box)
        {
            string path = string.Empty;

            for (DependencyObject up = box; up != null; up = VisualTreeHelper.GetParent(up))
            {
                path =
                    up.GetType().Name
                    + (up is FrameworkElement f && f.Name.Length > 0 ? "#" + f.Name : "")
                    + "/"
                    + path;
            }

            return (box.GetType().Name + " " + box.ToolTip + " " + AutomationProperties.GetName(box)).Trim()
                + " at "
                + path;
        }

        /// <summary>A bitmap file of 8 by 8 pixels of 24 bits.</summary>
        private string Bitmap(string name)
        {
            var bytes = new List<byte> { (byte)'B', (byte)'M' };

            void Little(int value)
            {
                for (int i = 0; i < 4; i++)
                {
                    bytes.Add((byte)(value >> (8 * i)));
                }
            }

            Little(54 + 24 * 8);
            Little(0);
            Little(54);
            Little(40);
            Little(8);
            Little(8);
            bytes.AddRange(new byte[] { 1, 0, 24, 0 });
            Little(0);
            Little(24 * 8);
            Little(3780);
            Little(3780);
            Little(0);
            Little(0);

            for (int i = 0; i < 24 * 8; i++)
            {
                bytes.Add((byte)(i * 37));
            }

            string path = Path.Combine(_folder, name);
            File.WriteAllBytes(path, bytes.ToArray());
            return path;
        }

        private static DxfRasterImage Place(DxfDatabase database, string path)
        {
            DxfDBDictionary images = DxfRasterImageDef.CreateImageDictionary(database);
            var definition = new DxfRasterImageDef(path, new GeoVector2(8, 8));
            images.SetAt("colour", definition);
            var image = new DxfRasterImage(definition);
            image.SetOrientation(new GeoPoint3(0, 0, 0), new GeoVector3(40, 0, 0), new GeoVector3(0, 40, 0));
            database.ModelSpace.AppendEntity(image);
            return image;
        }
    }
}
