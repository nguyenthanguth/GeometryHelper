using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;
using static GeometryHelper.Cad.UnitTest.LispEntityTests;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// <c>sssetfirst</c>, <c>ssgetfirst</c> and <c>nentselp</c>, with the values of AutoCAD 2021's accoreconsole (probe r9 of
    /// the L3 spec, decision D125): the pick-first selection a routine sets and reads, and the pick at a point given with
    /// nothing asked. The drawing of the probe is a line from (0, 0) to (10, 0), the name <c>e</c>.
    /// </summary>
    public sealed class LispPickfirstTests
    {
        // The drawing of r9: one line from (0,0) to (10,0), its name in e.
        private static CadCommandSession WithALine()
        {
            CadCommandSession session = Session(Blank());
            Run(session, "LINE", "0,0", "10,0", Enter);
            Value(session, "(setq e (entlast))");
            return session;
        }

        // The same with a block reference of a line at (100,50) turned by the angle.
        private static CadCommandSession WithABlockReference()
        {
            CadDocument document = Blank();
            var block = new DxfBlockTableRecord { Name = "B" };
            document.Database.BlockTable.Add(block);
            block.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0)));
            document.ApplyEdit("T", null, s =>
                s.Append(document.ModelSpace, new DxfBlockReference(new GeoPoint3(100, 50, 0), block))
            );
            return Session(document);
        }

        private static int LiveCount(CadCommandSession session) =>
            session.Document.Database.ModelSpace.OfType<DxfEntity>().Count(e => !e.IsErased);

        #region sssetfirst and ssgetfirst

        [Fact]
        public void SssetfirstWithASet_GivesNilAndTheSetAsGiven()
        {
            CadCommandSession session = WithALine();

            // AutoCAD 2021 (r9): (sssetfirst nil (ssadd e)) is (nil <Selection set: 6>)
            Assert.Matches(@"^\(nil <Selection set: \d+>\)$", Value(session, "(sssetfirst nil (ssadd e))"));
            Assert.Equal("T", Value(session, "(progn (setq s (ssadd e)) (eq (cadr (sssetfirst nil s)) s))"));
        }

        [Fact]
        public void SssetfirstWithASet_SelectsItsObjects()
        {
            CadCommandSession session = WithALine();
            string handle = session.Document.Selection.Handles.FirstOrDefault();

            Assert.Null(handle);

            Value(session, "(sssetfirst nil (ssadd e))");

            Assert.Equal(1, session.Document.Selection.Count);
            Assert.True(
                session.Document.Selection.Contains(
                    session.Document.Database.ModelSpace.OfType<DxfEntity>().Single().Handle
                )
            );
        }

        [Fact]
        public void SsgetfirstWithNothingSelected_GivesNilNil()
        {
            CadCommandSession session = WithALine();

            // AutoCAD 2021 (r9): (ssgetfirst) with nothing selected is (nil nil)
            Assert.Equal("(nil nil)", Value(session, "(ssgetfirst)"));
        }

        [Fact]
        public void SsgetfirstAfterSssetfirst_GivesANewSetOfTheSameObjectEachTime()
        {
            CadCommandSession session = WithALine();
            Value(session, "(sssetfirst nil (ssadd e))");

            // AutoCAD 2021 (r9): then (ssgetfirst) is (nil <Selection set: 9>), a number of its own, and (sslength (cadr
            // (ssgetfirst))) is 1
            Assert.Matches(@"^\(nil <Selection set: \d+>\)$", Value(session, "(ssgetfirst)"));
            Assert.Equal("1", Value(session, "(sslength (cadr (ssgetfirst)))"));
            Assert.Equal("nil", Value(session, "(eq (cadr (ssgetfirst)) (cadr (ssgetfirst)))"));
            Assert.Equal("T", Value(session, "(equal (ssname (cadr (ssgetfirst)) 0) e)"));
        }

        [Fact]
        public void SssetfirstNilNil_ClearsTheSelectionAndGivesNilNil()
        {
            CadCommandSession session = WithALine();
            Value(session, "(sssetfirst nil (ssadd e))");

            // AutoCAD 2021 (r9): (sssetfirst nil nil) is (nil nil), and (ssgetfirst) after it (nil nil)
            Assert.Equal("(nil nil)", Value(session, "(sssetfirst nil nil)"));
            Assert.Equal("(nil nil)", Value(session, "(ssgetfirst)"));
            Assert.Equal(0, session.Document.Selection.Count);
        }

        [Fact]
        public void SssetfirstWithOneArgument_GivesAListOfNil()
        {
            CadCommandSession session = WithALine();

            // AutoCAD 2021 (r9): (sssetfirst nil) is (nil)
            Assert.Equal("(nil)", Value(session, "(sssetfirst nil)"));
        }

        [Theory]
        [InlineData("(sssetfirst 5)")] // AutoCAD 2021 (r9)
        [InlineData("(sssetfirst nil 5)")] // AutoCAD 2021 (r9)
        [InlineData("(sssetfirst \"a\" nil)")]
        public void SssetfirstWithSomethingThatIsNoSet_IsABadArgumentType(string form)
        {
            CadCommandSession session = WithALine();
            Value(session, "(sssetfirst nil (ssadd e))");

            string text = Value(session, form);

            Assert.Contains("; error: bad argument type: lselsetp ", text);
            Assert.Equal(1, session.Document.Selection.Count);
        }

        [Fact]
        public void ASetWhoseObjectWasDeleted_SelectsTheLiveOnesOnly()
        {
            CadCommandSession session = WithALine();
            Run(session, "CIRCLE", "20,0", "5");
            Value(session, "(setq c (entlast))");
            Value(session, "(setq both (ssadd c (ssadd e)))");
            Value(session, "(entdel c)");

            Value(session, "(sssetfirst nil both)");

            Assert.Equal(1, session.Document.Selection.Count);
            Assert.Equal("1", Value(session, "(sslength (cadr (ssgetfirst)))"));
            Assert.Equal("T", Value(session, "(equal (ssname (cadr (ssgetfirst)) 0) e)"));
        }

        [Fact]
        public void SsgetfirstAfterAnObjectSelectedIsDeleted_LeavesItOut()
        {
            CadCommandSession session = WithALine();
            Run(session, "CIRCLE", "20,0", "5");
            Value(session, "(setq c (entlast))");
            Value(session, "(sssetfirst nil (ssadd c (ssadd e)))");
            Value(session, "(entdel c)");

            Assert.Equal("1", Value(session, "(sslength (cadr (ssgetfirst)))"));
        }

        [Fact]
        public void SsgetfirstWithSeveralObjects_GivesThemInTheOrderOfTheSetItWasGiven()
        {
            CadCommandSession session = WithALine();
            Run(session, "CIRCLE", "20,0", "5");
            Value(session, "(setq c (entlast))");
            Value(session, "(sssetfirst nil (ssadd e (ssadd c)))");

            // AutoCAD 2021 (feed fdval-full of the L5 acceptance, replacing the order of (ssget "I") of D125): the set is kept as it was made,
            // here the circle, added first, and then the line
            Assert.Equal("T", Value(session, "(equal (ssname (cadr (ssgetfirst)) 0) c)"));
            Assert.Equal("T", Value(session, "(equal (ssname (cadr (ssgetfirst)) 1) e)"));
        }

        [Fact]
        public void ACommandStartedWithCommandAfterSssetfirst_TakesTheSetAsTyped()
        {
            CadCommandSession session = WithALine();
            Value(session, "(sssetfirst nil (ssadd e))");
            int before = session.History.Count;

            // AutoCAD 2021 (r9): (sssetfirst nil (ssadd e)) then (command "ERASE") writes ERASE nil and 1 found
            Run(session, "(command \"ERASE\")");

            // the line after ERASE is where the presenter's (command) writes its value, apart from this part (L2)
            Assert.Equal(
                new[] { "Command: (command \"ERASE\")", "ERASE", "1 found" },
                session.History.Skip(before).Select(l => l.TrimEnd()).Take(3).ToArray()
            );
            Assert.False(session.IsRunning);
            Assert.Equal(0, LiveCount(session));
        }

        [Fact]
        public void ACommandTypedAfterSssetfirst_TakesTheSetAsTyped()
        {
            CadCommandSession session = WithALine();
            Value(session, "(sssetfirst nil (ssadd e))");
            int before = session.History.Count;

            Run(session, "ERASE");

            Assert.Contains("1 found", session.History.Skip(before).Select(l => l.TrimEnd()));
            Assert.False(session.IsRunning);
            Assert.Equal(0, LiveCount(session));
        }

        [Fact]
        public void WithPickfirstZero_TheCommandAsksForItsObjectsAnyway()
        {
            CadCommandSession session = WithALine();
            session.Document.PickFirst = 0;
            Value(session, "(sssetfirst nil (ssadd e))");

            Run(session, "ERASE");

            Assert.True(session.IsRunning);
            Assert.Equal("Select objects:", session.PromptText);
            Assert.Equal(1, LiveCount(session));
        }

        [Fact]
        public void ASelectionClearedByTheCommandItTookPartIn_IsGoneForSsgetfirst()
        {
            CadCommandSession session = WithALine();
            Run(session, "CIRCLE", "20,0", "5");
            Value(session, "(sssetfirst nil (ssadd e))");

            Run(session, "ERASE");

            Assert.Equal("(nil nil)", Value(session, "(ssgetfirst)"));
        }

        #endregion

        #region nentselp

        [Fact]
        public void NentselpGivenAPointOnTheLine_GivesTheNameAndThePointWithNothingAsked()
        {
            CadCommandSession session = WithALine();
            int before = session.History.Count;

            // AutoCAD 2021 (r9): (nentselp '(5 0)) is (<Entity name: 20725ab90d0> (5.0 0.0 0.0)) with no prompt
            Run(session, "(nentselp '(5 0))");

            Assert.Equal(
                new[] { "Command: (nentselp '(5 0))", "(<Entity name: *> (5.0 0.0 0.0))" },
                session
                    .History.Skip(before)
                    .Select(l =>
                        System.Text.RegularExpressions.Regex.Replace(
                            l.TrimEnd(),
                            "<Entity name: [0-9A-Fa-f]+>",
                            "<Entity name: *>"
                        )
                    )
                    .ToArray()
            );
            Assert.False(session.IsRunning);
            Assert.Equal("T", Value(session, "(equal (car (nentselp '(5 0))) e)"));
        }

        [Fact]
        public void NentselpGivenAMessageAndAPoint_AsksNothingAndGivesTheSame()
        {
            CadCommandSession session = WithALine();

            // AutoCAD 2021 (r9): (nentselp "Pick: " '(5 0)) is (<Entity name: ...> (5.0 0.0 0.0)), nothing asked
            Assert.Equal("(<Entity name: *> (5.0 0.0 0.0))", Value(session, "(nentselp \"Pick: \" '(5 0))"));
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void NentselpGivenThePointBeforeTheMessage_TakesThePointAllTheSame()
        {
            CadCommandSession session = WithALine();

            Assert.Equal("(<Entity name: *> (5.0 0.0 0.0))", Value(session, "(nentselp '(5 0) \"Pick: \")"));
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void NentselpGivenAPointOnNothing_GivesNil()
        {
            CadCommandSession session = WithALine();

            // AutoCAD 2021 (r9): (nentselp '(50 50)) is nil
            Assert.Equal("nil", Value(session, "(nentselp '(50 50))"));
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void NentselpWithAMessageOnly_AsksAsNentselDoes()
        {
            CadCommandSession session = WithALine();
            int before = session.History.Count;

            // AutoCAD 2021 (r9): (nentselp "X: ") then 10,0 writes X: 10,0 and (<Entity name: ...> (10.0 0.0 0.0))
            Run(session, "(nentselp \"X: \")");

            Assert.Equal("X: ", session.PromptText);

            Run(session, "10,0");

            Assert.Equal("X: 10,0", session.History.Skip(before + 1).First().TrimEnd());
            Assert.Matches(@"^\(<Entity name: [0-9a-f]+> \(10\.0 0\.0 0\.0\)\)$", session.History.Last().TrimEnd());
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void NentselpWithNothingGiven_AsksSelectObjectAndEnterGivesNil()
        {
            CadCommandSession session = WithALine();
            int before = session.History.Count;

            // AutoCAD 2021 (r9): (nentselp) writes Select object: and gives nil at Enter
            Run(session, "(nentselp)");

            Assert.Equal("Select object: ", session.PromptText);

            Run(session, Enter);

            Assert.Equal(
                new[] { "Command: (nentselp)", "Select object:", "nil" },
                session.History.Skip(before).Select(l => l.TrimEnd()).ToArray()
            );
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void NentselpWithAPointOfAUcs_TakesThePointInTheUcs()
        {
            CadCommandSession session = WithALine();
            Run(session, "UCS", "10,10", Enter);

            // the line from (0,0) to (10,0) of the world: its point (5,0) is (-5,-10) in a UCS at (10,10); ours (nentsel's way)
            Assert.Equal("(<Entity name: *> (-5.0 -10.0 0.0))", Value(session, "(nentselp '(-5 -10))"));
            Assert.Equal("nil", Value(session, "(nentselp '(5 0))"));
        }

        [Fact]
        public void NentselpIntoABlockReference_GivesNentselsForm()
        {
            CadCommandSession session = WithABlockReference();

            Value(session, "(setq n (nentselp '(100 50)))");

            // the form of nentsel (L2 D66): the line inside, the point, the matrix, the reference
            Assert.Equal("4", Value(session, "(length n)"));
            Assert.Equal("\"LINE\"", Value(session, "(cdr (assoc 0 (entget (car n))))"));
            Assert.Equal("(100.0 50.0 0.0)", Value(session, "(cadr n)"));
            Assert.Equal("((1.0 0.0 0.0) (0.0 1.0 0.0) (0.0 0.0 1.0) (100.0 50.0 0.0))", Value(session, "(caddr n)"));
            Assert.Equal("\"INSERT\"", Value(session, "(cdr (assoc 0 (entget (car (cadddr n)))))"));
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void NentselpAndNentselOnTheSameBlockPoint_GiveTheSameList()
        {
            CadCommandSession session = WithABlockReference();

            Value(session, "(setq n (nentselp '(100 50)))");
            Value(session, "(setq m (nentsel))");
            Run(session, "100,50");

            Assert.Equal("T", Value(session, "(equal n m)"));
        }

        [Fact]
        public void NentselpGivenSomethingThatIsNoPointOrMessage_IsABadArgumentType()
        {
            CadCommandSession session = WithALine();

            // AutoCAD 2021 (runs/wait): (nentselp 5) gives ; error: bad argument type: 2D/3D point: 5
            Assert.Contains("; error: bad argument type: 2D/3D point: 5", Value(session, "(nentselp 5)"));

            // ours: D136, the same wording for a list too short and for a second string
            Assert.Contains("; error: bad argument type: 2D/3D point: (1)", Value(session, "(nentselp '(1))"));
            Assert.Contains("; error: bad argument type: 2D/3D point: \"b\"", Value(session, "(nentselp \"a\" \"b\")"));
            Assert.False(session.IsRunning);
        }

        #endregion
    }
}
