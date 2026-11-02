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
    /// Annotative texts, attributes and definitions read and moved as AutoCAD 2021 reads and moves them (plugin D35): shown at
    /// their context of the scale 1:1, else at their default context, whatever CANNOSCALE says; their height that of the
    /// shown scale; a property set written into the shown context, and into the record where that context is the default; a
    /// transformation carrying every context. The drawing is ezdxf's of R2018, its INSERT 65 placed and its ATTRIB 67 put
    /// where AutoCAD's sample Mechanical - Metric has the INSERT of Valve - Metric and its attribute ATT_DVSUP.
    /// </summary>
    public class DxfAnnotativeD35Tests
    {
        private const string ScaleList = "  0\nDICTIONARY\n  5\n14\n330\nA\n100\nAcDbDictionary\n281\n1\n";
        private const string AttributeContext = "ACDB_MTEXTATTRIBUTEOBJECTCONTEXTDATA_CLASS";
        private const string TextContext = "ACDB_TEXTOBJECTCONTEXTDATA_CLASS";

        private static readonly DxfTypedValue[] AnnotativeXData =
        {
            new DxfTypedValue(1001, "AcadAnnotative"), new DxfTypedValue(1000, "AnnotativeData"), new DxfTypedValue(1002, "{"),
            new DxfTypedValue(1070, "1"), new DxfTypedValue(1070, "1"), new DxfTypedValue(1002, "}"),
        };

        // ATT_DVSUP of the sample: its own point, and its context of 1:1.
        private const double OwnX = 0.6779381070854527;
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

        /// <summary>The own groups of a context of a text, AutoCAD's order: the horizontal mode, the angle, the two points.</summary>
        private static string TextGroups(int mode, double degrees, double x, double y, double ax = 0.0, double ay = 0.0)
            => " 70\n" + mode.ToString(CultureInfo.InvariantCulture) + "\n 50\n" + N(degrees) + "\n 10\n" + N(x) + "\n 20\n" + N(y) + "\n 11\n" + N(ax) + "\n 21\n" + N(ay) + "\n";

        /// <summary>Those of an attribute's or a definition's, with the flag of no MTEXT after them.</summary>
        private static string AttributeGroups(int mode, double degrees, double x, double y, double ax = 0.0, double ay = 0.0)
            => TextGroups(mode, degrees, x, y, ax, ay) + "290\n0\n";

        private static string N(double value) => value.ToString("R", CultureInfo.InvariantCulture);

        /// <summary>
        /// The ezdxf drawing of R2018 with the scales 1:1 (F0, the unit scale), 1:2 (F1) and 1:4 (F2) in its list, its INSERT 65
        /// at (13.88342029351079, 279.776177521622) unscaled and unturned and its ATTRIB 67 at (0.678, 265.33), 3 high, as the
        /// sample has ATT_DVSUP; the contexts given to the records of the handles given, made annotative.
        /// </summary>
        private static string Drawing(params KeyValuePair<string, Context[]>[] contexts)
        {
            string text = DxfFileEzdxfDrawings.R2018;
            Assert.Contains(ScaleList, text, StringComparison.Ordinal);
            string scales = Scale("F0", "1:1", "1", "1", "1") + Scale("F1", "1:2", "1", "2", "0") + Scale("F2", "1:4", "1", "4", "0");
            int end = text.LastIndexOf("  0\nENDSEC\n", StringComparison.Ordinal);
            text = (text.Substring(0, end) + scales + text.Substring(end)).Replace(ScaleList, ScaleList + "  3\nA0\n350\nF0\n  3\nA1\n350\nF1\n  3\nA2\n350\nF2\n");
            text = Swap(text, "65", "\n 10\n50.0\n 20\n60.0\n 30\n0.0\n 41\n1.5\n 42\n-1.5\n 43\n2.0\n 50\n45.0\n", "\n 10\n13.88342029351079\n 20\n279.776177521622\n 30\n0.0\n");
            text = Swap(text, "67", "\n 10\n50.0\n 20\n59.0\n 30\n0.0\n 40\n2.5\n", "\n 10\n" + N(OwnX) + "\n 20\n" + N(SampleY) + "\n 30\n0.0\n 40\n3.0\n");

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

        /// <summary>The sample's two contexts of ATT_DVSUP: of 1:1 where AutoCAD shows it, the default unless said; of 1:2 at x -100, turned 30 degrees.</summary>
        private static string Valve(bool unitIsDefault = true)
            => Drawing(On(
                "67",
                new Context("E01", AttributeContext, "F0", unitIsDefault, AttributeGroups(0, 0.0, SampleX, SampleY)),
                new Context("E02", AttributeContext, "F1", !unitIsDefault, AttributeGroups(0, 30.0, -100.0, SampleY))));

        /// <summary>The numbers of a context's groups after its scale (340), by code, the first of each.</summary>
        private static Dictionary<int, double> ContextOf(string text, string handle) => NumbersOf(text, handle, 340);

        /// <summary>The numbers of a record's groups after its text's part begins (the marker AcDbText), by code, the first of each.</summary>
        private static Dictionary<int, double> OwnOf(string text, string handle) => NumbersOf(text, handle, 100);

        private static Dictionary<int, double> NumbersOf(string text, string handle, int from)
        {
            string[] lines = Records(text).Single(r => HandleOf(r) == handle).Split('\n');
            var numbers = new Dictionary<int, double>();
            bool after = false;

            for (int i = 2; i + 1 < lines.Length; i += 2)
            {
                int code = int.Parse(lines[i].Trim(), CultureInfo.InvariantCulture);
                after |= code == from && (from != 100 || lines[i + 1].Trim() == "AcDbText");

                if (after && code != from && !numbers.ContainsKey(code) && double.TryParse(lines[i + 1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
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

        private static void At(Dictionary<int, double> context, double x, double y)
        {
            Near(x, context[10]);
            Near(y, context[20]);
        }

        // ---- 1. reading -----------------------------------------------------------------------------------------------------

        [Fact]
        public void AnAttributeOfTheSample_IsReadAtItsContextOf1To1_NotAtItsOwnPoint_AsAutoCadShowsATT_DVSUP()
        {
            // AutoCAD 2021 read the sample's ATT_DVSUP at (-6.151, 265.33), its context of 1:1, which its own groups put at
            // (0.678, 265.33), and saved it so (plugin D35 a).
            DxfDatabase db = DxfDatabase.Parse(Drawing(On("67", new Context("E01", AttributeContext, "F0", true, AttributeGroups(0, 0.0, SampleX, SampleY)))));
            DxfAttributeReference attribute = Attribute(db);

            Assert.Equal(new GeoPoint3(SampleX, SampleY, 0.0), attribute.Position);
            Assert.Equal(new GeoPoint3(0.0, 0.0, 0.0), attribute.AlignmentPoint);
            Assert.Equal(3.0, attribute.Height);
            Assert.Equal(0.0, attribute.Rotation);
        }

        [Fact]
        public void TheContextOf1To1_IsShown_ThoughAnotherIsTheDefault_ItsHeightThatOf1To1()
        {
            // AutoCAD 2021 showed the context of 1:1 whether it was the default or not, first or second in the dictionary, and
            // whatever CANNOSCALE said: its record 3 high at the default 1:2 is 1.5 high at 1:1 (plugin D35 a M1-M6, b B1).
            DxfAttributeReference attribute = Attribute(DxfDatabase.Parse(Valve(unitIsDefault: false)));
            Assert.Equal(new GeoPoint3(SampleX, SampleY, 0.0), attribute.Position);
            Assert.Equal(1.5, attribute.Height);
            Assert.Equal(0.0, attribute.Rotation);

            string swapped = Drawing(On(
                "67",
                new Context("E01", AttributeContext, "F1", true, AttributeGroups(0, 30.0, -100.0, SampleY)),
                new Context("E02", AttributeContext, "F0", false, AttributeGroups(0, 0.0, SampleX, SampleY))));
            attribute = Attribute(DxfDatabase.Parse(swapped));
            Assert.Equal(new GeoPoint3(SampleX, SampleY, 0.0), attribute.Position);
            Assert.Equal(1.5, attribute.Height);
        }

        [Fact]
        public void AnAttributeOfNoContextOf1To1_IsReadAtItsDefault_ItsOwnHeight_TurnedAsItSays()
        {
            // AutoCAD 2021 read one of 1:2 alone, or of 1:2 (the default) and 1:4 with CANNOSCALE 1:4, at its context of 1:2:
            // (-100, 265.33), turned 30 degrees, 3 high (plugin D35 b B2, B3).
            string text = Drawing(On(
                "67",
                new Context("E01", AttributeContext, "F1", true, AttributeGroups(0, 30.0, -100.0, SampleY)),
                new Context("E02", AttributeContext, "F2", false, AttributeGroups(0, 45.0, -200.0, SampleY))));
            DxfAttributeReference attribute = Attribute(DxfDatabase.Parse(text));

            Assert.Equal(new GeoPoint3(-100.0, SampleY, 0.0), attribute.Position);
            Assert.Equal(3.0, attribute.Height);
            Near(Math.PI / 6.0, attribute.Rotation);
        }

        [Fact]
        public void TheShownContextGivesTheTurnAndTheAlignmentPoint_TheRecordItsElevation()
        {
            // AutoCAD 2021 read the turn of 30 degrees and the second point of the context, and the elevation 7 of the record,
            // which no context holds (plugin D35 b B4).
            string text = Drawing(On("67", new Context("E01", AttributeContext, "F0", true, AttributeGroups(0, 30.0, SampleX, SampleY, -1.0, 271.0))));
            text = Swap(text, "67", "\n 30\n0.0\n 40\n3.0\n", "\n 30\n7.0\n 40\n3.0\n");
            DxfAttributeReference attribute = Attribute(DxfDatabase.Parse(text));

            Assert.Equal(new GeoPoint3(SampleX, SampleY, 7.0), attribute.Position);
            Assert.Equal(new GeoPoint3(-1.0, 271.0, 7.0), attribute.AlignmentPoint);
            Near(Math.PI / 6.0, attribute.Rotation);
        }

        [Fact]
        public void ATextAndADefinition_AreReadAtTheirContextOf1To1_TheirHeightThatOf1To1()
        {
            // AutoCAD 2021 read the TEXT, the ATTDEF and the MTEXT it made annotative at their contexts of 1:1, half as high
            // where 1:2 was the default (plugin D35 b G1, G2); the TEXT 64 here is centred, and read by its second point too.
            string text = Drawing(
                On("64", new Context("E01", TextContext, "F0", false, TextGroups(1, 0.0, 10.9, 23.0, 13.0, 23.0)), new Context("E02", TextContext, "F1", true, TextGroups(1, 0.0, 47.9, 23.0, 50.0, 23.0))),
                On("37", new Context("E03", AttributeContext, "F0", true, AttributeGroups(0, 0.0, 15.0, 66.0))));
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfDBText grid = ByHandle<DxfDBText>(db, "64");
            DxfAttributeDefinition mark = ByHandle<DxfAttributeDefinition>(db, "37");

            Assert.Equal(new GeoPoint3(10.9, 23.0, 0.0), grid.Position);
            Assert.Equal(new GeoPoint3(13.0, 23.0, 0.0), grid.AlignmentPoint);
            Assert.Equal(1.25, grid.Height);
            Assert.Equal(0.0, grid.Rotation);
            Assert.Equal(new GeoPoint3(15.0, 66.0, 0.0), mark.Position);
            Assert.Equal(0.5, mark.Height);
        }

        // ---- 2. setting -----------------------------------------------------------------------------------------------------

        [Fact]
        public void AnAttributeMoved_IsWrittenIntoItsContextOf1To1_AndItsRecordWhereThatIsTheDefault()
        {
            // AutoCAD 2021 set ATT_DVSUP to (50, 300) in its context of 1:1 and in its record where 1:1 was the default, and in
            // that context alone where 1:2 was; the context of 1:2 kept (plugin D35 a M1/M3 attpos, b B1 attpos).
            string text = Valve();
            DxfDatabase db = DxfDatabase.Parse(text);
            Attribute(db).Position = new GeoPoint3(50.0, 300.0, 0.0);
            string saved = db.ToText();
            Assert.Equal(new GeoPoint3(50.0, 300.0, 0.0), Attribute(DxfDatabase.Parse(saved)).Position);
            At(ContextOf(saved, "E01"), 50.0, 300.0);
            Assert.Equal(ContextOf(text, "E02"), ContextOf(saved, "E02"));
            Assert.Equal(50.0, OwnOf(saved, "67")[10]);
            Assert.Equal(300.0, OwnOf(saved, "67")[20]);

            text = Valve(unitIsDefault: false);
            db = DxfDatabase.Parse(text);
            Attribute(db).Position = new GeoPoint3(50.0, 300.0, 0.0);
            saved = db.ToText();
            Assert.Equal(new GeoPoint3(50.0, 300.0, 0.0), Attribute(DxfDatabase.Parse(saved)).Position);
            At(ContextOf(saved, "E01"), 50.0, 300.0);
            Assert.Equal(ContextOf(text, "E02"), ContextOf(saved, "E02"));
            Assert.Equal(Records(text).Single(r => HandleOf(r) == "67"), Records(saved).Single(r => HandleOf(r) == "67"));
        }

        [Fact]
        public void AnAttributeGivenAHeightAndATurn_WritesItsTurnIntoTheShownContext_AndItsHeightAtTheDefaultScale()
        {
            // AutoCAD 2021 set ATT_DVSUP 5 high and turned 0.5 radians at 1:1: its context of 1:1 turned 28.648 degrees, its
            // record 10 high where 1:2 was the default, and its record turned only where 1:1 was (plugin D35 a M1/M3 atthr).
            string text = Valve(unitIsDefault: false);
            DxfDatabase db = DxfDatabase.Parse(text);
            Attribute(db).Height = 5.0;
            Attribute(db).Rotation = 0.5;
            string saved = db.ToText();
            DxfAttributeReference attribute = Attribute(DxfDatabase.Parse(saved));
            Assert.Equal(5.0, attribute.Height);
            Near(0.5, attribute.Rotation);
            Near(28.64788975654116, ContextOf(saved, "E01")[50]);
            Assert.Equal(30.0, ContextOf(saved, "E02")[50]);
            Assert.Equal(10.0, OwnOf(saved, "67")[40]);
            Assert.False(OwnOf(saved, "67").ContainsKey(50));

            db = DxfDatabase.Parse(Valve());
            Attribute(db).Height = 5.0;
            Attribute(db).Rotation = 0.5;
            saved = db.ToText();
            Assert.Equal(5.0, OwnOf(saved, "67")[40]);
            Near(28.64788975654116, OwnOf(saved, "67")[50]);
            Near(28.64788975654116, ContextOf(saved, "E01")[50]);
        }

        [Fact]
        public void AnINSERTMoved_CarriesEveryContextOfItsAttributes_ASecondPointOfALeftTextKeptAtNought()
        {
            // AutoCAD 2021 moved the valve by (10, 5): ATT_DVSUP's context of 1:1 to (3.849, 270.33), that of 1:2 to (-90,
            // 270.33), the second points of both, the attribute aligned left, kept at (0, 0) (plugin D35 a M1 move).
            string text = Valve();
            DxfDatabase db = DxfDatabase.Parse(text);
            Insert(db).TransformBy(GeoTransform3.Translation(new GeoVector3(10.0, 5.0, 0.0)));
            string saved = db.ToText();

            At(ContextOf(saved, "E01"), 3.848608305585913, 270.3285550728426);
            At(ContextOf(saved, "E02"), -90.0, 270.3285550728426);
            Assert.Equal(0.0, ContextOf(saved, "E02")[11]);
            Assert.Equal(0.0, ContextOf(saved, "E02")[21]);
            Near(new GeoPoint3(3.848608305585913, 270.3285550728426, 0.0), Attribute(DxfDatabase.Parse(saved)).Position);
        }

        [Fact]
        public void AnINSERTTurnedAndScaled_TurnsAndScalesEveryContextOfItsAttributes()
        {
            // AutoCAD 2021 turned the valve 30 degrees about its point: the context of 1:2 to (-77.519, 210.322) turned 60
            // degrees; and scaled it by 2: that context to (-213.883, 250.881), the record 6 high (plugin D35 a M1 turn, scale).
            DxfDatabase db = DxfDatabase.Parse(Valve());
            GeoPoint3 at = Insert(db).Position;
            Insert(db).TransformBy(GeoTransform3.RotationAxis(at, new GeoVector3(0.0, 0.0, 1.0), Math.PI / 6.0));
            string saved = db.ToText();
            At(ContextOf(saved, "E01"), 3.756575376312525, 257.2467634627303);
            Near(29.99999999999973, ContextOf(saved, "E01")[50]);
            At(ContextOf(saved, "E02"), -77.51870352614014, 210.3224593099373);
            Near(59.99999999999997, ContextOf(saved, "E02")[50]);
            Near(Math.PI / 6.0, Attribute(DxfDatabase.Parse(saved)).Rotation);

            db = DxfDatabase.Parse(Valve());
            Insert(db).TransformBy(GeoTransform3.Scaling(at, 2.0));
            saved = db.ToText();
            At(ContextOf(saved, "E01"), -26.18620368233897, 250.8809326240633);
            At(ContextOf(saved, "E02"), -213.8834202935108, 250.8809326240633);
            Assert.Equal(6.0, Attribute(DxfDatabase.Parse(saved)).Height);
        }

        [Fact]
        public void AnAttributeMovedAlone_CarriesItsContexts()
        {
            // AutoCAD 2021 moved ATT_DVSUP alone by (10, 5): both its contexts with it (plugin D35 b M1 attmove).
            DxfDatabase db = DxfDatabase.Parse(Valve());
            Attribute(db).TransformBy(GeoTransform3.Translation(new GeoVector3(10.0, 5.0, 0.0)));
            string saved = db.ToText();
            At(ContextOf(saved, "E01"), 3.848608305585913, 270.3285550728426);
            At(ContextOf(saved, "E02"), -90.0, 270.3285550728426);
        }

        [Fact]
        public void AnAttributeFilledFromItsDefinition_IsPlacedInItsShownContextAlone_ItsRecordWhereThatIsTheDefault()
        {
            // AutoCAD 2021 filled ATT_DVSUP from its definition at (5.278, 265.33) in its context of 1:1, the context of 1:2
            // kept, and in its record where 1:1 was the default; where 1:2 was, the record kept the default's point and took
            // the height at 1:2, twice the definition's (plugin D35 c M1/M3 fromblock). Here the definition MARK of BOLT, at
            // (0, -1) 0.5 high in the block based at (1, 2), lands at (12.883, 276.776).
            var placed = new GeoPoint3(12.88342029351079, 276.776177521622, 0.0);
            string text = Valve();
            DxfDatabase db = DxfDatabase.Parse(text);
            Attribute(db).SetAttributeFromBlock(Insert(db).BlockTransform);
            string saved = db.ToText();
            Near(placed, Attribute(DxfDatabase.Parse(saved)).Position);
            Assert.Equal(0.5, Attribute(DxfDatabase.Parse(saved)).Height);
            At(ContextOf(saved, "E01"), placed.X, placed.Y);
            Assert.Equal(ContextOf(text, "E02"), ContextOf(saved, "E02"));
            Near(placed.X, OwnOf(saved, "67")[10]);
            Near(placed.Y, OwnOf(saved, "67")[20]);

            text = Valve(unitIsDefault: false);
            db = DxfDatabase.Parse(text);
            Attribute(db).SetAttributeFromBlock(Insert(db).BlockTransform);
            saved = db.ToText();
            Near(placed, Attribute(DxfDatabase.Parse(saved)).Position);
            Assert.Equal(0.5, Attribute(DxfDatabase.Parse(saved)).Height);
            At(ContextOf(saved, "E01"), placed.X, placed.Y);
            Assert.Equal(ContextOf(text, "E02"), ContextOf(saved, "E02"));
            Assert.Equal(OwnX, OwnOf(saved, "67")[10]);
            Assert.Equal(SampleY, OwnOf(saved, "67")[20]);
            Assert.Equal(1.0, OwnOf(saved, "67")[40]);
        }

        // ---- 3. texts and definitions ---------------------------------------------------------------------------------------

        [Fact]
        public void ATextSetAndMoved_WritesItsShownContext_AndCarriesEveryContext_ItsSecondPointWithIt()
        {
            // AutoCAD 2021 set the second point of a centred TEXT at 1:1 in that context alone where 1:2 was the default, and
            // moved both contexts' second points by (10, 5) (plugin D35 b G2 tset, G1 tmove).
            string text = Drawing(On("64", new Context("E01", TextContext, "F0", false, TextGroups(1, 0.0, 10.9, 23.0, 13.0, 23.0)), new Context("E02", TextContext, "F1", true, TextGroups(1, 0.0, 47.9, 23.0, 50.0, 23.0))));
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfDBText grid = ByHandle<DxfDBText>(db, "64");
            grid.AlignmentPoint = new GeoPoint3(12.0, 22.0, 0.0);
            string saved = db.ToText();
            Assert.Equal(12.0, ContextOf(saved, "E01")[11]);
            Assert.Equal(22.0, ContextOf(saved, "E01")[21]);
            Assert.Equal(ContextOf(text, "E02"), ContextOf(saved, "E02"));
            Assert.Equal(Records(text).Single(r => HandleOf(r) == "64"), Records(saved).Single(r => HandleOf(r) == "64"));
            Assert.Equal(new GeoPoint3(12.0, 22.0, 0.0), ByHandle<DxfDBText>(DxfDatabase.Parse(saved), "64").AlignmentPoint);

            grid.TransformBy(GeoTransform3.Translation(new GeoVector3(10.0, 5.0, 0.0)));
            saved = db.ToText();
            Assert.Equal(22.0, ContextOf(saved, "E01")[11]);
            Assert.Equal(27.0, ContextOf(saved, "E01")[21]);
            Assert.Equal(60.0, ContextOf(saved, "E02")[11]);
            Assert.Equal(28.0, ContextOf(saved, "E02")[21]);
        }

        [Fact]
        public void ATextOfNoContext_IsReadAndSetFromItsOwnGroups_AsBefore()
        {
            string text = Drawing();
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfDBText grid = ByHandle<DxfDBText>(db, "64");
            Assert.Equal(new GeoPoint3(7.0, 8.0, 0.0), grid.Position);
            Assert.Equal(2.5, grid.Height);
            Assert.Equal(new GeoPoint3(OwnX, SampleY, 0.0), Attribute(db).Position);
            Assert.Equal(3.0, Attribute(db).Height);
            grid.Position = new GeoPoint3(1.0, 2.0, 0.0);
            Insert(db).TransformBy(GeoTransform3.Translation(new GeoVector3(10.0, 5.0, 0.0)));
            Assert.Equal(new GeoPoint3(1.0, 2.0, 0.0), ByHandle<DxfDBText>(DxfDatabase.Parse(db.ToText()), "64").Position);
            Assert.Equal(new GeoPoint3(OwnX + 10.0, SampleY + 5.0, 0.0), Attribute(DxfDatabase.Parse(db.ToText())).Position);
        }

        [Fact]
        public void SettingWhatAnAnnotativeAttributeShows_ChangesNothing()
        {
            string text = Valve(unitIsDefault: false);
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfAttributeReference attribute = Attribute(db);
            attribute.Position = attribute.Position;
            attribute.AlignmentPoint = attribute.AlignmentPoint;
            attribute.Height = attribute.Height;
            attribute.Rotation = attribute.Rotation;
            attribute.TransformBy(GeoTransform3.Identity);
            Assert.Equal(text, db.ToText());
        }
    }
}
