using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// Drawing templates: the presenter's own imperial template beside the metric one, the templates of a folder found, a
    /// description read back from a .dwt, and SAVEAS Template typed (FILEDIA 0) writing a DWG with the extension .dwt, its
    /// description and measurement kept in the file.
    /// </summary>
    public class DrawingTemplateTests : IDisposable
    {
        private readonly string _folder = Path.Combine(
            Path.GetTempPath(),
            "gk",
            "drawingtemplate-" + Guid.NewGuid().ToString("N")
        );

        public DrawingTemplateTests()
        {
            Directory.CreateDirectory(_folder);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_folder, true);
            }
            catch (IOException)
            {
                // A file a watcher still holds is left for the system to clear.
            }
        }

        private static CadCommandSession NewDwg(out CadDocument document)
        {
            DxfDatabase database = CadTemplate.CreateMetric();
            database.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(1000, 0, 0)));
            document = CadDocument.From(database, "Drawing1.dwg");
            CadCommandSession session = Session(document);
            Run(session, "FILEDIA", "0");
            Assert.False(session.FileDialogs);
            return session;
        }

        private static string Magic(string path) => Encoding.ASCII.GetString(File.ReadAllBytes(path), 0, 6);

        private static string Header(DxfDatabase database, string name) =>
            string.Join(";", database.GetHeader(name).Select(g => g.TypeCode + "=" + g.Value));

        [Fact]
        public void TheImperialTemplateIsInInchesWithTheStandardDimensionStyle_EachSettingWrittenInTheHeader()
        {
            DxfDatabase database = CadTemplate.CreateImperial();

            Assert.Equal("AC1027", database.Version);
            Assert.Equal("70=0", Header(database, "$MEASUREMENT"));
            Assert.Equal("70=1", Header(database, "$INSUNITS"));
            Assert.Equal("40=0.2", Header(database, "$TEXTSIZE"));
            Assert.Equal("10=12.0;20=9.0", Header(database, "$LIMMAX"));
            Assert.Equal("2=Standard", Header(database, "$DIMSTYLE"));
            Assert.Equal(DxfMeasurementValue.English, database.Measurement);
            Assert.Equal(DxfUnits.Inches, database.Insunits);
            Assert.Equal(new GeoPoint2(12, 9), database.Limmax);
            Assert.Equal("Standard", database.Dimstyle);
            Assert.Equal(new[] { "0" }, database.LayerTable.Cast<DxfLayerTableRecord>().Select(l => l.Name));
            Assert.Empty(database.ModelSpace);
        }

        [Fact]
        public void TheImperialTemplateHoldsAutoCadsOwnStandardInEveryDimensionVariable_NotIso25()
        {
            DxfDatabase database = CadTemplate.CreateImperial();
            DxfDimStyleTableRecord standard = database.DimStyleTable["Standard"];
            DxfDimStyleTableRecord header = database.GetDimstyleData();

            Assert.Equal(0.18, standard.Dimasz);
            Assert.Equal(0.18, header.Dimasz);
            Assert.Equal(4, header.Dimdec);
            Assert.Equal('.', header.Dimdsep);

            foreach (
                PropertyInfo property in typeof(DxfDimStyleTableRecord)
                    .GetProperties()
                    .Where(p =>
                        p.Name.StartsWith("Dim", StringComparison.Ordinal) && p.GetIndexParameters().Length == 0
                    )
            )
            {
                Assert.True(
                    Text(property.GetValue(standard)) == Text(property.GetValue(header)),
                    property.Name
                        + " of Standard is "
                        + Text(property.GetValue(standard))
                        + ", the header's "
                        + Text(property.GetValue(header))
                        + "."
                );
            }

            string text = database.ToText();
            Assert.Equal(text, DxfDatabase.Parse(text).ToText());
        }

        private static string Text(object value)
        {
            switch (value)
            {
                case null:
                    return "(none)";
                case DxfSymbolTableRecord record:
                    return record.Name;
                case DxfColor color:
                    return color.ColorMethod + " " + color.ColorIndex;
                case double number:
                    return number.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
                default:
                    return value.ToString();
            }
        }

        [Fact]
        public void TheTemplatesOfAFolder_AreTheDwtDwgAndDxfFiles_ByNameThenExtension_AndNothingElse()
        {
            foreach (string name in new[] { "b.dwt", "A.DWG", "a.dwt", "c.dxf", "notes.txt", "d.dwt.bak", "a.dxf" })
            {
                File.WriteAllText(Path.Combine(_folder, name), "x");
            }

            Directory.CreateDirectory(Path.Combine(_folder, "sub"));
            File.WriteAllText(Path.Combine(_folder, "sub", "z.dwt"), "x");

            Assert.Equal(
                new[] { "A.DWG", "a.dwt", "a.dxf", "b.dwt", "c.dxf" },
                CadTemplate.FindIn(_folder).Select(Path.GetFileName)
            );
            Assert.Empty(CadTemplate.FindIn(Path.Combine(_folder, "missing")));
            Assert.Empty(CadTemplate.FindIn(null));
        }

        [Fact]
        public void ATemplateNamedWithNoExtension_IsLookedForAsDwt_AsTypedThenInTheTemplateFolder()
        {
            string file = Path.Combine(_folder, "sheet.dwt");
            File.WriteAllText(file, "x");

            Assert.Equal(file, CadTemplate.Find("sheet", _folder));
            Assert.Equal(file, CadTemplate.Find("sheet.dwt", _folder));
            Assert.Equal(file, CadTemplate.Find("\"" + file + "\"", null));
            Assert.Equal(file, CadTemplate.Find(Path.Combine(_folder, "sheet"), "elsewhere"));
            Assert.Null(CadTemplate.Find("sheet.dxf", _folder));
            Assert.Null(CadTemplate.Find("nothing", _folder));
            Assert.Null(CadTemplate.Find("  ", _folder));
        }

        [Fact]
        public void SaveAsTemplateWithFiledia0_AsksTheFile_TheDescriptionAndTheMeasurement_AndWritesADwgNamedDwt()
        {
            CadCommandSession session = NewDwg(out CadDocument document);
            string path = Path.Combine(_folder, "workshop");

            Run(session, "SAVEAS", "Template");

            Assert.Equal("Save drawing as <Drawing1.dwt>:", session.PromptText);
            Run(session, path);

            Assert.Equal("Enter template description:", session.PromptText);
            Run(session, "Workshop sheet A1, plan at 1:50");

            Assert.Equal("Enter measurement [English/Metric] <Metric>:", session.PromptText);
            Run(session, "English");

            Assert.False(session.IsRunning);
            Assert.Equal(path + ".dwt", document.FileName);
            Assert.Equal("AC1032", Magic(path + ".dwt"));
            Assert.Contains("Saved " + path + ".dwt.", session.History);
            Assert.Equal("Workshop sheet A1, plan at 1:50", CadTemplate.DescriptionOf(path + ".dwt"));

            DxfDatabase read = CadDrawingFile.Open(path + ".dwt");
            Assert.Equal(DxfMeasurementValue.English, read.Measurement);
            Assert.Single(read.ModelSpace.OfType<DxfLine>());
        }

        [Fact]
        public void SaveAsTemplate_EnterForTheDescriptionAndTheMeasurement_KeepsNoDescriptionAndTheDrawingsOwn()
        {
            CadCommandSession session = NewDwg(out CadDocument document);
            string path = Path.Combine(_folder, "plain.dwt");

            Run(session, "SAVEAS", "T", path, Enter, Enter);

            Assert.False(session.IsRunning);
            Assert.Equal(path, document.FileName);
            Assert.Equal(string.Empty, CadTemplate.DescriptionOf(path));
            Assert.Equal(DxfMeasurementValue.Metric, CadDrawingFile.Open(path).Measurement);
        }

        [Fact]
        public void ANameTypedWithAnotherExtension_IsGivenDwt_ForATemplate()
        {
            CadCommandSession session = NewDwg(out CadDocument document);
            string path = Path.Combine(_folder, "other.dwg");

            Run(session, "SAVEAS", "Template", path, Enter, "Metric");

            Assert.Equal(Path.Combine(_folder, "other.dwt"), document.FileName);
            Assert.True(File.Exists(Path.Combine(_folder, "other.dwt")));
            Assert.False(File.Exists(path));
        }

        [Fact]
        public void ADrawingSavedAsATemplate_KeepsItsDescription_WhenItIsSavedAgain()
        {
            CadCommandSession session = NewDwg(out CadDocument document);
            string path = Path.Combine(_folder, "again.dwt");
            Run(session, "SAVEAS", "Template", path, "Keep me", "Metric");

            document.Save();

            Assert.Equal("Keep me", document.TemplateDescription);
            Assert.Equal("Keep me", CadTemplate.DescriptionOf(path));
            Assert.Equal("AC1032", Magic(path));
        }

        [Fact]
        public void ATemplateWrittenAsDxf_HasNoDescription_AndAFileThatIsNoDrawingHasNone()
        {
            DxfDatabase database = CadTemplate.CreateMetric();
            string dxf = Path.Combine(_folder, "t.dxf");
            database.Save(dxf);
            string text = Path.Combine(_folder, "t.txt");
            File.WriteAllText(text, "not a drawing");

            Assert.Equal(string.Empty, CadTemplate.DescriptionOf(dxf));
            Assert.Equal(string.Empty, CadTemplate.DescriptionOf(text));
            Assert.Equal(string.Empty, CadTemplate.DescriptionOf(Path.Combine(_folder, "missing.dwt")));
        }

        [Fact]
        public void TheTemplateSettingsDefaultToNone_AreKeptInTheSettingsFile_AndFallBackToTheDefaultFolder()
        {
            var settings = new GlobalSettings();

            Assert.Equal(string.Empty, settings.TemplateFolder);
            Assert.Equal(string.Empty, settings.QnewTemplate);
            Assert.Equal(GlobalSettings.DefaultTemplateFolder, settings.EffectiveTemplateFolder);
            Assert.EndsWith("Template", GlobalSettings.DefaultTemplateFolder);

            settings.TemplateFolder = "  " + _folder + " ";
            settings.QnewTemplate = " office.dwt ";
            string file = Path.Combine(_folder, "settings.json");
            settings.Save(file);
            GlobalSettings read = GlobalSettings.Load(file);

            Assert.Equal(_folder, read.TemplateFolder);
            Assert.Equal(_folder, read.EffectiveTemplateFolder);
            Assert.Equal("office.dwt", read.QnewTemplate);
            Assert.Equal(_folder, read.Clone().TemplateFolder);
        }
    }
}
