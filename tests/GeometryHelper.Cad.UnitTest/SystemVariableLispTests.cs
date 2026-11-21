using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// What a system variable gives AutoLISP's <c>getvar</c> and takes from <c>setvar</c> (<c>ValueForLisp</c> and
    /// <c>SetFromLisp</c>), in the neutral types they are given in: a whole number is an <c>int</c>, a real a <c>double</c>
    /// (an angle in radians), a text a <c>string</c> and a point a <c>double[]</c>; and the variables AutoLISP routines ask of
    /// the program (PRODUCT, ACADVER, CMDACTIVE, CMDNAMES, UCSXDIR). Expected values marked AutoCAD 2021 are those of the
    /// measurement q6 (accoreconsole, blank drawing).
    /// </summary>
    public sealed class SystemVariableLispTests : IDisposable
    {
        // Ends the routine a test left waiting, so no worker thread outlives its test.
        public void Dispose() => WaitingRoutines.EndAll();

        private static CadCommandSession Start(CadDocument document)
        {
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            return session;
        }

        private static object Get(CadCommandSession session, string name) =>
            session.Registry.FindVariable(name).ValueForLisp(session);

        private static bool Set(CadCommandSession session, string name, object value)
        {
            bool taken = session.Registry.FindVariable(name).SetFromLisp(session, value);

            // A change outside any command opens the undo step of the evaluation; a test closes it where it undoes.
            session.CloseGroup();
            return taken;
        }

        [Fact]
        public void AWholeNumber_IsAnInt_AndASetTakesAWholeRealOrAnInt()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document);

            Assert.Equal(4, Get(session, "LUPREC")); // AutoCAD 2021 (q6): (getvar "LUPREC") 4
            Assert.True(Set(session, "LUPREC", 2)); // AutoCAD 2021 (q6): (setvar "LUPREC" 2) 2
            Assert.Equal(2, document.Database.Luprec);
            Assert.True(Set(session, "LUPREC", 3.0)); // AutoCAD 2021 (q6): (setvar "LUPREC" 2.0) 2.0, taken
            Assert.Equal(3, document.Database.Luprec);
            Assert.Equal(3, Get(session, "luprec"));
        }

        [Theory]
        [InlineData("x")] // AutoCAD 2021 (q6): (setvar "LUPREC" "x") rejected
        [InlineData(99)] // AutoCAD 2021 (q6): (setvar "LUPREC" 99) rejected
        [InlineData(-1)]
        [InlineData(2.5)] // ours: a real that is not whole is not a whole number
        [InlineData(null)] // nil is no value
        public void AWholeNumberOutOfRangeOrOfAnotherKind_IsRefusedAndLeavesTheValue(object value)
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document);

            Assert.False(Set(session, "LUPREC", value));
            Assert.Equal(4, document.Database.Luprec);
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void ARealIsADouble_AWholeNumberIsTakenForOne_AndNoughtIsRefusedWhereTheVariableIsPositive()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document);
            double before = (double)Get(session, "TEXTSIZE");

            Assert.True(Set(session, "TEXTSIZE", 3)); // AutoCAD 2021 (q6): (setvar "TEXTSIZE" 3) 3
            Assert.Equal(3.0, Get(session, "TEXTSIZE")); // AutoCAD 2021 (q6): (getvar "TEXTSIZE") 3.0
            Assert.False(Set(session, "TEXTSIZE", 0)); // SETVAR TEXTSIZE 0: "Value must be positive and nonzero."
            Assert.False(Set(session, "TEXTSIZE", "x"));
            Assert.False(Set(session, "TEXTSIZE", double.NaN));
            Assert.Equal(3.0, Get(session, "TEXTSIZE"));
            Assert.NotEqual(3.0, before);
        }

        [Fact]
        public void AnAngleIsInRadians_KeptFromNoughtUpToATurn_AndTheHeaderHoldsDegrees()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document);

            Assert.Equal(0.0, Get(session, "ANGBASE"));
            Assert.True(Set(session, "ANGBASE", Math.PI / 2.0));
            Assert.Equal(Math.PI / 2.0, (double)Get(session, "ANGBASE"), 9);
            Assert.Equal(Math.PI / 2.0, document.Database.Angbase, 9);

            Assert.True(Set(session, "ANGBASE", -Math.PI / 2.0)); // ours: as typed -90 is kept as 270
            Assert.Equal(3.0 * Math.PI / 2.0, (double)Get(session, "ANGBASE"), 9);
        }

        [Fact]
        public void ATextIsAString_AndALayerThatIsNotThereIsRefused()
        {
            CadDocument document = Plan();
            CadCommandSession session = Start(document);

            Assert.Equal("0", Get(session, "CLAYER")); // AutoCAD 2021 (q6): (getvar "CLAYER") "0"
            Assert.True(Set(session, "CLAYER", "Steel"));
            Assert.Equal("Steel", Get(session, "CLAYER"));
            Assert.False(Set(session, "CLAYER", "NOPE")); // AutoCAD 2021 (q6): (setvar "CLAYER" "NOPE") rejected
            Assert.False(Set(session, "CLAYER", 5));
            Assert.Equal("Steel", Get(session, "CLAYER"));
        }

        [Fact]
        public void APointIsAListOfCoordinates_ThreeOrTwo_AndASetTakesTwoOrThree()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document);

            Assert.Equal(new[] { 0.0, 0.0, 0.0 }, (double[])Get(session, "LASTPOINT")); // AutoCAD 2021 (q6): (0.0 0.0 0.0)
            Assert.True(Set(session, "LASTPOINT", new[] { 1.0, 2.0 })); // AutoCAD 2021 (q6): (setvar "LASTPOINT" '(1 2)) taken
            Assert.Equal(new[] { 1.0, 2.0, 0.0 }, (double[])Get(session, "LASTPOINT")); // AutoCAD 2021 (q6): (1.0 2.0 0.0)
            Assert.True(Set(session, "LASTPOINT", new[] { 3.0, 4.0, 5.0 }));
            Assert.Equal(new[] { 3.0, 4.0, 0.0 }, (double[])Get(session, "LASTPOINT")); // ours: LASTPOINT keeps the plane
            Assert.False(Set(session, "LASTPOINT", new[] { 1.0 }));
            Assert.False(Set(session, "LASTPOINT", 5));
            Assert.Equal(new[] { 0.0, 0.0, 0.0 }, (double[])Get(session, "UCSORG")); // AutoCAD 2021 (q6): (0.0 0.0 0.0)
            Assert.Equal(2, ((double[])Get(session, "LIMMAX")).Length); // reference: AutoCAD's LIMMAX is a 2D point
            Assert.True(Set(session, "LIMMAX", new[] { 420.0, 297.0 }));
            Assert.Equal(new[] { 420.0, 297.0 }, (double[])Get(session, "LIMMAX"));
        }

        [Fact]
        public void ASwitchIs0Or1_AndAnythingElseIsRefused()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document);

            Assert.Equal(1, Get(session, "FILLMODE"));
            Assert.True(Set(session, "FILLMODE", 0));
            Assert.Equal(0, Get(session, "FILLMODE"));
            Assert.False(document.Database.Fillmode);
            Assert.False(Set(session, "FILLMODE", 2));
            Assert.False(Set(session, "FILLMODE", "ON"));
        }

        [Fact]
        public void AVariableReadOnly_GivesItsValueAndRefusesASet()
        {
            CadCommandSession session = Start(Blank());

            Assert.Equal("24.0", Get(session, "ACADVER")); // AutoCAD 2021 (q6): (getvar "ACADVER") "24.0"
            Assert.False(Set(session, "ACADVER", "x")); // AutoCAD 2021 (q6): (setvar "ACADVER" "x") rejected
            Assert.False(Set(session, "ACADVER", "25.0"));
            Assert.Equal("24.0", Get(session, "ACADVER"));
            Assert.False(Set(session, "EXTMIN", new[] { 1.0, 2.0, 3.0 }));
            Assert.False(Set(session, "LISPINIT", 0));
            Assert.Equal(1, Get(session, "LISPINIT")); // AutoCAD 2021 (q6): (getvar "LISPINIT") 1
        }

        [Fact]
        public void TheProductTellsARoutineWhichProgramRunsIt()
        {
            CadCommandSession session = Start(Blank());

            Assert.Equal("GeometryHelper.Cad", Get(session, "PRODUCT")); // ours: D56; AutoCAD 2021 (q6) gives "AutoCAD"
            Assert.NotEqual("AutoCAD", Get(session, "PRODUCT"));
            Assert.False(Set(session, "PRODUCT", "AutoCAD"));
        }

        [Fact]
        public void BlipmodeAndMenuechoAreKeptAndDoNothing()
        {
            CadCommandSession session = Start(Blank());

            Assert.Equal(0, Get(session, "BLIPMODE")); // AutoCAD 2021 (q6): (getvar "BLIPMODE") 0
            Assert.Equal(0, Get(session, "MENUECHO")); // AutoCAD 2021 (q6): (getvar "MENUECHO") 0
            Assert.True(Set(session, "BLIPMODE", 1));
            Assert.Equal(1, Get(session, "BLIPMODE"));
            Assert.False(Set(session, "BLIPMODE", 2));
            Assert.True(Set(session, "MENUECHO", 3));
            Assert.Equal(3, Get(session, "MENUECHO"));
        }

        [Fact]
        public void CmdactiveAndCmdnames_TellWhatRuns()
        {
            CadCommandSession session = Start(Blank());

            Assert.Equal(0, Get(session, "CMDACTIVE")); // AutoCAD 2021 (q6): (getvar "CMDACTIVE") 0
            Assert.Equal(string.Empty, Get(session, "CMDNAMES")); // AutoCAD 2021 (q6): (getvar "CMDNAMES") ""

            Run(session, "LINE");
            Assert.Equal(1, Get(session, "CMDACTIVE")); // AutoCAD documents 1 for a command running
            Assert.Equal("LINE", Get(session, "CMDNAMES"));

            Run(session, Esc);
            Assert.Equal(0, Get(session, "CMDACTIVE"));
            Assert.False(Set(session, "CMDACTIVE", 0));
            Assert.False(Set(session, "CMDNAMES", "LINE"));
        }

        [Fact]
        public void CmdactiveIs3WhileATransparentCommandRunsInsideAnother()
        {
            CadCommandSession session = Start(Blank());

            Run(session, "LINE", "0,0", "'DIST");
            Assert.True(session.IsTransparent);
            Assert.Equal(3, Get(session, "CMDACTIVE")); // AutoCAD documents the sum of 1 and 2
            Assert.Equal("DIST", Get(session, "CMDNAMES")); // ours: the transparent command alone; AutoCAD writes LINE'DIST
        }

        [Fact]
        public void TheAxesOfTheUcs_AreReadOnlyAndFollowTheSystem()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document);

            Assert.Equal(new[] { 1.0, 0.0, 0.0 }, (double[])Get(session, "UCSXDIR")); // AutoCAD 2021 (q6): (1.0 0.0 0.0)
            Assert.Equal(new[] { 0.0, 1.0, 0.0 }, (double[])Get(session, "UCSYDIR")); // AutoCAD 2021 (q9 dump): (0.0 1.0 0.0)

            Run(session, "UCS", "Z", "90");
            double[] x = (double[])Get(session, "UCSXDIR");
            double[] y = (double[])Get(session, "UCSYDIR");

            Assert.Equal(0.0, x[0], 9);
            Assert.Equal(1.0, x[1], 9);
            Assert.Equal(-1.0, y[0], 9);
            Assert.Equal(0.0, y[1], 9);
            Assert.False(Set(session, "UCSXDIR", new[] { 1.0, 0.0, 0.0 }));
        }

        [Fact]
        public void ASetOfADrawingVariable_IsOneStepOfUndo()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document);

            Assert.True(Set(session, "LUPREC", 2));
            Assert.True(Set(session, "AUPREC", 3));
            Assert.Equal(2, document.Database.Luprec);

            Assert.True(document.Undo());
            Assert.Equal(2, document.Database.Luprec);
            Assert.Equal(0, document.Database.Auprec);
            Assert.True(document.Undo());
            Assert.Equal(4, document.Database.Luprec);
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void ASetOfTheValueAVariableHasAlready_IsTakenAndMakesNoStep()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document);

            Assert.True(Set(session, "LUPREC", 4));
            Assert.True(Set(session, "LASTPOINT", new[] { 0.0, 0.0, 0.0 }));
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void EveryVariableOfTheRegistry_GivesAValueOfOneOfTheNeutralTypes_AndTakesItBackUnlessItNamesAStyleTheDrawingLacks()
        {
            CadCommandSession session = Start(BlankWithMLeaderStyle());
            var refused = new List<string>();

            foreach (CadSystemVariable variable in session.Registry.Variables)
            {
                object value = variable.ValueForLisp(session);

                Assert.True(
                    value is int || value is double || value is string || value is double[],
                    variable.Name + " gave " + (value?.GetType().Name ?? "null")
                );

                if (value is double[] point)
                {
                    Assert.InRange(point.Length, 2, 3);
                }

                // A variable that can be set takes its own value back.
                if (!variable.IsReadOnly && !(value is string text && text.Length == 0))
                {
                    if (!variable.SetFromLisp(session, value))
                    {
                        refused.Add(variable.Name);
                    }

                    session.CloseGroup();
                }
            }

            // A blank drawing has no multiline style, table style or the like until the command that needs one makes it, while the
            // variable already says the name of the standard one: it is written off here, not asked to be fixed.
            Assert.Equal(new[] { "CMLSTYLE", "CTABLESTYLE" }, refused.OrderBy(n => n, StringComparer.Ordinal));
        }
    }
}
