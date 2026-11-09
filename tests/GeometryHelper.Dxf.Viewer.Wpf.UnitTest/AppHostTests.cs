using System;
using System.Diagnostics;
using System.IO;
using System.IO.Packaging;
using System.Threading;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The presenter's own app, GeometryHelper DXF: its window named after it, and its screenshot mode run as a process on a
    /// room built in code, drawn with no window shown.
    /// </summary>
    public sealed class AppHostTests
    {
        [Fact]
        public void TheAppWindow_IsNamedAfterTheApp_InItsTitleAndItsWorkspace()
        {
            using (var folder = new TestFolder())
            {
                string room = folder.Write("room.dxf", TestFiles.Room(out _, out _));
                string title = null;
                string workspaceName = null;
                string defaultName = null;

                OnSta(() =>
                {
                    _ = PackUriHelper.UriSchemePack;
                    var model = new MainViewModel();
                    Assert.True(model.Open(room));
                    var window = new CadAppWindow(model, "GeometryHelper DXF");

                    // The bindings are let run, as they run before a window shown is drawn.
                    Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Background);
                    title = window.Title;
                    workspaceName = window.WorkspaceView.AppName;
                    defaultName = new CadWorkspace().AppName;
                });

                Assert.Equal("GeometryHelper DXF  room.dxf", title);
                Assert.Equal("GeometryHelper DXF", workspaceName);
                Assert.Equal("GeometryHelper Drawing", defaultName);
            }
        }

        [Fact]
        public void TheDxfAppsScreenshot_DrawsTheRoomIntoAPng_AndRefusesADialogItDoesNotKnow()
        {
            using (var folder = new TestFolder())
            {
                string room = folder.Write("room.dxf", TestFiles.Room(out _, out _));
                string png = Path.Combine(folder.FullPath, "room.png");

                (int code, string output) = RunApp("--screenshot", png, room, "--size", "1200", "800");
                Assert.Equal(0, code);
                Assert.Contains("(1200 by 800)", output);
                var frame = BitmapFrame.Create(new Uri(png), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                Assert.Equal(1200, frame.PixelWidth);
                Assert.Equal(800, frame.PixelHeight);

                // A dialog of GeometryHelper Drawing's is not one of this app's.
                (int refused, string why) = RunApp("--screenshot", Path.Combine(folder.FullPath, "update.png"), "--dialog", "update");
                Assert.Equal(2, refused);
                Assert.Contains("--dialog update: no such dialog.", why);
                Assert.False(File.Exists(Path.Combine(folder.FullPath, "update.png")));
            }
        }

        /// <summary>Runs the app built beside this test's build, the same configuration, and gives its exit code and its output.</summary>
        private static (int Code, string Output) RunApp(params string[] args)
        {
            var bin = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
            string configuration = bin.Parent.Name;
            string root = bin.Parent.Parent.Parent.Parent.Parent.FullName;
            string exe = Path.Combine(root, "src", "GeometryHelper.Dxf.App", "bin", configuration, "net8.0-windows", "GeometryHelper.Dxf.App.exe");
            Assert.True(File.Exists(exe), exe);
            var start = new ProcessStartInfo(exe)
            {
                Arguments = string.Join(" ", Array.ConvertAll(args, a => "\"" + a + "\"")),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            using (Process process = Process.Start(start))
            {
                var error = process.StandardError.ReadToEndAsync();
                string output = process.StandardOutput.ReadToEnd();

                if (!process.WaitForExit(120000))
                {
                    process.Kill();
                    throw new TimeoutException("The app did not exit in two minutes.");
                }

                return (process.ExitCode, output + error.Result);
            }
        }

        private static void OnSta(Action action)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (failure != null)
            {
                throw new InvalidOperationException("The STA thread failed.", failure);
            }
        }
    }
}
