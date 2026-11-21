using System;
using System.Linq;
using System.Text.RegularExpressions;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// <c>entmod</c> of an ATTRIB of a block reference, as AutoCAD 2021's accoreconsole took it (probe d5 of the L5 spec, D332):
    /// the whole list or a partial one changes the attribute, a group 10 moves the attribute and not its block reference, a layer
    /// that is not there is made, and one undo step takes the change back. The attribute is written through its block reference,
    /// as the Properties palette writes it.
    /// </summary>
    public sealed class LispAttribEntmodTests : IDisposable
    {
        // Ends the routine a test left waiting, so no worker thread outlives its test.
        public void Dispose() => WaitingRoutines.EndAll();

        // The drawing of probe d5, read from a DXF (entmake makes neither blocks nor attributes): a block GHB with the attribute
        // definition TAG, and a reference of it at 10,10 whose attribute TAG reads OLD.
        private static CadCommandSession D5()
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
                    2.5,
                    "1",
                    "D",
                    "3",
                    "P",
                    "2",
                    "TAG",
                    "70",
                    0
                )
            );
            string insert =
                "0\nINSERT\n5\nB1\n8\n0\n66\n1\n2\nGHB\n10\n10\n20\n10\n30\n0\n"
                + TestDrawings.Entity(
                    "ATTRIB",
                    "0",
                    256,
                    "B2",
                    "10",
                    10.0,
                    "20",
                    10.0,
                    "30",
                    0.0,
                    "40",
                    2.5,
                    "1",
                    "OLD",
                    "2",
                    "TAG",
                    "70",
                    0
                )
                + "0\nSEQEND\n5\nB3\n8\n0\n";
            CadCommandSession session = Session(
                CadDocument.From(TestDrawings.Drawing(null, block, insert), "attrib.dxf")
            );
            Value(session, "(setq ins (entlast))");
            Value(session, "(setq att (entnext ins))");
            return session;
        }

        // What a form gives, as the lines after it: entity names written *.
        private static string Value(CadCommandSession session, string form)
        {
            int before = session.History.Count;
            session.Feed(form);
            string text = string.Join("\n", session.History.Skip(before + 1).Select(l => l.TrimEnd()));
            return Regex.Replace(text, @"<Entity name: [0-9A-Fa-f]+>", "<Entity name: *>");
        }

        [Fact]
        public void EntmodOfTheWholeListOfAnAttribute_ChangesItsText_AndGivesTheListBack()
        {
            CadCommandSession session = D5();
            Value(session, "(setq e (entget att))");

            // AutoCAD 2021 (d5): (entmod (subst (cons 1 "NEW") (assoc 1 e) e)) gives the list, and (cdr (assoc 1 (entget att))) is "NEW"
            Assert.Equal("\"OLD\"", Value(session, "(cdr (assoc 1 e))"));
            Assert.Equal(
                "T",
                Value(
                    session,
                    "(equal (entmod (subst (cons 1 \"NEW\") (assoc 1 e) e)) (subst (cons 1 \"NEW\") (assoc 1 e) e))"
                )
            );
            Assert.Equal("\"NEW\"", Value(session, "(cdr (assoc 1 (entget att)))"));
            Assert.Equal("\"ATTRIB\"", Value(session, "(cdr (assoc 0 (entget att)))"));
            Assert.Equal("\"TAG\"", Value(session, "(cdr (assoc 2 (entget att)))"));
        }

        [Fact]
        public void EntmodOfThePartialListOfAnAttribute_ChangesOnlyItsText()
        {
            CadCommandSession session = D5();

            // AutoCAD 2021 (d5): (entmod (list (cons -1 att) (cons 1 "PART"))) gives ((-1 . <Entity name: ...>) (1 . "PART"))
            Assert.Equal(
                "((-1 . <Entity name: *>) (1 . \"PART\"))",
                Value(session, "(entmod (list (cons -1 att) (cons 1 \"PART\")))")
            );
            Assert.Equal("\"PART\"", Value(session, "(cdr (assoc 1 (entget att)))"));
            Assert.Equal("(10.0 10.0 0.0)", Value(session, "(cdr (assoc 10 (entget att)))"));
            Assert.Equal("\"TAG\"", Value(session, "(cdr (assoc 2 (entget att)))"));
        }

        [Fact]
        public void EntmodOfAnAttributesPoint_MovesTheAttributeAndNotItsBlockReference()
        {
            CadCommandSession session = D5();

            // AutoCAD 2021 (d5): group 10 of the attribute is (20.0 20.0 0.0) after it, and group 10 of the block reference (10.0 10.0 0.0)
            Value(session, "(entmod (list (cons -1 att) (cons 10 '(20.0 20.0 0.0))))");

            Assert.Equal("(20.0 20.0 0.0)", Value(session, "(cdr (assoc 10 (entget att)))"));
            Assert.Equal("(10.0 10.0 0.0)", Value(session, "(cdr (assoc 10 (entget ins)))"));
        }

        [Fact]
        public void EntmodOfAnAttributeNamingALayerThatIsNotThere_MakesItWithColour7()
        {
            CadCommandSession session = D5();

            // AutoCAD 2021 (d5): the layer GHNEWL is made, (tblsearch "LAYER" "GHNEWL") is ((0 . "LAYER") (2 . "GHNEWL") (70 . 0) (62 . 7) (6 . "Continuous"))
            Value(session, "(entmod (list (cons -1 att) (cons 8 \"GHNEWL\")))");

            Assert.Equal("\"GHNEWL\"", Value(session, "(cdr (assoc 8 (entget att)))"));
            Assert.Equal(
                7,
                session.Document.Database.LayerTable["GHNEWL"] is DxfLayerTableRecord layer ? layer.ColorIndex : -1
            );
            Assert.Equal("\"0\"", Value(session, "(cdr (assoc 8 (entget ins)))"));
        }

        [Fact]
        public void EntupdOfTheBlockReference_GivesItsName_AndItsAttributeKeepsTheChange()
        {
            CadCommandSession session = D5();
            Value(session, "(entmod (list (cons -1 att) (cons 1 \"NEW\")))");

            // AutoCAD 2021 (d5): (entupd ins) gives <Entity name: ...>, and (entget (entnext ins)) still has the text NEW
            Assert.Equal("<Entity name: *>", Value(session, "(entupd ins)"));
            Assert.Equal("\"NEW\"", Value(session, "(cdr (assoc 1 (entget (entnext ins))))"));
            Assert.Equal("T", Value(session, "(equal ins (entupd ins))"));
        }

        [Fact]
        public void EntmodOfAnAttribute_IsOneUndoStep()
        {
            CadCommandSession session = D5();
            Value(session, "(entmod (list (cons -1 att) (cons 1 \"ONE\")))");
            Value(session, "(entmod (list (cons -1 att) (cons 1 \"TWO\") (cons 10 '(20.0 20.0 0.0))))");

            // ours (D332): one undo step as the entmod of any other entity; AutoCAD's d5 ran as one script, where U undid the lot
            Run(session, "U");

            Assert.Equal("\"ONE\"", Value(session, "(cdr (assoc 1 (entget att)))"));
            Assert.Equal("(10.0 10.0 0.0)", Value(session, "(cdr (assoc 10 (entget att)))"));
        }

        [Fact]
        public void EntmodOfAnAttribute_ChangesTheAttributeOfTheTypedModel()
        {
            CadCommandSession session = D5();

            // ours (D332): written through the block reference, as the Properties palette writes it
            Value(session, "(entmod (list (cons -1 att) (cons 1 \"TYPED\")))");

            DxfBlockReference insert = All<DxfBlockReference>(session.Document.ModelSpace).Single();
            Assert.Equal("TYPED", insert.AttributeCollection.Single().TextString);
        }

        [Fact]
        public void EntmodOfAnAttributeWithAChangedType_OrAValueOfTheWrongKind_IsNil()
        {
            CadCommandSession session = D5();

            // ours (D332): as for every entity (D-entmod of L2)
            Assert.Equal("nil", Value(session, "(entmod (list (cons -1 att) '(0 . \"TEXT\") (cons 1 \"X\")))"));
            Assert.Equal("nil", Value(session, "(entmod (list (cons -1 att) (cons 10 \"a\")))"));
            Assert.Equal("\"OLD\"", Value(session, "(cdr (assoc 1 (entget att)))"));
        }

        [Fact]
        public void EntmodOfAVertexOfAPolyline_StaysNil()
        {
            string drawing =
                "0\nSECTION\n2\nENTITIES\n"
                + "0\nPOLYLINE\n5\nA1\n8\n0\n66\n1\n10\n0\n20\n0\n30\n0\n70\n0\n"
                + "0\nVERTEX\n5\nA2\n8\n0\n10\n0\n20\n0\n30\n0\n"
                + "0\nVERTEX\n5\nA3\n8\n0\n10\n10\n20\n0\n30\n0\n"
                + "0\nSEQEND\n5\nA4\n8\n0\n"
                + "0\nENDSEC\n0\nEOF\n";
            CadCommandSession session = Session(CadDocument.From(DxfDatabase.Parse(drawing), "poly.dxf"));
            Value(session, "(setq v (entnext (entnext)))");

            // ours (D332): a vertex is a child of its polyline, and every child but an attribute stays out of entmod
            Assert.Equal("\"VERTEX\"", Value(session, "(cdr (assoc 0 (entget v)))"));
            Assert.Equal("nil", Value(session, "(entmod (list (cons -1 v) '(10 3.0 3.0 0.0)))"));
        }

        [Fact]
        public void AnE1LikeRoutine_TakesTheAttributeOfThePickedReference_AndChangesItsText()
        {
            CadCommandSession session = D5();
            Value(
                session,
                "(defun c:ghe1 (/ r a d) (setq r (nentselp '(10.5 10.5))) (setq a (entnext (car r))) (setq d (entget a)) (entmod (subst (cons 1 \"EDITED\") (assoc 1 d) d)) (princ))"
            );

            Run(session, "GHE1");

            // ours (D332): the presenter's nentsel gives the block reference for a pick on an attribute, so the routine walks to the attribute
            Assert.Equal("\"EDITED\"", Value(session, "(cdr (assoc 1 (entget att)))"));
            Assert.Equal("\"TAG\"", Value(session, "(cdr (assoc 2 (entget att)))"));
        }
    }
}
