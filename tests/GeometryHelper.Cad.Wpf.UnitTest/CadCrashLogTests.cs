using System;
using System.IO;
using System.Text;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// The crash log of the apps: what an entry holds, appended, cut at its size cap, and never throwing. The message box and
    /// the handlers themselves are not driven here, as they need a running window.
    /// </summary>
    public class CadCrashLogTests
    {
        private static Exception Thrown(string message, Exception inner = null)
        {
            try
            {
                throw new InvalidOperationException(message, inner);
            }
            catch (InvalidOperationException ex)
            {
                return ex;
            }
        }

        [Fact]
        public void AnEntry_HoldsTheTypeMessageStackInnerExceptionsVersionTimeAndLastCommand()
        {
            Exception outer = Thrown("outer failed", new ArgumentException("inner failed"));

            string entry = CadCrashLog.Format(
                outer,
                "Application.DispatcherUnhandledException",
                "PLOT",
                new DateTime(2026, 11, 17, 15, 19, 7, DateTimeKind.Local)
            );

            Assert.Contains("2026-11-17 15:19:07.000", entry);
            Assert.Contains("Application.DispatcherUnhandledException", entry);
            Assert.Contains("Last command: PLOT", entry);
            Assert.Contains("Fatal: no", entry);
            Assert.Contains("App: ", entry);
            Assert.Contains("System.InvalidOperationException: outer failed", entry);
            Assert.Contains("System.ArgumentException: inner failed", entry);
            Assert.Contains("CadCrashLogTests.Thrown", entry);
        }

        [Fact]
        public void WithNoCommandRun_TheEntrySaysNone()
        {
            Assert.Contains("Last command: (none)", CadCrashLog.Format(Thrown("x"), "test", null, DateTime.Now));
        }

        [Fact]
        public void TwoEntries_AreAppendedToTheSameFile_AndTheFolderIsMade()
        {
            string folder = Path.Combine(Path.GetTempPath(), "gh-crashlog-" + Guid.NewGuid().ToString("N"));

            try
            {
                string path = Path.Combine(folder, "inner", "crash.log");

                Assert.Equal(path, CadCrashLog.Write(path, Thrown("first"), "test", "LINE"));
                Assert.Equal(path, CadCrashLog.Write(path, Thrown("second"), "test", "MOVE"));

                string text = File.ReadAllText(path, Encoding.UTF8);
                Assert.Contains("first", text);
                Assert.Contains("Last command: LINE", text);
                Assert.Contains("second", text);
                Assert.Contains("Last command: MOVE", text);
                Assert.True(
                    text.IndexOf("first", StringComparison.Ordinal) < text.IndexOf("second", StringComparison.Ordinal)
                );
            }
            finally
            {
                if (Directory.Exists(folder))
                {
                    Directory.Delete(folder, true);
                }
            }
        }

        [Fact]
        public void AFilePastTheCap_IsMovedToTheOldFile_AndTheLogBeginsAgain()
        {
            string folder = Path.Combine(Path.GetTempPath(), "gh-crashlog-" + Guid.NewGuid().ToString("N"));

            try
            {
                Directory.CreateDirectory(folder);
                string path = Path.Combine(folder, "crash.log");
                File.WriteAllText(path, new string('x', (int)CadCrashLog.MaxBytes + 1));

                CadCrashLog.Write(path, Thrown("after the cap"), "test", null);

                Assert.Equal((int)CadCrashLog.MaxBytes + 1, new FileInfo(Path.Combine(folder, "crash.old.log")).Length);
                string text = File.ReadAllText(path);
                Assert.Contains("after the cap", text);
                Assert.DoesNotContain("xxxx", text);
            }
            finally
            {
                if (Directory.Exists(folder))
                {
                    Directory.Delete(folder, true);
                }
            }
        }

        [Fact]
        public void APathThatCannotBeWritten_ReturnsNullAndDoesNotThrow()
        {
            string folder = Path.Combine(Path.GetTempPath(), "gh-crashlog-" + Guid.NewGuid().ToString("N"));

            try
            {
                Directory.CreateDirectory(folder);

                // A folder where the file should be: appending to it fails.
                string path = Path.Combine(folder, "crash.log");
                Directory.CreateDirectory(path);

                Assert.Null(CadCrashLog.Write(path, Thrown("x"), "test", null));
                Assert.Null(CadCrashLog.Write("::bad|path?", Thrown("x"), "test", null));
            }
            finally
            {
                if (Directory.Exists(folder))
                {
                    Directory.Delete(folder, true);
                }
            }
        }

        [Fact]
        public void RecordWithACommandThatThrows_StillWritesTheEntry()
        {
            string folder = Path.Combine(Path.GetTempPath(), "gh-crashlog-" + Guid.NewGuid().ToString("N"));

            try
            {
                string path = Path.Combine(folder, "crash.log");

                string written = CadCrashLog.Record(path, Thrown("boom"), "TaskScheduler.UnobservedTaskException", () =>
                    throw new InvalidOperationException("no session")
                );

                Assert.Equal(path, written);
                Assert.Contains("Last command: (none)", File.ReadAllText(path));
            }
            finally
            {
                if (Directory.Exists(folder))
                {
                    Directory.Delete(folder, true);
                }
            }
        }

        [Fact]
        public void OutOfMemoryAndNativeFaultsAreFatal_OtherExceptionsAreNot()
        {
            Assert.True(CadCrashLog.IsFatal(new OutOfMemoryException()));
            Assert.True(CadCrashLog.IsFatal(new AccessViolationException()));
            Assert.True(CadCrashLog.IsFatal(new System.Runtime.InteropServices.SEHException()));
            Assert.False(CadCrashLog.IsFatal(new InvalidOperationException()));
            Assert.False(CadCrashLog.IsFatal(new NullReferenceException()));
        }

        [Fact]
        public void AMessageEndingInAPeriod_IsNotGivenASecondOne()
        {
            string message = CadCrashLog.Message(Thrown("The file is gone."), @"C:\x\crash.log");

            Assert.StartsWith("An error occurred: The file is gone. Details", message);
        }

        [Fact]
        public void TheMessage_NamesTheErrorTheFileAndThatTheDrawingIsKept()
        {
            string message = CadCrashLog.Message(
                Thrown("DialogResult can be set only after Window is created"),
                @"C:\x\crash.log"
            );

            Assert.Equal(
                "An error occurred: DialogResult can be set only after Window is created. Details were written to C:\\x\\crash.log. The drawing is kept; save your work.",
                message
            );
            Assert.Contains("could not be written", CadCrashLog.Message(Thrown("x"), null));
            Assert.Contains("will close", CadCrashLog.Message(new OutOfMemoryException(), @"C:\x\crash.log"));
        }
    }
}
