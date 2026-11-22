using System;
using System.Linq;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// <c>grdraw</c>, <c>grvecs</c> and <c>grclear</c>, with the rows AutoCAD 2021's accoreconsole gave (probe g1 of the C4b
    /// measurements, kept in the transcripts of the lispux scratch): each gives nil, or the error of a wrong argument. The
    /// vectors they draw are kept by the session, in millimetres of the world, for the window to show until a redraw takes
    /// them off.
    /// </summary>
    public sealed class LispTemporaryGraphicsTests : IDisposable
    {
        // Ends a routine a test left waiting, so no worker thread outlives its test.
        public void Dispose() => WaitingRoutines.EndAll();

        private static string Value(CadCommandSession session, string form) => LispEntityTests.Value(session, form);

        private static CadCommandSession Fresh()
        {
            CadCommandSession session = Session(Blank());
            Value(session, "(setvar \"CMDECHO\" 0)");
            return session;
        }

        #region grdraw

        [Fact]
        public void GrdrawOfAVector_GivesNilAndTheSessionHasTheVector()
        {
            CadCommandSession session = Fresh();

            // AutoCAD 2021 (g1): (grdraw '(0 0) '(10 10) 1) is nil; the points may have three coordinates.
            Assert.Equal("nil", Value(session, "(grdraw '(0 0) '(10 10) 1)"));
            Assert.Equal("nil", Value(session, "(grdraw '(0 0 0) '(10 10 5) 3 1)"));

            CadTemporaryVector first = session.TemporaryVectors[0];
            CadTemporaryVector second = session.TemporaryVectors[1];
            Assert.Equal(2, session.TemporaryVectors.Count);
            Assert.Equal(0.0, first.From.X, 9);
            Assert.Equal(10.0, first.To.X, 9);
            Assert.Equal(10.0, first.To.Y, 9);
            Assert.Equal(1, first.Colour);
            Assert.False(first.Highlight);
            Assert.Equal(5.0, second.To.Z, 9);
            Assert.Equal(3, second.Colour);
            Assert.True(second.Highlight);
        }

        [Fact]
        public void GrdrawWithAColourOfAnyWholeNumber_IsTakenAsItIs()
        {
            CadCommandSession session = Fresh();

            // AutoCAD 2021 (g1): -1 (XOR), 0, 256 and -300 are nil, not errors.
            foreach (int colour in new[] { -1, 0, 256, -300 })
            {
                Assert.Equal("nil", Value(session, "(grdraw '(0 0) '(10 10) " + colour + ")"));
            }

            Assert.Equal(new[] { -1, 0, 256, -300 }, session.TemporaryVectors.Select(v => v.Colour).ToArray());
        }

        [Fact]
        public void GrdrawTakesItsPointsInTheCurrentUcs()
        {
            CadCommandSession session = Fresh();
            Run(session, "UCS", "100,50", Enter);

            Value(session, "(grdraw '(1 2) '(3 4) 1)");

            Assert.Equal(101.0, session.TemporaryVectors[0].From.X, 9);
            Assert.Equal(52.0, session.TemporaryVectors[0].From.Y, 9);
            Assert.Equal(103.0, session.TemporaryVectors[0].To.X, 9);
        }

        [Theory]
        [InlineData("(grdraw '(0 0) '(10 10))", "; error: too few arguments")]
        [InlineData("(grdraw '(0 0) 1)", "; error: too few arguments")]
        [InlineData("(grdraw)", "; error: too few arguments")]
        [InlineData("(grdraw '(0 0) '(10 10) 1 1 1)", "; error: too many arguments")]
        [InlineData("(grdraw '(0 0) '(10 10) \"a\")", "; error: bad argument type: fixnump: \"a\"")]
        [InlineData("(grdraw '(0 0) '(10 10) 1.5)", "; error: bad argument type: fixnump: 1.5")]
        [InlineData("(grdraw '(0 0) '(10 10) 7 nil)", "; error: bad argument type: fixnump: nil")]
        [InlineData("(grdraw '(0 0) '(10 10) 1 T)", "; error: bad argument type: fixnump: T")]
        [InlineData("(grdraw \"a\" '(10 10) 1)", "; error: bad argument type: 2D/3D point: \"a\"")]
        [InlineData("(grdraw nil nil 1)", "; error: bad argument type: 2D/3D point: nil")]
        [InlineData("(grdraw '(0) '(10 10) 1)", "; error: bad argument type: 2D/3D point: (0)")]
        public void GrdrawWithTheWrongArguments_GivesAutoCadsErrorsAndDrawsNothing(string form, string error)
        {
            CadCommandSession session = Fresh();

            Assert.Equal(error, Value(session, form));
            Assert.Empty(session.TemporaryVectors);
        }

        #endregion

        #region grvecs

        [Fact]
        public void GrvecsOfAList_GivesNilAndDrawsEachVectorInTheColourBeforeIt()
        {
            CadCommandSession session = Fresh();

            // AutoCAD 2021 (g1): (grvecs '(1 (0 0) (10 10) 2 (5 5) (6 6))) is nil; a list with no colour, one with a lone colour
            // and one with a negative colour are nil too.
            Assert.Equal("nil", Value(session, "(grvecs '(1 (0 0) (10 10) 2 (5 5) (6 6)))"));

            Assert.Equal(new[] { 1, 2 }, session.TemporaryVectors.Select(v => v.Colour).ToArray());
            Assert.Equal(5.0, session.TemporaryVectors[1].From.X, 9);
            Assert.Equal("nil", Value(session, "(grvecs '((0 0) (10 10)))"));
            Assert.Equal(7, session.TemporaryVectors[2].Colour);
            Assert.Equal("nil", Value(session, "(grvecs '(1))"));
            Assert.Equal("nil", Value(session, "(grvecs '(-1 (0 0) (10 10)))"));
            Assert.Equal(-1, session.TemporaryVectors[3].Colour);
            Assert.Equal(4, session.TemporaryVectors.Count);
        }

        [Fact]
        public void GrvecsWithAMatrix_MovesEveryPointByItBeforeTheyAreDrawn()
        {
            CadCommandSession session = Fresh();

            // AutoCAD 2021 (g1): a matrix of four rows of four numbers is taken, whole numbers too; the last number of a row is the
            // shift, so a scale of 2 and a shift of 5 take (0 0) to (5 5) and (10 10) to (25 25).
            Assert.Equal(
                "nil",
                Value(
                    session,
                    "(grvecs '(1 (0 0) (10 10)) '((2.0 0.0 0.0 5.0) (0.0 2.0 0.0 5.0) (0.0 0.0 2.0 0.0) (0.0 0.0 0.0 1.0)))"
                )
            );
            Assert.Equal("nil", Value(session, "(grvecs '(1 (0 0) (10 10)) '((1 0 0 0)(0 1 0 0)(0 0 1 0)(0 0 0 1)))"));

            Assert.Equal(5.0, session.TemporaryVectors[0].From.X, 9);
            Assert.Equal(25.0, session.TemporaryVectors[0].To.Y, 9);
            Assert.Equal(10.0, session.TemporaryVectors[1].To.X, 9);
        }

        [Theory]
        [InlineData("(grvecs nil)", "; error: bad argument type: consp nil")]
        [InlineData("(grvecs 5)", "; error: bad argument type: consp 5")]
        [InlineData("(grvecs \"a\")", "; error: bad argument type: consp \"a\"")]
        [InlineData("(grvecs)", "; error: too few arguments")]
        [InlineData("(grvecs '(1 (0 0)))", "; error: grvecs missing endpoint")]
        [InlineData("(grvecs '(1 (0 0) (10 10) (3 3)))", "; error: grvecs missing endpoint")]
        [InlineData("(grvecs '(1.5 (0 0) (10 10)))", "; error: bad argument type: 2D/3D point: 1.5")]
        [InlineData("(grvecs '(1 (0 0) (10 10)) nil)", "; error: bad argument type: matrix: nil")]
        [InlineData("(grvecs '(1 (0 0) (10 10)) 5)", "; error: bad argument type: matrix: 5")]
        [InlineData(
            "(grvecs '(1 (0 0) (10 10)) '((2.0 0.0 0.0) (0.0 2.0 0.0) (0.0 0.0 2.0)))",
            "; error: bad argument type: matrix: ((2.0 0.0 0.0) (0.0 2.0 0.0) (0.0 0.0 2.0))"
        )]
        [InlineData(
            "(grvecs '(1 (0 0) (10 10)) '((1 0 0 0)(0 1 0 0)(0 0 1 0)(0 0 0 1)) 1)",
            "; error: too many arguments"
        )]
        public void GrvecsWithTheWrongArguments_GivesAutoCadsErrorsAndDrawsNothing(string form, string error)
        {
            CadCommandSession session = Fresh();

            Assert.Equal(error, Value(session, form));
            Assert.Empty(session.TemporaryVectors);
        }

        #endregion

        #region grclear and redraw

        [Fact]
        public void GrclearGivesNilAndTakesTheVectorsOff_AndWithAnArgumentIsTooManyArguments()
        {
            CadCommandSession session = Fresh();
            Value(session, "(grdraw '(0 0) '(10 10) 1)");

            // AutoCAD 2021 (g1): (grclear) is nil; (grclear 1) is too many arguments.
            Assert.Equal("; error: too many arguments", Value(session, "(grclear 1)"));
            Assert.Single(session.TemporaryVectors);
            Assert.Equal("nil", Value(session, "(grclear)"));
            Assert.Empty(session.TemporaryVectors);
        }

        [Theory]
        [InlineData("(redraw)")]
        [InlineData("REDRAW")]
        [InlineData("REDRAWALL")]
        [InlineData("REGEN")]
        public void RedrawAndRegen_TakeTheVectorsOff(string taken)
        {
            CadCommandSession session = Fresh();
            Value(session, "(grdraw '(0 0) '(10 10) 1)");
            Value(session, "(grvecs '(2 (0 0) (5 5)))");
            Assert.Equal(2, session.TemporaryVectors.Count);

            if (taken.StartsWith("(", StringComparison.Ordinal))
            {
                Value(session, taken);
            }
            else
            {
                Run(session, taken);
            }

            Assert.Empty(session.TemporaryVectors);
        }

        [Fact]
        public void RedrawOfAnEntity_LeavesTheVectors()
        {
            CadCommandSession session = Fresh();
            Value(session, "(setq e (progn (entmake '((0 . \"LINE\")(10 0.0 0.0 0.0)(11 5.0 5.0 0.0))) (entlast)))");
            Value(session, "(grdraw '(0 0) '(10 10) 1)");

            Value(session, "(redraw e 3)");

            Assert.Single(session.TemporaryVectors);
        }

        [Fact]
        public void TheSession_TellsTheWindowWhenTheVectorsChange()
        {
            CadCommandSession session = Fresh();
            int raised = 0;
            session.TemporaryGraphicsChanged += (s, e) => raised++;

            Value(session, "(grdraw '(0 0) '(10 10) 1)");
            Value(session, "(grclear)");
            Value(session, "(grclear)");

            Assert.Equal(2, raised);
        }

        #endregion
    }
}
