using System;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// <c>ssnamex</c>, with the rows AutoCAD 2021's accoreconsole gave (probes e1 and e2 of the C4b measurements, kept in the
    /// transcripts of the lispux scratch). The corners AutoCAD gives for a window are those of the console's own view, a few
    /// thousandths wider than the corners typed and an upper edge it takes from its view: the presenter gives the corners as they
    /// were typed. An entity name is written <c>*</c>, as the harness normalises it.
    /// </summary>
    public sealed class LispSsnamexTests : IDisposable
    {
        // Ends a routine a test left waiting, so no worker thread outlives its test.
        public void Dispose() => WaitingRoutines.EndAll();

        private static string Value(CadCommandSession session, string form) => LispEntityTests.Value(session, form);

        // Two lines and a circle: l1 from (0,0) to (10,0), l2 from (0,5) to (10,5) and c1 at (5,5) with radius 1.
        private static CadCommandSession Drawn()
        {
            CadCommandSession session = Session(Blank());
            Value(session, "(setvar \"CMDECHO\" 0)");
            Value(session, "(setq l1 (progn (entmake '((0 . \"LINE\")(10 0.0 0.0 0.0)(11 10.0 0.0 0.0))) (entlast)))");
            Value(session, "(setq l2 (progn (entmake '((0 . \"LINE\")(10 0.0 5.0 0.0)(11 10.0 5.0 0.0))) (entlast)))");
            Value(session, "(setq c1 (progn (entmake '((0 . \"CIRCLE\")(10 5.0 5.0 0.0)(40 . 1.0))) (entlast)))");
            return session;
        }

        [Fact]
        public void SsnamexOfASetMadeByAProgram_GivesEachEntityWithTheMethod0()
        {
            CadCommandSession session = Drawn();
            Value(session, "(setq ss (ssget \"_X\"))");

            // AutoCAD 2021 (e1): ((0 <E> 0) (0 <E> 0) (0 <E> 0)) for all three; the index picks one, -1 and nil all, one past the
            // last nil, a real is cut to a whole number.
            Assert.Equal(
                "((0 <Entity name: *> 0) (0 <Entity name: *> 0) (0 <Entity name: *> 0))",
                Value(session, "(ssnamex ss)")
            );
            Assert.Equal("((0 <Entity name: *> 0))", Value(session, "(ssnamex ss 0)"));
            Assert.Equal("((0 <Entity name: *> 0))", Value(session, "(ssnamex ss 2)"));
            Assert.Equal("((0 <Entity name: *> 0))", Value(session, "(ssnamex ss 1.5)"));
            Assert.Equal("nil", Value(session, "(ssnamex ss 3)"));
            Assert.Equal("nil", Value(session, "(ssnamex ss 5)"));
            Assert.Equal(Value(session, "(ssnamex ss)"), Value(session, "(ssnamex ss -1)"));
            Assert.Equal(Value(session, "(ssnamex ss)"), Value(session, "(ssnamex ss nil)"));
        }

        [Fact]
        public void SsnamexOfAnEmptySet_IsNil_AndTheNameOfTheEntitiesAddedIsMethod0()
        {
            CadCommandSession session = Drawn();
            Value(session, "(setq ss (ssadd))");

            // AutoCAD 2021 (e1): (ssnamex (ssadd)) is nil; after (ssadd l1 ss) it is ((0 E 0)); after (ssdel l1 ss) nil again.
            Assert.Equal("nil", Value(session, "(ssnamex ss)"));
            Value(session, "(ssadd l1 ss)");
            Assert.Equal("((0 <Entity name: *> 0))", Value(session, "(ssnamex ss)"));
            Value(session, "(ssadd c1 ss)");
            Value(session, "(ssdel l1 ss)");
            Assert.Equal("((0 <Entity name: *> 0))", Value(session, "(ssnamex ss)"));
        }

        [Fact]
        public void SsnamexOfAWindow_GivesTheMethod2AndTheCornersAsALastItem()
        {
            CadCommandSession session = Drawn();
            Value(session, "(setq w (ssget \"_W\" '(-1 -1) '(11 6)))");

            // AutoCAD 2021 (e1, e2): each entity is (2 E 0 -1) and the corners follow as (-1 (0 (x y z)) ...), from the top left
            // round; the corners are given with an index too, and as a last item of the list of all of them; added to
            // or deleted from, the set keeps its method.
            Assert.Equal(
                "((2 <Entity name: *> 0 -1) (2 <Entity name: *> 0 -1) (2 <Entity name: *> 0 -1) (-1 (0 (-1.0 6.0 0.0)) (0 (11.0 6.0 0.0)) (0 (11.0 -1.0 0.0)) (0 (-1.0 -1.0 0.0))))",
                Value(session, "(ssnamex w)")
            );
            Assert.Equal(
                "((2 <Entity name: *> 0 -1) (-1 (0 (-1.0 6.0 0.0)) (0 (11.0 6.0 0.0)) (0 (11.0 -1.0 0.0)) (0 (-1.0 -1.0 0.0))))",
                Value(session, "(ssnamex w 1)")
            );
            Value(session, "(ssdel c1 w)");
            Assert.Equal(
                "((2 <Entity name: *> 0 -1) (2 <Entity name: *> 0 -1) (-1 (0 (-1.0 6.0 0.0)) (0 (11.0 6.0 0.0)) (0 (11.0 -1.0 0.0)) (0 (-1.0 -1.0 0.0))))",
                Value(session, "(ssnamex w)")
            );
        }

        [Fact]
        public void SsnamexOfAWindowTypedFromTheOtherCorner_GivesTheSameCorners()
        {
            CadCommandSession session = Drawn();

            // AutoCAD 2021 (e2): the corners of (ssget "_W" '(11 6) '(-1 -1)) are those of (ssget "_W" '(-1 -1) '(11 6)).
            Value(session, "(setq w (ssget \"_W\" '(-1 -1) '(11 6)))");
            Value(session, "(setq v (ssget \"_W\" '(11 6) '(-1 -1)))");

            Assert.Equal(Value(session, "(ssnamex w)"), Value(session, "(ssnamex v)"));
        }

        [Fact]
        public void SsnamexOfACrossing_GivesTheMethod3AndTheCorners()
        {
            CadCommandSession session = Drawn();
            Value(session, "(setq w (ssget \"_C\" '(4 2) '(6 4.5)))");

            // AutoCAD 2021 (e2): (3 E 0 -1) and the corners; the circle only is crossed.
            Assert.Equal(
                "((3 <Entity name: *> 0 -1) (-1 (0 (4.0 4.5 0.0)) (0 (6.0 4.5 0.0)) (0 (6.0 2.0 0.0)) (0 (4.0 2.0 0.0))))",
                Value(session, "(ssnamex w)")
            );
        }

        [Fact]
        public void SsnamexOfAPolygon_GivesTheMethodOfItsKindAndItsPointsFromTheLastBack()
        {
            CadCommandSession session = Drawn();
            Value(session, "(setq cp (ssget \"_CP\" '((4 2) (6 2) (6 4.5) (4 4.5))))");

            // AutoCAD 2021 (e2): a crossing polygon is method 3 and lists its points from the last back to the first.
            Assert.Equal(
                "((3 <Entity name: *> 0 -1) (-1 (0 (4.0 4.5 0.0)) (0 (6.0 4.5 0.0)) (0 (6.0 2.0 0.0)) (0 (4.0 2.0 0.0))))",
                Value(session, "(ssnamex cp)")
            );
            Value(session, "(setq wp (ssget \"_WP\" '((-1 -1) (11 -1) (11 6) (-1 6))))");
            Assert.StartsWith("((2 <Entity name: *> 0 -1)", Value(session, "(ssnamex wp)"));
            Assert.EndsWith(
                "(-1 (0 (-1.0 6.0 0.0)) (0 (11.0 6.0 0.0)) (0 (11.0 -1.0 0.0)) (0 (-1.0 -1.0 0.0))))",
                Value(session, "(ssnamex wp)")
            );
        }

        [Fact]
        public void SsnamexOfAPick_GivesTheMethod1AndThePickedPoint()
        {
            CadCommandSession session = Drawn();
            Value(session, "(setq pk (ssget '(5 0)))");

            // AutoCAD 2021 (e2): ((1 E 0 (0 (5.0 0.0 0.0)))); a point of three coordinates gives the same.
            Assert.Equal("((1 <Entity name: *> 0 (0 (5.0 0.0 0.0))))", Value(session, "(ssnamex pk)"));
            Value(session, "(setq pk2 (ssget '(5.0 0.0 0.0)))");
            Assert.Equal("((1 <Entity name: *> 0 (0 (5.0 0.0 0.0))))", Value(session, "(ssnamex pk2)"));
        }

        [Fact]
        public void SsnamexOfAFence_GivesTheMethod4ForEachEntity()
        {
            CadCommandSession session = Drawn();
            Value(session, "(setq f (ssget \"_F\" '((5 -1) (5 6))))");

            // AutoCAD 2021 (e1) gives (4 E 0 (0 (x y z)) ...) with the points the fence crosses each entity at; the presenter
            // gives (4 E 0): the method, and no points.
            Assert.Equal(
                "((4 <Entity name: *> 0) (4 <Entity name: *> 0) (4 <Entity name: *> 0))",
                Value(session, "(ssnamex f)")
            );
        }

        [Fact]
        public void SsnamexOfTheLastAndPreviousSets_GivesTheMethodOfTheSetTheyAre()
        {
            CadCommandSession session = Drawn();
            Value(session, "(setq lt (ssget \"_L\"))");

            // AutoCAD 2021 (e1, e2): Last is ((0 E 0)); Previous after a window is that window's data.
            Assert.Equal("((0 <Entity name: *> 0))", Value(session, "(ssnamex lt)"));
            Value(session, "(setq w (ssget \"_C\" '(4 2) '(6 4.5)))");
            Value(session, "(setq pv (ssget \"_P\"))");
            Assert.Equal(Value(session, "(ssnamex w)"), Value(session, "(ssnamex pv)"));
        }

        [Theory]
        [InlineData("(ssnamex)", "; error: too few arguments")]
        [InlineData("(ssnamex 5)", "; error: bad argument type: lselsetp 5")]
        [InlineData("(ssnamex nil)", "; error: bad argument type: lselsetp nil")]
        [InlineData("(ssnamex ss 0 0)", "; error: too many arguments")]
        [InlineData("(ssnamex ss \"a\")", "; error: bad argument type: numberp: \"a\"")]
        [InlineData("(ssnamex ss T)", "; error: bad argument type: numberp: T")]
        [InlineData("(ssnamex ss '(1))", "; error: bad argument type: numberp: (1)")]
        [InlineData("(ssnamex ss 100000000000)", "; error: Invalid integer value: 1.0e+11")]
        public void SsnamexWithTheWrongArguments_GivesAutoCadsErrors(string form, string error)
        {
            CadCommandSession session = Drawn();
            Value(session, "(setq ss (ssget \"_X\"))");

            Assert.Equal(error, Value(session, form));
        }
    }
}
