using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;
using static GeometryHelper.Cad.UnitTest.LispEntityTests;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// <c>trans</c>, <c>osnap</c>, <c>alert</c>, <c>grtext</c>, <c>redraw</c>, <c>nentsel</c> into a block and the sine and
    /// cosine of AutoLISP, with the values of AutoCAD 2021's accoreconsole (probes q6 and q10 of the L2 spec).
    /// </summary>
    public sealed class LispSessionFunctionTests : IDisposable
    {
        // Ends the routine a test left waiting, so no worker thread outlives its test.
        public void Dispose() => WaitingRoutines.EndAll();

        private static CadCommandSession WithTheUcsAt1010()
        {
            CadCommandSession session = Session(Blank());

            // AutoCAD 2021 (q6): (command "UCS" "O" '(10 10 0)) moves the origin to (10 10 0)
            Run(session, "UCS", "10,10", Enter);
            return session;
        }

        [Fact]
        public void Trans_BetweenTheWorldAndTheUcs_MovesThePointByTheOrigin()
        {
            CadCommandSession session = Session(Blank());

            // AutoCAD 2021 (q6): in the world UCS (trans '(1 2 0) 0 1) is (1.0 2.0 0.0)
            Assert.Equal("(1.0 2.0 0.0)", Value(session, "(trans '(1 2 0) 0 1)"));

            Run(session, "UCS", "10,10", Enter);

            // AutoCAD 2021 (q6): with the UCS at 10,10 (trans '(1 2 0) 0 1) is (-9.0 -8.0 0.0), (trans '(1 2 0) 1 0) (11.0 12.0 0.0)
            Assert.Equal("(-9.0 -8.0 0.0)", Value(session, "(trans '(1 2 0) 0 1)"));
            Assert.Equal("(11.0 12.0 0.0)", Value(session, "(trans '(1 2 0) 1 0)"));
        }

        [Fact]
        public void Trans_OfADisplacement_LeavesOutTheOrigin()
        {
            CadCommandSession session = WithTheUcsAt1010();

            // AutoCAD 2021 (q6): (trans '(1 2 0) 1 0 T) is (1.0 2.0 0.0)
            Assert.Equal("(1.0 2.0 0.0)", Value(session, "(trans '(1 2 0) 1 0 T)"));
        }

        [Fact]
        public void Trans_OfATwoDimensionalPoint_GivesThreeCoordinates()
        {
            CadCommandSession session = WithTheUcsAt1010();

            // AutoCAD 2021 (q6): (trans '(1 2) 1 2) is (11.0 12.0 0.0): the display is the world for a view in plan
            Assert.Equal("(11.0 12.0 0.0)", Value(session, "(trans '(1 2) 1 2)"));
            Assert.Equal("(11.0 12.0 0.0)", Value(session, "(trans '(1 2 0) 1 3)"));
        }

        [Fact]
        public void Trans_ToAnExtrusionVector_GivesTheWorldPointOfTheWorldZ()
        {
            CadCommandSession session = WithTheUcsAt1010();

            // AutoCAD 2021 (q6): (trans '(1 2 0) 1 '(0 0 1)) is (11.0 12.0 0.0)
            Assert.Equal("(11.0 12.0 0.0)", Value(session, "(trans '(1 2 0) 1 '(0 0 1))"));
        }

        [Fact]
        public void Trans_ToTheSystemOfAnEntitysExtrusion_UsesTheArbitraryAxisAlgorithm()
        {
            CadCommandSession session = Session(Blank());
            Value(session, "(entmake '((0 . \"CIRCLE\") (10 0.0 0.0 0.0) (40 . 1.0) (210 0.0 0.0 -1.0)))");

            // ours (AutoCAD's documented arbitrary axis algorithm): the x axis of an extrusion of -z is -x, the y axis y
            Assert.Equal("(-1.0 2.0 -3.0)", Value(session, "(trans '(1 2 3) 0 (entlast))"));
            Assert.Equal("(1.0 2.0 3.0)", Value(session, "(trans '(-1 2 -3) (entlast) 0)"));
            Assert.Equal("(-1.0 2.0 -3.0)", Value(session, "(trans '(1 2 3) 0 '(0 0 -1))"));
        }

        [Fact]
        public void Trans_ToAnExtrusionLeaningOverTheWorld_TakesTheWorldZCrossedWithIt()
        {
            CadCommandSession session = Session(Blank());

            // ours: the normal (1 0 0) has an x axis of (0 1 0) (the world z crossed with it), then y (0 0 1)
            Assert.Equal("(2.0 3.0 1.0)", Value(session, "(trans '(1 2 3) 0 '(1 0 0))"));
        }

        [Fact]
        public void Trans_OfWhatIsNoPoint_IsAnError_AndOfASystemThatIsNone_Nil()
        {
            CadCommandSession session = Session(Blank());

            Assert.Equal("; error: bad argument type: 2D/3D point: 5", Value(session, "(trans 5 0 1)"));
            Assert.Equal("nil", Value(session, "(trans '(1 2 3) 0 7)"));
        }

        [Theory]
        [InlineData("(osnap '(1 0.1) \"end\")", "(0.0 0.0 0.0)")] // AutoCAD 2021 (q6)
        [InlineData("(osnap '(1 0.1) \"_end,_mid\")", "(0.0 0.0 0.0)")] // AutoCAD 2021 (q6)
        [InlineData("(osnap '(4 0.1) \"mid\")", "(5.0 0.0 0.0)")] // AutoCAD 2021 (q6)
        [InlineData("(osnap '(50 50) \"end\")", "nil")] // AutoCAD 2021 (q6): no object there
        [InlineData("(osnap '(1 0) \"foo\")", "nil")] // AutoCAD 2021 (q6): a mode that is none
        [InlineData("(osnap '(1 0.5) \"non\")", "(1.0 0.5 0.0)")] // ours: no snap leaves the point
        [InlineData("(osnap '(1 0.1) \"cen\")", "nil")] // a line has no centre
        public void Osnap_GivesTheNearestSnapOfTheModes(string form, string expected)
        {
            CadCommandSession session = Session(Blank());
            Run(session, "LINE", "0,0", "10,0", Enter);

            Assert.Equal(expected, Value(session, form));
        }

        [Fact]
        public void Osnap_WhereTheDrawingHasNothing_IsNil()
        {
            CadCommandSession session = Session(Blank());

            // AutoCAD 2021 (q6): (osnap '(0 0) "end") nil in a drawing with no object
            Assert.Equal("nil", Value(session, "(osnap '(0 0) \"end\")"));
        }

        [Fact]
        public void Osnap_OfACircle_GivesTheCentreAndTheQuadrants()
        {
            CadCommandSession session = Session(Blank());
            Run(session, "CIRCLE", "20,0", "5");

            Assert.Equal("(20.0 0.0 0.0)", Value(session, "(osnap '(21 1) \"cen\")"));
            Assert.Equal("(25.0 0.0 0.0)", Value(session, "(osnap '(24.5 0.5) \"qua\")"));
        }

        [Fact]
        public void Alert_WhereTheWindowAnswers_RaisesTheRequestWithTheText_AndWritesNothing()
        {
            CadCommandSession session = Session(Blank());
            var seen = new List<string>();
            session.UiRequested += (sender, e) =>
            {
                if (e.Name == "ALERT")
                {
                    seen.Add((string)e.Payload);
                    e.Handled = true;
                }
            };

            // AutoCAD 2021 (q6): (alert "Xong") shows a message box and gives nil
            Assert.Equal("nil", Value(session, "(alert \"Xong\")"));
            Assert.Equal(new[] { "Xong" }, seen);
        }

        [Fact]
        public void Alert_WithNoWindow_WritesTheLinesOfTheText()
        {
            CadCommandSession session = Session(Blank());

            // AutoCAD 2021 (q6, runs/tca): the message box block is the text, then nil
            Assert.Equal("Xong\nnil", Value(session, "(alert \"Xong\")"));
            Assert.Equal("one\ntwo\nnil", Value(session, "(alert \"one\\ntwo\")"));
        }

        [Fact]
        public void AlertAfterACommandOfTheRoutine_WritesItsTextOnALineOfItsOwn_AndTheValueAfterTheCommandPrompt()
        {
            CadCommandSession session = Session(Blank());
            Run(session, "(defun c:gha () (command \"CIRCLE\" '(0 0) 3) (alert \"Xong\"))", "GHA");

            // AutoCAD 2021 (runs/tca, the message box block normalised): Xong on a line of its own, then Command: nil
            string[] lines = session.History.Select(l => l.TrimEnd()).ToArray();
            Assert.Equal(new[] { "Xong", "Command: nil" }, lines.Skip(lines.Length - 2).ToArray());
        }

        [Fact]
        public void Alert_OfWhatIsNoString_IsAnError()
        {
            CadCommandSession session = Session(Blank());

            Assert.Equal("; error: bad argument type: stringp 5", Value(session, "(alert 5)"));
        }

        [Fact]
        public void Grtext_GivesTheTextBack_AndNilWithNoArguments()
        {
            CadCommandSession session = Session(Blank());

            Assert.Equal("\"status\"", Value(session, "(grtext -1 \"status\")")); // AutoCAD 2021 (q6)
            Assert.Equal("nil", Value(session, "(grtext)")); // AutoCAD 2021 (q6)
        }

        [Fact]
        public void Redraw_IsNil_AndOfNoEntityAnError()
        {
            CadCommandSession session = Session(Blank());

            Assert.Equal("nil", Value(session, "(redraw)")); // AutoCAD 2021 (q6)
            Assert.Equal("; error: bad argument type: lentityp nil", Value(session, "(redraw (entlast) 3)")); // AutoCAD 2021 (q6)
        }

        [Fact]
        public void Redraw_OfAnEntity_IsNil()
        {
            CadCommandSession session = Q5();

            Assert.Equal("nil", Value(session, "(redraw (entlast) 3)"));
        }

        [Theory]
        [InlineData("(sin pi)", "1.22465e-16")] // AutoCAD 2021 (q10)
        [InlineData("(cos (/ pi 2))", "6.12323e-17")] // AutoCAD 2021 (q10)
        [InlineData("(sin 1e20)", "-0.645251")] // AutoCAD 2021 (q10)
        [InlineData("(sin 0)", "0.0")]
        [InlineData("(cos 0)", "1.0")]
        [InlineData("(sin (/ pi 2))", "1.0")]
        [InlineData("(cos pi)", "-1.0")]
        public void SinAndCos_AreToTheLastDigitNearAMultipleOfAQuarterTurn(string form, string expected)
        {
            CadCommandSession session = Session(Blank());

            Assert.Equal(expected, Value(session, form));
        }

        private static CadCommandSession WithABlockReference(double rotation)
        {
            CadDocument document = Blank();
            var block = new DxfBlockTableRecord { Name = "B" };
            document.Database.BlockTable.Add(block);
            block.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0)));
            document.ApplyEdit("T", null, s =>
                s.Append(
                    document.ModelSpace,
                    new DxfBlockReference(new GeoPoint3(100, 50, 0), block) { Rotation = rotation }
                )
            );
            return Session(document);
        }

        [Fact]
        public void NentselOnAnObjectOfASpace_GivesTheNameAndThePointOnly()
        {
            CadCommandSession session = Q5();

            // AutoCAD 2021 (q5): (nentsel "N: ") at the line gives the name and the point; the list of entsel
            Value(session, "(setq r (nentsel))");
            Run(session, "0,0");

            Assert.Equal("2", Value(session, "(length r)"));
        }

        [Fact]
        public void NentselOnABlockReference_GivesTheObjectInsideItsPointTheMatrixAndTheReference()
        {
            CadCommandSession session = WithABlockReference(0.0);

            // AutoCAD 2021 (GUI, X2 pick2): (entname pickpoint matrix (block ename)), the matrix 4 by 4 row by row, the images of
            // the three axes in its columns and the origin in the fourth, the references the object is in from the innermost out
            Value(session, "(setq r (nentsel))");
            Run(session, "100,50");

            Assert.Equal("4", Value(session, "(length r)"));
            Assert.Equal("\"LINE\"", Value(session, "(cdr (assoc 0 (entget (car r))))"));
            Assert.Equal("(100.0 50.0 0.0)", Value(session, "(cadr r)"));
            Assert.Equal(
                "((1.0 0.0 0.0 100.0) (0.0 1.0 0.0 50.0) (0.0 0.0 1.0 0.0) (0.0 0.0 0.0 1.0))",
                Value(session, "(caddr r)")
            );
            Assert.Equal("\"INSERT\"", Value(session, "(cdr (assoc 0 (entget (car (cadddr r)))))"));
            Assert.Equal("1", Value(session, "(length (cadddr r))"));
        }

        [Fact]
        public void NentselOnATurnedBlockReference_GivesTheMatrixOfItsTurn()
        {
            CadCommandSession session = WithABlockReference(System.Math.PI / 2.0);

            Value(session, "(setq r (nentsel))");
            Run(session, "100,50");

            // AutoCAD 2021 (GUI, X2 pick2): the block turned a quarter turn, its x axis along the world y and its y axis along minus x,
            // are the columns (0 1 0) and (-1 0 0): the rows read (0 -1 0 100) and (1 0 0 50)
            Assert.Equal(
                "((0.0 -1.0 0.0 100.0) (1.0 0.0 0.0 50.0) (0.0 0.0 1.0 0.0) (0.0 0.0 0.0 1.0))",
                Value(session, "(caddr r)")
            );
        }
    }
}
