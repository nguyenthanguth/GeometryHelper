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
    /// What <see cref="DxfNormalD35dTests"/> left, as AutoCAD 2021 does it (plugin D35e): an entity handed over to one of
    /// another kind keeping its extension dictionary, whose contexts AutoCAD keeps unconverted and then crashes reading; the
    /// names of the entries of the scales added to a drawing; an annotative INSERT of no context. The drawing is ezdxf's of
    /// R2018 with the scales 1:1 (F0), 1:2 (F1) and 1:4 (F2) under AutoCAD's names A0, A1 and A2.
    /// </summary>
    public class DxfAnnotativeD35eTests
    {
        private const string ScaleList = "  0\nDICTIONARY\n  5\n14\n330\nA\n100\nAcDbDictionary\n281\n1\n";
        private const string TextContext = "ACDB_TEXTOBJECTCONTEXTDATA_CLASS";
        private const string AttributeContext = "ACDB_MTEXTATTRIBUTEOBJECTCONTEXTDATA_CLASS";
        private const string MTextContext = "ACDB_MTEXTOBJECTCONTEXTDATA_CLASS";
        private const string InsertContext = "ACDB_BLKREFOBJECTCONTEXTDATA_CLASS";
        private const string HatchContext = "ACDB_HATCHSCALECONTEXTDATA_CLASS";

        private static readonly DxfTypedValue[] AnnotativeXData =
        {
            new DxfTypedValue(1001, "AcadAnnotative"), new DxfTypedValue(1000, "AnnotativeData"), new DxfTypedValue(1002, "{"),
            new DxfTypedValue(1070, "1"), new DxfTypedValue(1070, "1"), new DxfTypedValue(1002, "}"),
        };

        // The groups after the scale of each kind of context, as AutoCAD 2021 wrote them (plugin D35e r1).
        private static readonly Dictionary<string, string> Groups = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [TextContext] = " 70\n0\n 50\n0.0\n 10\n11.0\n 20\n12.0\n 11\n0.0\n 21\n0.0\n",
            [AttributeContext] = " 70\n0\n 50\n0.0\n 10\n15.0\n 20\n66.0\n 11\n0.0\n 21\n0.0\n290\n0\n",
            [MTextContext] = " 70\n1\n 10\n1.0\n 20\n0.0\n 30\n0.0\n 11\n14.0\n 21\n44.0\n 31\n0.0\n 40\n0.0\n 41\n0.0\n 42\n0.0\n 43\n0.0\n 71\n0\n",
            [InsertContext] = " 50\n0.0\n 10\n100.0\n 20\n100.0\n 30\n0.0\n 41\n1.0\n 42\n1.0\n 43\n1.0\n",
            [HatchContext] = string.Empty,
        };

        private static DxfBlockReference Insert(DxfDatabase db) => ByHandle<DxfBlockReference>(db, "65");

        private static DxfMText Words(DxfDatabase db) => ByHandle<DxfMText>(db, "73");

        private static DxfDBText Grid(DxfDatabase db) => ByHandle<DxfDBText>(db, "64");

        private static DxfObjectContextCollection Scales(DxfDatabase db) => db.ObjectContextManager.GetContextCollection("ACDB_ANNOTATIONSCALES");

        private static string[] ShownAt(DxfDatabase db, DxfDBObject o)
            => Scales(db).Where(s => o.HasContext(s)).Select(s => s.Name).OrderBy(s => s, StringComparer.Ordinal).ToArray();

        private static string Scale(string handle, string name, string paper, string drawing, string unit)
            => "  0\nSCALE\n  5\n" + handle + "\n102\n{ACAD_REACTORS\n330\n14\n102\n}\n330\n14\n100\nAcDbScale\n 70\n0\n300\n" + name + "\n140\n" + paper + "\n141\n" + drawing + "\n290\n" + unit + "\n";

        /// <summary>The ezdxf drawing with the scales 1:1 (F0), 1:2 (F1) and 1:4 (F2), named A0, A1 and A2 as AutoCAD names its own.</summary>
        private static DxfDatabase Scaled()
        {
            string text = DxfFileEzdxfDrawings.R2018;
            Assert.Contains(ScaleList, text, StringComparison.Ordinal);
            string scales = Scale("F0", "1:1", "1", "1", "1") + Scale("F1", "1:2", "1", "2", "0") + Scale("F2", "1:4", "1", "4", "0");
            int end = text.LastIndexOf("  0\nENDSEC\n", StringComparison.Ordinal);
            text = (text.Substring(0, end) + scales + text.Substring(end)).Replace(ScaleList, ScaleList + "  3\nA0\n350\nF0\n  3\nA1\n350\nF1\n  3\nA2\n350\nF2\n");
            return DxfDatabase.Parse(text);
        }

        /// <summary>
        /// The drawing read back with each entity named given two contexts of the kind named, at 1:1 and at 1:2, its default,
        /// under ACDB_ANNOTATIONSCALES of AcDbContextDataManager of its extension dictionary, as AutoCAD 2021 writes them; the
        /// entity made annotative, an INSERT by its block.
        /// </summary>
        private static DxfDatabase WithContexts(DxfDatabase db, params string[] handleAndKind)
        {
            var owners = new List<KeyValuePair<string, string>>();

            for (int k = 0; k < handleAndKind.Length; k += 2)
            {
                DxfEntity entity = ByHandle<DxfEntity>(db, handleAndKind[k]);

                if (entity is DxfBlockReference insert)
                {
                    db.BlockTable[insert.Name].XData = AnnotativeXData;
                }
                else
                {
                    entity.XData = AnnotativeXData;
                }

                entity.CreateExtensionDictionary();
                var manager = new DxfDBDictionary { TreatElementsAsHard = true };
                entity.ExtensionDictionary.SetAt("AcDbContextDataManager", manager);
                var dictionary = new DxfDBDictionary { TreatElementsAsHard = true };
                manager.SetAt("ACDB_ANNOTATIONSCALES", dictionary);
                owners.Add(new KeyValuePair<string, string>(dictionary.Handle, handleAndKind[k + 1]));
            }

            string text = db.ToText();
            int next = 0xE01;

            foreach (KeyValuePair<string, string> owner in owners)
            {
                var records = new System.Text.StringBuilder();
                var entries = new System.Text.StringBuilder();
                string[] scales = { "F0", "F1" };

                for (int i = 0; i < 2; i++)
                {
                    string handle = (next++).ToString("X", CultureInfo.InvariantCulture);
                    records.Append("  0\n" + owner.Value + "\n  5\n" + handle + "\n102\n{ACAD_REACTORS\n330\n" + owner.Key + "\n102\n}\n330\n" + owner.Key
                        + "\n100\nAcDbObjectContextData\n 70\n4\n290\n" + (i == 1 ? "1" : "0") + "\n100\nAcDbAnnotScaleObjectContextData\n340\n" + scales[i] + "\n" + Groups[owner.Value]);
                    entries.Append("  3\n*A" + (i + 1).ToString(CultureInfo.InvariantCulture) + "\n350\n" + handle + "\n");
                }

                int end = text.LastIndexOf("  0\nENDSEC\n", StringComparison.Ordinal);
                text = text.Substring(0, end) + records + text.Substring(end);
                string record = Records(text).Single(r => HandleOf(r) == owner.Key);
                text = text.Replace(record, record + entries);
            }

            return DxfDatabase.Parse(text);
        }

        /// <summary>The types of the records under ACDB_ANNOTATIONSCALES of an entity's extension dictionary, in their order.</summary>
        private static string[] ContextTypes(DxfDatabase db, DxfEntity entity)
        {
            DxfDBDictionary extension = entity.ExtensionDictionary;

            if (extension == null || !extension.Contains("AcDbContextDataManager"))
            {
                return new string[0];
            }

            var scales = (DxfDBDictionary)((DxfDBDictionary)extension.GetAt("AcDbContextDataManager")).GetAt("ACDB_ANNOTATIONSCALES");
            List<string> records = Records(db.ToText());
            return scales.Select(e => records.Single(r => HandleOf(r) == e.Value.Handle).Split('\n')[1]).ToArray();
        }

        /// <summary>The entries of the drawing's ACAD_SCALELIST, each as its name and that of its scale, in their order.</summary>
        private static string[] ScaleKeys(DxfDatabase db)
        {
            var list = (DxfDBDictionary)db.NamedObjectsDictionary.GetAt("ACAD_SCALELIST");
            Dictionary<string, string> names = Scales(db).Cast<DxfAnnotationScale>().ToDictionary(s => s.Handle, s => s.Name, StringComparer.OrdinalIgnoreCase);
            return list.Select(e => e.Key + "=" + names[e.Value.Handle]).ToArray();
        }

        private static void AddScale(DxfDatabase db, string name, double drawing)
            => Scales(db).AddContext(new DxfAnnotationScale { Name = name, PaperUnits = 1.0, DrawingUnits = drawing });

        // ---- 1. an entity handed over to one of another kind, its extension dictionary kept ----------------------------------

        [Fact]
        public void ATextHandedToAnMText_KeepingItsDictionary_IsRefused_AndNothingIsWritten()
        {
            // AutoCAD 2021's TEXT handed over to an MTEXT, its extension dictionary kept, left the MTEXT the TEXT's records
            // (ACDB_TEXTOBJECTCONTEXTDATA_CLASS); reading the drawing it saved, AutoCAD crashed (FATAL access violation, plugin
            // D35d r4 A5; D35e r1 the same in memory).
            DxfDatabase db = WithContexts(Scaled(), "64", TextContext);
            string before = db.ToText();
            var copy = (DxfMText)Words(db).Clone();

            var refused = Assert.Throws<ArgumentException>(() => Grid(db).HandOverTo(copy, true, true));
            Assert.Equal("newPointer", refused.ParamName);
            Assert.Contains(TextContext, refused.Message, StringComparison.Ordinal);
            Assert.Equal(before, db.ToText());
            Assert.False(copy.IsResident);
            Assert.Equal(new[] { "1:1", "1:2" }, ShownAt(db, Grid(db)));
        }

        [Fact]
        public void AnMTextHandedToAText_KeepingItsDictionary_IsRefused()
        {
            // AutoCAD 2021 crashed reading its own drawing of a TEXT holding an MTEXT's records (exception 0xc0000409 in acdb24,
            // plugin D35e r1 H0-7B_ho.78.1.1).
            DxfDatabase db = WithContexts(Scaled(), "73", MTextContext);
            string before = db.ToText();

            Assert.Throws<ArgumentException>(() => Words(db).HandOverTo(Grid(db).Clone(), true, true));
            Assert.Equal(before, db.ToText());
            Assert.Equal(new[] { MTextContext, MTextContext }, ContextTypes(db, Words(db)));
        }

        [Fact]
        public void AnInsertAndATextHandedToEachOther_KeepingTheirDictionaries_AreRefused()
        {
            // AutoCAD 2021 crashed reading its own drawings of a TEXT holding an INSERT's record (0xc0000409) and of an INSERT
            // holding a TEXT's (FATAL access violation), plugin D35e r1 H0-83_ho.78.1.1 and H0-78_ho.83.1.1.
            DxfDatabase db = WithContexts(Scaled(), "65", InsertContext, "64", TextContext);
            string before = db.ToText();

            Assert.Throws<ArgumentException>(() => Insert(db).HandOverTo(Grid(db).Clone(), true, true));
            Assert.Throws<ArgumentException>(() => Grid(db).HandOverTo(Insert(db).Clone(), true, true));
            Assert.Equal(before, db.ToText());
        }

        [Fact]
        public void AnotherAnnotativeKindKeepingItsDictionary_IsHandedOnlyToOneOfItsOwnClass()
        {
            // Of the other annotative kinds AutoCAD's records are not checked: one of them keeps its dictionary only for an entity
            // of its own class, the records written for that class.
            DxfDatabase db = WithContexts(Scaled(), "74", HatchContext);
            DxfEntity hatch = ByHandle<DxfEntity>(db, "74");
            string before = db.ToText();

            Assert.Throws<ArgumentException>(() => hatch.HandOverTo(ByHandle<DxfEntity>(db, "89").Clone(), true, true));
            Assert.Equal(before, db.ToText());

            DxfEntity copy = hatch.Clone();
            hatch.HandOverTo(copy, true, true);
            Assert.Equal(new[] { HatchContext, HatchContext }, ContextTypes(db, copy));
        }

        [Fact]
        public void ATextAndADefinitionHandedToEachOther_KeepTheirContexts_AsAutoCADReadThem()
        {
            // AutoCAD 2021 read its own drawings of an ATTDEF holding a TEXT's records and of a TEXT holding an ATTDEF's, each
            // shown at 1:1 and 1:2 (plugin D35e r1 H0-78_ho.7C.1.1 and H0-7C_ho.78.1.1): the records of the two derive from one
            // another, and are kept.
            DxfDatabase db = WithContexts(Scaled(), "64", TextContext);
            string handle = Grid(db).Handle;
            var definition = new DxfAttributeDefinition { Tag = "MD", TextString = "VMD", Position = new GeoPoint3(15.0, 66.0, 0.0), Height = 2.5 };
            Grid(db).HandOverTo(definition, true, true);
            Assert.Equal(new[] { "1:1", "1:2" }, ShownAt(db, definition));

            DxfDatabase back = DxfDatabase.Parse(db.ToText());
            var again = ByHandle<DxfAttributeDefinition>(back, handle);
            Assert.Equal(new[] { TextContext, TextContext }, ContextTypes(back, again));
            Assert.Equal(new[] { "1:1", "1:2" }, ShownAt(back, again));

            db = Scaled();
            var made = new DxfAttributeDefinition { Tag = "MD", TextString = "VMD", Position = new GeoPoint3(15.0, 66.0, 0.0), Height = 2.5 };
            db.ModelSpace.AppendEntity(made);
            db = WithContexts(db, made.Handle, AttributeContext);
            var held = ByHandle<DxfAttributeDefinition>(db, made.Handle);
            var text = (DxfDBText)Grid(db).Clone();
            held.HandOverTo(text, true, true);
            Assert.Equal(new[] { AttributeContext, AttributeContext }, ContextTypes(db, text));
            Assert.Equal(new[] { "1:1", "1:2" }, ShownAt(db, text));
        }

        [Fact]
        public void ATextHandedToALine_KeepingItsDictionary_LeavesTheLineItsRecords_AsAutoCADReadIt()
        {
            // AutoCAD 2021 read its own drawing of a LINE holding a TEXT's records, and kept them (plugin D35e r1
            // H0-78_ho.LINE.1.1): a LINE is never annotative, and they are not read for it.
            DxfDatabase db = WithContexts(Scaled(), "64", TextContext);
            var line = new DxfLine(new GeoPoint3(0.0, 0.0, 0.0), new GeoPoint3(20.0, 0.0, 0.0));
            Grid(db).HandOverTo(line, true, true);
            Assert.Equal(new[] { TextContext, TextContext }, ContextTypes(db, line));
            DxfDatabase back = DxfDatabase.Parse(db.ToText());
            Assert.Equal(new[] { TextContext, TextContext }, ContextTypes(back, ByHandle<DxfLine>(back, "64")));
        }

        [Fact]
        public void EntitiesHandedToOnesOfAnotherKind_KeepingNoDictionary_AreNotRefused()
        {
            // Not kept, the dictionary goes with its records, and the new entity writes its own (check D35d).
            DxfDatabase db = WithContexts(Scaled(), "64", TextContext, "73", MTextContext, "65", InsertContext);
            var mtext = (DxfMText)Words(db).Clone();
            Grid(db).HandOverTo(mtext, true, false);
            Assert.Equal(new[] { MTextContext, MTextContext }, ContextTypes(db, mtext));

            db = WithContexts(Scaled(), "64", TextContext, "65", InsertContext);
            var text = (DxfDBText)Grid(db).Clone();
            Insert(db).HandOverTo(text, true, false);
            Assert.Equal(new[] { TextContext, TextContext }, ContextTypes(db, text));
            DxfDatabase back = DxfDatabase.Parse(db.ToText());
            Assert.Equal(new[] { "1:1", "1:2" }, ShownAt(back, ByHandle<DxfDBText>(back, "65")));
        }

        // ---- 2. the names of the entries of the scales added ------------------------------------------------------------------

        [Fact]
        public void AScaleAddedToAnEmptyList_IsNamedStarA1_AsAutoCADNamesIt()
        {
            // AutoCAD 2021's AddContext named its scales' entries *A1, *A2 and *A3 (plugin D35e r1 S0).
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            AddScale(db, "1:50", 50.0);
            Assert.Equal(new[] { "*A1=1:50" }, ScaleKeys(db));
            Assert.Equal(new[] { "*A1=1:50" }, ScaleKeys(DxfDatabase.Parse(db.ToText())));
        }

        [Fact]
        public void ScalesAddedBesideAutoCADsOwn_AreNamedStarA1On_AndARemovedOnesNumberIsNotTakenAgain()
        {
            // AutoCAD 2021 added Z1, Z2 and Z3 as *A1, *A2 and *A3 beside its own A0 to B6, took Z2 out, and added Z4 as *A4
            // (plugin D35e r1 S0). The scales of AutoCAD's names are read as they were.
            DxfDatabase db = Scaled();
            AddScale(db, "Z1", 4.0);
            AddScale(db, "Z2", 6.0);
            AddScale(db, "Z3", 8.0);
            Assert.Equal(new[] { "A0=1:1", "A1=1:2", "A2=1:4", "*A1=Z1", "*A2=Z2", "*A3=Z3" }, ScaleKeys(db));

            Scales(db).RemoveContext("Z2");
            AddScale(db, "Z4", 9.0);
            Assert.Equal(new[] { "A0=1:1", "A1=1:2", "A2=1:4", "*A1=Z1", "*A3=Z3", "*A4=Z4" }, ScaleKeys(db));

            DxfDatabase back = DxfDatabase.Parse(db.ToText());
            Assert.Equal(ScaleKeys(db), ScaleKeys(back));
            Assert.Equal(new[] { "1:1", "1:2", "1:4", "Z1", "Z3", "Z4" }, Scales(back).Select(s => s.Name).ToArray());
        }

        [Fact]
        public void CanRemoveContext_IsTrueForAScaleInNoUse_AndFalseForTheCurrentOneAndANameTheListHasNot()
        {
            // AutoCAD 2021's -SCALELISTEDIT deletes a scale it added and answers "Scale 1:1 is referenced so it cannot be
            // deleted." for the current one.
            DxfDatabase db = Scaled();
            AddScale(db, "Z1", 4.0);

            Assert.True(Scales(db).CanRemoveContext("Z1"));
            Assert.True(Scales(db).CanRemoveContext("z1"));
            Assert.False(Scales(db).CanRemoveContext("NOSUCH"));
            Assert.False(Scales(db).CanRemoveContext(db.Cannoscale));
            Assert.Throws<ArgumentNullException>(() => Scales(db).CanRemoveContext(null));

            Scales(db).RemoveContext("Z1");
            Assert.False(Scales(db).CanRemoveContext("Z1"));
        }

        [Fact]
        public void TheHighestScaleTakenOut_ItsNumberIsNotGivenAgainWhileTheDrawingIsOpen()
        {
            // AutoCAD 2021 added Z1, Z2 and Z3 as *A1, *A2 and *A3, took Z3 out and added Z4 as *A4; took Z4 and Z1 out and added
            // Z5 as *A5 (plugin D35e r2 S1).
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            AddScale(db, "Z1", 4.0);
            AddScale(db, "Z2", 6.0);
            AddScale(db, "Z3", 8.0);
            Scales(db).RemoveContext("Z3");
            AddScale(db, "Z4", 9.0);
            Assert.Equal(new[] { "*A1=Z1", "*A2=Z2", "*A4=Z4" }, ScaleKeys(db));

            Scales(db).RemoveContext("Z4");
            Scales(db).RemoveContext("Z1");
            AddScale(db, "Z5", 11.0);
            Assert.Equal(new[] { "*A2=Z2", "*A5=Z5" }, ScaleKeys(db));
        }

        // ---- 3. an annotative INSERT of no context ----------------------------------------------------------------------------

        [Fact]
        public void AnInsertOfAnAnnotativeBlockAppended_IsWrittenWithNoExtensionDictionary_AndReadAtOneToOne()
        {
            // AutoCAD 2021 wrote an INSERT of an annotative block it appended with no extension dictionary, as before it was
            // asked anything; it makes one, of a context of 1:1, only when it reads the drawing (plugin D35e r2 B0). The library
            // writes none, and reads the INSERT shown at 1:1.
            DxfDatabase db = Scaled();
            db.BlockTable[Insert(db).Name].XData = AnnotativeXData;
            var insert = new DxfBlockReference(new GeoPoint3(100.0, 100.0, 0.0), db.BlockTable[Insert(db).Name]);
            db.ModelSpace.AppendEntity(insert);
            Assert.Equal(DxfAnnotativeStates.True, insert.Annotative);
            Assert.Null(insert.ExtensionDictionary);

            DxfDatabase back = DxfDatabase.Parse(db.ToText());
            var again = ByHandle<DxfBlockReference>(back, insert.Handle);
            Assert.Null(again.ExtensionDictionary);
            Assert.Equal(new[] { "1:1" }, ShownAt(back, again));
        }
    }
}
