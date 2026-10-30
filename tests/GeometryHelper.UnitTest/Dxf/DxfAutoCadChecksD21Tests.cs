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
    /// What followed from AutoCAD 2021's answers to plugin run v13 of 31 October 2026: a cell of the flag 4 outside every range
    /// is linked where it holds a content (73p); a spline given NURBS data keeps the flags 1 and 2 of a curve closed round a
    /// smooth seam and loses the flags 32 and 1024 (17k); fit data set keep a spline closed, and set periodic fit AutoCAD's
    /// periodic cubic (17l); and a spline of the flag 2048 is periodic only where its seam runs on smoothly (17m). Each drawing
    /// is built in the form AutoCAD wrote, read as a file is.
    /// </summary>
    public class DxfAutoCadChecksD21Tests
    {
        private static readonly Tolerance Tight = new Tolerance(1E-9, 1E-9, 1E-9);

        private static GeoPoint3 P3(double x, double y, double z) => new GeoPoint3(x, y, z);

        private static string G(int code, string value) => code.ToString(CultureInfo.InvariantCulture).PadLeft(3) + "\n" + value + "\n";

        private static string G(int code, double value) => G(code, value.ToString("R", CultureInfo.InvariantCulture));

        private static DxfTable Table(DxfDatabase db) => DxfAutoCadChecksD16Tests.Table(db);

        private static string Flags(DxfSpline spline) => spline.Item.Groups.First(g => g.TypeCode == 70).Value.Trim();

        #region Point 73p: a cell of the flag 4 outside every range

        private static readonly string Begin = G(1, "LINKEDTABLEDATACELL_BEGIN");

        // The content AutoCAD 2021 gave the cells of a row of data of its 77 (95 1, a value of no text), where the TABLECONTENT has 95 0.
        private static readonly string Content = G(95, "1") + G(302, "CONTENT") + G(1, "CELLCONTENT_BEGIN") + G(90, "1") + G(300, "VALUE") + G(93, "7")
            + G(90, "0") + G(94, "0") + G(300, string.Empty) + G(302, string.Empty) + G(304, "ACVALUE_END") + G(91, "0") + G(309, "CELLCONTENT_END")
            + G(1, "FORMATTEDCELLCONTENT_BEGIN") + G(170, "0") + G(309, "FORMATTEDCELLCONTENT_END");

        /// <summary>
        /// The table F6 of AutoCAD's linked by the library over (1, 0) to (1, 1), as the library linked 73-r, the cells (row,
        /// column) of its TABLECONTENT B03 then given the states (90) named, and a content where asked.
        /// </summary>
        private static DxfDatabase LinkedOverRowOne(params (int Row, int Column, string State, bool Content)[] given)
        {
            DxfDatabase db = DxfDatabase.Parse(DxfAutoCadChecksD16Tests.LinkedByAutoCad(DxfDimensionDrawings.R2018));
            DxfTable table = Table(db);
            table.RemoveDataLink();
            table.SetDataLink(new DxfCellRange(1, 0, 1, 1), (DxfDataLink)db.DataLinkDictionary["GH_PARTS"]);

            string text = db.ToText();
            string content = DxfAutoCadChecksD16Tests.Record(text, "B03");
            string[] cells = content.Split(new[] { Begin }, StringSplitOptions.None);

            foreach (var (row, column, state, held) in given)
            {
                int k = 1 + row * 2 + column;
                int end = cells[k].IndexOf('\n', cells[k].IndexOf('\n') + 1) + 1;
                Assert.StartsWith(" 90\n", cells[k], StringComparison.Ordinal);
                cells[k] = G(90, state) + cells[k].Substring(end);

                if (held)
                {
                    int none = cells[k].IndexOf(G(95, "0"), StringComparison.Ordinal);
                    Assert.True(none > 0);
                    cells[k] = cells[k].Substring(0, none) + Content + cells[k].Substring(none + G(95, "0").Length);
                }
            }

            return DxfDatabase.Parse(text.Replace(content, string.Join(Begin, cells)));
        }

        [Fact]
        public void ACellOfTheFlag4OutsideEveryRange_HoldingAContent_IsLinked_AsAutoCadReadTheTitleAndACellOfData()
        {
            // Plugin v13, 73p: the library's link over (1, 0) to (1, 1), the title (0, 0), its content Parts, and (2, 1) of a row of
            // data, a content of no text, both given the state 4: AutoCAD 2021 read both as linked, with no range and no link.
            DxfDatabase db = LinkedOverRowOne((0, 0, "4", true), (2, 1, "4", true));

            foreach (DxfTable read in new[] { Table(db), Table(DxfDatabase.Parse(db.ToText())) })
            {
                Assert.True(read.IsLinked(0, 0));
                Assert.False(read.IsLinked(0, 1));
                Assert.True(read.IsLinked(1, 0));
                Assert.False(read.IsLinked(1, 1));
                Assert.False(read.IsLinked(2, 0));
                Assert.True(read.IsLinked(2, 1));
                Assert.Null(read.GetDataLinkRange(0, 0));
                Assert.Null(read.GetDataLinkRange(2, 1));
                Assert.Null(read.GetDataLink(2, 1));
                Assert.Equal(new DxfCellRange(1, 0, 1, 1), read.GetDataLinkRange(1, 1));
            }
        }

        [Fact]
        public void ACellOfTheFlag4OutsideEveryRange_HoldingNoContent_IsNotLinked_NorACellOfAContentAlone()
        {
            // Plugin v12, 73m: (0, 1) of the state 4 and 95 0, outside every range, AutoCAD 2021 read as not linked; a content with
            // the state 0 makes no cell linked either.
            DxfDatabase db = LinkedOverRowOne((0, 1, "4", false), (2, 0, "0", true));
            DxfTable table = Table(db);

            Assert.False(table.IsLinked(0, 1));
            Assert.False(table.IsLinked(2, 0));
            Assert.True(table.IsLinked(1, 0));
        }

        #endregion

        #region The splines AutoCAD saved

        /// <summary>
        /// A cubic as AutoCAD 2021 saved it, its group 70 as given: its control points, weights where given, and knots, no fit
        /// points (74 0), as its v8-17f-autocad-knot-halfway.dxf (70 3115) and -closed-clamped.dxf (70 11) hold them.
        /// </summary>
        private static DxfSpline SavedByAutoCad(int flags, double[] knots, GeoPoint3[] points, double[] weights = null)
            => ByHandle<DxfSpline>(DrawingOf(flags, knots, points, weights), "F00");

        /// <summary>The drawing of the R2018 file of ezdxf holding the cubic F00 as <see cref="SavedByAutoCad"/> gives it.</summary>
        private static DxfDatabase DrawingOf(int flags, double[] knots, GeoPoint3[] points, double[] weights)
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

            foreach (double weight in weights ?? new double[0])
            {
                record.Append(G(41, weight));
            }

            foreach (GeoPoint3 point in points)
            {
                record.Append(G(10, point.X)).Append(G(20, point.Y)).Append(G(30, point.Z));
            }

            string text = DxfFileEzdxfDrawings.R2018.Replace("\r\n", "\n").Replace("$HANDSEED\n  5\nA1\n", "$HANDSEED\n  5\nF01\n")
                .Replace("  0\nENDSEC\n  0\nSECTION\n  2\nOBJECTS\n", record + "  0\nENDSEC\n  0\nSECTION\n  2\nOBJECTS\n");
            return DxfDatabase.Parse(text);
        }

        private static readonly double[] HalfwayKnots = { 0.0, 0, 0, 0, 30, 45, 55, 80, 80, 80, 80 };

        // AutoCAD's periodic cubic through (0, 0), (30, 0), (30, 15) and (20, 15), its knots 0, 30, 45, 55 and 80.
        private static DxfSpline Halfway(int flags) => ByHandle<DxfSpline>(HalfwayDrawing(flags), "F00");

        private static DxfDatabase HalfwayDrawing(int flags) => DrawingOf(
            flags,
            HalfwayKnots,
            new[]
            {
                P3(0.0000000000000007, -0.0000000000000004, 0), P3(1.9696772796188, -6.041152263374485, 0), P3(33.61425167857916, -10.88065843621399, 0),
                P3(33.34423507327991, 17.56241426611796, 0), P3(14.65605371453325, 14.81893004115226, 0), P3(-1.641397733015665, 5.03429355281207, 0),
                P3(0.0000000000000007, -0.0000000000000004, 0),
            },
            null);

        // AutoCAD's closed cubic through (0, 0), (40, 0), (50, 30) and (10, 25), its knots the chords; a control point moved as given.
        private static DxfSpline ClosedClamped(int flags, int moved = -1, double by = 0.0) => ByHandle<DxfSpline>(ClosedClampedDrawing(flags, moved, by), "F00");

        private static DxfDatabase ClosedClampedDrawing(int flags, int moved, double by)
        {
            GeoPoint3[] points =
            {
                P3(0.0000000000000016, 0, 0), P3(4.762265129346506, -9.87516948054726, 0), P3(44.57965357801522, -10.52612047361797, 0),
                P3(60.52497076467425, 41.58068210387638, 0), P3(8.562414603983973, 34.00936527765936, 0), P3(-3.205697822100078, 6.647426893891477, 0),
                P3(0.0000000000000016, 0, 0),
            };

            if (moved >= 0)
            {
                points[moved] = P3(points[moved].X, points[moved].Y + by, 0);
            }

            return DrawingOf(flags, new[] { 0.0, 0, 0, 0, 40, 71.6227766016838, 111.9340653431765, 138.859889378849, 138.859889378849, 138.859889378849, 138.859889378849 }, points, null);
        }

        #endregion

        #region Point 17k: NURBS data given to a closed spline

        [Fact]
        public void AutoCadsClosedCubicGivenNurbsDataNotPeriodicOrNotClosed_KeepsIts70Of11_AsAutoCadWroteIt()
        {
            // Plugin v13, 17k: AutoCAD 2021 gave its closed cubic of 70 11 its own NURBS data, not periodic, and saved 70 11; given
            // them with closed false, saved 70 11 again, the control points as they were, and read it closed. Its ends meet, and
            // its seam is smooth.
            DxfSpline notPeriodic = ClosedClamped(11);
            DxfNurbsData data = notPeriodic.NurbsData;

            notPeriodic.SetNurbsData(data.Degree, data.Rational, true, false, data.GetControlPoints(), data.GetKnots(), data.GetWeights());

            Assert.Equal("11", Flags(notPeriodic));
            Assert.False(notPeriodic.IsPeriodic);

            DxfSpline open = ClosedClamped(11);
            open.SetNurbsData(data.Degree, data.Rational, false, false, data.GetControlPoints(), data.GetKnots(), data.GetWeights());

            Assert.Equal("11", Flags(open));
            Assert.True(open.Closed);
        }

        [Fact]
        public void AutoCadsPeriodicCubicGivenItsNurbsDataBack_Is2059_The1024And32TakenOff()
        {
            // Plugin v13, 17k: its periodic cubic of 70 3115 given its own NURBS data back AutoCAD 2021 saved as 70 2059, the same
            // 7 control points and 11 knots.
            DxfSpline spline = Halfway(3115);
            DxfNurbsData data = spline.NurbsData;

            spline.SetNurbsData(data.Degree, data.Rational, data.Closed, true, data.GetControlPoints(), data.GetKnots(), data.GetWeights());

            Assert.Equal("2059", Flags(spline));
            Assert.True(spline.IsPeriodic);
            Assert.Equal(0, spline.NumFitPoints);
            Assert.Equal(7, spline.NumControlPoints);
        }

        [Fact]
        public void ASplineGivenNurbsData_IsWrittenClosedOnlyWhereItsEndsMeet_AndWithTheFlag2OnlyWhereItsSeamIsSmooth()
        {
            // AutoCAD writes the flag 2 on a closed spline whose seam runs on smoothly (11, 1067) and not on one with a corner there
            // (its 1065 of 17l, turning by 114 degrees): an open cubic stays open, a cubic closed round a corner is written 1.
            var open = new DxfSpline(3, false, false, false, new[] { P3(0, 0, 0), P3(10, 10, 0), P3(20, 10, 0), P3(30, 0, 0) }, new[] { 0.0, 0, 0, 0, 1, 1, 1, 1 }, null, 1E-10, 1E-9);
            Assert.Equal("0", Flags(open));

            var corner = new DxfSpline(3, false, false, false, new[] { P3(0, 0, 0), P3(10, 0, 0), P3(10, 10, 0), P3(0, 10, 0), P3(0, 0, 0) }, new[] { 0.0, 0, 0, 0, 1, 2, 2, 2, 2 }, null, 1E-10, 1E-9);
            Assert.Equal("1", Flags(corner));
            Assert.True(corner.Closed);
        }

        #endregion

        #region Point 17l: fit data set on a closed spline

        // The fit points AutoCAD 2021 gave its 1035 and 3115, the point 1 moved up by 1 as the plugin moved it.
        private static readonly GeoPoint3[] ClampedFit =
        {
            P3(1.6E-15, 0, 0), P3(40, 1.0000000000000018, 0), P3(49.999999999999986, 30.000000000000014, 0), P3(10, 25, 0), P3(8.5136505941951943E-15, 7.180282345832494E-15, 0),
        };

        private static readonly GeoPoint3[] HalfwayFit =
        {
            P3(6.6613381477509392E-16, -4.4408920985006262E-16, 0), P3(30, 0.99999999999999467, 0), P3(30.000000000000004, 14.999999999999995, 0),
            P3(20.000000000000004, 14.999999999999996, 0), P3(-4.3820922566520504E-15, -3.109928246216526E-15, 0),
        };

        // The tangent AutoCAD 2021 gave at both ends of its 1035.
        private static readonly GeoVector3 ClampedTangent = new GeoVector3(0.35716988470098787, -0.74063771104104448, 0);

        private static DxfFitData Fit(GeoPoint3[] points, bool periodic, GeoVector3 tangent) => new DxfFitData(points, 3, 1E-10)
        {
            StartTangent = tangent,
            EndTangent = tangent,
            TangentsExist = true,
            KnotParam = DxfKnotParameterizationEnum.Chord,
            Periodic = periodic,
        };

        [Fact]
        public void FitDataNotPeriodicSetOnAClosedSpline_KeepItClosed_70Of1067_AsAutoCadWroteIt()
        {
            // Plugin v13, 17l: AutoCAD 2021 set the fit data of its 1035, its fit point 1 moved, not periodic, and saved 70 1067,
            // closed, its 5 fit points and its tangents written; the library opened it (70 8).
            DxfSpline spline = ClosedClamped(1035);

            spline.FitData = Fit(ClampedFit, false, ClampedTangent);

            Assert.Equal("1067", Flags(spline));
            Assert.True(spline.Closed);
            Assert.False(spline.IsPeriodic);
            Assert.Equal(5, spline.NumFitPoints);
            Assert.True(ClampedFit[1].IsEqualTo(spline.GetFitPointAt(1), Tight));
        }

        [Fact]
        public void FitDataNotPeriodicOfNoTangentsSetOnAClosedSpline_KeepItClosed_WithTheFlagsOfFitData()
        {
            // Plugin v13, 17l: on its 3115, of no tangents, AutoCAD 2021 saved 70 1065, closed, with the flags 32 and 1024 of fit
            // data, the flag 2048 taken off; its seam has a corner, where the library's runs on smoothly (the flag 2 is its own).
            DxfSpline spline = Halfway(3115);

            spline.FitData = Fit(HalfwayFit, false, new GeoVector3(0, 0, 0));

            Assert.True(spline.Closed);
            Assert.False(spline.IsPeriodic);
            Assert.Equal(1 | 8 | 32 | 1024, int.Parse(Flags(spline), CultureInfo.InvariantCulture) & (1 | 8 | 32 | 1024 | 2048));
            Assert.Equal(5, spline.NumFitPoints);
        }

        [Fact]
        public void FitDataSetPeriodic_GiveAutoCadsPeriodicCubicThroughThePoints_70Of3115()
        {
            // Plugin v13, 17l: AutoCAD 2021 set the fit data periodic on its 1035 and its 3115 and saved each 70 3115, no fit points
            // (74 0) and no tangents, the periodic cubic through the points at the lengths of the chords, clamped into 7 control
            // points and 11 knots; read again, periodic, its 5 fit points the points at the knots.
            var cases = new[]
            {
                (ClosedClampedDrawing(1035, -1, 0.0), ClampedFit, new[] { 40.0124980474851, 70.68822134784105, 110.9995100893338, 137.9253341250063 }, new[]
                {
                    P3(7E-16, -4E-16, 0), P3(4.779817524211599, -9.706152486512153, 0), P3(44.0674242924151, -9.267442935706313, 0), P3(60.3933474585412, 41.45727892221826, 0),
                    P3(8.493718016367543, 34.09840920261455, 0), P3(-3.216508137702602, 6.531613037633318, 0), P3(7E-16, -4E-16, 0),
                }),
                (HalfwayDrawing(3115), HalfwayFit, new[] { 30.01666203960727, 44.01666203960727, 54.01666203960727, 79.01666203960727 }, new[]
                {
                    P3(-3E-15, -9E-16, 0), P3(2.034717904401862, -5.851804429439167, 0), P3(33.20606088794133, -9.742652912678967, 0), P3(33.06706263814898, 17.56725547365124, 0),
                    P3(14.60035561747842, 14.80329871583492, 0), P3(-1.694657038911454, 4.873796778034191, 0), P3(-3E-15, -9E-16, 0),
                }),
            };

            foreach (var (db, fit, knots, points) in cases)
            {
                ByHandle<DxfSpline>(db, "F00").FitData = Fit(fit, true, ClampedTangent);

                foreach (DxfSpline read in new[] { ByHandle<DxfSpline>(db, "F00"), ByHandle<DxfSpline>(DxfDatabase.Parse(db.ToText()), "F00") })
                {
                    Assert.Equal("3115", Flags(read));
                    Assert.True(read.IsPeriodic);
                    Assert.True(read.Closed);
                    Assert.DoesNotContain(read.Item.Groups, g => g.TypeCode == 11 || g.TypeCode == 12 || g.TypeCode == 13);
                    Assert.Equal(7, read.NumControlPoints);

                    for (int i = 0; i < 7; i++)
                    {
                        Assert.True(points[i].IsEqualTo(read.GetControlPointAt(i), Tight), "control point " + i + ": " + read.GetControlPointAt(i));
                    }

                    double[] wanted = new[] { 0.0, 0, 0, 0 }.Concat(knots.Take(3)).Concat(Enumerable.Repeat(knots[3], 4)).ToArray();
                    Assert.Equal(wanted.Length, read.Knots.Count);

                    for (int i = 0; i < wanted.Length; i++)
                    {
                        Assert.True(Math.Abs(wanted[i] - read.Knots[i]) <= 1E-9, "knot " + i + ": " + read.Knots[i]);
                    }

                    Assert.Equal(5, read.NumFitPoints);

                    for (int i = 0; i < 5; i++)
                    {
                        Assert.True(fit[i].IsEqualTo(read.GetFitPointAt(i), Tight), "fit point " + i + ": " + read.GetFitPointAt(i));
                    }
                }
            }
        }

        #endregion

        #region Point 17m: the seam of a spline of the flag 2048

        [Fact]
        public void ASplineOfTheFlag2048WhoseSeamIsNotSmooth_IsNotPeriodic_AsAutoCadReadIt()
        {
            // Plugin v13, 17m: AutoCAD 2021 read 2059 (its closed cubic of the flag 2048) as periodic, and not with its control point
            // 1 moved down by 5 (17-n) or its point 5 up by 5 (17-o), each a corner at the seam: 7 control points and 11 knots.
            Assert.True(ClosedClamped(2059).IsPeriodic);
            Assert.False(ClosedClamped(2059, 1, -5.0).IsPeriodic);
            Assert.False(ClosedClamped(2059, 5, 5.0).IsPeriodic);
            Assert.True(ClosedClamped(2059, 1, -5.0).Closed);
        }

        [Fact]
        public void ARationalSplineOfTheFlag2048AndASmoothSeam_IsPeriodic()
        {
            // Plugin v13, 17m: AutoCAD 2021 read 17-m, AutoCAD's periodic cubic of 2091 given the weights 1, 2, 1, 1 and clamped as
            // AutoCAD writes one (70 2095), as periodic.
            DxfSpline spline = SavedByAutoCad(
                2095,
                HalfwayKnots,
                new[]
                {
                    P3(2.9475862219173026, 2.9803434719635833, 0), P3(1.9696772796188, -6.041152263374486, 0), P3(33.614251678579166, -10.880658436213993, 0),
                    P3(33.34423507327991, 17.562414266117962, 0), P3(14.65605371453325, 14.81893004115226, 0), P3(3.5051658819997815, 8.12417875965634, 0),
                    P3(2.9475862219173026, 2.9803434719635833, 0),
                },
                new[] { 1.2517482517482517, 1.0, 1.0, 1.0, 2.0, 1.4615384615384617, 1.2517482517482517 });

            Assert.True(spline.IsRational);
            Assert.True(spline.IsPeriodic);
        }

        #endregion
    }
}
