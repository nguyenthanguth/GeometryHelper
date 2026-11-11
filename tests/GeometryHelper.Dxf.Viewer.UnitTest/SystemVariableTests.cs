using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// SETVAR and the system variables typed as commands, in AutoCAD 2021's words: each read and set where the presenter keeps
    /// it, a variable of the drawing as one undo step and written to the header as AutoCAD writes it, one of the session at once.
    /// </summary>
    public class SystemVariableTests
    {
        private static CadCommandSession Start(CadDocument document, params object[] inputs)
        {
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            return Run(session, inputs);
        }

        [Fact]
        public void SetvarAsksTheNameThenTheValue_AsOneUndoStep_AndOffersTheNameGivenLast()
        {
            CadDocument document = Blank();

            CadCommandSession session = Start(document, "SETVAR");
            Assert.Equal("Enter variable name or [?]:", session.PromptText);
            Run(session, "filletrad");
            Assert.Equal("Enter new value for FILLETRAD <10.0000>:", session.PromptText);
            Run(session, "5");

            Assert.False(session.IsRunning);
            Assert.Equal(5.0, document.Database.Filletrad);
            Assert.Equal("40=5", Header(document, "$FILLETRAD"));

            // The name given last is offered, and Enter takes it; Enter at the value keeps the value.
            Run(session, "SETVAR");
            Assert.Equal("Enter variable name or [?] <FILLETRAD>:", session.PromptText);
            Run(session, Enter);
            Assert.Equal("Enter new value for FILLETRAD <5.0000>:", session.PromptText);
            Run(session, Enter);
            Assert.Equal(5.0, document.Database.Filletrad);

            Assert.True(document.Undo());
            Assert.Equal(10.0, document.Database.Filletrad);
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void AVariablesNameTypedAlone_AsksItsValue_AndAWholeNumberOutOfRangeIsRefusedAndAskedAgain()
        {
            CadDocument document = Blank();

            CadCommandSession session = Start(document, "SORTENTS");
            Assert.Equal("Enter new value for SORTENTS <127>:", session.PromptText);
            Run(session, "200");

            Assert.Equal("Requires an integer between 0 and 127.", session.History.Last());
            Assert.Equal("Enter new value for SORTENTS <127>:", session.PromptText);
            Run(session, "3");

            Assert.False(session.IsRunning);
            Assert.Equal(3, document.Database.SortEnts);
            Assert.Equal("280=3", Header(document, "$SORTENTS"));
        }

        [Fact]
        public void ARealOfNoughtWhereItMustBeAboveNought_IsRefusedInAutoCadsWords()
        {
            CadDocument document = Blank();

            CadCommandSession session = Start(document, "TEXTSIZE");
            Assert.Equal("Enter new value for TEXTSIZE <2.5000>:", session.PromptText);
            Run(session, "0");
            Assert.Equal("Value must be positive and nonzero.", session.History.Last());
            Run(session, "3.5");

            Assert.Equal(3.5, document.Database.Textsize);

            // TEXT offers the height TEXTSIZE gives.
            Run(session, "TEXT", P(0, 0));
            Assert.Equal("Specify height <3.5000>:", session.PromptText);
        }

        [Fact]
        public void AVariableReadOnly_SaysItsValue_AndOneUnknownIsSaidSo()
        {
            CadDocument document = Blank();

            CadCommandSession session = Start(document, "EXTMIN");
            Assert.False(session.IsRunning);
            Assert.Equal("EXTMIN = " + CadCommandContext.Length(document.Database.Extmin.X) + "," + CadCommandContext.Length(document.Database.Extmin.Y) + "," + CadCommandContext.Length(document.Database.Extmin.Z) + " (read only)", session.History.Last());

            Run(session, "SETVAR", "DWGNAME");
            Assert.Equal("DWGNAME = \"blank.dxf\" (read only)", session.History.Last());

            Run(session, "SETVAR", "DIMSTYLE");
            Assert.Equal("DIMSTYLE = \"" + document.Database.Dimstyle + "\" (read only)", session.History.Last());

            Run(session, "SETVAR", "NOSUCHVAR");
            Assert.Equal("Unknown variable name.  Type SETVAR ? for a list of variables.", session.History.Last());
            Assert.False(document.IsModified);
        }

        [Fact]
        public void SetvarQuestionMark_ListsTheVariablesAPatternOfWildcardsMatches_InOrderOfTheirNames()
        {
            CadDocument document = Blank();

            CadCommandSession session = Start(document, "SETVAR", "?");
            Assert.Equal("Enter variable(s) to list <*>:", session.PromptText);
            int before = session.History.Count;
            Run(session, "CHAMFER?");

            Assert.Equal(new[] { "CHAMFERA          0.0000", "CHAMFERB          0.0000" }, session.History.Skip(before + 1));

            Run(session, "SETVAR", "?", Enter);
            Assert.Contains(session.History, l => l.StartsWith("EXTMAX ", StringComparison.Ordinal) && l.EndsWith("(read only)", StringComparison.Ordinal));
            Assert.Contains(session.History, l => l.StartsWith("PDMODE            0", StringComparison.Ordinal));
        }

        [Fact]
        public void HeaderVariablesAreWritten_AsAutoCadWritesThem_EachOneUndoStep()
        {
            CadDocument document = Plan();

            CadCommandSession session = Start(document, "SETVAR", "LTSCALE");
            Assert.Equal("Enter new value for LTSCALE <1.0000>:", session.PromptText);
            Run(session, "2", "PSLTSCALE", "0", "MIRRTEXT", "0", "CHAMFERA", "4", "CLAYER", "steel", "CELWEIGHT", "35");

            Assert.Equal("40=2", Header(document, "$LTSCALE"));
            Assert.Equal("70=0", Header(document, "$PSLTSCALE"));
            Assert.Equal("70=0", Header(document, "$MIRRTEXT"));
            Assert.Equal("40=4", Header(document, "$CHAMFERA"));
            Assert.Equal("8=Steel", Header(document, "$CLAYER"));
            Assert.Equal("370=35", Header(document, "$CELWEIGHT"));
            AssertReopens(document);

            for (int i = 0; i < 6; i++)
            {
                Assert.True(document.Undo());
            }

            Assert.False(document.CanUndo);
            Assert.Equal(1.0, document.Database.Ltscale);
            Assert.Equal("0", document.Database.Clayer);
        }

        [Fact]
        public void ALayerOrALineweightNotTaken_IsRefused_AndChangesNothing()
        {
            CadDocument document = Plan();

            CadCommandSession session = Start(document, "CLAYER", "NOPE");
            Assert.Equal("Cannot find layer \"NOPE\".", session.History.Last());

            Run(session, "CELWEIGHT", "24");
            Assert.StartsWith("Requires a lineweight", session.History.Last(), StringComparison.Ordinal);

            Run(session, "CELTYPE");
            Assert.Equal("Enter new value for CELTYPE <ByLayer>:", session.PromptText);
            Run(session, Esc, "SETVAR", "CELTYPE");
            Assert.Equal("Enter new value for CELTYPE <\"ByLayer\">:", session.PromptText);
            Run(session, "DASHED");
            Assert.Equal("Linetype DASHED not found. Use the Load option to load it.", session.History.Last());
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void VariablesOfTheSession_ChangeAtOnce_WithNoUndoStep()
        {
            CadDocument document = Blank();

            CadCommandSession session = Start(document, "PICKSTYLE", "0", "SELECTIONCYCLING", "1", "OFFSETDIST", "25");

            Assert.Equal(0, document.PickStyle);
            Assert.Equal(1, document.SelectionCycling);
            Assert.Equal(25.0, session.Remembered["offset.distance"]);
            Assert.False(document.CanUndo);

            // OFFSET offers the distance OFFSETDIST gives.
            Run(session, "OFFSET");
            Assert.EndsWith("<25.0000>:", session.PromptText, StringComparison.Ordinal);
            Run(session, Esc);

            // TILEMODE 0 shows a layout, 1 model space again.
            Run(session, "TILEMODE", "0");
            Assert.False(document.CurrentSpace.IsModel);
            Run(session, "TILEMODE", "1");
            Assert.True(document.CurrentSpace.IsModel);
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void TheVariablesOfTheDefaultRegistry_AreThoseListed_AndALtsAliasStillReachesLtscale()
        {
            CadCommandRegistry registry = CadCommandRegistry.CreateDefault();

            Assert.Equal(
                new[]
                {
                    "ATTDIA", "ATTREQ", "CANNOSCALE", "CDATE", "CECOLOR", "CELTSCALE", "CELTYPE", "CELWEIGHT", "CHAMFERA", "CHAMFERB", "CLAYER", "CMDDIA", "DATE", "DIMASSOC", "DIMSCALE", "DIMSTYLE",
                    "DWGNAME", "DWGPREFIX", "EXTMAX", "EXTMIN", "FILEDIA", "FILLETRAD", "IMAGEFRAME", "INSBASE", "LIMMAX", "LIMMIN", "LTSCALE", "LWDISPLAY", "MIRRTEXT",
                    "MSLTSCALE", "OFFSETDIST", "ORTHOMODE", "PDMODE", "PDSIZE", "PEDITACCEPT", "PICKADD", "PICKFIRST", "PICKSTYLE", "PLINETYPE", "PSLTSCALE", "REVCLOUDAPPROXARCLEN", "REVCLOUDARCVARIANCE", "REVCLOUDCREATEMODE", "SELECTIONCYCLING", "SORTENTS", "TEXTSIZE",
                    "TEXTSTYLE", "TILEMODE", "UCSNAME", "UCSORG",
                },
                registry.Variables.Select(v => v.Name));
            Assert.Equal("LTSCALE", registry.FindVariable("LTS").Name);
            Assert.Equal(176, registry.Commands.Count);
            Assert.NotNull(registry.Find("SETVAR"));
            Assert.Null(registry.Find("PDMODE"));
            Assert.True(registry.FindVariable("PDMODE").IsInDrawing);
            Assert.False(registry.FindVariable("PICKSTYLE").IsInDrawing);
            Assert.True(registry.FindVariable("EXTMIN").IsReadOnly);
        }

        [Fact]
        public void PickfirstZero_MakesACommandAskForItsObjects_WhatIsSelectedBeforeItLeftAside()
        {
            CadDocument document = Plan();
            document.Selection.Set(new[] { "A1" });

            // PICKFIRST 1, the default: MOVE takes the line selected and asks its base point.
            CadCommandSession session = Start(document, "MOVE");
            Assert.StartsWith("Specify base point", session.PromptText, StringComparison.Ordinal);
            Run(session, Esc);

            Run(session, "PICKFIRST");
            Assert.Equal("Enter new value for PICKFIRST <1>:", session.PromptText);
            Run(session, "0");
            Assert.Equal(0, document.PickFirst);
            Assert.False(document.CanUndo);

            // PICKFIRST 0: MOVE asks "Select objects:", and moves what is picked there, not the line selected before.
            document.Selection.Set(new[] { "A1" });
            Run(session, "MOVE");
            Assert.Equal("Select objects:", session.PromptText);
            Run(session, new[] { "A2" }, Enter, P(0, 0), P(10, 0));

            Near(new GeoPoint3(10, 0, 0), Entity<DxfLine>(document, "A2").StartPoint);
            Near(new GeoPoint3(0, 0, 0), Entity<DxfLine>(document, "A1").StartPoint);
        }

        [Fact]
        public void APointVariable_TakesAPointTyped_AndInsbaseKeepsItsZ()
        {
            CadDocument document = Blank();

            CadCommandSession session = Start(document, "INSBASE");
            Assert.Equal("Enter new value for INSBASE <0.0000,0.0000,0.0000>:", session.PromptText);
            Run(session, "100,50", "LIMMAX");
            Assert.Equal("Enter new value for LIMMAX <420.0000,297.0000>:", session.PromptText);
            Run(session, "841,594");

            Near(new GeoPoint3(100, 50, 0), document.Database.Insbase);
            Near(P(841, 594), document.Database.Limmax);
            Assert.True(document.Undo());
            Near(P(420, 297), document.Database.Limmax);
        }

        private static string Header(CadDocument document, string name)
            => string.Join(";", document.Database.GetHeader(name).Select(g => g.TypeCode + "=" + g.Value.Trim()));
    }
}
