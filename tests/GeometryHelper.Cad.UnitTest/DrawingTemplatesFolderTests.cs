using System;
using System.IO;
using Xunit;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The DrawingTemplates folder beside the program: it ships with a README.txt, and it is where NEW looks for templates
    /// where Global settings name no template folder. The folder of old, a Template folder beside the settings file, is kept
    /// only where it already holds templates and DrawingTemplates holds none. The program folder and the per-user folder
    /// are made under the temporary folder and removed after.
    /// </summary>
    public sealed class DrawingTemplatesFolderTests : IDisposable
    {
        private readonly string _root = Path.Combine(
            Path.GetTempPath(),
            "gh-drawing-templates-" + Guid.NewGuid().ToString("N")
        );

        public DrawingTemplatesFolderTests()
        {
            Directory.CreateDirectory(_root);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_root, true);
            }
            catch (IOException) { }
        }

        private string Program => Path.Combine(_root, "app");

        private string User => Path.Combine(_root, "user", "Template");

        private static void Touch(string folder, string name)
        {
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, name), "x");
        }

        private string Resolve() => GlobalSettings.ResolveDefaultTemplateFolder(new CadSupportFolders(Program), User);

        [Fact]
        public void TheDrawingTemplatesFolder_IsTheFifthDefaultFolderBesideTheProgram()
        {
            var roots = new CadSupportFolders(Program);

            Assert.Equal(Path.Combine(Program, "DrawingTemplates"), roots.FolderOf(CadSupportKind.DrawingTemplates));
            Assert.Empty(roots.ExistingOf(CadSupportKind.DrawingTemplates));
            Directory.CreateDirectory(Path.Combine(Program, "DrawingTemplates"));
            Assert.Equal(
                new[] { Path.Combine(Program, "DrawingTemplates") },
                roots.ExistingOf(CadSupportKind.DrawingTemplates)
            );
        }

        [Fact]
        public void TheShippedFolder_IsBesideTheDll_WithItsReadme_AndNoTemplate()
        {
            CadSupportFolders folders = CadSupportFolders.Default;
            string folder = folders.FolderOf(CadSupportKind.DrawingTemplates);

            Assert.Single(folders.ExistingOf(CadSupportKind.DrawingTemplates));
            string readme = File.ReadAllText(Path.Combine(folder, "README.txt"));
            Assert.Contains(".dwt", readme);
            Assert.Contains("SAVEAS", readme);
            Assert.Contains("Drawing Template", readme);
            Assert.Empty(CadTemplate.FindIn(folder));
        }

        [Fact]
        public void WhereNoFolderIsNamed_TheTemplatesAreLookedForInDrawingTemplates()
        {
            Assert.Equal(Path.Combine(Program, "DrawingTemplates"), Resolve());

            Touch(Path.Combine(Program, "DrawingTemplates"), "office.dwt");
            Assert.Equal(Path.Combine(Program, "DrawingTemplates"), Resolve());
        }

        [Fact]
        public void TheTemplateFolderOfGlobalSettings_StillWinsOverTheDefault()
        {
            Touch(Path.Combine(Program, "DrawingTemplates"), "office.dwt");
            var settings = new GlobalSettings { TemplateFolder = Path.Combine(_root, "mine") };

            Assert.Equal(Path.Combine(_root, "mine"), settings.EffectiveTemplateFolder);
        }

        [Fact]
        public void WithNoSettingNamed_TheEffectiveFolderIsTheDefaultOne()
        {
            var settings = new GlobalSettings();

            Assert.Equal(GlobalSettings.DefaultTemplateFolder, settings.EffectiveTemplateFolder);
            Assert.Equal(string.Empty, settings.TemplateFolder);
        }

        [Fact]
        public void TheFolderBesideTheSettingsFile_IsTheFallback_OnlyWhereItHoldsTemplatesAndDrawingTemplatesNone()
        {
            Touch(User, "old.dwt");
            Assert.Equal(User, Resolve());

            // A .dxf or .dwg counts as a template, as Select Template lists it.
            Directory.Delete(Path.GetDirectoryName(User), true);
            Touch(User, "old.dxf");
            Assert.Equal(User, Resolve());

            // One template in DrawingTemplates, and the old folder is no longer looked at.
            Touch(Path.Combine(Program, "DrawingTemplates"), "new.dwt");
            Assert.Equal(Path.Combine(Program, "DrawingTemplates"), Resolve());
        }

        [Fact]
        public void AnOldFolderHoldingNoTemplate_IsNotTheFallback()
        {
            Touch(User, "notes.txt");

            Assert.Equal(Path.Combine(Program, "DrawingTemplates"), Resolve());
        }

        [Fact]
        public void WithNoProgramFolder_TheOldFolderIsTheDefault()
        {
            Assert.Equal(User, GlobalSettings.ResolveDefaultTemplateFolder(new CadSupportFolders(null), User));
        }
    }
}
