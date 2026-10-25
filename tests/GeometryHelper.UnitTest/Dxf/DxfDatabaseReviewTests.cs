using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// What the review of the editing model found and AutoCAD does otherwise: model and paper space standing for their
    /// records from the first, AutoCAD's names for them, a true colour's index, erasing while walking, copies of R12 written
    /// in the forms of R13, what an R12 file refuses, the records that follow an entity as objects of their own, extended
    /// data with escapes, the look taken whole or not at all, and the MTEXT whose columns hide its direction.
    /// </summary>
    public class DxfDatabaseReviewTests
    {
        private static readonly string[] Drawings = { DxfFileEzdxfDrawings.R12, DxfFileEzdxfDrawings.R2000, DxfFileEzdxfDrawings.R2018 };

        #region Model and paper space

        [Fact]
        public void ModelSpace_StandsForItsRecordFromTheStart_ItsExtendedDataSavedWithoutTheBlockTableRead()
        {
            foreach (string text in Drawings)
            {
                DxfDatabase db = DxfDatabase.Parse(text);

                Assert.NotNull(db.ModelSpace.Handle);
                Assert.NotNull(db.PaperSpace.Handle);
                db.ModelSpace.XData = new[] { new DxfTypedValue(1001, "MSX"), new DxfTypedValue(1000, "v") };

                DxfDatabase reopened = DxfDatabase.Parse(db.ToText());
                Assert.Equal(new[] { "1001 MSX", "1000 v" }, reopened.ModelSpace.GetXDataForApplication("MSX").Select(g => g.ToString()));
                Assert.Same(reopened.ModelSpace, reopened.BlockTable["*Model_Space"]);
            }

            // The owner of the BLOCK_RECORD of model space is the block table, from R13 on.
            DxfDatabase r2018 = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            Assert.Equal(r2018.BlockTable.Handle, r2018.ModelSpace.OwnerHandle);
        }

        [Fact]
        public void PaperSpace_TakesAnOriginBeforeTheBlockTableIsRead()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            db.PaperSpace.Origin = new GeoPoint3(5, 5, 0);

            Assert.Equal(new GeoPoint3(5, 5, 0), db.PaperSpace.Origin);
            Assert.Equal(new GeoPoint3(5, 5, 0), DxfDatabase.Parse(db.ToText()).PaperSpace.Origin);
        }

        [Fact]
        public void ModelSpaceAndPaperSpace_HaveAutoCADsNames_HoweverTheFileCallsThem()
        {
            foreach (string text in Drawings)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfEntity first = db.ModelSpace.First();

                Assert.Equal("*Model_Space", db.ModelSpace.Name);
                Assert.Equal("*Paper_Space", db.PaperSpace.Name);
                Assert.Equal("*Model_Space", first.BlockName);
                Assert.Equal("*Model_Space", db.BlockTable.First().Name);
            }
        }

        [Fact]
        public void ModelSpaceOfAFileWithNoRecordOfIt_RefusesExtendedData()
        {
            string bare = "  0\nSECTION\n  2\nENTITIES\n  0\nLINE\n  8\n0\n 10\n0\n 20\n0\n 11\n1\n 21\n0\n  0\nENDSEC\n  0\nEOF\n";
            DxfDatabase db = DxfDatabase.Parse(bare);

            Assert.Null(db.ModelSpace.Handle);
            Assert.Throws<InvalidOperationException>(() => db.ModelSpace.XData = new[] { new DxfTypedValue(1001, "MSX") });
            Assert.Equal(bare, db.ToText());
        }

        #endregion

        #region Colours

        [Fact]
        public void ATrueColourCopiedThroughColor_IsWrittenWithTheIndexNearestIt()
        {
            var orange = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 0, 0)) { TrueColor = 0xFF8000 };
            var copy = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 0, 0)) { ColorIndex = 5 };

            Assert.Equal(30, orange.Color.ColorIndex);
            Assert.Equal(DxfColor.FromRgb(255, 128, 0), orange.Color);

            copy.Color = orange.Color;
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            db.ModelSpace.AppendEntity(copy);
            string record = Records(db.ToText()).Single(r => HandleOf(r) == copy.Handle);

            Assert.Contains("\n 62\n30\n", record, StringComparison.Ordinal);
            Assert.Contains("\n420\n16744448\n", record, StringComparison.Ordinal);
            Assert.Equal(30, copy.ColorIndex);
            Assert.Equal(0xFF8000, copy.TrueColor);
        }

        #endregion

        #region Walking and erasing

        [Fact]
        public void ErasingEveryEntityWhileWalkingTheBlock_ErasesThemAll_AsInAutoCAD()
        {
            foreach (string text in Drawings)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                int count = db.ModelSpace.Count;
                int walked = 0;

                foreach (DxfEntity entity in db.ModelSpace)
                {
                    entity.Erase();
                    walked++;
                }

                Assert.Equal(count, walked);
                Assert.Equal(0, db.ModelSpace.Count);
                Assert.Empty(DxfDatabase.Parse(db.ToText()).ModelSpace);
            }
        }

        [Fact]
        public void AnEntityErasedAheadOfTheWalk_IsPassedOver()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            List<DxfEntity> all = db.ModelSpace.ToList();
            var walked = new List<DxfEntity>();

            foreach (DxfEntity entity in db.ModelSpace)
            {
                if (walked.Count == 0)
                {
                    all[all.Count - 1].Erase();
                }

                walked.Add(entity);
            }

            Assert.Equal(all.Take(all.Count - 1), walked);
        }

        #endregion

        #region Copies of R12 in later drawings

        [Fact]
        public void ACopyOfALineOfR12_IsWrittenInTheFormsOfR13_InADrawingOfR2000OrR2018()
        {
            foreach (string later in new[] { DxfFileEzdxfDrawings.R2000, DxfFileEzdxfDrawings.R2018 })
            {
                DxfDatabase r12 = DxfDatabase.Parse(DxfFileEzdxfDrawings.R12);
                DxfLine line = ByHandle<DxfLine>(r12, "41");
                var copies = new List<DxfEntity>
                {
                    line.GetTransformedCopy(GeoTransform3.Identity),
                    line.GetOffsetCurves(1.0)[0],
                    line.GetSplitCurves(new[] { line.Length / 2 })[0],
                };

                DxfDatabase db = DxfDatabase.Parse(later);

                foreach (DxfEntity copy in copies)
                {
                    db.ModelSpace.AppendEntity(copy);

                    string record = Records(db.ToText()).Single(r => HandleOf(r) == copy.Handle);
                    Assert.StartsWith("  0\nLINE\n  5\n" + copy.Handle + "\n330\n" + db.ModelSpace.Handle + "\n100\nAcDbEntity\n", record, StringComparison.Ordinal);
                    Assert.Contains("\n100\nAcDbLine\n", record, StringComparison.Ordinal);
                    Assert.True(record.IndexOf("\n  8\n", StringComparison.Ordinal) < record.IndexOf("\n100\nAcDbLine\n", StringComparison.Ordinal));
                    Assert.True(record.IndexOf("\n100\nAcDbLine\n", StringComparison.Ordinal) < record.IndexOf("\n 10\n", StringComparison.Ordinal));
                }

                DxfDatabase reopened = DxfDatabase.Parse(db.ToText());
                Assert.Equal(Bits(((GeoLine3)copies[0].GetGeometry()[0]).StartPoint), Bits(((GeoLine3)ByHandle<DxfLine>(reopened, copies[0].Handle).GetGeometry()[0]).StartPoint));
            }
        }

        [Fact]
        public void EveryEntityOfR12_IsGivenAutoCADsSubclassMarkersInItsOrder_WhenCopiedIntoR2018()
        {
            // The entities of ezdxf's drawing of R12, each copied into one of R2018 at the level of its records: the markers
            // ezdxf writes for each, and the reader's shapes the same as those of the original.
            var expected = new Dictionary<string, string>
            {
                ["41"] = "AcDbEntity AcDbLine",
                ["42"] = "AcDbEntity AcDbCircle",
                ["43"] = "AcDbEntity AcDbCircle AcDbArc",
                ["44"] = "AcDbEntity AcDbPoint",
                ["45"] = "AcDbEntity AcDbFace",
                ["46"] = "AcDbEntity AcDbTrace",
                ["47"] = "AcDbEntity AcDb3dPolyline|AcDbEntity AcDbVertex AcDb3dPolylineVertex|AcDbEntity AcDbVertex AcDb3dPolylineVertex|AcDbEntity AcDbVertex AcDb3dPolylineVertex|AcDbEntity",
                ["4C"] = "AcDbEntity AcDb2dPolyline|AcDbEntity AcDbVertex AcDb2dVertex|AcDbEntity AcDbVertex AcDb2dVertex|AcDbEntity AcDbVertex AcDb2dVertex|AcDbEntity",
                ["67"] = "AcDbEntity AcDbText AcDbText",
                ["68"] = "AcDbEntity AcDbBlockReference|AcDbEntity AcDbText AcDbAttribute|AcDbEntity",
            };

            DxfDatabase r12 = DxfDatabase.Parse(DxfFileEzdxfDrawings.R12);
            DxfDatabase r2018 = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);

            foreach (KeyValuePair<string, string> entry in expected)
            {
                DxfEntity original = r12.ModelSpace.Single(e => e.Handle == entry.Key);
                DxfItem copy = r12.File.CopyOf(original.Item);
                DxfItem added = r2018.File.Adopt(r2018.File.ModelSpace, copy);
                string markers = string.Join("|", Enumerable.Range(0, r2018.File.RecordCount(added)).Select(i => string.Join(" ", Markers(r2018.File, added, i))));

                Assert.Equal(entry.Value, markers);
                Assert.Equal(original.GetGeometry().Count, DxfEntity.Of(added).GetGeometry().Count);
            }

            // Saved and opened again, each is read back as it was added.
            DxfDatabase reopened = DxfDatabase.Parse(r2018.ToText());
            Assert.Equal(r2018.ModelSpace.Count, reopened.ModelSpace.Count);
        }

        /// <summary>The subclass markers of one record of an entity, in their order.</summary>
        private static IEnumerable<string> Markers(DxfFile file, DxfItem item, int record)
        {
            DxfFile.Rec rec = file.RecordAt(item, record);
            string[] lines = Encoding.UTF8.GetString(rec.Bytes, rec.Start, rec.End - rec.Start).Replace("\r\n", "\n").Split('\n');

            for (int i = 0; i + 1 < lines.Length; i += 2)
            {
                if (lines[i].Trim() == "100")
                {
                    yield return lines[i + 1].Trim();
                }
            }
        }

        #endregion

        #region What a file of R12 refuses

        [Fact]
        public void AnEntityOfR12_RefusesALinetypeScaleOrVisibilityOfItsOwn_AsItRefusesALineweight()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R12);
            DxfLine line = ByHandle<DxfLine>(db, "41");
            string before = db.ToText();

            Assert.Throws<InvalidOperationException>(() => line.LinetypeScale = 2.0);
            Assert.Throws<InvalidOperationException>(() => line.Visible = false);
            Assert.Throws<InvalidOperationException>(() => line.LineWeight = DxfLineWeight.LineWeight050);
            line.LinetypeScale = 1.0;
            line.Visible = true;
            Assert.Equal(before, db.ToText());

            Assert.Throws<ArgumentException>(() => db.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 0, 0)) { LinetypeScale = 2.0 }));
            Assert.Throws<ArgumentException>(() => db.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 0, 0)) { Visible = false }));
            Assert.Equal(before, db.ToText());
        }

        [Fact]
        public void AHeaderOfR12_RefusesTheVariablesR2000Brought()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R12);
            string before = db.ToText();

            Assert.Throws<InvalidOperationException>(() => db.Celweight = DxfLineWeight.LineWeight050);
            Assert.Throws<InvalidOperationException>(() => db.Insunits = DxfUnits.Millimeters);
            Assert.Throws<InvalidOperationException>(() => db.LineWeightDisplay = true);
            Assert.Throws<InvalidOperationException>(() => db.Saveproxygraphics = 0);
            db.Celweight = db.Celweight;
            db.Insunits = db.Insunits;
            db.LineWeightDisplay = db.LineWeightDisplay;
            db.Saveproxygraphics = db.Saveproxygraphics;
            Assert.Equal(before, db.ToText());

            DxfDatabase r2000 = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            r2000.LineWeightDisplay = true;
            r2000.Saveproxygraphics = 0;
            DxfDatabase reopened = DxfDatabase.Parse(r2000.ToText());
            Assert.True(reopened.LineWeightDisplay);
            Assert.Equal(0, reopened.Saveproxygraphics);
            Assert.Equal("1", reopened.GetHeader("$LWDISPLAY")[0].Value);
        }

        #endregion

        #region The records that follow an entity

        [Fact]
        public void AVertex_IsOwnedByItsPolyline_NotByTheBlockItsRecordNames()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            DxfEntity polyline = db.ModelSpace.Single(e => e.Handle == "44");
            DxfEntity vertex = DxfEntity.Of(db.File.FollowerOf(polyline.Item, 1));

            Assert.Equal("46", vertex.Handle);
            Assert.Equal("44", vertex.OwnerHandle);
            Assert.Same(polyline, vertex.Owner);
            Assert.Equal("*Model_Space", vertex.BlockName);
            Assert.Same(vertex, DxfEntity.Of(db.File.FollowerOf(polyline.Item, 1)));
        }

        [Fact]
        public void TheLookAndExtendedDataOfAVertex_AreSavedInItsOwnRecord()
        {
            foreach (string text in new[] { DxfFileEzdxfDrawings.R2000, DxfFileEzdxfDrawings.R2018 })
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfEntity polyline = db.ModelSpace.First(e => e.Item.Type == "POLYLINE");
                DxfEntity vertex = DxfEntity.Of(db.File.FollowerOf(polyline.Item, 2));
                string handle = vertex.Handle;
                string layer = db.LayerTable.Select(l => l.Name).First(n => n != vertex.Layer);

                // A layer and an application the tables list, so that the vertex's record alone changes.
                vertex.Layer = layer;
                vertex.ColorIndex = 3;
                vertex.XData = new[] { new DxfTypedValue(1001, "EZDXF"), new DxfTypedValue(1000, "second") };

                OnlyTheseChanged(text, db.ToText(), handle);
                DxfDatabase reopened = DxfDatabase.Parse(db.ToText());
                DxfEntity again = DxfEntity.Of(reopened.File.FollowerOf(reopened.ModelSpace.Single(e => e.Handle == polyline.Handle).Item, 2));

                Assert.Equal(handle, again.Handle);
                Assert.Equal(layer, again.Layer);
                Assert.Equal(3, again.ColorIndex);
                Assert.Equal(new[] { "1001 EZDXF", "1000 second" }, again.XData.Select(g => g.ToString()));
                Assert.Equal(polyline.Layer, reopened.ModelSpace.Single(e => e.Handle == polyline.Handle).Layer);
            }
        }

        [Fact]
        public void AVertexPutInOrTakenOut_LeavesEveryOtherVertexItsHandle()
        {
            foreach (string text in Drawings)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfEntity polyline = db.ModelSpace.First(e => e.Item.Type == "POLYLINE");
                int count = db.File.RecordCount(polyline.Item);
                List<DxfEntity> before = Enumerable.Range(1, count - 1).Select(i => DxfEntity.Of(db.File.FollowerOf(polyline.Item, i))).ToList();
                string[] handles = before.Select(v => v.Handle).ToArray();

                // A vertex made in code put in second: (7, 8) of a 2D polyline.
                var groups = new List<DxfTypedValue> { new DxfTypedValue(100, "AcDbVertex"), new DxfTypedValue(100, "AcDb2dVertex"), new DxfTypedValue(10, "7"), new DxfTypedValue(20, "8"), new DxfTypedValue(30, "0") };
                var made = new DxfUnknownEntity(DxfFile.Detached("VERTEX", groups));
                made.Bind(db.File.InsertFollower(polyline.Item, 2, made.Item));

                Assert.Same(made, DxfEntity.Of(db.File.FollowerOf(polyline.Item, 2)));
                Assert.Equal(2, made.Item.Index);
                Assert.Equal(3, before[1].Item.Index);
                Assert.Same(polyline, made.Owner);
                Assert.Equal(handles, before.Select(v => v.Handle));
                Assert.NotNull(made.Handle);
                Assert.DoesNotContain(made.Handle, handles);

                // The first vertex taken out: the others keep their handles and objects.
                before[0].Erase();
                Assert.True(before[0].IsErased);
                Assert.Same(made, DxfEntity.Of(db.File.FollowerOf(polyline.Item, 1)));
                Assert.Equal(handles.Skip(1), before.Skip(1).Select(v => v.Handle));
                Assert.Throws<InvalidOperationException>(() => before[0].Erase());
                Assert.Throws<InvalidOperationException>(() => before[before.Count - 1].Erase());

                DxfDatabase reopened = DxfDatabase.Parse(db.ToText());
                DxfEntity again = reopened.ModelSpace.Single(e => e.Handle == polyline.Handle);
                Assert.Equal(count, reopened.File.RecordCount(again.Item));
                Assert.Equal("VERTEX", reopened.File.RecordType(again.Item, 1));
                Assert.Equal(handles.Skip(1), Enumerable.Range(2, count - 2).Select(i => DxfEntity.Of(reopened.File.FollowerOf(again.Item, i)).Handle));
            }
        }

        [Fact]
        public void EveryVertexOfALongPolylineSetInOneWriting_TakesOnePass()
        {
            // 2 000 vertices, each moved by one: one writing of the polyline, not 2 000.
            var points = Enumerable.Range(0, 2000).Select(i => new GeoPoint3(i, i % 7, 0)).ToList();
            var writer = new DxfWriter();
            writer.Add(new GeoPolyline3(points));
            DxfDatabase db = DxfDatabase.Parse(writer.ToString());
            DxfEntity polyline = db.ModelSpace.Single();
            int count = db.File.RecordCount(polyline.Item);
            var order = Array.AsReadOnly(new[] { 100, 100, 10, 20, 30, 70 });
            var values = new List<KeyValuePair<int, IList<KeyValuePair<int, string>>>>();

            for (int i = 1; i < count - 1; i++)
            {
                values.Add(new KeyValuePair<int, IList<KeyValuePair<int, string>>>(i, new[] { new KeyValuePair<int, string>(20, (points[i - 1].Y + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)) }));
            }

            DxfEntity last = DxfEntity.Of(db.File.FollowerOf(polyline.Item, count - 2));
            string handle = last.Handle;
            var watch = Stopwatch.StartNew();
            db.File.SetValuesAtMany(polyline.Item, values, order, true);
            watch.Stop();

            Assert.True(watch.Elapsed.TotalSeconds < 2.0, "The writing took " + watch.Elapsed.TotalSeconds + " s.");
            Assert.Equal(handle, last.Handle);
            Assert.Equal("1", db.File.ValueOf(db.File.FollowerOf(polyline.Item, 1), 20));
            GeoPolyline3 read = Assert.IsType<GeoPolyline3>(DxfReader.Parse(db.ToText()).Shapes.Single().Shape);
            Assert.Equal(points.Select(p => p.Y + 1), read.Vertices.Select(p => p.Y));
        }

        #endregion

        #region Extended data, the look, defaults and copies

        [Fact]
        public void ATextOfExtendedDataWithLettersTheCodePageHasNot_ComesBackAsItWasSet()
        {
            string name = "x" + (char)0x110 + "y" + (char)0xE9;
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R12);
            var line = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 0, 0));
            line.XData = new[] { new DxfTypedValue(1001, "APP" + (char)0xE9), new DxfTypedValue(1000, name) };
            db.ModelSpace.AppendEntity(line);

            Assert.Equal(new[] { "APP" + (char)0xE9, name }, line.XData.Select(g => g.Value));
            DxfDatabase reopened = Opened(Saved(db));
            DxfLine again = ByHandle<DxfLine>(reopened, line.Handle);
            Assert.Equal(new[] { "APP" + (char)0xE9, name }, again.XData.Select(g => g.Value));

            // Set to what it reads, it changes nothing, the escapes kept as they are.
            byte[] before = Saved(reopened);
            again.XData = again.XData;
            Assert.Equal(before, Saved(reopened));
        }

        [Fact]
        public void TheLookOfAnother_IsTakenWithTheNameOfItsColourBook_OrNotAtAll()
        {
            var source = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 0, 0)) { Layer = "brass", TrueColor = 0xB5A642, LinetypeScale = 2.0 };
            source.Item.File.SetCommon(source.Item, 430, "RAL$RAL 1000");

            DxfDatabase r2018 = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfLine target = r2018.ModelSpace.OfType<DxfLine>().First();
            target.SetPropertiesFrom(source);
            Assert.Equal("RAL$RAL 1000", target.Color.ColorName);
            Assert.Equal(0xB5A642, target.TrueColor);
            Assert.Equal("brass", target.Layer);

            // A file of R12 holds no true colour: nothing is written, the layer not either.
            DxfDatabase r12 = DxfDatabase.Parse(DxfFileEzdxfDrawings.R12);
            DxfLine old = ByHandle<DxfLine>(r12, "41");
            string before = r12.ToText();
            Assert.Throws<InvalidOperationException>(() => old.SetPropertiesFrom(source));
            Assert.Equal(before, r12.ToText());
            Assert.NotEqual("brass", old.Layer);
        }

        [Fact]
        public void SetDatabaseDefaults_WithNoDrawingGiven_TakesThatOfTheEntity()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            var line = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 0, 0));

            Assert.Throws<InvalidOperationException>(() => line.SetDatabaseDefaults());
            db.Clayer = "current";
            db.ModelSpace.AppendEntity(line);
            line.SetDatabaseDefaults();
            Assert.Equal("current", line.Layer);
        }

        [Fact]
        public void AnErasedEntity_IsNotCopied()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            DxfLine line = ByHandle<DxfLine>(db, "3E");
            line.Erase();

            Assert.Throws<InvalidOperationException>(() => line.GetTransformedCopy(GeoTransform3.Identity));
            Assert.Throws<InvalidOperationException>(() => line.GetOffsetCurves(1.0));
        }

        [Fact]
        public void ALinetypesComments_AreItsAsciiDescription()
        {
            var dashed = new DxfLinetypeTableRecord { Name = "DASHED2", Comments = "__ __" };
            Assert.Equal("__ __", dashed.AsciiDescription);

            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            DxfLinetypeTableRecord last = db.LinetypeTable.Last();
            Assert.Equal(last.AsciiDescription, last.Comments);
            last.Comments = "dash dot";
            Assert.Equal("dash dot", last.AsciiDescription);
            Assert.Equal("dash dot", DxfDatabase.Parse(db.ToText()).LinetypeTable[last.Name].Comments);
        }

        #endregion

        #region AutoCAD's names

        [Fact]
        public void TheNames_AreAutoCADs()
        {
            // TypedValue: TypeCode a short, as AutoCAD's is.
            var group = new DxfTypedValue(1040, "2.5");
            Assert.Equal((short)1040, group.TypeCode);
            Assert.Equal(typeof(short), typeof(DxfTypedValue).GetProperty(nameof(DxfTypedValue.TypeCode)).PropertyType);

            // UnitsValue, names and numbers.
            string[] names =
            {
                "Undefined", "Inches", "Feet", "Miles", "Millimeters", "Centimeters", "Meters", "Kilometers", "MicroInches", "Mils",
                "Yards", "Angstroms", "Nanometers", "Microns", "Decimeters", "Dekameters", "Hectometers", "Gigameters",
                "Astronomical", "LightYears", "Parsecs", "USSurveyFeet", "USSurveyInch", "USSurveyYard", "USSurveyMile",
            };
            Assert.Equal(names, Enumerable.Range(0, 25).Select(i => ((DxfUnits)i).ToString()));
            Assert.Equal(names.Length, Enum.GetValues(typeof(DxfUnits)).Length);

            // The reader's types spelled as AutoCAD spells them.
            var shape = new DxfShape(new GeoPoint3(1, 2, 3), "0", color: 1, trueColor: 0xFF8000, lineWeight: 25);
            Assert.Equal(1, shape.Color);
            Assert.Equal(0xFF8000, shape.TrueColor);
            Assert.Equal(25, shape.LineWeight);
            Assert.True(new DxfReadOptions { ToMillimeters = true }.ToMillimeters);
            Assert.NotNull(typeof(DxfLayer).GetProperty("Color"));
            Assert.NotNull(typeof(DxfLayer).GetProperty("TrueColor"));
            Assert.NotNull(typeof(DxfLayer).GetProperty("LineWeight"));
        }

        #endregion

        #region An MTEXT with columns

        [Fact]
        public void AnMTextWithColumns_IsReadFromItsOwnGroups_NotThoseOfTheObjectEmbeddedInIt()
        {
            // An MTEXT of R2018 turned by 30 degrees (50) with no direction (11) and no width (41) of its own: the object of its
            // columns, embedded after its 101, has a direction (10), the insertion point (11) and a height (41) of its own.
            string text = string.Join("\n", new[]
            {
                "  0", "SECTION", "  2", "HEADER", "  9", "$ACADVER", "  1", "AC1032", "  0", "ENDSEC",
                "  0", "SECTION", "  2", "ENTITIES",
                "  0", "MTEXT", "  5", "2A", "100", "AcDbEntity", "  8", "0", "100", "AcDbMText",
                " 10", "5.0", " 20", "6.0", " 30", "0.0", " 40", "2.5", " 71", "1", " 72", "5", "  1", "Two columns", " 50", "30.0",
                "101", "Embedded Object", " 70", "1", " 10", "1.0", " 20", "0.0", " 30", "0.0", " 11", "5.0", " 21", "6.0", " 31", "0.0",
                " 40", "50.0", " 41", "12.0", " 42", "110.0", " 43", "12.0", " 71", "2", " 72", "2", " 44", "50.0", " 45", "10.0", " 73", "0", " 74", "0",
                "  0", "ENDSEC", "  0", "EOF", string.Empty,
            });

            DxfText read = Assert.IsType<DxfText>(DxfReader.Parse(text).Shapes.Single().Shape);

            Assert.Equal(Bits(DxfText.Turn(30.0 * Math.PI / 180.0)), Bits(read.Rotation));
            Assert.Equal(0.0, read.Width);
            Assert.Equal(new GeoPoint3(5, 6, 0), read.Position);
            Assert.Equal("Two columns", read.Value);
        }

        #endregion
    }
}
