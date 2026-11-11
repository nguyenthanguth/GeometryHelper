using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// Annotative MTEXTs and INSERTs read and moved as AutoCAD 2021 reads and moves them (plugin D35b), as
    /// <see cref="DxfAnnotativeD35Tests"/> checks texts and attributes: shown at their context of the scale 1:1, else at their
    /// default; a property set written into the shown context, and into the record where that context is the default; a
    /// transformation carrying every context. With them an attribute of several lines made of an annotative one, the
    /// annotative state of attributes, the horizontal mode of a context and the copy of an annotative text. The drawing is
    /// ezdxf's of R2018, its MTEXT 73 at (20, 40) 3 high and 40 wide, its INSERT 65 of BOLT placed and its ATTRIB 67 put where
    /// AutoCAD's sample Mechanical - Metric has the INSERT of Valve - Metric and its attribute ATT_DVSUP.
    /// </summary>
    public class DxfAnnotativeD35bTests
    {
        private const string ScaleList = "  0\nDICTIONARY\n  5\n14\n330\nA\n100\nAcDbDictionary\n281\n1\n";
        private const string AttributeContext = "ACDB_MTEXTATTRIBUTEOBJECTCONTEXTDATA_CLASS";
        private const string TextContext = "ACDB_TEXTOBJECTCONTEXTDATA_CLASS";
        private const string MTextContext = "ACDB_MTEXTOBJECTCONTEXTDATA_CLASS";
        private const string InsertContext = "ACDB_BLKREFOBJECTCONTEXTDATA_CLASS";

        private static readonly DxfTypedValue[] AnnotativeXData =
        {
            new DxfTypedValue(1001, "AcadAnnotative"),
            new DxfTypedValue(1000, "AnnotativeData"),
            new DxfTypedValue(1002, "{"),
            new DxfTypedValue(1070, "1"),
            new DxfTypedValue(1070, "1"),
            new DxfTypedValue(1002, "}"),
        };

        // The valve of the sample: its own point, and that of its context of 1:1 that AutoCAD 2021 was given (plugin D35b I1).
        private const double OwnX = 13.88342029351079;
        private const double ShownX = 113.88342029351079;
        private const double ValveY = 279.776177521622;

        // ATT_DVSUP of the sample: its context of 1:1.
        private const double SampleX = -6.151391694414086;
        private const double SampleY = 265.3285550728426;

        /// <summary>A context of a scale, as AutoCAD 2021 writes one: its default flag, its scale and its own groups.</summary>
        private sealed class Context
        {
            internal Context(string handle, string type, string scale, bool isDefault, string groups)
            {
                Handle = handle;
                Type = type;
                Scale = scale;
                IsDefault = isDefault;
                Groups = groups;
            }

            internal string Handle { get; }

            internal string Type { get; }

            internal string Scale { get; }

            internal bool IsDefault { get; }

            internal string Groups { get; }
        }

        private static string N(double value) => value.ToString("R", CultureInfo.InvariantCulture);

        /// <summary>The own groups of a context of an MTEXT, AutoCAD's order: attachment, direction, location, width, box, extents, columns.</summary>
        private static string MTextGroups(
            int attachment,
            double dx,
            double dy,
            double x,
            double y,
            double width,
            double box
        ) =>
            " 70\n"
            + attachment.ToString(CultureInfo.InvariantCulture)
            + "\n 10\n"
            + N(dx)
            + "\n 20\n"
            + N(dy)
            + "\n 30\n0.0\n 11\n"
            + N(x)
            + "\n 21\n"
            + N(y)
            + "\n 31\n0.0\n 40\n"
            + N(width)
            + "\n 41\n"
            + N(box)
            + "\n 42\n0.0\n 43\n0.0\n 71\n0\n";

        /// <summary>Those of an INSERT's: the angle, the point, the scale factors.</summary>
        private static string InsertGroups(double degrees, double x, double y, double scale) =>
            " 50\n"
            + N(degrees)
            + "\n 10\n"
            + N(x)
            + "\n 20\n"
            + N(y)
            + "\n 30\n0.0\n 41\n"
            + N(scale)
            + "\n 42\n"
            + N(scale)
            + "\n 43\n"
            + N(scale)
            + "\n";

        /// <summary>Those of an attribute's: the horizontal mode, the angle, the two points, the flag of no MTEXT after them.</summary>
        private static string AttributeGroups(
            int mode,
            double degrees,
            double x,
            double y,
            double ax = 0.0,
            double ay = 0.0
        ) =>
            " 70\n"
            + mode.ToString(CultureInfo.InvariantCulture)
            + "\n 50\n"
            + N(degrees)
            + "\n 10\n"
            + N(x)
            + "\n 20\n"
            + N(y)
            + "\n 11\n"
            + N(ax)
            + "\n 21\n"
            + N(ay)
            + "\n290\n0\n";

        /// <summary>Those of a TEXT's.</summary>
        private static string TextGroups(int mode, double degrees, double x, double y) =>
            " 70\n"
            + mode.ToString(CultureInfo.InvariantCulture)
            + "\n 50\n"
            + N(degrees)
            + "\n 10\n"
            + N(x)
            + "\n 20\n"
            + N(y)
            + "\n 11\n0.0\n 21\n0.0\n";

        /// <summary>
        /// The ezdxf drawing of R2018 with the scales 1:1 (F0), 1:2 (F1) and 1:4 (F2) in its list, its INSERT 65 at (13.883,
        /// 279.776) unscaled and unturned and its ATTRIB 67 at (0.678, 265.33), 3 high; the contexts given to the records of the
        /// handles given, those made annotative.
        /// </summary>
        private static string Drawing(params KeyValuePair<string, Context[]>[] contexts)
        {
            string text = DxfFileEzdxfDrawings.R2018;
            Assert.Contains(ScaleList, text, StringComparison.Ordinal);
            string scales =
                Scale("F0", "1:1", "1", "1", "1")
                + Scale("F1", "1:2", "1", "2", "0")
                + Scale("F2", "1:4", "1", "4", "0");
            int end = text.LastIndexOf("  0\nENDSEC\n", StringComparison.Ordinal);
            text = (text.Substring(0, end) + scales + text.Substring(end)).Replace(
                ScaleList,
                ScaleList + "  3\nA0\n350\nF0\n  3\nA1\n350\nF1\n  3\nA2\n350\nF2\n"
            );
            text = Swap(
                text,
                "65",
                "\n 10\n50.0\n 20\n60.0\n 30\n0.0\n 41\n1.5\n 42\n-1.5\n 43\n2.0\n 50\n45.0\n",
                "\n 10\n" + N(OwnX) + "\n 20\n" + N(ValveY) + "\n 30\n0.0\n"
            );
            text = Swap(
                text,
                "67",
                "\n 10\n50.0\n 20\n59.0\n 30\n0.0\n 40\n2.5\n",
                "\n 10\n0.6779381070854527\n 20\n" + N(SampleY) + "\n 30\n0.0\n 40\n3.0\n"
            );

            DxfDatabase db = DxfDatabase.Parse(text);
            var owners = new Dictionary<string, string>();

            foreach (KeyValuePair<string, Context[]> held in contexts)
            {
                DxfEntity entity =
                    held.Key == "67" ? Insert(db).AttributeCollection[0] : ByHandle<DxfEntity>(db, held.Key);
                entity.XData = AnnotativeXData;
                entity.CreateExtensionDictionary();
                var manager = new DxfDBDictionary { TreatElementsAsHard = true };
                entity.ExtensionDictionary.SetAt("AcDbContextDataManager", manager);
                var dictionary = new DxfDBDictionary { TreatElementsAsHard = true };
                manager.SetAt("ACDB_ANNOTATIONSCALES", dictionary);
                owners[held.Key] = dictionary.Handle;
            }

            text = db.ToText();

            foreach (KeyValuePair<string, Context[]> held in contexts)
            {
                string owner = owners[held.Key];
                var records = new System.Text.StringBuilder();
                var entries = new System.Text.StringBuilder();

                for (int i = 0; i < held.Value.Length; i++)
                {
                    Context c = held.Value[i];
                    records.Append(
                        "  0\n"
                            + c.Type
                            + "\n  5\n"
                            + c.Handle
                            + "\n102\n{ACAD_REACTORS\n330\n"
                            + owner
                            + "\n102\n}\n330\n"
                            + owner
                            + "\n100\nAcDbObjectContextData\n 70\n4\n290\n"
                            + (c.IsDefault ? "1" : "0")
                            + "\n100\nAcDbAnnotScaleObjectContextData\n340\n"
                            + c.Scale
                            + "\n"
                            + c.Groups
                    );
                    entries.Append(
                        "  3\n*A" + (i + 1).ToString(CultureInfo.InvariantCulture) + "\n350\n" + c.Handle + "\n"
                    );
                }

                end = text.LastIndexOf("  0\nENDSEC\n", StringComparison.Ordinal);
                text = text.Substring(0, end) + records + text.Substring(end);
                string record = Records(text).Single(r => HandleOf(r) == owner);
                text = text.Replace(record, record + entries);
            }

            return text;
        }

        private static string Scale(string handle, string name, string paper, string drawing, string unit) =>
            "  0\nSCALE\n  5\n"
            + handle
            + "\n102\n{ACAD_REACTORS\n330\n14\n102\n}\n330\n14\n100\nAcDbScale\n 70\n0\n300\n"
            + name
            + "\n140\n"
            + paper
            + "\n141\n"
            + drawing
            + "\n290\n"
            + unit
            + "\n";

        /// <summary>The text with the part of a record replaced.</summary>
        private static string Swap(string text, string handle, string from, string to)
        {
            string record = Records(text).Single(r => HandleOf(r) == handle);
            Assert.Contains(from, record, StringComparison.Ordinal);
            return text.Replace(record, record.Replace(from, to));
        }

        private static KeyValuePair<string, Context[]> On(string handle, params Context[] contexts) =>
            new KeyValuePair<string, Context[]>(handle, contexts);

        private static DxfBlockReference Insert(DxfDatabase db) => ByHandle<DxfBlockReference>(db, "65");

        private static DxfAttributeReference Attribute(DxfDatabase db) => Insert(db).AttributeCollection[0];

        private static DxfMText Words(DxfDatabase db) => ByHandle<DxfMText>(db, "73");

        /// <summary>
        /// The MTEXT 73 of two contexts, as AutoCAD 2021 read its M1 (plugin D35b W1, W2): of 1:1 at (14, 44) turned 30 degrees,
        /// held by its middle, 30 wide in a box 15 high; of 1:2 at (60, 44), unturned, 80 wide in a box 40 high; 1:1 the default
        /// unless said.
        /// </summary>
        private static string Note(bool unitIsDefault = true) =>
            Drawing(
                On(
                    "73",
                    new Context(
                        "E01",
                        MTextContext,
                        "F0",
                        unitIsDefault,
                        MTextGroups(5, 0.8660254037844387, 0.5, 14.0, 44.0, 30.0, 15.0)
                    ),
                    new Context(
                        "E02",
                        MTextContext,
                        "F1",
                        !unitIsDefault,
                        MTextGroups(1, 1.0, 0.0, 60.0, 44.0, 80.0, 40.0)
                    )
                )
            );

        /// <summary>
        /// The INSERT 65 of two contexts, as AutoCAD 2021 read the sample's valve (plugin D35b I2): of 1:1 at (113.883, 279.776)
        /// turned 45 degrees, scaled by 2; of 1:2 at (-50, 279.776) turned 10 degrees, scaled by 3; 1:1 the default unless said,
        /// and then the only one.
        /// </summary>
        private static string Valve(bool unitIsDefault = true) =>
            unitIsDefault
                ? Drawing(
                    On("65", new Context("E01", InsertContext, "F0", true, InsertGroups(45.0, ShownX, ValveY, 2.0)))
                )
                : Drawing(
                    On(
                        "65",
                        new Context("E01", InsertContext, "F0", false, InsertGroups(45.0, ShownX, ValveY, 2.0)),
                        new Context("E02", InsertContext, "F1", true, InsertGroups(10.0, -50.0, ValveY, 3.0))
                    )
                );

        /// <summary>The numbers of a context's groups after its scale (340), by code, the first of each, up to its MTEXT.</summary>
        private static Dictionary<int, double> ContextOf(string text, string handle) => NumbersOf(text, handle, 340, 1);

        /// <summary>The numbers of the MTEXT an attribute's context holds, after its second scale (340), by code.</summary>
        private static Dictionary<int, double> LinesOf(string text, string handle) => NumbersOf(text, handle, 340, 2);

        /// <summary>The numbers of a record's own groups after the marker given, by code, the first of each.</summary>
        private static Dictionary<int, double> OwnOf(string text, string handle, string marker)
        {
            string[] lines = Records(text).Single(r => HandleOf(r) == handle).Split('\n');
            var numbers = new Dictionary<int, double>();
            bool after = false;

            for (int i = 2; i + 1 < lines.Length; i += 2)
            {
                int code = int.Parse(lines[i].Trim(), CultureInfo.InvariantCulture);

                if (code == 100)
                {
                    after |= lines[i + 1].Trim() == marker;
                    continue;
                }

                if (
                    after
                    && !numbers.ContainsKey(code)
                    && double.TryParse(
                        lines[i + 1].Trim(),
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out double value
                    )
                )
                {
                    numbers[code] = value;
                }
            }

            return numbers;
        }

        private static Dictionary<int, double> NumbersOf(string text, string handle, int from, int turn)
        {
            string[] lines = Records(text).Single(r => HandleOf(r) == handle).Split('\n');
            var numbers = new Dictionary<int, double>();
            int seen = 0;

            for (int i = 2; i + 1 < lines.Length; i += 2)
            {
                int code = int.Parse(lines[i].Trim(), CultureInfo.InvariantCulture);

                if (code == from)
                {
                    seen++;
                    continue;
                }

                if (seen == turn && turn == 1 && code == 290)
                {
                    break;
                }

                if (
                    seen == turn
                    && !numbers.ContainsKey(code)
                    && double.TryParse(
                        lines[i + 1].Trim(),
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out double value
                    )
                )
                {
                    numbers[code] = value;
                }
            }

            return numbers;
        }

        private static string RecordOf(string text, string handle) => Records(text).Single(r => HandleOf(r) == handle);

        private static void Near(double expected, double actual) =>
            Assert.True(
                Math.Abs(expected - actual) <= 1E-9 * Math.Max(1.0, Math.Abs(expected)),
                "expected " + N(expected) + ", got " + N(actual)
            );

        private static void Near(GeoPoint3 expected, GeoPoint3 actual)
        {
            Near(expected.X, actual.X);
            Near(expected.Y, actual.Y);
            Near(expected.Z, actual.Z);
        }

        private static void At(Dictionary<int, double> context, int code, double x, double y)
        {
            Near(x, context[code]);
            Near(y, context[code + 10]);
        }

        private static DxfObjectContext ScaleNamed(DxfDatabase db, string name) =>
            db.ObjectContextManager.GetContextCollection("ACDB_ANNOTATIONSCALES").GetContext(name);

        // ---- 1. MTEXT -------------------------------------------------------------------------------------------------------

        [Fact]
        public void AnAnnotativeMText_IsReadAtItsContextOf1To1_ItsWidthAndBoxThatContextsOwn_ItsHeightThatOfItsScale()
        {
            // AutoCAD 2021 read the M1 its record put at (10, 40), 40 wide in a box 20, at its context of 1:1: at (14, 44), turned
            // 30 degrees, held by its middle, 30 wide in a box 15; its height the record's 2.5, or 1.25 where 1:2 was the
            // default (plugin D35b W1, W2). Here the record is 3 high.
            foreach (bool unitIsDefault in new[] { true, false })
            {
                DxfMText words = Words(DxfDatabase.Parse(Note(unitIsDefault)));
                Assert.Equal(new GeoPoint3(14.0, 44.0, 0.0), words.Location);
                Near(0.8660254037844387, words.Direction.X);
                Near(0.5, words.Direction.Y);
                Near(Math.PI / 6.0, words.Rotation);
                Assert.Equal(30.0, words.Width);
                Assert.Equal(15.0, words.Height);
                Assert.Equal(DxfAttachmentPoint.MiddleCenter, words.Attachment);
                Assert.Equal(unitIsDefault ? 3.0 : 1.5, words.TextHeight);
            }
        }

        [Fact]
        public void AnAnnotativeMTextSet_WritesItsShownContextAlone_WhereAnotherIsTheDefault_ItsHeightAtTheDefaultScale()
        {
            // AutoCAD 2021, with 1:2 the default, set M1's location (20, 45), turn 0.5, width 50, box 30 and attachment 7 in its
            // context of 1:1 alone, and its height 5 as 10 in the record (plugin D35b W2 loc, rot, w, bh, th; r2 att7).
            string text = Note(unitIsDefault: false);
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfMText words = Words(db);
            words.Location = new GeoPoint3(20.0, 45.0, 0.0);
            words.Rotation = 0.5;
            words.Width = 50.0;
            words.Height = 30.0;
            words.Attachment = DxfAttachmentPoint.BottomLeft;
            words.TextHeight = 5.0;
            string saved = db.ToText();

            Dictionary<int, double> shown = ContextOf(saved, "E01");
            At(shown, 11, 20.0, 45.0);
            At(shown, 10, Math.Cos(0.5), Math.Sin(0.5));
            Assert.Equal(50.0, shown[40]);
            Assert.Equal(30.0, shown[41]);
            Assert.Equal(7.0, shown[70]);
            Assert.Equal(ContextOf(text, "E02"), ContextOf(saved, "E02"));
            Dictionary<int, double> own = OwnOf(saved, "73", "AcDbMText");
            Assert.Equal(20.0, own[10]);
            Assert.Equal(40.0, own[20]);
            Assert.Equal(40.0, own[41]);
            Assert.Equal(10.0, own[40]);

            DxfMText back = Words(DxfDatabase.Parse(saved));
            Assert.Equal(new GeoPoint3(20.0, 45.0, 0.0), back.Location);
            Near(0.5, back.Rotation);
            Assert.Equal(50.0, back.Width);
            Assert.Equal(5.0, back.TextHeight);
        }

        [Fact]
        public void AnAnnotativeMTextSet_WritesItsRecordToo_WhereItsShownContextIsTheDefault()
        {
            DxfDatabase db = DxfDatabase.Parse(Note());
            Words(db).Location = new GeoPoint3(20.0, 45.0, 0.0);
            Words(db).Width = 50.0;
            string saved = db.ToText();
            At(ContextOf(saved, "E01"), 11, 20.0, 45.0);
            Assert.Equal(50.0, ContextOf(saved, "E01")[40]);
            Dictionary<int, double> own = OwnOf(saved, "73", "AcDbMText");
            Assert.Equal(20.0, own[10]);
            Assert.Equal(45.0, own[20]);
            Assert.Equal(50.0, own[41]);
        }

        [Fact]
        public void AnAnnotativeMTextTurnedAndScaled_CarriesEveryContext_ItsWidthAndBoxStretched()
        {
            // AutoCAD 2021 turned M1 30 degrees about the origin: its context of 1:2 to (29.962, 68.105), along (0.866, 0.5);
            // and scaled it by 2: that of 1:1 to (28, 88), 60 wide in a box 30, that of 1:2 to (120, 88), 160 by 80 (plugin D35b
            // W1 turn, scale).
            DxfDatabase db = DxfDatabase.Parse(Note());
            Words(db)
                .TransformBy(
                    GeoTransform3.RotationAxis(
                        new GeoPoint3(0.0, 0.0, 0.0),
                        new GeoVector3(0.0, 0.0, 1.0),
                        Math.PI / 6.0
                    )
                );
            string saved = db.ToText();
            At(ContextOf(saved, "E02"), 11, 29.96152422706633, 68.1051177665153);
            At(ContextOf(saved, "E02"), 10, 0.8660254037844387, 0.5);
            At(ContextOf(saved, "E01"), 11, -9.875644347017853, 45.1051177665153);
            Near(Math.PI / 3.0, Words(DxfDatabase.Parse(saved)).Rotation);

            db = DxfDatabase.Parse(Note());
            Words(db).TransformBy(GeoTransform3.Scaling(new GeoPoint3(0.0, 0.0, 0.0), 2.0));
            saved = db.ToText();
            At(ContextOf(saved, "E01"), 11, 28.0, 88.0);
            Near(60.0, ContextOf(saved, "E01")[40]);
            Near(30.0, ContextOf(saved, "E01")[41]);
            At(ContextOf(saved, "E02"), 11, 120.0, 88.0);
            Near(160.0, ContextOf(saved, "E02")[40]);
            Near(80.0, ContextOf(saved, "E02")[41]);
            Assert.Equal(6.0, Words(DxfDatabase.Parse(saved)).TextHeight);
        }

        [Fact]
        public void AnMTextOfNoContext_IsReadSetAndMovedFromItsOwnGroups_AsBefore()
        {
            string text = Drawing();
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfMText words = Words(db);
            Assert.Equal(new GeoPoint3(20.0, 40.0, 0.0), words.Location);
            Assert.Equal(3.0, words.TextHeight);
            Assert.Equal(40.0, words.Width);
            Assert.Equal(DxfAttachmentPoint.TopLeft, words.Attachment);
            words.Location = new GeoPoint3(1.0, 2.0, 0.0);
            words.TransformBy(GeoTransform3.Translation(new GeoVector3(10.0, 5.0, 0.0)));
            Assert.Equal(new GeoPoint3(11.0, 7.0, 0.0), Words(DxfDatabase.Parse(db.ToText())).Location);
        }

        [Fact]
        public void SettingWhatAnAnnotativeMTextShows_ChangesNothing()
        {
            string text = Note(unitIsDefault: false);
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfMText words = Words(db);
            words.Location = words.Location;
            words.Rotation = words.Rotation;
            words.TextHeight = words.TextHeight;
            words.Width = words.Width;
            words.Height = words.Height;
            words.Attachment = words.Attachment;
            words.TransformBy(GeoTransform3.Identity);
            Assert.Equal(text, db.ToText());
        }

        // ---- 2. INSERT ------------------------------------------------------------------------------------------------------

        [Fact]
        public void AnINSERTOfContexts_IsReadAtItsContextOf1To1_TurnedAndScaledSo()
        {
            // AutoCAD 2021 read the sample's valve, whose record says (13.883, 279.776) unturned and unscaled, at its context of
            // 1:1: at (113.883, 279.776), turned 45 degrees, scaled by 2, the default or not (plugin D35b I1, I2).
            foreach (bool unitIsDefault in new[] { true, false })
            {
                DxfBlockReference valve = Insert(DxfDatabase.Parse(Valve(unitIsDefault)));
                Assert.Equal(new GeoPoint3(ShownX, ValveY, 0.0), valve.Position);
                Assert.Equal(Math.PI / 4.0, valve.Rotation);
                Assert.Equal(new GeoVector3(2.0, 2.0, 2.0), valve.ScaleFactors);
                Near(new GeoPoint3(ShownX, ValveY, 0.0), valve.BlockTransform.Transform(valve.BlockTableRecord.Origin));
            }
        }

        [Fact]
        public void AnINSERTSet_WritesItsShownContext_AndItsScaleIntoEveryContextByItsScale()
        {
            // AutoCAD 2021, with 1:2 the default, wrote the valve's point (50, 300) and turn 0.5 into its context of 1:1 alone;
            // its scale 3 as 3 there and as 6 at 1:2 and in its record, which holds the default's (plugin D35b I2 pos, rot, sf).
            string text = Valve(unitIsDefault: false);
            DxfDatabase db = DxfDatabase.Parse(text);
            Insert(db).Position = new GeoPoint3(50.0, 300.0, 0.0);
            Insert(db).Rotation = 0.5;
            string saved = db.ToText();
            At(ContextOf(saved, "E01"), 10, 50.0, 300.0);
            Near(28.64788975654116, ContextOf(saved, "E01")[50]);
            Assert.Equal(ContextOf(text, "E02"), ContextOf(saved, "E02"));
            Assert.Equal(OwnX, OwnOf(saved, "65", "AcDbBlockReference")[10]);
            Assert.False(OwnOf(saved, "65", "AcDbBlockReference").ContainsKey(50));

            db = DxfDatabase.Parse(text);
            Insert(db).ScaleFactors = new GeoVector3(3.0, 3.0, 3.0);
            saved = db.ToText();
            Assert.Equal(3.0, ContextOf(saved, "E01")[41]);
            Assert.Equal(6.0, ContextOf(saved, "E02")[41]);
            Assert.Equal(6.0, OwnOf(saved, "65", "AcDbBlockReference")[41]);
            Assert.Equal(new GeoVector3(3.0, 3.0, 3.0), Insert(DxfDatabase.Parse(saved)).ScaleFactors);
        }

        [Fact]
        public void AnINSERTTurned_CarriesEveryContext_EachFromItsOwnPointAndAngle()
        {
            // AutoCAD 2021 turned the valve 30 degrees about the origin: its context of 1:1 to (-41.262, 299.235) turned 75
            // degrees, that of 1:2 to (-183.189, 217.293) turned 40 (plugin D35b I2 turn).
            DxfDatabase db = DxfDatabase.Parse(Valve(unitIsDefault: false));
            Insert(db)
                .TransformBy(
                    GeoTransform3.RotationAxis(
                        new GeoPoint3(0.0, 0.0, 0.0),
                        new GeoVector3(0.0, 0.0, 1.0),
                        Math.PI / 6.0
                    )
                );
            string saved = db.ToText();
            At(ContextOf(saved, "E01"), 10, -41.26215371677034, 299.2349872541849);
            Near(75.0, ContextOf(saved, "E01")[50]);
            At(ContextOf(saved, "E02"), 10, -183.1893589500329, 217.2932771074295);
            Near(40.0, ContextOf(saved, "E02")[50]);
            Near(new GeoPoint3(-41.26215371677034, 299.2349872541849, 0.0), Insert(DxfDatabase.Parse(saved)).Position);
        }

        // ---- 3. annotative states, horizontal mode ----------------------------------------------------------------------------

        [Fact]
        public void AnINSERTOfAnAnnotativeBlock_AndItsAttribute_HoldingNoContext_AreShownAt1To1()
        {
            // AutoCAD 2021 said HasContext of 1:1, not of 1:2, of the INSERT of an annotative block that held no context, and of
            // an attribute of it that held none and no AcadAnnotative, which it took as annotative (plugin D35b E1, E3).
            DxfDatabase db = DxfDatabase.Parse(Drawing());
            Insert(db).BlockTableRecord.XData = AnnotativeXData;
            Assert.True(Insert(db).HasContext(ScaleNamed(db, "1:1")));
            Assert.False(Insert(db).HasContext(ScaleNamed(db, "1:2")));
            Assert.Equal(DxfAnnotativeStates.True, Attribute(db).Annotative);
            Assert.True(Attribute(db).HasContext(ScaleNamed(db, "1:1")));
            Assert.False(ByHandle<DxfMText>(db, "73").HasContext(ScaleNamed(db, "1:1")));

            // That scale is the last it is shown at, which AutoCAD keeps.
            Assert.Throws<InvalidOperationException>(() => Insert(db).RemoveContext(ScaleNamed(db, "1:1")));
        }

        [Fact]
        public void AnAttribute_IsAnnotativeAsItsINSERTIs_WhateverItsOwnDataSays()
        {
            // AutoCAD 2021 read the sample's ATT_DVSUP, whose AcadAnnotative says 0, as annotative in its annotative valve, and
            // an attribute of contexts as not annotative where its block was not (plugin D35 a, D35b E4).
            string text = Drawing(
                On("67", new Context("E01", AttributeContext, "F0", true, AttributeGroups(0, 0.0, SampleX, SampleY)))
            );
            DxfDatabase db = DxfDatabase.Parse(text);
            Attribute(db).XData = new[]
            {
                new DxfTypedValue(1001, "AcadAnnotative"),
                new DxfTypedValue(1000, "AnnotativeData"),
                new DxfTypedValue(1002, "{"),
                new DxfTypedValue(1070, "1"),
                new DxfTypedValue(1070, "0"),
                new DxfTypedValue(1002, "}"),
            };
            Assert.Equal(DxfAnnotativeStates.False, Attribute(db).Annotative);
            Insert(db).BlockTableRecord.XData = AnnotativeXData;
            Assert.Equal(DxfAnnotativeStates.True, Attribute(db).Annotative);
        }

        [Fact]
        public void AnAnnotativeAttribute_TakesItsHorizontalModeFromItsShownContext_AndIsSetThere()
        {
            // AutoCAD 2021 read an attribute aligned left whose context of 1:1 says 2 as aligned right (plugin D35 b B4), and set
            // a centred TEXT aligned right in its context of 1:1 alone where 1:2 was the default (plugin D35b G2 hm).
            DxfDatabase db = DxfDatabase.Parse(
                Drawing(
                    On(
                        "67",
                        new Context(
                            "E01",
                            AttributeContext,
                            "F0",
                            true,
                            AttributeGroups(2, 0.0, SampleX, SampleY, -1.0, 271.0)
                        )
                    )
                )
            );
            Assert.Equal(DxfTextHorizontalMode.TextRight, Attribute(db).HorizontalMode);
            Assert.Equal(DxfAttachmentPoint.BaseRight, Attribute(db).Justify);
            Assert.False(Attribute(db).IsDefaultAlignment);

            string text = Drawing(
                On(
                    "64",
                    new Context("E01", TextContext, "F0", false, TextGroups(1, 0.0, 10.9, 23.0)),
                    new Context("E02", TextContext, "F1", true, TextGroups(1, 0.0, 47.9, 23.0))
                )
            );
            db = DxfDatabase.Parse(text);
            ByHandle<DxfDBText>(db, "64").HorizontalMode = DxfTextHorizontalMode.TextRight;
            string saved = db.ToText();
            Assert.Equal(2.0, ContextOf(saved, "E01")[70]);
            Assert.Equal(1.0, ContextOf(saved, "E02")[70]);
            Assert.Equal(
                DxfTextHorizontalMode.TextRight,
                ByHandle<DxfDBText>(DxfDatabase.Parse(saved), "64").HorizontalMode
            );
            Assert.Equal(RecordOf(text, "64"), RecordOf(saved, "64"));
        }

        // ---- 4. attributes of several lines, copies ---------------------------------------------------------------------------

        [Fact]
        public void AnAnnotativeAttributeMadeOfSeveralLines_WritesItsMTextIntoEveryContext_AndTheMoveCarriesThem()
        {
            // AutoCAD 2021 made ATTRIB T1 of two contexts one of several lines: each context's second point the MTEXT's location,
            // its 290 made 1, the MTEXT's own context embedded after it; moved by (10, 5), each context's MTEXT moved with it
            // (plugin D35b G1/G2 multi, r2 M1/M2 move).
            DxfDatabase db = DxfDatabase.Parse(
                Drawing(
                    On(
                        "67",
                        new Context("E01", AttributeContext, "F0", true, AttributeGroups(0, 0.0, SampleX, SampleY)),
                        new Context("E02", AttributeContext, "F1", false, AttributeGroups(0, 0.0, -100.0, SampleY))
                    )
                )
            );
            Attribute(db).IsMTextAttribute = true;
            string saved = db.ToText();
            GeoPoint3 at = Attribute(db).MTextAttribute.Location;

            foreach (string context in new[] { "E01", "E02" })
            {
                Assert.Contains(
                    "290\n1\n101\nEmbedded Object\n100\nAcDbObjectContextData\n",
                    RecordOf(saved, context),
                    StringComparison.Ordinal
                );
                At(LinesOf(saved, context), 11, at.X, at.Y);
                At(ContextOf(saved, context), 11, at.X, at.Y);
            }

            Attribute(db).TransformBy(GeoTransform3.Translation(new GeoVector3(10.0, 5.0, 0.0)));
            saved = db.ToText();
            At(LinesOf(saved, "E01"), 11, at.X + 10.0, at.Y + 5.0);
            At(LinesOf(saved, "E02"), 11, at.X + 10.0, at.Y + 5.0);
            At(ContextOf(saved, "E02"), 10, -90.0, SampleY + 5.0);
        }

        [Fact]
        public void TheMTextOfAnAnnotativeAttribute_IsAsTallAsItsShownScale()
        {
            // AutoCAD 2021 gave the MTEXT of T1, its record 2.5 high where 1:2 was the default, 1.25 high (plugin D35b G2 multi).
            DxfDatabase db = DxfDatabase.Parse(
                Drawing(
                    On(
                        "67",
                        new Context("E01", AttributeContext, "F0", false, AttributeGroups(0, 0.0, SampleX, SampleY)),
                        new Context("E02", AttributeContext, "F1", true, AttributeGroups(0, 0.0, -100.0, SampleY))
                    )
                )
            );
            Attribute(db).IsMTextAttribute = true;
            DxfAttributeReference back = Attribute(DxfDatabase.Parse(db.ToText()));
            Assert.Equal(1.5, back.Height);
            Assert.Equal(1.5, back.MTextAttribute.TextHeight);
            Assert.Equal(3.0, OwnOf(db.ToText(), "67", "AcDbText")[40]);

            // The MTEXT embedded in the record holds the height at the default's scale too, as AutoCAD 2021 wrote it, 2.5.
            string record = RecordOf(db.ToText(), "67");
            string embedded = record.Substring(record.IndexOf("101\nEmbedded Object\n", StringComparison.Ordinal));
            string height = embedded.Substring(embedded.IndexOf("\n 40\n", StringComparison.Ordinal) + 5).Split('\n')[
                0
            ];
            Assert.Equal(3.0, double.Parse(height, CultureInfo.InvariantCulture));
        }

        [Fact]
        public void TheCopyOfAnAnnotativeText_ReadsWhereItsOriginalIsShown()
        {
            // AutoCAD 2021's Clone and GetTransformedCopy of T1, shown at (11, 12) 1.25 high at 1:1 where 1:2 was the default,
            // read (11, 12) and, moved by (10, 5), (21, 17), 1.25 high (plugin D35b G2 clone, gtc); so its MTEXT and INSERT.
            string text = Drawing(
                On(
                    "64",
                    new Context("E01", TextContext, "F0", false, TextGroups(0, 15.0, 11.0, 12.0)),
                    new Context("E02", TextContext, "F1", true, TextGroups(0, 0.0, 40.0, 12.0))
                )
            );
            DxfDBText grid = ByHandle<DxfDBText>(DxfDatabase.Parse(text), "64");
            var copy = (DxfDBText)grid.Clone();
            Assert.Equal(new GeoPoint3(11.0, 12.0, 0.0), copy.Position);
            Assert.Equal(1.25, copy.Height);
            Near(15.0 * Math.PI / 180.0, copy.Rotation);
            var moved = (DxfDBText)grid.GetTransformedCopy(GeoTransform3.Translation(new GeoVector3(10.0, 5.0, 0.0)));
            Near(new GeoPoint3(21.0, 17.0, 0.0), moved.Position);

            DxfMText words = Words(DxfDatabase.Parse(Note(unitIsDefault: false)));
            var lines = (DxfMText)words.Clone();
            Assert.Equal(new GeoPoint3(14.0, 44.0, 0.0), lines.Location);
            Assert.Equal(1.5, lines.TextHeight);
            Assert.Equal(30.0, lines.Width);

            var valve = (DxfBlockReference)Insert(DxfDatabase.Parse(Valve(unitIsDefault: false))).Clone();
            Assert.Equal(new GeoPoint3(ShownX, ValveY, 0.0), valve.Position);
            Assert.Equal(new GeoVector3(2.0, 2.0, 2.0), valve.ScaleFactors);
        }
    }
}
