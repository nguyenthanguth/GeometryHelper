using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>Join of a spline with an arc, a polyline and a spline, the knots and control points AutoCAD 2021's JOIN and SPLINEDIT Join wrote.</summary>
    public class SplineEditJoinKnotsTests
    {
        private static CadCommandSession Start(CadDocument document, params object[] inputs)
        {
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            return Run(session, inputs);
        }

        private static GeoPoint3 P3(double x, double y) => new GeoPoint3(x, y, 0);

        private static DxfSpline Probe(CadDocument document)
        {
            var spline = new DxfSpline();
            document.Database.ModelSpace.AppendEntity(spline);
            spline.FitData = new DxfFitData(new[] { P3(0, 0), P3(10, 10), P3(20, 0), P3(30, 10), P3(40, 0) }, 3, 0.0);
            return spline;
        }

        private static void Matches(DxfSpline spline, double[] x, double[] y, double[] knots)
        {
            DxfNurbsData data = spline.NurbsData;
            IReadOnlyList<GeoPoint3> points = data.GetControlPoints();
            Assert.Equal(x.Length, points.Count);

            for (int i = 0; i < x.Length; i++)
            {
                Assert.Equal(x[i], points[i].X, 5);
                Assert.Equal(y[i], points[i].Y, 5);
            }

            IReadOnlyList<double> have = data.GetKnots();
            Assert.Equal(knots.Length, have.Count);

            for (int i = 0; i < knots.Length; i++)
            {
                Assert.Equal(knots[i], have[i], 5);
            }
        }

        [Fact]
        public void Join_AnArcOfThreePoints_RunsItsKnotsOnByItsAngle()
        {
            CadDocument document = Blank();
            DxfSpline spline = Probe(document);
            double start = Math.Atan2(7.5, 10);
            var arc = new DxfArc(P3(50, -7.5), 12.5, start, Math.PI - start);
            document.Database.ModelSpace.AppendEntity(arc);
            CadCommandSession session = Start(
                document,
                "SPLINEDIT",
                new[] { spline.Handle },
                "J",
                new[] { arc.Handle },
                Enter
            );

            Assert.Contains("1 object joined to source", session.History);
            Matches(
                spline,
                new[] { 0.0, 3.333333333333333, 10.0, 20.0, 30.0, 36.66666666666666, 40.0, 42.40536184886392, 45.99106358522679, 49.99999999999999, 54.0089364147732, 57.59463815113607, 60.0 },
                new[] { 0.0, 5.714285714285712, 17.14285714285714, -8.571428571428569, 17.14285714285714, 5.714285714285713, 0.0, 3.207149131818563, 5.0, 5.0, 5.0, 3.207149131818564, 0.0 },
                new[] { 0.0, 0.0, 0.0, 0.0, 14.14213562373095, 28.2842712474619, 42.42640687119285, 56.5685424949238, 56.5685424949238, 56.5685424949238, 57.49583771292542, 57.49583771292542, 57.49583771292542, 58.42313293092702, 58.42313293092702, 58.42313293092702, 58.42313293092702 }
            );
        }

        [Fact]
        public void Join_APolylineOfTwoSegments_RunsItsKnotsOnOneForEachSegment()
        {
            CadDocument document = Blank();
            DxfSpline spline = Probe(document);
            var polyline = new DxfPolyline(3);
            polyline.AddVertexAt(0, new GeoPoint2(40, 0), 0, 0, 0);
            polyline.AddVertexAt(1, new GeoPoint2(50, 10), 0, 0, 0);
            polyline.AddVertexAt(2, new GeoPoint2(60, 0), 0, 0, 0);
            document.Database.ModelSpace.AppendEntity(polyline);
            CadCommandSession session = Start(
                document,
                "SPLINEDIT",
                new[] { spline.Handle },
                "J",
                new[] { polyline.Handle },
                Enter
            );

            Assert.Contains("1 object joined to source", session.History);
            Matches(
                spline,
                new[] { 0.0, 3.333333333333333, 10.0, 20.0, 30.0, 36.66666666666666, 40.0, 43.33333333333334, 46.66666666666667, 50.00000000000001, 53.33333333333334, 56.66666666666667, 60.00000000000001 },
                new[] { 0.0, 5.714285714285712, 17.14285714285714, -8.571428571428569, 17.14285714285714, 5.714285714285713, 0.0, 3.333333333333333, 6.666666666666666, 10.0, 6.666666666666666, 3.333333333333332, 0.0 },
                new[] { 0.0, 0.0, 0.0, 0.0, 14.14213562373095, 28.2842712474619, 42.42640687119285, 56.5685424949238, 56.5685424949238, 56.5685424949238, 57.56854249492381, 57.56854249492381, 57.56854249492381, 58.5685424949238, 58.5685424949238, 58.5685424949238, 58.5685424949238 }
            );
        }

        [Fact]
        public void Join_ASpline_RunsItsOwnKnotsOnFromTheEnd()
        {
            CadDocument document = Blank();
            DxfSpline spline = Probe(document);
            var other = new DxfSpline();
            document.Database.ModelSpace.AppendEntity(other);
            other.FitData = new DxfFitData(new[] { P3(40, 0), P3(50, 10), P3(60, 0) }, 3, 0.0);
            CadCommandSession session = Start(
                document,
                "SPLINEDIT",
                new[] { spline.Handle },
                "J",
                new[] { other.Handle },
                Enter
            );

            Assert.Contains("1 object joined to source", session.History);
            Matches(
                spline,
                new[] { 0.0, 3.333333333333333, 10.0, 20.0, 30.0, 36.66666666666666, 40.0, 43.33333333333333, 50.0, 56.66666666666667, 60.0 },
                new[] { 0.0, 5.714285714285712, 17.14285714285714, -8.571428571428569, 17.14285714285714, 5.714285714285713, 0.0, 5.0, 15.0, 5.0, 0.0 },
                new[] { 0.0, 0.0, 0.0, 0.0, 14.14213562373095, 28.2842712474619, 42.42640687119285, 56.5685424949238, 56.5685424949238, 56.5685424949238, 70.71067811865475, 84.85281374238571, 84.85281374238571, 84.85281374238571, 84.85281374238571 }
            );
        }
    }
}
