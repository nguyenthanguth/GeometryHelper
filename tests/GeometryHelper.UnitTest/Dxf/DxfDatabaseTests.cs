using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfLines;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The AutoCAD-like model of a DXF file, <see cref="DxfDatabase"/>: opened and saved byte for byte, its blocks, layers,
    /// linetypes and text styles as AutoCAD names them, its header, extended data, colours, and entities appended to model
    /// space, paper space and a new block. Every value set was read back by ezdxf 1.4.5 in the probe, whose recover and audit
    /// found nothing to fix but the DASHED linetype the drawings of ezdxf leave undefined.
    /// </summary>
    public class DxfDatabaseTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        private static readonly string[] Drawings = { DxfFileEzdxfDrawings.R12, DxfFileEzdxfDrawings.R2000, DxfFileEzdxfDrawings.R2018 };

        #region Opened and saved

        [Fact]
        public void ADrawingOpenedAndSaved_IsTheSameBytes_FromAPathAStreamOrATextOfEachVersion()
        {
            string writer = DxfReaderRoundTripTests.OneOfEach().ToString();

            foreach (string text in Drawings.Concat(new[] { writer, Plain }))
            {
                byte[] bytes = new UTF8Encoding(false).GetBytes(text.Replace("\n", "\r\n"));

                Assert.Equal(bytes, Saved(Opened(bytes)));
                Assert.Equal(text, DxfDatabase.Parse(text).ToText());
            }

            string from = Path.Combine(Path.GetTempPath(), "dxfdb-" + Guid.NewGuid().ToString("N") + ".dxf");
            string to = Path.Combine(Path.GetTempPath(), "dxfdb-" + Guid.NewGuid().ToString("N") + ".dxf");

            try
            {
                System.IO.File.WriteAllBytes(from, Encoding.GetEncoding(1252).GetBytes(DxfFileEzdxfDrawings.R2000.Replace("\n", "\r\n")));
                DxfDatabase.Open(from).Save(to);
                Assert.Equal(System.IO.File.ReadAllBytes(from), System.IO.File.ReadAllBytes(to));
            }
            finally
            {
                System.IO.File.Delete(from);
                System.IO.File.Delete(to);
            }
        }

        private static readonly string Plain = R2000(G(0, "LINE", 5, "2A", 8, "0", 10, "0", 20, "0", 11, "1", 21, "0"));

        [Fact]
        public void AFileTheReaderRefuses_IsRefusedWithTheReadersMessage()
        {
            foreach (string text in new[] { G(0, "SECTION", 2, "HEADER", 0, "ENDSEC", 0, "EOF"), string.Empty, "AutoCAD Binary DXF\r\n\u001a\0" })
            {
                string expected = Assert.Throws<FormatException>(() => DxfReader.Parse(text)).Message;

                Assert.Equal(expected, Assert.Throws<FormatException>(() => DxfDatabase.Parse(text)).Message);
            }

            Assert.Throws<ArgumentNullException>(() => DxfDatabase.Open((string)null));
            Assert.Throws<ArgumentNullException>(() => DxfDatabase.Open((Stream)null));
            Assert.Throws<ArgumentNullException>(() => DxfDatabase.Parse(null));
        }

        [Fact]
        public void TheVersionCodePageAndWarnings_AreThoseTheFileSays()
        {
            Assert.Equal("AC1009", DxfDatabase.Parse(DxfFileEzdxfDrawings.R12).Version);
            Assert.Equal("AC1032", DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018).Version);
            Assert.Equal("ANSI_1252", Opened(Encoding.GetEncoding(1252).GetBytes(DxfFileEzdxfDrawings.R2000)).CodePage);
            Assert.Empty(Opened(Encoding.GetEncoding(1252).GetBytes(DxfFileEzdxfDrawings.R2000)).Warnings);
        }

        #endregion

        #region Blocks and spaces

        [Fact]
        public void TheBlockTable_IsModelSpacePaperSpaceAndTheBlocks_InTheOrderOfTheFile()
        {
            DxfDatabase r2000 = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            DxfDatabase r12 = DxfDatabase.Parse(DxfFileEzdxfDrawings.R12);

            Assert.Equal(new[] { "*Model_Space", "*Paper_Space", "BOLT", "FRAME", "*D1", "_CLOSEDFILLED", "*Paper_Space0" }, r2000.BlockTable.Select(b => b.Name));
            Assert.Same(r2000.ModelSpace, r2000.BlockTable[DxfBlockTableRecord.ModelSpace]);
            Assert.Same(r2000.PaperSpace, r2000.BlockTable["*PAPER_SPACE"]);
            Assert.Same(r2000.BlockTable["bolt"], r2000.BlockTable["BOLT"]);
            Assert.Equal("17", r2000.ModelSpace.Handle);
            Assert.Equal(new GeoPoint3(1, 2, 0), r2000.BlockTable["BOLT"].Origin);
            Assert.Equal(new[] { true, true, false, false, false, false, true }, r2000.BlockTable.Select(b => b.IsLayout));
            Assert.True(r2000.BlockTable["*D1"].IsAnonymous);
            Assert.True(r2000.BlockTable["BOLT"].HasAttributeDefinitions);
            Assert.False(r2000.BlockTable.Has("NOPE"));
            Assert.Throws<KeyNotFoundException>(() => r2000.BlockTable["NOPE"]);

            // ezdxf's files of R12 hold model and paper space as the blocks $Model_Space and $Paper_Space: AutoCAD's names all the same.
            Assert.Equal(new[] { "*Model_Space", "*Paper_Space", "BOLT", "FRAME" }, r12.BlockTable.Select(b => b.Name));
            Assert.Same(r12.ModelSpace, r12.BlockTable["*Model_Space"]);
            Assert.Equal(12, r12.ModelSpace.Count);
            Assert.Equal(27, r2000.ModelSpace.Count);
        }

        [Fact]
        public void TheEntities_AreOfTheirClasses_TheSameObjectEachTime_AndTheReadersShapes()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            DxfDocument read = DxfReader.Parse(DxfFileEzdxfDrawings.R2000, new DxfReadOptions { Tolerance = Tol, ExpandBlocks = false });
            List<DxfEntity> first = db.ModelSpace.ToList();

            Assert.Equal(first, db.ModelSpace.ToList());
            Assert.IsType<DxfLine>(first[0]);
            Assert.IsType<DxfHatch>(first.Single(e => e.Handle == "74"));
            Assert.Equal("IMAGE", Assert.IsType<DxfUnknownEntity>(first.Single(e => e.Handle == "8E")).DxfName);
            Assert.All(first, e => Assert.Same(db.ModelSpace, e.Owner));

            // A LEADER, which the reader does not read, gives its curve (A8).
            Assert.Equal(read.Shapes.Count(s => s.Type != "INSERT"), first.Where(e => e.Item.Type != "INSERT" && e.Item.Type != "LEADER").Sum(e => e.GetGeometry().Count));
        }

        [Fact]
        public void ABlockAdded_HoldsWhatIsAppendedToIt_ForTheReaderAndTheInsertsOfIt()
        {
            foreach (string text in Drawings)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                var block = new DxfBlockTableRecord { Name = "GH_BLOCK", Origin = new GeoPoint3(1, 2, 0) };

                Assert.Throws<InvalidOperationException>(() => block.AppendEntity(new DxfLine()));
                db.BlockTable.Add(block);
                block.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0)) { Layer = "parts" });

                Assert.Same(block, db.BlockTable["gh_block"]);
                Assert.Equal(1, block.Count);
                Assert.Equal("GH_BLOCK", block.Single().BlockName);
                Assert.Throws<ArgumentException>(() => db.BlockTable.Add(new DxfBlockTableRecord { Name = "gh_block" }));

                string saved = db.ToText();
                DxfBlock read = DxfReader.Parse(saved, new DxfReadOptions { Tolerance = Tol }).Blocks.Single(b => b.Name == "GH_BLOCK");
                Assert.Equal(new GeoPoint3(1, 2, 0), read.BasePoint);
                Assert.Equal(new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0)), Assert.Single(read.Shapes).Shape);
                DxfDatabase again = DxfDatabase.Parse(saved);
                Assert.Equal(new GeoPoint3(1, 2, 0), again.BlockTable["GH_BLOCK"].Origin);
                Assert.Equal("parts", Assert.IsType<DxfLine>(Assert.Single(again.BlockTable["GH_BLOCK"])).Layer);
            }
        }

        [Fact]
        public void AnEntityAppendedToPaperSpace_IsOfPaperSpace_AndOneErasedFromABlockLeavesIt()
        {
            string text = DxfFileEzdxfDrawings.R2000;
            DxfDatabase db = DxfDatabase.Parse(text);
            var line = new DxfLine(new GeoPoint3(5, 5, 0), new GeoPoint3(50, 5, 0)) { ColorIndex = 5 };
            DxfLine inBolt = db.BlockTable["BOLT"].OfType<DxfLine>().Single();

            db.PaperSpace.AppendEntity(line);
            inBolt.Erase();

            Assert.Equal("1B", line.OwnerHandle);
            Assert.Same(db.PaperSpace, line.Owner);
            Assert.Equal(2, db.BlockTable["BOLT"].Count);
            string saved = db.ToText();
            OnlyTheseChanged(text, saved, line.Handle, "36");
            Assert.Contains("100\nAcDbEntity\n 67\n1\n", Records(saved).Single(r => HandleOf(r) == line.Handle), StringComparison.Ordinal);
            DxfDatabase again = DxfDatabase.Parse(saved);
            Assert.Contains(again.PaperSpace, e => e.Handle == line.Handle);
            Assert.DoesNotContain(again.BlockTable["BOLT"], e => e is DxfLine);
        }

        #endregion

        #region Layers, linetypes and text styles

        [Fact]
        public void TheLayers_AreThoseOfTheTable_AsEzdxfReadsThem()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);

            Assert.Equal(new[] { "0", "Defpoints", "steel", "Wand€Außen" }, db.LayerTable.Select(l => l.Name));
            DxfLayerTableRecord steel = db.LayerTable["STEEL"];
            Assert.Equal(1, steel.ColorIndex);
            Assert.Equal("DASHED", steel.LinetypeName);
            Assert.Equal(DxfLineWeight.ByLineWeightDefault, steel.LineWeight);
            Assert.False(steel.IsOff || steel.IsFrozen || steel.IsLocked);
            Assert.False(db.LayerTable["Defpoints"].IsPlottable);
            Assert.Equal(5, db.LayerTable["Wand€Außen"].ColorIndex);
            Assert.Equal(DxfColorMethod.ByAci, steel.Color.ColorMethod);
            Assert.Same(steel, db.LayerTable.First(l => l.Name == "steel"));
        }

        [Fact]
        public void ALayerChanged_IsItsRecordWrittenAnew_AndSettingWhatItHasChangesNothing()
        {
            string text = DxfFileEzdxfDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfLayerTableRecord steel = db.LayerTable["steel"];

            foreach (DxfLayerTableRecord layer in db.LayerTable)
            {
                layer.ColorIndex = layer.ColorIndex;
                layer.Color = layer.Color;
                layer.TrueColor = layer.TrueColor;
                layer.LinetypeName = layer.LinetypeName;
                layer.LineWeight = layer.LineWeight;
                layer.IsOff = layer.IsOff;
                layer.IsFrozen = layer.IsFrozen;
                layer.IsLocked = layer.IsLocked;
                layer.IsPlottable = layer.IsPlottable;
                layer.Name = layer.Name;
                layer.XData = layer.XData;
            }

            Assert.Equal(text, db.ToText());

            // As ezdxf reads them: the colour -6 (off), the flags 1 (frozen) and 4 (locked), the lineweight 35, plot 0.
            steel.ColorIndex = 6;
            steel.IsOff = true;
            steel.IsFrozen = true;
            steel.IsLocked = true;
            steel.LineWeight = DxfLineWeight.LineWeight035;
            steel.IsPlottable = false;
            steel.TrueColor = 0xFF8000;
            steel.LinetypeName = "Continuous";

            string saved = db.ToText();
            OnlyTheseChanged(text, saved, steel.Handle);
            DxfLayerTableRecord again = DxfDatabase.Parse(saved).LayerTable["steel"];
            Assert.Equal(6, again.ColorIndex);
            Assert.True(again.IsOff && again.IsFrozen && again.IsLocked && !again.IsPlottable);
            Assert.Equal(DxfLineWeight.LineWeight035, again.LineWeight);
            Assert.Equal(0xFF8000, again.TrueColor);
            Assert.Equal("Continuous", again.LinetypeName);
            Assert.Contains(" 62\n-6\n", Records(saved).Single(r => HandleOf(r) == steel.Handle), StringComparison.Ordinal);
            DxfLayer read = DxfReader.Parse(saved, new DxfReadOptions { Tolerance = Tol }).GetLayer("steel");
            Assert.True(read.IsOff && read.IsFrozen && read.IsLocked && !read.IsPlotted);

            Assert.Throws<InvalidOperationException>(() => db.LayerTable["0"].Name = "iron");
            Assert.Throws<ArgumentException>(() => steel.LinetypeName = "NOPE");
            Assert.Throws<ArgumentOutOfRangeException>(() => steel.ColorIndex = 256);
            Assert.Throws<ArgumentOutOfRangeException>(() => steel.LineWeight = DxfLineWeight.ByLayer);
            Assert.Throws<InvalidOperationException>(() => DxfDatabase.Parse(DxfFileEzdxfDrawings.R12).LayerTable["steel"].LineWeight = DxfLineWeight.LineWeight050);
        }

        [Fact]
        public void ALayerAndALinetypeAdded_AreWrittenInTheTables_AndTakenByAnEntity()
        {
            foreach (string text in Drawings)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                var dashed = new DxfLinetypeTableRecord { Name = "GH_DASHED", AsciiDescription = "__ __ __", NumDashes = 2 };
                dashed.SetDashLengthAt(0, 0.5);
                dashed.SetDashLengthAt(1, -0.25);
                db.LinetypeTable.Add(dashed);
                var layer = new DxfLayerTableRecord { Name = "GH_Layer", ColorIndex = 3, LinetypeName = "GH_DASHED", IsLocked = true };
                db.LayerTable.Add(layer);
                DxfLine line = db.ModelSpace.OfType<DxfLine>().First();
                line.Linetype = "GH_DASHED";
                line.Layer = "GH_Layer";

                Assert.Equal(0.75, dashed.PatternLength);
                Assert.NotNull(layer.Handle);
                Assert.Throws<ArgumentException>(() => db.LayerTable.Add(new DxfLayerTableRecord { Name = "gh_layer" }));
                Assert.Throws<ArgumentException>(() => db.LayerTable.Add(new DxfLayerTableRecord { Name = "a:b" }));
                Assert.Throws<InvalidOperationException>(() => dashed.NumDashes = 3);

                DxfDatabase again = DxfDatabase.Parse(db.ToText());
                DxfLinetypeTableRecord read = again.LinetypeTable["GH_DASHED"];
                Assert.Equal(new[] { 0.5, -0.25 }, Enumerable.Range(0, read.NumDashes).Select(read.DashLengthAt));
                Assert.Equal("__ __ __", read.AsciiDescription);
                Assert.Equal(0.75, read.PatternLength);
                Assert.Equal(3, again.LayerTable["GH_Layer"].ColorIndex);
                Assert.True(again.LayerTable["GH_Layer"].IsLocked);
                Assert.Equal("GH_DASHED", again.LayerTable["GH_Layer"].LinetypeName);
                Assert.Equal("GH_DASHED", ((DxfLine)again.ModelSpace.First()).Linetype);
            }
        }

        [Fact]
        public void TheTextStyles_AreRead()
        {
            DxfTextStyleTableRecord standard = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000).TextStyleTable["Standard"];

            Assert.Equal("txt", standard.FileName);
            Assert.Equal(0.0, standard.TextSize);
            Assert.Equal(1.0, standard.XScale);
            Assert.Throws<ArgumentException>(() => DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000).TextStyleTable.Add(new DxfLayerTableRecord { Name = "x" }));
        }

        #endregion

        #region The header

        [Fact]
        public void TheHeader_IsReadAndSetByItsVariables_TypedAndRaw()
        {
            string text = DxfFileEzdxfDrawings.R2000;
            DxfDatabase db = DxfDatabase.Parse(text);

            Assert.Equal("A1", db.Handseed);
            Assert.Equal(DxfUnits.Meters, db.Insunits);
            Assert.Equal("0", db.Clayer);
            Assert.Equal(1E20, db.Extmin.X);
            Assert.Equal(new[] { "70 6" }, db.GetHeader("$INSUNITS").Select(g => g.ToString()));
            Assert.Null(db.GetHeader("$NOPE"));

            db.Clayer = db.Clayer;
            db.Insunits = db.Insunits;
            db.Ltscale = db.Ltscale;
            db.Angbase = db.Angbase;
            db.Limmax = db.Limmax;
            db.Cecolor = db.Cecolor;
            db.Celweight = db.Celweight;
            db.Mirrtext = db.Mirrtext;
            Assert.Equal(text, db.ToText());

            db.Insunits = DxfUnits.Millimeters;
            db.Ltscale = 2.5;
            db.Clayer = "GH_Layer";
            db.Angbase = Math.PI / 2;
            db.Limmax = new GeoPoint2(100, 200);
            db.Cecolor = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 3);
            db.Angdir = true;
            db.SetHeader("$USERI1", new DxfTypedValue(70, "42"));

            // As ezdxf reads them: $LTSCALE 2.5, $INSUNITS 4, $CLAYER GH_Layer, $ANGBASE 90.0 (degrees), $LIMMAX (100, 200).
            DxfDatabase again = DxfDatabase.Parse(db.ToText());
            Assert.Equal(DxfUnits.Millimeters, again.Insunits);
            Assert.Equal(2.5, again.Ltscale);
            Assert.Equal("GH_Layer", again.Clayer);
            Assert.True(again.LayerTable.Has("GH_Layer"));
            Assert.Equal(new[] { "50 90" }, again.GetHeader("$ANGBASE").Select(g => g.ToString()));
            Assert.Equal(Math.PI / 2, again.Angbase);
            Assert.Equal(new GeoPoint2(100, 200), again.Limmax);
            Assert.Equal(3, again.Cecolor.ColorIndex);
            Assert.True(again.Angdir);
            Assert.Equal("42", again.GetHeader("$USERI1").Single().Value);

            Assert.Throws<InvalidOperationException>(() => db.SetHeader("$HANDSEED", new DxfTypedValue(5, "FFFF")));
            Assert.Throws<ArgumentException>(() => db.SetHeader("LTSCALE", new DxfTypedValue(40, "1")));
            Assert.Throws<ArgumentException>(() => db.SetHeader("$LTSCALE"));
            Assert.Throws<ArgumentException>(() => db.Cecolor = DxfColor.FromRgb(1, 2, 3));
        }

        #endregion

        #region Extended data and colours

        [Fact]
        public void ExtendedData_IsSetApplicationByApplication_AsAutoCADSetsIt_ItsApplicationRegistered()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            DxfLine line = ByHandle<DxfLine>(db, "3E");
            IReadOnlyList<DxfTypedValue> probe = line.GetXDataForApplication("ghprobe");

            Assert.Equal("GHPROBE", probe[0].Value);
            line.XData = new[] { new DxfTypedValue(1001, "GH_APP"), new DxfTypedValue(1000, "hello"), new DxfTypedValue(1040, "2.5") };
            Assert.Equal(new[] { "GHPROBE", "GH_APP" }, line.XData.Where(g => g.TypeCode == 1001).Select(g => g.Value));
            line.XData = new[] { new DxfTypedValue(1001, "GHPROBE") };
            Assert.Null(line.GetXDataForApplication("GHPROBE"));
            Assert.Equal(new[] { "1001 GH_APP", "1000 hello", "1040 2.5" }, line.XData.Select(g => g.ToString()));
            Assert.Contains(Records(db.ToText()), r => r.StartsWith("  0\nAPPID\n", StringComparison.Ordinal) && r.Contains("\nGH_APP\n"));

            Assert.Throws<ArgumentException>(() => line.XData = new[] { new DxfTypedValue(1000, "no application") });
            Assert.Throws<ArgumentException>(() => line.XData = new[] { new DxfTypedValue(1001, "A"), new DxfTypedValue(8, "0") });

            // Given before the line is added, it is written with it.
            var made = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 1, 1)) { XData = new[] { new DxfTypedValue(1001, "GH_NEW"), new DxfTypedValue(1070, "7") } };
            db.ModelSpace.AppendEntity(made);
            Assert.Equal("7", ByHandle<DxfLine>(DxfDatabase.Parse(db.ToText()), made.Handle).GetXDataForApplication("GH_NEW")[1].Value);
        }

        [Fact]
        public void AColour_IsAsAutoCADGivesIt_AndAnEntitysColourAgreesWithItsIndexAndTrueColour()
        {
            Assert.Equal(new byte[] { 255, 0, 0 }, new[] { DxfColor.FromColorIndex(DxfColorMethod.ByAci, 1).Red, DxfColor.FromColorIndex(DxfColorMethod.ByAci, 1).Green, DxfColor.FromColorIndex(DxfColorMethod.ByAci, 1).Blue });
            Assert.Equal(30, DxfColor.FromRgb(255, 128, 0).ColorIndex);
            Assert.Equal(0xFF8000, DxfColor.FromRgb(255, 128, 0).ColorValue);
            Assert.True(DxfColor.ByLayer.IsByLayer);
            Assert.Equal(256, DxfColor.ByLayer.ColorIndex);
            Assert.Equal(DxfColor.ByBlock, DxfColor.FromColorIndex(DxfColorMethod.ByBlock, 5));

            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfLine line = ByHandle<DxfLine>(db, "3E");
            Assert.True(line.Color.IsByLayer);

            line.Color = DxfColor.FromRgb(255, 128, 0);
            Assert.Equal(0xFF8000, line.TrueColor);
            Assert.Equal(30, line.ColorIndex);
            Assert.True(line.Color.IsByColor);

            line.Color = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 4);
            Assert.Null(line.TrueColor);
            Assert.Equal(4, line.ColorIndex);
            Assert.Throws<InvalidOperationException>(() => ByHandle<DxfLine>(DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000), "3E").Color = DxfColor.FromRgb(1, 2, 3));
        }

        #endregion

        #region What the library keeps raw

        [Fact]
        public void AnEntityOfATypeWithNoClass_IsKeptRaw_ItsLookChanged_AndNotMoved()
        {
            string text = DxfFileEzdxfDrawings.R2000;
            DxfDatabase db = DxfDatabase.Parse(text);
            var hatch = (DxfUnknownEntity)db.ModelSpace.Single(e => e.Handle == "8E");

            Assert.Equal("IMAGE", hatch.DxfName);
            Assert.Equal(hatch.Groups.Count, hatch.Groups.Count);
            Assert.Empty(hatch.GetGeometry());
            Assert.Throws<NotSupportedException>(() => hatch.TransformBy(GeoTransform3.Translation(new GeoVector3(1, 0, 0))));
            Assert.Throws<NotSupportedException>(() => hatch.GetTransformedCopy(GeoTransform3.Translation(new GeoVector3(1, 0, 0))));
            Assert.IsType<DxfUnknownEntity>(hatch.GetTransformedCopy(GeoTransform3.Identity));

            hatch.ColorIndex = 2;
            hatch.Visible = false;
            hatch.LinetypeScale = 2.0;
            OnlyTheseChanged(text, db.ToText(), "8E");
            DxfUnknownEntity again = (DxfUnknownEntity)DxfDatabase.Parse(db.ToText()).ModelSpace.Single(e => e.Handle == "8E");
            Assert.Equal(2, again.ColorIndex);
            Assert.False(again.Visible);
            Assert.Equal(2.0, again.LinetypeScale);
        }

        [Fact]
        public void AnInsert_HasTheShapesItPlacesForItsGeometry()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            DxfDocument placed = DxfReader.Parse(DxfFileEzdxfDrawings.R2000, new DxfReadOptions { Tolerance = Tol });
            DxfEntity insert = db.ModelSpace.Single(e => e.Handle == "65");

            Assert.Equal(placed.Shapes.Count(s => s.InsertHandles.Count > 0 && s.InsertHandles[0] == "65"), insert.GetGeometry().Count);
        }

        #endregion

        #region Made in code

        [Fact]
        public void AnEntityMadeInCode_IsCheckedAgainstTheFileItIsAppendedTo()
        {
            DxfDatabase r12 = DxfDatabase.Parse(DxfFileEzdxfDrawings.R12);
            DxfDatabase r2000 = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);

            Assert.Throws<ArgumentException>(() => r12.ModelSpace.AppendEntity(new DxfLine { LineWeight = DxfLineWeight.LineWeight050 }));
            Assert.Throws<ArgumentException>(() => r2000.ModelSpace.AppendEntity(new DxfLine { TrueColor = 0xFF0000 }));
            Assert.Throws<ArgumentException>(() => r2000.ModelSpace.AppendEntity(new DxfLine { Linetype = "NOPE" }));
            Assert.Throws<ArgumentNullException>(() => r2000.ModelSpace.AppendEntity(null));
            Assert.Throws<ArgumentException>(() => new DxfLine { Layer = "a:b" });

            DxfCurve made = DxfCurve.CreateFromGeCurve(new GeoLine3(new GeoPoint3(1, 1, 1), new GeoPoint3(2, 2, 2)));
            Assert.IsType<DxfLine>(made);
            Assert.Null(made.Handle);
            Assert.Throws<ArgumentException>(() => DxfCurve.CreateFromGeCurve(new GeoPoint3(0, 0, 0)));
        }

        #endregion
    }
}
