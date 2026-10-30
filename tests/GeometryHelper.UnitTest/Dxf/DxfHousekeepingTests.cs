using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// Keeping a drawing tidy: its empty objects counted and erased by AutoCAD's flags, the hard references to its objects
    /// counted, what nothing uses kept by Purge, and its extents worked out anew by UpdateExt, shrinking too.
    /// </summary>
    public class DxfHousekeepingTests
    {
        private static DxfDatabase NewDrawing() => DxfDatabase.Parse(new DxfWriter().ToString());

        /// <summary>The number next to a value, one unit of its last place up.</summary>
        private static double NextUp(double value) => BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(value) + 1);

        [Fact]
        public void TheEmptyObjects_AreTheCurvesOfNoLengthAndTheTextsOfNoWords_CountedAsTheyAreErased()
        {
            DxfDatabase db = NewDrawing();
            var point = new DxfLine(new GeoPoint3(5, 5, 0), new GeoPoint3(5, NextUp(5), 0));
            var shortLine = new DxfLine(new GeoPoint3(5, 5, 0), new GeoPoint3(5, 5.0004, 0));
            var dot = new DxfCircle(new GeoPoint3(1, 1, 0), new GeoVector3(0, 0, 1), 0.0);
            var ring = new DxfCircle(new GeoPoint3(1, 1, 0), new GeoVector3(0, 0, 1), 2.0);
            var folded = new DxfPolyline();
            folded.AddVertexAt(0, new GeoPoint2(3, 3), 0, 0, 0);
            folded.AddVertexAt(1, new GeoPoint2(3, NextUp(3)), 1, 0, 0);
            var blank = new DxfDBText { TextString = "   " };
            var words = new DxfDBText { TextString = "B-12" };
            var formatted = new DxfMText { Contents = "{\\fArial|b0|i0;}" };
            var note = new DxfMText { Contents = "Plate B-12" };

            foreach (DxfEntity entity in new DxfEntity[] { point, shortLine, dot, ring, folded, blank, words, formatted, note })
            {
                db.ModelSpace.AppendEntity(entity);
            }

            var inBlock = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(0, 0, 0));
            var block = new DxfBlockTableRecord { Name = "GH_EMPTY" };
            db.BlockTable.Add(block);
            block.AppendEntity(inBlock);

            Assert.Equal(4, db.CountEmptyObjects((int)DxfEraseFlags.ZeroLengthCurve));
            Assert.Equal(2, db.CountEmptyObjects((int)DxfEraseFlags.EmptyText));
            Assert.Equal(6, db.CountEmptyObjects((int)DxfEraseFlags.AllEmptyObj));
            Assert.Equal(0, db.CountEmptyObjects(0));

            Assert.Equal(2, db.EraseEmptyObjects((int)DxfEraseFlags.EmptyText));
            Assert.True(blank.IsErased);
            Assert.True(formatted.IsErased);
            Assert.False(point.IsErased);
            Assert.Equal(4, db.EraseEmptyObjects(-1));
            Assert.True(point.IsErased);
            Assert.True(dot.IsErased);
            Assert.True(folded.IsErased);
            Assert.True(inBlock.IsErased);
            Assert.False(shortLine.IsErased);
            Assert.False(ring.IsErased);
            Assert.False(words.IsErased);
            Assert.False(note.IsErased);
            Assert.Equal(0, db.CountEmptyObjects(-1));

            DxfDatabase read = DxfDatabase.Parse(db.ToText());
            Assert.Equal(new[] { "LINE", "CIRCLE", "TEXT", "MTEXT" }, read.ModelSpace.Select(e => e.Item.Type));
        }

        [Fact]
        public void ACurveWithAThickness_OrShorterThanTheTolerance_IsNotEmpty()
        {
            DxfDatabase db = NewDrawing();
            var post = new DxfLine(new GeoPoint3(5, 5, 0), new GeoPoint3(5, 5, 0)) { Thickness = 100 };
            var pin = new DxfCircle(new GeoPoint3(1, 1, 0), new GeoVector3(0, 0, 1), 0.0) { Thickness = 50 };
            var stub = new DxfPolyline { Thickness = 20 };
            stub.AddVertexAt(0, new GeoPoint2(3, 3), 0, 0, 0);
            stub.AddVertexAt(1, new GeoPoint2(3, 3), 0, 0, 0);

            // A line 0.0008 long in a block an INSERT draws at a scale of 10 000 is 8 long where it is drawn.
            var weld = new DxfLine(new GeoPoint3(1000, 0, 0), new GeoPoint3(1000.0008, 0, 0));
            var tick = new DxfLine(new GeoPoint3(1000, 0, 0), new GeoPoint3(NextUp(1000), 0, 0));
            var hair = new DxfCircle(new GeoPoint3(1, 1, 0), new GeoVector3(0, 0, 1), 1E-9);

            foreach (DxfEntity entity in new DxfEntity[] { post, pin, stub, hair })
            {
                db.ModelSpace.AppendEntity(entity);
            }

            var detail = new DxfBlockTableRecord { Name = "GH_DETAIL" };
            db.BlockTable.Add(detail);
            detail.AppendEntity(weld);
            detail.AppendEntity(tick);
            db.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(0, 0, 0), detail) { ScaleFactors = new GeoVector3(10000, 10000, 10000) });

            Assert.Equal(1, db.CountEmptyObjects((int)DxfEraseFlags.ZeroLengthCurve));
            Assert.Equal(1, db.EraseEmptyObjects(-1));
            Assert.True(tick.IsErased);
            Assert.False(weld.IsErased);
            Assert.False(post.IsErased);
            Assert.False(pin.IsErased);
            Assert.False(stub.IsErased);
            Assert.False(hair.IsErased);
        }

        [Fact]
        public void APolylineOfThousandsOfVertices_IsFoundEmptyOrNot_InAMomentAndInItsOwnPlane()
        {
            DxfDatabase db = NewDrawing();
            var tilted = new GeoVector3(0, 1, 1).Normalize();
            var folded = new DxfPolyline { Normal = tilted, Elevation = 250 };
            var open = new DxfPolyline { Normal = tilted, Elevation = 250 };

            for (int i = 0; i < 8000; i++)
            {
                folded.AddVertexAt(i, new GeoPoint2(1234.5, -678.25), 0, 0, 0);
                open.AddVertexAt(i, new GeoPoint2(1234.5 + i, -678.25), 0, 0, 0);
            }

            db.ModelSpace.AppendEntity(folded);
            db.ModelSpace.AppendEntity(open);
            var watch = System.Diagnostics.Stopwatch.StartNew();

            Assert.Equal(1, db.CountEmptyObjects((int)DxfEraseFlags.ZeroLengthCurve));
            Assert.Equal(1, db.EraseEmptyObjects((int)DxfEraseFlags.ZeroLengthCurve));
            Assert.True(watch.ElapsedMilliseconds < 3000, "Two polylines of 8 000 vertices took " + watch.ElapsedMilliseconds + " ms.");
            Assert.True(folded.IsErased);
            Assert.False(open.IsErased);
        }

        [Fact]
        public void ADrawingWithNoEmptyObject_IsLeftByteForByte()
        {
            foreach (string text in new[] { DxfFileEzdxfDrawings.R12, DxfFileEzdxfDrawings.R2000, DxfFileEzdxfDrawings.R2018 })
            {
                DxfDatabase db = DxfDatabase.Parse(text);

                Assert.Equal(0, db.CountEmptyObjects(-1));
                Assert.Equal(0, db.EraseEmptyObjects(-1));
                Assert.Equal(text, db.ToText());
            }
        }

        [Fact]
        public void TheHardReferences_AreThePointersAndTheNamesThatStandForThem_AddedToTheCounts()
        {
            DxfDatabase db = NewDrawing();
            var dashed = new DxfLinetypeTableRecord { Name = "DASHED", AsciiDescription = "__ __", NumDashes = 2 };
            dashed.SetDashLengthAt(0, 0.5);
            dashed.SetDashLengthAt(1, -0.25);
            db.LinetypeTable.Add(dashed);
            var steel = new DxfLayerTableRecord { Name = "steel" };
            db.LayerTable.Add(steel);
            var hidden = new DxfLayerTableRecord { Name = "hidden", LinetypeName = "DASHED" };
            db.LayerTable.Add(hidden);
            var bolt = new DxfBlockTableRecord { Name = "BOLT" };
            db.BlockTable.Add(bolt);
            bolt.AppendEntity(new DxfCircle(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), 5) { Layer = "steel" });
            var first = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0)) { Layer = "steel" };
            db.ModelSpace.AppendEntity(first);
            db.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 5, 0), new GeoPoint3(10, 5, 0)) { Layer = "steel", Linetype = "DASHED" });
            db.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(20, 0, 0), bolt));
            first.CreateExtensionDictionary();
            DxfDBDictionary extension = first.ExtensionDictionary;

            var objects = new List<DxfDBObject> { steel, hidden, dashed, bolt, first, extension, db.LayerTable["0"] };
            int[] counts = { 100, 0, 0, 0, 0, 0, 0 };
            db.CountHardReferences(objects, counts);

            // steel: the circle of BOLT and the two lines; hidden: none; DASHED: the layer hidden and the second line; BOLT:
            // its INSERT; the line: its block; its extension dictionary: the line's 360; layer 0: the INSERT, the BLOCK and
            // ENDBLK of each block and the header's $CLAYER, as many as the drawing writes them.
            Assert.Equal(103, counts[0]);
            Assert.Equal(0, counts[1]);
            Assert.Equal(2, counts[2]);
            Assert.Equal(1, counts[3]);
            Assert.Equal(1, counts[4]);
            Assert.Equal(1, counts[5]);
            Assert.True(counts[6] >= 4);

            Assert.Throws<ArgumentException>(() => db.CountHardReferences(objects, new int[3]));
            Assert.Throws<ArgumentException>(() => db.CountHardReferences(new List<DxfDBObject> { new DxfLayerTableRecord { Name = "x" } }, new int[1]));
            Assert.Throws<ArgumentException>(() => db.CountHardReferences(new List<DxfDBObject> { DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018).LayerTable["steel"] }, new int[1]));
            Assert.Throws<ArgumentNullException>(() => db.CountHardReferences(null, new int[1]));
            Assert.Throws<ArgumentNullException>(() => db.CountHardReferences(objects, null));
        }

        [Fact]
        public void APurge_KeepsWhatNothingUses_AndWhatAutoCadKeepsItselfIsNeverAmongIt()
        {
            DxfDatabase db = NewDrawing();
            var dashed = new DxfLinetypeTableRecord { Name = "DASHED", NumDashes = 2 };
            dashed.SetDashLengthAt(0, 0.5);
            dashed.SetDashLengthAt(1, -0.25);
            db.LinetypeTable.Add(dashed);
            db.LinetypeTable.Add(new DxfLinetypeTableRecord { Name = "SPARE_LT" });
            db.LayerTable.Add(new DxfLayerTableRecord { Name = "steel" });
            var hidden = new DxfLayerTableRecord { Name = "hidden", LinetypeName = "DASHED" };
            db.LayerTable.Add(hidden);
            db.LayerTable.Add(new DxfLayerTableRecord { Name = "Defpoints" });
            db.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = "GH_TXT" });
            var bolt = new DxfBlockTableRecord { Name = "BOLT" };
            db.BlockTable.Add(bolt);
            db.BlockTable.Add(new DxfBlockTableRecord { Name = "SPARE" });
            var line = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0)) { Layer = "steel" };
            db.ModelSpace.AppendEntity(line);
            db.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(20, 0, 0), bolt));

            Assert.Equal(new[] { "hidden" }, db.Purge(db.LayerTable.Cast<DxfDBObject>().ToList()).Select(o => ((DxfLayerTableRecord)o).Name));
            Assert.Equal(new[] { "SPARE_LT" }, db.Purge(db.LinetypeTable.Cast<DxfDBObject>().ToList()).Select(o => ((DxfLinetypeTableRecord)o).Name));

            // A text style nothing uses is offered as AutoCAD offers it, though the library does not erase a text style yet.
            Assert.Equal(new[] { "GH_TXT" },db.Purge(db.TextStyleTable.Cast<DxfDBObject>().ToList()).Select(o => ((DxfTextStyleTableRecord)o).Name));
            Assert.Equal(new[] { "SPARE" }, db.Purge(db.BlockTable.Cast<DxfDBObject>().ToList()).Select(o => ((DxfBlockTableRecord)o).Name));
            Assert.Empty(db.Purge(db.DimStyleTable.Cast<DxfDBObject>().ToList()));
            Assert.Empty(db.Purge(new List<DxfDBObject> { line, db.LayerTable, db.NamedObjectsDictionary, db.GroupDictionary }));

            // The linetype the layer hidden draws with is free once that layer goes, and the purge is run again.
            string before = db.ToText();
            hidden.Erase();
            Assert.Equal(new[] { "DASHED", "SPARE_LT" }, db.Purge(db.LinetypeTable.Cast<DxfDBObject>().ToList()).Select(o => ((DxfLinetypeTableRecord)o).Name));
            Assert.NotEqual(before, db.ToText());

            foreach (DxfDBObject free in db.Purge(db.LinetypeTable.Cast<DxfDBObject>().ToList()))
            {
                free.Erase();
            }

            Assert.Equal(new[] { "ByBlock", "ByLayer", "Continuous" }, DxfDatabase.Parse(db.ToText()).LinetypeTable.Select(l => l.Name));
        }

        [Fact]
        public void APurge_OffersNothingEraseRefuses_ThePointersOfOverridesTheStylesOfTablesAndTheShapeFiles()
        {
            // The ACAD_TABLE of the drawing has a cell in the text style GH_CELL, which nothing else writes in.
            string text = DxfDimensionDrawings.R2000;
            int table = text.IndexOf("  0\nACAD_TABLE\n", StringComparison.Ordinal);
            int cell = text.IndexOf("  7\nGH_TXT\n", table, StringComparison.Ordinal);
            DxfDatabase db = DxfDatabase.Parse(text.Substring(0, cell) + "  7\nGH_CELL\n" + text.Substring(cell + "  7\nGH_TXT\n".Length));

            foreach (string name in new[] { "GH_CELL", "GH_DIMTXT", "GH_TS", "GH_FREE" })
            {
                db.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = name });
            }

            db.File.AddTextStyle("GH_SHAPES", 1, 0.0, 1.0, 0.0, "ltypeshp.shx", string.Empty);
            var arrow = new DxfBlockTableRecord { Name = "ARROWX" };
            var soft = new DxfBlockTableRecord { Name = "GH_SOFT" };
            var spare = new DxfBlockTableRecord { Name = "GH_SPARE" };

            foreach (DxfBlockTableRecord block in new[] { arrow, soft, spare })
            {
                db.BlockTable.Add(block);
            }

            // A dimension whose overrides name the arrow ARROWX and the text style GH_DIMTXT (1005s of ACAD DSTYLE); a table style
            // whose rows are written in GH_TS (its 7); and an XRECORD that points at GH_SOFT (a 330 of its data).
            var dimension = new DxfRotatedDimension(0.0, new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(50, 20, 0), string.Empty, null) { DimBlock = db.BlockTable["*D2"] };
            db.ModelSpace.AppendEntity(dimension);
            dimension.Dimblk = arrow;
            dimension.TextStyle = db.TextStyleTable["GH_DIMTXT"];

            if (!db.NamedObjectsDictionary.Contains("ACAD_TABLESTYLE"))
            {
                db.NamedObjectsDictionary.SetAt("ACAD_TABLESTYLE", new DxfDBDictionary());
            }

            ((DxfDBDictionary)db.NamedObjectsDictionary["ACAD_TABLESTYLE"]).Add("GH_PLATES", "TABLESTYLE", b =>
            {
                b.Group(100, "AcDbTableStyle");
                b.Group(3, "Plates");
                b.Group(70, "0");
                b.Group(7, "GH_TS");
            });

            db.NamedObjectsDictionary.SetAt("GH_LINK", new DxfXrecord { Data = new List<DxfTypedValue> { new DxfTypedValue(330, soft.Handle) } });

            List<string> styles = db.Purge(db.TextStyleTable.Cast<DxfDBObject>().ToList()).Select(o => ((DxfTextStyleTableRecord)o).Name).ToList();
            Assert.Contains("GH_FREE", styles);
            Assert.DoesNotContain("GH_CELL", styles);
            Assert.DoesNotContain("GH_DIMTXT", styles);
            Assert.DoesNotContain("GH_TS", styles);
            Assert.DoesNotContain("GH_SHAPES", styles);

            List<string> blocks = db.Purge(db.BlockTable.Cast<DxfDBObject>().ToList()).Select(o => ((DxfBlockTableRecord)o).Name).ToList();
            Assert.Contains("GH_SPARE", blocks);
            Assert.DoesNotContain("ARROWX", blocks);
            Assert.DoesNotContain("GH_SOFT", blocks);

            // Each object a purge gives back of the kinds the library erases is erased, none refused, the purge run again
            // until it finds none.
            for (int pass = 0; pass < 10; pass++)
            {
                List<DxfDBObject> all = db.LayerTable.Cast<DxfDBObject>().Concat(db.LinetypeTable.Cast<DxfDBObject>())
                    .Concat(db.DimStyleTable.Cast<DxfDBObject>()).Concat(db.BlockTable.Cast<DxfDBObject>()).ToList();
                IReadOnlyList<DxfDBObject> free = db.Purge(all);

                if (free.Count == 0)
                {
                    break;
                }

                foreach (DxfDBObject value in free)
                {
                    value.Erase();
                }
            }

            Assert.True(db.BlockTable.Has("ARROWX"));
            Assert.True(db.BlockTable.Has("GH_SOFT"));
            Assert.False(db.BlockTable.Has("GH_SPARE"));
        }

        [Fact]
        public void APurge_OffersNoLayerAViewportFreezes_ByHandleOrInAnR12DrawingByName()
        {
            // The viewport 40 of the drawing of R2000 freezes a layer by its handle (331), the viewport 35 of R12 by its name.
            foreach (KeyValuePair<string, string> drawing in new[] { new KeyValuePair<string, string>(DxfObjectsEzdxfDrawings.R2000, "40"), new KeyValuePair<string, string>(DxfObjectsEzdxfDrawings.R12, "35") })
            {
                DxfDatabase db = DxfDatabase.Parse(drawing.Key);
                var frozen = new DxfLayerTableRecord { Name = "GH_FROZEN" };
                db.LayerTable.Add(frozen);
                db.LayerTable.Add(new DxfLayerTableRecord { Name = "GH_EMPTY" });
                ByHandle<DxfViewport>(db, drawing.Value).FreezeLayersInViewport(new[] { frozen });

                IReadOnlyList<DxfDBObject> free = db.Purge(db.LayerTable.Cast<DxfDBObject>().ToList());
                List<string> names = free.Select(o => ((DxfLayerTableRecord)o).Name).ToList();
                Assert.Contains("GH_EMPTY", names);
                Assert.DoesNotContain("GH_FROZEN", names);

                int[] counts = new int[1];
                db.CountHardReferences(new List<DxfDBObject> { frozen }, counts);
                Assert.Equal(1, counts[0]);

                foreach (DxfDBObject value in free)
                {
                    value.Erase();
                }

                Assert.True(db.LayerTable.Has("GH_FROZEN"));
            }
        }

        [Fact]
        public void APurge_KeepsTheObjectsAutoCadNeverPurges_ItsMaterialsVariablesAndStandardStyles()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            var objects = new List<DxfDBObject>();
            var dictionaries = new List<DxfDBDictionary> { db.NamedObjectsDictionary };

            for (int i = 0; i < dictionaries.Count; i++)
            {
                foreach (DxfDBDictionaryEntry entry in dictionaries[i])
                {
                    objects.Add(entry.Value);

                    if (entry.Value is DxfDBDictionary inner)
                    {
                        dictionaries.Add(inner);
                    }
                }
            }

            List<string> types = db.Purge(objects).Select(o => o.Item.Type).ToList();

            // ezdxf writes them as AutoCAD's template has them: the materials Global, ByLayer and ByBlock, the variables of a
            // dictionary of variables, the multileader style Standard, the plot style Normal and the variables of images. An
            // XRECORD nothing points at is offered still.
            Assert.Equal(3, objects.Count(o => o.Item.Type == "MATERIAL"));
            Assert.Equal(2, objects.Count(o => o.Item.Type == "DICTIONARYVAR"));
            Assert.DoesNotContain("MATERIAL", types);
            Assert.DoesNotContain("DICTIONARYVAR", types);
            Assert.DoesNotContain("MLEADERSTYLE", types);
            Assert.DoesNotContain("ACDBPLACEHOLDER", types);
            Assert.DoesNotContain("RASTERVARIABLES", types);
            Assert.Contains("XRECORD", types);
        }

        [Fact]
        public void APurge_KeepsEachKindAutoCadNeverPurges_ThoughNothingPointsAtIt_AndOffersTheOthersOfTheKind()
        {
            DxfDatabase db = NewDrawing();
            var made = new List<DxfDBObject>();

            // Each object in a dictionary of its own, nothing pointing at it.
            void Add(string dictionary, string name, string type, Action<DxfFile.Builder> groups)
            {
                if (!db.NamedObjectsDictionary.Contains(dictionary))
                {
                    db.NamedObjectsDictionary.SetAt(dictionary, new DxfDBDictionary());
                }

                var owner = (DxfDBDictionary)db.NamedObjectsDictionary[dictionary];
                owner.Add(name, type, groups);
                made.Add(owner[name]);
            }

            Add("GH_MLEADERSTYLES", "Standard", "MLEADERSTYLE", b => b.Group(100, "AcDbMLeaderStyle"));
            Add("GH_MLEADERSTYLES", "GH_SPARE", "MLEADERSTYLE", b => b.Group(100, "AcDbMLeaderStyle"));
            Add("GH_TABLESTYLES", "Standard", "TABLESTYLE", b => b.Group(100, "AcDbTableStyle"));
            Add("GH_TABLESTYLES", "GH_SPARE", "TABLESTYLE", b => b.Group(100, "AcDbTableStyle"));
            Add("GH_PLOTSTYLES", "Normal", "ACDBPLACEHOLDER", b => { });
            Add("GH_PLOTSTYLES", "GH_SPARE", "ACDBPLACEHOLDER", b => { });
            Add("GH_VARIABLES", "GH_WIPEOUT", "WIPEOUTVARIABLES", b => { b.Group(100, "AcDbWipeoutVariables"); b.Group(70, "1"); });
            Add("GH_VARIABLES", "GH_RASTER", "RASTERVARIABLES", b => { b.Group(100, "AcDbRasterVariables"); b.Group(90, "0"); });
            Add("GH_VARIABLES", "GH_VAR", "DICTIONARYVAR", b => { b.Group(100, "DictionaryVariables"); b.Group(280, "0"); b.Group(1, "1"); });
            Add("GH_VISUALSTYLES", "Realistic", "VISUALSTYLE", b => { b.Group(100, "AcDbVisualStyle"); b.Group(2, "Realistic"); });
            Add("GH_MATERIALS", "Global", "MATERIAL", b => { b.Group(100, "AcDbMaterial"); b.Group(1, "Global"); });
            Add("GH_MATERIALS", "GH_STEEL", "MATERIAL", b => { b.Group(100, "AcDbMaterial"); b.Group(1, "GH_STEEL"); });

            IReadOnlyList<DxfDBObject> free = db.Purge(made);

            // Of each kind AutoCAD keeps its own, Standard, Normal, Global, the variables and the visual styles, and offers the
            // others nothing uses.
            Assert.Equal(new[] { "MLEADERSTYLE GH_SPARE", "TABLESTYLE GH_SPARE", "ACDBPLACEHOLDER GH_SPARE", "MATERIAL GH_STEEL" },
                free.Select(o => o.Item.Type + " " + ((DxfDBDictionary)DxfDBObject.OfObject(db.File.ObjectItem(o.OwnerHandle))).NameAt(o)));
        }

        [Fact]
        public void AnArrowADimensionOfR12OverridesByName_IsCounted_AndNeitherPurgedNorErased()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R12);
            var arrow = new DxfBlockTableRecord { Name = "B_OVR" };
            db.BlockTable.Add(arrow);
            arrow.AppendEntity(new DxfLine(new GeoPoint3(-1, -0.5, 0), new GeoPoint3(0, 0, 0)));
            DxfDimension dimension = db.ModelSpace.OfType<DxfDimension>().First();
            dimension.Dimblk = arrow;

            // The override is written by name, as a file of R12 writes it: 1070 5, then 1000 B_OVR, in ACAD's DSTYLE.
            Assert.Contains("1070\n5\n1000\nB_OVR\n", Records(db.ToText()).Single(r => HandleOf(r) == dimension.Handle), StringComparison.Ordinal);

            int[] counts = new int[1];
            db.CountHardReferences(new List<DxfDBObject> { arrow }, counts);
            Assert.Equal(1, counts[0]);
            Assert.DoesNotContain(arrow, db.Purge(db.BlockTable.Cast<DxfDBObject>().ToList()));
            string text = db.ToText();
            Assert.Throws<InvalidOperationException>(() => arrow.Erase());
            Assert.Equal(text, db.ToText());
        }

        [Fact]
        public void AnArrowADimensionStyleOfR14Names_IsCounted_AndALinetypeOfTheNameOfAnR12Arrow_IsErased()
        {
            // AutoCAD's drawing of R14 names a style's arrows by name (5, 6, 7), as one of R12 does: GH_EMPTY's first is
            // GH_R14ARROW.
            string text = DxfDimensionDrawings.R2000.Replace("AC1015", "AC1014");
            int style = text.IndexOf("\n  2\nGH_EMPTY\n 70\n", text.IndexOf("  0\nTABLE\n  2\nDIMSTYLE\n", StringComparison.Ordinal), StringComparison.Ordinal);
            int after = text.IndexOf('\n', text.IndexOf("\n 70\n", style + 1, StringComparison.Ordinal) + 5) + 1;
            DxfDatabase r14 = DxfDatabase.Parse(text.Substring(0, after) + "  5\nGH_R14ARROW\n" + text.Substring(after));
            var arrow = new DxfBlockTableRecord { Name = "GH_R14ARROW" };
            r14.BlockTable.Add(arrow);

            int[] counts = new int[1];
            r14.CountHardReferences(new List<DxfDBObject> { arrow }, counts);
            Assert.Equal(1, counts[0]);
            Assert.DoesNotContain(arrow, r14.Purge(r14.BlockTable.Cast<DxfDBObject>().ToList()));
            Assert.Throws<InvalidOperationException>(() => arrow.Erase());

            // GH_DIM of R12 draws its first arrow with the block DOT (its 6), which is no linetype: the linetype DOT nothing draws
            // with is purged and erased.
            DxfDatabase r12 = DxfDatabase.Parse(DxfDimensionDrawings.R12);
            var dot = new DxfLinetypeTableRecord { Name = "DOT", NumDashes = 2 };
            dot.SetDashLengthAt(0, 0.0);
            dot.SetDashLengthAt(1, -0.25);
            r12.LinetypeTable.Add(dot);

            Assert.Contains(dot, r12.Purge(r12.LinetypeTable.Cast<DxfDBObject>().ToList()));
            dot.Erase();
            Assert.False(DxfDatabase.Parse(r12.ToText()).LinetypeTable.Has("DOT"));
        }

        [Fact]
        public void ASplineOfThousandsOfPointsOnOnePoint_IsFoundEmpty_InAMoment()
        {
            DxfDatabase db = NewDrawing();
            var points = Enumerable.Repeat(new GeoPoint3(1234.5, -678.25, 90), 8000).ToList();
            var knots = Enumerable.Repeat(0.0, 4).Concat(Enumerable.Range(1, 7996).Select(k => (double)k)).Concat(Enumerable.Repeat(7997.0, 4)).ToList();
            var spline = new DxfSpline(3, false, false, false, points, knots, new double[0], 0.0, 0.0);
            db.ModelSpace.AppendEntity(spline);
            var watch = System.Diagnostics.Stopwatch.StartNew();

            Assert.Equal(1, db.CountEmptyObjects((int)DxfEraseFlags.ZeroLengthCurve));
            Assert.True(watch.ElapsedMilliseconds < 3000, "A spline of 8 000 points took " + watch.ElapsedMilliseconds + " ms.");
        }

        [Fact]
        public void APurgeOrACount_WritesNothing()
        {
            foreach (string text in new[] { DxfFileEzdxfDrawings.R12, DxfFileEzdxfDrawings.R2000, DxfFileEzdxfDrawings.R2018 })
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                List<DxfDBObject> all = db.LayerTable.Cast<DxfDBObject>().Concat(db.LinetypeTable.Cast<DxfDBObject>()).Concat(db.TextStyleTable.Cast<DxfDBObject>())
                    .Concat(db.DimStyleTable.Cast<DxfDBObject>()).Concat(db.BlockTable.Cast<DxfDBObject>()).ToList();

                db.CountHardReferences(all, new int[all.Count]);
                db.Purge(all);
                Assert.Equal(text, db.ToText());
            }
        }

        [Fact]
        public void TheExtentsWorkedOutAnew_AreTheBoxOfWhatTheSpacesHold_ShrinkingToo()
        {
            DxfDatabase db = NewDrawing();
            var far = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(5110, 300, 0));
            db.ModelSpace.AppendEntity(far);
            db.ModelSpace.AppendEntity(new DxfCircle(new GeoPoint3(100, 50, 0), new GeoVector3(0, 0, 1), 5));
            far.Erase();
            Assert.Equal(new GeoPoint3(5110, 300, 0), db.Extmax);

            db.UpdateExt(true);

            Assert.Equal(new GeoPoint3(95, 45, 0), db.Extmin);
            Assert.Equal(new GeoPoint3(105, 55, 0), db.Extmax);
            Assert.Equal(new GeoPoint3(1E+20, 1E+20, 1E+20), db.Pextmin);
            Assert.Equal(new GeoPoint3(-1E+20, -1E+20, -1E+20), db.Pextmax);

            // A text counts as the point it stands at; paper space has its own box.
            db.ModelSpace.AppendEntity(new DxfDBText { TextString = "B-12", Position = new GeoPoint3(200, 10, 0), Height = 2.5 });
            db.PaperSpace.AppendEntity(new DxfLine(new GeoPoint3(10, 20, 0), new GeoPoint3(30, 40, 0)));
            db.UpdateExt(false);

            DxfDatabase read = DxfDatabase.Parse(db.ToText());
            Assert.Equal(new GeoPoint3(95, 10, 0), read.Extmin);
            Assert.Equal(new GeoPoint3(200, 55, 0), read.Extmax);
            Assert.Equal(new GeoPoint3(10, 20, 0), read.Pextmin);
            Assert.Equal(new GeoPoint3(30, 40, 0), read.Pextmax);

            string text = db.ToText();
            db.UpdateExt(true);
            Assert.Equal(text, db.ToText());
        }

        [Fact]
        public void TheExtentsOfEzdxfsDrawings_AreThoseOfTheirShapes()
        {
            foreach (string text in new[] { DxfFileEzdxfDrawings.R12, DxfFileEzdxfDrawings.R2000, DxfFileEzdxfDrawings.R2018 })
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                double[] box = { double.MaxValue, double.MaxValue, double.MaxValue, double.MinValue, double.MinValue, double.MinValue };

                foreach (DxfEntity entity in db.ModelSpace)
                {
                    GeoAabb3 extents;

                    try
                    {
                        // A MINSERT's GeometricExtents is its first cell's, as AutoCAD's: the drawing's take in every cell.
                        extents = entity is DxfMInsertBlock array ? array.EveryCellExtents : entity.GeometricExtents;
                    }
                    catch (InvalidOperationException)
                    {
                        continue;
                    }

                    box = new[]
                    {
                        Math.Min(box[0], extents.Min.X), Math.Min(box[1], extents.Min.Y), Math.Min(box[2], extents.Min.Z),
                        Math.Max(box[3], extents.Max.X), Math.Max(box[4], extents.Max.Y), Math.Max(box[5], extents.Max.Z),
                    };
                }

                db.UpdateExt(true);

                Assert.Equal(Bits(new GeoPoint3(box[0], box[1], box[2])), Bits(DxfDatabase.Parse(db.ToText()).Extmin));
                Assert.Equal(Bits(new GeoPoint3(box[3], box[4], box[5])), Bits(DxfDatabase.Parse(db.ToText()).Extmax));
            }
        }
    }
}
