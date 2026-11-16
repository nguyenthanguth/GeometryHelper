using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// An annotative TEXT or MTEXT mirrored turns each of its contexts over as it does its record, as AutoCAD 2021's
    /// <c>TransformBy</c> does (probed in its console on 24 annotative texts and 18 annotative MTEXTs of 3 planes at 1:1 and 1:2,
    /// under 10 transformations: every group of the records and, but for the texts' fonts, of the contexts). The words of a TEXT
    /// are turned over about the middle of their ink at the height of each context's scale.
    /// </summary>
    public class DxfAnnotativeMirrorTests
    {
        /// <summary>A capital as txt.shx draws one: the pen down from the origin up 6 and along 4, lifted, down 6 and along 2.</summary>
        private static readonly byte[] Capital = { 1, 0x64, 0x40, 2, 0x6C, 0x20, 0 };

        private static byte[] Unifont()
        {
            var letters = new SortedDictionary<int, byte[]> { { ' ', new byte[] { 2, 0x60, 0 } } };

            for (char c = 'A'; c <= 'Z'; c++)
            {
                letters.Add(c, Capital);
            }

            var bytes = new List<byte>(Encoding.ASCII.GetBytes("AutoCAD-86 unifont 1.0\r\n\u001A"));
            byte[] header = Encoding.ASCII.GetBytes("GH TEST").Concat(new byte[] { 0, 6, 2, 2, 0, 0, 0 }).ToArray();
            bytes.AddRange(BitConverter.GetBytes((uint)(letters.Count + 1)));
            bytes.AddRange(BitConverter.GetBytes((ushort)header.Length));
            bytes.AddRange(header);

            foreach (KeyValuePair<int, byte[]> letter in letters)
            {
                byte[] record = new byte[] { 0 }
                    .Concat(letter.Value)
                    .ToArray();
                bytes.AddRange(BitConverter.GetBytes((ushort)letter.Key));
                bytes.AddRange(BitConverter.GetBytes((ushort)record.Length));
                bytes.AddRange(record);
            }

            return bytes.ToArray();
        }

        private static void InFolder(Action<string> test)
        {
            string folder = Path.Combine(Path.GetTempPath(), "gh-fonts-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);

            try
            {
                File.WriteAllBytes(Path.Combine(folder, "txt.shx"), Unifont());
                test(folder);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        /// <summary>A drawing that holds the scales 1:1 and 1:2.</summary>
        private static DxfDatabase Scaled(string fonts)
        {
            var db = new DxfDatabase("AC1027");

            if (fonts != null)
            {
                db.FontSearchPath = fonts;
            }

            DxfObjectContextCollection scales = db.ObjectContextManager.GetContextCollection("ACDB_ANNOTATIONSCALES");
            scales.AddContext(
                new DxfAnnotationScale
                {
                    Name = "1:1",
                    PaperUnits = 1,
                    DrawingUnits = 1,
                }
            );
            scales.AddContext(
                new DxfAnnotationScale
                {
                    Name = "1:2",
                    PaperUnits = 1,
                    DrawingUnits = 2,
                }
            );
            return db;
        }

        private static DxfAnnotationScale Of(DxfDatabase db, string name) =>
            (DxfAnnotationScale)db.ObjectContextManager.GetContextCollection("ACDB_ANNOTATIONSCALES").GetContext(name);

        /// <summary>The groups of a context after its scale (340), as the text of the drawing holds them, numbers rounded to six places.</summary>
        private static string Context(DxfDatabase db, DxfDBObject o, string key)
        {
            var manager = (DxfDBDictionary)o.ExtensionDictionary.GetAt("AcDbContextDataManager");
            var scales = (DxfDBDictionary)manager.GetAt("ACDB_ANNOTATIONSCALES");
            string handle = scales.GetAt(key).Handle;
            string[] lines = Records(db.ToText()).Single(r => HandleOf(r) == handle).Split('\n');
            var groups = new List<KeyValuePair<int, string>>();

            for (int i = 2; i + 1 < lines.Length; i += 2)
            {
                groups.Add(
                    new KeyValuePair<int, string>(
                        int.Parse(lines[i].Trim(), CultureInfo.InvariantCulture),
                        lines[i + 1].Trim()
                    )
                );
            }

            return string.Join(
                " ",
                groups
                    .SkipWhile(g => g.Key != 340)
                    .Skip(1)
                    .Select(g =>
                        g.Key
                        + "="
                        + (
                            g.Key != 100
                            && double.TryParse(g.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double d)
                                ? Math.Round(d + 0.0, 6).ToString("0.######", CultureInfo.InvariantCulture)
                                : g.Value
                        )
                    )
            );
        }

        private static GeoTransform3 AcrossX40() =>
            GeoTransform3.Mirror(new GeoPlane3(new GeoPoint3(40, 0, 0), new GeoVector3(1, 0, 0)));

        [Fact]
        public void AnAnnotativeTextMirrored_HasEachContextTurnedOverAboutTheMiddleOfItsInkAtItsOwnHeight()
        {
            InFolder(folder =>
            {
                DxfDatabase db = Scaled(folder);
                var text = new DxfDBText
                {
                    Position = new GeoPoint3(20, 40, 0),
                    TextString = "ABC",
                    Height = 3.0,
                };
                db.ModelSpace.AppendEntity(text);
                text.Annotative = DxfAnnotativeStates.True;
                text.AddContext(Of(db, "1:2"));

                text.TransformBy(AcrossX40());

                // AutoCAD 2021 (its "ABC" inked 8.445 wide at 3 high): the record and the context of 1:1 at 51.555, the context of 1:2,
                // which stands 6 high and inked 16.89, at 43.109, each reading along x with the normal (0, 0, 1) as they did. Here
                // the ink is 8 wide: 52 and 44.
                Assert.Equal(52.0, text.Position.X, 9);
                Assert.Equal(40.0, text.Position.Y, 9);
                Assert.Equal(0.0, text.Normal.X, 12);
                Assert.Equal(1.0, text.Normal.Z, 12);
                Assert.StartsWith("70=0 50=0 10=52 20=40", Context(db, text, "*A1"), StringComparison.Ordinal);
                Assert.StartsWith("70=0 50=0 10=44 20=40", Context(db, text, "*A2"), StringComparison.Ordinal);
            });
        }

        [Fact]
        public void AnAnnotativeTextHeldByItsRightEnd_Mirrored_HasEveryContextsPointsSwapped_WithNoFont()
        {
            DxfDatabase db = Scaled(null);
            var text = new DxfDBText
            {
                Position = new GeoPoint3(22, 45, 0),
                TextString = "ABC",
                Height = 3.0,
            };
            db.ModelSpace.AppendEntity(text);
            text.HorizontalMode = DxfTextHorizontalMode.TextRight;
            text.AlignmentPoint = new GeoPoint3(30, 45, 0);
            text.Annotative = DxfAnnotativeStates.True;
            text.AddContext(Of(db, "1:2"));

            text.TransformBy(AcrossX40());

            // AutoCAD 2021: the first point (50, 45), the second (58, 45), in the record and in each context.
            Assert.Equal(50.0, text.Position.X, 9);
            Assert.Equal(58.0, text.AlignmentPoint.X, 9);
            Assert.StartsWith("70=2 50=0 10=50 20=45 11=58 21=45", Context(db, text, "*A1"), StringComparison.Ordinal);
            Assert.StartsWith("70=2 50=0 10=50 20=45 11=58 21=45", Context(db, text, "*A2"), StringComparison.Ordinal);
        }

        [Fact]
        public void AnAnnotativeMTextMirrored_IsHeldByTheOtherSideOfItsBox_InItsRecordAndItsContexts()
        {
            DxfDatabase db = Scaled(null);
            var words = new DxfMText
            {
                Contents = "ABC DEF",
                TextHeight = 3.0,
                Width = 20.0,
                Location = new GeoPoint3(20, 40, 0),
            };
            db.ModelSpace.AppendEntity(words);
            words.Annotative = DxfAnnotativeStates.True;
            words.AddContext(Of(db, "1:2"));

            words.TransformBy(AcrossX40());

            // AutoCAD 2021: held by its top right (3) where it was held by its top left (1), reading along (1, 0, 0) with the
            // normal (0, 0, 1) from the mirrored point (60, 40), in the record and in the contexts of 1:1 and 1:2.
            Assert.Equal(DxfAttachmentPoint.TopRight, words.Attachment);
            Assert.Equal(60.0, words.Location.X, 9);
            Assert.StartsWith(
                "70=3 10=1 20=0 30=0 11=60 21=40 31=0 40=20",
                Context(db, words, "*A1"),
                StringComparison.Ordinal
            );
            Assert.StartsWith(
                "70=3 10=1 20=0 30=0 11=60 21=40 31=0 40=40",
                Context(db, words, "*A2"),
                StringComparison.Ordinal
            );
        }
    }
}
