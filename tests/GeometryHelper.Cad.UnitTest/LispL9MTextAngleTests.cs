using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// An MTEXT's rotation through <c>entmake</c>, <c>entmakex</c> and <c>entmod</c> (I6 of L9): group 50 is in radians, group 11
    /// is the direction the text runs, and of the two the one that comes later in the list wins and the other is derived from
    /// it. The expected values are those of AutoCAD 2021's accoreconsole, rows m1 to m12 of <c>scratch/l9/m/console.md</c>
    /// (feed <c>mtext.feed</c>), marked <c>AutoCAD 2021 (l9 M1-c mN)</c>.
    /// </summary>
    public sealed class LispL9MTextAngleTests : IDisposable
    {
        // Ends a routine a test left waiting, so no worker thread outlives its test.
        public void Dispose() => WaitingRoutines.EndAll();

        private const string Head =
            "(0 . \"MTEXT\") (100 . \"AcDbEntity\") (100 . \"AcDbMText\") (10 0.0 0.0 0.0) (1 . \"A\")";

        private static CadCommandSession Start()
        {
            CadCommandSession session = Session(Blank());
            session.Tolerance = Tolerance.Default;
            return session;
        }

        private static string Value(CadCommandSession session, string form)
        {
            int before = session.History.Count;
            session.Feed(form);
            return string.Join("\n", session.History.Skip(before + 1).Select(l => l.TrimEnd()));
        }

        // Makes the MTEXT from the groups after the head; the last entity of the drawing is the text.
        private static void Make(CadCommandSession session, string function, string groups) =>
            Value(session, "(" + function + " '(" + Head + " " + groups + "))");

        // Group 11's x and y and group 50 of the last entity, as the form (list x y a) prints them.
        private static double[] Turn(CadCommandSession session)
        {
            string text = Value(
                session,
                "(list (nth 1 (assoc 11 (entget (entlast)))) (nth 2 (assoc 11 (entget (entlast)))) (cdr (assoc 50 (entget (entlast)))))"
            );
            return Regex
                .Matches(text, @"-?\d+(\.\d+)?")
                .Cast<Match>()
                .Select(m => double.Parse(m.Value, CultureInfo.InvariantCulture))
                .ToArray();
        }

        private static void AssertTurn(double[] turn, double x, double y, double angle)
        {
            Assert.Equal(3, turn.Length);
            Assert.Equal(x, turn[0], 4);
            Assert.Equal(y, turn[1], 4);
            Assert.Equal(angle, turn[2], 4);
        }

        // entmod of the entity's own list with one group replaced (the list of entget has 11 before 50, as AutoCAD's).
        private static void Replace(CadCommandSession session, int code, string group)
        {
            Value(session, "(setq e (entget (entlast)))");
            Value(session, "(entmod (subst '" + group + " (assoc " + code + " e) e))");
        }

        [Theory]
        [InlineData("entmake", 0.5)]
        [InlineData("entmakex", 0.5)]
        [InlineData("entmake", 1.0)]
        public void AnMTextWithOnlyAGroup50_TurnsByThatManyRadians(string function, double radians)
        {
            CadCommandSession session = Start();

            // AutoCAD 2021 (l9 M1-c m1, m6): group 50 0.5 and no 11 reads back as 11 (0.877583 0.479426 0) and 50 0.5, that is
            // 28.65 degrees; ours (pin) turned it half a degree. 1.0 radian is ours, from the same rule (m7 gives 0.540302
            // 0.841471 for it).
            Make(session, function, "(50 . " + radians.ToString("0.0###", CultureInfo.InvariantCulture) + ")");

            AssertTurn(Turn(session), Math.Cos(radians), Math.Sin(radians), radians);
        }

        [Fact]
        public void AnMTextWithOnlyAGroup11_ReadsBackItsAngleAsGroup50InRadians()
        {
            CadCommandSession session = Start();

            // AutoCAD 2021 (l9 M1-c m12): 11 (0 1 0) and no 50 reads back as 11 (0 1 0) and 50 1.5708. Green on the pin.
            Make(session, "entmake", "(11 0.0 1.0 0.0)");

            AssertTurn(Turn(session), 0.0, 1.0, Math.PI / 2);
        }

        [Fact]
        public void AGroup50ListedAfterGroup11_WinsOverIt()
        {
            CadCommandSession session = Start();

            // AutoCAD 2021 (l9 M1-c m2): 11 (0 1 0) then 50 0.5 gives 11 (0.877583 0.479426 0) and 50 0.5; ours (pin) kept 11.
            Make(session, "entmake", "(11 0.0 1.0 0.0) (50 . 0.5)");

            AssertTurn(Turn(session), Math.Cos(0.5), Math.Sin(0.5), 0.5);
        }

        [Fact]
        public void AGroup11ListedAfterGroup50_WinsOverIt_AndGroup50FollowsIt()
        {
            CadCommandSession session = Start();

            // AutoCAD 2021 (l9 M1-c m8): 50 0.5 then 11 (0 1 0) gives 11 (0 1 0) and 50 1.5708; ours (pin) kept 50 at 0.5.
            Make(session, "entmake", "(50 . 0.5) (11 0.0 1.0 0.0)");

            AssertTurn(Turn(session), 0.0, 1.0, Math.PI / 2);
        }

        [Fact]
        public void AnEntmodThatChangesOnlyGroup50OfAnEntgetList_TurnsTheText()
        {
            CadCommandSession session = Start();
            Make(session, "entmake", "(11 1.0 0.0 0.0)");

            // AutoCAD 2021 (l9 M1-c m3): the list of entget holds 11 (1 0 0) and then 50 0.0; with 50 replaced by 1.0 the text
            // turns, 11 (0.540302 0.841471 0) and 50 1.0; ours (pin) left 11 (1 0 0). The first text is a -MTEXT in the row; the
            // entmade one holds the same groups.
            Replace(session, 50, "(50 . 1.0)");

            AssertTurn(Turn(session), Math.Cos(1.0), Math.Sin(1.0), 1.0);
        }

        [Fact]
        public void AnEntmodThatChangesOnlyGroup11OfAnEntgetList_LeavesTheTextWhereGroup50PutsIt()
        {
            CadCommandSession session = Start();
            Make(session, "entmake", "(50 . 1.0)");

            // AutoCAD 2021 (l9 M1-c m4): 50 comes after 11 in the list, so a new 11 (0 1 0) is ignored: 11 stays (0.540302
            // 0.841471 0) and 50 stays 1.0; ours (pin) took the 11.
            Replace(session, 11, "(11 0.0 1.0 0.0)");

            AssertTurn(Turn(session), Math.Cos(1.0), Math.Sin(1.0), 1.0);
        }

        [Fact]
        public void AnEntmodThatChangesGroup11OfAnUnturnedText_LeavesItUnturned()
        {
            CadCommandSession session = Start();
            Make(session, "entmake", "(11 1.0 0.0 0.0)");

            // AutoCAD 2021 (l9 M1-c m9): a text with 50 0.0, entmod with 11 (0 1 0) and the 50 0.0 still in the list: 11 stays
            // (1 0 0), 50 stays 0.0; ours (pin) turned it a quarter turn.
            Replace(session, 11, "(11 0.0 1.0 0.0)");

            AssertTurn(Turn(session), 1.0, 0.0, 0.0);
        }

        [Fact]
        public void AnEntmodWithBothGroupsChanged_FollowsGroup50()
        {
            CadCommandSession session = Start();
            Make(session, "entmake", "(11 1.0 0.0 0.0)");
            Value(session, "(setq e (entget (entlast)))");

            // AutoCAD 2021 (l9 M1-c m5): 11 (-1 0 0) and 50 0.25 together give 11 (0.968912 0.247404 0) and 50 0.25; ours (pin)
            // kept 11 (-1 0 0).
            Value(session, "(entmod (subst '(11 -1.0 0.0 0.0) (assoc 11 e) (subst '(50 . 0.25) (assoc 50 e) e)))");

            AssertTurn(Turn(session), Math.Cos(0.25), Math.Sin(0.25), 0.25);
        }

        [Fact]
        public void AnEntmodWithGroup50TakenOutOfTheList_FollowsGroup11()
        {
            CadCommandSession session = Start();
            Make(session, "entmake", "(11 1.0 0.0 0.0)");
            Value(session, "(setq e (entget (entlast)))");

            // AutoCAD 2021 (l9 M1-c m10): the list with 50 removed and 11 (0 1 0): 11 (0 1 0) and 50 1.5708; ours (pin) gave 50 0.0.
            Value(session, "(entmod (subst '(11 0.0 1.0 0.0) (assoc 11 e) (vl-remove (assoc 50 e) e)))");

            AssertTurn(Turn(session), 0.0, 1.0, Math.PI / 2);
        }

        [Fact]
        public void AnEntmodWithGroup11TakenOutOfTheList_FollowsGroup50()
        {
            CadCommandSession session = Start();
            Make(session, "entmake", "(11 1.0 0.0 0.0)");
            Value(session, "(setq e (entget (entlast)))");

            // AutoCAD 2021 (l9 M1-c m11): the list with 11 removed and 50 0.7: 11 (0.764842 0.644218 0) and 50 0.7; ours (pin)
            // left 11 where it was.
            Value(session, "(entmod (subst '(50 . 0.7) (assoc 50 e) (vl-remove (assoc 11 e) e)))");

            AssertTurn(Turn(session), Math.Cos(0.7), Math.Sin(0.7), 0.7);
        }

        [Fact]
        public void AnEntmodListWithOnlyGroup50_TurnsTheText()
        {
            CadCommandSession session = Start();
            Make(session, "entmake", "(50 . 0.5)");
            Value(
                session,
                "(entmod (list (cons -1 (entlast)) '(0 . \"MTEXT\") '(100 . \"AcDbEntity\") '(100 . \"AcDbMText\") '(10 0.0 0.0 0.0) '(1 . \"A\") '(50 . 1.0)))"
            );

            // AutoCAD 2021 (l9 M1-c m7): a list with 50 1.0 and no 11 gives 11 (0.540302 0.841471 0) and 50 1.0; ours (pin) gave
            // 11 (0.999848 0.017452 0), a degree.
            AssertTurn(Turn(session), Math.Cos(1.0), Math.Sin(1.0), 1.0);
        }

        [Fact]
        public void AnMTextMadeWithAGroup50_IsReadBackByItsRotationInRadians()
        {
            CadCommandSession session = Start();
            Make(session, "entmake", "(50 . 0.5)");

            // ours: the ActiveX Rotation of the text is the 0.5 radians the list gave, as the 11 it was given says (the DXF layer
            // reads a 50 with no 11 in degrees)
            Value(session, "(vl-load-com)");
            Assert.Equal(
                "T",
                Value(session, "(< (abs (- (vla-get-Rotation (vlax-ename->vla-object (entlast))) 0.5)) 1E-6)")
            );
        }

        [Fact]
        public void AnMTextOnTheNormalMinusZ_TurnsItsAngleInThePlaneOfItsNormal()
        {
            CadCommandSession session = Start();

            // ours: the x axis of the plane of the normal (0, 0, -1) is (-1, 0, 0) and its y axis (0, 1, 0), so 0.5 radians is
            // the world direction (-cos 0.5, sin 0.5, 0)
            Make(session, "entmake", "(210 0.0 0.0 -1.0) (50 . 0.5)");

            AssertTurn(Turn(session), -Math.Cos(0.5), Math.Sin(0.5), 0.5);
        }
    }
}
