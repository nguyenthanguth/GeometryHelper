using System;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The SPLINE as a <see cref="DxfSpline"/>: read as ezdxf 1.4.5 reads it, walked by its knot value, its control points,
    /// knots and weights set and read back to the bit, made in code, and moved as ezdxf moves one.
    /// </summary>
    public class DxfSplineTests
    {
        [Fact]
        public void TheSplinesOfEzdxf_AreReadAsEzdxfReadsThem_ToTheBit()
        {
            // As ezdxf 1.4.5 reads them: 70, a cubic of 5 control points, the second (1.6029485114965314, 7.618807207894353,
            // 0.2013363482471295) (0x1.9a5ad56a0caaep+0, 0x1.e79a898c1bdc4p+2, 0x1.9c563b39bd6a7p-3), its inner knot
            // 0.4920371418403775 (0x1.f7d895a27ca80p-2); 6F, given by its 4 fit points alone.
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            DxfSpline spline = ByHandle<DxfSpline>(db, "70");
            DxfSpline fit = ByHandle<DxfSpline>(db, "6F");

            Assert.Equal(3, spline.Degree);
            Assert.Equal(5, spline.NumControlPoints);
            Assert.Equal(Bits(new GeoPoint3(FromBits(0x3ff9a5ad56a0caae), FromBits(0x401e79a898c1bdc4), FromBits(0x3fc9c563b39bd6a7))), Bits(spline.GetControlPointAt(1)));
            Assert.Equal(new[] { 0.0, 0.0, 0.0, 0.0, FromBits(0x3fdf7d895a27ca80), 1.0, 1.0, 1.0, 1.0 }, spline.Knots.ToArray());
            Assert.Empty(spline.Weights);
            Assert.Equal(1.0, spline.WeightAt(3));
            Assert.False(spline.IsRational);
            Assert.False(spline.Closed);
            Assert.False(spline.IsPeriodic);
            Assert.False(spline.HasFitData);
            Assert.Equal(DxfSplineType.ControlPoints, spline.Type);
            Assert.Equal(0.0, spline.StartParam);
            Assert.Equal(1.0, spline.EndParam);
            Assert.Equal(Bits(new GeoPoint3(0, 0, 0)), Bits(spline.StartPoint));
            Assert.True(spline.EndPoint.IsEqualTo(new GeoPoint3(12, 0, 1), Tolerance.Default));

            Assert.Equal(4, fit.NumFitPoints);
            Assert.Equal(0, fit.NumControlPoints);
            Assert.True(fit.HasFitData);
            Assert.Equal(DxfSplineType.FitPoints, fit.Type);
            Assert.Equal(Bits(new GeoPoint3(7, -2, 0)), Bits(fit.GetFitPointAt(2)));
            Assert.True(fit.EndPoint.IsEqualTo(new GeoPoint3(10, 4, 0), Tolerance.Default));
            Assert.Equal(Bits(new GeoVector3(0, 0, 0)), Bits(fit.StartFitTangent));
        }

        [Fact]
        public void ASplinesShape_IsTheReaders_AndItIsWalkedByItsKnots()
        {
            DxfSpline spline = ByHandle<DxfSpline>(DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000), "70");
            DxfDocument read = DxfReader.Parse(DxfFileEzdxfDrawings.R2000, new DxfReadOptions { Tolerance = Tolerance.Default });
            var shape = (GeoSpline3)Assert.Single(spline.GetGeometry());

            Assert.Equal(read.Shapes.Single(s => s.Handle == "70").Shape, shape);
            Assert.True(spline.GetPointAtParameter(0.3).IsEqualTo(shape.GetPointAtParameter(0.3), Tolerance.Default));
            double half = spline.GetDistanceAtParameter(0.6);
            Assert.Equal(0.6, spline.GetParameterAtDistance(half), 9);
            Assert.True(spline.GetPointAtDist(half).IsEqualTo(spline.GetPointAtParameter(0.6), Tolerance.Default));
            Assert.Equal(0.6, spline.GetParameterAtPoint(spline.GetPointAtParameter(0.6)), 9);

            // A spline of degree 1 from (0, 0, 0) to (3, 4, 0) is the segment: half way along at its parameter 0.5.
            var line = new DxfSpline(1, false, false, false, new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(3, 4, 0) }, new[] { 0.0, 0.0, 1.0, 1.0 }, null, 0.0, 0.0);
            Assert.Equal(2.5, line.GetDistanceAtParameter(0.5), 14);
            Assert.True(line.GetFirstDerivative(0.5).IsEqualTo(new GeoVector3(3, 4, 0), Tolerance.Default));
            Assert.Equal(Bits(new GeoVector3(0, 0, 0)), Bits(line.GetSecondDerivative(0.5)));
        }

        [Fact]
        public void EverySetterGivenTheValueItGives_LeavesTheFileByteForByte()
        {
            foreach (string text in new[] { DxfFileEzdxfDrawings.R2000, DxfFileEzdxfDrawings.R2018, DxfReaderRoundTripTests.OneOfEach().ToString() })
            {
                SettingWhatItHasChangesNothing<DxfSpline>(text, spline =>
                {
                    spline.Degree = spline.Degree;
                    spline.ControlPoints = spline.ControlPoints;
                    spline.Knots = spline.Knots;
                    spline.FitTolerance = spline.FitTolerance;

                    if (spline.IsRational)
                    {
                        spline.Weights = spline.Weights;
                    }

                    for (int i = 0; i < spline.NumControlPoints; i++)
                    {
                        spline.SetControlPointAt(i, spline.GetControlPointAt(i));
                        spline.SetWeightAt(i, spline.WeightAt(i));
                    }

                    spline.Layer = spline.Layer;
                    spline.TransformBy(GeoTransform3.Identity);
                });
            }
        }

        [Fact]
        public void AControlPointAndAWeightSet_AreThatRecordWrittenAnew_ReadBackToTheBit()
        {
            string text = DxfFileEzdxfDrawings.R2000;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfSpline spline = ByHandle<DxfSpline>(db, "70");

            spline.SetControlPointAt(2, new GeoPoint3(6.5, 5.5, -1.5));
            spline.SetWeightAt(1, 2.5);

            string saved = Encoding.UTF8.GetString(Saved(db));
            OnlyTheseChanged(text, saved, "70");
            DxfSpline again = ByHandle<DxfSpline>(DxfDatabase.Parse(saved), "70");
            Assert.Equal(Bits(new GeoPoint3(6.5, 5.5, -1.5)), Bits(again.GetControlPointAt(2)));
            Assert.Equal(Bits(spline.GetControlPointAt(1)), Bits(again.GetControlPointAt(1)));
            Assert.True(again.IsRational);
            Assert.Equal(new[] { 1.0, 2.5, 1.0, 1.0, 1.0 }, again.Weights.ToArray());
        }

        [Fact]
        public void AKnotInserted_KeepsTheCurve()
        {
            DxfSpline spline = ByHandle<DxfSpline>(DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018), "70");
            GeoPoint3 before = spline.GetPointAtParameter(0.3);

            spline.InsertKnot(0.25);

            Assert.Equal(6, spline.NumControlPoints);
            Assert.Equal(10, spline.Knots.Count);
            Assert.True(spline.GetPointAtParameter(0.3).IsEqualTo(before, Tolerance.Default));
            Assert.Throws<ArgumentOutOfRangeException>(() => spline.InsertKnot(1.5));
        }

        [Fact]
        public void ADegreeSet_GivesTheSamePointsAClampedUniformKnotVector_SoTheCountsStillAgree()
        {
            // The cubic 70 of 5 points over 0 to 1 made of degree 4: 10 knots, 0 five times and 1 five times.
            string text = DxfFileEzdxfDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfSpline spline = ByHandle<DxfSpline>(db, "70");

            spline.Degree = 4;

            string saved = db.ToText();
            OnlyTheseChanged(text, saved, "70");
            DxfSpline again = ByHandle<DxfSpline>(DxfDatabase.Parse(saved), "70");
            Assert.Equal(4, again.Degree);
            Assert.Equal(5, again.NumControlPoints);
            Assert.Equal(new[] { 0.0, 0.0, 0.0, 0.0, 0.0, 1.0, 1.0, 1.0, 1.0, 1.0 }, again.Knots.ToArray());
            Assert.IsType<GeoSpline3>(Assert.Single(again.GetGeometry()));
            Assert.Throws<ArgumentException>(() => again.Degree = 5);

            spline.Degree = 2;
            Assert.Equal(new[] { 0.0, 0.0, 0.0, 1.0 / 3, 2.0 / 3, 1.0, 1.0, 1.0 }, spline.Knots.ToArray());
        }

        [Fact]
        public void KnotsPointsOrWeightsOfAnotherCount_AreRefused_TheRecordKept()
        {
            string text = DxfFileEzdxfDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfSpline spline = ByHandle<DxfSpline>(db, "70");

            Assert.Throws<ArgumentException>(() => spline.Knots = spline.Knots.Skip(1).ToList());
            Assert.Throws<ArgumentException>(() => spline.Knots = spline.Knots.Reverse().ToList());
            Assert.Throws<ArgumentException>(() => spline.ControlPoints = spline.ControlPoints.Take(4).ToList());
            Assert.Throws<ArgumentException>(() => spline.Weights = new[] { 1.0, 2.0 });

            Assert.Equal(new UTF8Encoding(false).GetBytes(text), Saved(db));
            Assert.Single(spline.GetGeometry());
        }

        [Fact]
        public void SettersGivenTheValuesTheyGive_ChangeNothing_EvenOnASplineWhoseCountsDisagree()
        {
            // Two splines as broken files have them: degree 3 with 3 knots for 4 control points, and degree 3 with 2 knots for 2
            // control points and 3 weights. Setting what they give writes nothing; setting anything else is still refused.
            string text = string.Join("\n", new[]
            {
                "  0", "SECTION", "  2", "ENTITIES",
                "  0", "SPLINE", "  5", "2A", "100", "AcDbEntity", "  8", "0", "100", "AcDbSpline",
                " 70", "4", " 71", "3", " 72", "3", " 73", "4", " 74", "0",
                " 40", "0.0", " 40", "0.5", " 40", "1.0",
                " 41", "1.0", " 41", "2.0", " 41", "1.0", " 41", "1.0",
                " 10", "0.0", " 20", "0.0", " 30", "0.0", " 10", "1.0", " 20", "2.0", " 30", "0.0",
                " 10", "3.0", " 20", "2.0", " 30", "0.0", " 10", "4.0", " 20", "0.0", " 30", "0.0",
                "  0", "SPLINE", "  5", "2B", "100", "AcDbEntity", "  8", "0", "100", "AcDbSpline",
                " 70", "4", " 71", "3", " 72", "2", " 73", "2", " 74", "0",
                " 40", "0.0", " 40", "1.0",
                " 41", "1.0", " 41", "1.0", " 41", "1.0",
                " 10", "0.0", " 20", "0.0", " 30", "0.0", " 10", "5.0", " 20", "0.0", " 30", "0.0",
                "  0", "ENDSEC", "  0", "EOF", string.Empty,
            });

            SettingWhatItHasChangesNothing<DxfSpline>(text, spline =>
            {
                spline.Knots = spline.Knots;
                spline.Weights = spline.Weights;
                spline.ControlPoints = spline.ControlPoints;
                spline.Degree = spline.Degree;
                spline.FitTolerance = spline.FitTolerance;

                for (int i = 0; i < spline.NumControlPoints; i++)
                {
                    spline.SetControlPointAt(i, spline.GetControlPointAt(i));
                }
            });

            DxfSpline broken = ByHandle<DxfSpline>(DxfDatabase.Parse(text), "2A");
            Assert.Throws<ArgumentException>(() => broken.Knots = new[] { 0.0, 0.25, 1.0 });
            Assert.Throws<ArgumentException>(() => broken.Weights = new[] { 1.0, 3.0, 1.0, 1.0 });
        }

        [Fact]
        public void NurbsDataSetAtOnce_ReplacesTheDataAndTheFitPoints_CheckedTogether()
        {
            // The spline 6F, given by its 4 fit points alone, given the cubic of 4 control points from (0, 0, 0) to (4, 0, 0).
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfSpline spline = ByHandle<DxfSpline>(db, "6F");
            var points = new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(1, 2, 0), new GeoPoint3(3, 2, 0), new GeoPoint3(4, 0, 0) };

            Assert.Throws<ArgumentException>(() => spline.SetNurbsData(3, false, false, false, points, new[] { 0.0, 0, 0, 1, 1, 1, 1 }, null));
            Assert.Throws<ArgumentException>(() => spline.SetNurbsData(3, true, false, false, points, new[] { 0.0, 0, 0, 0, 1, 1, 1, 1 }, new[] { 1.0, 1.0 }));
            Assert.Throws<ArgumentException>(() => spline.SetNurbsData(4, false, false, false, points, new[] { 0.0, 0, 0, 0, 0, 1, 1, 1, 1 }, null));
            Assert.Equal(4, spline.NumFitPoints);

            spline.SetNurbsData(3, true, false, false, points, new[] { 0.0, 0, 0, 0, 1, 1, 1, 1 }, new[] { 1.0, 0.5, 2.0, 1.0 });

            DxfSpline again = ByHandle<DxfSpline>(DxfDatabase.Parse(db.ToText()), "6F");
            Assert.Equal(0, again.NumFitPoints);
            Assert.Equal(DxfSplineType.ControlPoints, again.Type);
            Assert.Equal(4, again.NumControlPoints);
            Assert.True(again.IsRational);
            Assert.Equal(new[] { 1.0, 0.5, 2.0, 1.0 }, again.Weights.ToArray());
            Assert.Equal(Bits(new GeoPoint3(4, 0, 0)), Bits(again.EndPoint));
            Assert.IsType<GeoSpline3>(Assert.Single(again.GetGeometry()));
        }

        [Fact]
        public void ASplineReversed_RunsBackOverTheSameCurve()
        {
            DxfSpline spline = ByHandle<DxfSpline>(DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018), "70");
            GeoPoint3 at = spline.GetPointAtParameter(0.3);

            spline.ReverseCurve();

            Assert.Equal(Bits(new GeoPoint3(12, 0, 1)), Bits(spline.GetControlPointAt(0)));
            Assert.Equal(1.0 - FromBits(0x3fdf7d895a27ca80), spline.Knots[4]);
            Assert.True(spline.GetPointAtParameter(0.7).IsEqualTo(at, Tolerance.Default));
        }

        [Fact]
        public void ASplineMadeInCode_IsAppendedFromR2000On_AndRefusedByR12()
        {
            foreach (string text in new[] { DxfFileEzdxfDrawings.R12, DxfFileEzdxfDrawings.R2000, DxfFileEzdxfDrawings.R2018 })
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                var spline = new DxfSpline(3, true, false, false, new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(1, 2, 0), new GeoPoint3(3, 2, 0), new GeoPoint3(4, 0, 0) }, new[] { 0.0, 0, 0, 0, 1, 1, 1, 1 }, new[] { 1.0, 0.5, 2.0, 1.0 }, 1E-10, 1E-10) { Layer = "rebar" };

                if (text == DxfFileEzdxfDrawings.R12)
                {
                    Assert.Throws<ArgumentException>(() => db.ModelSpace.AppendEntity(spline));
                    continue;
                }

                db.ModelSpace.AppendEntity(spline);
                DxfSpline again = ByHandle<DxfSpline>(DxfDatabase.Parse(db.ToText()), spline.Handle);
                Assert.Equal(4, again.NumControlPoints);
                Assert.Equal(new[] { 1.0, 0.5, 2.0, 1.0 }, again.Weights.ToArray());
                Assert.True(again.IsRational);
                Assert.Equal(Bits(new GeoPoint3(4, 0, 0)), Bits(again.GetControlPointAt(3)));
                Assert.IsType<GeoSpline3>(Assert.Single(again.GetGeometry()));
            }
        }

        [Fact]
        public void ASplineTurned_IsWhereEzdxfPutsIt()
        {
            // ezdxf 1.4.5's transform() of 70: its control points (10, -5, 2.5), (7.653057708339265, 2.206512871368604,
            // 4.292829506983384), (19.6869059325053, -10.168129255390173, 3.160223294023397), (13.899012124006417,
            // 5.3250928812218685, -0.4051591316809251), (19.774409885058102, 1.5300142690344236, -0.11147947437564953).
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            ByHandle<DxfSpline>(db, "70").TransformBy(DxfPolylineTests.Turn());

            DxfSpline again = ByHandle<DxfSpline>(DxfDatabase.Parse(db.ToText()), "70");
            DxfPolylineTests.Near(new GeoPoint3(10, -5, 2.5), again.GetControlPointAt(0));
            DxfPolylineTests.Near(new GeoPoint3(7.653057708339265, 2.206512871368604, 4.292829506983384), again.GetControlPointAt(1));
            DxfPolylineTests.Near(new GeoPoint3(19.6869059325053, -10.168129255390173, 3.160223294023397), again.GetControlPointAt(2));
            DxfPolylineTests.Near(new GeoPoint3(13.899012124006417, 5.3250928812218685, -0.4051591316809251), again.GetControlPointAt(3));
            DxfPolylineTests.Near(new GeoPoint3(19.774409885058102, 1.5300142690344236, -0.11147947437564953), again.GetControlPointAt(4));
            Assert.Equal(new[] { 0.0, 0.0, 0.0, 0.0, FromBits(0x3fdf7d895a27ca80), 1.0, 1.0, 1.0, 1.0 }, again.Knots.ToArray());
        }
    }
}
