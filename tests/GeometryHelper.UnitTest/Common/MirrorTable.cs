using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Common
{
    /// <summary>
    /// A table of questions asked from both sides: for each member and each case, the new side's answer
    /// beside the answer of the side it mirrors, with no tolerance and then under each tolerance given.
    /// </summary>
    /// <remarks>
    /// Each row is named <c>"Member: case"</c> and run on its own, so a theory over <see cref="Names"/> shows
    /// every member and case as a test of its own. The rows are kept in the order they were added, and
    /// looked up by name, so nothing follows the order of a dictionary.
    /// </remarks>
    internal sealed class MirrorTable
    {
        private readonly Tolerance[] tolerances;

        private readonly List<string> names = new List<string>();

        private readonly Dictionary<string, Action> checks = new Dictionary<string, Action>(StringComparer.Ordinal);

        private readonly List<string> members = new List<string>();

        private readonly Dictionary<string, List<Func<string>>> answers = new Dictionary<string, List<Func<string>>>(StringComparer.Ordinal);

        /// <summary>
        /// Starts a table whose rows are each checked without a tolerance and then under every one of these.
        /// </summary>
        internal MirrorTable(params Tolerance[] tolerances)
        {
            this.tolerances = tolerances;
        }

        /// <summary>
        /// Adds one row per case: <paramref name="mine"/> must give exactly what <paramref name="theirs"/>
        /// gives, and <paramref name="mineWithin"/> exactly what <paramref name="theirsWithin"/> gives.
        /// </summary>
        /// <param name="member">The new member, as <c>GeoLine3.CollidesWith(GeoEdge3)</c>.</param>
        /// <param name="cases">The named pairs the member is asked about.</param>
        /// <param name="mine">The new member, without a tolerance.</param>
        /// <param name="mineWithin">The new member, within a tolerance.</param>
        /// <param name="theirs">The answer it mirrors, without a tolerance, already turned round where it is a line.</param>
        /// <param name="theirsWithin">The answer it mirrors, within a tolerance.</param>
        /// <param name="same">Asserts that two answers are the same, bit for bit.</param>
        internal void Add<TA, TB, TR>(
            string member,
            IEnumerable<(string Case, TA Asked, TB Argument)> cases,
            Func<TA, TB, TR> mine,
            Func<TA, TB, Tolerance, TR> mineWithin,
            Func<TA, TB, TR> theirs,
            Func<TA, TB, Tolerance, TR> theirsWithin,
            Action<TR, TR> same)
        {
            members.Add(member);
            answers.Add(member, new List<Func<string>>());

            foreach ((string Case, TA Asked, TB Argument) c in cases)
            {
                string name = member + ": " + c.Case;

                names.Add(name);
                checks.Add(name, () =>
                {
                    Agree(() => theirs(c.Asked, c.Argument), () => mine(c.Asked, c.Argument), same);

                    foreach (Tolerance tolerance in tolerances)
                    {
                        Agree(() => theirsWithin(c.Asked, c.Argument, tolerance), () => mineWithin(c.Asked, c.Argument, tolerance), same);
                    }
                });

                answers[member].Add(() => string.Join(" | ", tolerances.Select(t => Answer(() => theirsWithin(c.Asked, c.Argument, t)))));
            }
        }

        /// <summary>Gets the rows by name, in the order they were added.</summary>
        internal TheoryData<string> Names()
        {
            var data = new TheoryData<string>();

            foreach (string name in names)
            {
                data.Add(name);
            }

            return data;
        }

        /// <summary>Gets the members, in the order they were added.</summary>
        internal TheoryData<string> Members()
        {
            var data = new TheoryData<string>();

            foreach (string member in members)
            {
                data.Add(member);
            }

            return data;
        }

        /// <summary>Runs one row.</summary>
        internal void Check(string name) => checks[name]();

        /// <summary>
        /// Gets how many different answers the side being mirrored gives over the cases of one member, read
        /// under every tolerance at once, so that a member answering the same whatever it is asked is caught.
        /// </summary>
        internal int DistinctAnswers(string member) => answers[member].Select(answer => answer()).Distinct(StringComparer.Ordinal).Count();

        /// <summary>
        /// Asserts the new side answers as the other side does; where the other side refuses the pair with an
        /// argument exception, the new side has to refuse it with the same exception and message.
        /// </summary>
        private static void Agree<TR>(Func<TR> theirs, Func<TR> mine, Action<TR, TR> same)
        {
            TR expected;

            try
            {
                expected = theirs();
            }
            catch (ArgumentException refused)
            {
                Assert.Equal(refused.Message, Assert.Throws(refused.GetType(), () => mine()).Message);
                return;
            }

            same(expected, mine());
        }

        private static string Answer<TR>(Func<TR> theirs)
        {
            try
            {
                return Key(theirs());
            }
            catch (ArgumentException refused)
            {
                return "refused: " + refused.GetType().Name;
            }
        }

        /// <summary>Asserts two points are the same, bit for bit.</summary>
        internal static void Same(GeoPoint2 expected, GeoPoint2 actual)
        {
            Assert.Equal(expected.X, actual.X);
            Assert.Equal(expected.Y, actual.Y);
        }

        /// <summary>Asserts two points are the same, bit for bit.</summary>
        internal static void Same(GeoPoint3 expected, GeoPoint3 actual)
        {
            Assert.Equal(expected.X, actual.X);
            Assert.Equal(expected.Y, actual.Y);
            Assert.Equal(expected.Z, actual.Z);
        }

        /// <summary>Asserts two lists of points are the same points in the same order.</summary>
        internal static void Same(GeoPoint2[] expected, GeoPoint2[] actual)
        {
            Assert.Equal(expected == null, actual == null);

            if (expected != null)
            {
                Assert.Equal(expected.Length, actual.Length);

                for (int i = 0; i < expected.Length; i++)
                {
                    Same(expected[i], actual[i]);
                }
            }
        }

        /// <summary>Asserts two lists of points are the same points in the same order.</summary>
        internal static void Same(GeoPoint3[] expected, GeoPoint3[] actual)
        {
            Assert.Equal(expected == null, actual == null);

            if (expected != null)
            {
                Assert.Equal(expected.Length, actual.Length);

                for (int i = 0; i < expected.Length; i++)
                {
                    Same(expected[i], actual[i]);
                }
            }
        }

        /// <summary>Asserts two answers of a TryIntersectWith are the same bool and the same points in order.</summary>
        internal static void Same((bool Hit, GeoPoint2[] Points) expected, (bool Hit, GeoPoint2[] Points) actual)
        {
            Assert.Equal(expected.Hit, actual.Hit);
            Same(expected.Points, actual.Points);
        }

        /// <summary>Asserts two answers of a TryIntersectWith are the same bool and the same points in order.</summary>
        internal static void Same((bool Hit, GeoPoint3[] Points) expected, (bool Hit, GeoPoint3[] Points) actual)
        {
            Assert.Equal(expected.Hit, actual.Hit);
            Same(expected.Points, actual.Points);
        }

        /// <summary>Asserts two segments run between the same points in the same direction.</summary>
        internal static void Same(GeoLine2 expected, GeoLine2 actual)
        {
            Same(expected.StartPoint, actual.StartPoint);
            Same(expected.EndPoint, actual.EndPoint);
        }

        /// <summary>Asserts two segments run between the same points in the same direction.</summary>
        internal static void Same(GeoLine3 expected, GeoLine3 actual)
        {
            Same(expected.StartPoint, actual.StartPoint);
            Same(expected.EndPoint, actual.EndPoint);
        }

        /// <summary>Asserts two numbers are the same, bit for bit.</summary>
        internal static void Same(double expected, double actual) => Assert.Equal(expected, actual);

        /// <summary>Asserts two answers are the same.</summary>
        internal static void Same(bool expected, bool actual) => Assert.Equal(expected, actual);

        private static string Key(object answer)
        {
            switch (answer)
            {
                case null:
                    return "null";
                case double d:
                    return d.ToString("R", CultureInfo.InvariantCulture);
                case GeoPoint2 p:
                    return Key(p.X) + "," + Key(p.Y);
                case GeoPoint3 p:
                    return Key(p.X) + "," + Key(p.Y) + "," + Key(p.Z);
                case GeoLine2 l:
                    return Key(l.StartPoint) + ">" + Key(l.EndPoint);
                case GeoLine3 l:
                    return Key(l.StartPoint) + ">" + Key(l.EndPoint);
                case ValueTuple<bool, GeoPoint2[]> t:
                    return Key(t.Item1) + Key(t.Item2);
                case ValueTuple<bool, GeoPoint3[]> t:
                    return Key(t.Item1) + Key(t.Item2);
                case Array a:
                    return "[" + string.Join(";", a.Cast<object>().Select(Key)) + "]";
                default:
                    return Convert.ToString(answer, CultureInfo.InvariantCulture);
            }
        }
    }
}
