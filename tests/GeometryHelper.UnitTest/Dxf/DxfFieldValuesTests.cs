using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The fields of a text as AutoCAD 2021 writes them once evaluated: the field of an object's property (its object by handle,
    /// the property by name, a number, a point), a date, and the checksum of the words. The groups are those AutoCAD 2021's own
    /// FIELDs had in the drawings it saved for the same texts; a DWG of R2004, R2007 and R2018 keeps each of them.
    /// </summary>
    public class DxfFieldValuesTests
    {
        private static readonly DateTime Moment = new DateTime(2026, 11, 17, 3, 21, 5);

        private static DxfFieldDraft Child(string evaluator, string code, string shown, DxfFieldValue value)
        {
            var child = new DxfFieldDraft
            {
                Evaluator = evaluator,
                Code = code,
                State = 59,
                Status = 2,
                Value = value,
                Shown = shown,
                Format = DxfField.FormatOf(code),
            };
            value.Flags = 4;
            value.Format = child.Format;
            value.Shown = shown;
            return child;
        }

        /// <summary>A TEXT in a drawing of a version, holding the words "Area 314.16 at 100.0, 100.0, 0.0 on 17/11/2026" as three fields.</summary>
        private static DxfDatabase Fielded(string version, out DxfDBText text, out DxfCircle circle)
        {
            var db = new DxfDatabase(version);
            circle = new DxfCircle { Center = new GeoPoint3(100, 100, 0), Radius = 10 };
            db.ModelSpace.AppendEntity(circle);
            text = new DxfDBText
            {
                TextString = "x",
                Position = new GeoPoint3(0, 0, 0),
                Height = 2.5,
            };
            db.ModelSpace.AppendEntity(text);

            DxfFieldDraft area = Child(
                "AcObjProp",
                "\\AcObjProp Object(%<\\_ObjIdx 0>%).Area \\f \"%lu2%pr2\"",
                "314.16",
                new DxfFieldValue { Type = 2, Double = Math.PI * 100.0 }
            );
            area.Objects.Add(circle.Handle);
            area.Data.Add(
                new KeyValuePair<string, DxfFieldValue>("ObjectPropertyId", DxfFieldValue.Object(circle.Handle))
            );
            area.Data.Add(new KeyValuePair<string, DxfFieldValue>("ObjectPropertyName", DxfFieldValue.Named("Area")));
            DxfFieldDraft center = Child(
                "AcObjProp",
                "\\AcObjProp Object(%<\\_ObjIdx 0>%).Center",
                "100.000000, 100.000000, 0.000000",
                new DxfFieldValue
                {
                    Type = 32,
                    X = 100,
                    Y = 100,
                    Z = 0,
                }
            );
            center.Objects.Add(circle.Handle);
            center.Data.Add(
                new KeyValuePair<string, DxfFieldValue>("ObjectPropertyId", DxfFieldValue.Object(circle.Handle))
            );
            center.Data.Add(
                new KeyValuePair<string, DxfFieldValue>("ObjectPropertyName", DxfFieldValue.Named("Center"))
            );
            DxfFieldDraft date = Child(
                "AcVar",
                "\\AcVar Date \\f \"dd/MM/yyyy\"",
                "17/11/2026",
                new DxfFieldValue { Type = 8, Date = Moment }
            );
            date.Data.Add(new KeyValuePair<string, DxfFieldValue>("Variable", DxfFieldValue.Named("Date")));
            date.Option = DxfFieldDraft.OnDemand;

            string words = "Area 314.16 at 100.000000, 100.000000, 0.000000 on 17/11/2026";
            var root = new DxfFieldDraft
            {
                Evaluator = "_text",
                Code = "Area %<\\_FldIdx 0>% at %<\\_FldIdx 1>% on %<\\_FldIdx 2>%",
                State = 9,
                Status = 2,
                Value = new DxfFieldValue { Flags = 3 },
            };
            root.Children.Add(area);
            root.Children.Add(center);
            root.Children.Add(date);
            root.Data.Add(
                new KeyValuePair<string, DxfFieldValue>(
                    DxfField.ChecksumKey,
                    DxfFieldValue.Checksum(DxfField.Checksum(words))
                )
            );
            text.SetField("TEXT", root);
            text.TextString = words;
            return db;
        }

        /// <summary>The records of the FIELDs of a text, each as its groups with a real read as one, so that 314.1592653589793 and 314.15926535897933 are the same.</summary>
        private static List<string[]> Fields(DxfDatabase db) =>
            Records(db.ToText())
                .Where(r => r.Split('\n')[0].Trim() == "0" && r.Split('\n')[1] == "FIELD")
                .Select(r =>
                {
                    string[] lines = r.Split('\n');
                    var groups = new List<string>();

                    for (int i = 0; i + 1 < lines.Length; i += 2)
                    {
                        string value = lines[i + 1];
                        groups.Add(
                            lines[i].Trim()
                                + "="
                                + (
                                    value.IndexOf('.') >= 0
                                    && double.TryParse(
                                        value,
                                        NumberStyles.Float,
                                        CultureInfo.InvariantCulture,
                                        out double real
                                    )
                                        ? real.ToString("R", CultureInfo.InvariantCulture)
                                        : value
                                )
                        );
                    }

                    return groups.ToArray();
                })
                .ToList();

        [Fact]
        public void TheChecksumOfWords_IsEachLettersCodeTimesItsPlaceFromOne_AsAutoCADKeepsIt()
        {
            Assert.Equal(7540.0, DxfField.Checksum("Drawn 17/11/2026"));
            Assert.Equal(2747.0, DxfField.Checksum("17/11/2026"));
            Assert.Equal(957.0, DxfField.Checksum("S ----"));
            Assert.Equal(94113.0, DxfField.Checksum("{\\fArial|b1;Bold} Jane Author\\Pline2 My Title"));
            Assert.Equal(0.0, DxfField.Checksum(null));
        }

        [Fact]
        public void AFieldEvaluated_IsWrittenInTheGroupsAutoCADWritesForItsValues()
        {
            DxfDatabase db = Fielded("AC1032", out DxfDBText text, out DxfCircle circle);
            string saved = db.ToText();
            string handle = circle.Handle;

            // The field of the area: its object in 331 and in the value ObjectPropertyId (90 of 64, 330), the property by name, the number.
            Assert.Contains(
                " 97\n1\n331\n"
                    + handle
                    + "\n 91\n63\n 92\n0\n 94\n59\n 95\n2\n 96\n0\n300\n\n 93\n2\n  6\nObjectPropertyId\n 93\n2\n 90\n64\n330\n"
                    + handle
                    + "\n 94\n0\n300\n\n302\n\n304\nACVALUE_END\n  6\nObjectPropertyName\n 93\n2\n 90\n4\n  1\nArea\n 94\n0\n300\n\n302\n\n304\nACVALUE_END\n  7\nACFD_FIELD_VALUE\n 93\n4\n 90\n2\n140\n314.1592653589793\n 94\n0\n300\n%lu2%pr2\n302\n314.16\n304\nACVALUE_END\n301\n314.16\n 98\n6\n",
                saved,
                StringComparison.Ordinal
            );
            // The point: 11, 21 and 31 of a value of 32.
            Assert.Contains(
                " 90\n32\n 11\n100.0\n 21\n100.0\n 31\n0.0\n 94\n0\n300\n\n302\n100.000000, 100.000000, 0.000000\n",
                saved,
                StringComparison.Ordinal
            );
            // The date of R2007 and later: the 16 bytes of a SYSTEMTIME, the date evaluated on demand (91 of 32).
            Assert.Contains(
                " 91\n32\n 92\n0\n 94\n59\n 95\n2\n 96\n0\n300\n\n 93\n1\n  6\nVariable\n 93\n2\n 90\n4\n  1\nDate\n 94\n0\n300\n\n302\n\n304\nACVALUE_END\n  7\nACFD_FIELD_VALUE\n 93\n4\n 90\n8\n 92\n16\n310\nEA070B00020011000300150005000000\n",
                saved,
                StringComparison.Ordinal
            );
            Assert.Contains(
                "140\n" + DxfField.Checksum(text.TextString).ToString("0.0", CultureInfo.InvariantCulture) + "\n",
                saved,
                StringComparison.Ordinal
            );
        }

        [Fact]
        public void ADateOfR2004_IsTheSecondsSince1970InUniversalTime_AndHasNoFlagsOrUnit()
        {
            DxfDatabase db = Fielded("AC1018", out _, out _);
            string saved = db.ToText();
            double seconds = (
                TimeZoneInfo.ConvertTimeToUtc(Moment, TimeZoneInfo.Local) - new DateTime(1970, 1, 1)
            ).TotalSeconds;
            byte[] bytes = BitConverter.GetBytes((uint)seconds);
            string hex = string.Concat(bytes.Select(b => b.ToString("X2", CultureInfo.InvariantCulture))) + "00000000";

            Assert.Contains(
                "  6\nVariable\n 90\n4\n  1\nDate\n  7\nACFD_FIELD_VALUE\n 90\n8\n 92\n8\n310\n"
                    + hex
                    + "\n301\n17/11/2026\n 98\n10\n",
                saved,
                StringComparison.Ordinal
            );
            Assert.Contains("  4\ndd/MM/yyyy\n", saved, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("AC1032")]
        [InlineData("AC1021")]
        [InlineData("AC1018")]
        public void TheFieldsOfAnObjectsPropertyKeepTheirObjectAndValuesInADwgOfTheSameVersion(string version)
        {
            DxfDatabase db = Fielded(version, out _, out _);
            List<string[]> before = Fields(db);
            Assert.Equal(4, before.Count);

            using (var stream = new MemoryStream())
            {
                DwgWriter.Write(db, stream, version);
                stream.Position = 0;
                DxfDatabase again = DwgReader.Open(stream);
                List<string[]> after = Fields(again);

                Assert.Equal(before.Count, after.Count);

                for (int i = 0; i < before.Count; i++)
                {
                    Assert.Equal(before[i], after[i]);
                }

                Assert.Equal(
                    db.ModelSpace.OfType<DxfDBText>().First().TextString,
                    again.ModelSpace.OfType<DxfDBText>().First().TextString
                );
            }
        }

        [Fact]
        public void AFieldGivenToATextInADrawingWithNoListOfFields_MakesTheListAsAutoCADDoes_AndListsEveryFieldInIt()
        {
            DxfDatabase db = Fielded("AC1032", out DxfDBText text, out _);
            DxfDBObject list = db.NamedObjectsDictionary.GetAt("ACAD_FIELDLIST");
            DxfField field = text.GetField();
            string record = Records(db.ToText()).Single(r => HandleOf(r) == list.Handle);

            Assert.Equal("FIELDLIST", list.Item.Type);
            Assert.Contains("100\nAcDbIdSet\n 90\n4\n330\n" + field.Handle + "\n", record, StringComparison.Ordinal);
            Assert.EndsWith("100\nAcDbFieldList\n", record, StringComparison.Ordinal);
            Assert.Contains("  1\nFIELDLIST\n  2\nAcDbFieldList\n", db.ToText(), StringComparison.Ordinal);
        }
    }
}
