using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// A drawing of dynamic blocks inserted into another (D23c), as AutoCAD's Insert keeps them dynamic: the block's extended
    /// data and its evaluation graph, the anonymous blocks standing for its INSERTs and each INSERT's representation data
    /// copied, every handle among them taken to the copies in the drawing inserted into; the INSERTs read the values they had,
    /// and are set as the INSERTs they were copied from are.
    /// </summary>
    public class DxfDynamicBlockD23cTests
    {
        private const double Close = 1E-9;

        private static readonly Tolerance Exact = new Tolerance(1E-9, 1E-9);

        /// <summary>
        /// A drawing of R2018 with the dynamic block GH_PANEL, a rectangle 100 by 50 of four lines given Width from (0, 0) to
        /// (100, 0), from 50 to 300 by 10, and the stretch of its right edge; an INSERT of it at the origin at its own width, and
        /// one at (0, 200) set to 200, which places an anonymous block.
        /// </summary>
        private static DxfDatabase Source()
        {
            var db = new DxfDatabase("AC1032");
            DxfBlockTableRecord block = Panel(db, "GH_PANEL");
            db.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(0, 0, 0), block));
            var set = new DxfBlockReference(new GeoPoint3(0, 200, 0), block);
            db.ModelSpace.AppendEntity(set);
            Width(set).Value = 200.0;
            return db;
        }

        /// <summary>The dynamic block of a name in a drawing, as <see cref="Source"/> says.</summary>
        private static DxfBlockTableRecord Panel(DxfDatabase db, string name)
        {
            var block = new DxfBlockTableRecord { Name = name, Origin = new GeoPoint3(0, 0, 0) };
            db.BlockTable.Add(block);
            var lines = new[]
            {
                new DxfLine(new GeoPoint3(100, 0, 0), new GeoPoint3(100, 50, 0)),
                new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)),
                new DxfLine(new GeoPoint3(0, 50, 0), new GeoPoint3(100, 50, 0)),
                new DxfLine(new GeoPoint3(0, 50, 0), new GeoPoint3(0, 0, 0)),
            };

            foreach (DxfLine line in lines)
            {
                block.AppendEntity(line);
            }

            DxfBlockLinearParameter width = block.AddLinearParameter(
                "Width",
                "Overall width of the panel",
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(100, 0, 0),
                -20,
                DxfBlockParameterValueSet.Increments(50, 300, 10)
            );
            block.AddStretchAction(width, new GeoPoint2(90, -10), new GeoPoint2(110, 60), lines.Take(3));
            return block;
        }

        private static DxfDynamicBlockReferenceProperty Width(DxfBlockReference insert) =>
            insert.DynamicBlockReferencePropertyCollection.Single(p => p.PropertyName == "Width");

        private static double Real(DxfDynamicBlockReferenceProperty property) => Assert.IsType<double>(property.Value);

        private static DxfDatabase Reopened(DxfDatabase db) => DxfDatabase.Parse(db.ToText());

        /// <summary>The INSERTs of model space that place GH_PANEL or a block standing for it, in their order.</summary>
        private static List<DxfBlockReference> Panels(IEnumerable<DxfEntity> space) =>
            space
                .OfType<DxfBlockReference>()
                .Where(r => r.Name == "GH_PANEL" || r.Name.StartsWith("*U", StringComparison.Ordinal))
                .ToList();

        /// <summary>The ends of the lines of the block an INSERT places, in their order.</summary>
        private static List<GeoPoint3> Drawn(DxfBlockReference insert) =>
            insert.BlockTableRecord.OfType<DxfLine>().SelectMany(l => new[] { l.StartPoint, l.EndPoint }).ToList();

        [Fact]
        public void ADynamicBlockAndItsInserts_InsertedIntoAnotherDrawing_StayDynamic_WithTheValuesTheyHad()
        {
            var db = new DxfDatabase("AC1032");
            db.Insert(GeoTransform3.Identity, Source(), true);
            db = Reopened(db);

            DxfBlockTableRecord block = db.BlockTable["GH_PANEL"];
            Assert.True(block.IsDynamicBlock);

            List<DxfBlockReference> inserts = Panels(db.ModelSpace);
            Assert.Equal(2, inserts.Count);
            Assert.All(inserts, r => Assert.True(r.IsDynamicBlock));
            Assert.All(inserts, r => Assert.Equal("GH_PANEL", r.DynamicBlockTableRecord.Name));
            Assert.Equal(100.0, Real(Width(inserts[0])), Close);
            Assert.Equal(200.0, Real(Width(inserts[1])), Close);
            Assert.Equal("Overall width of the panel", Width(inserts[1]).Description);
            Assert.Null(inserts[0].AnonymousBlockTableRecord);
            Assert.True(inserts[1].AnonymousBlockTableRecord.IsAnonymous);
            Assert.Equal(
                new[] { inserts[1].AnonymousBlockTableRecord.Name },
                block.GetAnonymousBlocks().Select(b => b.Name)
            );
        }

        [Fact]
        public void TheBlockKeepsItsTrueNameGuidAndTags_AndEachEntityItsTagNamingItsOwnCopy()
        {
            DxfDatabase source = Source();
            var db = new DxfDatabase("AC1032");
            db.Insert(GeoTransform3.Identity, source, true);
            db = Reopened(db);

            DxfBlockTableRecord theirs = source.BlockTable["GH_PANEL"];
            DxfBlockTableRecord ours = db.BlockTable["GH_PANEL"];

            foreach (
                string application in new[] { "AcDbDynamicBlockTrueName", "AcDbDynamicBlockGUID", "AcDbBlockRepETag" }
            )
            {
                Assert.Equal(
                    theirs.GetXDataForApplication(application).Select(g => g.ToString()),
                    ours.GetXDataForApplication(application)?.Select(g => g.ToString())
                );
            }

            List<DxfEntity> drawn = ours.Cast<DxfEntity>().ToList();
            List<DxfEntity> was = theirs.Cast<DxfEntity>().ToList();
            Assert.Equal(was.Count, drawn.Count);

            for (int i = 0; i < drawn.Count; i++)
            {
                IReadOnlyList<DxfTypedValue> tag = drawn[i].GetXDataForApplication("AcDbBlockRepETag");
                Assert.Equal(was[i].GetXDataForApplication("AcDbBlockRepETag")[2].Value, tag[2].Value);
                Assert.Equal(drawn[i].Handle, tag[3].Value);
            }

            IReadOnlyList<DxfTypedValue> represents = Panels(db.ModelSpace)[1]
                .AnonymousBlockTableRecord.GetXDataForApplication("AcDbBlockRepBTag");
            Assert.Equal(ours.Handle, represents.Single(g => g.TypeCode == 1005).Value);
        }

        [Fact]
        public void EveryPointerOfTheCopiedObjects_NamesInTheTargetWhatTheSourcesNamedThere()
        {
            DxfDatabase source = Source();
            var db = new DxfDatabase("AC1032");
            db.Insert(GeoTransform3.Identity, source, true);

            Dictionary<string, Rec> theirs = Records(source.ToText());
            Dictionary<string, Rec> ours = Records(db.ToText());

            // The block's graph and purge preventer, the anonymous block's tag, and each INSERT's representation, object by
            // object in the order they are owned, every group as the source has it and every pointer naming the same kind of
            // record in the same block: no handle of the source's left, none naming nothing.
            Assert.NotEmpty(Shape(ours, Xdictionary(ours, "BLOCK_RECORD", "GH_PANEL")));
            Assert.Equal(
                Shape(theirs, Xdictionary(theirs, "BLOCK_RECORD", "GH_PANEL")),
                Shape(ours, Xdictionary(ours, "BLOCK_RECORD", "GH_PANEL"))
            );

            List<DxfBlockReference> was = Panels(source.ModelSpace);
            List<DxfBlockReference> now = Panels(db.ModelSpace);
            Assert.Equal(
                Shape(theirs, Xdictionary(theirs, was[1].Handle)),
                Shape(ours, Xdictionary(ours, now[1].Handle))
            );
            Assert.Equal(
                theirs[was[1].AnonymousBlockTableRecord.Handle].Data(theirs),
                ours[now[1].AnonymousBlockTableRecord.Handle].Data(ours)
            );

            // Nothing of the whole drawing names a handle it has not.
            Assert.Empty(Dangling(ours));
        }

        [Fact]
        public void TheClassesAndApplicationsTheCopiedObjectsNeed_AreListedInTheTarget()
        {
            var db = new DxfDatabase("AC1032");
            db.Insert(GeoTransform3.Identity, Source(), true);
            string text = db.ToText();

            foreach (
                string name in new[]
                {
                    "ACAD_EVALUATION_GRAPH",
                    "ACDB_DYNAMICBLOCKPURGEPREVENTER_VERSION",
                    "BLOCKLINEARPARAMETER",
                    "BLOCKLINEARGRIP",
                    "BLOCKGRIPLOCATIONCOMPONENT",
                    "BLOCKSTRETCHACTION",
                    "ACDB_BLOCKREPRESENTATION_DATA",
                }
            )
            {
                Assert.Contains("  0\nCLASS\n  1\n" + name + "\n", text);
            }

            foreach (
                string application in new[]
                {
                    "AcDbDynamicBlockTrueName",
                    "AcDbDynamicBlockGUID",
                    "AcDbBlockRepETag",
                    "AcDbBlockRepBTag",
                    "ACAUTHENVIRON",
                }
            )
            {
                Assert.Contains(Records(text).Values, r =>
                    r.Type == "APPID" && r.Groups.Exists(g => g.Key == 2 && g.Value.Trim() == application)
                );
            }
        }

        [Fact]
        public void ACopiedInsertSetToAnotherWidth_StretchesItsBlockAsTheInsertItWasCopiedFrom()
        {
            DxfDatabase source = Source();
            var db = new DxfDatabase("AC1032");
            db.Insert(GeoTransform3.Identity, source, true);

            for (int i = 0; i < 2; i++)
            {
                Width(Panels(source.ModelSpace)[i]).Value = 270.0;
                Width(Panels(db.ModelSpace)[i]).Value = 270.0;
            }

            db = Reopened(db);
            source = Reopened(source);

            for (int i = 0; i < 2; i++)
            {
                DxfBlockReference copy = Panels(db.ModelSpace)[i];
                Assert.Equal(270.0, Real(Width(copy)), Close);
                Assert.Equal(Drawn(Panels(source.ModelSpace)[i]), Drawn(copy));
                Assert.Contains(Drawn(copy), p => p.IsEqualTo(new GeoPoint3(270, 50, 0), Exact));
            }
        }

        [Fact]
        public void AnAnonymousBlockPlacedByTwoInserts_IsCopiedOnce_AndBothStayDynamic()
        {
            // As AutoCAD's own save of 24a-d holds it: two INSERTs of one *U, which the copy brings once for both (D23d), as
            // AutoCAD's own Insert keeps it.
            DxfDatabase source = Source();
            DxfBlockReference set = Panels(source.ModelSpace)[1];
            source.ModelSpace.AppendEntity(
                new DxfBlockReference(new GeoPoint3(0, 400, 0), set.AnonymousBlockTableRecord)
            );
            var db = new DxfDatabase("AC1032");
            db.Insert(GeoTransform3.Identity, source, true);
            db = Reopened(db);

            List<DxfBlockReference> was = Panels(source.ModelSpace);
            List<DxfBlockReference> now = Panels(db.ModelSpace);
            Assert.Equal(3, now.Count);
            Assert.All(now, r => Assert.True(r.IsDynamicBlock));

            for (int i = 0; i < 3; i++)
            {
                Assert.Equal(Real(Width(was[i])), Real(Width(now[i])), Close);
                Assert.Equal(Drawn(was[i]), Drawn(now[i]));
            }
        }

        [Fact]
        public void ADrawingInsertedMoved_KeepsItsInsertsDynamic_TheirValuesInTheBlocksOwnCoordinates()
        {
            var db = new DxfDatabase("AC1032");
            db.Insert(GeoTransform3.Translation(new GeoVector3(1000, 0, 0)), Source(), true);

            List<DxfBlockReference> inserts = Panels(db.ModelSpace);
            Assert.True(inserts[1].Position.IsEqualTo(new GeoPoint3(1000, 200, 0), Exact));
            Assert.Equal(200.0, Real(Width(inserts[1])), Close);

            Width(inserts[1]).Value = 150.0;
            DxfBlockReference back = Panels(Reopened(db).ModelSpace)[1];
            Assert.True(back.IsDynamicBlock);
            Assert.Equal(150.0, Real(Width(back)), Close);
            Assert.Contains(Drawn(back), p => p.IsEqualTo(new GeoPoint3(150, 0, 0), Exact));
        }

        [Fact]
        public void ADrawingMadeABlock_HoldsDynamicInserts()
        {
            var db = new DxfDatabase("AC1032");
            DxfBlockTableRecord sheet = db.Insert("SHEET", Source(), true);
            db.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(0, 0, 0), sheet));
            db = Reopened(db);

            List<DxfBlockReference> inserts = Panels(db.BlockTable["SHEET"]);
            Assert.Equal(2, inserts.Count);
            Assert.All(inserts, r => Assert.True(r.IsDynamicBlock));
            Assert.Equal(100.0, Real(Width(inserts[0])), Close);
            Assert.Equal(200.0, Real(Width(inserts[1])), Close);
        }

        [Fact]
        public void ADynamicInsertInABlock_IsCopiedWithThatBlock_AndStaysDynamic()
        {
            var source = new DxfDatabase("AC1032");
            DxfBlockTableRecord panel = Panel(source, "GH_PANEL");
            var holder = new DxfBlockTableRecord { Name = "HOLDER", Origin = new GeoPoint3(0, 0, 0) };
            source.BlockTable.Add(holder);
            var held = new DxfBlockReference(new GeoPoint3(10, 0, 0), panel);
            holder.AppendEntity(held);
            Width(held).Value = 250.0;
            source.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(0, 0, 0), holder));

            var db = new DxfDatabase("AC1032");
            db.Insert(GeoTransform3.Identity, source, true);
            db = Reopened(db);

            DxfBlockReference copy = Panels(db.BlockTable["HOLDER"]).Single();
            Assert.True(copy.IsDynamicBlock);
            Assert.Equal("GH_PANEL", copy.DynamicBlockTableRecord.Name);
            Assert.Equal(250.0, Real(Width(copy)), Close);
        }

        [Fact]
        public void ATargetWithTheSameDynamicBlock_KeepsItsOwn_AndTheCopiedInsertsPlaceIt()
        {
            var db = new DxfDatabase("AC1032");
            DxfBlockTableRecord own = Panel(db, "GH_PANEL");
            db.Insert(GeoTransform3.Identity, Source(), true);
            db = Reopened(db);

            Assert.Equal(own.Handle, db.BlockTable["GH_PANEL"].Handle);
            List<DxfBlockReference> inserts = Panels(db.ModelSpace);
            Assert.All(inserts, r => Assert.True(r.IsDynamicBlock));
            Assert.All(inserts, r => Assert.Equal(own.Handle, r.DynamicBlockTableRecord.Handle));
            Assert.Equal(100.0, Real(Width(inserts[0])), Close);
            Assert.Equal(200.0, Real(Width(inserts[1])), Close);
        }

        [Fact]
        public void ATargetWithAStaticBlockOfTheName_KeepsItsOwn_AndTheCopiedInsertsComeStatic()
        {
            var db = new DxfDatabase("AC1032");
            var own = new DxfBlockTableRecord { Name = "GH_PANEL", Origin = new GeoPoint3(0, 0, 0) };
            db.BlockTable.Add(own);
            own.AppendEntity(new DxfCircle(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), 5));
            db.Insert(GeoTransform3.Identity, Source(), true);
            db = Reopened(db);

            Assert.False(db.BlockTable["GH_PANEL"].IsDynamicBlock);
            Assert.Single(db.BlockTable["GH_PANEL"].OfType<DxfCircle>());
            List<DxfBlockReference> inserts = Panels(db.ModelSpace);
            Assert.Equal(2, inserts.Count);
            Assert.All(inserts, r => Assert.False(r.IsDynamicBlock));
            Assert.Empty(Dangling(Records(db.ToText())));
        }

        [Fact]
        public void IntoADrawingOfR2000_TheDynamicBlockComesStatic_AsBefore()
        {
            var db = new DxfDatabase("AC1015");
            db.Insert(GeoTransform3.Identity, Source(), true);
            db = Reopened(db);

            Assert.False(db.BlockTable["GH_PANEL"].IsDynamicBlock);
            Assert.All(Panels(db.ModelSpace), r => Assert.False(r.IsDynamicBlock));
            Assert.Equal(new GeoPoint3(200, 0, 0), Drawn(Panels(db.ModelSpace)[1])[0], new PointComparer());
        }

        [Fact]
        public void AnInsertAsAutoCadSavedIt_WithReactorsAndAHistory_IsCopiedWhole_AndReadsTheSameValue()
        {
            DxfDatabase source = DxfDatabase.Parse(AutoCadLike());
            Assert.Equal(750.0, Real(Width(Panels(source.ModelSpace)[1])), Close);

            var db = new DxfDatabase("AC1032");
            db.Insert(GeoTransform3.Identity, source, true);

            Dictionary<string, Rec> theirs = Records(source.ToText());
            Dictionary<string, Rec> ours = Records(db.ToText());
            Assert.Equal(Shape(theirs, "1022"), Shape(ours, Xdictionary(ours, Panels(db.ModelSpace)[1].Handle)));
            Assert.Empty(Dangling(ours));

            db = Reopened(db);
            List<DxfBlockReference> inserts = Panels(db.ModelSpace);
            Assert.All(inserts, r => Assert.True(r.IsDynamicBlock));
            Assert.Equal(500.0, Real(Width(inserts[0])), Close);
            Assert.Equal(750.0, Real(Width(inserts[1])), Close);
        }

        private sealed class PointComparer : IEqualityComparer<GeoPoint3>
        {
            public bool Equals(GeoPoint3 x, GeoPoint3 y) => x.IsEqualTo(y, Exact);

            public int GetHashCode(GeoPoint3 obj) => 0;
        }

        private static string G(int code, string value) =>
            code.ToString(CultureInfo.InvariantCulture).PadLeft(3) + "\n" + value + "\n";

        private static string G(int code, double value) => G(code, value.ToString("R", CultureInfo.InvariantCulture));

        private static string P(int code, double x, double y) => G(code, x) + G(code + 10, y) + G(code + 20, 0.0);

        /// <summary>
        /// The R2018 drawing of ezdxf with the dynamic block GH_PANEL as AutoCAD saves one, its names and numbers made up: its graph
        /// of one linear parameter (node 7) from (0, 0) to (500, 0), a line in it; an INSERT of GH_PANEL itself (1020) and one of
        /// its anonymous block *U8 (1021), whose representation keeps 750 and a history, each object listing its owner among
        /// its reactors, as AutoCAD writes them.
        /// </summary>
        private static string AutoCadLike()
        {
            string Reactors(string owner) => G(102, "{ACAD_REACTORS") + G(330, owner) + G(102, "}") + G(330, owner);

            string records =
                G(0, "BLOCK_RECORD")
                + G(5, "1001")
                + G(102, "{ACAD_XDICTIONARY")
                + G(360, "1002")
                + G(102, "}")
                + G(330, "9")
                + G(100, "AcDbSymbolTableRecord")
                + G(100, "AcDbBlockTableRecord")
                + G(2, "GH_PANEL")
                + G(340, "0")
                + G(70, "0")
                + G(280, "1")
                + G(281, "0")
                + G(102, "{BLKREFS")
                + G(331, "1020")
                + G(102, "}")
                + G(1001, "AcDbBlockRepETag")
                + G(1070, "1")
                + G(1071, "2")
                + G(1001, "AcDbDynamicBlockTrueName")
                + G(1000, "GH_PANEL")
                + G(1001, "AcDbDynamicBlockGUID")
                + G(1000, "{0D23C23C-0000-4000-8000-00000000D23C}")
                + G(0, "BLOCK_RECORD")
                + G(5, "1010")
                + G(102, "{ACAD_XDICTIONARY")
                + G(360, "1014")
                + G(102, "}")
                + G(330, "9")
                + G(100, "AcDbSymbolTableRecord")
                + G(100, "AcDbBlockTableRecord")
                + G(2, "*U8")
                + G(340, "0")
                + G(70, "0")
                + G(280, "1")
                + G(281, "0")
                + G(102, "{BLKREFS")
                + G(331, "1021")
                + G(102, "}")
                + G(1001, "AcDbBlockRepBTag")
                + G(1070, "1")
                + G(1005, "1001");
            string blocks =
                G(0, "BLOCK")
                + G(5, "1003")
                + G(330, "1001")
                + G(100, "AcDbEntity")
                + G(8, "0")
                + G(100, "AcDbBlockBegin")
                + G(2, "GH_PANEL")
                + G(70, "0")
                + P(10, 0, 0)
                + G(3, "GH_PANEL")
                + G(1, string.Empty)
                + G(0, "LINE")
                + G(5, "1004")
                + G(330, "1001")
                + G(100, "AcDbEntity")
                + G(8, "0")
                + G(100, "AcDbLine")
                + P(10, 0, 0)
                + P(11, 500, 0)
                + G(1001, "AcDbBlockRepETag")
                + G(1070, "1")
                + G(1071, "1")
                + G(1005, "1004")
                + G(0, "ENDBLK")
                + G(5, "1005")
                + G(330, "1001")
                + G(100, "AcDbEntity")
                + G(8, "0")
                + G(100, "AcDbBlockEnd")
                + G(0, "BLOCK")
                + G(5, "1011")
                + G(330, "1010")
                + G(100, "AcDbEntity")
                + G(8, "0")
                + G(100, "AcDbBlockBegin")
                + G(2, "*U8")
                + G(70, "1")
                + P(10, 0, 0)
                + G(3, "*U8")
                + G(1, string.Empty)
                + G(0, "LINE")
                + G(5, "1012")
                + G(330, "1010")
                + G(100, "AcDbEntity")
                + G(8, "0")
                + G(100, "AcDbLine")
                + P(10, 0, 0)
                + P(11, 750, 0)
                + G(1001, "AcDbBlockRepETag")
                + G(1070, "1")
                + G(1071, "1")
                + G(1005, "1012")
                + G(0, "ENDBLK")
                + G(5, "1013")
                + G(330, "1010")
                + G(100, "AcDbEntity")
                + G(8, "0")
                + G(100, "AcDbBlockEnd");
            string inserts =
                G(0, "INSERT")
                + G(5, "1020")
                + G(330, "17")
                + G(100, "AcDbEntity")
                + G(8, "0")
                + G(100, "AcDbBlockReference")
                + G(2, "GH_PANEL")
                + P(10, 0, 0)
                + G(0, "INSERT")
                + G(5, "1021")
                + G(102, "{ACAD_XDICTIONARY")
                + G(360, "1022")
                + G(102, "}")
                + G(330, "17")
                + G(100, "AcDbEntity")
                + G(8, "0")
                + G(100, "AcDbBlockReference")
                + G(2, "*U8")
                + P(10, 1000, 0);
            string objects =
                G(0, "DICTIONARY")
                + G(5, "1002")
                + G(330, "1001")
                + G(100, "AcDbDictionary")
                + G(280, "1")
                + G(281, "1")
                + G(3, "ACAD_ENHANCEDBLOCK")
                + G(360, "1030")
                + G(3, "AcDbDynamicBlockRoundTripPurgePreventer")
                + G(360, "1031")
                + G(0, "ACAD_EVALUATION_GRAPH")
                + G(5, "1030")
                + Reactors("1002")
                + G(100, "AcDbEvalGraph")
                + G(96, "7")
                + G(97, "7")
                + G(91, "0")
                + G(93, "32")
                + G(95, "7")
                + G(360, "1032")
                + G(92, "-1")
                + G(92, "-1")
                + G(92, "-1")
                + G(92, "-1")
                + G(0, "ACDB_DYNAMICBLOCKPURGEPREVENTER_VERSION")
                + G(5, "1031")
                + Reactors("1002")
                + G(100, "AcDbDynamicBlockPurgePreventer")
                + G(70, "1")
                + G(0, "BLOCKLINEARPARAMETER")
                + G(5, "1032")
                + G(330, "1030")
                + G(100, "AcDbEvalExpr")
                + G(90, "7")
                + G(98, "33")
                + G(99, "329")
                + G(100, "AcDbBlockElement")
                + G(300, "Linear")
                + G(98, "33")
                + G(99, "329")
                + G(1071, "0")
                + G(100, "AcDbBlockParameter")
                + G(280, "1")
                + G(281, "0")
                + G(100, "AcDbBlock2PtParameter")
                + P(1010, 0, 0)
                + P(1011, 500, 0)
                + G(170, "4")
                + G(91, "0")
                + G(91, "0")
                + G(91, "0")
                + G(91, "0")
                + G(171, "0")
                + G(172, "0")
                + G(173, "0")
                + G(174, "0")
                + G(177, "0")
                + G(100, "AcDbBlockLinearParameter")
                + G(305, "Width")
                + G(306, "Width of the panel")
                + G(140, 7.5)
                + G(307, string.Empty)
                + G(96, "1")
                + G(141, 0.0)
                + G(142, 0.0)
                + G(143, 0.0)
                + G(175, "0")
                + G(1001, "ACAUTHENVIRON")
                + P(1010, 250, 7.5)
                + G(0, "DICTIONARY")
                + G(5, "1014")
                + G(330, "1010")
                + G(100, "AcDbDictionary")
                + G(280, "1")
                + G(281, "1")
                + G(0, "DICTIONARY")
                + G(5, "1022")
                + G(330, "1021")
                + G(100, "AcDbDictionary")
                + G(280, "1")
                + G(281, "1")
                + G(3, "AcDbBlockRepresentation")
                + G(360, "1023")
                + G(0, "DICTIONARY")
                + G(5, "1023")
                + Reactors("1022")
                + G(100, "AcDbDictionary")
                + G(280, "1")
                + G(281, "1")
                + G(3, "AcDbRepData")
                + G(360, "1024")
                + G(3, "AppDataCache")
                + G(360, "1025")
                + G(0, "ACDB_BLOCKREPRESENTATION_DATA")
                + G(5, "1024")
                + Reactors("1023")
                + G(100, "AcDbBlockRepresentationData")
                + G(70, "1")
                + G(340, "1001")
                + G(0, "DICTIONARY")
                + G(5, "1025")
                + Reactors("1023")
                + G(100, "AcDbDictionary")
                + G(280, "1")
                + G(281, "1")
                + G(3, "ACAD_ENHANCEDBLOCKDATA")
                + G(360, "1026")
                + G(3, "ACAD_ENHANCEDBLOCKHISTORY")
                + G(360, "1027")
                + G(0, "DICTIONARY")
                + G(5, "1026")
                + Reactors("1025")
                + G(100, "AcDbDictionary")
                + G(280, "1")
                + G(281, "1")
                + G(3, "7")
                + G(360, "1040")
                + G(0, "XRECORD")
                + G(5, "1040")
                + Reactors("1026")
                + G(100, "AcDbXrecord")
                + G(280, "1")
                + G(1071, "18597260")
                + G(1071, "25303744")
                + G(70, "25")
                + G(70, "104")
                + P(10, 0, 0)
                + P(10, 750, 0)
                + G(10, 0.0)
                + G(20, 0.0)
                + G(30, -1.0)
                + G(0, "XRECORD")
                + G(5, "1027")
                + Reactors("1025")
                + G(100, "AcDbXrecord")
                + G(280, "1")
                + G(1070, "3")
                + G(1071, "7")
                + G(300, "Width")
                + G(40, 750.0);
            string classes =
                G(0, "CLASS")
                + G(1, "ACAD_EVALUATION_GRAPH")
                + G(2, "AcDbEvalGraph")
                + G(3, "ObjectDBX Classes")
                + G(90, "1153")
                + G(91, "1")
                + G(280, "0")
                + G(281, "0")
                + G(0, "CLASS")
                + G(1, "BLOCKLINEARPARAMETER")
                + G(2, "AcDbBlockLinearParameter")
                + G(3, "ObjectDBX Classes")
                + G(90, "1153")
                + G(91, "1")
                + G(280, "0")
                + G(281, "0")
                + G(0, "CLASS")
                + G(1, "ACDB_DYNAMICBLOCKPURGEPREVENTER_VERSION")
                + G(2, "AcDbDynamicBlockPurgePreventer")
                + G(3, "ObjectDBX Classes")
                + G(90, "1153")
                + G(91, "1")
                + G(280, "0")
                + G(281, "0")
                + G(0, "CLASS")
                + G(1, "ACDB_BLOCKREPRESENTATION_DATA")
                + G(2, "AcDbBlockRepresentationData")
                + G(3, "ObjectDBX Classes")
                + G(90, "1153")
                + G(91, "1")
                + G(280, "0")
                + G(281, "0");
            string applications =
                G(0, "APPID")
                + G(5, "1050")
                + G(330, "3")
                + G(100, "AcDbSymbolTableRecord")
                + G(100, "AcDbRegAppTableRecord")
                + G(2, "AcDbBlockRepETag")
                + G(70, "0")
                + G(0, "APPID")
                + G(5, "1051")
                + G(330, "3")
                + G(100, "AcDbSymbolTableRecord")
                + G(100, "AcDbRegAppTableRecord")
                + G(2, "AcDbDynamicBlockTrueName")
                + G(70, "0")
                + G(0, "APPID")
                + G(5, "1052")
                + G(330, "3")
                + G(100, "AcDbSymbolTableRecord")
                + G(100, "AcDbRegAppTableRecord")
                + G(2, "AcDbDynamicBlockGUID")
                + G(70, "0")
                + G(0, "APPID")
                + G(5, "1053")
                + G(330, "3")
                + G(100, "AcDbSymbolTableRecord")
                + G(100, "AcDbRegAppTableRecord")
                + G(2, "AcDbBlockRepBTag")
                + G(70, "0")
                + G(0, "APPID")
                + G(5, "1054")
                + G(330, "3")
                + G(100, "AcDbSymbolTableRecord")
                + G(100, "AcDbRegAppTableRecord")
                + G(2, "ACAUTHENVIRON")
                + G(70, "0");

            string text = DxfFileEzdxfDrawings.R2018.Replace("\r\n", "\n");
            int applicationEnd = text.IndexOf(
                "  0\nENDTAB\n",
                text.IndexOf("  2\nAPPID\n", StringComparison.Ordinal),
                StringComparison.Ordinal
            );
            text = text.Substring(0, applicationEnd) + applications + text.Substring(applicationEnd);
            int blockEnd = text.IndexOf(
                "  0\nENDTAB\n",
                text.IndexOf("  2\nBLOCK_RECORD\n", StringComparison.Ordinal),
                StringComparison.Ordinal
            );
            text = text.Substring(0, blockEnd) + records + text.Substring(blockEnd);
            int classEnd = text.IndexOf(
                "  0\nENDSEC\n",
                text.IndexOf("  2\nCLASSES\n", StringComparison.Ordinal),
                StringComparison.Ordinal
            );
            text = text.Substring(0, classEnd) + classes + text.Substring(classEnd);
            return text.Replace("$HANDSEED\n  5\nA1\n", "$HANDSEED\n  5\n2000\n")
                .Replace(
                    "  0\nENDSEC\n  0\nSECTION\n  2\nENTITIES\n",
                    blocks + "  0\nENDSEC\n  0\nSECTION\n  2\nENTITIES\n"
                )
                .Replace(
                    "  0\nENDSEC\n  0\nSECTION\n  2\nOBJECTS\n",
                    inserts + "  0\nENDSEC\n  0\nSECTION\n  2\nOBJECTS\n"
                )
                .Replace("  0\nENDSEC\n  0\nEOF\n", objects + "  0\nENDSEC\n  0\nEOF\n");
        }

        /// <summary>A record of a DXF text: its type, handle and groups, and the block an entity of the BLOCKS section is in.</summary>
        private sealed class Rec
        {
            internal string Type { get; set; }

            internal string Handle { get; set; }

            internal string Block { get; set; }

            internal List<KeyValuePair<int, string>> Groups { get; } = new List<KeyValuePair<int, string>>();

            /// <summary>The groups after the handle, a pointer as what it names (<see cref="Named"/>).</summary>
            internal List<string> Data(Dictionary<string, Rec> records) =>
                Groups
                    .Where(g => g.Key != 5)
                    .Select(g =>
                        g.Key.ToString(CultureInfo.InvariantCulture)
                        + "="
                        + (IsPointer(g.Key) ? Named(records, g.Value) : g.Value.Trim())
                    )
                    .ToList();
        }

        private static bool IsPointer(int code) =>
            (code >= 320 && code <= 369) || (code >= 390 && code <= 399) || code == 480 || code == 481 || code == 1005;

        /// <summary>The records of a DXF text by handle, those of the header and classes left out.</summary>
        private static Dictionary<string, Rec> Records(string text)
        {
            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            var records = new Dictionary<string, Rec>(StringComparer.OrdinalIgnoreCase);
            string section = null;
            string block = null;
            Rec current = null;

            for (int i = 0; i + 1 < lines.Length; i += 2)
            {
                int code = int.Parse(lines[i].Trim(), CultureInfo.InvariantCulture);
                string value = lines[i + 1];

                if (code == 0)
                {
                    current = new Rec
                    {
                        Type = value.Trim(),
                        Block = section == "BLOCKS" && value.Trim() != "BLOCK" ? block : null,
                    };
                    continue;
                }

                current.Groups.Add(new KeyValuePair<int, string>(code, value));

                if (current.Type == "SECTION" && code == 2)
                {
                    section = value.Trim();
                }
                else if (current.Type == "BLOCK" && code == 2)
                {
                    block = value.Trim();
                }
                else if (code == 5 && current.Type != "SECTION" && current.Handle == null)
                {
                    current.Handle = value.Trim();
                    records[current.Handle] = current;
                }
                else if (code == 105 && current.Type == "DIMSTYLE")
                {
                    current.Handle = value.Trim();
                    records[current.Handle] = current;
                }
            }

            return records;
        }

        /// <summary>What a pointer names, so that the source's and the target's compare: its type, the name of a table's record, and the block an entity is in, an anonymous one as *U.</summary>
        private static string Named(Dictionary<string, Rec> records, string pointer)
        {
            string handle = pointer.Trim();

            if (handle == "0")
            {
                return "0";
            }

            if (!records.TryGetValue(handle, out Rec record))
            {
                return "NOTHING";
            }

            string name = record.Type == "BLOCK_RECORD" ? record.Groups.First(g => g.Key == 2).Value.Trim() : null;
            string block = record.Block;
            return record.Type
                + (name != null ? "#" + Plain(name) : string.Empty)
                + (block != null ? "@" + Plain(block) : string.Empty);
        }

        private static string Plain(string name) =>
            name.StartsWith("*U", StringComparison.OrdinalIgnoreCase) ? "*U" : name;

        /// <summary>The handle of the extension dictionary of the record of a type and name (2).</summary>
        private static string Xdictionary(Dictionary<string, Rec> records, string type, string name) =>
            Xdictionary(
                records,
                records
                    .Values.First(r => r.Type == type && r.Groups.Any(g => g.Key == 2 && g.Value.Trim() == name))
                    .Handle
            );

        /// <summary>The handle of the extension dictionary of the record of a handle; null where it has none.</summary>
        private static string Xdictionary(Dictionary<string, Rec> records, string handle)
        {
            List<KeyValuePair<int, string>> groups = records[handle].Groups;
            int at = groups.FindIndex(g => g.Key == 102 && g.Value.Trim() == "{ACAD_XDICTIONARY");
            return at < 0 ? null : groups[at + 1].Value.Trim();
        }

        /// <summary>The objects a root owns, depth first in the order of their owners' groups, each as its data reads.</summary>
        private static List<string> Shape(Dictionary<string, Rec> records, string root)
        {
            var shape = new List<string>();

            if (root == null)
            {
                return shape;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Walk(root);
            return shape;

            void Walk(string handle)
            {
                if (!seen.Add(handle) || !records.TryGetValue(handle, out Rec record))
                {
                    shape.Add("MISSING " + handle);
                    return;
                }

                shape.Add(record.Type + ": " + string.Join(" ", record.Data(records)));

                foreach (KeyValuePair<int, string> group in record.Groups)
                {
                    if ((group.Key == 350 || group.Key == 360) && group.Value.Trim() != "0")
                    {
                        Walk(group.Value.Trim());
                    }
                }
            }
        }

        /// <summary>Every pointer of a drawing's records that names a handle the drawing has not, as its record's type, code and value.</summary>
        private static List<string> Dangling(Dictionary<string, Rec> records)
        {
            var dangling = new List<string>();

            foreach (Rec record in records.Values)
            {
                foreach (KeyValuePair<int, string> group in record.Groups)
                {
                    if (IsPointer(group.Key) && group.Value.Trim() != "0" && !records.ContainsKey(group.Value.Trim()))
                    {
                        dangling.Add(
                            record.Type
                                + " "
                                + record.Handle
                                + " "
                                + group.Key.ToString(CultureInfo.InvariantCulture)
                                + "="
                                + group.Value.Trim()
                        );
                    }
                }
            }

            dangling.Sort(StringComparer.Ordinal);
            return dangling;
        }
    }
}
