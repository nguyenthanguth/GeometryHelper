using System.Linq;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// <c>getvar</c> and <c>setvar</c> typed at the command line, with the values and the refusals of AutoCAD 2021's
    /// accoreconsole (probe q6 of the L2 spec, a blank drawing).
    /// </summary>
    public sealed class LispVariableTests
    {
        // What a form gives, as the line after it.
        private static string Value(CadCommandSession session, string form)
        {
            int before = session.History.Count;
            session.Feed(form);
            return string.Join("\n", session.History.Skip(before + 1).Select(l => l.TrimEnd()));
        }

        [Theory]
        [InlineData("(getvar \"PRODUCT\")", "\"GeometryHelper.Cad\"")] // AutoCAD 2021 (q6): "AutoCAD"; ours (D56): tells the program
        [InlineData("(getvar \"ACADVER\")", "\"24.0\"")] // AutoCAD 2021 (q6)
        [InlineData("(getvar \"OSMODE\")", "167")] // AutoCAD 2021 (q6)
        [InlineData("(getvar \"osmode\")", "167")] // AutoCAD 2021 (q6): any case
        [InlineData("(getvar 'osmode)", "167")] // AutoCAD 2021 (spec D55): a symbol too
        [InlineData("(getvar \"CLAYER\")", "\"0\"")] // AutoCAD 2021 (q6)
        [InlineData("(getvar \"BLIPMODE\")", "0")] // AutoCAD 2021 (q6)
        [InlineData("(getvar \"LASTPOINT\")", "(0.0 0.0 0.0)")] // AutoCAD 2021 (q6)
        [InlineData("(getvar \"DIMSCALE\")", "1.0")] // AutoCAD 2021 (q6)
        [InlineData("(getvar \"TEXTSIZE\")", "2.5")] // AutoCAD 2021 (q6)
        [InlineData("(getvar \"UCSORG\")", "(0.0 0.0 0.0)")] // AutoCAD 2021 (q6)
        [InlineData("(getvar \"UCSXDIR\")", "(1.0 0.0 0.0)")] // AutoCAD 2021 (q6)
        [InlineData("(getvar \"LUPREC\")", "4")] // AutoCAD 2021 (q6)
        [InlineData("(getvar \"CMDECHO\")", "1")] // AutoCAD 2021 (q6)
        [InlineData("(getvar \"CMDNAMES\")", "\"\"")] // AutoCAD 2021 (q6)
        [InlineData("(getvar \"CMDACTIVE\")", "0")] // AutoCAD 2021 (q6)
        [InlineData("(getvar \"LISPINIT\")", "1")] // AutoCAD 2021 (q6)
        [InlineData("(getvar \"NOSUCH\")", "nil")] // AutoCAD 2021 (q6): a variable that is not there is nil
        public void Getvar_GivesTheValueOfAnAutoCadVariable(string form, string expected)
        {
            CadCommandSession session = Session(Blank());

            Assert.Equal(expected, Value(session, form));
        }

        [Fact]
        public void GetvarOfANumber_IsAnError_AndWithNoArgument_TooFew()
        {
            CadCommandSession session = Session(Blank());

            Assert.Equal("; error: bad argument type: (or stringp symbolp): 5", Value(session, "(getvar 5)")); // AutoCAD 2021 (q6)
            Assert.Equal("; error: too few arguments", Value(session, "(getvar)")); // AutoCAD 2021 (q6)
        }

        [Fact]
        public void SetvarOfAWholeNumber_TakesAWholeRealToo_AndGivesTheValueAsItWasGiven()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            // AutoCAD 2021 (q6): (setvar "LUPREC" 2) 2; (setvar "LUPREC" 2.0) 2.0
            Assert.Equal("2", Value(session, "(setvar \"LUPREC\" 2)"));
            Assert.Equal(2, document.Database.Luprec);
            Assert.Equal("3.0", Value(session, "(setvar \"LUPREC\" 3.0)"));
            Assert.Equal(3, document.Database.Luprec);
        }

        [Fact]
        public void SetvarOfARealGivenAsAWholeNumber_IsTakenAsAReal()
        {
            CadCommandSession session = Session(Blank());

            // AutoCAD 2021 (q6): (setvar "TEXTSIZE" 3) 3, then (getvar "TEXTSIZE") 3.0
            Assert.Equal("3", Value(session, "(setvar \"TEXTSIZE\" 3)"));
            Assert.Equal("3.0", Value(session, "(getvar \"TEXTSIZE\")"));
        }

        [Fact]
        public void SetvarOfAPoint_GivesTheListAsItWasGiven_AndGetvarThreeReals()
        {
            CadCommandSession session = Session(Blank());

            // AutoCAD 2021 (q6): (setvar "LASTPOINT" '(1 2)) (1 2), then (getvar "LASTPOINT") (1.0 2.0 0.0)
            Assert.Equal("(1 2)", Value(session, "(setvar \"LASTPOINT\" '(1 2))"));
            Assert.Equal("(1.0 2.0 0.0)", Value(session, "(getvar \"LASTPOINT\")"));
        }

        [Theory]
        [InlineData("(setvar \"NOSUCH\" 1)", "\"NOSUCH\" 1")] // AutoCAD 2021 (q6)
        [InlineData("(setvar \"ACADVER\" \"x\")", "\"ACADVER\" \"x\"")] // AutoCAD 2021 (q6): read only
        [InlineData("(setvar \"LUPREC\" \"x\")", "\"LUPREC\" \"x\"")] // AutoCAD 2021 (q6)
        [InlineData("(setvar \"LUPREC\" 99)", "\"LUPREC\" 99")] // AutoCAD 2021 (q6)
        [InlineData("(setvar \"CLAYER\" \"NOPE\")", "\"CLAYER\" \"NOPE\"")] // AutoCAD 2021 (q6): a layer that is not there
        [InlineData("(setvar \"PRODUCT\" \"x\")", "\"PRODUCT\" \"x\"")] // read only
        [InlineData("(setvar \"LUPREC\" nil)", "\"LUPREC\" nil")]
        public void SetvarOfWhatTheVariableRefuses_IsAnErrorNamingTheVariableAndTheValue(string form, string shown)
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Assert.Equal("; error: AutoCAD variable setting rejected: " + shown, Value(session, form));
            Assert.Equal(4, document.Database.Luprec);
        }

        [Fact]
        public void SetvarOfALayerThatIsThere_SetsTheCurrentLayer()
        {
            CadDocument document = Blank();
            document.Database.LayerTable.Add(
                new GeometryHelper.Dxf.DxfLayerTableRecord { Name = "GHA", ColorIndex = 1 }
            );
            CadCommandSession session = Session(document);

            Assert.Equal("\"GHA\"", Value(session, "(setvar \"CLAYER\" \"GHA\")"));
            Assert.Equal("\"GHA\"", Value(session, "(getvar \"CLAYER\")"));
        }

        [Fact]
        public void ASetvarOfADrawingVariable_IsOneUndoStepOfItsOwn_NamedForTheTypedForm()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Value(session, "(setvar \"LUPREC\" 2)");
            Run(session, "U");

            // ours (D42): a change made outside any command is an undo step of its own
            Assert.Equal(4, document.Database.Luprec);
        }
    }
}
