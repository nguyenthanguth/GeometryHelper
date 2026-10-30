using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// What followed from AutoCAD 2021's answers to plugin run v11 of 31 October 2026: a cell of a range linked in the
    /// TABLECONTENT is linked at the range's top left cell and wherever its own state has the flag 4 (73k, 73l); a width set on
    /// an MTEXT in columns before R2018 is not saved (09h); and a spline is periodic by the flag 2048 of its group 70, and has
    /// the points of its curve at its knots as fit points by the flag 1024 where its DXF holds none (17h). Each drawing is one
    /// of ezdxf's, or built in the form AutoCAD wrote, read as a file is.
    /// </summary>
    public class DxfAutoCadChecksD19Tests
    {
        private static readonly Tolerance Tight = new Tolerance(1E-9, 1E-9, 1E-9);

        private static GeoPoint3 P3(double x, double y, double z) => new GeoPoint3(x, y, z);

        private static string G(int code, string value) => code.ToString(CultureInfo.InvariantCulture).PadLeft(3) + "\n" + value + "\n";

        private static string G(int code, double value) => G(code, value.ToString("R", CultureInfo.InvariantCulture));

        private static DxfTable Table(DxfDatabase db) => DxfAutoCadChecksD16Tests.Table(db);

        private static string Flags(DxfSpline spline) => spline.Item.Groups.First(g => g.TypeCode == 70).Value.Trim();

        private static string Record(string text, string handle) => DxfAutoCadChecksD16Tests.Record(text, handle);

        #region Points 73k and 73l: the cells of a range linked in the TABLECONTENT

        /// <summary>
        /// The table F6 of AutoCAD's linked by the library over (1, 0) to (2, 1), as 73-h, its TABLECONTENT B03's cell (row,
        /// column) then given the state <paramref name="state"/> (90), the other groups kept.
        /// </summary>
        private static DxfDatabase LinkedWithState(int row, int column, string state)
        {
            DxfDatabase db = DxfDatabase.Parse(DxfAutoCadChecksD16Tests.LinkedByAutoCad(DxfDimensionDrawings.R2018));
            DxfTable table = Table(db);
            table.RemoveDataLink();
            table.SetDataLink(new DxfCellRange(1, 0, 2, 1), (DxfDataLink)db.DataLinkDictionary["GH_PARTS"]);

            string text = db.ToText();
            string content = Record(text, "B03");
            string begin = G(1, "LINKEDTABLEDATACELL_BEGIN");
            string[] cells = content.Split(new[] { begin }, StringSplitOptions.None);
            int k = 1 + row * 2 + column;
            int end = cells[k].IndexOf('\n', cells[k].IndexOf('\n') + 1) + 1;
            Assert.StartsWith(" 90\n", cells[k], StringComparison.Ordinal);
            cells[k] = G(90, state) + cells[k].Substring(end);
            return DxfDatabase.Parse(text.Replace(content, string.Join(begin, cells)));
        }

        [Fact]
        public void ACellOfALinkedRangeWithTheFlag4InItsState_IsLinked_ThoughNotTheRangesTopLeft()
        {
            // Plugin v11, 73k: AutoCAD 2021 read the library's link over (1, 0) to (2, 1), its TABLECONTENT's cell (2, 1) given the
            // state 4 too, as linked at (1, 0) and (2, 1), not at (1, 1) or (2, 0); every cell of the range gave it and the link 142.
            DxfDatabase db = LinkedWithState(2, 1, "4");

            foreach (DxfTable read in new[] { Table(db), Table(DxfDatabase.Parse(db.ToText())) })
            {
                Assert.True(read.IsLinked(1, 0));
                Assert.False(read.IsLinked(1, 1));
                Assert.False(read.IsLinked(2, 0));
                Assert.True(read.IsLinked(2, 1));
                Assert.False(read.IsLinked(0, 0));

                foreach (var (row, column) in new[] { (1, 0), (1, 1), (2, 0), (2, 1) })
                {
                    Assert.Equal(new DxfCellRange(1, 0, 2, 1), read.GetDataLinkRange(row, column));
                    Assert.Equal("GH_PARTS", read.GetDataLink(row, column).Name);
                }
            }
        }

        [Fact]
        public void TheTopLeftCellOfALinkedRange_IsLinked_WithNoFlag4InItsState()
        {
            // Plugin v11, 73l: the range's top left cell (1, 0) of the state 0, its link run kept, AutoCAD 2021 still read as
            // linked, and (1, 0) alone; every cell of the range gave it.
            DxfDatabase db = LinkedWithState(1, 0, "0");
            DxfTable table = Table(db);

            Assert.True(table.IsLinked(1, 0));
            Assert.False(table.IsLinked(1, 1));
            Assert.False(table.IsLinked(2, 0));
            Assert.False(table.IsLinked(2, 1));
            Assert.Equal(new DxfCellRange(1, 0, 2, 1), table.GetDataLinkRange(2, 1));
        }

        #endregion

        #region Point 09h: a width set on an MTEXT in columns before R2018

        /// <summary>
        /// The R2000 drawing of ezdxf with the MTEXT 73 in two static columns 50 wide with a gutter of 10, as AutoCAD 2021 saved
        /// its 09-a as R2000: the column data, the second column an MTEXT of its own (B1) linked by ACAD_MTEXT_COLUMNS, its 41 50.
        /// </summary>
        private static string StaticColumnsOfR2000()
        {
            string info = G(1000, "ACAD_MTEXT_COLUMN_INFO_BEGIN") + G(1070, "75") + G(1070, "1") + G(1070, "79") + G(1070, "0") + G(1070, "76") + G(1070, "2")
                + G(1070, "78") + G(1070, "0") + G(1070, "48") + G(1040, "50.0") + G(1070, "49") + G(1040, "10.0") + G(1000, "ACAD_MTEXT_COLUMN_INFO_END");
            string column = G(0, "MTEXT") + G(5, "B1") + G(330, "17") + G(100, "AcDbEntity") + G(8, "0") + G(100, "AcDbMText") + G(10, "60.0") + G(20, "40.0")
                + G(30, "0.0") + G(40, "3.0") + G(41, "50.0") + G(71, "1") + G(1, "column") + G(1001, "ACAD") + info;
            string data = G(1001, "ACAD") + info + G(1000, "ACAD_MTEXT_COLUMNS_BEGIN") + G(1070, "47") + G(1070, "2") + G(1005, "B1") + G(1000, "ACAD_MTEXT_COLUMNS_END");
            return DxfFileEzdxfDrawings.R2000
                .Replace("\n 41\n40.0\n 71\n1\n  1\n{\\fArial|b1;Bold}\\Pline two é\n  0\n", "\n 41\n50.0\n 71\n1\n  1\n{\\fArial|b1;Bold}\\Pline two é\n" + data + column + "  0\n");
        }

        [Fact]
        public void AWidthSetOnAnMTextInColumnsBeforeR2018_LeavesIts41AndItsColumns_AsAutoCadSavedThem()
        {
            // Plugin v11, 09h: AutoCAD 2021 set Width = 80 on 8D, two static columns 50 wide with a gutter of 10, of its own R2000
            // and of the library's R2007, read 80 back, and saved each with 41 50 and the column data as they were; read again,
            // 110 wide, the columns 50, the gutter 10.
            DxfDatabase r2007 = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018.Replace("AC1032", "AC1021"));
            ByHandle<DxfMText>(r2007, "73").SetStaticColumns(50.0, 10.0, 2);

            foreach (DxfDatabase db in new[] { DxfDatabase.Parse(StaticColumnsOfR2000()), r2007 })
            {
                DxfMText mtext = ByHandle<DxfMText>(db, "73");
                Assert.Equal(110.0, mtext.Width);
                string before = db.ToText();

                mtext.Width = 80.0;

                Assert.Equal(before, db.ToText());
                Assert.Equal(110.0, mtext.Width);
                Assert.Equal(50.0, mtext.ColumnWidth);
                Assert.Equal(10.0, mtext.ColumnGutterWidth);
                Assert.Equal(2, mtext.ColumnCount);
                Assert.Throws<ArgumentOutOfRangeException>(() => mtext.Width = -1.0);
            }
        }

        [Fact]
        public void AWidthSetOnAnMTextOfR2018InColumns_OrOfR2007InNone_IsWrittenAsIts41()
        {
            // AutoCAD keeps the width of all the columns in the 41 of R2018, and an MTEXT of no columns wraps at its 41.
            DxfDatabase r2018 = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfMText columns = ByHandle<DxfMText>(r2018, "73");
            columns.SetStaticColumns(50.0, 10.0, 2);
            columns.Width = 80.0;
            Assert.Contains("\n 41\n80\n", Record(r2018.ToText(), "73"), StringComparison.Ordinal);

            DxfDatabase r2007 = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018.Replace("AC1032", "AC1021"));
            DxfMText plain = ByHandle<DxfMText>(r2007, "73");
            plain.Width = 80.0;
            Assert.Equal(80.0, ByHandle<DxfMText>(DxfDatabase.Parse(r2007.ToText()), "73").Width);
        }

        #endregion

        #region Point 17h: the flags of a periodic spline and of fit points at the knots

        /// <summary>
        /// A closed cubic as AutoCAD 2021 saved it, its group 70 as given: its control points and clamped knots, no fit points
        /// (74 0), as its v8-17f-autocad-knot-halfway.dxf (70 3115) and -closed-clamped.dxf (70 11) hold them.
        /// </summary>
        private static DxfSpline SavedByAutoCad(int flags, double[] knots, GeoPoint3[] points)
        {
            var record = new StringBuilder();
            record.Append(G(0, "SPLINE")).Append(G(5, "F00")).Append(G(330, "17")).Append(G(100, "AcDbEntity")).Append(G(8, "0")).Append(G(100, "AcDbSpline"))
                .Append(G(210, "0.0")).Append(G(220, "0.0")).Append(G(230, "1.0")).Append(G(70, flags.ToString(CultureInfo.InvariantCulture))).Append(G(71, "3"))
                .Append(G(72, knots.Length.ToString(CultureInfo.InvariantCulture))).Append(G(73, points.Length.ToString(CultureInfo.InvariantCulture)))
                .Append(G(74, "0")).Append(G(42, "0.000000001")).Append(G(43, "0.0000000001"));

            foreach (double knot in knots)
            {
                record.Append(G(40, knot));
            }

            foreach (GeoPoint3 point in points)
            {
                record.Append(G(10, point.X)).Append(G(20, point.Y)).Append(G(30, point.Z));
            }

            string text = DxfFileEzdxfDrawings.R2018.Replace("\r\n", "\n").Replace("$HANDSEED\n  5\nA1\n", "$HANDSEED\n  5\nF01\n")
                .Replace("  0\nENDSEC\n  0\nSECTION\n  2\nOBJECTS\n", record + "  0\nENDSEC\n  0\nSECTION\n  2\nOBJECTS\n");
            return ByHandle<DxfSpline>(DxfDatabase.Parse(text), "F00");
        }

        // AutoCAD's periodic cubic through (0, 0), (30, 0), (30, 15) and (20, 15), its knots 0, 30, 45, 55 and 80.
        private static DxfSpline Halfway(int flags) => SavedByAutoCad(
            flags,
            new[] { 0.0, 0, 0, 0, 30, 45, 55, 80, 80, 80, 80 },
            new[]
            {
                P3(0.0000000000000007, -0.0000000000000004, 0), P3(1.9696772796188, -6.041152263374485, 0), P3(33.61425167857916, -10.88065843621399, 0),
                P3(33.34423507327991, 17.56241426611796, 0), P3(14.65605371453325, 14.81893004115226, 0), P3(-1.641397733015665, 5.03429355281207, 0),
                P3(0.0000000000000007, -0.0000000000000004, 0),
            });

        // AutoCAD's closed cubic through (0, 0), (40, 0), (50, 30) and (10, 25), not periodic, its knots the chords.
        private static DxfSpline ClosedClamped(int flags) => SavedByAutoCad(
            flags,
            new[] { 0.0, 0, 0, 0, 40, 71.6227766016838, 111.9340653431765, 138.859889378849, 138.859889378849, 138.859889378849, 138.859889378849 },
            new[]
            {
                P3(0.0000000000000016, 0, 0), P3(4.762265129346506, -9.87516948054726, 0), P3(44.57965357801522, -10.52612047361797, 0),
                P3(60.52497076467425, 41.58068210387638, 0), P3(8.562414603983973, 34.00936527765936, 0), P3(-3.205697822100078, 6.647426893891477, 0),
                P3(0.0000000000000016, 0, 0),
            });

        [Fact]
        public void ASpline_IsPeriodicByTheFlag2048OfIts70Alone_AsAutoCadReadIt()
        {
            // Plugin v11, 17h: AutoCAD 2021 read its 3115 as periodic and its 11 not, 3115 less 2048 (1067) not, less 1024 (2091)
            // or 32 (3083) still; 11 and 2048 (2059) periodic, and 1024 (1035) or 32 (43) not. Each is closed (the flag 1).
            var cases = new[]
            {
                (Halfway(3115), true), (Halfway(1067), false), (Halfway(2091), true), (Halfway(3083), true),
                (ClosedClamped(11), false), (ClosedClamped(2059), true), (ClosedClamped(1035), false), (ClosedClamped(43), false),
            };

            foreach (var (spline, periodic) in cases)
            {
                Assert.Equal(Flags(spline) + ": " + periodic, Flags(spline) + ": " + spline.IsPeriodic);
                Assert.True(spline.Closed);
            }
        }

        [Fact]
        public void ASplineOfTheFlag1024AndNoFitPointsInItsDxf_HasThePointsAtItsKnotsAsFitPoints_AsAutoCadGaveThem()
        {
            // Plugin v11, 17h: AutoCAD 2021 gave 5 fit points to 3115, 1067 (less 2048) and 3083 (less 32), none to 2091 (less
            // 1024); 5 to 11 with 1024 (1035), none to 11, 2059 or 43: the points of the curve at the knots, the first again last.
            GeoPoint3[] halfway = { P3(0, 0, 0), P3(30, 0, 0), P3(30, 15, 0), P3(20, 15, 0), P3(0, 0, 0) };
            GeoPoint3[] clamped = { P3(0, 0, 0), P3(40, 0, 0), P3(50, 30, 0), P3(10, 25, 0), P3(0, 0, 0) };
            var cases = new[]
            {
                (Halfway(3115), halfway), (Halfway(1067), halfway), (Halfway(2091), null), (Halfway(3083), halfway),
                (ClosedClamped(11), null), (ClosedClamped(2059), null), (ClosedClamped(1035), clamped), (ClosedClamped(43), null),
            };

            foreach (var (spline, wanted) in cases)
            {
                string flags = Flags(spline);
                Assert.Equal(flags + ": " + (wanted?.Length ?? 0), flags + ": " + spline.NumFitPoints);
                Assert.Equal(wanted != null, spline.HasFitData);
                Assert.Equal(wanted != null ? DxfSplineType.FitPoints : DxfSplineType.ControlPoints, spline.Type);
                Assert.Equal(7, spline.NumControlPoints);

                for (int i = 0; wanted != null && i < wanted.Length; i++)
                {
                    Assert.True(wanted[i].IsEqualTo(spline.FitPoints[i], Tight), flags + ", fit point " + i + ": " + spline.FitPoints[i]);
                    Assert.True(wanted[i].IsEqualTo(spline.GetFitPointAt(i), Tight), flags + ", fit point " + i + ": " + spline.GetFitPointAt(i));
                }

                Assert.Throws<ArgumentOutOfRangeException>(() => spline.GetFitPointAt(wanted?.Length ?? 0));
            }
        }

        [Fact]
        public void ASplineOfTheFlag1024WhoseDataMakeNoSpline_HasNoFitPoints()
        {
            // A knot short of what the seven control points of a cubic ask for: no curve, so no points at its knots, and no throw.
            DxfSpline spline = SavedByAutoCad(3115, new[] { 0.0, 0, 0, 0, 30, 45, 55, 80, 80, 80 }, Enumerable.Range(0, 7).Select(i => P3(i, i % 2, 0)).ToArray());

            Assert.Equal(0, spline.NumFitPoints);
            Assert.False(spline.HasFitData);
            Assert.Empty(spline.FitPoints);
            Assert.Throws<ArgumentOutOfRangeException>(() => spline.GetFitPointAt(0));
        }

        [Fact]
        public void ASplineOfAutoCadsFlagsGivenNurbsDataNotPeriodic_IsNeitherPeriodicNorFitted()
        {
            // Its NURBS data set not periodic, AutoCAD's 3115 takes off the flag 2048, and the flags 1024 and 32 with its fit data:
            // 11, closed and planar, the flag 2 kept on its smooth seam, as AutoCAD 2021 wrote its 11 given data not periodic
            // (plugin v13, 17k); given them periodic, the flag 2048 is kept, 2059, as AutoCAD wrote its 3115 given its own data.
            DxfSpline spline = Halfway(3115);
            DxfNurbsData data = spline.NurbsData;

            spline.SetNurbsData(data.Degree, data.Rational, data.Closed, false, data.GetControlPoints(), data.GetKnots(), data.GetWeights());

            Assert.Equal("11", Flags(spline));
            Assert.False(spline.IsPeriodic);
            Assert.False(spline.HasFitData);

            DxfSpline kept = Halfway(3115);
            kept.SetNurbsData(data.Degree, data.Rational, data.Closed, true, data.GetControlPoints(), data.GetKnots(), data.GetWeights());
            Assert.Equal("2059", Flags(kept));
            Assert.True(kept.IsPeriodic);

            DxfSpline purged = Halfway(3115);
            purged.PurgeFitData();
            Assert.Equal("2091", Flags(purged));
            Assert.Equal(0, purged.NumFitPoints);
        }

        [Fact]
        public void ASplineTheLibraryMakesPeriodic_IsWrittenWithTheFlag2_WhichAutoCadReadsAsNotPeriodic()
        {
            // Plugin v8, 17f: AutoCAD 2021 read the library's 17-f, its 70 of 3, as not periodic. The flag 2 is written as before,
            // and its NURBS data carry it.
            var points = new[] { P3(0, 0, 0), P3(10, 0, 0), P3(10, 10, 0), P3(0, 0, 0) };
            var spline = new DxfSpline(3, false, true, true, points, new[] { 0.0, 0, 0, 0, 1, 1, 1, 1 }, null, 0.0000000001, 0.000000001);

            Assert.Equal("3", Flags(spline));
            Assert.False(spline.IsPeriodic);
            Assert.True(spline.NurbsData.Periodic);
            Assert.True(spline.Spline.NurbsData.Periodic);
        }

        #endregion
    }
}
