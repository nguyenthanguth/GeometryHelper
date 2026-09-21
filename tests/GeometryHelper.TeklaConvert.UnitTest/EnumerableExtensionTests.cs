using System;
using System.Collections.Generic;
using GeometryHelper.TeklaConvert;
using TSG = Tekla.Structures.Geometry3d;
using Xunit;

namespace GeometryHelper.TeklaConvert.UnitTest
{
    public class EnumerableExtensionTests
    {
        private static List<TSG.Point> OnX(params double[] xs)
        {
            var points = new List<TSG.Point>();
            foreach (double x in xs)
            {
                points.Add(new TSG.Point(x, 0.0, 0.0));
            }

            return points;
        }

        private static TSG.LineSegment Segment(double sx, double sy, double sz, double ex, double ey, double ez)
        {
            return new TSG.LineSegment(new TSG.Point(sx, sy, sz), new TSG.Point(ex, ey, ez));
        }
    }
}
