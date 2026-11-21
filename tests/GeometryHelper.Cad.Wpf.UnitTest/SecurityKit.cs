using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using static GeometryHelper.Cad.Wpf.UnitTest.PythonWindowKit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>What the tests of the load warning, the Security page and the gate share (L6, wave 2).</summary>
    internal static class SecurityKit
    {
        #region Load security

        /// <summary>The write time the tests give a code file: 7 March 2026, 09:05 local time (day 7 and month 3 apart, 24-hour clock).</summary>
        public static readonly DateTime WrittenAt = new DateTime(2026, 3, 7, 9, 5, 0, DateTimeKind.Local);

        /// <summary>Writes a file of code in the folder, sets its last write time and gives its full path.</summary>
        public static string Code(TestFolder folder, string name, string text = "(princ)\n", DateTime? writtenAt = null)
        {
            string path = folder.WriteText(name, text);
            File.SetLastWriteTime(path, writtenAt ?? WrittenAt);
            return path;
        }

        /// <summary>The SHA-256 of a file's bytes in lower-case hex, as an Always Load entry of the settings carries it before the bar.</summary>
        public static string Sha256(string path)
        {
            using (SHA256 hash = SHA256.Create())
            {
                return string.Concat(hash.ComputeHash(File.ReadAllBytes(path)).Select(b => b.ToString("x2")));
            }
        }

        /// <summary>Runs <paramref name="body"/> with the thread's culture set to <paramref name="name"/>, as a user whose Windows is set to it.</summary>
        public static void InCulture(string name, Action body)
        {
            CultureInfo before = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = new CultureInfo(name);

            try
            {
                body();
            }
            finally
            {
                CultureInfo.CurrentCulture = before;
            }
        }

        /// <summary>
        /// Shows a modal window over an owner that is off the screen (as the DCL window tests do), runs
        /// <paramref name="whileShown"/> on the window's dispatcher once the window is up, closes the window as its cross does
        /// if the body has not answered, and gives what <paramref name="ask"/> returned. What the body throws is thrown here;
        /// a window still open after 30 seconds fails the test.
        /// </summary>
        public static T AskOver<T>(Func<Window, T> ask, Action<Window> whileShown)
        {
            T answer = default(T);
            ExceptionDispatchInfo failure = null;

            OnWindowThread(() =>
            {
                var owner = new Window
                {
                    Width = 400,
                    Height = 300,
                    Left = 30100,
                    Top = 30100,
                    ShowInTaskbar = false,
                };
                owner.Show();

                using (var watchdog = new Watchdog())
                {
                    Dispatcher.CurrentDispatcher.BeginInvoke(
                        DispatcherPriority.ApplicationIdle,
                        new Action(() =>
                        {
                            Window shown = owner.OwnedWindows.Cast<Window>().Single();

                            try
                            {
                                whileShown(shown);
                            }
                            catch (Exception ex)
                            {
                                failure = ExceptionDispatchInfo.Capture(ex);
                            }
                            finally
                            {
                                if (shown.IsVisible)
                                {
                                    shown.Close();
                                }
                            }
                        })
                    );
                    answer = ask(owner);
                    Xunit.Assert.False(
                        watchdog.TimedOut,
                        "The window was still open after " + GiveUpSeconds + " seconds."
                    );
                }

                owner.Close();
            });

            failure?.Throw();
            return answer;
        }

        /// <summary>The text of every text block in the window's visual tree, in order.</summary>
        public static List<string> Texts(Window window) =>
            Descendants(window).OfType<TextBlock>().Select(t => t.Text).ToList();

        /// <summary>The one button of the window whose label is <paramref name="label"/>.</summary>
        public static Button ButtonLabelled(Window window, string label) =>
            Descendants(window).OfType<Button>().Single(b => b.Content as string == label);

        /// <summary>
        /// A folder the app cannot create a file in: the current user is denied the right to create files in it, which is what
        /// the probe of the writable-folder warning (D915) tries. Disposing gives the right back so that the folder can go.
        /// </summary>
        public sealed class ReadOnlyFolder : IDisposable
        {
            private readonly DirectoryInfo _folder;
            private readonly FileSystemAccessRule _deny;

            public ReadOnlyFolder(string path)
            {
                Directory.CreateDirectory(path);
                _folder = new DirectoryInfo(path);
                _deny = new FileSystemAccessRule(
                    WindowsIdentity.GetCurrent().User,
                    FileSystemRights.CreateFiles,
                    InheritanceFlags.None,
                    PropagationFlags.None,
                    AccessControlType.Deny
                );
                DirectorySecurity security = _folder.GetAccessControl();
                security.AddAccessRule(_deny);
                _folder.SetAccessControl(security);
                FullPath = path;
            }

            public string FullPath { get; }

            public void Dispose()
            {
                DirectorySecurity security = _folder.GetAccessControl();
                security.RemoveAccessRule(_deny);
                _folder.SetAccessControl(security);
            }
        }

        #endregion
    }
}
