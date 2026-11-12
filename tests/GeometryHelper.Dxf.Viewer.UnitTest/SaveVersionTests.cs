using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// A drawing saved as a DXF of another version of AutoCAD, as AutoCAD 2021's Save Drawing As does with the type chosen: the
    /// version written, kept for the next save as AutoCAD's QSAVE keeps the format of the last Save As, what it could not hold
    /// said, and a DWG written as AutoCAD 2018 only.
    /// </summary>
    public class SaveVersionTests : IDisposable
    {
        private static readonly string[] Versions = { "AC1015", "AC1018", "AC1021", "AC1024", "AC1027", "AC1032" };

        private readonly string _folder = Path.Combine(
            Path.GetTempPath(),
            "gk",
            "saveversion-" + Guid.NewGuid().ToString("N")
        );

        public SaveVersionTests()
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

        /// <summary>A metric drawing of a line from (0, 0) to (1 000, 0) and a circle of radius 100 about the origin, named Plan.dxf.</summary>
        private static CadDocument Plan()
        {
            DxfDatabase database = CadTemplate.CreateMetric();
            database.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(1000, 0, 0)));
            database.ModelSpace.AppendEntity(new DxfCircle(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), 100.0));
            return CadDocument.From(database, "Plan.dxf");
        }

        [Fact]
        public void ADrawingSavedAsEachVersion_IsWrittenInIt_AndKeepsItsEntities()
        {
            foreach (string version in Versions)
            {
                CadDocument document = Plan();
                string path = Path.Combine(_folder, "plan-" + version + ".dxf");

                document.SaveAs(path, version, null);

                DxfDatabase saved = DxfDatabase.Open(path);
                Assert.Equal(version, saved.Version);
                Assert.Single(saved.ModelSpace.OfType<DxfLine>());
                Assert.Equal(100.0, Assert.Single(saved.ModelSpace.OfType<DxfCircle>()).Radius, 9);
                Assert.Equal(path, document.FileName);
                Assert.Equal(version, document.SaveVersion);
                Assert.False(document.IsModified);
            }
        }

        [Fact]
        public void ADrawingSavedAsAVersion_IsSavedAsItAgain_UntilItIsSavedAsAnother()
        {
            CadDocument document = Plan();
            string path = Path.Combine(_folder, "plan.dxf");

            document.SaveAs(path, "AC1024", null);
            document.ModelSpace.Block.AppendEntity(new DxfLine(new GeoPoint3(0, 5, 0), new GeoPoint3(10, 5, 0)));
            document.Save();

            Assert.Equal("AC1024", DxfDatabase.Open(path).Version);
            Assert.Equal(2, DxfDatabase.Open(path).ModelSpace.OfType<DxfLine>().Count());

            document.SaveAs(path, "AC1018", null);
            document.Save();
            Assert.Equal("AC1018", DxfDatabase.Open(path).Version);

            // SaveAs with a path alone keeps the version too, as the Save As command's file does.
            document.SaveAs(Path.Combine(_folder, "copy.dxf"));
            Assert.Equal("AC1018", DxfDatabase.Open(Path.Combine(_folder, "copy.dxf")).Version);
        }

        [Fact]
        public void ADrawingSavedAsNoVersion_IsSavedInItsOwnAndForgetsTheLastOne()
        {
            CadDocument document = Plan();
            string own = document.Database.Version;
            string path = Path.Combine(_folder, "plan.dxf");

            document.SaveAs(path, "AC1015", null);
            Assert.Equal("AC1015", document.SaveVersion);

            document.SaveAs(path, null, null);

            Assert.Null(document.SaveVersion);
            Assert.Equal(own, DxfDatabase.Open(path).Version);
            Assert.Empty(document.LeftOutOfDxf);
        }

        [Fact]
        public void ADrawingSavedInItsOwnVersion_IsTheFileSaveAsWritesWithNoVersion()
        {
            CadDocument document = Plan();
            string plain = Path.Combine(_folder, "plain.dxf");
            string named = Path.Combine(_folder, "named.dxf");

            document.SaveAs(plain);
            document.SaveAs(named, document.Database.Version, null);

            Assert.Equal(File.ReadAllBytes(plain), File.ReadAllBytes(named));
        }

        [Fact]
        public void AVersionForADwgName_IsRefusedAndTheFileIsNotTouched()
        {
            CadDocument document = Plan();
            string path = Path.Combine(_folder, "plan.dwg");
            File.WriteAllText(path, "kept");

            Assert.Throws<NotSupportedException>(() => document.SaveAs(path, "AC1024", null));

            Assert.Equal("kept", File.ReadAllText(path));
            Assert.Null(document.SaveVersion);

            // The version of the DWG itself, AutoCAD 2018, is no other.
            document.SaveAs(path, "AC1032", null);
            Assert.Equal("AC1032", CadDrawingFile.Open(path).Version);
        }

        [Fact]
        public void AVersionThatIsNoneOfTheSix_IsRefused()
        {
            CadDocument document = Plan();

            Assert.Throws<ArgumentException>(() => document.SaveAs(Path.Combine(_folder, "plan.dxf"), "AC1014", null));
            Assert.Throws<ArgumentException>(() => document.SaveAs(Path.Combine(_folder, "plan.dxf"), "R2000", null));
            Assert.False(File.Exists(Path.Combine(_folder, "plan.dxf")));
        }

        [Fact]
        public void WhatTheVersionCouldNotHold_IsKeptInLeftOutOfDxf_UntilTheNextSave()
        {
            string text = CadTemplate.CreateMetric().ToText();
            int end = text.LastIndexOf("ENDSEC", StringComparison.Ordinal);
            int line = text.LastIndexOf("  0", end, StringComparison.Ordinal);
            string eol = text.Contains("\r\n") ? "\r\n" : "\n";
            string style = string.Join(
                eol,
                "  0",
                "VISUALSTYLE",
                "  5",
                "FFF1",
                "330",
                "0",
                "100",
                "AcDbVisualStyle",
                "  2",
                "custom",
                string.Empty
            );
            CadDocument document = CadDocument.From(DxfDatabase.Parse(text.Insert(line, style)), "Styled.dxf");
            string path = Path.Combine(_folder, "styled.dxf");
            string own = document.Database.Version;

            document.SaveAs(path, own == "AC1015" ? "AC1018" : "AC1015", null);

            Assert.Contains(document.LeftOutOfDxf, s => s.Contains("VISUALSTYLE 1"));

            document.SaveAs(path, own, null);

            Assert.Empty(document.LeftOutOfDxf);
        }
    }
}
