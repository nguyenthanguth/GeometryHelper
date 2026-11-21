using System;
using System.Globalization;
using System.Text.RegularExpressions;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;
using static GeometryHelper.Cad.UnitTest.LispEntityTests;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The eighteen <c>vlax-curve-*</c> functions on entity names, against AutoCAD 2021's accoreconsole: probes a3, a4 and a5 of
    /// the L4 measurements. The drawings of the probes are made here by the same typed commands, and each row is a form and the
    /// text AutoCAD printed for it, six significant digits as the console prints them. The number of AutoCAD's own counter in
    /// <c>bad argument value: AcDbCurve 48</c> is written <c>*</c>, as the harness normalises it. Rows marked <c>ours</c> are a
    /// choice of the spec where AutoCAD was not asked.
    /// </summary>
    public sealed class LispCurveTests : IDisposable
    {
        // Ends the routine a test left waiting, so no worker thread outlives its test.
        public void Dispose() => WaitingRoutines.EndAll();

        private static string V(CadCommandSession session, string form) =>
            Regex.Replace(Value(session, form), @"AcDbCurve \d+", "AcDbCurve *");

        // The drawing of probe a3: a LINE, a CIRCLE, an ARC, an open and a closed polyline, a TEXT and an ELLIPSE.
        internal static CadCommandSession Probe3()
        {
            CadCommandSession session = Session(Blank());
            Run(session, "LINE", "0,0", "10,0", Enter);
            V(session, "(setq eL (entlast))");
            Run(session, "CIRCLE", "20,0", "5");
            V(session, "(setq eC (entlast))");
            Run(session, "ARC", "C", "40,0", "45,0", "40,5");
            V(session, "(setq eA (entlast))");
            Run(session, "PLINE", "0,20", "10,20", "10,30", Enter);
            V(session, "(setq eP (entlast))");
            Run(session, "PLINE", "20,20", "30,20", "30,30", "C");
            V(session, "(setq eQ (entlast))");
            Run(session, "TEXT", "0,40", "2.5", "0", "abc", Enter);
            V(session, "(setq eT (entlast))");
            Run(session, "ELLIPSE", "50,0", "60,0", "3");
            V(session, "(setq eE (entlast))");
            V(session, "(vl-load-com)");
            return session;
        }

        [Fact]
        public void TheFunctions_AreNotThereBeforeVlLoadCom_AndSubrsAfter()
        {
            CadCommandSession session = Session(Blank());

            // AutoCAD 2021: nil before (vl-load-com), SUBR after (a3)
            Assert.Equal("nil", V(session, "(type vlax-curve-getStartPoint)"));
            V(session, "(vl-load-com)");
            Assert.Equal("SUBR", V(session, "(type vlax-curve-getStartPoint)"));
            Assert.Equal("SUBR", V(session, "(type vlax-curve-getClosestPointToProjection)"));
        }

        [Theory]
        [InlineData("(vlax-curve-getStartPoint eC)", "(25.0 0.0 0.0)")]
        [InlineData("(vlax-curve-getEndPoint eC)", "(25.0 0.0 0.0)")]
        [InlineData("(vlax-curve-getStartParam eC)", "0.0")]
        [InlineData("(vlax-curve-getEndParam eC)", "6.28319")]
        [InlineData("(vlax-curve-getDistAtParam eC pi)", "15.708")]
        [InlineData("(vlax-curve-getPointAtParam eC (/ pi 2))", "(20.0 5.0 0.0)")]
        [InlineData("(vlax-curve-getParamAtPoint eC '(15 0 0))", "3.14159")]
        [InlineData("(vlax-curve-getDistAtPoint eC '(20 5 0))", "7.85398")]
        [InlineData("(vlax-curve-getClosestPointTo eC '(30 0 0))", "(25.0 0.0 0.0)")]
        [InlineData("(vlax-curve-getArea eC)", "78.5398")]
        [InlineData("(vlax-curve-isClosed eC)", "T")]
        [InlineData("(vlax-curve-isPeriodic eC)", "T")]
        [InlineData("(vlax-curve-isPlanar eC)", "T")]
        [InlineData("(vlax-curve-getFirstDeriv eC 0)", "(0.0 5.0 0.0)")]
        [InlineData("(vlax-curve-getSecondDeriv eC 0)", "(-5.0 0.0 0.0)")]
        public void ACircle_GivesAutoCadsNumbers(string form, string expected)
        {
            // AutoCAD 2021: every row as printed (a3)
            Assert.Equal(expected, V(Probe3(), form));
        }

        [Theory]
        [InlineData("(vlax-curve-getStartPoint eA)", "(45.0 0.0 0.0)")]
        [InlineData("(vlax-curve-getEndPoint eA)", "(40.0 5.0 0.0)")]
        [InlineData("(vlax-curve-getStartParam eA)", "0.0")]
        [InlineData("(vlax-curve-getEndParam eA)", "1.5708")]
        [InlineData("(vlax-curve-getDistAtParam eA (vlax-curve-getEndParam eA))", "7.85398")]
        [InlineData("(vlax-curve-getArea eA)", "7.13495")]
        [InlineData("(vlax-curve-isClosed eA)", "nil")]
        public void AnArcOfAQuarterTurn_GivesAutoCadsNumbers(string form, string expected)
        {
            // AutoCAD 2021: every row as printed (a3)
            Assert.Equal(expected, V(Probe3(), form));
        }

        [Theory]
        [InlineData("(vlax-curve-getStartParam eP)", "0.0")]
        [InlineData("(vlax-curve-getEndParam eP)", "2.0")]
        [InlineData("(vlax-curve-getPointAtParam eP 1.5)", "(10.0 25.0 0.0)")]
        [InlineData("(vlax-curve-getDistAtParam eP 1.5)", "15.0")]
        [InlineData("(vlax-curve-getParamAtDist eP 15)", "1.5")]
        [InlineData("(vlax-curve-getDistAtPoint eP '(10 25 0))", "15.0")]
        [InlineData("(vlax-curve-getClosestPointTo eP '(12 25 0))", "(10.0 25.0 0.0)")]
        [InlineData("(vlax-curve-getClosestPointTo eP '(12 35 0) T)", "(10.0 30.0 0.0)")]
        [InlineData("(vlax-curve-getFirstDeriv eP 1.5)", "(0.0 10.0 0.0)")]
        [InlineData("(vlax-curve-getArea eP)", "50.0")]
        [InlineData("(vlax-curve-isClosed eP)", "nil")]
        [InlineData("(vlax-curve-isPeriodic eP)", "nil")]
        public void AnOpenPolyline_GivesAutoCadsNumbers(string form, string expected)
        {
            // AutoCAD 2021: every row as printed (a3)
            Assert.Equal(expected, V(Probe3(), form));
        }

        [Theory]
        [InlineData("(vlax-curve-getEndParam eQ)", "3.0")]
        [InlineData("(vlax-curve-getArea eQ)", "50.0")]
        [InlineData("(vlax-curve-isClosed eQ)", "T")]
        [InlineData("(vlax-curve-getDistAtParam eQ 3)", "34.1421")]
        [InlineData("(vlax-curve-getDistAtParam eQ 3.5)", "nil")]
        [InlineData("(vlax-curve-getPointAtParam eQ 5)", "nil")]
        [InlineData("(vlax-curve-getPointAtDist eQ 100)", "nil")]
        public void AClosedPolyline_GivesAutoCadsNumbers_AndNilPastItsEnd(string form, string expected)
        {
            // AutoCAD 2021: every row as printed (a3)
            Assert.Equal(expected, V(Probe3(), form));
        }

        [Theory]
        [InlineData("(vlax-curve-getPointAtDist eL -1)", "nil")]
        [InlineData("(vlax-curve-getPointAtParam eL 11)", "nil")]
        [InlineData("(vlax-curve-getParamAtPoint eL '(5 1 0))", "nil")]
        [InlineData("(vlax-curve-getDistAtPoint eL '(5 1 0))", "nil")]
        [InlineData("(vlax-curve-getClosestPointTo eL '(5 1 0))", "(5.0 0.0 0.0)")]
        [InlineData("(vlax-curve-getClosestPointTo eL '(15 1 0))", "(10.0 0.0 0.0)")]
        [InlineData("(vlax-curve-getClosestPointTo eL '(15 1 0) T)", "(15.0 0.0 0.0)")]
        [InlineData("(vlax-curve-getClosestPointToProjection eL '(5 1 7) '(0 0 1))", "(5.0 0.0 0.0)")]
        [InlineData("(vlax-curve-getArea eL)", "0.0")]
        [InlineData("(vlax-curve-isClosed eL)", "nil")]
        [InlineData("(vlax-curve-isPlanar eL)", "T")]
        [InlineData("(vlax-curve-getFirstDeriv eL 3)", "(10.0 0.0 0.0)")]
        public void ALine_GivesAutoCadsNumbers(string form, string expected)
        {
            // AutoCAD 2021: every row as printed (a3)
            Assert.Equal(expected, V(Probe3(), form));
        }

        [Theory]
        [InlineData("(vlax-curve-getStartParam eE)", "0.0")]
        [InlineData("(vlax-curve-getEndParam eE)", "6.28319")]
        [InlineData("(vlax-curve-getArea eE)", "47.1239")]
        [InlineData("(vlax-curve-getDistAtParam eE pi)", "12.7635")]
        public void AWholeEllipse_GivesAutoCadsNumbers(string form, string expected)
        {
            // AutoCAD 2021: every row as printed (a3)
            Assert.Equal(expected, V(Probe3(), form));
        }

        [Theory]
        [InlineData("(vlax-curve-getStartPoint eT)", "; error: bad argument value: AcDbCurve *")]
        [InlineData("(vlax-curve-getArea eT)", "; error: bad argument value: AcDbCurve *")]
        [InlineData("(vlax-curve-getStartPoint 5)", "; error: unable to get ObjectID: 5")]
        [InlineData("(vlax-curve-getStartPoint nil)", "; error: unable to get ObjectID: nil")]
        [InlineData("(vlax-curve-getStartPoint \"a\")", "; error: unable to get ObjectID: \"a\"")]
        [InlineData("(vlax-curve-getPointAtParam eL \"a\")", "; error: bad argument type: numberp: \"a\"")]
        [InlineData("(vlax-curve-getPointAtParam eL)", "; error: too few arguments")]
        [InlineData("(vlax-curve-getPointAtParam eL 5 6)", "; error: too many arguments")]
        [InlineData("(vlax-curve-getClosestPointTo eL 5)", "; error: bad argument type: 2D/3D point: 5")]
        [InlineData("(vlax-curve-getStartPoint)", "; error: too few arguments")]
        public void ARefusal_IsWordedAsAutoCadWordsIt(string form, string expected)
        {
            // AutoCAD 2021: a TEXT is no curve, a value that is no entity has no ObjectID, an argument of the wrong kind
            // is named (a3, a4)
            Assert.Equal(expected, V(Probe3(), form));
        }

        private static string Vertex(string handle, double x, double y, double z) =>
            "0\nVERTEX\n5\n"
            + handle
            + "\n8\n0\n100\nAcDbEntity\n100\nAcDbVertex\n100\nAcDb3dPolylineVertex\n10\n"
            + x.ToString(CultureInfo.InvariantCulture)
            + "\n20\n"
            + y.ToString(CultureInfo.InvariantCulture)
            + "\n30\n"
            + z.ToString(CultureInfo.InvariantCulture)
            + "\n70\n32\n";

        // The drawing of probe a4: a polyline with an arc, a fit spline, a 3D polyline, an elliptical arc, an XLINE and a RAY.
        internal static CadCommandSession Probe4()
        {
            // The presenter has no 3DPOLY command, so the 3D polyline is in the drawing from the start.
            string poly3d =
                "0\nPOLYLINE\n5\nB1\n8\n0\n100\nAcDbEntity\n100\nAcDb3dPolyline\n66\n1\n10\n0\n20\n0\n30\n0\n70\n8\n"
                + Vertex("B2", 0, 50, 0)
                + Vertex("B3", 10, 50, 5)
                + Vertex("B4", 10, 60, 5)
                + "0\nSEQEND\n5\nB5\n8\n0\n";
            CadCommandSession session = Session(
                CadDocument.From(TestDrawings.Drawing(null, null, poly3d), "probe4.dxf")
            );
            V(session, "(vl-load-com)");
            V(session, "(setq e3 (entnext))");
            Run(session, "PLINE", "0,0", "10,0", "A", "10,10", "L", "0,10", Enter);
            V(session, "(setq eB (entlast))");
            Run(session, "SPLINE", "0,30", "10,40", "20,30", "30,40", Enter);
            V(session, "(setq eS (entlast))");
            // The ELLIPSE command of the presenter takes the major axis from the centre to the second end point, where AutoCAD takes
            // it from the centre to the first (so that angle 0 is at 50,0 and the parameters are 0 to 1.5708, not pi to 3 pi / 2):
            // the ellipse is made with the groups AutoCAD holds for it.
            V(
                session,
                "(entmake '((0 . \"ELLIPSE\") (100 . \"AcDbEntity\") (100 . \"AcDbEllipse\") (10 55.0 0.0 0.0) (11 -5.0 0.0 0.0) (210 0.0 0.0 1.0) (40 . 0.6) (41 . 0.0) (42 . 1.5707963267948966)))"
            );
            V(session, "(setq eEA (entlast))");
            Run(session, "XLINE", "0,0", "10,10", Enter);
            V(session, "(setq eX (entlast))");
            Run(session, "RAY", "0,0", "10,0", Enter);
            V(session, "(setq eR (entlast))");
            return session;
        }

        [Theory]
        [InlineData("(vlax-curve-getEndParam eB)", "3.0")]
        [InlineData("(vlax-curve-getDistAtParam eB 1)", "10.0")]
        [InlineData("(vlax-curve-getDistAtParam eB 2)", "25.708")]
        [InlineData("(vlax-curve-getDistAtParam eB 1.5)", "17.854")]
        [InlineData("(vlax-curve-getPointAtParam eB 1.5)", "(15.0 5.0 0.0)")]
        [InlineData("(vlax-curve-getParamAtDist eB 12)", "1.12732")]
        [InlineData("(vlax-curve-getFirstDeriv eB 1.5)", "(0.0 5.0 0.0)")]
        [InlineData("(vlax-curve-getSecondDeriv eB 1.5)", "(-5.0 0.0 0.0)")]
        [InlineData("(vlax-curve-getArea eB)", "139.27")]
        [InlineData("(vlax-curve-getClosestPointTo eB '(20 5 0))", "(15.0 5.0 0.0)")]
        public void APolylineWithABulge_RunsLinearInAngleOnTheArc(string form, string expected)
        {
            // AutoCAD 2021: parameter 1.5 on the half circle is its mid-point (a4)
            Assert.Equal(expected, V(Probe4(), form));
        }

        [Theory]
        [InlineData("(vlax-curve-getStartParam eS)", "0.0")]
        [InlineData("(vlax-curve-getEndParam eS)", "42.4264")]
        [InlineData("(vlax-curve-getStartPoint eS)", "(0.0 30.0 0.0)")]
        [InlineData("(vlax-curve-getEndPoint eS)", "(30.0 40.0 0.0)")]
        [InlineData("(vlax-curve-getDistAtParam eS (vlax-curve-getEndParam eS))", "44.2385")]
        [InlineData("(vlax-curve-isClosed eS)", "nil")]
        [InlineData("(vlax-curve-isPlanar eS)", "T")]
        public void AFitSpline_RunsByChordLength(string form, string expected)
        {
            // AutoCAD 2021: the end parameter 42.4264 is the sum of the chords, the length of the curve 44.2385 (a4)
            Assert.Equal(expected, V(Probe4(), form));
        }

        [Theory]
        [InlineData("(vlax-curve-getEndParam e3)", "2.0")]
        [InlineData("(vlax-curve-getDistAtParam e3 1)", "11.1803")]
        [InlineData("(vlax-curve-getPointAtParam e3 0.5)", "(5.0 50.0 2.5)")]
        [InlineData("(vlax-curve-isPlanar e3)", "T")]
        [InlineData("(vlax-curve-getArea e3)", "55.9017")]
        public void A3DPolyline_GivesTheAreaOfItsTiltedPlane(string form, string expected)
        {
            // AutoCAD 2021: every row as printed (a4)
            Assert.Equal(expected, V(Probe4(), form));
        }

        [Theory]
        [InlineData("(vlax-curve-getStartParam eEA)", "0.0")]
        [InlineData("(vlax-curve-getEndParam eEA)", "1.5708")]
        [InlineData("(vlax-curve-getStartPoint eEA)", "(50.0 0.0 0.0)")]
        [InlineData("(vlax-curve-getEndPoint eEA)", "(55.0 -3.0 0.0)")]
        [InlineData("(vlax-curve-getDistAtParam eEA (vlax-curve-getEndParam eEA))", "6.38175")]
        [InlineData("(vlax-curve-getArea eEA)", "4.28097")]
        public void AnEllipticalArc_GivesAutoCadsNumbers(string form, string expected)
        {
            // AutoCAD 2021: every row as printed (a4)
            Assert.Equal(expected, V(Probe4(), form));
        }

        [Theory]
        [InlineData("(vlax-curve-getStartParam eX)", "nil")]
        [InlineData("(vlax-curve-getEndParam eX)", "nil")]
        [InlineData("(vlax-curve-getStartPoint eX)", "nil")]
        [InlineData("(vlax-curve-getPointAtParam eX 2)", "(1.41421 1.41421 0.0)")]
        [InlineData("(vlax-curve-getClosestPointTo eX '(10 0 0))", "(5.0 5.0 0.0)")]
        public void AnXline_HasNoStartAndNoEnd(string form, string expected)
        {
            // AutoCAD 2021: the parameter is the distance from the base point (a4)
            Assert.Equal(expected, V(Probe4(), form));
        }

        [Theory]
        [InlineData("(vlax-curve-getEndParam eR)", "nil")]
        [InlineData("(vlax-curve-getStartParam eR)", "0.0")]
        [InlineData("(vlax-curve-getStartPoint eR)", "(0.0 0.0 0.0)")]
        [InlineData("(vlax-curve-getPointAtParam eR 2)", "(2.0 0.0 0.0)")]
        public void ARay_StartsAtItsBaseAndHasNoEnd(string form, string expected)
        {
            // AutoCAD 2021: the end parameter is nil, the start point is the base (a4); the start parameter 0.0 (ours: D209)
            Assert.Equal(expected, V(Probe4(), form));
        }

        [Fact]
        public void ALineOfNoLength_EndsAtNought_AndAPointIsNoCurve()
        {
            CadCommandSession session = Session(Blank());
            V(session, "(vl-load-com)");
            Run(session, "LINE", "0,0", "0,0,5", Enter);

            // AutoCAD 2021: (vlax-curve-getEndParam (entlast)) 0.0 (a4)
            Assert.Equal("0.0", V(session, "(vlax-curve-getEndParam (entlast))"));

            Run(session, "POINT", "5,5");

            // AutoCAD 2021: a POINT is no curve, bad argument value: AcDbCurve 49 (a4)
            Assert.Equal(
                "; error: bad argument value: AcDbCurve *",
                V(session, "(vlax-curve-getStartPoint (entlast))")
            );
        }

        [Fact]
        public void ABlockReference_IsNoCurve()
        {
            CadCommandSession session = Session(Blank());
            V(session, "(vl-load-com)");
            Run(session, "CIRCLE", "100,100", "1");
            V(session, "(setq eC (entlast))");
            V(session, "(command \"-BLOCK\" \"GHB4\" \"100,100\" eC \"\")");
            V(session, "(command \"-INSERT\" \"GHB4\" \"200,0\" 1 1 0)");

            // AutoCAD 2021: bad argument value: AcDbCurve 50 (a4)
            Assert.Equal(
                "; error: bad argument value: AcDbCurve *",
                V(session, "(vlax-curve-getStartPoint (entlast))")
            );
        }

        [Fact]
        public void AnErasedLine_IsNoCurve_UntilEntdelBringsItBack()
        {
            CadCommandSession session = Session(Blank());
            V(session, "(vl-load-com)");
            Run(session, "LINE", "0,100", "10,100", Enter);
            V(session, "(setq eL (entlast))");
            V(session, "(entdel eL)");

            // AutoCAD 2021: erased, bad argument value: AcDbCurve 51 (a4)
            Assert.Equal("; error: bad argument value: AcDbCurve *", V(session, "(vlax-curve-getStartPoint eL)"));
            Assert.Equal("; error: bad argument value: AcDbCurve *", V(session, "(vlax-curve-getEndParam eL)"));

            // AutoCAD 2021: a second entdel brings it back and the old name works (a4)
            V(session, "(entdel eL)");
            Assert.Equal("10.0", V(session, "(vlax-curve-getEndParam eL)"));
            Assert.Equal("(2.5 100.0 0.0)", V(session, "(vlax-curve-getPointAtParam eL 2.5)"));
        }

        [Theory]
        [InlineData("(vlax-curve-getPointAtParam eL 10)", "(10.0 100.0 0.0)")]
        [InlineData("(vlax-curve-getPointAtParam eL 0)", "(0.0 100.0 0.0)")]
        [InlineData("(vlax-curve-getParamAtPoint eL '(5 100 0))", "5.0")]
        [InlineData("(vlax-curve-getParamAtPoint eL '(5 100.0001 0))", "nil")]
        [InlineData("(vlax-curve-getParamAtPoint eL '(5 100.01 0))", "nil")]
        [InlineData("(vlax-curve-getDistAtPoint eL '(5 100 7))", "nil")]
        [InlineData("(vlax-curve-getClosestPointTo eL '(5 105 3))", "(5.0 100.0 0.0)")]
        [InlineData("(vlax-curve-getPointAtDist eL 10)", "(10.0 100.0 0.0)")]
        [InlineData("(vlax-curve-getPointAtDist eL 10.0001)", "nil")]
        [InlineData("(vlax-curve-getParamAtDist eL 2)", "2.0")]
        [InlineData("(vlax-curve-getDistAtParam eL -0.5)", "-0.5")]
        [InlineData("(vlax-curve-getClosestPointTo eL '(1 2))", "(1.0 100.0 0.0)")]
        [InlineData("(vlax-curve-getPointAtParam eL 5)", "(5.0 100.0 0.0)")]
        public void ALine_OffTheCurveOrPastItsEnd_IsNil(string form, string expected)
        {
            CadCommandSession session = Session(Blank());
            V(session, "(vl-load-com)");
            Run(session, "LINE", "0,100", "10,100", Enter);
            V(session, "(setq eL (entlast))");

            // AutoCAD 2021: every row as printed; a point 0.0001 off the line is already nil, a 2D point takes Z 0 (a4)
            Assert.Equal(expected, V(session, form));
        }

        // The drawing of probe a5: arcs from 90 degrees and from 180, an elliptical arc, a rotated ellipse, a circle, a closed
        // polyline and an open one.
        internal static CadCommandSession Probe5()
        {
            CadCommandSession session = Session(Blank());
            V(session, "(vl-load-com)");
            Run(session, "ARC", "C", "0,0", "0,10", "-10,0");
            V(session, "(setq eA (entlast))");
            Run(session, "ARC", "C", "50,0", "40,0", "60,0");
            V(session, "(setq eA2 (entlast))");
            Run(session, "ELLIPSE", "A", "C", "0,50", "10,50", "4", "45", "135");
            V(session, "(setq eE (entlast))");
            Run(session, "ELLIPSE", "C", "0,100", "0,110", "4");
            V(session, "(setq eR (entlast))");
            Run(session, "CIRCLE", "0,200", "10");
            V(session, "(setq eC (entlast))");
            Run(session, "PLINE", "0,300", "10,300", "10,310", "C");
            V(session, "(setq eQ (entlast))");
            Run(session, "PLINE", "0,400", "10,400", Enter);
            V(session, "(setq eP (entlast))");
            return session;
        }

        [Theory]
        [InlineData("(vlax-curve-getStartParam eA)", "1.5708")]
        [InlineData("(vlax-curve-getEndParam eA)", "3.14159")]
        [InlineData("(vlax-curve-getPointAtParam eA 2.0)", "(-4.16147 9.09297 0.0)")]
        [InlineData("(vlax-curve-getDistAtParam eA 2.0)", "4.29204")]
        [InlineData("(vlax-curve-getParamAtDist eA 1.0)", "1.6708")]
        [InlineData("(vlax-curve-getFirstDeriv eA 2.0)", "(-9.09297 -4.16147 0.0)")]
        [InlineData("(vlax-curve-getSecondDeriv eA 2.0)", "(4.16147 -9.09297 0.0)")]
        [InlineData("(vlax-curve-getPointAtParam eA 0.5)", "nil")]
        [InlineData("(vlax-curve-getStartParam eA2)", "3.14159")]
        [InlineData("(vlax-curve-getEndParam eA2)", "6.28319")]
        [InlineData("(vlax-curve-getPointAtParam eA2 4.0)", "(43.4636 -7.56802 0.0)")]
        public void AnArc_RunsInAbsoluteAngles(string form, string expected)
        {
            // AutoCAD 2021: the parameters of an arc are its angles, not counted from its start (a5)
            Assert.Equal(expected, V(Probe5(), form));
        }

        [Theory]
        [InlineData("(vlax-curve-getStartParam eE)", "1.19029")]
        [InlineData("(vlax-curve-getEndParam eE)", "1.9513")]
        [InlineData("(vlax-curve-getStartPoint eE)", "(3.71391 53.7139 0.0)")]
        [InlineData("(vlax-curve-getEndPoint eE)", "(-3.71391 53.7139 0.0)")]
        [InlineData("(vlax-curve-getFirstDeriv eE (vlax-curve-getStartParam eE))", "(-9.28477 1.48556 0.0)")]
        [InlineData("(vlax-curve-getStartPoint eR)", "(0.0 110.0 0.0)")]
        [InlineData("(vlax-curve-getPointAtParam eR 1.0)", "(-3.36588 105.403 0.0)")]
        [InlineData("(vlax-curve-getDistAtParam eR 6.28319)", "46.0262")]
        public void AnEllipse_RunsByItsOwnParameter(string form, string expected)
        {
            // AutoCAD 2021: the parameters of the arc from 45 to 135 degrees are 1.19029 to 1.9513 (a5)
            Assert.Equal(expected, V(Probe5(), form));
        }

        [Theory]
        [InlineData("(vlax-curve-getPointAtParam eC 7.0)", "(7.53902 206.57 0.0)")]
        [InlineData("(vlax-curve-getDistAtParam eC 7.0)", "70.0")]
        [InlineData("(vlax-curve-getParamAtDist eC 70)", "0.716815")]
        public void ACircle_PastATurn_GoesRound(string form, string expected)
        {
            // AutoCAD 2021: a circle is periodic: parameter 7.0 is the point at angle 7.0, distance 70.0 (a5)
            Assert.Equal(expected, V(Probe5(), form));
        }

        [Theory]
        [InlineData("(vlax-curve-getEndParam eQ)", "3.0")]
        [InlineData("(vlax-curve-getEndPoint eQ)", "(0.0 300.0 0.0)")]
        [InlineData("(vlax-curve-getPointAtParam eQ 2.5)", "(5.0 305.0 0.0)")]
        [InlineData("(vlax-curve-getParamAtPoint eQ '(0 300 0))", "0.0")]
        [InlineData("(vlax-curve-getFirstDeriv eQ 2.5)", "(-10.0 -10.0 0.0)")]
        [InlineData("(vlax-curve-getFirstDeriv eQ 1.0)", "(0.0 10.0 0.0)")]
        [InlineData("(vlax-curve-getFirstDeriv eQ 3.0)", "(-10.0 -10.0 0.0)")]
        [InlineData("(vlax-curve-getFirstDeriv eP 1.0)", "(10.0 0.0 0.0)")]
        [InlineData("(vlax-curve-getSecondDeriv eP 0.5)", "(0.0 0.0 0.0)")]
        public void AClosedPolyline_AtItsVertices_TakesTheNextSegmentAndTheLastAtTheEnd(string form, string expected)
        {
            // AutoCAD 2021: every row as printed (a5)
            Assert.Equal(expected, V(Probe5(), form));
        }

        [Fact]
        public void TheUcs_DoesNotTouchTheCurvePoints()
        {
            CadCommandSession session = Probe5();
            Run(session, "UCS", "Z", "90");

            // AutoCAD 2021: a point is WCS in and out whatever the UCS (a5)
            Assert.Equal("(0.0 400.0 0.0)", V(session, "(vlax-curve-getStartPoint eP)"));
            Assert.Equal("(5.0 400.0 0.0)", V(session, "(vlax-curve-getClosestPointTo eP '(5 -400 0))"));
        }

        [Fact]
        public void AWidePolyline_HasNoAreaFromItsWidth()
        {
            CadCommandSession session = Session(Blank());
            V(session, "(vl-load-com)");
            Run(session, "PLINE", "0,500", "W", "1", "1", "10,500", Enter);

            // AutoCAD 2021: end parameter 1.0, area 0.0: a width does not count (a5)
            Assert.Equal("1.0", V(session, "(vlax-curve-getEndParam (entlast))"));
            Assert.Equal("0.0", V(session, "(vlax-curve-getArea (entlast))"));
        }

        [Theory]
        [InlineData("(vlax-curve-getDistAtParam eL 11)", "11.0")] // ours: D210, a line gives the parameter as it is
        [InlineData("(vlax-curve-getParamAtPoint eL '(5 100.0000000001 0))", "5.0")] // ours: D210, a thousand-millionth of 1 + the size is on the curve
        [InlineData("(vlax-curve-getPointAtParam eL 10.0000000001)", "(10.0 100.0 0.0)")] // ours: D210, a value that close to the end is the end
        [InlineData("(vlax-curve-getPointAtParam eL -0.0001)", "nil")] // ours: D210
        [InlineData("(vlax-curve-getParamAtDist eL -0.0001)", "nil")] // ours: D210
        public void ALine_TakesAValueAtItsEndWithinAThousandMillionthOfItsSize(string form, string expected)
        {
            CadCommandSession session = Session(Blank());
            V(session, "(vl-load-com)");
            Run(session, "LINE", "0,100", "10,100", Enter);
            V(session, "(setq eL (entlast))");

            Assert.Equal(expected, V(session, form));
        }

        [Theory]
        [InlineData("(vlax-curve-getPointAtDist eC 70)", "(7.53902 206.57 0.0)")] // ours: a distance past a turn goes round, as getParamAtDist does (a5)
        [InlineData("(vlax-curve-getPointAtParam eC -1)", "nil")] // ours: a parameter before the start is nil
        [InlineData("(vlax-curve-getParamAtDist eC -1)", "nil")] // ours
        [InlineData("(vlax-curve-getDistAtParam eC 13.0)", "130.0")] // ours: two turns and a bit, the distance is the radius times the parameter as at 7.0 (a5)
        public void ACircle_PastATurn_GoesRoundForEveryFunction(string form, string expected)
        {
            Assert.Equal(expected, V(Probe5(), form));
        }

        [Theory]
        [InlineData("(vlax-curve-getClosestPointToProjection eC '(30 240 5) '(0 0 1))", "(6.0 208.0 0.0)")] // ours: the point above (30 240) is nearest the point of the circle towards it
        [InlineData("(vlax-curve-getClosestPointToProjection eC '(30 200 5) '(0 0 1))", "(10.0 200.0 0.0)")] // ours: and the one above (30 200) the right of it
        [InlineData("(vlax-curve-getClosestPointToProjection eP '(5 450 9) '(0 0 1))", "(5.0 400.0 0.0)")] // ours
        [InlineData("(vlax-curve-getClosestPointToProjection eP '(5 450 9) '(0 0 0))", "(5.0 400.0 0.0)")] // ours: no normal is the plain closest point
        [InlineData("(vlax-curve-getClosestPointToProjection eP '(20 450 9) '(0 0 1))", "(10.0 400.0 0.0)")] // ours: past the end, kept on the curve
        [InlineData("(vlax-curve-getClosestPointToProjection eP '(20 450 9) '(0 0 1) T)", "(10.0 400.0 0.0)")] // ours: a polyline's ends are not extended here
        public void AClosestPointWithANormal_IsMeasuredInTheFlatView(string form, string expected)
        {
            // The circle of probe a5 is about (0 200) with radius 10, the open polyline runs from (0 400) to (10 400); a point 5 or
            // 9 above them is measured as if it were on the plane.
            Assert.Equal(expected, V(Probe5(), form));
        }

        [Fact]
        public void AClosestPointWithANormalAlongTheLine_IsTheOneBelowInTheFlatView()
        {
            CadCommandSession session = Session(Blank());
            V(session, "(vl-load-com)");
            Run(session, "LINE", "0,0", "10,0", Enter);
            V(session, "(setq eL (entlast))");

            // ours: seen along the Y axis the line is the stretch of X from 0 to 10 at Z 0, and the point (4 7 3) is 3 above X 4
            Assert.Equal("(4.0 0.0 0.0)", V(session, "(vlax-curve-getClosestPointToProjection eL '(4 7 3) '(0 1 0))"));

            // ours: seen along the line itself it is a point, and every point of it is as near: the start
            Assert.Equal("(0.0 0.0 0.0)", V(session, "(vlax-curve-getClosestPointToProjection eL '(4 7 3) '(1 0 0))"));

            // ours: extend lets the foot run past the end
            Assert.Equal(
                "(14.0 0.0 0.0)",
                V(session, "(vlax-curve-getClosestPointToProjection eL '(14 7 3) '(0 1 0) T)")
            );
            Assert.Equal(
                "(10.0 0.0 0.0)",
                V(session, "(vlax-curve-getClosestPointToProjection eL '(14 7 3) '(0 1 0))")
            );
        }

        [Fact]
        public void EveryFunctionOnEveryOtherCurve_AnswersWithoutAnInternalError()
        {
            CadCommandSession session = Probe4();
            string[] curves = { "eB", "eS", "e3", "eEA", "eX", "eR" };
            string[] numbers = { "0", "1", "-1", "1.5", "pi", "7", "100", "1000000000.0" };
            string[] points = { "'(0 0 0)", "'(5 5)", "'(1000 -1000 5)", "'(10 10 10)" };
            string[] byNumber =
            {
                "PointAtParam",
                "DistAtParam",
                "ParamAtDist",
                "PointAtDist",
                "FirstDeriv",
                "SecondDeriv",
            };
            var failures = new System.Text.StringBuilder();

            foreach (string curve in curves)
            {
                var forms = new System.Collections.Generic.List<string>
                {
                    "(vlax-curve-getStartParam " + curve + ")",
                    "(vlax-curve-getEndParam " + curve + ")",
                    "(vlax-curve-getStartPoint " + curve + ")",
                    "(vlax-curve-getEndPoint " + curve + ")",
                    "(vlax-curve-getArea " + curve + ")",
                    "(vlax-curve-isClosed " + curve + ")",
                    "(vlax-curve-isPeriodic " + curve + ")",
                    "(vlax-curve-isPlanar " + curve + ")",
                };

                foreach (string number in numbers)
                {
                    foreach (string name in byNumber)
                    {
                        forms.Add("(vlax-curve-get" + name + " " + curve + " " + number + ")");
                    }
                }

                foreach (string point in points)
                {
                    forms.Add("(vlax-curve-getParamAtPoint " + curve + " " + point + ")");
                    forms.Add("(vlax-curve-getDistAtPoint " + curve + " " + point + ")");
                    forms.Add("(vlax-curve-getClosestPointTo " + curve + " " + point + ")");
                    forms.Add("(vlax-curve-getClosestPointTo " + curve + " " + point + " T)");
                    forms.Add("(vlax-curve-getClosestPointToProjection " + curve + " " + point + " '(0 0 1))");
                    forms.Add("(vlax-curve-getClosestPointToProjection " + curve + " " + point + " '(1 1 1) T)");
                }

                foreach (string form in forms)
                {
                    string result = V(session, form);

                    if (result.Contains("internal error"))
                    {
                        failures.AppendLine(form + " => " + result);
                    }
                }
            }

            Assert.Equal(string.Empty, failures.ToString());
        }

        [Fact]
        public void ALineOfNoLength_AnswersEveryFunctionWithoutAnInternalError()
        {
            CadCommandSession session = Session(Blank());
            V(session, "(vl-load-com)");
            Run(session, "LINE", "3,4", "3,4", Enter);
            V(session, "(setq eZ (entlast))");
            var failures = new System.Text.StringBuilder();

            foreach (
                string form in new[]
                {
                    "(vlax-curve-getStartParam eZ)",
                    "(vlax-curve-getEndPoint eZ)",
                    "(vlax-curve-getArea eZ)",
                    "(vlax-curve-isClosed eZ)",
                    "(vlax-curve-getPointAtParam eZ 0)",
                    "(vlax-curve-getDistAtParam eZ 0)",
                    "(vlax-curve-getParamAtDist eZ 0)",
                    "(vlax-curve-getPointAtDist eZ 0)",
                    "(vlax-curve-getFirstDeriv eZ 0)",
                    "(vlax-curve-getSecondDeriv eZ 0)",
                    "(vlax-curve-getParamAtPoint eZ '(3 4 0))",
                    "(vlax-curve-getDistAtPoint eZ '(3 4 0))",
                    "(vlax-curve-getClosestPointTo eZ '(0 0 0))",
                    "(vlax-curve-getClosestPointToProjection eZ '(0 0 0) '(0 0 1))",
                }
            )
            {
                string result = V(session, form);

                if (result.Contains("internal error"))
                {
                    failures.AppendLine(form + " => " + result);
                }
            }

            Assert.Equal(string.Empty, failures.ToString());
        }

        [Fact]
        public void ARefusalInsideVlCatchAllApply_IsTheMessageOfTheError()
        {
            CadCommandSession session = Probe3();

            Assert.Equal(
                "\"bad argument value: AcDbCurve *\"",
                V(session, "(vl-catch-all-error-message (vl-catch-all-apply 'vlax-curve-getStartPoint (list eT)))")
            );
            Assert.Equal(
                "\"bad argument type: numberp: \\\"a\\\"\"",
                V(
                    session,
                    "(vl-catch-all-error-message (vl-catch-all-apply 'vlax-curve-getPointAtParam (list eL \"a\")))"
                )
            );
        }

        [Fact]
        public void AnOpenSpline_ClosesItsAreaByTheChord()
        {
            CadCommandSession session = Session(Blank());
            V(session, "(vl-load-com)");
            Run(session, "SPLINE", "0,0", "10,10", "20,0", Enter);
            V(session, "(setq eS (entlast))");

            // ours: AutoCAD was not asked for a spline's area; it is that between the curve and the chord from its start to its end,
            // which a shoelace over 1000 points of the curve, made here from vlax-curve-getPointAtParam, meets within 0.01
            string check =
                "(progn (setq n 1000 i 0 a 0.0 e (vlax-curve-getEndParam eS) p0 (vlax-curve-getStartPoint eS))"
                + " (while (< i n) (setq p (vlax-curve-getPointAtParam eS (* i (/ e n)))"
                + " q (vlax-curve-getPointAtParam eS (* (1+ i) (/ e n)))"
                + " a (+ a (- (* (car p) (cadr q)) (* (car q) (cadr p)))) i (1+ i)))"
                + " (setq a (+ a (- (* (car q) (cadr p0)) (* (car p0) (cadr q)))))"
                + " (list (< (abs (- (abs (/ a 2.0)) (vlax-curve-getArea eS))) 0.01) (< 100.0 (vlax-curve-getArea eS) 140.0)))";
            Assert.Equal("(T T)", V(session, check));
        }

        [Fact]
        public void TheNameOfAnEntselPick_IsACurveToo()
        {
            CadCommandSession session = Session(Blank());
            V(session, "(vl-load-com)");
            Run(session, "LINE", "0,0", "10,0", Enter);

            // AutoCAD 2021: (vlax-curve-getStartPoint (car (entsel "\nPick: "))) with the pick at 10,0 gives (0.0 0.0 0.0) (a3)
            int before = session.History.Count;
            Run(session, "(vlax-curve-getStartPoint (car (entsel \"\nPick: \")))", "10,0");

            Assert.Equal("(0.0 0.0 0.0)", session.History[session.History.Count - 1].TrimEnd());
            Assert.True(session.History.Count > before);
        }
    }
}
