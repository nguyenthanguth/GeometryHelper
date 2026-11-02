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
    /// Small leftovers of D31e, D33g and D35e, as AutoCAD 2021 answered them (plugin D37, headless): bit 24 of a multileader's
    /// overrides on a <c>TransformBy</c> that scales; a leader line taken down to one vertex; the heights and widths a table
    /// writes as reals; and the contexts an entity that is never annotative holds.
    /// </summary>
    public class DxfParityD37Tests
    {
        private const int ScaleBit = 1 << 24;
        private const string TextContext = "ACDB_TEXTOBJECTCONTEXTDATA_CLASS";

        private static readonly Tolerance Close = new Tolerance(1E-6, 1E-6);

        private static readonly DxfTypedValue[] AnnotativeXData =
        {
            new DxfTypedValue(1001, "AcadAnnotative"), new DxfTypedValue(1000, "AnnotativeData"), new DxfTypedValue(1002, "{"),
            new DxfTypedValue(1070, "1"), new DxfTypedValue(1070, "1"), new DxfTypedValue(1002, "}"),
        };

        private static GeoPoint3 P(double x, double y) => new GeoPoint3(x, y, 0);

        private static void Near(GeoPoint3 expected, GeoPoint3 actual) => Assert.True(expected.IsEqualTo(actual, Close), "expected " + expected + ", got " + actual);

        // ---- multileaders, on GHD37's Standard of words 4 high, arrows 4, a gap of 2 and doglegs of 8 -----------------------------

        private static DxfDatabase Drawing() => DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);

        /// <summary>A multileader of the box of lines from (-10, -5) to (20, 15) placed at (100, 50), or of "ABC" 4 high there, as GHD37M made AutoCAD's, then its lines from the points given.</summary>
        private static DxfMLeader Made(string content, params GeoPoint3[] points)
        {
            DxfDatabase db = Drawing();
            var mleader = new DxfMLeader();
            db.ModelSpace.AppendEntity(mleader);

            if (content == "block")
            {
                var block = new DxfBlockTableRecord { Name = "GH_BOX" };
                db.BlockTable.Add(block);
                block.AppendEntity(new DxfLine(P(-10, -5), P(20, -5)));
                block.AppendEntity(new DxfLine(P(20, -5), P(20, 15)));
                block.AppendEntity(new DxfLine(P(20, 15), P(-10, 15)));
                block.AppendEntity(new DxfLine(P(-10, 15), P(-10, -5)));
                mleader.BlockContent = block;
                mleader.BlockPosition = P(100, 50);
            }
            else
            {
                mleader.MText = new DxfMText { Contents = "ABC", Location = P(100, 50), TextHeight = 4.0 };
            }

            foreach (GeoPoint3 point in points)
            {
                mleader.AddLeaderLine(point);
            }

            return mleader;
        }

        private static DxfMLeader FromLeft(string content = "words") => Made(content, P(0, 0));

        private static List<DxfTypedValue> Tail(DxfEntity entity) => entity.Item.File.TailOf(entity.Item, 0);

        private static DxfDatabase Db(DxfEntity entity) => entity.Item.File.Database;

        /// <summary>The overrides of the style, the 90 after the context data.</summary>
        private static int Flags(DxfMLeader mleader)
        {
            List<DxfTypedValue> tail = Tail(mleader);
            int end = tail.FindIndex(g => g.TypeCode == 301);
            return int.Parse(tail.Skip(end + 1).First(g => g.TypeCode == 90).Value.Trim(), CultureInfo.InvariantCulture);
        }

        /// <summary>The groups of a leader of the context data, its LEADER{ section, counted from nought, as "code value" texts.</summary>
        private static List<string> Leader(DxfEntity entity, int leader)
        {
            List<DxfTypedValue> tail = Tail(entity);
            int start = Enumerable.Range(0, tail.Count).Where(i => tail[i].Value.Trim() == "LEADER{").Skip(leader).First();
            return tail.Skip(start + 1).TakeWhile(g => g.TypeCode != 303).Select(g => g.TypeCode.ToString(CultureInfo.InvariantCulture) + " " + g.Value.Trim()).ToList();
        }

        private static double Number(List<string> groups, string code)
            => double.Parse(groups.First(g => g.StartsWith(code + " ", StringComparison.Ordinal)).Substring(code.Length + 1), CultureInfo.InvariantCulture);

        /// <summary>The point a leader's dogleg starts at, as written (its 10, 20 and 30).</summary>
        private static GeoPoint3 LeaderPoint(DxfEntity entity, int leader)
        {
            List<string> groups = Leader(entity, leader);
            return new GeoPoint3(Number(groups, "10"), Number(groups, "20"), Number(groups, "30"));
        }

        /// <summary>The vertices a leader's lines hold of their own, as written: the 10 of each LEADER_LINE.</summary>
        private static int OwnVertices(DxfEntity entity, int leader)
        {
            List<string> groups = Leader(entity, leader);
            int line = groups.FindIndex(g => g == "304 LEADER_LINE{");
            return groups.Skip(line + 1).TakeWhile(g => g != "305 }").Count(g => g.StartsWith("10 ", StringComparison.Ordinal));
        }

        private static List<DxfLine> Lines(DxfMLeader mleader) => mleader.Explode().OfType<DxfLine>().ToList();

        private static GeoTransform3 MirrorXY => GeoTransform3.Mirror(new GeoPlane3(P(0, 0), new GeoVector3(0, 0, 1)));

        public static IEnumerable<object[]> Scalings()
        {
            foreach (string content in new[] { "words", "block" })
            {
                yield return new object[] { content, "scale2" };
                yield return new object[] { content, "scale05" };
                yield return new object[] { content, "scale2-about" };
                yield return new object[] { content, "rotscale" };
                yield return new object[] { content, "mirrorscale" };
                yield return new object[] { content, "scale2-back" };
            }
        }

        private static void Transform(DxfMLeader mleader, string how)
        {
            GeoTransform3 turn = GeoTransform3.RotationZ(Math.PI / 6);

            switch (how)
            {
                case "scale2": mleader.TransformBy(GeoTransform3.Scaling(2.0)); break;
                case "scale05": mleader.TransformBy(GeoTransform3.Scaling(0.5)); break;
                case "scale2-about": mleader.TransformBy(GeoTransform3.Scaling(P(100, 50), 2.0)); break;
                case "rotscale": mleader.TransformBy(turn * GeoTransform3.Scaling(3.0)); break;
                case "mirrorscale": mleader.TransformBy(MirrorXY * GeoTransform3.Scaling(2.0)); break;
                case "scale2-back": mleader.TransformBy(GeoTransform3.Scaling(2.0)); mleader.TransformBy(GeoTransform3.Scaling(0.5)); break;
                case "scale1": mleader.TransformBy(GeoTransform3.Scaling(1.0)); break;
                case "identity": mleader.TransformBy(GeoTransform3.Identity); break;
                case "move": mleader.TransformBy(GeoTransform3.Translation(new GeoVector3(10, 20, 0))); break;
                case "rot30": mleader.TransformBy(turn); break;
                case "mirrorxy": mleader.TransformBy(MirrorXY); break;
                default: throw new ArgumentException(how);
            }
        }

        [Theory]
        [MemberData(nameof(Scalings))]
        public void ATransformByThatScales_SetsTheBitOfTheScale_AsAutoCadDoes(string content, string how)
        {
            // AutoCAD 2021 set bit 24 of the overrides (the scale's) on a TransformBy that scaled a multileader of words or of a
            // block, by 2, by 0.5, about (100, 50), turned and scaled by 3, mirrored and scaled by 2, and kept it once scaled back
            // by 0.5 (GHD37M t-*-scale2 ... t-*-scale2-back), the scale itself still 1. The library left the bit.
            DxfMLeader mleader = FromLeft(content);
            Assert.Equal(0, Flags(mleader) & ScaleBit);

            Transform(mleader, how);

            Assert.Equal(ScaleBit, Flags(mleader) & ScaleBit);
            Assert.Equal(1.0, mleader.Scale);
        }

        [Theory]
        [InlineData("words", "scale1")]
        [InlineData("words", "identity")]
        [InlineData("words", "move")]
        [InlineData("words", "rot30")]
        [InlineData("words", "mirrorxy")]
        [InlineData("block", "scale1")]
        [InlineData("block", "move")]
        [InlineData("block", "rot30")]
        [InlineData("block", "mirrorxy")]
        public void ATransformByThatDoesNotScale_LeavesTheBitOfTheScale_AsAutoCadDoes(string content, string how)
        {
            // AutoCAD 2021 left bit 24 on a TransformBy of a move, a turn of 30 degrees, a mirror across the XY plane, a scaling by
            // 1 and the identity (GHD37M t-*-move, -rot30, -mirrorxy, -scale1, -identity).
            DxfMLeader mleader = FromLeft(content);

            Transform(mleader, how);

            Assert.Equal(0, Flags(mleader) & ScaleBit);
        }

        [Theory]
        [InlineData("words", 90, 48, 98)]
        [InlineData("block", 82, 55, 90)]
        public void RemoveFirstVertex_TakesALineOfTwoDownToItsLanding_AsAutoCadDoes(string content, double x, double y, double end)
        {
            // AutoCAD 2021: RemoveFirstVertex of the line from (0, 0) left it the one vertex where its dogleg starts, (90, 48)
            // beside the words, (82, 55) beside the box; written a LEADER_LINE of no vertex of its own, its leader landed there,
            // drawn as its dogleg alone, the content left (GHD37M r-first, r-k-first). The library refused below two vertices.
            DxfMLeader mleader = FromLeft(content);

            mleader.RemoveFirstVertex(0);

            Assert.Equal(1, mleader.VerticesCount(0));
            Near(P(x, y), mleader.GetFirstVertex(0));
            Near(P(x, y), mleader.GetLastVertex(0));
            Assert.Equal(0, OwnVertices(mleader, 0));
            Assert.Contains("290 1", Leader(mleader, 0));
            Near(P(x, y), LeaderPoint(mleader, 0));
            DxfLine dogleg = Assert.Single(Lines(mleader));
            Near(P(x, y), dogleg.StartPoint);
            Near(P(end, y), dogleg.EndPoint);
            Assert.DoesNotContain(mleader.Explode(), e => e is DxfPolyline || e is DxfSolid);
            Near(P(100, 50), content == "words" ? mleader.TextLocation : mleader.BlockPosition);

            // Read back, it is of one vertex still.
            DxfMLeader back = ByHandle<DxfMLeader>(DxfDatabase.Parse(Db(mleader).ToText()), mleader.Handle);
            Assert.Equal(1, back.VerticesCount(0));
            Near(P(x, y), back.GetFirstVertex(0));
        }

        [Fact]
        public void RemoveFirstVertex_OfALeaderLandedByItsFirstVertex_LeavesTheLanding_AsAutoCadDoes()
        {
            // AutoCAD 2021: a leader added, its line given (-20, 80) and then (-30, 90) before it, RemoveFirstVertex left it the
            // landing (-20, 80) alone, its dogleg from (-20, 80) to (-12, 80) (D31e addfirst-removefirst, GHD37M r-addfirst-removefirst).
            DxfMLeader mleader = FromLeft();
            int line = mleader.AddLeaderLine(mleader.AddLeader());
            mleader.AddFirstVertex(line, P(-20, 80));
            mleader.AddFirstVertex(line, P(-30, 90));

            mleader.RemoveFirstVertex(line);

            Assert.Equal(1, mleader.VerticesCount(line));
            Near(P(-20, 80), mleader.GetFirstVertex(line));
            Assert.Contains(Lines(mleader), d => d.StartPoint.IsEqualTo(P(-20, 80), Close) && d.EndPoint.IsEqualTo(P(-12, 80), Close));
        }

        [Fact]
        public void RemoveLastVertex_MakesTheVertexBeforeItTheLanding_AsItStands_AsAutoCadDoes()
        {
            // AutoCAD 2021: RemoveLastVertex of the line from (0, 0) landed its leader at (0, 0), the dogleg drawn from (0, 0) to
            // (8, 0), the words left at (100, 50) (GHD37M r-last); with no dogleg it wrote the leader's point (0, 0) as well, the
            // line's vertex then read (8, 0), the end the dogleg would have, from a line of two vertices as from one of three
            // (r-nodogleg-last, r-nodogleg-three-last). The library refused the first and wrote (-8, 0) for the third.
            DxfMLeader mleader = FromLeft();
            mleader.RemoveLastVertex(0);
            Assert.Equal(1, mleader.VerticesCount(0));
            Near(P(0, 0), mleader.GetLastVertex(0));
            Near(P(0, 0), LeaderPoint(mleader, 0));
            DxfLine dogleg = Assert.Single(Lines(mleader));
            Near(P(8, 0), dogleg.EndPoint);
            Near(P(100, 50), mleader.TextLocation);

            DxfMLeader plain = FromLeft();
            plain.EnableDogleg = false;
            plain.RemoveLastVertex(0);
            Near(P(0, 0), LeaderPoint(plain, 0));
            Near(P(8, 0), plain.GetLastVertex(0));

            DxfMLeader three = FromLeft();
            three.EnableDogleg = false;
            three.AddFirstVertex(0, P(-10, -10));
            three.RemoveLastVertex(0);
            Assert.Equal(2, three.VerticesCount(0));
            Near(P(0, 0), LeaderPoint(three, 0));
            Near(P(-10, -10), three.GetFirstVertex(0));
            Near(P(8, 0), three.GetLastVertex(0));
        }

        [Fact]
        public void ALineOfOneVertex_KeepsIt_AsAutoCadCrashesTakingItOutOfADrawingItRead()
        {
            // AutoCAD 2021 read its own drawing of a line of one vertex (r-first) and took it out by RemoveFirstVertex, then
            // crashed (FATAL access violation, GHD37E); it crashed taking the vertex out of a line just added to a landed leader
            // (r-emptyline-first) and of a leader landed by the line's first vertex (r-noland-first). The library keeps it, of
            // either end, and a line of no vertex gives none.
            DxfMLeader mleader = FromLeft();
            mleader.RemoveFirstVertex(0);
            string text = Db(mleader).ToText();

            Assert.Throws<InvalidOperationException>(() => mleader.RemoveFirstVertex(0));
            Assert.Throws<InvalidOperationException>(() => mleader.RemoveLastVertex(0));
            Assert.Equal(text, Db(mleader).ToText());

            DxfMLeader empty = FromLeft();
            int line = empty.AddLeaderLine(empty.AddLeader());
            Assert.Equal(0, empty.VerticesCount(line));
            Assert.Throws<InvalidOperationException>(() => empty.RemoveFirstVertex(line));
            Assert.Throws<InvalidOperationException>(() => empty.RemoveLastVertex(line));
        }

        [Fact]
        public void RemoveLastVertex_OfALeaderOfTwoLines_IsRefused_AsAutoCadRefusesIt()
        {
            // AutoCAD 2021 answered eInvalidContext to RemoveLastVertex of either line of a leader of two, the line of two vertices
            // or of three, and changed nothing (GHD37M r-twolines-last, r-twolines-other-last, r-twolines-three-last); it took the
            // first vertex out of one (r-twolines-first). The library moved the landing both lines end at.
            DxfMLeader mleader = FromLeft();
            int other = mleader.AddLeaderLine(mleader.GetLeaderIndex(0));
            mleader.AddFirstVertex(other, P(0, 100));
            string text = Db(mleader).ToText();

            Assert.Throws<InvalidOperationException>(() => mleader.RemoveLastVertex(0));
            Assert.Throws<InvalidOperationException>(() => mleader.RemoveLastVertex(other));
            mleader.AddFirstVertex(0, P(-10, -10));
            Assert.Throws<InvalidOperationException>(() => mleader.RemoveLastVertex(0));
            Assert.Equal(3, mleader.VerticesCount(0));
            Assert.NotEqual(text, Db(mleader).ToText());

            DxfMLeader first = FromLeft();
            int second = first.AddLeaderLine(first.GetLeaderIndex(0));
            first.AddFirstVertex(second, P(0, 100));
            first.RemoveFirstVertex(0);
            Assert.Equal(1, first.VerticesCount(0));
            Assert.Equal(2, first.VerticesCount(second));
            Near(P(90, 48), first.GetLastVertex(second));
        }

        [Fact]
        public void ALineOfOneVertex_TakesVerticesAgain_AsAutoCadGivesThem()
        {
            // AutoCAD 2021, the line from (0, 0) taken down to (90, 48) (GHD37M r-first-*): AddFirstVertex at (-10, -10) put the
            // arrow there; AddLastVertex at (95, 40) landed the leader there, the words left; SetFirstVertex at (70, 30) moved the
            // landing there and the words with it, to (80, 32); taken down to (0, 0) by RemoveLastVertex, SetLastVertex at (80, 40)
            // moved the words to (180, 90) (r-last-setlast).
            DxfMLeader first = FromLeft();
            first.RemoveFirstVertex(0);
            first.AddFirstVertex(0, P(-10, -10));
            Assert.Equal(2, first.VerticesCount(0));
            Near(P(-10, -10), first.GetFirstVertex(0));
            Near(P(90, 48), first.GetLastVertex(0));

            DxfMLeader last = FromLeft();
            last.RemoveFirstVertex(0);
            last.AddLastVertex(0, P(95, 40));
            Near(P(90, 48), last.GetFirstVertex(0));
            Near(P(95, 40), last.GetLastVertex(0));
            Near(P(100, 50), last.TextLocation);

            DxfMLeader moved = FromLeft();
            moved.RemoveFirstVertex(0);
            moved.SetFirstVertex(0, P(70, 30));
            Near(P(70, 30), LeaderPoint(moved, 0));
            Near(P(80, 32), moved.TextLocation);

            DxfMLeader back = FromLeft();
            back.RemoveLastVertex(0);
            back.SetLastVertex(0, P(80, 40));
            Near(P(180, 90), back.TextLocation);
        }

        // ---- tables, on D33b's table F6 of AutoCAD's Standard -----------------------------------------------------------------------

        private static DxfDatabase TableDrawing() => DxfDatabase.Parse(DxfDimensionDrawings.R2018);

        private static string TableRecord(DxfDatabase db) => DxfAutoCadChecksD16Tests.Record(db.ToText(), "F6");

        [Fact]
        public void TheHeightsAndWidthsATablePutsIn_AreWrittenAsReals_AsAutoCadWritesThem()
        {
            // AutoCAD 2021 wrote the row InsertRows put in 141 9.0 and the column InsertColumns put in 142 20.0 (D33g g01, k07,
            // D33 n9, D33c e14), a row's height set 141 12.0 (D33c e11); the library wrote 141 9, 142 20 and 141 12.
            DxfDatabase db = TableDrawing();
            DxfTable table = DxfAutoCadChecksD16Tests.Table(db);
            table.InsertRows(3, 9.0, 1);
            table.InsertColumns(1, 20.0, 1);
            string record = TableRecord(db);
            Assert.Contains("\n141\n9.0\n", record, StringComparison.Ordinal);
            Assert.Contains("\n142\n20.0\n", record, StringComparison.Ordinal);
            Assert.DoesNotContain("\n141\n9\n", record, StringComparison.Ordinal);
            Assert.DoesNotContain("\n142\n20\n", record, StringComparison.Ordinal);

            DxfDatabase sized = TableDrawing();
            DxfTable other = DxfAutoCadChecksD16Tests.Table(sized);
            other.SetRowHeight(2, 12.0);
            other.SetColumnWidth(1, 30.0);
            string set = TableRecord(sized);
            Assert.Contains("\n141\n12.0\n", set, StringComparison.Ordinal);
            Assert.Contains("\n142\n30.0\n", set, StringComparison.Ordinal);

            // SetSize's new row, 9 high by AutoCAD's Standard (D28d).
            DxfDatabase grown = TableDrawing();
            DxfAutoCadChecksD16Tests.Table(grown).SetSize(4, 2);
            Assert.Contains("\n141\n9.0\n", TableRecord(grown), StringComparison.Ordinal);
            Assert.DoesNotContain("\n141\n9\n", TableRecord(grown), StringComparison.Ordinal);
        }

        [Fact]
        public void TheTextHeightsAndNumbersOfACell_AreWrittenAsReals_AsAutoCadWritesThem()
        {
            // AutoCAD 2021 wrote a cell's text height set 140 7.0 (D33 s07, D33f h02, h05, h07) and the double a cell's text of
            // digits was made 140 12.0 (D33 t1); the library wrote 140 7 and 140 12.
            DxfDatabase db = TableDrawing();
            DxfTable table = DxfAutoCadChecksD16Tests.Table(db);
            table.SetTextHeight(2, 0, 7.0);
            Assert.Contains("\n140\n7.0\n", TableRecord(db), StringComparison.Ordinal);
            Assert.DoesNotContain("\n140\n7\n", TableRecord(db), StringComparison.Ordinal);

            DxfDatabase valued = TableDrawing();
            DxfTable cell = DxfAutoCadChecksD16Tests.Table(valued);
            cell.SetValue(2, 0, "12");
            cell.SetDataType(2, 0, DxfDataType.Double, DxfUnitType.Unitless);
            Assert.Contains("\n140\n12.0\n", TableRecord(valued), StringComparison.Ordinal);
            Assert.DoesNotContain("\n140\n12\n", TableRecord(valued), StringComparison.Ordinal);
        }

        [Fact]
        public void TheSizesATableScaledWrites_AreReals()
        {
            // A table scaled by 2 writes its heights and its cells' text heights as AutoCAD writes every real of a table, with its
            // point: the title of 10 high 141 20.0, and no real of a whole number written without one.
            DxfDatabase db = TableDrawing();
            DxfTable table = DxfAutoCadChecksD16Tests.Table(db);
            table.TransformBy(GeoTransform3.Scaling(2.0));
            string record = TableRecord(db);
            Assert.Contains("\n141\n20.0\n", record, StringComparison.Ordinal);
            Assert.DoesNotMatch("\n(140|141|142|144)\n-?[0-9]+\n", record);
        }

        // ---- contexts held by entities never annotative --------------------------------------------------------------------------

        private static string Scale(string handle, string name, string paper, string drawing, string unit)
            => "  0\nSCALE\n  5\n" + handle + "\n102\n{ACAD_REACTORS\n330\n14\n102\n}\n330\n14\n100\nAcDbScale\n 70\n0\n300\n" + name + "\n140\n" + paper + "\n141\n" + drawing + "\n290\n" + unit + "\n";

        /// <summary>
        /// The ezdxf drawing with the scales 1:1 (F0) and 1:2 (F1), its TEXT 64 annotative and holding two contexts of a TEXT, at
        /// 1:1 and at 1:2, its default, as AutoCAD 2021 writes them (D35e's drawing).
        /// </summary>
        private static DxfDatabase TextWithContexts()
        {
            const string ScaleList = "  0\nDICTIONARY\n  5\n14\n330\nA\n100\nAcDbDictionary\n281\n1\n";
            string text = DxfFileEzdxfDrawings.R2018;
            Assert.Contains(ScaleList, text, StringComparison.Ordinal);
            int end = text.LastIndexOf("  0\nENDSEC\n", StringComparison.Ordinal);
            text = (text.Substring(0, end) + Scale("F0", "1:1", "1", "1", "1") + Scale("F1", "1:2", "1", "2", "0") + text.Substring(end))
                .Replace(ScaleList, ScaleList + "  3\nA0\n350\nF0\n  3\nA1\n350\nF1\n");
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfEntity grid = ByHandle<DxfEntity>(db, "64");
            grid.XData = AnnotativeXData;
            grid.CreateExtensionDictionary();
            var manager = new DxfDBDictionary { TreatElementsAsHard = true };
            grid.ExtensionDictionary.SetAt("AcDbContextDataManager", manager);
            var scales = new DxfDBDictionary { TreatElementsAsHard = true };
            manager.SetAt("ACDB_ANNOTATIONSCALES", scales);
            text = db.ToText();
            string records = string.Empty;
            string entries = string.Empty;
            string[] handles = { "E01", "E02" };
            string[] scaleOf = { "F0", "F1" };

            for (int i = 0; i < 2; i++)
            {
                records += "  0\n" + TextContext + "\n  5\n" + handles[i] + "\n102\n{ACAD_REACTORS\n330\n" + scales.Handle + "\n102\n}\n330\n" + scales.Handle
                    + "\n100\nAcDbObjectContextData\n 70\n4\n290\n" + (i == 1 ? "1" : "0") + "\n100\nAcDbAnnotScaleObjectContextData\n340\n" + scaleOf[i]
                    + "\n 70\n0\n 50\n0.0\n 10\n11.0\n 20\n12.0\n 11\n0.0\n 21\n0.0\n";
                entries += "  3\n*A" + (i + 1).ToString(CultureInfo.InvariantCulture) + "\n350\n" + handles[i] + "\n";
            }

            end = text.LastIndexOf("  0\nENDSEC\n", StringComparison.Ordinal);
            text = text.Substring(0, end) + records + text.Substring(end);
            string record = Records(text).Single(r => HandleOf(r) == scales.Handle);
            return DxfDatabase.Parse(text.Replace(record, record + entries));
        }

        private static string[] ShownAt(DxfDatabase db, DxfDBObject o)
            => db.ObjectContextManager.GetContextCollection("ACDB_ANNOTATIONSCALES").Where(s => o.HasContext(s)).Select(s => s.Name).OrderBy(s => s, StringComparer.Ordinal).ToArray();

        public static IEnumerable<object[]> NeverAnnotative()
        {
            foreach (string kind in new[] { "LINE", "CIRCLE", "ARC", "POINT", "ELLIPSE", "XLINE", "RAY", "SOLID", "3DFACE", "LWPOLYLINE", "SPLINE" })
            {
                yield return new object[] { kind };
            }
        }

        private static DxfEntity Entity(string kind)
        {
            switch (kind)
            {
                case "LINE": return new DxfLine(P(0, 0), P(20, 0));
                case "CIRCLE": return new DxfCircle(P(5, 5), new GeoVector3(0, 0, 1), 3.0);
                case "ARC": return new DxfArc(P(5, 5), 3.0, 0.0, Math.PI / 2.0);
                case "POINT": return new DxfDBPoint(P(5, 5));
                case "ELLIPSE": return new DxfEllipse(P(5, 5), new GeoVector3(0, 0, 1), new GeoVector3(4, 0, 0), 0.5, 0.0, 2.0 * Math.PI);
                case "XLINE": return new DxfXline();
                case "RAY": return new DxfRay();
                case "SOLID": return new DxfSolid(P(0, 0), P(4, 0), P(0, 4), P(4, 4));
                case "3DFACE": return new DxfFace(P(0, 0), P(4, 0), P(4, 4), P(0, 4), true, true, true, true);
                case "LWPOLYLINE":
                    var polyline = new DxfPolyline();
                    polyline.AddVertexAt(0, new GeoPoint2(0, 0), 0.0, 0.0, 0.0);
                    polyline.AddVertexAt(1, new GeoPoint2(10, 5), 0.0, 0.0, 0.0);
                    return polyline;
                case "SPLINE": return new DxfSpline(3, false, false, false, new[] { P(0, 0), P(5, 5), P(10, 0), P(15, 5) }, new[] { 0.0, 0.0, 0.0, 0.0, 1.0, 1.0, 1.0, 1.0 }, new double[0], 0.0, 0.0);
                default: throw new ArgumentException(kind);
            }
        }

        [Theory]
        [MemberData(nameof(NeverAnnotative))]
        public void AnEntityNeverAnnotative_HoldingContexts_IsShownAtNone_AsAutoCadReadsIt(string kind)
        {
            // AutoCAD 2021: the TEXT of contexts of 1:1 and 1:2 handed over to a new entity of each class never annotative, its
            // extension dictionary kept, held both records and answered HasContext of neither scale, Annotative NotApplicable
            // (GHD37H, D35e's LINE read back so too). The library read the records as contexts.
            DxfDatabase db = TextWithContexts();
            Assert.Equal(new[] { "1:1", "1:2" }, ShownAt(db, ByHandle<DxfEntity>(db, "64")));
            DxfEntity entity = Entity(kind);

            ByHandle<DxfEntity>(db, "64").HandOverTo(entity, true, true);

            Assert.Equal(DxfAnnotativeStates.NotApplicable, entity.Annotative);
            Assert.Empty(ShownAt(db, entity));
            DxfObjectContextCollection scales = db.ObjectContextManager.GetContextCollection("ACDB_ANNOTATIONSCALES");
            string text = db.ToText();
            entity.RemoveContext(scales.GetContext("1:1"));
            Assert.Equal(text, db.ToText());
            Assert.Contains("\n" + TextContext + "\n  5\nE01\n", text, StringComparison.Ordinal);

            DxfDatabase back = DxfDatabase.Parse(text);
            Assert.Empty(ShownAt(back, ByHandle<DxfEntity>(back, "64")));
        }
    }
}
