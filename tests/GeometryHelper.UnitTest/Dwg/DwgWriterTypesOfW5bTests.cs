using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dwg
{
    /// <summary>
    /// The types the DWG writer of R2018 took on in W5b: MULTILEADER and MLEADERSTYLE, ACAD_TABLE with its TABLESTYLE and
    /// the round-trip TABLECONTENT and TABLEGEOMETRY, attributes of several lines, and RASTERVARIABLES and
    /// WIPEOUTVARIABLES. Each drawing is AutoCAD's own DXF, written as a DWG and read back by the reader to the same
    /// records; and the reader reads AutoCAD's own DWG of a table of R2018 to the DXF AutoCAD wrote of it.
    /// </summary>
    public class DwgWriterTypesOfW5bTests
    {
        /// <summary>The types of AutoCAD's new drawing of R2018 that the writer still leaves out: none since the view styles.</summary>
        private static readonly string[] StillLeftOut = { };

        private static byte[] Written(DxfDatabase database)
        {
            using (var stream = new MemoryStream())
            {
                DwgWriter.Write(database, stream);
                return stream.ToArray();
            }
        }

        private static DxfDatabase Source(string name) =>
            DxfDatabase.Open(new MemoryStream(DwgAnswerKey.Key(name), false));

        /// <summary>The comparison of a drawing written as DWG and read back with the DXF it was written from.</summary>
        private static DwgAnswerKey.Result RoundTrip(string name) =>
            DwgAnswerKey.Compare(DwgAnswerKey.Key(name), DwgReader.ToDxf(Written(Source(name))));

        /// <summary>
        /// What may differ: what the types still left out lead to, the dictionaries that list them and the material the
        /// header names.
        /// </summary>
        private static void OnlyWhatTheTypesLeftOutLeadTo(DwgAnswerKey.Result result, params string[] alsoMissing)
        {
            Assert.All(result.Differences, difference =>
                Assert.True(difference.Contains("$CMATERIAL") || difference.Contains("OBJECTS DICTIONARY"), difference)
            );
            Assert.All(result.Missing.Keys, type => Assert.Contains(type, StillLeftOut.Concat(alsoMissing)));
        }

        [Fact]
        public void MultiLeadersAndTheirStyles_WrittenAsDwg_ReadBackToTheSameRecords()
        {
            // Three multileaders, of text, of a spline leader and of a block, and two multileader styles.
            DwgAnswerKey.Result result = RoundTrip("leaders_AC1032");

            OnlyWhatTheTypesLeftOutLeadTo(result);
            Assert.True(result.Paired > 350, result.Paired + " records paired");
        }

        [Fact]
        public void AttributesOfSeveralLines_WrittenAsDwg_ReadBackWithTheirText()
        {
            // An ATTDEF of "first\Psecond" and an ATTRIB of "line one\Pline two", each with its MTEXT, and their SEQEND.
            DwgAnswerKey.Result result = RoundTrip("meshes_AC1032");

            OnlyWhatTheTypesLeftOutLeadTo(result);
            DxfDatabase back = DwgReader.Open(new MemoryStream(Written(Source("meshes_AC1032")), false));
            string text = back.ToText();
            Assert.Contains("line one\\Pline two", text);
            Assert.Contains("first\\Psecond", text);
        }

        [Fact]
        public void TablesAndTheirStyles_WrittenAsDwg_ReadBackToTheSameCellsAndStyles()
        {
            // A table of 3 by 3 of the Standard style, its title merged across, and one of 2 by 4 of a style of its own,
            // with a merged range in each row; the second keeps its content for a round trip, read back with it.
            DwgAnswerKey.Result result = RoundTrip("table_AC1032");

            OnlyWhatTheTypesLeftOutLeadTo(result);
            Assert.True(result.Paired > 380, result.Paired + " records paired");
        }

        [Fact]
        public void ATableOfR2018_ReadFromAutoCadsDwg_IsTheDxfAutoCadWroteOfIt()
        {
            DwgAnswerKey.Result result = DwgAnswerKey.Compare(
                DwgAnswerKey.Key("table_AC1032"),
                DwgReader.ToDxf(DwgAnswerKey.Dwg("table_AC1032"))
            );

            Assert.Empty(result.Differences);
            Assert.All(result.Missing.Keys, type =>
                Assert.Contains(type, StillLeftOut.Concat(new[] { "CLASS CELLSTYLEMAP" }))
            );
            Assert.True(result.Paired > 380, result.Paired + " records paired");
        }

        [Fact]
        public void TheVariablesOfImagesAndWipeouts_AreWrittenAndReadBack()
        {
            DxfDatabase database = Source("curves_AC1032");
            DxfDBDictionary images = DxfRasterImageDef.CreateImageDictionary(database);
            var definition = new DxfRasterImageDef(@"C:\pictures\px43.png", new GeoVector2(4, 3));
            images.SetAt("px43", definition);
            var image = new DxfRasterImage(definition);
            image.SetOrientation(new GeoPoint3(10, 20, 0), new GeoVector3(40, 0, 0), new GeoVector3(0, 30, 0));
            database.ModelSpace.AppendEntity(image);
            var wipeout = new DxfWipeout();
            wipeout.SetFrom(
                new[]
                {
                    new GeoPoint2(-30, -10),
                    new GeoPoint2(10, -10),
                    new GeoPoint2(10, 20),
                    new GeoPoint2(-30, -10),
                },
                new GeoVector3(0, 0, 1)
            );
            database.ModelSpace.AppendEntity(wipeout);

            DwgAnswerKey.Result result = DwgAnswerKey.Compare(
                System.Text.Encoding.UTF8.GetBytes(database.ToText()),
                DwgReader.ToDxf(Written(database))
            );

            OnlyWhatTheTypesLeftOutLeadTo(result);
            string back = DwgReader.Open(new MemoryStream(Written(database), false)).ToText();

            // Frames shown, pictures finely, no unit; the frames of wipeouts shown.
            Assert.Contains("RASTERVARIABLES", back);
            Assert.Contains("WIPEOUTVARIABLES", back);
        }

        [Fact]
        public void TheLeftOutLine_NamesNoneOfTheTypesOfW5b()
        {
            var lines = new List<string>();
            bool enabled = GeometryHelperLog.Enable;
            Action<GeometryHelperLogLevel, string, Exception> writer = GeometryHelperLog.Writer;

            try
            {
                GeometryHelperLog.Enable = true;
                GeometryHelperLog.Writer = (level, message, fault) =>
                {
                    if (
                        level == GeometryHelperLogLevel.Warn
                        && message.StartsWith("DwgWriter", StringComparison.Ordinal)
                    )
                    {
                        lines.Add(message);
                    }
                };

                Written(Source("leaders_AC1032"));
                Written(Source("table_AC1032"));
                Written(Source("meshes_AC1032"));
            }
            finally
            {
                GeometryHelperLog.Writer = writer;
                GeometryHelperLog.Enable = enabled;
            }

            // Since the view styles are written, these drawings leave nothing out and give no such line.
            Assert.Empty(lines);

            foreach (string type in new[] { "MULTILEADER", "MLEADERSTYLE", "TABLESTYLE", "ACAD_TABLE", "CELLSTYLEMAP" })
            {
                Assert.All(lines, line => Assert.DoesNotContain(" " + type + " ", line));
            }

            Assert.All(lines, line => Assert.DoesNotContain("ATTRIB", line));
            Assert.All(lines, line => Assert.DoesNotContain("TABLECONTENT", line));
        }

        [Fact]
        public void ADrawingWithTablesAndMultiLeaders_IsWrittenInTheSameBytes()
        {
            Assert.Equal(Written(Source("table_AC1032")), Written(Source("table_AC1032")));
            Assert.Equal(Written(Source("leaders_AC1032")), Written(Source("leaders_AC1032")));
        }
    }
}
