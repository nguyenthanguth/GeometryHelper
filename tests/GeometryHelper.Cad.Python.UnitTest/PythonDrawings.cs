using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Cad.Python.UnitTest
{
    /// <summary>The drawings, answers, scripts and fake Python installs the Python tests share.</summary>
    internal static class PythonDrawings
    {
        #region Drawings

        /// <summary>A new empty drawing of R2013 in millimetres whose tab is called <paramref name="name"/>.</summary>
        public static CadDocument Blank(string name = "blank.dxf") => CadDocument.From(new DxfDatabase("AC1027"), name);

        /// <summary>One edit of the drawing, as a script makes one through the object model: a line from (0, y) to (100, y).</summary>
        public static DxfLine AddLine(CadDocument document, double y)
        {
            var line = new DxfLine { StartPoint = new GeoPoint3(0, y, 0), EndPoint = new GeoPoint3(100, y, 0) };
            document.ApplyEdit("draw", null, scope => scope.Append(document.ModelSpace, line));
            return line;
        }

        public static List<T> All<T>(CadDocument document)
            where T : DxfEntity => document.Database.ModelSpace.OfType<T>().ToList();

        /// <summary>The same edit as <see cref="AddLine"/>, written in Python: <c>add_line(doc, y)</c> (pythonnet needs the delegate and the generic argument named).</summary>
        public const string AddLineInPython =
            "from System import Action\n"
            + "from GeometryHelper.Cad import CadEditScope\n"
            + "from GeometryHelper.Dxf import DxfLine\n"
            + "from GeometryHelper.Geometry import GeoPoint3\n"
            + "def add_line(doc, y):\n"
            + "    line = DxfLine()\n"
            + "    line.StartPoint = GeoPoint3(0, y, 0)\n"
            + "    line.EndPoint = GeoPoint3(100, y, 0)\n"
            + "    doc.ApplyEdit('draw', None, Action[CadEditScope](lambda scope: scope.Append[DxfLine](doc.ModelSpace, line)))\n";

        #endregion

        #region Scripted answers

        /// <summary>The answer of a user who types <paramref name="text"/> at the prompt.</summary>
        public static Action<CadCommandSession> Typed(string text) =>
            session =>
            {
                if (!session.Feed(text))
                {
                    throw new InvalidOperationException("The prompt did not take '" + text + "'.");
                }
            };

        /// <summary>The answer of a user who picks the point (<paramref name="x"/>, <paramref name="y"/>) on the canvas.</summary>
        public static Action<CadCommandSession> Picked(double x, double y) =>
            session => session.Feed(new GeoPoint2(x, y));

        /// <summary>The answer of a user who selects objects (or picks the one object asked for).</summary>
        public static Action<CadCommandSession> Objects(params string[] handles) =>
            session => session.FeedSelection(handles);

        /// <summary>Enter.</summary>
        public static readonly Action<CadCommandSession> EnterKey = session => session.Enter();

        /// <summary>Esc.</summary>
        public static readonly Action<CadCommandSession> EscKey = session => session.Escape();

        #endregion

        #region Scripts

        /// <summary>The lines of a script, ended by a line end (LF, as every file of the repository).</summary>
        public static string Lines(params string[] lines) => string.Join("\n", lines) + "\n";

        /// <summary>A Python raw string literal of a path: <c>r'C:older'</c>.</summary>
        public static string Raw(string text) => "r'" + text + "'";

        /// <summary>The lines every script that uses the module begins with.</summary>
        public const string UsesCad = "import geometryhelper.cad as cad";

        /// <summary>Writes a file of text, UTF-8 without a BOM, and gives its full path.</summary>
        public static string WriteFile(string folder, string name, string text)
        {
            string path = Path.Combine(folder, name);
            File.WriteAllText(path, text, new UTF8Encoding(false));
            return path;
        }

        #endregion

        #region Fake Python installs

        /// <summary>
        /// A <c>python.exe</c> beside a <c>python3NN.dll</c> that is no Python but has the header of a PE file of the machine
        /// asked for: the finder reads the header and never loads the file (D713).
        /// </summary>
        /// <returns>The path of the <c>python.exe</c>.</returns>
        public static string FakeInstall(string folder, string library, bool x64 = true)
        {
            Directory.CreateDirectory(folder);
            string exe = Path.Combine(folder, "python.exe");
            File.WriteAllBytes(exe, new byte[] { 0x4D, 0x5A });
            File.WriteAllBytes(Path.Combine(folder, library), PeHeader(x64 ? (ushort)0x8664 : (ushort)0x014C));
            return exe;
        }

        /// <summary>
        /// A venv: <c>Scripts\python.exe</c> and a <c>pyvenv.cfg</c> above it naming <paramref name="home"/> and
        /// <paramref name="version"/>, as <c>python -m venv</c> writes them.
        /// </summary>
        /// <returns>The path of the venv's <c>python.exe</c>.</returns>
        public static string FakeVenv(string folder, string home, string version)
        {
            string scripts = Path.Combine(folder, "Scripts");
            Directory.CreateDirectory(scripts);
            string exe = Path.Combine(scripts, "python.exe");
            File.WriteAllBytes(exe, new byte[] { 0x4D, 0x5A });
            File.WriteAllText(
                Path.Combine(folder, "pyvenv.cfg"),
                Lines("home = " + home, "include-system-site-packages = false", "version = " + version),
                new UTF8Encoding(false)
            );
            return exe;
        }

        /// <summary>The first bytes of a PE file: MZ, the offset of the PE signature at 0x3C, then PE and the machine.</summary>
        private static byte[] PeHeader(ushort machine)
        {
            var bytes = new byte[0x100];
            bytes[0] = 0x4D;
            bytes[1] = 0x5A;
            bytes[0x3C] = 0x80;
            bytes[0x80] = 0x50;
            bytes[0x81] = 0x45;
            bytes[0x84] = (byte)(machine & 0xFF);
            bytes[0x85] = (byte)(machine >> 8);
            return bytes;
        }

        #endregion
    }
}
