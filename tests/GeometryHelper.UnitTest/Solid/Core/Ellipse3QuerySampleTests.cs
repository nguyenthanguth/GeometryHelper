using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;
using Xunit.Sdk;
using static GeometryHelper.UnitTest.Solid.Ellipse3Oracle;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Decision 20, the deterministic sample of the fuzz gate: for each pair type twelve random cases, drawn from a fixed
    /// seed by <see cref="Ellipse3Oracle.MakeCase"/>, the scratch harness drawing thousands the same way. Each case is
    /// built so its answer is known: the other shape is made through rim points of the ellipse, or, in one plane, the
    /// crossings come from a dense sample of the rim refined by bisection. Frames lie any way, centres within 1 000 of
    /// the origin, radii from 1 to 1 000 and as thin as a thousandth; planes lean 5 to 90 degrees, or a thousandth of a
    /// degree to one, or lie 1E-4 apart; and every case keeps twenty times the tolerance clear of every band. An answer
    /// built exactly must come within 1E-9 of the size, a graze within 1E-4, and one read off planes nearly one or 1E-4
    /// apart within the point tolerance: the circle's own error there reaches 5.8E-4 (measured on 81e409e, a circle of
    /// 963 about a centre 1 340 out, planes 0.0018 degrees apart). With equal radii the ellipse,
    /// made by FromCircle and with its axis turned, must give what the circle gives, in order, to 1E-12 of the size
    /// (decision 6), except where planes are nearly one or the curves graze, where the ellipse is held to the answer only.
    /// </summary>
    public class Ellipse3QuerySampleTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        private const int PerKind = 12;

        public static IEnumerable<object[]> Kinds() => QueryKinds.Select(kind => new object[] { kind });

        /// <summary>A seed from the kind's name, the same on every run and every machine.</summary>
        private static int Seed(string kind) => kind.Aggregate(17, (h, c) => unchecked(h * 31 + c));

        private static void Check(QueryCase c, GeoEllipse3 ellipse, string which)
        {
            try
            {
                GeoPoint3[] found = c.Cross(ellipse, Tol);
                if (c.Ordered)
                {
                    SamePointsInOrder(c.Expected, found, c.Within);
                }
                else
                {
                    SamePoints(c.Expected, found, c.Within);
                }

                Assert.Equal(c.Collides, c.Collide(ellipse, Tol));
                Assert.Equal(found.Length > 0, c.Try(ellipse, Tol, out GeoPoint3[] tried));
                Assert.Equal(found.Select(p => (p.X, p.Y, p.Z)), tried.Select(p => (p.X, p.Y, p.Z)));
            }
            catch (XunitException failure)
            {
                throw new XunitException($"{which}, {c}: {failure.Message}");
            }
        }

        [Theory]
        [MemberData(nameof(Kinds))]
        public void ARandomCaseIsAnsweredAsItWasBuilt(string kind)
        {
            var random = new Random(Seed(kind));
            for (int i = 0; i < PerKind; i++)
            {
                QueryCase c = MakeCase(kind, random, false);
                Check(c, c.Ellipse.Build(), $"case {i}");
            }
        }

        [Theory]
        [MemberData(nameof(Kinds))]
        public void ARandomCaseOfACircleIsAnsweredAsTheCircleAnswersIt(string kind)
        {
            var random = new Random(Seed(kind) + 1);
            for (int i = 0; i < PerKind; i++)
            {
                QueryCase c = MakeCase(kind, random, true);
                GeoCircle3 circle = AsCircle(c.Ellipse);
                double agree = 1E-12 * (c.Ellipse.Center.DistanceTo(GeoPoint3.Origin) + c.Ellipse.A);
                GeoEllipse3 turned = new GeoEllipse3(circle.Center, circle.Normal, c.Ellipse.U.Multiply(Math.Cos(0.4)).Add(c.Ellipse.V.Multiply(Math.Sin(0.4))), c.Ellipse.A, c.Ellipse.A);

                foreach (KeyValuePair<string, GeoEllipse3> ellipse in new Dictionary<string, GeoEllipse3> { ["from the circle"] = GeoEllipse3.FromCircle(circle), ["turned"] = turned })
                {
                    string which = $"case {i}, {ellipse.Key}";
                    Check(c, ellipse.Value, which);

                    // Where planes are nearly one or the curves graze, the circle is no oracle: it misplaces crossings by
                    // more than the tolerance there, and loses some (see the report on 81e409e), so only the answer built
                    // in, checked above, holds the ellipse.
                    if (c.IllConditioned)
                    {
                        continue;
                    }

                    GeoPoint3[] byCircle = c.CircleCross(Tol);
                    GeoPoint3[] byEllipse = c.Cross(ellipse.Value, Tol);
                    try
                    {
                        Assert.Equal(c.CircleCollide(Tol), c.Collide(ellipse.Value, Tol));
                        SamePointsInOrder(byCircle, byEllipse, agree);
                    }
                    catch (XunitException failure)
                    {
                        throw new XunitException($"{which}, against the circle, {c}: {failure.Message}");
                    }
                }
            }
        }
    }
}
