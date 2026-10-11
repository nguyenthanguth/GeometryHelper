using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Geometry;
using GeometryHelper.UnitTest.Common;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// The plane's twin of the solid guard of the same name: the measuring questions every shape in the plane
    /// answers, read by reflection, so that a question one shape answers about another and the other cannot
    /// answer back shows up here, named, rather than in a caller's compile error.
    /// </summary>
    public class ApiSymmetryTests
    {
        [Fact]
        public void EveryShapeInThePlaneAnswersEachQueryAnotherShapeAnswersAboutIt()
        {
            // DistanceTo, CollidesWith, GetIntersections, TryIntersectWith and GetShortestLineTo, read from
            // either side: where A answers one about B, B answers it about A. The only gaps allowed are an
            // ellipse as the argument and the point's three it answers with IsPointOn and Contains.
            string[] missing = QuerySymmetry.OneSidedPairs('2');

            if (missing.Length > 0)
            {
                Assert.Fail(missing.Length + " one-sided pairs in the plane, the member missing first:\n" + string.Join("\n", missing));
            }
        }

        [Fact]
        public void EveryShapeInThePlaneGivesTheShortestLineToAPointWithAndWithoutATolerance()
        {
            // Each shape gives the line leaving it and landing on a point, in both forms, as the arc and the
            // face do; and the point gives each of them the other way round, but for the ellipse, which is
            // asked from the ellipse.
            var missing = new List<string>();

            foreach (Type shape in QuerySymmetry.ShapeTypes('2'))
            {
                if (shape == typeof(GeoPoint2))
                {
                    continue;
                }

                Require(missing, shape, typeof(GeoPoint2));
                Require(missing, shape, typeof(GeoPoint2), typeof(Tolerance));

                if (shape != typeof(GeoEllipse2))
                {
                    Require(missing, typeof(GeoPoint2), shape);
                    Require(missing, typeof(GeoPoint2), shape, typeof(Tolerance));
                }
            }

            if (missing.Count > 0)
            {
                Assert.Fail(missing.Count + " shortest lines to a point missing:\n" + string.Join("\n", missing));
            }
        }

        private static void Require(List<string> missing, Type asked, params Type[] arguments)
        {
            if (asked.GetMethod("GetShortestLineTo", arguments)?.ReturnType != typeof(GeoLine2))
            {
                missing.Add(asked.Name + ".GetShortestLineTo(" + string.Join(", ", Array.ConvertAll(arguments, t => t.Name)) + ")");
            }
        }
    }
}
