using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Enums;
using Xunit;

namespace GeometryHelper.UnitTest.Common
{
    /// <summary>
    /// GeometryHelperLog is process-wide state. The suite runs serially (see AssemblyInfo.cs) and every test here puts
    /// the settings back when it is done.
    /// </summary>
    public class GeometryHelperLogTests : IDisposable
    {
        private readonly List<(GeometryHelperLogLevel Level, string Message, Exception Exception)> _written =
            new List<(GeometryHelperLogLevel Level, string Message, Exception Exception)>();

        public GeometryHelperLogTests()
        {
            GeometryHelperLog.Enable = true;
            GeometryHelperLog.Writer = (level, message, exception) => _written.Add((level, message, exception));
        }

        public void Dispose()
        {
            GeometryHelperLog.Writer = null;
            GeometryHelperLog.Enable = true;
        }

        [Fact]
        public void EachMethod_WritesItsLevel_ItsMessage_AndTheException()
        {
            IOException error = new IOException("The file is in use.");

            GeometryHelperLog.Debug("d");
            GeometryHelperLog.Info("i");
            GeometryHelperLog.Warn("w", error);
            GeometryHelperLog.Err("e", error);

            Assert.Equal(new[] { GeometryHelperLogLevel.Debug, GeometryHelperLogLevel.Info, GeometryHelperLogLevel.Warn, GeometryHelperLogLevel.Err }, _written.Select(w => w.Level));
            Assert.Equal(new[] { "d", "i", "w", "e" }, _written.Select(w => w.Message));
            Assert.Null(_written[0].Exception);
            Assert.Same(error, _written[2].Exception);
            Assert.Same(error, _written[3].Exception);
        }

        [Fact]
        public void Disabled_WritesNothing()
        {
            GeometryHelperLog.Enable = false;

            GeometryHelperLog.Debug("d");
            GeometryHelperLog.Info("i");
            GeometryHelperLog.Warn("w");
            GeometryHelperLog.Err("e");

            Assert.Empty(_written);
        }

        [Fact]
        public void AWriterThatThrows_DoesNotBreakTheCaller()
        {
            GeometryHelperLog.Writer = (level, message, exception) => throw new InvalidOperationException("The writer is broken.");

            Assert.Null(Record.Exception(() => GeometryHelperLog.Warn("w")));
            Assert.Null(Record.Exception(() => GeometryHelperLog.Err("e", new Exception("inner"))));
        }

        [Fact]
        public void ANullMessage_IsWrittenAsEmpty()
        {
            GeometryHelperLog.Info(null);

            Assert.Equal(string.Empty, _written.Single().Message);
        }

        [Fact]
        public void WithoutAWriter_MessagesGoToTraceUnderTheGeometryHelperCategory()
        {
            GeometryHelperLog.Writer = null;
            StringWriter text = new StringWriter();
            TextWriterTraceListener listener = new TextWriterTraceListener(text);
            Trace.Listeners.Add(listener);

            try
            {
                GeometryHelperLog.Warn("Reference model 7 is not IFC.", new IOException("locked"));
                listener.Flush();
            }
            finally
            {
                Trace.Listeners.Remove(listener);
            }

            Assert.Contains("GeometryHelper: WARN Reference model 7 is not IFC. [IOException: locked]", text.ToString());
        }

        [Fact]
        public void Format_NamesTheLevelAndTheException()
        {
            Assert.Equal("INFO done", GeometryHelperLog.Format(GeometryHelperLogLevel.Info, "done", null));
            Assert.Equal("DEBUG ", GeometryHelperLog.Format(GeometryHelperLogLevel.Debug, null, null));
            Assert.Equal("ERR failed [ArgumentException: bad]", GeometryHelperLog.Format(GeometryHelperLogLevel.Err, "failed", new ArgumentException("bad")));
        }
    }
}
