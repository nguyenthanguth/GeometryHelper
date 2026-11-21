using System;
using System.IO;
using System.Text;
using GeometryHelper.Cad.Dcl;
using GeometryHelper.Cad.Lisp;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The metrics a dialog host gives (L5 part F, item 2): <c>dimx_tile</c> and <c>dimy_tile</c> measure the layout with them,
    /// so that they give what the host's window draws; with no host, or the script host, they keep
    /// <see cref="DclMetrics.Default"/>.
    /// </summary>
    public sealed class DclHostMetricsTests : IDisposable
    {
        private const string Kit = """
            ghmetrics : dialog {
                label = "Metrics";
                : edit_box { key = "eb"; label = "Name:"; edit_width = 10; }
                ok_cancel;
            }
            """;

        private readonly string _folder = Path.Combine(
            Path.GetTempPath(),
            "gh-dcl-metrics-" + Guid.NewGuid().ToString("N")
        );
        private readonly string _path;

        public DclHostMetricsTests()
        {
            Directory.CreateDirectory(_folder);
            _path = Path.Combine(_folder, "kit.dcl");
            File.WriteAllText(_path, Kit, new UTF8Encoding(false));
        }

        public void Dispose()
        {
            WaitingRoutines.EndAll();

            try
            {
                Directory.Delete(_folder, true);
            }
            catch (IOException) { }
        }

        /// <summary>A host whose window draws with metrics of its own: a button's label is 30 wide whatever it says.</summary>
        private sealed class Drawn : IDclHost
        {
            private readonly DclScriptHost _script = new DclScriptHost(new[] { "esc" });

            public DclMetrics Metrics { get; } = new DclMetrics { TextWidth = text => 30, ButtonPaddingX = 19 };

            public void Open(DclDialog dialog) => _script.Open(dialog);

            public DclEvent Next(DclDialog dialog) => _script.Next(dialog);

            public void Close(DclDialog dialog) => _script.Close(dialog);
        }

        private string Run(CadCommandSession session, string code)
        {
            string path = _path.Replace('\\', '/');
            LispResult result = session.Lisp.Run(
                "(setq id (load_dialog \"" + path + "\")) (new_dialog \"ghmetrics\" id) " + code
            );
            return result.Failed ? "error: " + result.Error : LispInterpreter.Print(result.Value, true);
        }

        [Fact]
        public void DimxOfOk_IsWhatTheHostsMetricsLayOut_NotTheDefaults()
        {
            // ours: F2 (the window draws with its own text widths: a label of 30 and padding of 19 make OK 49, where the
            // default character count of "  OK  " makes it 52)
            CadCommandSession session = Session(Blank());
            session.DclHost = new Drawn();

            string value = Run(session, "(list (dimx_tile \"accept\") (dimy_tile \"accept\") (dimx_tile \"cancel\"))");

            Assert.Equal("(49 22 49)", value);
        }

        [Fact]
        public void DimxOfOk_WithTheScriptHost_KeepsTheDefaultMetrics()
        {
            // ours: F2 (the script host and a session with no host measure with DclMetrics.Default: "  OK  " is 6 characters,
            // 36 + 16 = 52, and so is "Cancel")
            CadCommandSession session = Session(Blank());
            session.DclHost = new DclScriptHost(new[] { "esc" });

            string value = Run(session, "(list (dimx_tile \"accept\") (dimy_tile \"accept\") (dimx_tile \"cancel\"))");

            Assert.Equal("(52 22 52)", value);
        }

        [Fact]
        public void TheScriptHost_GivesTheDefaultMetrics()
        {
            // ours: F2 (the contract: the script host draws nothing, so the headless numbers hold)
            var host = new DclScriptHost(new[] { "esc" });

            DclMetrics metrics = host.Metrics;

            Assert.Equal(6, metrics.CharWidth, 6);
            Assert.Equal(13, metrics.CharHeight, 6);
            Assert.Equal(36, metrics.TextWidth("  OK  "), 6);
        }
    }
}
