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

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// What an extension can give the presenter so that its own objects are edited as the presenter's are: items in the canvas's
    /// right-click menu for the objects selected, rows of Properties that can be typed into, ticked or chosen, a button in the
    /// header of the DRAWINGS list, and the contextual tabs (Hatch Editor, Image, PDF Underlay, Array) kept shut for objects
    /// whose owner lets only a move or nothing through. The room has its bottom wall from (0, 0) to (4 000, 0), a column of
    /// radius 100 about (2 000, 1 500) and its top wall from (0, 3 000) to (4 000, 3 000).
    /// </summary>
    public sealed class ExtensionSeamsTests
    {
        private sealed class SeamExtension : CadWorkspaceExtension
        {
            public readonly List<string[]> Asked = new List<string[]>();
            public Func<IReadOnlyList<string>, IReadOnlyList<CadExtensionMenuItem>> Menu = _ => null;
            public Func<CadSelectionInfo, CadSelectionInfo> Describe = shown => shown;
            public IReadOnlyList<CadExtensionButton> Buttons = Array.Empty<CadExtensionButton>();

            public override IReadOnlyList<CadExtensionMenuItem> CanvasMenuItems(
                CadDocument document,
                IReadOnlyList<string> handles
            )
            {
                Asked.Add(handles.ToArray());
                return Menu(handles);
            }

            public override CadSelectionInfo DescribeSelection(
                CadDocument document,
                IReadOnlyList<string> handles,
                CadSelectionInfo shown,
                out string banner
            )
            {
                banner = null;
                return Describe(shown);
            }

            public override IReadOnlyList<CadExtensionButton> FileListButtons => Buttons;
        }

        private sealed class FixedOwner : ICadEntityOwner
        {
            private readonly CadEditRule _rule;

            public FixedOwner(CadEditRule rule) => _rule = rule;

            public string Name => "Test.Fixed";

            public CadEditRule GetEditRule(DxfEntity entity) => _rule;

            public bool TryApply(CadDocument document, CadEdit edit) => false;
        }

        private static MainViewModel Open(TestFolder folder, out string bottom, out string top)
        {
            DxfDatabase database = TestFiles.Room(out bottom, out _);
            top = database.ModelSpace.OfType<DxfLine>().Select(l => l.Handle).Last();
            var model = new MainViewModel();
            Assert.True(model.Open(folder.Write("room.dxf", database)));
            return model;
        }

        private static string[] Headers(IEnumerable<CadMenuItemViewModel> items) =>
            items.Select(i => i.Header).ToArray();

        private static CadSelectionInfo Rows(string label, params CadPropertyRow[] rows) =>
            new CadSelectionInfo(label, new[] { new CadPropertyGroup("Group", rows) });

        private static PropertyRowViewModel RowOf(MainViewModel model, string label) =>
            model.PropertyGroups.SelectMany(g => g.Rows).Single(r => r.Label == label);

        #region The canvas menu

        [Fact]
        public void AnExtensionsItems_AreInTheEditMenuOfTheObjectsSelected_BeforeTheClipboard_AndTheEnginesStayAfterThem()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder, out string bottom, out _);
                var extension = new SeamExtension
                {
                    Menu = _ =>
                        new[]
                        {
                            new CadExtensionMenuItem("Properties...", () => { }),
                            CadExtensionMenuItem.Separator(),
                            new CadExtensionMenuItem("Delete view", () => { }),
                        },
                };
                model.AddExtension(extension);
                model.Document.Selection.Set(new[] { bottom });

                string[] headers = Headers(model.CanvasMenu());

                Assert.Equal(new[] { "Properties...", string.Empty, "Delete view", string.Empty }, headers.Take(4));
                Assert.True(Array.IndexOf(headers, "Delete view") < Array.IndexOf(headers, "Clipboard"));
                Assert.Contains("Erase", headers);
                Assert.Contains("Properties", headers);
            }
        }

        [Fact]
        public void TheExtensionIsAskedForTheHandlesSelected_InOrdinalOrder_AndNotAskedWithNothingSelected()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder, out string bottom, out string top);
                var extension = new SeamExtension();
                model.AddExtension(extension);

                Assert.NotNull(model.CanvasMenu());
                Assert.Empty(extension.Asked);

                model.Document.Selection.Set(new[] { top, bottom });
                model.CanvasMenu();

                string[] expected = new[] { top, bottom }.OrderBy(h => h, StringComparer.OrdinalIgnoreCase).ToArray();
                Assert.Equal(expected, Assert.Single(extension.Asked));
            }
        }

        [Fact]
        public void AnItemRunsItsActionWhenChosen_AGreyOneDoesNot_AndATickAndASubmenuAreShown()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder, out string bottom, out _);
                var ran = new List<string>();
                var extension = new SeamExtension
                {
                    Menu = _ =>
                        new[]
                        {
                            new CadExtensionMenuItem("Fit to sheet", () => ran.Add("fit")),
                            new CadExtensionMenuItem("Delete view", () => ran.Add("delete"), isEnabled: false),
                            new CadExtensionMenuItem(
                                "Change representation",
                                new[]
                                {
                                    new CadExtensionMenuItem("All bars", () => ran.Add("all"), isChecked: true),
                                    new CadExtensionMenuItem("Middle bar", () => ran.Add("middle"), isChecked: false),
                                }
                            ),
                        },
                };
                model.AddExtension(extension);
                model.Document.Selection.Set(new[] { bottom });
                IReadOnlyList<CadMenuItemViewModel> items = model.CanvasMenu();

                CadMenuItemViewModel fit = items.Single(i => i.Header == "Fit to sheet");
                CadMenuItemViewModel delete = items.Single(i => i.Header == "Delete view");
                CadMenuItemViewModel representation = items.Single(i => i.Header == "Change representation");

                Assert.True(fit.IsEnabled);
                Assert.False(fit.IsCheckable);
                Assert.True(fit.Command.CanExecute(null));
                fit.Command.Execute(null);
                Assert.False(delete.IsEnabled);
                Assert.False(delete.Command.CanExecute(null));
                Assert.Equal(new[] { "All bars", "Middle bar" }, Headers(representation.Items));
                Assert.True(representation.Items[0].IsCheckable);
                Assert.True(representation.Items[0].IsChecked);
                Assert.True(representation.Items[1].IsCheckable);
                Assert.False(representation.Items[1].IsChecked);
                representation.Items[1].Command.Execute(null);

                Assert.Equal(new[] { "fit", "middle" }, ran);
            }
        }

        [Fact]
        public void AnExtensionThatAddsNothing_LeavesTheEditMenuAsItWas()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel plain = Open(folder, out string bottom, out _);
                plain.Document.Selection.Set(new[] { bottom });
                string[] before = Headers(plain.CanvasMenu());

                MainViewModel model = Open(folder, out string bottom2, out _);
                model.AddExtension(new SeamExtension { Menu = _ => Array.Empty<CadExtensionMenuItem>() });
                model.Document.Selection.Set(new[] { bottom2 });

                Assert.Equal(before, Headers(model.CanvasMenu()));
            }
        }

        [Fact]
        public void AnItemNeedsATextAndAnAction_ASubmenuNeedsItems()
        {
            Assert.Throws<ArgumentException>(() => new CadExtensionMenuItem(" ", () => { }));
            Assert.Throws<ArgumentNullException>(() => new CadExtensionMenuItem("Fit", (Action)null));
            Assert.Throws<ArgumentNullException>(() =>
                new CadExtensionMenuItem("Fit", (IReadOnlyList<CadExtensionMenuItem>)null)
            );
            Assert.True(CadExtensionMenuItem.Separator().IsSeparator);
        }

        #endregion

        #region Properties rows that can be changed

        [Fact]
        public void ATypedRowOfAnExtension_CallsItsSetterWithTheTextTyped_AndKeepsTheValueWhereItIsTaken()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder, out string bottom, out _);
                var typed = new List<string>();
                string content = "Mark, Count";

                // The extension describes the group as it is, so a change made is read back.
                model.AddExtension(
                    new SeamExtension
                    {
                        Describe = _ => Rows("Rebar group (1)", new CadPropertyRow("Label content", content, t =>
                                {
                                    typed.Add(t);
                                    content = t;
                                    return null;
                                })),
                    }
                );
                model.Document.Selection.Set(new[] { bottom });

                PropertyRowViewModel row = RowOf(model, "Label content");
                Assert.False(row.IsReadOnly);
                Assert.True(row.IsTyped);
                Assert.False(row.HasOptions);

                row.Value = "Mark, Size";

                Assert.Equal(new[] { "Mark, Size" }, typed);
                Assert.Equal("Mark, Size", RowOf(model, "Label content").Value);
            }
        }

        [Fact]
        public void ARowTheSetterRefuses_GoesBackToItsValue_AndTheRefusalIsWrittenOnTheCommandLine()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder, out string bottom, out _);
                model.AddExtension(
                    new SeamExtension
                    {
                        Describe = _ =>
                            Rows(
                                "View (1)",
                                new CadPropertyRow("Scale", "1:50", t => "A scale is 1: and a number above nought.")
                            ),
                    }
                );
                model.Document.Selection.Set(new[] { bottom });
                PropertyRowViewModel row = RowOf(model, "Scale");

                row.Value = "1:0";

                Assert.Equal("1:50", row.Value);
                Assert.Contains("A scale is 1: and a number above nought.", model.History);
            }
        }

        [Fact]
        public void ASetterThatThrows_IsRefusedInItsWords_AndTheRowKeepsItsValue()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder, out string bottom, out _);
                model.AddExtension(
                    new SeamExtension
                    {
                        Describe = _ =>
                            Rows(
                                "View (1)",
                                new CadPropertyRow("Scale", "1:50", t =>
                                    throw new InvalidOperationException("The drawing is locked.")
                                )
                            ),
                    }
                );
                model.Document.Selection.Set(new[] { bottom });
                PropertyRowViewModel row = RowOf(model, "Scale");

                row.Value = "1:20";

                Assert.Equal("1:50", row.Value);
                Assert.Contains("The drawing is locked.", model.History);
            }
        }

        [Fact]
        public void AChoiceRowOfAnExtension_ShowsItsListAndCallsItsSetterWithTheChoice()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder, out string bottom, out _);
                var typed = new List<string>();
                var ways = new[] { "All bars", "Middle bar", "First and last" };
                model.AddExtension(
                    new SeamExtension
                    {
                        Describe = _ =>
                            Rows(
                                "Rebar group (1)",
                                new CadPropertyRow(
                                    "Representation",
                                    "Middle bar",
                                    t =>
                                    {
                                        typed.Add(t);
                                        return null;
                                    },
                                    ways
                                )
                            ),
                    }
                );
                model.Document.Selection.Set(new[] { bottom });
                PropertyRowViewModel row = RowOf(model, "Representation");

                Assert.True(row.HasOptions);
                Assert.False(row.IsTyped);
                Assert.Equal(ways, row.Options);

                row.Value = "First and last";

                Assert.Equal(new[] { "First and last" }, typed);
            }
        }

        [Fact]
        public void ACheckRowOfAnExtension_IsATickedBox_AndTickingItCallsTheSetterWithYesOrNo()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder, out string bottom, out _);
                var typed = new List<string>();
                bool dashed = true;
                model.AddExtension(
                    new SeamExtension
                    {
                        Describe = _ => Rows("View (1)", CadPropertyRow.Check("Hidden lines", dashed, t =>
                                {
                                    typed.Add(t);
                                    dashed = t == CadPropertyRow.YesText;
                                    return null;
                                })),
                    }
                );
                model.Document.Selection.Set(new[] { bottom });
                PropertyRowViewModel row = RowOf(model, "Hidden lines");

                Assert.True(row.IsCheck);
                Assert.False(row.IsTyped);
                Assert.False(row.HasOptions);
                Assert.True(row.IsOn);
                Assert.Equal("Yes", row.Value);

                row.IsOn = false;

                Assert.Equal(new[] { "No" }, typed);
                Assert.False(RowOf(model, "Hidden lines").IsOn);
            }
        }

        [Fact]
        public void ARowWithNoSetter_StaysReadOnly_AndTypingInItCallsNothing()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder, out string bottom, out _);
                model.AddExtension(
                    new SeamExtension
                    {
                        Describe = _ =>
                            Rows(
                                "Rebar group (1)",
                                new CadPropertyRow("GUID", "a1b2", true),
                                CadPropertyRow.Check("Linked", true, null)
                            ),
                    }
                );
                model.Document.Selection.Set(new[] { bottom });
                PropertyRowViewModel guid = RowOf(model, "GUID");
                PropertyRowViewModel linked = RowOf(model, "Linked");

                Assert.True(guid.IsReadOnly);
                Assert.True(linked.IsReadOnly);
                Assert.False(linked.IsCheck);
                guid.Value = "other";
                Assert.Equal("a1b2", guid.Value);
            }
        }

        [Fact]
        public void ARowMadeWithASetter_IsNotReadOnly_AndOneMadeAsBefore_IsAsItWas()
        {
            var old = new CadPropertyRow("Layer", "0", false);
            var made = new CadPropertyRow("Layer", "0", t => null);

            Assert.False(old.IsReadOnly);
            Assert.Null(old.Setter);
            Assert.False(made.IsReadOnly);
            Assert.NotNull(made.Setter);
            Assert.Throws<ArgumentNullException>(() => new CadPropertyRow("Layer", "0", (Func<string, string>)null));
        }

        #endregion

        #region A button in the DRAWINGS header

        [Fact]
        public void AnExtensionsButtons_AreListedByThePresenter_InTheOrderTheExtensionsWereAdded()
        {
            var model = new MainViewModel();
            var ran = new List<string>();
            model.AddExtension(
                new SeamExtension
                {
                    Buttons = new[]
                    {
                        new CadExtensionButton(System.Windows.Media.Geometry.Parse("M0 0L10 10"), "First", () =>
                            ran.Add("a")
                        ),
                    },
                }
            );
            model.AddExtension(
                new SeamExtension
                {
                    Buttons = new[]
                    {
                        new CadExtensionButton(System.Windows.Media.Geometry.Parse("M0 10L10 0"), "Second", () =>
                            ran.Add("b")
                        ),
                    },
                }
            );

            Assert.Equal(new[] { "First", "Second" }, model.FileListButtons.Select(b => b.ToolTip));
            model.FileListButtons[1].Command.Execute(null);
            Assert.Equal(new[] { "b" }, ran);
        }

        [Fact]
        public void TheHeaderOfTheDrawingsList_ShowsAnExtensionsButtonWithItsTooltip_AndItRunsItsAction()
        {
            var ran = 0;
            OnWorkspace(
                new SeamExtension
                {
                    Buttons = new[]
                    {
                        new CadExtensionButton(
                            System.Windows.Media.Geometry.Parse("M0 0L10 10"),
                            "Refresh the drawings",
                            () => ran++
                        ),
                    },
                },
                (model, workspace) =>
                {
                    Button button = Descendants(workspace)
                        .OfType<Button>()
                        .Single(b => Equals(b.ToolTip, "Refresh the drawings"));
                    var files = (FrameworkElement)workspace.FindName("FileListPanel");

                    Assert.True(files.IsAncestorOf(button));
                    Assert.True(button.ActualWidth > 0.0);
                    Assert.True(button.Command.CanExecute(null));
                    button.Command.Execute(null);
                    Assert.Equal(1, ran);
                }
            );
        }

        [Fact]
        public void WithNoButtonOfAnExtension_TheHeaderHoldsTheTwoButtonsItAlwaysHad()
        {
            OnWorkspace(new SeamExtension(), (model, workspace) =>
            {
                var files = (FrameworkElement)workspace.FindName("FileListPanel");
                string[] tips = Descendants(files)
                    .OfType<Button>()
                    .Select(b => b.ToolTip as string)
                    .Where(t => t == "Look for new files" || t == "Open a DXF (Ctrl+O)")
                    .ToArray();

                Assert.Equal(new[] { "Look for new files", "Open a DXF (Ctrl+O)" }, tips);
            });
        }

        [Fact]
        public void ThePaletteDrawsACheckRowAsABox_ATypedRowAsABox_AndAChoiceRowAsAList_AndTheBoxCallsTheSetter()
        {
            var typed = new List<string>();
            bool on = true;
            var extension = new SeamExtension
            {
                Describe = _ => Rows("View (1)", CadPropertyRow.Check("Hidden lines", on, t =>
                        {
                            typed.Add(t);
                            on = t == CadPropertyRow.YesText;
                            return null;
                        }), new CadPropertyRow("Scale", "1:50", t => null), new CadPropertyRow("Way", "Middle bar", t => null, new[] { "All bars", "Middle bar" }), new CadPropertyRow("GUID", "AAAA", true)),
            };
            using (var folder = new TestFolder())
            {
                string file = folder.Write("room.dxf", TestFiles.Room(out string bottom, out _));
                OnWorkspace(
                    extension,
                    (model, workspace) =>
                    {
                        model.Document.Selection.Set(new[] { bottom });
                        workspace.UpdateLayout();

                        CheckBox Box() =>
                            Descendants(workspace)
                                .OfType<CheckBox>()
                                .Single(c =>
                                    System.Windows.Automation.AutomationProperties.GetName(c) == "Hidden lines"
                                );

                        Assert.Equal(Visibility.Visible, Box().Visibility);
                        Assert.True(Box().IsChecked);

                        Box().IsChecked = false;
                        workspace.UpdateLayout();

                        Assert.Equal(new[] { "No" }, typed);
                        Assert.False(Box().IsChecked);
                    },
                    file
                );
            }
        }

        [Fact]
        public void AButtonNeedsAnIconATooltipAndAnAction()
        {
            System.Windows.Media.Geometry icon = System.Windows.Media.Geometry.Parse("M0 0L10 10");

            Assert.Throws<ArgumentNullException>(() => new CadExtensionButton(null, "Refresh", () => { }));
            Assert.Throws<ArgumentException>(() => new CadExtensionButton(icon, " ", () => { }));
            Assert.Throws<ArgumentNullException>(() => new CadExtensionButton(icon, "Refresh", null));
        }

        #endregion

        #region Contextual tabs

        /// <summary>Two plates 100 by 60, the first from (0, 0) and the second from (200, 0), each hatched; the hatches' handles.</summary>
        private static MainViewModel Hatched(TestFolder folder, out string hatch, out string second)
        {
            var database = new DxfDatabase("AC1027");

            foreach (double x in new[] { 0.0, 200.0 })
            {
                GeoPoint3[] corners =
                {
                    new GeoPoint3(x, 0, 0),
                    new GeoPoint3(x + 100, 0, 0),
                    new GeoPoint3(x + 100, 60, 0),
                    new GeoPoint3(x, 60, 0),
                };

                for (int i = 0; i < 4; i++)
                {
                    database.ModelSpace.AppendEntity(new DxfLine(corners[i], corners[(i + 1) % 4]));
                }
            }

            var model = new MainViewModel();
            model.OpenDocument(CadDocument.From(database, Path.Combine(folder.FullPath, "plates.dxf")));

            foreach (string at in new[] { "5,5", "205,5" })
            {
                model.Execute("HATCH");
                model.Execute(at);
                model.Execute(string.Empty);
            }

            List<DxfHatch> hatches = model.Document.ModelSpace.Block.OfType<DxfHatch>().ToList();
            Assert.Equal(2, hatches.Count);
            hatch = hatches[0].Handle;
            second = hatches[1].Handle;
            return model;
        }

        [Theory]
        [InlineData(CadEditRule.Free, true)]
        [InlineData(CadEditRule.Edit, true)]
        [InlineData(CadEditRule.MoveOnly, false)]
        [InlineData(CadEditRule.ReadOnly, false)]
        public void AHatchSelected_OpensTheHatchEditorTab_OnlyWhereItsOwnerLetsItBeEditedAsAHatch(
            CadEditRule rule,
            bool opens
        )
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Hatched(folder, out string hatch, out _);
                model.Document.Selection.Clear();
                var entity = model.Document.FindEntity(hatch);
                new CadOwnerTag("Test.Fixed", "H-1").WriteTo(entity);
                model.Document.Owners.Register(new FixedOwner(rule));

                model.Document.Selection.Set(new[] { hatch });

                Assert.Equal(opens, model.HatchEditorTab.IsTabVisible);
                Assert.Equal(opens, model.HatchTab != null);
            }
        }

        [Fact]
        public void AHatchWithNoOwner_OpensTheHatchEditorTabAsItAlwaysDid()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Hatched(folder, out string hatch, out _);
                model.Document.Selection.Clear();

                model.Document.Selection.Set(new[] { hatch });

                Assert.True(model.HatchEditorTab.IsTabVisible);
            }
        }

        [Fact]
        public void AHatchOfAnOwnerThatOnlyMovesIt_SelectedWithAFreeOne_OpensTheEditorForTheFreeOneAlone()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Hatched(folder, out string owned, out string free);
                model.Document.Selection.Clear();
                new CadOwnerTag("Test.Fixed", "H-1").WriteTo(model.Document.FindEntity(owned));
                model.Document.Owners.Register(new FixedOwner(CadEditRule.MoveOnly));

                model.Document.Selection.Set(new[] { owned, free });

                Assert.True(model.HatchEditorTab.IsTabVisible);
                Assert.Equal(new[] { free }, model.HatchTab.Handles);
            }
        }

        [Theory]
        [InlineData(CadEditRule.Free, true)]
        [InlineData(CadEditRule.MoveOnly, false)]
        [InlineData(CadEditRule.ReadOnly, false)]
        public void AnImageSelected_OpensTheImageTab_OnlyWhereItsOwnerLetsItBeEditedAsAnImage(
            CadEditRule rule,
            bool opens
        )
        {
            using (var folder = new TestFolder())
            {
                var database = new DxfDatabase("AC1027");
                DxfDBDictionary images = DxfRasterImageDef.CreateImageDictionary(database);
                var definition = new DxfRasterImageDef(Path.Combine(folder.FullPath, "scan.bmp"), new GeoVector2(8, 8));
                images.SetAt("scan", definition);
                var image = new DxfRasterImage(definition);
                image.SetOrientation(new GeoPoint3(0, 0, 0), new GeoVector3(40, 0, 0), new GeoVector3(0, 40, 0));
                database.ModelSpace.AppendEntity(image);
                new CadOwnerTag("Test.Fixed", "I-1").WriteTo(image);
                var model = new MainViewModel();
                model.OpenDocument(CadDocument.From(database, Path.Combine(folder.FullPath, "scan.dxf")));
                model.Document.Owners.Register(new FixedOwner(rule));

                model.Document.Selection.Set(new[] { image.Handle });

                Assert.Equal(opens, model.ImageTab.IsTabVisible);
            }
        }

        #endregion

        #region Offscreen

        private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
        {
            int count = VisualTreeHelper.GetChildrenCount(root);

            for (int i = 0; i < count; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, i);
                yield return child;

                foreach (DependencyObject below in Descendants(child))
                {
                    yield return below;
                }
            }
        }

        /// <summary>Builds the model with the extension and the workspace on a thread of their own, laid out offscreen at 1 600 by 960, and runs the check.</summary>
        private static void OnWorkspace(
            SeamExtension extension,
            Action<MainViewModel, CadWorkspace> check,
            string openFile = null
        )
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    _ = PackUriHelper.UriSchemePack;
                    var model = new MainViewModel();
                    model.AddExtension(extension);

                    if (openFile != null)
                    {
                        Assert.True(model.Open(openFile));
                    }

                    var workspace = new CadWorkspace { DataContext = model };

                    try
                    {
                        workspace.Measure(new Size(1600, 960));
                        workspace.Arrange(new Rect(0, 0, 1600, 960));
                        workspace.UpdateLayout();
                        check(model, workspace);
                    }
                    finally
                    {
                        workspace.DrawingCanvas.Dispose();
                    }
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

        #endregion
    }
}
