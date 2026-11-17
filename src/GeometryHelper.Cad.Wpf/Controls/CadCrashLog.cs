using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// The crash log of the apps: what an exception nobody caught leaves in <c>%AppData%\GeometryHelper\Drawing\crash.log</c>,
    /// and the handlers that write it. An exception of the window's thread is written, shown in a message box and marked
    /// handled, so the app and its drawings stay; an exception of another thread, or one nobody observed in a task, is written
    /// and left to run its course. Out of memory and the faults of native code (an access violation) are written too but
    /// end the app, as the process cannot go on after them; a stack overflow ends it before any handler runs and is not
    /// written at all.
    /// </summary>
    internal static class CadCrashLog
    {
        /// <summary>The size past which the log is moved to <c>crash.old.log</c> and begun again, so that the two files hold at most twice this.</summary>
        internal const long MaxBytes = 256 * 1024;

        private static readonly object Gate = new object();

        /// <summary>Gets the file the log is kept in: %AppData%\GeometryHelper\Drawing\crash.log.</summary>
        internal static string DefaultPath =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "GeometryHelper",
                "Drawing",
                "crash.log"
            );

        /// <summary>Says whether the process cannot be trusted to go on after the exception: out of memory or a fault of native code.</summary>
        internal static bool IsFatal(Exception exception) =>
            exception is OutOfMemoryException || exception is AccessViolationException || exception is SEHException;

        /// <summary>Writes one entry: the exception with its inner ones, where it was caught, the app's version and the time.</summary>
        internal static string Format(Exception exception, string source, string lastCommand, DateTime time)
        {
            var text = new StringBuilder();
            text.AppendLine(
                "==== " + time.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture) + " ===="
            );
            Assembly entry = Assembly.GetEntryAssembly() ?? typeof(CadCrashLog).Assembly;
            text.AppendLine(
                "App: "
                    + entry.GetName().Name
                    + " "
                    + entry.GetName().Version
                    + ", "
                    + RuntimeInformation.FrameworkDescription
            );
            text.AppendLine("OS: " + RuntimeInformation.OSDescription);
            text.AppendLine("Caught by: " + source);
            text.AppendLine("Last command: " + (string.IsNullOrEmpty(lastCommand) ? "(none)" : lastCommand));
            text.AppendLine("Fatal: " + (IsFatal(exception) ? "yes" : "no"));
            text.AppendLine(exception == null ? "(no exception)" : exception.ToString());
            text.AppendLine();
            return text.ToString();
        }

        /// <summary>
        /// Appends the entry to the file, first moving a file past <see cref="MaxBytes"/> to <c>crash.old.log</c> beside it. It
        /// never throws: a log that cannot be written must not hide the crash it was written for.
        /// </summary>
        /// <returns>The path written; null where it could not be.</returns>
        internal static string Write(string path, Exception exception, string source, string lastCommand)
        {
            try
            {
                string entry = Format(exception, source, lastCommand, DateTime.Now);

                lock (Gate)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
                    var info = new FileInfo(path);

                    if (info.Exists && info.Length > MaxBytes)
                    {
                        string old = Path.Combine(
                            info.DirectoryName,
                            Path.GetFileNameWithoutExtension(path) + ".old" + info.Extension
                        );
                        File.Copy(path, old, true);
                        File.Delete(path);
                    }

                    File.AppendAllText(path, entry, new UTF8Encoding(false));
                }

                return path;
            }
            catch (Exception ex)
                when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException)
            {
                GeometryHelperLog.Warn("CadCrashLog: the crash log could not be written.", ex);
                return null;
            }
        }

        /// <summary>
        /// Writes the exception to the file and to <see cref="GeometryHelperLog"/>; the message box and the Handled mark of a
        /// dispatcher exception are the caller's.
        /// </summary>
        /// <returns>The path written; null where it could not be.</returns>
        internal static string Record(string path, Exception exception, string source, Func<string> lastCommand)
        {
            string last = null;

            try
            {
                last = lastCommand?.Invoke();
            }
            catch (Exception)
            {
                // The command is only for the log; a session in the middle of a crash may not say it.
            }

            GeometryHelperLog.Warn("CadCrashLog: unhandled exception caught by " + source + ".", exception);
            return Write(path, exception, source, last);
        }

        /// <summary>What the message box says of an exception of the window's thread.</summary>
        internal static string Message(Exception exception, string path)
        {
            string where =
                path == null
                    ? "The details could not be written to the crash log."
                    : "Details were written to " + path + ".";
            return IsFatal(exception)
                ? "A fatal error occurred: "
                    + exception.Message
                    + ". "
                    + where
                    + " The app cannot go on and will close; the drawings are not saved."
                : "An error occurred: "
                    + exception.Message.TrimEnd('.')
                    + ". "
                    + where
                    + " The drawing is kept; save your work.";
        }

        /// <summary>
        /// Puts the handlers in: the window's thread (written, shown, handled), any other thread (written) and a task whose
        /// exception nobody observed (written and observed).
        /// </summary>
        /// <param name="application">The application whose dispatcher is watched.</param>
        /// <param name="appName">The name in the title of the message box.</param>
        /// <param name="lastCommand">Says the command run last, for the log; null for none.</param>
        /// <param name="path">The log file; null for <see cref="DefaultPath"/>.</param>
        internal static void Install(
            Application application,
            string appName,
            Func<string> lastCommand,
            string path = null
        )
        {
            path = path ?? DefaultPath;

            application.DispatcherUnhandledException += (s, e) =>
            {
                string written = Record(path, e.Exception, "Application.DispatcherUnhandledException", lastCommand);

                try
                {
                    MessageBox.Show(Message(e.Exception, written), appName, MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch (Exception ex)
                {
                    GeometryHelperLog.Warn("CadCrashLog: the message box could not be shown.", ex);
                }

                e.Handled = !IsFatal(e.Exception);
            };

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                Record(
                    path,
                    e.ExceptionObject as Exception
                        ?? new InvalidOperationException(
                            Convert.ToString(e.ExceptionObject, CultureInfo.InvariantCulture)
                        ),
                    "AppDomain.UnhandledException (terminating: " + e.IsTerminating + ")",
                    lastCommand
                );

            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                Record(path, e.Exception, "TaskScheduler.UnobservedTaskException", lastCommand);
                e.SetObserved();
            };
        }
    }
}
