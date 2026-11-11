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
    /// What <see cref="DxfAnnotativeD35cTests"/> left, as AutoCAD 2021 does it (plugin D35d): a normal set on a TEXT, an
    /// attribute, an MTEXT, an INSERT or a SOLID of no contexts; the width factor of an attribute made of several lines; an
    /// annotative copy added to another drawing, its scales found there by name; an entity handed over to a copy of contexts.
    /// The drawing is ezdxf's of R2018, given the contexts of <see cref="DxfAnnotativeD35cTests"/> where a test needs them.
    /// </summary>
    public class DxfNormalD35dTests
    {
        private const string ScaleList = "  0\nDICTIONARY\n  5\n14\n330\nA\n100\nAcDbDictionary\n281\n1\n";
        private const string TextContext = "ACDB_TEXTOBJECTCONTEXTDATA_CLASS";
        private const string AttributeContext = "ACDB_MTEXTATTRIBUTEOBJECTCONTEXTDATA_CLASS";

        private static readonly DxfTypedValue[] AnnotativeXData =
        {
            new DxfTypedValue(1001, "AcadAnnotative"),
            new DxfTypedValue(1000, "AnnotativeData"),
            new DxfTypedValue(1002, "{"),
            new DxfTypedValue(1070, "1"),
            new DxfTypedValue(1070, "1"),
            new DxfTypedValue(1002, "}"),
        };

        // The normal AutoCAD's checks set: its plane's x axis is (-1, 0, 0), its y axis (0, -0.8, 0.6).
        private static readonly GeoVector3 Tilted = new GeoVector3(0.0, 0.6, 0.8);

        private static string N(double value) => value.ToString("R", CultureInfo.InvariantCulture);

        /// <summary>A point of the plane z = 0 where it stands once its plane is that of <see cref="Tilted"/>, its coordinates kept.</summary>
        private static GeoPoint3 OnTilted(GeoPoint3 p) =>
            new GeoPoint3(-p.X, -0.8 * p.Y + 0.6 * p.Z, 0.6 * p.Y + 0.8 * p.Z);

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

        private static void Near(GeoVector3 expected, GeoVector3 actual)
        {
            Near(expected.X, actual.X);
            Near(expected.Y, actual.Y);
            Near(expected.Z, actual.Z);
        }

        private static DxfDatabase Ezdxf() => DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);

        private static DxfBlockReference Insert(DxfDatabase db) => ByHandle<DxfBlockReference>(db, "65");

        private static DxfAttributeReference Attribute(DxfDatabase db) => Insert(db).AttributeCollection[0];

        private static DxfMText Words(DxfDatabase db) => ByHandle<DxfMText>(db, "73");

        private static DxfDBText Grid(DxfDatabase db) => ByHandle<DxfDBText>(db, "64");

        private static DxfObjectContextCollection Scales(DxfDatabase db) =>
            db.ObjectContextManager.GetContextCollection("ACDB_ANNOTATIONSCALES");

        private static string[] ShownAt(DxfDatabase db, DxfDBObject o) =>
            Scales(db)
                .Where(s => o.HasContext(s))
                .Select(s => s.Name)
                .OrderBy(s => s, StringComparer.Ordinal)
                .ToArray();

        private static string[] ScaleRows(DxfDatabase db) =>
            Scales(db)
                .Cast<DxfAnnotationScale>()
                .Select(s => s.Name + "=" + N(s.PaperUnits) + "/" + N(s.DrawingUnits))
                .OrderBy(s => s, StringComparer.Ordinal)
                .ToArray();

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

        /// <summary>Those of a TEXT's context: the horizontal mode, the angle, the two points.</summary>
        private static string TextGroups(double degrees, double x, double y) =>
            " 70\n0\n 50\n" + N(degrees) + "\n 10\n" + N(x) + "\n 20\n" + N(y) + "\n 11\n0.0\n 21\n0.0\n";

        /// <summary>Those of an attribute's: the horizontal mode, the angle, the two points, the flag of no MTEXT after them.</summary>
        private static string AttributeGroups(double x, double y) =>
            " 70\n0\n 50\n0.0\n 10\n" + N(x) + "\n 20\n" + N(y) + "\n 11\n0.0\n 21\n0.0\n290\n0\n";

        /// <summary>
        /// The ezdxf drawing with the scales 1:1 (F0), 1:2 (F1) and 1:4 (F2), its TEXT 64 shown at (11, 12) turned 15 degrees
        /// at 1:1 beside its default of 1:2 at (40, 12) (plugin D35b G2), and, where asked, its ATTRIB 67 shown at (-6.15,
        /// 265.33) at 1:1 beside its default of 1:2 at (-100, 265.33) and the block of its INSERT 65 annotative.
        /// </summary>
        private static string Drawing(bool attribute)
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
            DxfDatabase db = DxfDatabase.Parse(text);
            var held = new List<KeyValuePair<DxfEntity, string>>
            {
                new KeyValuePair<DxfEntity, string>(Grid(db), TextContext),
            };

            if (attribute)
            {
                held.Add(new KeyValuePair<DxfEntity, string>(Attribute(db), AttributeContext));
                db.BlockTable[Insert(db).Name].XData = AnnotativeXData;
            }

            var owners = new List<string>();

            foreach (KeyValuePair<DxfEntity, string> entity in held)
            {
                entity.Key.XData = AnnotativeXData;
                entity.Key.CreateExtensionDictionary();
                var manager = new DxfDBDictionary { TreatElementsAsHard = true };
                entity.Key.ExtensionDictionary.SetAt("AcDbContextDataManager", manager);
                var dictionary = new DxfDBDictionary { TreatElementsAsHard = true };
                manager.SetAt("ACDB_ANNOTATIONSCALES", dictionary);
                owners.Add(dictionary.Handle);
            }

            text = db.ToText();
            string[][] contexts =
            {
                new[] { "E01", "F0", "0", TextGroups(15.0, 11.0, 12.0), "E02", "F1", "1", TextGroups(0.0, 40.0, 12.0) },
                new[]
                {
                    "E03",
                    "F0",
                    "0",
                    AttributeGroups(-6.151391694414086, 265.3285550728426),
                    "E04",
                    "F1",
                    "1",
                    AttributeGroups(-100.0, 265.3285550728426),
                },
            };

            for (int k = 0; k < owners.Count; k++)
            {
                string owner = owners[k];
                var records = new System.Text.StringBuilder();
                var entries = new System.Text.StringBuilder();

                for (int i = 0; i < 2; i++)
                {
                    string[] c = contexts[k];
                    records.Append(
                        "  0\n"
                            + held[k].Value
                            + "\n  5\n"
                            + c[4 * i]
                            + "\n102\n{ACAD_REACTORS\n330\n"
                            + owner
                            + "\n102\n}\n330\n"
                            + owner
                            + "\n100\nAcDbObjectContextData\n 70\n4\n290\n"
                            + c[4 * i + 2]
                            + "\n100\nAcDbAnnotScaleObjectContextData\n340\n"
                            + c[4 * i + 1]
                            + "\n"
                            + c[4 * i + 3]
                    );
                    entries.Append(
                        "  3\n*A" + (i + 1).ToString(CultureInfo.InvariantCulture) + "\n350\n" + c[4 * i] + "\n"
                    );
                }

                end = text.LastIndexOf("  0\nENDSEC\n", StringComparison.Ordinal);
                text = text.Substring(0, end) + records + text.Substring(end);
                string record = Records(text).Single(r => HandleOf(r) == owner);
                text = text.Replace(record, record + entries);
            }

            return text;
        }

        // ---- 1. a normal set on an entity of no contexts ---------------------------------------------------------------------

        [Fact]
        public void APlainMTextGivenANormal_KeepsItsLocation_AndReadsAlongTheXAxisOfItsNewPlane()
        {
            // AutoCAD 2021 gave an MTEXT at (10, 200) turned 30 degrees the normal (0, 0.6, 0.8): it stayed at (10, 200, 0) and
            // read along (-1, 0, 0), turned 0; given (0.6, 0, 0.8) first, it read along (0, 1, 0) (plugin D35d N0 normal,
            // normal2). An annotative one does the same (check D35c).
            DxfDatabase db = Ezdxf();
            DxfMText words = Words(db);
            words.Location = new GeoPoint3(10.0, 200.0, 0.0);
            words.Rotation = Math.PI / 6.0;
            words.Normal = Tilted;
            Assert.Equal(new GeoPoint3(10.0, 200.0, 0.0), words.Location);
            Near(new GeoVector3(-1.0, 0.0, 0.0), words.Direction);
            Near(0.0, words.Rotation);
            Near(new GeoVector3(-1.0, 0.0, 0.0), Words(DxfDatabase.Parse(db.ToText())).Direction);

            db = Ezdxf();
            words = Words(db);
            words.Rotation = Math.PI / 6.0;
            words.Normal = new GeoVector3(0.6, 0.0, 0.8);
            Near(new GeoVector3(0.0, 1.0, 0.0), words.Direction);
            words.Normal = Tilted;
            Near(new GeoVector3(-1.0, 0.0, 0.0), words.Direction);
        }

        [Fact]
        public void APlainINSERTGivenANormal_KeepsItsPointInItsPlane()
        {
            // AutoCAD 2021 gave an INSERT at (300, 100) turned 0.3 the normal (0, 0.6, 0.8): it stood at (-300, -80, 60), still
            // turned 0.3; given (0.6, 0, 0.8) first, at (-80, 300, 60) (plugin D35d N0 normal, normal2).
            DxfDatabase db = Ezdxf();
            DxfBlockReference insert = Insert(db);
            insert.Position = new GeoPoint3(300.0, 100.0, 0.0);
            insert.Rotation = 0.3;
            insert.Normal = new GeoVector3(0.6, 0.0, 0.8);
            Near(new GeoPoint3(-80.0, 300.0, 60.0), insert.Position);
            insert.Normal = Tilted;
            Near(new GeoPoint3(-300.0, -80.0, 60.0), insert.Position);
            Near(0.3, insert.Rotation);

            string record = Records(db.ToText()).Single(r => HandleOf(r) == "65");
            Assert.Contains(" 10\n300", record, StringComparison.Ordinal);
            Near(new GeoPoint3(-300.0, -80.0, 60.0), Insert(DxfDatabase.Parse(db.ToText())).Position);
        }

        [Fact]
        public void ATextAndItsAttributesGivenANormal_KeepTheirPointsInTheirPlane()
        {
            // AutoCAD 2021 gave a TEXT at (11, 12) turned 0.3, a centred TEXT on (30, 80), an ATTDEF at (15, 66) and an ATTRIB
            // the normal (0, 0.6, 0.8): they stood at (-11, -9.6, 7.2), on (-30, -64, 48), at (-15, -52.8, 39.6) and where their
            // points fall in the new plane, turned as they were (plugin D35d N0 normal).
            DxfDatabase db = Ezdxf();
            DxfDBText grid = Grid(db);
            grid.Position = new GeoPoint3(11.0, 12.0, 0.0);
            grid.Rotation = 0.3;
            grid.Normal = Tilted;
            Near(new GeoPoint3(-11.0, -9.6, 7.2), grid.Position);
            Near(0.3, grid.Rotation);

            db = Ezdxf();
            grid = Grid(db);
            grid.HorizontalMode = DxfTextHorizontalMode.TextCenter;
            grid.AlignmentPoint = new GeoPoint3(30.0, 80.0, 0.0);
            GeoPoint3 first = grid.Position;
            grid.Normal = Tilted;
            Near(new GeoPoint3(-30.0, -64.0, 48.0), grid.AlignmentPoint);
            Near(OnTilted(first), grid.Position);

            var mark = ByHandle<DxfAttributeDefinition>(db, "37");
            mark.Position = new GeoPoint3(15.0, 66.0, 0.0);
            mark.Normal = Tilted;
            Near(new GeoPoint3(-15.0, -52.8, 39.6), mark.Position);

            DxfAttributeReference attribute = Attribute(db);
            GeoPoint3 at = attribute.Position;
            attribute.Normal = Tilted;
            Near(OnTilted(at), attribute.Position);
            Near(OnTilted(at), Attribute(DxfDatabase.Parse(db.ToText())).Position);
        }

        [Fact]
        public void ASolidGivenANormal_KeepsItsCornersInItsPlane()
        {
            // AutoCAD 2021 gave the SOLID of corners (200, 0), (210, 0), (200, 10) and (210, 10) the normal (0, 0.6, 0.8): its
            // corners stood at (-200, 0, 0), (-210, 0, 0), (-200, -8, 6) and (-210, -8, 6) (plugin D35d N0 normal).
            DxfDatabase db = Ezdxf();
            var solid = new DxfSolid(
                new GeoPoint3(200.0, 0.0, 0.0),
                new GeoPoint3(210.0, 0.0, 0.0),
                new GeoPoint3(200.0, 10.0, 0.0),
                new GeoPoint3(210.0, 10.0, 0.0)
            );
            db.ModelSpace.AppendEntity(solid);
            solid.Normal = Tilted;
            Near(new GeoPoint3(-200.0, 0.0, 0.0), solid.GetPointAt(0));
            Near(new GeoPoint3(-210.0, 0.0, 0.0), solid.GetPointAt(1));
            Near(new GeoPoint3(-200.0, -8.0, 6.0), solid.GetPointAt(2));
            Near(new GeoPoint3(-210.0, -8.0, 6.0), solid.GetPointAt(3));
        }

        [Fact]
        public void TheEntitiesAutoCADKeepsInTheWorldOrInTheirPlane_AreGivenANormalAsBefore()
        {
            // AutoCAD 2021 kept a LINE's ends, a POINT and a CIRCLE's centre in the world, and an LWPOLYLINE's vertices in its
            // plane, when it gave them the normal (0, 0.6, 0.8) (plugin D35d N0 normal): as the library did already.
            DxfDatabase db = Ezdxf();
            var line = new DxfLine(new GeoPoint3(10.0, 10.0, 0.0), new GeoPoint3(30.0, 20.0, 0.0));
            var point = new DxfDBPoint(new GeoPoint3(40.0, 10.0, 0.0));
            var circle = new DxfCircle(new GeoPoint3(50.0, 50.0, 0.0), new GeoVector3(0.0, 0.0, 1.0), 5.0);
            var polyline = new DxfPolyline { Elevation = 3.0 };
            polyline.AddVertexAt(0, new GeoPoint2(100.0, 0.0), 0.0, 0.0, 0.0);
            polyline.AddVertexAt(1, new GeoPoint2(120.0, 10.0), 0.0, 0.0, 0.0);

            foreach (DxfEntity entity in new DxfEntity[] { line, point, circle, polyline })
            {
                db.ModelSpace.AppendEntity(entity);
            }

            line.Normal = Tilted;
            point.Normal = Tilted;
            circle.Normal = Tilted;
            polyline.Normal = Tilted;
            Assert.Equal(new GeoPoint3(30.0, 20.0, 0.0), line.EndPoint);
            Assert.Equal(new GeoPoint3(40.0, 10.0, 0.0), point.Position);
            Near(new GeoPoint3(50.0, 50.0, 0.0), circle.Center);
            Near(new GeoPoint3(-120.0, -6.2, 8.4), polyline.GetPoint3dAt(1));
        }

        // ---- 2. the width factor of an attribute made of several lines -------------------------------------------------------

        [Fact]
        public void AnAttributeOfNarrowLettersMadeOfSeveralLines_KeepsItsWidthFactor_AndItsMTextSaysSo()
        {
            // AutoCAD 2021 made the attribute A7, 0.7 wide, of several lines: the ATTRIB and the ATTDEF kept their 41 of 0.7, and
            // their MTEXT's words began \W0.7000; (plugin D35d P0 multi, @A7 and %A7). One of width 1 is given no \W.
            DxfDatabase db = Ezdxf();
            DxfAttributeReference attribute = Attribute(db);
            string value = attribute.TextString;
            attribute.WidthFactor = 0.7;
            attribute.IsMTextAttribute = true;
            Assert.Equal(0.7, attribute.WidthFactor);
            Assert.Equal("\\W0.7000;" + value, attribute.MTextAttribute.Contents);
            Assert.Equal(value, attribute.TextString);

            var mark = ByHandle<DxfAttributeDefinition>(db, "37");
            string words = mark.TextString;
            mark.WidthFactor = 0.7;
            mark.IsMTextAttributeDefinition = true;
            Assert.Equal(0.7, mark.WidthFactor);
            Assert.Equal("\\W0.7000;" + words, mark.MTextAttributeDefinition.Contents);

            DxfDatabase back = DxfDatabase.Parse(db.ToText());
            Assert.Equal(0.7, Attribute(back).WidthFactor);
            Assert.Equal("\\W0.7000;" + value, Attribute(back).MTextAttribute.Contents);

            db = Ezdxf();
            Attribute(db).IsMTextAttribute = true;
            Assert.Equal(value, Attribute(db).MTextAttribute.Contents);
            Assert.Equal(1.0, Attribute(db).WidthFactor);
        }

        // ---- 3. an annotative copy added to another drawing ------------------------------------------------------------------

        [Fact]
        public void AnAnnotativeTextCopiedIntoAnotherDrawing_KeepsItsContexts_OnItsScalesOfTheSameName()
        {
            // AutoCAD 2021's WblockCloneObjects and Insert of T1, of 1:1 and its default 1:2, into a drawing of both kept both
            // contexts on that drawing's scales: shown at (11, 12) 1.25 high, its 1:2 at (40, 12) (plugin D35d G2>X1, r3).
            DxfDatabase source = DxfDatabase.Parse(Drawing(false));
            DxfDatabase target = DxfDatabase.Parse(Drawing(false));
            var copy = (DxfDBText)source.WblockCloneObjects(new[] { Grid(source) }, target.ModelSpace)[0];
            Assert.Equal(new[] { "1:1", "1:2" }, ShownAt(target, copy));
            Assert.Equal(new GeoPoint3(11.0, 12.0, 0.0), copy.Position);
            Assert.Equal(1.25, copy.Height);

            DxfDatabase back = DxfDatabase.Parse(target.ToText());
            var again = ByHandle<DxfDBText>(back, copy.Handle);
            Assert.Equal(new[] { "1:1", "1:2" }, ShownAt(back, again));
            again.RemoveContext(Scales(back).GetContext("1:1"));
            Assert.Equal(new GeoPoint3(40.0, 12.0, 0.0), again.Position);
            Assert.Equal(new[] { "1:1=1/1", "1:2=1/2", "1:4=1/4" }, ScaleRows(back));

            target = DxfDatabase.Parse(Drawing(false));
            target.Insert(GeoTransform3.Identity, source, true);
            DxfDBText inserted = target
                .ModelSpace.OfType<DxfDBText>()
                .Last(t => t.TextString == Grid(source).TextString);
            Assert.Equal(new[] { "1:1", "1:2" }, ShownAt(target, inserted));
            Assert.Equal(new GeoPoint3(11.0, 12.0, 0.0), inserted.Position);
        }

        [Fact]
        public void AScaleTheOtherDrawingHasNot_IsAddedByName_AndOneOfItsNameOfOtherUnits_ComesAsItsName_1()
        {
            // AutoCAD 2021 copied contexts of Q5 (1:5), which the other drawing had not, onto a Q5 it added, though that drawing
            // had R5 of 1:5; and of S7 of 1:3, where that drawing's S7 was 1:7, onto an S7_1 of 1:3 it added (plugin D35d r3).
            DxfDatabase source = DxfDatabase.Parse(Drawing(false));
            DxfDatabase target = new DxfDatabase("AC1032");
            Scales(target)
                .AddContext(
                    new DxfAnnotationScale
                    {
                        Name = "1:2",
                        PaperUnits = 1.0,
                        DrawingUnits = 3.0,
                    }
                );
            var copy = (DxfDBText)Grid(source).Clone();
            target.ModelSpace.AppendEntity(copy);
            Assert.Equal(new[] { "1:1", "1:2_1" }, ShownAt(target, copy));
            Assert.Equal(new[] { "1:1=1/1", "1:2=1/3", "1:2_1=1/2" }, ScaleRows(target));
            Assert.Equal(new GeoPoint3(11.0, 12.0, 0.0), copy.Position);
            Assert.Equal(1.25, copy.Height);

            DxfDatabase back = DxfDatabase.Parse(target.ToText());
            var again = ByHandle<DxfDBText>(back, copy.Handle);
            Assert.Equal(new[] { "1:1", "1:2_1" }, ShownAt(back, again));
            again.RemoveContext(Scales(back).GetContext("1:1"));
            Assert.Equal(new GeoPoint3(40.0, 12.0, 0.0), again.Position);
        }

        [Fact]
        public void AnAnnotativeCopyAddedToADrawingOfNoScales_IsPlacedWhereItIsRead_AsBefore()
        {
            // A drawing of R2000 holds no list of scales: the copy is given the placing it is read at, its contexts left.
            DxfDatabase source = DxfDatabase.Parse(Drawing(false));
            DxfDatabase target = new DxfDatabase("AC1015");
            var copy = (DxfDBText)Grid(source).Clone();
            target.ModelSpace.AppendEntity(copy);
            Assert.Empty(ShownAt(target, copy));
            Assert.Equal(new GeoPoint3(11.0, 12.0, 0.0), copy.Position);
            Assert.Equal(1.25, copy.Height);
        }

        [Fact]
        public void AnAnnotativeINSERTCopiedIntoAnotherDrawing_BringsItsBlockAnnotative_ItsAttribShownWhereItWas()
        {
            // AutoCAD 2021's copy of an INSERT of the annotative block XA into a drawing without it brought XA annotative, and
            // its attribute kept its contexts of 1:1 and Q5 (plugin D35d X0>X1 wb, ins).
            DxfDatabase source = DxfDatabase.Parse(Drawing(true));
            DxfDatabase target = new DxfDatabase("AC1032");
            var copy = (DxfBlockReference)source.WblockCloneObjects(new[] { Insert(source) }, target.ModelSpace)[0];
            Assert.Equal(DxfAnnotativeStates.True, copy.BlockTableRecord.Annotative);
            Assert.Equal(DxfAnnotativeStates.True, copy.Annotative);
            DxfAttributeReference attribute = copy.AttributeCollection[0];
            Assert.Equal(new[] { "1:1", "1:2" }, ShownAt(target, attribute));
            Near(new GeoPoint3(-6.151391694414086, 265.3285550728426, 0.0), attribute.Position);

            target = new DxfDatabase("AC1032");
            target.Insert(GeoTransform3.Identity, source, true);
            DxfBlockReference inserted = target
                .ModelSpace.OfType<DxfBlockReference>()
                .First(b =>
                    b.Name == Insert(source).Name
                    && b.AttributeCollection.Count > 0
                    && b.Position == Insert(source).Position
                );
            Assert.Equal(DxfAnnotativeStates.True, inserted.Annotative);
            Assert.Equal(new[] { "1:1", "1:2" }, ShownAt(target, inserted.AttributeCollection[0]));
        }

        // ---- 4. an entity handed over to a copy of contexts -------------------------------------------------------------------

        [Fact]
        public void ALineHandedToTheCopyOfAnAnnotativeText_KeepingNoDictionary_WritesTheCopysContexts()
        {
            // AutoCAD 2021's LINE handed over to the copy of T1, its extension dictionary not kept, wrote the copy's contexts
            // under a dictionary of its own: read back, shown at (11, 12) 1.25 high at 1:1 and 1:2 (plugin D35d H0, r4 A2).
            DxfDatabase db = DxfDatabase.Parse(Drawing(false));
            var line = new DxfLine(new GeoPoint3(0.0, 0.0, 0.0), new GeoPoint3(20.0, 0.0, 0.0));
            db.ModelSpace.AppendEntity(line);
            string handle = line.Handle;
            var copy = (DxfDBText)Grid(db).Clone();
            line.HandOverTo(copy, true, false);
            Assert.Equal(handle, copy.Handle);
            Assert.Equal(new[] { "1:1", "1:2" }, ShownAt(db, copy));
            Assert.Equal(new GeoPoint3(11.0, 12.0, 0.0), copy.Position);

            DxfDatabase back = DxfDatabase.Parse(db.ToText());
            var again = ByHandle<DxfDBText>(back, handle);
            Assert.Equal(DxfAnnotativeStates.True, again.Annotative);
            Assert.Equal(new[] { "1:1", "1:2" }, ShownAt(back, again));
            Assert.Equal(new GeoPoint3(11.0, 12.0, 0.0), again.Position);
            Assert.Equal(1.25, again.Height);
        }

        [Fact]
        public void ALineHandedToTheCopyOfAnAnnotativeText_KeepingItsDictionaryAndData_LeavesTheCopyItsOwnData()
        {
            // AutoCAD 2021's LINE of no extension dictionary and no extended data, handed over to the copy of T1 keeping both,
            // wrote the copy of no contexts, still annotative: read back at its record, AutoCAD's (40, 12) 2.5 high, here the
            // record of ezdxf's TEXT (plugin D35d H0, r4 A1). A LINE of the data GHA gave the copy GHA alone, not annotative (r4
            // H1).
            DxfDBText record = Grid(Ezdxf());
            DxfDatabase db = DxfDatabase.Parse(Drawing(false));
            var line = new DxfLine(new GeoPoint3(0.0, 0.0, 0.0), new GeoPoint3(20.0, 0.0, 0.0));
            db.ModelSpace.AppendEntity(line);
            var copy = (DxfDBText)Grid(db).Clone();
            line.HandOverTo(copy, true, true);
            Assert.Equal(DxfAnnotativeStates.True, copy.Annotative);
            Assert.Empty(ShownAt(db, copy));
            Assert.Equal(record.Position, copy.Position);
            Assert.Equal(record.Height, copy.Height);

            db = DxfDatabase.Parse(Drawing(false));
            line = new DxfLine(new GeoPoint3(0.0, 0.0, 0.0), new GeoPoint3(20.0, 0.0, 0.0));
            db.ModelSpace.AppendEntity(line);
            line.XData = new[] { new DxfTypedValue(1001, "GHA"), new DxfTypedValue(1000, "a") };
            copy = (DxfDBText)Grid(db).Clone();
            line.HandOverTo(copy, true, true);
            Assert.Equal(DxfAnnotativeStates.False, copy.Annotative);
            Assert.Equal(new[] { "GHA" }, copy.XData.Where(g => g.TypeCode == 1001).Select(g => g.Value).ToArray());
        }
    }
}
