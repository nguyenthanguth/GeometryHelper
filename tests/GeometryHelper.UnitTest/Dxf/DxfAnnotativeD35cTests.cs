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
    /// What <see cref="DxfAnnotativeD35bTests"/> left of annotative objects, as AutoCAD 2021 does it (plugin D35c): copies that
    /// keep their contexts, in no file and once added; the MTEXT's <c>SetAttachmentMovingLocation</c> and <c>Normal</c> and
    /// the INSERT's <c>Normal</c> and <c>BlockTransform</c> written into the context shown; an attribute made of several lines
    /// held by its top one height above; the box of an MTEXT scaled; an ATTRIB flagged annotative in a block that is not. The
    /// drawing is ezdxf's of R2018 with the contexts of <see cref="DxfAnnotativeD35bTests"/>.
    /// </summary>
    public class DxfAnnotativeD35cTests
    {
        private const string ScaleList = "  0\nDICTIONARY\n  5\n14\n330\nA\n100\nAcDbDictionary\n281\n1\n";
        private const string AttributeContext = "ACDB_MTEXTATTRIBUTEOBJECTCONTEXTDATA_CLASS";
        private const string TextContext = "ACDB_TEXTOBJECTCONTEXTDATA_CLASS";
        private const string MTextContext = "ACDB_MTEXTOBJECTCONTEXTDATA_CLASS";
        private const string InsertContext = "ACDB_BLKREFOBJECTCONTEXTDATA_CLASS";

        private static readonly DxfTypedValue[] AnnotativeXData =
        {
            new DxfTypedValue(1001, "AcadAnnotative"), new DxfTypedValue(1000, "AnnotativeData"), new DxfTypedValue(1002, "{"),
            new DxfTypedValue(1070, "1"), new DxfTypedValue(1070, "1"), new DxfTypedValue(1002, "}"),
        };

        // The valve of AutoCAD's sample: its own point, and that of its context of 1:1 (plugin D35b I1).
        private const double OwnX = 13.88342029351079;
        private const double ShownX = 113.88342029351079;
        private const double ValveY = 279.776177521622;

        // ATT_DVSUP of the sample: its context of 1:1.
        private const double SampleX = -6.151391694414086;
        private const double SampleY = 265.3285550728426;

        private static readonly GeoTransform3 Move = GeoTransform3.Translation(new GeoVector3(10.0, 5.0, 0.0));

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
        private static string MTextGroups(int attachment, double dx, double dy, double x, double y, double width, double box)
            => " 70\n" + attachment.ToString(CultureInfo.InvariantCulture) + "\n 10\n" + N(dx) + "\n 20\n" + N(dy) + "\n 30\n0.0\n 11\n" + N(x) + "\n 21\n" + N(y)
                + "\n 31\n0.0\n 40\n" + N(width) + "\n 41\n" + N(box) + "\n 42\n0.0\n 43\n0.0\n 71\n0\n";

        /// <summary>Those of an INSERT's: the angle, the point, the scale factors.</summary>
        private static string InsertGroups(double degrees, double x, double y, double scale)
            => " 50\n" + N(degrees) + "\n 10\n" + N(x) + "\n 20\n" + N(y) + "\n 30\n0.0\n 41\n" + N(scale) + "\n 42\n" + N(scale) + "\n 43\n" + N(scale) + "\n";

        /// <summary>Those of an attribute's: the horizontal mode, the angle, the two points, the flag of no MTEXT after them.</summary>
        private static string AttributeGroups(double x, double y)
            => " 70\n0\n 50\n0.0\n 10\n" + N(x) + "\n 20\n" + N(y) + "\n 11\n0.0\n 21\n0.0\n290\n0\n";

        /// <summary>Those of a TEXT's.</summary>
        private static string TextGroups(double degrees, double x, double y)
            => " 70\n0\n 50\n" + N(degrees) + "\n 10\n" + N(x) + "\n 20\n" + N(y) + "\n 11\n0.0\n 21\n0.0\n";

        /// <summary>
        /// The ezdxf drawing of R2018 with the scales 1:1 (F0), 1:2 (F1) and 1:4 (F2) in its list, its INSERT 65 at (13.883,
        /// 279.776) unscaled and unturned and its ATTRIB 67 at (0.678, 265.33), 3 high; the contexts given to the records of the
        /// handles given, those made annotative.
        /// </summary>
        private static string Drawing(params KeyValuePair<string, Context[]>[] contexts)
        {
            string text = DxfFileEzdxfDrawings.R2018;
            Assert.Contains(ScaleList, text, StringComparison.Ordinal);
            string scales = Scale("F0", "1:1", "1", "1", "1") + Scale("F1", "1:2", "1", "2", "0") + Scale("F2", "1:4", "1", "4", "0");
            int end = text.LastIndexOf("  0\nENDSEC\n", StringComparison.Ordinal);
            text = (text.Substring(0, end) + scales + text.Substring(end)).Replace(ScaleList, ScaleList + "  3\nA0\n350\nF0\n  3\nA1\n350\nF1\n  3\nA2\n350\nF2\n");
            text = Swap(text, "65", "\n 10\n50.0\n 20\n60.0\n 30\n0.0\n 41\n1.5\n 42\n-1.5\n 43\n2.0\n 50\n45.0\n", "\n 10\n" + N(OwnX) + "\n 20\n" + N(ValveY) + "\n 30\n0.0\n");
            text = Swap(text, "67", "\n 10\n50.0\n 20\n59.0\n 30\n0.0\n 40\n2.5\n", "\n 10\n0.6779381070854527\n 20\n" + N(SampleY) + "\n 30\n0.0\n 40\n3.0\n");

            DxfDatabase db = DxfDatabase.Parse(text);
            var owners = new Dictionary<string, string>();

            foreach (KeyValuePair<string, Context[]> held in contexts)
            {
                DxfEntity entity = held.Key == "67" ? Insert(db).AttributeCollection[0] : ByHandle<DxfEntity>(db, held.Key);
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
                    records.Append("  0\n" + c.Type + "\n  5\n" + c.Handle + "\n102\n{ACAD_REACTORS\n330\n" + owner + "\n102\n}\n330\n" + owner
                        + "\n100\nAcDbObjectContextData\n 70\n4\n290\n" + (c.IsDefault ? "1" : "0") + "\n100\nAcDbAnnotScaleObjectContextData\n340\n" + c.Scale + "\n" + c.Groups);
                    entries.Append("  3\n*A" + (i + 1).ToString(CultureInfo.InvariantCulture) + "\n350\n" + c.Handle + "\n");
                }

                end = text.LastIndexOf("  0\nENDSEC\n", StringComparison.Ordinal);
                text = text.Substring(0, end) + records + text.Substring(end);
                string record = Records(text).Single(r => HandleOf(r) == owner);
                text = text.Replace(record, record + entries);
            }

            return text;
        }

        private static string Scale(string handle, string name, string paper, string drawing, string unit)
            => "  0\nSCALE\n  5\n" + handle + "\n102\n{ACAD_REACTORS\n330\n14\n102\n}\n330\n14\n100\nAcDbScale\n 70\n0\n300\n" + name + "\n140\n" + paper + "\n141\n" + drawing + "\n290\n" + unit + "\n";

        /// <summary>The text with the part of a record replaced.</summary>
        private static string Swap(string text, string handle, string from, string to)
        {
            string record = Records(text).Single(r => HandleOf(r) == handle);
            Assert.Contains(from, record, StringComparison.Ordinal);
            return text.Replace(record, record.Replace(from, to));
        }

        private static KeyValuePair<string, Context[]> On(string handle, params Context[] contexts) => new KeyValuePair<string, Context[]>(handle, contexts);

        private static DxfBlockReference Insert(DxfDatabase db) => ByHandle<DxfBlockReference>(db, "65");

        private static DxfAttributeReference Attribute(DxfDatabase db) => Insert(db).AttributeCollection[0];

        private static DxfMText Words(DxfDatabase db) => ByHandle<DxfMText>(db, "73");

        /// <summary>The TEXT 64 shown at (11, 12) turned 15 degrees at 1:1, beside its default of 1:2 at (40, 12) (plugin D35b G2).</summary>
        private static string Grid()
            => Drawing(On("64", new Context("E01", TextContext, "F0", false, TextGroups(15.0, 11.0, 12.0)), new Context("E02", TextContext, "F1", true, TextGroups(0.0, 40.0, 12.0))));

        /// <summary>
        /// The MTEXT 73 of two contexts, as AutoCAD 2021 read its M1 (plugin D35b W1, W2): of 1:1 at (14, 44) turned 30 degrees,
        /// held by its middle, 30 wide in a box 15 high; of 1:2 at (60, 44), unturned, 80 wide in a box 40 high.
        /// </summary>
        private static string Note(bool unitIsDefault)
            => Drawing(On(
                "73",
                new Context("E01", MTextContext, "F0", unitIsDefault, MTextGroups(5, 0.8660254037844387, 0.5, 14.0, 44.0, 30.0, 15.0)),
                new Context("E02", MTextContext, "F1", !unitIsDefault, MTextGroups(1, 1.0, 0.0, 60.0, 44.0, 80.0, 40.0))));

        /// <summary>
        /// The INSERT 65 as AutoCAD 2021 read the sample's valve (plugin D35b I2): of 1:1 at (113.883, 279.776) turned 45
        /// degrees, scaled by 2, beside its default of 1:2 at (-50, 279.776) turned 10 degrees, scaled by 3.
        /// </summary>
        private static KeyValuePair<string, Context[]> ValveContexts()
            => On("65", new Context("E01", InsertContext, "F0", false, InsertGroups(45.0, ShownX, ValveY, 2.0)), new Context("E02", InsertContext, "F1", true, InsertGroups(10.0, -50.0, ValveY, 3.0)));

        /// <summary>The ATTRIB 67 shown at ATT_DVSUP's point at 1:1, beside its default of 1:2 at (-100, 265.33).</summary>
        private static KeyValuePair<string, Context[]> SampleContexts()
            => On("67", new Context("E03", AttributeContext, "F0", false, AttributeGroups(SampleX, SampleY)), new Context("E04", AttributeContext, "F1", true, AttributeGroups(-100.0, SampleY)));

        /// <summary>The numbers of a context's groups after its scale (340), by code, the first of each, up to its MTEXT.</summary>
        private static Dictionary<int, double> ContextOf(string text, string handle) => NumbersOf(text, handle, 1);

        /// <summary>The numbers of the MTEXT an attribute's context holds, after its second scale (340), by code.</summary>
        private static Dictionary<int, double> LinesOf(string text, string handle) => NumbersOf(text, handle, 2);

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

                if (after && !numbers.ContainsKey(code) && double.TryParse(lines[i + 1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                {
                    numbers[code] = value;
                }
            }

            return numbers;
        }

        private static Dictionary<int, double> NumbersOf(string text, string handle, int turn)
        {
            string[] lines = Records(text).Single(r => HandleOf(r) == handle).Split('\n');
            var numbers = new Dictionary<int, double>();
            int seen = 0;

            for (int i = 2; i + 1 < lines.Length; i += 2)
            {
                int code = int.Parse(lines[i].Trim(), CultureInfo.InvariantCulture);

                if (code == 340)
                {
                    seen++;
                    continue;
                }

                if (seen == turn && turn == 1 && code == 290)
                {
                    break;
                }

                if (seen == turn && !numbers.ContainsKey(code) && double.TryParse(lines[i + 1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                {
                    numbers[code] = value;
                }
            }

            return numbers;
        }

        private static void Near(double expected, double actual) => Assert.True(Math.Abs(expected - actual) <= 1E-9 * Math.Max(1.0, Math.Abs(expected)), "expected " + N(expected) + ", got " + N(actual));

        private static void Near(GeoPoint3 expected, GeoPoint3 actual)
        {
            Near(expected.X, actual.X);
            Near(expected.Y, actual.Y);
            Near(expected.Z, actual.Z);
        }

        private static void Near(GeoVector3 expected, GeoVector3 actual)
        {
            Near(expected.X, actual.X);
            Near(expected.Y, actual.Y);
            Near(expected.Z, actual.Z);
        }

        private static void ShownAt(DxfDatabase db, DxfDBObject o, params string[] scales)
        {
            foreach (string name in new[] { "1:1", "1:2", "1:4" })
            {
                Assert.Equal(scales.Contains(name), o.HasContext(db.ObjectContextManager.GetContextCollection("ACDB_ANNOTATIONSCALES").GetContext(name)));
            }
        }

        private static DxfObjectContext ScaleNamed(DxfDatabase db, string name) => db.ObjectContextManager.GetContextCollection("ACDB_ANNOTATIONSCALES").GetContext(name);

        // ---- 1. copies ------------------------------------------------------------------------------------------------------

        [Fact]
        public void TheCloneOfAnAnnotativeText_IsShownAtEveryScaleOfItsOriginal_InNoFileAndOnceAdded()
        {
            // AutoCAD 2021's Clone of T1 said HasContext of 1:1 and 1:2, read (11, 12) 1.25 high, and once appended kept both
            // contexts under an extension dictionary of its own (plugin D35c G2 clone).
            DxfDatabase db = DxfDatabase.Parse(Grid());
            var copy = (DxfDBText)ByHandle<DxfDBText>(db, "64").Clone();
            ShownAt(db, copy, "1:1", "1:2");
            Assert.Equal(new GeoPoint3(11.0, 12.0, 0.0), copy.Position);
            Assert.Equal(1.25, copy.Height);

            db.ModelSpace.AppendEntity(copy);
            ShownAt(db, copy, "1:1", "1:2");
            Assert.Equal(new GeoPoint3(11.0, 12.0, 0.0), copy.Position);

            DxfDatabase back = DxfDatabase.Parse(db.ToText());
            var again = ByHandle<DxfDBText>(back, copy.Handle);
            ShownAt(back, again, "1:1", "1:2");
            Assert.Equal(new GeoPoint3(11.0, 12.0, 0.0), again.Position);
            Assert.Equal(1.25, again.Height);
            ShownAt(back, ByHandle<DxfDBText>(back, "64"), "1:1", "1:2");

            // Its default, shown once its context of 1:1 is taken out, is the original's.
            again.RemoveContext(ScaleNamed(back, "1:1"));
            Assert.Equal(new GeoPoint3(40.0, 12.0, 0.0), again.Position);
        }

        [Fact]
        public void TheTransformedCopyOfAnAnnotativeText_MovesItsShownContextAlone_ItsCloneMovedMovesEvery()
        {
            // AutoCAD 2021's GetTransformedCopy of T1 moved by (10, 5) moved its context of 1:1 to (21, 17) and left that of 1:2
            // at (40, 12); its Clone moved by TransformBy moved both, 1:2 to (50, 17) (plugin D35c G1/G2 gtc, clonet).
            DxfDatabase db = DxfDatabase.Parse(Grid());
            DxfDBText grid = ByHandle<DxfDBText>(db, "64");
            var moved = (DxfDBText)grid.GetTransformedCopy(Move);
            var carried = (DxfDBText)grid.Clone();
            carried.TransformBy(Move);
            Near(new GeoPoint3(21.0, 17.0, 0.0), moved.Position);
            Near(new GeoPoint3(21.0, 17.0, 0.0), carried.Position);
            db.ModelSpace.AppendEntity(moved);
            db.ModelSpace.AppendEntity(carried);

            moved.RemoveContext(ScaleNamed(db, "1:1"));
            carried.RemoveContext(ScaleNamed(db, "1:1"));
            Assert.Equal(new GeoPoint3(40.0, 12.0, 0.0), moved.Position);
            Near(new GeoPoint3(50.0, 17.0, 0.0), carried.Position);
        }

        [Fact]
        public void TheTransformedCopiesOfAnAnnotativeMTextAndINSERT_MoveEveryContext()
        {
            // AutoCAD 2021's GetTransformedCopy of M1 and of the valve moved by (10, 5) moved every context (plugin D35c G2 gtc,
            // I2 gtc): M1's 1:2 from (60, 44) to (70, 49), the valve's 1:2 from (-50, 279.776) to (-40, 284.776).
            DxfDatabase db = DxfDatabase.Parse(Note(false));
            var words = (DxfMText)Words(db).GetTransformedCopy(Move);
            ShownAt(db, words, "1:1", "1:2");
            Near(new GeoPoint3(24.0, 49.0, 0.0), words.Location);
            db.ModelSpace.AppendEntity(words);
            words.RemoveContext(ScaleNamed(db, "1:1"));
            Near(new GeoPoint3(70.0, 49.0, 0.0), words.Location);

            db = DxfDatabase.Parse(Drawing(ValveContexts()));
            var valve = (DxfBlockReference)Insert(db).GetTransformedCopy(Move);
            ShownAt(db, valve, "1:1", "1:2");
            Near(new GeoPoint3(ShownX + 10.0, ValveY + 5.0, 0.0), valve.Position);
            Assert.Equal(new GeoVector3(2.0, 2.0, 2.0), valve.ScaleFactors);
            db.ModelSpace.AppendEntity(valve);
            ShownAt(db, valve, "1:1", "1:2");
            valve.RemoveContext(ScaleNamed(db, "1:1"));
            Near(new GeoPoint3(-40.0, ValveY + 5.0, 0.0), valve.Position);
            Near(new GeoVector3(3.0, 3.0, 3.0), valve.ScaleFactors);
        }

        [Fact]
        public void TheCopyOfAnAnnotativeINSERT_KeepsTheContextsOfItsATTRIBs()
        {
            // AutoCAD 2021's copy of an annotative INSERT, appended, had its attribute shown at 1:1 and 1:2 (plugin D35c K0
            // clone); the library's copy carries its ATTRIBs, and their contexts with them.
            DxfDatabase db = DxfDatabase.Parse(Drawing(ValveContexts(), SampleContexts()));
            var valve = (DxfBlockReference)Insert(db).Clone();
            ShownAt(db, valve.AttributeCollection[0], "1:1", "1:2");
            Assert.Equal(new GeoPoint3(SampleX, SampleY, 0.0), valve.AttributeCollection[0].Position);

            db.ModelSpace.AppendEntity(valve);
            DxfDatabase back = DxfDatabase.Parse(db.ToText());
            DxfAttributeReference again = ByHandle<DxfBlockReference>(back, valve.Handle).AttributeCollection[0];
            ShownAt(back, again, "1:1", "1:2");
            Assert.Equal(new GeoPoint3(SampleX, SampleY, 0.0), again.Position);
            ShownAt(back, Attribute(back), "1:1", "1:2");
        }

        [Fact]
        public void TheCopyOfAnAnnotativeText_AddedToAnotherDrawing_KeepsItsContexts_OnTheScalesOfTheirNames()
        {
            // Another drawing of the same scales takes the copy's contexts on them, as AutoCAD 2021's WblockCloneObjects keeps
            // them (check D35d): shown where it is read, at 1:1 and 1:2.
            var copy = (DxfDBText)ByHandle<DxfDBText>(DxfDatabase.Parse(Grid()), "64").Clone();
            DxfDatabase other = DxfDatabase.Parse(Drawing());
            other.ModelSpace.AppendEntity(copy);
            ShownAt(other, copy, "1:1", "1:2");
            Assert.Equal(new GeoPoint3(11.0, 12.0, 0.0), copy.Position);
            Assert.Equal(1.25, copy.Height);
            Near(15.0 * Math.PI / 180.0, copy.Rotation);
        }

        // ---- 2. setters that wrote the record alone ---------------------------------------------------------------------------

        [Fact]
        public void AnAnnotativeMTextHeldByAnotherPoint_MovesByTheWidthOfItsShownContext_AndWritesThatContext()
        {
            // AutoCAD 2021's SetAttachmentMovingLocation(MiddleLeft) of M1, shown at (14, 44) 30 wide held by its middle, moved it
            // 15 back along its direction to (1.01, 36.5) in its context of 1:1 and its record, 1:2 left (plugin D35c W1 sam4).
            DxfDatabase db = DxfDatabase.Parse(Note(true));
            Words(db).SetAttachmentMovingLocation(DxfAttachmentPoint.MiddleLeft);
            Near(new GeoPoint3(1.0096189432334199, 36.5, 0.0), Words(db).Location);
            Assert.Equal(DxfAttachmentPoint.MiddleLeft, Words(db).Attachment);

            string saved = db.ToText();
            Assert.Equal(4.0, ContextOf(saved, "E01")[70]);
            Near(1.0096189432334199, ContextOf(saved, "E01")[11]);
            Assert.Equal(1.0, ContextOf(saved, "E02")[70]);
            Assert.Equal(60.0, ContextOf(saved, "E02")[11]);
            Assert.Equal(4.0, OwnOf(saved, "73", "AcDbMText")[71]);
        }

        [Fact]
        public void AnAnnotativeMTextGivenANormal_ReadsAlongTheXAxisOfItsNewPlane_InItsShownContext()
        {
            // AutoCAD 2021 gave M1, turned 30 degrees, the normal (0, 0.6, 0.8): it read along (-1, 0, 0) at (14, 44), its context
            // of 1:2 left reading along (1, 0, 0) (plugin D35c W1, W2 normal).
            foreach (bool unitIsDefault in new[] { true, false })
            {
                DxfDatabase db = DxfDatabase.Parse(Note(unitIsDefault));
                Words(db).Normal = new GeoVector3(0.0, 0.6, 0.8);
                Near(new GeoVector3(-1.0, 0.0, 0.0), Words(db).Direction);
                Assert.Equal(new GeoPoint3(14.0, 44.0, 0.0), Words(db).Location);
                string saved = db.ToText();
                Assert.Equal(-1.0, ContextOf(saved, "E01")[10]);
                Assert.Equal(1.0, ContextOf(saved, "E02")[10]);
            }
        }

        [Fact]
        public void AnAnnotativeINSERTGivenANormal_KeepsItsPointsInItsPlane_AndWritesTheNormalAlone()
        {
            // AutoCAD 2021 gave the valve the normal (0, 0.6, 0.8): its contexts and its record's point were left as they were, and
            // it stood at (-113.883, -223.821, 167.866) (plugin D35c I1, I2 normal).
            DxfDatabase db = DxfDatabase.Parse(Drawing(ValveContexts()));
            Insert(db).Normal = new GeoVector3(0.0, 0.6, 0.8);
            Near(new GeoPoint3(-113.8834202935108, -223.82094201729763, 167.86570651297319), Insert(db).Position);
            string saved = db.ToText();
            Assert.Equal(OwnX, OwnOf(saved, "65", "AcDbBlockReference")[10]);
            Assert.Equal(ShownX, ContextOf(saved, "E01")[10]);
            Assert.Equal(-50.0, ContextOf(saved, "E02")[10]);
        }

        [Fact]
        public void AnAnnotativeINSERTGivenABlockTransform_WritesItsShownContextAlone_WhereAnotherIsTheDefault()
        {
            // AutoCAD 2021 gave the valve the placing to (50, 300) turned 0.5 and scaled by 3: its context of 1:1 so, its default
            // of 1:2 and its record left (plugin D35c I2 bt).
            DxfDatabase db = DxfDatabase.Parse(Drawing(ValveContexts()));
            var origin = new GeoPoint3(0.0, 0.0, 0.0);
            GeoTransform3 placing = GeoTransform3.Translation(new GeoVector3(50.0, 300.0, 0.0)) * GeoTransform3.RotationAxis(origin, new GeoVector3(0.0, 0.0, 1.0), 0.5) * GeoTransform3.Scaling(origin, 3.0);
            Insert(db).BlockTransform = placing;
            Near(placing.Transform(Insert(db).BlockTableRecord.Origin), Insert(db).Position);
            Near(0.5, Insert(db).Rotation);
            Near(new GeoVector3(3.0, 3.0, 3.0), Insert(db).ScaleFactors);

            for (int r = 0; r < 3; r++)
            {
                for (int c = 0; c < 4; c++)
                {
                    Near(placing[r, c], Insert(db).BlockTransform[r, c]);
                }
            }

            string saved = db.ToText();
            Near(28.64788975654116, ContextOf(saved, "E01")[50]);
            Assert.Equal(-50.0, ContextOf(saved, "E02")[10]);
            Assert.Equal(10.0, ContextOf(saved, "E02")[50]);
            Assert.Equal(3.0, ContextOf(saved, "E02")[41]);
            Assert.Equal(OwnX, OwnOf(saved, "65", "AcDbBlockReference")[10]);
        }

        // ---- 3. attributes of several lines -------------------------------------------------------------------------------

        [Fact]
        public void AnAttributeMadeOfSeveralLines_IsHeldByItsTopOneHeightAbove_ItsPointKept()
        {
            // AutoCAD 2021 made attributes of one line, left or centred on their baselines, turned or not, in a TrueType style or
            // not, of several: each MTEXT held by its top left or centre one text height above the text's point or second point,
            // the text's point kept (plugin D35c P0 multi, A1 to A5).
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfAttributeReference mark = Insert(db).AttributeCollection[0];
            mark.Rotation = Math.PI / 6.0;
            mark.IsMTextAttribute = true;
            DxfMText made = mark.MTextAttribute;
            Near(new GeoPoint3(50.0 - 1.25, 59.0 + 2.5 * Math.Cos(Math.PI / 6.0), 0.0), made.Location);
            Assert.Equal(DxfAttachmentPoint.TopLeft, made.Attachment);
            Assert.Equal(new GeoPoint3(50.0, 59.0, 0.0), mark.Position);
            Near(made.Location, mark.AlignmentPoint);

            db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            mark = Insert(db).AttributeCollection[0];
            mark.HorizontalMode = DxfTextHorizontalMode.TextCenter;
            mark.AlignmentPoint = new GeoPoint3(60.0, 59.0, 0.0);
            GeoPoint3 point = mark.Position;
            mark.IsMTextAttribute = true;
            Near(new GeoPoint3(60.0, 61.5, 0.0), mark.MTextAttribute.Location);
            Assert.Equal(DxfAttachmentPoint.TopCenter, mark.MTextAttribute.Attachment);
            Assert.Equal(point, mark.Position);
        }

        [Fact]
        public void AnAnnotativeAttributeMadeOfSeveralLines_IsHeldOneShownHeightAbove_InEveryContext()
        {
            // AutoCAD 2021 made T1, shown at (105, 96) 1.25 high at 1:1 where 1:2 was the default, of several lines: its MTEXT at
            // (105, 97.25) in every context (plugin D35b G2 multi). Here the attribute is 1.5 high at 1:1.
            DxfDatabase db = DxfDatabase.Parse(Drawing(SampleContexts()));
            Attribute(db).IsMTextAttribute = true;
            Near(new GeoPoint3(SampleX, SampleY + 1.5, 0.0), Attribute(db).MTextAttribute.Location);
            Assert.Equal(DxfAttachmentPoint.TopLeft, Attribute(db).MTextAttribute.Attachment);
            Near(new GeoPoint3(SampleX, SampleY, 0.0), Attribute(db).Position);

            string saved = db.ToText();
            Near(SampleY + 1.5, LinesOf(saved, "E03")[21]);
            Near(SampleY + 1.5, LinesOf(saved, "E04")[21]);
        }

        // ---- 4. the box of an MTEXT ---------------------------------------------------------------------------------------

        [Fact]
        public void AnMTextScaled_HasItsBoxScaled_InNoColumns()
        {
            // AutoCAD 2021 scaled an MTEXT 40 wide in a box 20 high by 2 about the origin: 80 wide in a box 40 high (plugin D35c
            // P0 scale).
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            Words(db).Height = 20.0;
            Words(db).TransformBy(GeoTransform3.Scaling(new GeoPoint3(0.0, 0.0, 0.0), 2.0));
            Assert.Equal(40.0, Words(db).Height);
            Assert.Equal(40.0, Words(DxfDatabase.Parse(db.ToText())).Height);
        }

        // ---- 5. an ATTRIB flagged annotative in a block that is not ---------------------------------------------------------

        [Fact]
        public void AnAttribFlaggedAnnotativeInABlockThatIsNot_IsAnnotativeWhereItHoldsAContext()
        {
            // AutoCAD 2021 read the ATTRIB it made of an annotative definition in a block that was not annotative as annotative,
            // shown at 1:1; the same flagged 1 holding no context as not annotative (plugin D35c P0, P2).
            DxfDatabase db = DxfDatabase.Parse(Drawing(SampleContexts()));
            Assert.Equal(DxfAnnotativeStates.False, Insert(db).Annotative);
            Assert.Equal(DxfAnnotativeStates.True, Attribute(db).Annotative);

            db = DxfDatabase.Parse(Drawing());
            Attribute(db).XData = AnnotativeXData;
            Assert.Equal(DxfAnnotativeStates.False, Attribute(db).Annotative);
            ShownAt(db, Attribute(db));
        }
    }
}
