using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.Wpf.UnitTest.CanvasMouseTests;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// Drawing Properties (DWGPROPS), hyperlinks and the clean screen in the window's view model: the two dialogs driven
    /// with no window shown, the Ctrl+click that follows a link and the tooltip over its object, the Hyperlink and Group items
    /// of the Edit menu, and CLEANSCREENON, CLEANSCREENOFF and Ctrl+0.
    /// </summary>
    public class DrawingPropertiesAndHyperlinkTests
    {
        private static MainViewModel Opened(TestFolder folder, out string bottom, out string column)
        {
            var model = new MainViewModel();
            Assert.True(model.Open(folder.Write("room.dxf", TestFiles.Room(out bottom, out column))));
            return model;
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
                catch (Exception e)
                {
                    failure = e;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            Assert.Null(failure);
        }

        [Fact]
        public void DrawingProperties_ShowsTheDialogOverTheRequest_AndOkWritesItAsOneUndoStep()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Opened(folder, out _, out _);
                DrawingPropertiesViewModel shown = null;
                model.ShowDrawingProperties = dialog =>
                {
                    shown = dialog;
                    Assert.Equal("room.dxf Properties", dialog.WindowTitle);
                    Assert.True(dialog.IsGeneralTab);
                    dialog.Title = "Room";
                    dialog.Author = "J. Doe";
                    dialog.HyperlinkBase = "C:\\plans";
                    dialog.Add();
                    dialog.CustomProperties[0].Name = "Part number";
                    dialog.CustomProperties[0].Value = "B-12";
                    Assert.True(dialog.IsCustomTab);
                    Assert.True(dialog.Accept());
                    return true;
                };

                model.Execute("DWGPROPS");

                Assert.NotNull(shown);
                Assert.False(model.IsCommandRunning);
                DxfDatabaseSummaryInfo info = model.Document.Database.SummaryInfo;
                Assert.Equal("Room", info.Title);
                Assert.Equal("J. Doe", info.Author);
                Assert.Equal("C:\\plans", info.HyperlinkBase);
                Assert.Equal(
                    "Part number=B-12",
                    Assert.Single(info.CustomProperties).Key + "=" + info.CustomProperties[0].Value
                );
                Assert.Equal("DWGPROPS", model.Document.NextUndo.Name);
                Assert.True(model.Undo());
                Assert.Equal(string.Empty, model.Document.Database.SummaryInfo.Title);
            }
        }

        [Fact]
        public void DrawingProperties_CancelWritesNothing_AndWithNoWindowTheCommandSaysSo()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Opened(folder, out _, out _);

                model.Execute("DWGPROPS");
                Assert.Contains("DWGPROPS is shown by the window.", model.History);

                model.ShowDrawingProperties = dialog =>
                {
                    dialog.Title = "Never";
                    dialog.Cancel();
                    return true;
                };
                model.Execute("DWGPROPS");

                Assert.Equal(string.Empty, model.Document.Database.SummaryInfo.Title);
                Assert.Null(model.Document.NextUndo);
            }
        }

        [Fact]
        public void TheDialogRefusesACustomPropertyNamedTwice_ShowsWhyInItsFooter_AndStaysOpenUntilItIsPut_Right()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Opened(folder, out _, out _);
                string notice = null;
                model.ShowDrawingProperties = dialog =>
                {
                    dialog.Add();
                    dialog.Add();
                    dialog.CustomProperties[0].Name = "Part";
                    dialog.CustomProperties[1].Name = "part";
                    Assert.False(dialog.Accept());
                    notice = dialog.Notice;
                    Assert.NotEqual(CadDialogAction.Ok, dialog.Request.Action);

                    dialog.Delete(dialog.CustomProperties[1]);
                    Assert.True(dialog.Accept());
                    Assert.Equal(string.Empty, dialog.Notice);
                    return true;
                };

                model.Execute("DWGPROPS");

                Assert.Contains("part", notice, StringComparison.OrdinalIgnoreCase);
                Assert.Equal("Part", Assert.Single(model.Document.Database.SummaryInfo.CustomProperties).Key);
            }
        }

        [Fact]
        public void TheDialogsTabsAreOneAtATime_AndItsRowsAreTheRequests()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Opened(folder, out _, out _);
                model.ShowDrawingProperties = dialog =>
                {
                    Assert.Equal("General", dialog.Tab);
                    dialog.IsSummaryTab = true;
                    Assert.False(dialog.IsGeneralTab);
                    Assert.Equal("Summary", dialog.Tab);
                    dialog.IsStatisticsTab = true;
                    Assert.Equal(
                        new[] { "Created", "Modified", "Last saved by", "Revision number", "Total editing time" },
                        dialog.Statistics.Select(s => s.Key)
                    );
                    dialog.IsCustomTab = true;
                    Assert.True(dialog.IsCustomTab);
                    Assert.Equal(
                        new[] { "Type", "Location", "Size", "Created", "Modified", "Accessed" },
                        dialog.General.Select(g => g.Key)
                    );
                    Assert.Equal("room.dxf", dialog.FileName);
                    dialog.Cancel();
                    return true;
                };

                model.Execute("DWGPROPS");
            }
        }

        [Fact]
        public void TheTwoDialogsAreWindowsOfTheDarkLook_ThatMeasureOnAScreenWithNoWindowShown()
        {
            OnSta(() =>
            {
                using (var folder = new TestFolder())
                {
                    MainViewModel model = Opened(folder, out string bottom, out _);
                    model.Document.FindEntity(bottom).Hyperlinks.Add(new DxfHyperLink { Name = "a.pdf" });
                    var windows = new List<Window>();
                    model.ShowDrawingProperties = dialog =>
                    {
                        dialog.Add();
                        dialog.CustomProperties[0].Name = "Part number";
                        var window = new DrawingPropertiesWindow(dialog);
                        windows.Add(window);

                        foreach (string tab in new[] { "General", "Summary", "Statistics", "Custom" })
                        {
                            dialog.Tab = tab;
                            Measured(window);
                        }

                        dialog.Cancel();
                        return true;
                    };
                    model.Execute("DWGPROPS");
                    string linkTitle = null;
                    model.ShowHyperlink = dialog =>
                    {
                        var window = new HyperlinkWindow(dialog);
                        linkTitle = dialog.WindowTitle;
                        windows.Add(window);
                        Assert.True(dialog.CanRemove);

                        dialog.IsViewKind = true;
                        Measured(window);
                        dialog.IsEmailKind = true;
                        Measured(window);
                        dialog.IsFileKind = true;
                        Measured(window);
                        dialog.Cancel();
                        return true;
                    };
                    model.Document.Selection.Set(new[] { bottom });
                    model.Execute("HYPERLINK");

                    Assert.Equal(2, windows.Count);
                    Assert.Equal("Edit Hyperlink", linkTitle);
                }
            });
        }

        private static void Measured(Window window)
        {
            FrameworkElement body = window is DrawingPropertiesWindow props
                ? props.Body
                : ((HyperlinkWindow)window).Body;
            body.Measure(new Size(window.Width, window.Height));
            body.Arrange(new Rect(0, 0, window.Width, window.Height));
            body.UpdateLayout();
            Assert.True(body.ActualWidth > 0, window.Title);
        }

        [Fact]
        public void Hyperlink_ShowsInsertHyperlinkOverTheSelection_AndOkAttachesTheLink()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Opened(folder, out string bottom, out _);
                HyperlinkViewModel shown = null;
                model.ShowHyperlink = dialog =>
                {
                    shown = dialog;
                    dialog.Address = "https://example.com/room.pdf";
                    dialog.TextToDisplay = "Room plan";
                    Assert.True(dialog.IsFileKind);
                    Assert.True(dialog.Accept());
                    return true;
                };
                model.Document.Selection.Set(new[] { bottom });

                model.Execute("HYPERLINK");

                Assert.Equal("Insert Hyperlink", shown.WindowTitle);
                Assert.False(shown.CanRemove);
                DxfHyperLink link = Assert.Single(model.Document.FindEntity(bottom).Hyperlinks);
                Assert.Equal("https://example.com/room.pdf", link.Name);
                Assert.Equal("Room plan", link.Description);
                Assert.Equal("HYPERLINK", model.Document.NextUndo.Name);
            }
        }

        [Fact]
        public void TheHyperlinkDialog_ChoosesOneKindAtATime_RefusesWhatCannotBeWritten_AndRemoveLinkRemoves()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Opened(folder, out string bottom, out _);
                model.Document.FindEntity(bottom).Hyperlinks.Add(new DxfHyperLink { Name = "old.pdf" });
                string refusal = null;
                bool removeOffered = false;
                model.ShowHyperlink = dialog =>
                {
                    removeOffered = dialog.CanRemove;
                    dialog.IsEmailKind = true;
                    Assert.False(dialog.IsFileKind);
                    Assert.True(dialog.IsEmailKind);
                    Assert.False(dialog.Accept());
                    refusal = dialog.Notice;
                    dialog.IsViewKind = true;
                    Assert.False(dialog.Accept());
                    Assert.Equal("Choose a view of this drawing.", dialog.Notice);
                    dialog.RemoveLink();
                    return true;
                };
                model.Document.Selection.Set(new[] { bottom });

                model.Execute("HYPERLINK");

                Assert.True(removeOffered);
                Assert.Equal("Type an e-mail address.", refusal);
                Assert.Empty(model.Document.FindEntity(bottom).Hyperlinks);
            }
        }

        [Fact]
        public void BrowseGivesTheFileChosenAsTheAddress_AndNothingWhereItIsCancelled()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Opened(folder, out string bottom, out _);
                model.ShowHyperlink = dialog =>
                {
                    Assert.False(dialog.Browse());
                    dialog.BrowseFile = () => "C:\\plans\\room.pdf";
                    Assert.True(dialog.Browse());
                    Assert.Equal("C:\\plans\\room.pdf", dialog.Address);
                    dialog.Cancel();
                    return true;
                };
                model.Document.Selection.Set(new[] { bottom });

                model.Execute("HYPERLINK");

                Assert.Empty(model.Document.FindEntity(bottom).Hyperlinks);
            }
        }

        [Fact]
        public void FollowingALink_OpensAWebAddressOrAFileWithTheSystem_AndSaysWhatItCannotFind()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Opened(folder, out string bottom, out string column);
                var opened = new List<string>();
                model.OpenLink = address =>
                {
                    opened.Add(address);
                    return true;
                };
                DxfEntity line = model.Document.FindEntity(bottom);
                line.Hyperlinks.Add(new DxfHyperLink { Name = "https://example.com/room.pdf" });

                Assert.True(model.FollowHyperlink(bottom));
                Assert.Equal(new[] { "https://example.com/room.pdf" }, opened);

                string pdf = folder.WriteText("detail.pdf", "%PDF");
                line.Hyperlinks.Clear();
                line.Hyperlinks.Add(new DxfHyperLink { Name = "detail.pdf" });
                Assert.True(model.FollowHyperlink(bottom));
                Assert.Equal(pdf, opened[1], ignoreCase: true);

                line.Hyperlinks.Clear();
                line.Hyperlinks.Add(new DxfHyperLink { Name = "missing.pdf" });
                Assert.False(model.FollowHyperlink(bottom));
                Assert.Contains("Hyperlink destination cannot be found.", model.History);

                line.Hyperlinks.Clear();
                line.Hyperlinks.Add(new DxfHyperLink { Name = "mailto:me@example.com?subject=Hi" });
                Assert.True(model.FollowHyperlink(bottom));
                Assert.Equal("mailto:me@example.com?subject=Hi", opened[2]);

                line.Hyperlinks.Clear();
                line.Hyperlinks.Add(new DxfHyperLink { Description = "nowhere" });
                Assert.False(model.FollowHyperlink(bottom));
                Assert.Contains("** No hyperlink to navigate to **", model.History);

                // An object with no link, a handle that is none, and a command running follow nothing.
                Assert.False(model.FollowHyperlink(column));
                Assert.False(model.FollowHyperlink("ZZZZ"));
                line.Hyperlinks.Add(new DxfHyperLink { Name = "https://example.com" });
                model.StartCommand("LINE");
                Assert.False(model.FollowHyperlink(bottom));
            }
        }

        [Fact]
        public void FollowingALinkToADrawing_OpensItInThePresenter()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Opened(folder, out string bottom, out _);
                string other = folder.Write("other.dxf", TestFiles.Room(out _, out _));
                model.Document.FindEntity(bottom).Hyperlinks.Add(new DxfHyperLink { Name = "other.dxf" });

                Assert.True(model.FollowHyperlink(bottom));

                Assert.Equal(other, model.Document.FileName, ignoreCase: true);
            }
        }

        [Fact]
        public void FollowingALinkToAViewOfTheDrawing_RestoresTheView_AndAViewThatIsGoneIsSaidNotFound()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Opened(folder, out string bottom, out _);
                model.Document.FindEntity(bottom).Hyperlinks.Add(new DxfHyperLink { SubLocation = "ENTRANCE" });
                Assert.False(model.FollowHyperlink(bottom));
                Assert.Contains("Hyperlink destination cannot be found.", model.History);

                model.Execute("-VIEW");
                model.Execute("Save");
                model.Execute("ENTRANCE");

                Assert.True(model.FollowHyperlink(bottom));
                Assert.False(model.IsCommandRunning);
                Assert.Contains(model.History, line => line.Contains("Restore"));
            }
        }

        [Fact]
        public void TheTooltipOfAnObjectWithALink_IsItsTextAndTheCtrlClickHint()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Opened(folder, out string bottom, out string column);
                model
                    .Document.FindEntity(bottom)
                    .Hyperlinks.Add(new DxfHyperLink { Name = "a.pdf", Description = "Plan" });

                Assert.Equal("Plan\nCTRL + click to follow link", model.HyperlinkTooltip(bottom));
                Assert.Null(model.HyperlinkTooltip(column));
                Assert.Null(model.HyperlinkTooltip("ZZZZ"));
            }
        }

        [Fact]
        public void CtrlClickOnAnObjectWithALink_FollowsItAndSelectsNothing_AClickWithoutCtrlPicksIt()
        {
            using (var folder = new TestFolder())
            {
                var model = new MainViewModel();
                model.Open(folder.Write("room.dxf", TestFiles.Room(out string bottom, out string column)));
                model
                    .Document.FindEntity(bottom)
                    .Hyperlinks.Add(new DxfHyperLink { Name = "a.pdf", Description = "Plan" });
                OnSta(() =>
                {
                    var canvas = new CadCanvas
                    {
                        Document = model.Document,
                        Space = model.Space,
                        View = model.View,
                        Session = model.Session,
                        Snap = model.Snap,
                        HyperlinkTooltip = model.HyperlinkTooltip,
                    };
                    canvas.PixelSizeChanged += (s, e) => model.SetCanvasSize(canvas.PixelWidth, canvas.PixelHeight);
                    canvas.Measure(new Size(800, 600));
                    canvas.Arrange(new Rect(0, 0, 800, 600));
                    canvas.RenderNow();
                    var followed = new List<string>();
                    canvas.HyperlinkClicked += (s, e) => followed.Add(e.Handle);
                    Point onLine = At(canvas, 2000, 0);

                    // Over the object the tooltip shows; over nothing it does not.
                    Mouse(canvas, UIElement.MouseMoveEvent, onLine);
                    Assert.Equal("Plan\nCTRL + click to follow link", canvas.LinkTip);
                    Mouse(canvas, UIElement.MouseMoveEvent, At(canvas, 2000, 700));
                    Assert.Null(canvas.LinkTip);

                    // Ctrl held: followed, and not selected.
                    canvas.KeysHeld = () => ModifierKeys.Control;
                    Click(canvas, onLine);
                    Assert.Equal(new[] { bottom }, followed);
                    Assert.Empty(model.Document.Selection.Handles);

                    // No Ctrl: a plain pick.
                    canvas.KeysHeld = () => ModifierKeys.None;
                    Click(canvas, onLine);
                    Assert.Equal(new[] { bottom }, followed);
                    Assert.Contains(bottom, model.Document.Selection.Handles);

                    // The column has no link: Ctrl+click picks it.
                    model.Document.Selection.Clear();
                    canvas.KeysHeld = () => ModifierKeys.Control;
                    Click(canvas, At(canvas, 2100, 1500));
                    Assert.Equal(new[] { bottom }, followed);
                    Assert.Contains(column, model.Document.Selection.Handles);
                    canvas.Dispose();
                });
            }
        }

        [Fact]
        public void TheEditMenuOfAnObjectWithALink_HasAHyperlinkMenuBeforeTheClipboard()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Opened(folder, out string bottom, out string column);
                model
                    .Document.FindEntity(bottom)
                    .Hyperlinks.Add(new DxfHyperLink { Name = "https://example.com/a.pdf", Description = "Plan" });
                string copied = null;
                var opened = new List<string>();
                model.WriteClipboardText = text => copied = text;
                model.OpenLink = address =>
                {
                    opened.Add(address);
                    return true;
                };

                model.Document.Selection.Set(new[] { column });
                Assert.DoesNotContain(model.CanvasMenu(), i => i.Header == "Hyperlink");

                model.Document.Selection.Set(new[] { bottom });
                IReadOnlyList<CadMenuItemViewModel> items = model.CanvasMenu();
                int at = items.ToList().FindIndex(i => i.Header == "Hyperlink");
                Assert.True(at >= 0);
                Assert.Equal("Clipboard", items[at + 2].Header);
                CadMenuItemViewModel menu = items[at];
                Assert.Equal(
                    new[] { "Open \"Plan\"", "Copy Hyperlink", "Edit Hyperlink..." },
                    menu.Items.Select(i => i.Header)
                );
                Assert.All(menu.Items, i => Assert.True(i.IsEnabled));

                menu.Items[1].Command.Execute(null);
                Assert.Equal("https://example.com/a.pdf", copied);
                menu.Items[0].Command.Execute(null);
                Assert.Equal(new[] { "https://example.com/a.pdf" }, opened);

                string title = null;
                model.ShowHyperlink = dialog =>
                {
                    title = dialog.WindowTitle;
                    dialog.Cancel();
                    return true;
                };
                menu.Items[2].Command.Execute(null);
                Assert.Equal("Edit Hyperlink", title);
            }
        }

        [Fact]
        public void TheGroupMenuOfTheEditMenu_AddsAndRemovesThroughGroupedit_NotGreyAnyMore()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Opened(folder, out string bottom, out string column);
                model.Document.Selection.Set(new[] { bottom });

                CadMenuItemViewModel group = model.CanvasMenu().Single(i => i.Header == "Group");
                Assert.Equal(
                    new[] { "Group", "Ungroup", "Add to Group", "Remove from Group" },
                    group.Items.Select(i => i.Header)
                );
                Assert.All(group.Items, i => Assert.True(i.IsEnabled));
                Assert.Null(group.Items[2].ToolTip);

                group.Items[2].Command.Execute(null);

                Assert.Equal("GROUPEDIT", model.Session.Current.Name);
                Assert.Equal("Select group or [Name]:", model.Prompt);
            }
        }

        [Fact]
        public void CleanScreenOn_HidesTheRibbonAndThePanelsAndKeepsTheCommandLine_CleanScreenOffBringsThemBack()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Opened(folder, out _, out _);
                model.IsRibbonShown = true;
                model.IsPropertiesPanelShown = true;
                model.IsFileListShown = false;
                Assert.True(model.IsCommandLineShown);
                Assert.False(model.IsCleanScreen);

                model.Execute("CLEANSCREENON");

                Assert.True(model.IsCleanScreen);
                Assert.False(model.IsRibbonShown);
                Assert.False(model.IsPropertiesPanelShown);
                Assert.False(model.IsFileListShown);
                Assert.True(model.IsCommandLineShown);

                // A second CLEANSCREENON keeps what the first remembered.
                model.Execute("CLEANSCREENON");
                model.Execute("CLEANSCREENOFF");

                Assert.False(model.IsCleanScreen);
                Assert.True(model.IsRibbonShown);
                Assert.True(model.IsPropertiesPanelShown);
                Assert.False(model.IsFileListShown);
                Assert.True(model.IsCommandLineShown);

                // Off with it off is nothing.
                model.Execute("CLEANSCREENOFF");
                Assert.True(model.IsRibbonShown);
            }
        }

        [Fact]
        public void CtrlZero_SwitchesTheCleanScreenOnAndOff_AsAutoCadsCtrlZeroDoes()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Opened(folder, out _, out _);
                model.IsRibbonShown = true;

                Assert.True(model.RunKey("Ctrl+0"));
                Assert.True(model.IsCleanScreen);
                Assert.False(model.IsRibbonShown);

                Assert.True(model.RunKey("Ctrl+0"));
                Assert.False(model.IsCleanScreen);
                Assert.True(model.IsRibbonShown);
            }
        }

        [Fact]
        public void CleanScreenCommandsNeedNoDrawing()
        {
            var model = new MainViewModel();
            model.IsRibbonShown = true;

            model.Execute("CLEANSCREENON");
            Assert.False(model.IsRibbonShown);
            model.Execute("CLEANSCREENOFF");
            Assert.True(model.IsRibbonShown);
        }
    }
}
