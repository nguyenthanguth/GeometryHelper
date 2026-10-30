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
    /// What followed from AutoCAD 2021's answers to the checks of 30 October 2026 still open after plugin run v7: a closed
    /// spline made a polyline has its knots among its vertices and its last vertex on its first (point 17n, g); a drawing of
    /// R2004 or later keeps its properties in the header, each custom property a <c>$CUSTOMPROPERTYTAG</c> and a
    /// <c>$CUSTOMPROPERTY</c>, and no DWGPROPS (26); and a MINSERT of more than one cell is not exploded, while exploded into
    /// its owner it gives its first cell (51). Each drawing is one of ezdxf's, or built here, read as a file is.
    /// </summary>
    public class DxfAutoCadChecksD15Tests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        private static GeoPoint3 P3(double x, double y, double z) => new GeoPoint3(x, y, z);

        #region Point 17n (g): a closed spline made a polyline

        /// <summary>
        /// The closed cubic of 17n (g) through (0, 0), (40, 0), (50, 30) and (10, 25), its knots the lengths of the chords, as
        /// AutoCAD 2021 saved it in v7-17-autocad-periodic-cubic.dxf: periodic, seven control points, the knots clamped.
        /// </summary>
        private static DxfSpline Periodic()
        {
            var points = new[]
            {
                P3(0.0000000000000016, 0.0, 0.0), P3(4.762265129346506, -9.875169480547258, 0.0), P3(44.57965357801522, -10.52612047361797, 0.0),
                P3(60.52497076467426, 41.58068210387638, 0.0), P3(8.562414603983974, 34.00936527765936, 0.0), P3(-3.205697822100078, 6.647426893891476, 0.0),
                P3(0.0000000000000016, 0.0, 0.0),
            };
            var knots = new[] { 0.0, 0.0, 0.0, 0.0, 40.0, 71.62277660168379, 111.9340653431765, 138.859889378849, 138.859889378849, 138.859889378849, 138.859889378849 };
            return new DxfSpline(3, false, true, true, points, knots, null, 0.0000000001, 0.000000001);
        }

        [Fact]
        public void AClosedSplineMadeAPolyline_HasAutoCadsCountOfVertices_ItsLastOnItsFirst_AtEveryPrecision()
        {
            // Plugin v7, 17n (g): AutoCAD 2021's ToPolylineWithPrecision of the closed cubic, of lines and of arcs alike, every
            // polyline closed and its last vertex on its first.
            DxfSpline spline = Periodic();
            int[] precisions = { 0, 1, 2, 3, 5, 10, 20, 50, 99 };
            int[] counts = { 5, 9, 15, 17, 30, 48, 86, 252, 493 };

            for (int k = 0; k < precisions.Length; k++)
            {
                var lines = Assert.IsType<DxfPolyline>(spline.ToPolylineWithPrecision(precisions[k], false, true));
                var arcs = Assert.IsType<DxfPolyline>(spline.ToPolylineWithPrecision(precisions[k], true, true));

                Assert.Equal("at " + precisions[k] + ": " + counts[k] + ", " + counts[k], "at " + precisions[k] + ": " + lines.NumberOfVertices + ", " + arcs.NumberOfVertices);
                Assert.True(lines.Closed);
                Assert.True(arcs.Closed);
                Assert.True(lines.GetPoint2dAt(counts[k] - 1).IsEqualTo(lines.GetPoint2dAt(0), Tol), "at " + precisions[k]);
            }
        }

        [Fact]
        public void AClosedSplineMadeAPolylineAtNought_IsItsFitPoints_EachArcBentByAQuarterOfTheTangentsTurn()
        {
            // Plugin v7, 17n (g) at 0: the four points it runs through, which are its knots, and the first again; the arcs' bulges
            // 0.418, 0.398, 0.435 and 0.406, the last vertex's nought, each the tangent of a quarter of the turn over a quarter of
            // the range, the pieces the halving made, not over the knots' spans (0.480 for the first). AutoCAD's g ran through
            // its fit points; the same spline of none is the halving alone (plugin v8, 17f, in DxfAutoCadChecksD16Tests).
            DxfSpline spline = DxfAutoCadChecksD16Tests.FittedG();
            var lines = Assert.IsType<DxfPolyline>(spline.ToPolylineWithPrecision(0, false, true));
            var arcs = Assert.IsType<DxfPolyline>(spline.ToPolylineWithPrecision(0, true, true));
            GeoPoint2[] wanted = { new GeoPoint2(0, 0), new GeoPoint2(40, 0), new GeoPoint2(50, 30), new GeoPoint2(10, 25), new GeoPoint2(0, 0) };
            double[] bulges = { 0.41791605272607174, 0.39799678903684765, 0.43536140735722262, 0.40586110215633225, 0.0 };

            for (int i = 0; i < wanted.Length; i++)
            {
                Assert.True(wanted[i].IsEqualTo(lines.GetPoint2dAt(i), Tol), "vertex " + i + ": " + lines.GetPoint2dAt(i));
                Assert.Equal(bulges[i], arcs.GetBulgeAt(i), 12);
            }
        }

        [Fact]
        public void AClosedSplineMadeAPolylineAtTen_HasEachKnotInThePlaceOfTheVertexNearestIt_AsAutoCadGaveIt()
        {
            // Plugin v7, 17n (g) at 10: the whole range halved into 64ths, and each knot, 40, 71.62 and 111.93, in the place of
            // the 64th nearest it: (36.81, -2.20), the knot (40, 0), then (41.17, 0.97) at 19/64, not the 64th at 18/64.
            var lines = Assert.IsType<DxfPolyline>(DxfAutoCadChecksD16Tests.FittedG().ToPolylineWithPrecision(10, false, true));
            var wanted = new Dictionary<int, GeoPoint2>
            {
                [12] = new GeoPoint2(36.806966792921443, -2.1998622303112678),
                [13] = new GeoPoint2(40, 0),
                [14] = new GeoPoint2(41.167318907080436, 0.96889939325457752),
                [37] = new GeoPoint2(13.435815345662515, 27.664356239167876),
                [38] = new GeoPoint2(10, 25),
                [39] = new GeoPoint2(5.5100027371473432, 20.346214795941624),
                [47] = new GeoPoint2(0, 0),
            };

            Assert.Equal(48, lines.NumberOfVertices);

            foreach (KeyValuePair<int, GeoPoint2> vertex in wanted)
            {
                Assert.True(vertex.Value.IsEqualTo(lines.GetPoint2dAt(vertex.Key), new Tolerance(1E-9, 1E-9, 1E-9)), "vertex " + vertex.Key + ": " + lines.GetPoint2dAt(vertex.Key));
            }

            // Of arcs, AutoCAD's bulges of those vertices.
            var arcs = Assert.IsType<DxfPolyline>(DxfAutoCadChecksD16Tests.FittedG().ToPolylineWithPrecision(10, true, true));
            var bulges = new Dictionary<int, double>
            {
                [0] = 0.04765626161679417, [12] = 0.021455394627566883, [13] = 0.022638777411657811, [14] = 0.02164784907839656,
                [37] = 0.035425807053091771, [38] = 0.035907763299519675, [46] = 0.046829237693960274, [47] = 0.0,
            };

            foreach (KeyValuePair<int, double> bulge in bulges)
            {
                Assert.Equal(bulge.Value, arcs.GetBulgeAt(bulge.Key), 12);
            }
        }

        [Fact]
        public void AnOpenSplineMadeAPolyline_HasNoKnotAmongItsVertices_AsBefore()
        {
            // Plugin v4, 17-b: the cubic of three spans has 78 vertices at 0, its knots 1 and 2 none of them, as AutoCAD gave it.
            var spline = new DxfSpline(3, false, false, false, new[] { P3(0, -40, 0), P3(10, -30, 0), P3(20, -30, 0), P3(30, -40, 0), P3(40, -50, 0), P3(50, -40, 0) }, new[] { 0.0, 0, 0, 0, 1, 2, 3, 3, 3, 3 }, null, 0.0, 0.0);

            var lines = Assert.IsType<DxfPolyline>(spline.ToPolylineWithPrecision(0, false, true));

            Assert.Equal(78, lines.NumberOfVertices);
            Assert.False(lines.Closed);
        }

        #endregion

        #region Point 26: the properties of a drawing of R2004 or later are kept in its header

        /// <summary>The properties of 26: Plate B-12 by GeometryHelper, a hyperlink base and two custom properties.</summary>
        private static DxfDatabaseSummaryInfo Plate()
        {
            var builder = new DxfDatabaseSummaryInfoBuilder { Title = "Plate B-12", Author = "GeometryHelper", HyperlinkBase = "https://example.com/plates/" };
            builder.CustomPropertyTable.Add(new KeyValuePair<string, string>("Project", "P4 checks"));
            builder.CustomPropertyTable.Add(new KeyValuePair<string, string>("Checked by", "GH"));
            return builder.ToDatabaseSummaryInfo();
        }

        /// <summary>The run of the header AutoCAD 2021 saved 26-a of R2018 with, after its $DWGCODEPAGE.</summary>
        private const string AutoCadRun = "  9\n$TITLE\n  1\nPlate B-12\n  9\n$AUTHOR\n  1\nGeometryHelper\n"
            + "  9\n$CUSTOMPROPERTYTAG\n  1\nProject\n  9\n$CUSTOMPROPERTY\n  1\nP4 checks\n"
            + "  9\n$CUSTOMPROPERTYTAG\n  1\nChecked by\n  9\n$CUSTOMPROPERTY\n  1\nGH\n";

        [Fact]
        public void ADrawingAutoCadSavedAsR2018_GivesTheCustomPropertiesOfItsHeader_InTheirOrder()
        {
            // Plugin v4, 26: AutoCAD 2021 saved 26-a as R2018 with $TITLE, $AUTHOR and a $CUSTOMPROPERTYTAG and a
            // $CUSTOMPROPERTY for each custom property after $DWGCODEPAGE, and no DWGPROPS; it read them back so.
            string text = DxfFileEzdxfDrawings.R2018.Replace("\r\n", "\n").Replace("$DWGCODEPAGE\n  3\nANSI_1252\n", "$DWGCODEPAGE\n  3\nANSI_1252\n" + AutoCadRun);

            DxfDatabaseSummaryInfo info = DxfDatabase.Parse(text).SummaryInfo;

            Assert.Equal("Plate B-12", info.Title);
            Assert.Equal("GeometryHelper", info.Author);
            Assert.Equal(new[] { "Project=P4 checks", "Checked by=GH" }, info.CustomProperties.Select(p => p.Key + "=" + p.Value));
        }

        [Fact]
        public void ThePropertiesOfADrawingOfR2018_AreWrittenInItsHeaderAsAutoCadWritesThem_AndNoDwgprops()
        {
            // AutoCAD 2021's R2018 of 26-a: the texts and the pairs after $DWGCODEPAGE, the $LASTSAVEDBY ezdxf wrote moved
            // between them before the revision, as AutoCAD wrote it in plugin v8 (D16); the hyperlink base in $HYPERLINKBASE.
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);

            db.SummaryInfo = Plate();

            string text = db.ToText();
            Assert.Contains("$DWGCODEPAGE\n  3\nANSI_1252\n  9\n$TITLE\n  1\nPlate B-12\n  9\n$AUTHOR\n  1\nGeometryHelper\n  9\n$LASTSAVEDBY\n  1\nezdxf\n  9\n$CUSTOMPROPERTYTAG\n  1\nProject\n", text, StringComparison.Ordinal);
            Assert.Contains("$HYPERLINKBASE\n  1\nhttps://example.com/plates/\n", text, StringComparison.Ordinal);
            Assert.DoesNotContain("DWGPROPS", text, StringComparison.Ordinal);
            DxfDatabaseSummaryInfo read = DxfDatabase.Parse(text).SummaryInfo;
            Assert.Equal(Plate(), read);

            // The same again changes nothing.
            db.SummaryInfo = read;
            Assert.Equal(text, db.ToText());
        }

        [Fact]
        public void PropertiesSetInADrawingOfR2018WithADwgprops_TakeItOut_AsAutoCadSavedOne()
        {
            // Plugin v4, 26: the library's 26-a of R2018 had a DWGPROPS; AutoCAD 2021 read it, and saved it as R2018 with the
            // header's form and no DWGPROPS.
            DxfDatabase db = DxfDatabase.Parse(DxfObjectsEzdxfDrawings.R2018);
            Assert.Contains("DWGPROPS", db.ToText(), StringComparison.Ordinal);

            var builder = new DxfDatabaseSummaryInfoBuilder(db.SummaryInfo) { Title = "Plate B-13" };
            db.SummaryInfo = builder.ToDatabaseSummaryInfo();

            string text = db.ToText();
            Assert.DoesNotContain("DWGPROPS", text, StringComparison.Ordinal);
            Assert.Contains("  9\n$TITLE\n  1\nPlate B-13\n  9\n$SUBJECT\n  1\nSteel plates\n  9\n$AUTHOR\n  1\nJ. Doe\n", text, StringComparison.Ordinal);
            Assert.Contains("  9\n$CUSTOMPROPERTYTAG\n  1\nProject\n  9\n$CUSTOMPROPERTY\n  1\nGrid A\n  9\n$CUSTOMPROPERTYTAG\n  1\nPhase\n  9\n$CUSTOMPROPERTY\n  1\n2\n", text, StringComparison.Ordinal);
            DxfDatabaseSummaryInfo read = DxfDatabase.Parse(text).SummaryInfo;
            Assert.Equal("Plate B-13", read.Title);
            Assert.Equal("Checked", read.Comments);
            Assert.Equal(new[] { "Project=Grid A", "Phase=2" }, read.CustomProperties.Select(p => p.Key + "=" + p.Value));
        }

        [Fact]
        public void ATextOfADrawingOfR2018MadeEmpty_LeavesTheHeader_AndMoreThanTenCustomPropertiesAreKept()
        {
            // AutoCAD 2021 writes no $TITLE in a drawing that has no title; the header's pairs are not held to the ten groups
            // of DWGPROPS.
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            db.SummaryInfo = Plate();
            var builder = new DxfDatabaseSummaryInfoBuilder(db.SummaryInfo) { Title = string.Empty };

            for (int i = 0; i < 11; i++)
            {
                builder.CustomPropertyTable.Add(new KeyValuePair<string, string>("P" + i.ToString(CultureInfo.InvariantCulture), "v" + i.ToString(CultureInfo.InvariantCulture)));
            }

            db.SummaryInfo = builder.ToDatabaseSummaryInfo();

            string text = db.ToText();
            Assert.DoesNotContain("$TITLE", text, StringComparison.Ordinal);
            DxfDatabaseSummaryInfo read = DxfDatabase.Parse(text).SummaryInfo;
            Assert.Equal(13, read.CustomProperties.Count);
            Assert.Equal("P10=v10", read.CustomProperties[12].Key + "=" + read.CustomProperties[12].Value);
        }

        [Fact]
        public void ThePropertiesOfADrawingOfR2000_AreStillADwgprops_AndNotTheHeadersForm()
        {
            // AutoCAD 2021 saved 26-b as R2000 with a DWGPROPS and no $CUSTOMPROPERTYTAG.
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);

            db.SummaryInfo = Plate();

            string text = db.ToText();
            Assert.Contains("DWGPROPS COOKIE", text, StringComparison.Ordinal);
            Assert.DoesNotContain("$CUSTOMPROPERTY", text, StringComparison.Ordinal);
            Assert.Equal(new[] { "Project=P4 checks", "Checked by=GH" }, DxfDatabase.Parse(text).SummaryInfo.CustomProperties.Select(p => p.Key + "=" + p.Value));
        }

        #endregion

        #region Point 51: a MINSERT of more than one cell is not exploded

        /// <summary>The MINSERT of 51: a block of one circle of radius 3 at the origin, 3 columns 10 apart by 2 rows 8 apart.</summary>
        private static DxfMInsertBlock Grid(out DxfDatabase db)
        {
            db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            var ring = new DxfBlockTableRecord { Name = "GH_RING" };
            db.BlockTable.Add(ring);
            ring.AppendEntity(new DxfCircle(P3(0, 0, 0), new GeoVector3(0, 0, 1), 3.0));
            var grid = new DxfMInsertBlock(P3(0, 0, 0), ring, 3, 2, 10.0, 8.0);
            db.ModelSpace.AppendEntity(grid);
            return grid;
        }

        [Fact]
        public void AMInsertOfSixCells_IsNotExploded_AsAutoCadRefusedIt()
        {
            // Plugin v1 and v2, 51: AutoCAD 2021's Explode() of the MINSERT of 3 columns by 2 rows threw eNotApplicable.
            DxfMInsertBlock grid = Grid(out _);

            Assert.Throws<NotSupportedException>(() => grid.Explode());
        }

        [Fact]
        public void AMInsertOfSixCellsExplodedIntoItsOwner_GivesTheCircleOfItsFirstCell_AsAutoCadGaveIt()
        {
            // Plugin v2, 51: AutoCAD 2021's ExplodeToOwnerSpace() of it made one CIRCLE, at (0, 0, 0) of radius 3.
            DxfMInsertBlock grid = Grid(out DxfDatabase db);
            DxfEntity[] before = db.ModelSpace.ToArray();

            grid.ExplodeToOwnerSpace();

            DxfEntity[] made = db.ModelSpace.Where(e => !before.Contains(e)).ToArray();
            var circle = Assert.IsType<DxfCircle>(Assert.Single(made));
            Assert.True(P3(0, 0, 0).IsEqualTo(circle.Center, Tol));
            Assert.Equal(3.0, circle.Radius);
            Assert.False(grid.IsErased);
        }

        [Fact]
        public void AMInsertOfSixCells_HasNoBestFit_AndOneOfOneCellIsNotExplodedEither()
        {
            // Plugin v8, 51: AutoCAD 2021's GeometryExtentsBestFit() refused it (eInvalidExtents), and its Explode() refused
            // a MINSERT of one cell too (D16).
            DxfMInsertBlock grid = Grid(out _);

            Assert.Throws<InvalidOperationException>(() => grid.GeometryExtentsBestFit(GeoTransform3.Translation(new GeoVector3(0, 0, 0.5))));

            grid.Columns = 1;
            grid.Rows = 1;
            Assert.Throws<NotSupportedException>(() => grid.Explode());
        }

        #endregion
    }
}
