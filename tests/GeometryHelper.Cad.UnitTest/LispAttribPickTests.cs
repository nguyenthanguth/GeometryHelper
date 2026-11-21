using System;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// <c>nentselp</c> and <c>nentsel</c> on the text of an attribute, and <c>entdel</c> of one, as AutoCAD 2021 gave them in its
    /// own window (acad.exe, probes pick2 to pick4 of the L5 part X2). A pick on an attribute's text gives the ATTRIB itself and
    /// the point, with no matrix and no list of references after them; <c>entsel</c> still gives the block reference; an
    /// invisible attribute cannot be picked; <c>entdel</c> of an attribute gives nil and leaves it where it is.
    /// </summary>
    public sealed class LispAttribPickTests : IDisposable
    {
        // Ends the routine a test left waiting, so no worker thread outlives its test.
        public void Dispose() => WaitingRoutines.EndAll();

        // The drawing of the probes: a block GHB of an attribute definition TAG1 (height 5 at the origin) and a line from (20,5) to
        // (30,5); a reference of it at (100,0) whose attribute reads +20 (names i1 and a1); a reference at (200,0) at scale 2 and
        // turned 90 degrees, whose attribute reads ROT, 10 high (i2 and a2); a reference at (300,0) whose attribute reads HID and
        // is invisible (i3 and a3).
        private static CadCommandSession Probe()
        {
            string block = TestDrawings.Block(
                "GHB",
                TestDrawings.Entity(
                    "ATTDEF",
                    "0",
                    256,
                    null,
                    "10",
                    0.0,
                    "20",
                    0.0,
                    "30",
                    0.0,
                    "40",
                    5.0,
                    "1",
                    "D",
                    "3",
                    "P",
                    "2",
                    "TAG1",
                    "70",
                    0
                ),
                TestDrawings.Entity(
                    "LINE",
                    "0",
                    256,
                    "B0",
                    "10",
                    20.0,
                    "20",
                    5.0,
                    "30",
                    0.0,
                    "11",
                    30.0,
                    "21",
                    5.0,
                    "31",
                    0.0
                )
            );
            string entities =
                Reference("B1", 100.0, 1.0, 0.0)
                + Attribute("B2", 100.0, 5.0, 0.0, "+20", 0)
                + "0\nSEQEND\n5\nB3\n8\n0\n"
                + Reference("B4", 200.0, 2.0, 90.0)
                + Attribute("B5", 200.0, 10.0, 90.0, "ROT", 0)
                + "0\nSEQEND\n5\nB6\n8\n0\n"
                + Reference("B7", 300.0, 1.0, 0.0)
                + Attribute("B8", 300.0, 5.0, 0.0, "HID", 1)
                + "0\nSEQEND\n5\nB9\n8\n0\n";
            CadCommandSession session = Session(
                CadDocument.From(TestDrawings.Drawing(null, block, entities), "attribpick.dxf")
            );
            Value(session, "(setq i1 (handent \"B1\") a1 (entnext i1))");
            Value(session, "(setq i2 (handent \"B4\") a2 (entnext i2))");
            Value(session, "(setq i3 (handent \"B7\") a3 (entnext i3))");
            return session;
        }

        private static string Reference(string handle, double x, double scale, double angle) =>
            "0\nINSERT\n5\n"
            + handle
            + "\n8\n0\n66\n1\n2\nGHB\n10\n"
            + x.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + "\n20\n0\n30\n0\n41\n"
            + scale.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + "\n42\n"
            + scale.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + "\n43\n"
            + scale.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + "\n50\n"
            + angle.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + "\n";

        private static string Attribute(string handle, double x, double height, double angle, string text, int flags) =>
            TestDrawings.Entity(
                "ATTRIB",
                "0",
                256,
                handle,
                "10",
                x,
                "20",
                0.0,
                "30",
                0.0,
                "40",
                height,
                "1",
                text,
                "50",
                angle,
                "2",
                "TAG1",
                "70",
                flags
            );

        // What a form gives, as the lines after it: entity names written *.
        private static string Value(CadCommandSession session, string form)
        {
            int before = session.History.Count;
            session.Feed(form);
            string text = string.Join("\n", session.History.Skip(before + 1).Select(l => l.TrimEnd()));
            return Regex.Replace(text, @"<Entity name: [0-9A-Fa-f]+>", "<Entity name: *>");
        }

        [Fact]
        public void NentselpOnTheTextOfAnAttribute_GivesTheAttributeAndThePointAndNothingMore()
        {
            CadCommandSession session = Probe();

            // AutoCAD 2021 (GUI, pick2): (nentselp '(103 2.5 0)) is (<Entity name: ...> (103.0 2.5 0.0)), the ATTRIB, two items
            Assert.Equal("(<Entity name: *> (103.0 2.5 0.0))", Value(session, "(nentselp '(103 2.5 0))"));
            Assert.Equal("2", Value(session, "(length (nentselp '(103 2.5)))"));
            Assert.Equal("T", Value(session, "(equal (car (nentselp '(103 2.5))) a1)"));
            Assert.Equal("\"ATTRIB\"", Value(session, "(cdr (assoc 0 (entget (car (nentselp '(103 2.5))))))"));
            Assert.Equal("\"+20\"", Value(session, "(cdr (assoc 1 (entget (car (nentselp '(103 2.5))))))"));
        }

        [Fact]
        public void NentselpOnTheTextOfAnAttributeOfAScaledAndTurnedReference_GivesTheAttributeToo()
        {
            CadCommandSession session = Probe();

            // AutoCAD 2021 (GUI, pick4): at 2 times and turned 90, the text ROT reaches x 190 to 200 and y 0 to 27; a pick at 195,10 gives
            // (<Entity name: ...> (195.0 10.0 0.0)), the ATTRIB of that reference, two items
            Assert.Equal("(<Entity name: *> (195.0 10.0 0.0))", Value(session, "(nentselp '(195 10 0))"));
            Assert.Equal("T", Value(session, "(equal (car (nentselp '(195 10))) a2)"));
        }

        [Fact]
        public void NentselTypedOnTheTextOfAnAttribute_GivesTheSameListAsNentselp()
        {
            CadCommandSession session = Probe();

            // AutoCAD 2021 (GUI, pick2): (nentsel "N: ") answered 103,2.5 gives (<Entity name: ...> (103.0 2.5 0.0)), the ATTRIB
            Value(session, "(setq n (nentselp '(103 2.5)))");
            Value(session, "(setq m (nentsel \"N: \"))");
            Run(session, "103,2.5");

            Assert.Equal("T", Value(session, "(equal n m)"));
            Assert.Equal("T", Value(session, "(equal (car m) a1)"));
            Assert.Equal("2", Value(session, "(length m)"));
        }

        [Fact]
        public void EntselOnTheTextOfAnAttribute_StillGivesTheBlockReference()
        {
            CadCommandSession session = Probe();

            // AutoCAD 2021 (GUI, pick4): (entsel "E: ") answered 103,2.5 gives the INSERT, its handle that of the reference
            Value(session, "(setq e (entsel \"E: \"))");
            Run(session, "103,2.5");

            Assert.Equal("T", Value(session, "(equal (car e) i1)"));
            Assert.Equal("\"INSERT\"", Value(session, "(cdr (assoc 0 (entget (car e))))"));
        }

        [Fact]
        public void NentselpOnTheLineOfAReferenceWithAnAttribute_StillGivesTheLineAndItsReference()
        {
            CadCommandSession session = Probe();

            // AutoCAD 2021 (GUI, pick2): at 125,5 the line of the block, four items: the line, the point, a matrix, the reference
            Assert.Equal("4", Value(session, "(length (nentselp '(125 5)))"));
            Assert.Equal("\"LINE\"", Value(session, "(cdr (assoc 0 (entget (car (nentselp '(125 5))))))"));
            Assert.Equal("T", Value(session, "(equal (car (cadddr (nentselp '(125 5)))) i1)"));
        }

        [Fact]
        public void NentselpOnTheTextOfAnInvisibleAttribute_GivesNil()
        {
            CadCommandSession session = Probe();

            // AutoCAD 2021 (GUI, pick4): at 303,2.5 an invisible attribute gives nil for nentselp, entsel and ssget alike
            Assert.Equal("nil", Value(session, "(nentselp '(303 2.5 0))"));
        }

        [Fact]
        public void EntdelOfAnAttribute_GivesNil_AndLeavesItInItsReference()
        {
            CadCommandSession session = Probe();

            // AutoCAD 2021 (GUI, pick): (entdel a1) is nil, twice; (entget a1) and the chain after the reference are as before, and
            // the attribute can still be picked
            Assert.Equal("nil", Value(session, "(entdel a1)"));
            Assert.Equal("nil", Value(session, "(entdel a1)"));
            Assert.Equal("\"+20\"", Value(session, "(cdr (assoc 1 (entget a1)))"));
            Assert.Equal("T", Value(session, "(equal (entnext i1) a1)"));
            Assert.Equal("T", Value(session, "(equal (car (nentselp '(103 2.5))) a1)"));
        }

        [Fact]
        public void AnE1LikeRoutine_PicksTheAttributeWithNentsel_AndChangesItsText()
        {
            CadCommandSession session = Probe();
            Value(
                session,
                "(defun c:ghe1 (/ r d) (setq r (nentsel \"Pick: \")) (setq d (entget (car r))) (entmod (subst (cons 1 \"EDITED\") (assoc 1 d) d)) (princ))"
            );

            Run(session, "GHE1", "103,2.5");

            // ours (D332 and X2): the routine takes the attribute from the pick itself, as it does in AutoCAD 2021 (pick2)
            Assert.Equal("\"EDITED\"", Value(session, "(cdr (assoc 1 (entget a1)))"));
            Assert.Equal("\"TAG1\"", Value(session, "(cdr (assoc 2 (entget a1)))"));
            Assert.Equal("\"ROT\"", Value(session, "(cdr (assoc 1 (entget a2)))"));
        }
    }
}
