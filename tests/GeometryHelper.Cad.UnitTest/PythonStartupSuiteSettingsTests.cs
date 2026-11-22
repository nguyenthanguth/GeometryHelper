using System;
using System.IO;
using Xunit;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// <see cref="GlobalSettings.PythonStartupSuite"/> (D1616): the .py files loaded at every start, kept in the settings
    /// file under <c>support</c> apart from <see cref="GlobalSettings.LispStartupSuite"/>, copied and read as that is; and
    /// the <c>Pythons</c> support folder beside the dll (D1606).
    /// </summary>
    public sealed class PythonStartupSuiteSettingsTests : IDisposable
    {
        private readonly string _folder = Path.Combine(
            Path.GetTempPath(),
            "gh-pysuite-" + Guid.NewGuid().ToString("N")
        );

        public PythonStartupSuiteSettingsTests()
        {
            Directory.CreateDirectory(_folder);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_folder, true);
            }
            catch (IOException) { }
        }

        private string SettingsFile => Path.Combine(_folder, "settings.json");

        [Fact]
        public void ThePythonStartupSuite_IsEmptyByDefault()
        {
            Assert.Empty(new GlobalSettings().PythonStartupSuite);
        }

        [Fact]
        public void ThePythonStartupSuite_KeepsItsOrderThroughTheFile_BlanksLeftOut_AndAFileGivenTwiceKeptOnce()
        {
            // Saved as b, blank, blank, a, then b again in other letters: read back as b, a, as the Lisp suite is.
            new GlobalSettings
            {
                PythonStartupSuite = new[] { @" D:\Py\b.py ", "", "  ", @"D:\Py\a.py", @"d:\py\B.PY" },
            }.Save(SettingsFile);

            GlobalSettings read = GlobalSettings.Load(SettingsFile);

            Assert.Equal(new[] { @"D:\Py\b.py", @"D:\Py\a.py" }, read.PythonStartupSuite);
        }

        [Fact]
        public void ThePythonStartupSuite_IsWrittenUnderSupport_AsPythonStartupSuite()
        {
            new GlobalSettings { PythonStartupSuite = new[] { @"D:\Py\a.py" } }.Save(SettingsFile);

            string text = File.ReadAllText(SettingsFile);

            Assert.Contains("\"support\"", text);
            Assert.Contains(
                "\"pythonStartupSuite\"",
                text.Substring(text.IndexOf("\"support\"", StringComparison.Ordinal))
            );
        }

        [Fact]
        public void ThePythonAndTheLispSuites_AreKeptApart_ThroughTheFileAndByClone()
        {
            var settings = new GlobalSettings
            {
                LispStartupSuite = new[] { @"D:\Lisp\a.lsp" },
                PythonStartupSuite = new[] { @"D:\Py\a.py" },
            };
            settings.Save(SettingsFile);

            GlobalSettings read = GlobalSettings.Load(SettingsFile);
            GlobalSettings copy = settings.Clone();

            Assert.Equal(new[] { @"D:\Lisp\a.lsp" }, read.LispStartupSuite);
            Assert.Equal(new[] { @"D:\Py\a.py" }, read.PythonStartupSuite);
            Assert.Equal(new[] { @"D:\Lisp\a.lsp" }, copy.LispStartupSuite);
            Assert.Equal(new[] { @"D:\Py\a.py" }, copy.PythonStartupSuite);
        }

        [Fact]
        public void ACopyOfTheSettings_HasAPythonSuiteOfItsOwn_AndNullIsEmpty()
        {
            var settings = new GlobalSettings { PythonStartupSuite = new[] { "a.py" } };
            GlobalSettings copy = settings.Clone();

            copy.PythonStartupSuite = new[] { "b.py" };

            Assert.Equal(new[] { "a.py" }, settings.PythonStartupSuite);
            Assert.Equal(new[] { "b.py" }, copy.PythonStartupSuite);

            settings.PythonStartupSuite = null;
            Assert.Empty(settings.PythonStartupSuite);
        }

        [Fact]
        public void AnOldSettingsFileWithNoPythonSuite_ReadsAnEmptyOne_AndKeepsTheLispSuite()
        {
            // A file written before the setting existed: the support block has the Lisp suite and no pythonStartupSuite.
            File.WriteAllText(
                SettingsFile,
                "{\n  \"support\": {\n    \"lispStartupSuite\": [ \"D:\\\\Lisp\\\\a.lsp\" ]\n  }\n}\n"
            );

            GlobalSettings read = GlobalSettings.Load(SettingsFile);

            Assert.Empty(read.PythonStartupSuite);
            Assert.Equal(new[] { @"D:\Lisp\a.lsp" }, read.LispStartupSuite);
        }

        [Fact]
        public void ThePythonsFolder_IsBesideTheDllRoot_AsLispsIs()
        {
            var roots = new CadSupportFolders(@"D:\App");

            Assert.Equal(@"D:\App\Pythons", roots.FolderOf(CadSupportKind.Pythons));
            Assert.Equal(@"D:\App\Lisps", roots.FolderOf(CadSupportKind.Lisps));
        }

        [Fact]
        public void ThePythonsFolder_IsListedWhereItExists_AndPassedOverWhereItDoesNot()
        {
            string program = Path.Combine(_folder, "program");
            Directory.CreateDirectory(program);
            var roots = new CadSupportFolders(program);
            Assert.Empty(roots.ExistingOf(CadSupportKind.Pythons));

            string pythons = Directory.CreateDirectory(Path.Combine(program, "Pythons")).FullName;

            Assert.Equal(new[] { pythons }, roots.ExistingOf(CadSupportKind.Pythons));
        }
    }
}
