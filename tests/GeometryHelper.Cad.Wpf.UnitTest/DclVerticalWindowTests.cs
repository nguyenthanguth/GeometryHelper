using System;
using System.IO;
using System.IO.Packaging;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using GeometryHelper.Cad.Dcl;
using GeometryHelper.Cad.Lisp;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// The windows of two dialogs shaped like the acceptance routines' (L5 part F): E1's must be the 433 by 109 AutoCAD 2021
    /// drew with its controls at g1's rectangles, FDVAL's 227 high (the width follows the true width of its text), and
    /// <c>dimx_tile</c> of OK must give the 49 the window draws. Rows marked <c>AutoCAD 2021 (GUI g1)</c> are measured,
    /// <c>ours: F1</c> and <c>F2</c> are the part's own.
    /// </summary>
    public class DclVerticalWindowTests
    {
        private const string Kit = """
            ghedit : dialog {
                label = "Edit text"; initial_focus = "text";
                : edit_box { label = "Text:"; key = "text"; edit_width = 60; allow_accept = true; }
                spacer_1;
                ok_cancel;
            }
            ghfields : dialog {
                label = "Fields to filter ";
                : text { label = "Choose the fields to filter ..."; }
                spacer;
                : list_box { key = "lst"; multiple_select = true; }
                spacer;
                ok_cancel;
            }
            """;

        private static DclDialog Make(string name)
        {
            DclFile file = DclParser.Parse(Kit, @"C:\dcl\kit.dcl", null);
            var dialog = new DclDialog(file.NewDialog(name), 1, name, null);
            dialog.Initialise();
            dialog.ScreenX = 30000;
            dialog.ScreenY = 30000;
            return dialog;
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

        private static void Lay(DclDialogWindow window)
        {
            window.Chrome.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            window.Chrome.Arrange(new Rect(window.Chrome.DesiredSize));
            window.Chrome.UpdateLayout();
        }

        private static Point At(DclDialogWindow window, string key) =>
            window.ViewOf(key).Element.TranslatePoint(new Point(0, 0), window.Chrome);

        [Fact]
        public void TheEditDialogsWindow_IsFourHundredThirtyThreeByOneHundredNine_AsAutoCadDrewIt()
        {
            // AutoCAD 2021 (GUI g1, e1.png): the window is 433 x 109
            OnSta(() =>
            {
                var window = new DclDialogWindow(Make("ghedit"));
                Lay(window);

                Assert.Equal(433, window.Chrome.ActualWidth, 6);
                Assert.Equal(109, window.Chrome.ActualHeight, 6);
            });
        }

        [Fact]
        public void TheEditDialogsControls_StandWhereAutoCadPutThem()
        {
            // AutoCAD 2021 (GUI g1, e1.png): the edit tile at 16,37, OK at 151,73 (49 x 22) and Cancel at 228,73 (55 x 22)
            OnSta(() =>
            {
                var window = new DclDialogWindow(Make("ghedit"));
                Lay(window);

                Point tile = At(window, "text");
                Assert.Equal(16, tile.X, 6);
                Assert.Equal(37, tile.Y, 6);
                Point ok = At(window, "accept");
                Assert.Equal(151, ok.X, 6);
                Assert.Equal(73, ok.Y, 6);
                Point cancel = At(window, "cancel");
                Assert.Equal(228, cancel.X, 6);
                Assert.Equal(73, cancel.Y, 6);
            });
        }

        [Fact]
        public void TheFieldsDialogsWindow_IsTwoHundredTwentySevenHigh_AsAutoCadDrewIt()
        {
            // AutoCAD 2021 (GUI g3, fdval.png): the window is 249 x 227, its text at 16,35, its list at 16,59; the width follows
            // the width of the text in the window's font, which is not AutoCAD's 219, so only the heights are checked here
            OnSta(() =>
            {
                var window = new DclDialogWindow(Make("ghfields"));
                Lay(window);

                Assert.Equal(227, window.Chrome.ActualHeight, 6);
                DclTileView text = Assert.Single(window.Views, view => view.Tile.Kind == "text");
                Assert.Equal(35, text.Element.TranslatePoint(new Point(0, 0), window.Chrome).Y, 6);
                Assert.Equal(59, At(window, "lst").Y, 6);
                Assert.Equal(191, At(window, "accept").Y, 6);
            });
        }

        [Fact]
        public void DimxOfOk_WithTheWindowHost_IsTheWidthTheWindowDraws()
        {
            // ours: F2 (the host's metrics are the window's: OK is 49 wide, as drawn and as AutoCAD 2021 measured, not the 52 of
            // the headless character count)
            string folder = Path.Combine(Path.GetTempPath(), "gh-dcl-metrics-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);

            try
            {
                string path = Path.Combine(folder, "kit.dcl");
                File.WriteAllText(path, Kit, new UTF8Encoding(false));

                OnSta(() =>
                {
                    var host = new DclWindowHost(() => null);
                    var session = new CadCommandSession(CadDocument.From(new DxfDatabase("AC1027"), "blank.dxf"));
                    session.DclHost = host;
                    double drawn = 0;
                    Dispatcher.CurrentDispatcher.BeginInvoke(
                        new Action(() =>
                        {
                            DclDialogWindow window = Assert.Single(host.Windows);
                            drawn = ((Button)window.ViewOf("accept").Element).Width;
                            ((Button)window.ViewOf("cancel").Element).RaiseEvent(
                                new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)
                            );
                        }),
                        DispatcherPriority.Background
                    );

                    LispResult result = session.Lisp.Run(
                        "(setq id (load_dialog \""
                            + path.Replace('\\', '/')
                            + "\")) (new_dialog \"ghedit\" id \"\" (quote (30000 30000))) "
                            + "(setq w (dimx_tile \"accept\") h (dimy_tile \"accept\") c (dimx_tile \"cancel\")) (start_dialog) (list w h c)"
                    );

                    Assert.False(result.Failed, result.Error);
                    Assert.Equal("(49 22 55)", LispInterpreter.Print(result.Value, true));
                    Assert.Equal(49, drawn, 6);
                });
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }
    }
}
