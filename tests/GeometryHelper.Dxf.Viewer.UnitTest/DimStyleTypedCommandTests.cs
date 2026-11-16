using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// -DIMSTYLE and DIMUPDATE, as AutoCAD 2021 asks them (probed in its console): the current style and its overrides written
    /// first, then the option ANnotative, Save, Restore, STatus, Variables, Apply or ?; Save keeps the variables, overrides and
    /// all, as a style that becomes current; Apply and DIMUPDATE give the current style and overrides to the dimensions
    /// selected.
    /// </summary>
    public class DimStyleTypedCommandTests
    {
        private const string Options =
            "Enter a dimension style option [ANnotative/Save/Restore/STatus/Variables/Apply/?] <Restore>:";

        private static CadDocument Metric() => CadDocument.From(CadTemplate.CreateMetric(), "metric.dxf");

        /// <summary>A linear dimension of 100 in the drawing, with its handle.</summary>
        private static string Dimension(CadDocument document)
        {
            Run(Session(document), "DLI", P(0, 0), P(100, 0), P(50, 20));
            return Assert.Single(All<DxfRotatedDimension>(document.ModelSpace)).Handle;
        }

        [Fact]
        public void TheCommandWritesTheCurrentStyle_AndAsksTheOptionsInAutoCadsWords()
        {
            CadDocument document = Metric();

            CadCommandSession session = Run(Session(document), "-DIMSTYLE");

            Assert.Contains("Current dimension style: ISO-25   Annotative: No", session.History);
            Assert.DoesNotContain("Current dimension overrides:", session.History);
            Assert.Equal(Options, session.PromptText);
        }

        [Fact]
        public void AVariableTypedThatDiffersFromTheStyle_IsListedAsAnOverride()
        {
            CadDocument document = Metric();
            CadCommandSession session = Session(document);
            document.Database.Dimtxt = 5.0;

            Run(session, "-DIMSTYLE");

            int at = session.History.ToList().IndexOf("Current dimension overrides:");
            Assert.True(at > 0);
            Assert.Equal(" DIMTXT      5.0000", session.History[at + 1]);
            Assert.Equal(at + 2, session.History.Count);
        }

        [Fact]
        public void Save_KeepsTheVariablesWithTheirOverridesAsAStyle_ThatBecomesCurrent_InOneUndoStep()
        {
            CadDocument document = Metric();
            CadCommandSession session = Session(document);
            document.Database.Dimtxt = 5.0;

            Run(session, "-DIMSTYLE", "Save");
            Assert.Equal("Enter name for new dimension style or [?]:", session.PromptText);
            Run(session, "Plate");

            Assert.False(session.IsRunning);
            DxfDimStyleTableRecord plate = document.Database.DimStyleTable["Plate"];
            Assert.Equal(5.0, plate.Dimtxt);
            Assert.Equal(2.5, document.Database.DimStyleTable["ISO-25"].Dimtxt);
            Assert.Equal("Plate", CadDimStyleEditor.Current(document.Database));

            CadCommandSession again = Run(Session(document), "-DIMSTYLE");
            Assert.Contains("Current dimension style: Plate   Annotative: No", again.History);
            Assert.DoesNotContain("Current dimension overrides:", again.History);

            Run(session, "U");
            Assert.False(document.Database.DimStyleTable.Has("Plate"));
            Assert.Equal("ISO-25", CadDimStyleEditor.Current(document.Database));
        }

        [Fact]
        public void ANameTheDrawingHas_AsksToRedefineIt_AndNoAsksTheNameAgain()
        {
            CadDocument document = Metric();
            CadCommandSession session = Session(document);
            document.Database.Dimtxt = 5.0;

            Run(session, "-DIMSTYLE", "Save", "ISO-25");
            Assert.Equal("That name is already in use, redefine it? <N>:", session.PromptText);

            Run(session, "N");
            Assert.Equal("Enter name for new dimension style or [?]:", session.PromptText);
            Assert.Equal(2.5, document.Database.DimStyleTable["ISO-25"].Dimtxt);

            Run(session, "ISO-25", "Y");
            Assert.Equal(5.0, document.Database.DimStyleTable["ISO-25"].Dimtxt);
            Assert.Equal("ISO-25", CadDimStyleEditor.Current(document.Database));
        }

        [Fact]
        public void ANameWithACharacterAStyleCannotHave_IsRefused_AndAskedAgain()
        {
            CadCommandSession session = Run(Session(Metric()), "-DIMSTYLE", "Save", "a:b");

            Assert.Equal("Invalid style name.", session.History.Last());
            Assert.Equal("Enter name for new dimension style or [?]:", session.PromptText);
        }

        [Fact]
        public void Annotative_SavesAStyleThatIsAnnotative_AndAsksTheOptionAgain()
        {
            CadDocument document = Metric();

            CadCommandSession session = Run(Session(document), "-DIMSTYLE", "ANnotative");
            Assert.Equal("Create annotative dimension style [Yes/No] <Yes>:", session.PromptText);

            Run(session, Enter, "Ann");
            Assert.Equal(DxfAnnotativeStates.True, document.Database.DimStyleTable["Ann"].Annotative);
            Assert.Equal("Ann", CadDimStyleEditor.Current(document.Database));
            Assert.Equal(Options, session.PromptText);

            Run(session, "AN", "No", "Plain");
            Assert.NotEqual(DxfAnnotativeStates.True, document.Database.DimStyleTable["Plain"].Annotative);

            Run(session, "?", "*");
            Assert.Contains("Named dimension styles:", session.History);
            Assert.Contains("  Ann".PadRight(34) + "Annotative", session.History);
            Assert.Contains("  Plain", session.History);
        }

        [Fact]
        public void Restore_MakesAStyleCurrent_TheHeaderTakingItsVariables_AndSaysAnUnknownOne()
        {
            CadDocument document = Metric();
            CadCommandSession session = Session(document);
            document.Database.Dimtxt = 5.0;
            Run(session, "-DIMSTYLE", "Save", "Plate");
            Run(session, "-DIMSTYLE", "Restore");
            Assert.Equal("Enter a dimension style name, [?] or <select dimension>:", session.PromptText);

            Run(session, "NOPE");
            Assert.Equal("Unknown dimension style.", session.History[session.History.Count - 2]);

            Run(session, "-DIMSTYLE", "Restore", "ISO-25");
            Assert.Equal("ISO-25", CadDimStyleEditor.Current(document.Database));
            Assert.Equal(2.5, document.Database.Dimtxt);
            Assert.Equal("Current dimension style: ISO-25   Annotative: No", session.History.Last());
        }

        [Fact]
        public void RestoreWithEnter_TakesTheStyleAndTheOverridesOfADimensionPicked()
        {
            CadDocument document = Metric();
            CadCommandSession session = Session(document);
            string handle = Dimension(document);
            Run(session, "DIMOVERRIDE", "DIMTXT", "7", Enter, new[] { handle }, Enter);
            Run(session, "-DIMSTYLE", "Save", "Other");
            Assert.Equal("Other", CadDimStyleEditor.Current(document.Database));

            Run(session, "-DIMSTYLE", "Restore", Enter);
            Assert.Equal("Select dimension:", session.PromptText);
            Run(session, (object)new[] { handle });

            Assert.Equal("ISO-25", CadDimStyleEditor.Current(document.Database));
            Assert.Equal(7.0, document.Database.Dimtxt);
            Assert.Contains("Current dimension overrides:", session.History);
            Assert.Equal(" DIMTXT      7.0000", session.History.Last());
        }

        [Fact]
        public void Status_ListsEveryVariable_NameValueAndWhatItIs_InAlphabeticalOrder()
        {
            CadDocument document = Metric();
            CadCommandSession session = Session(document);
            document.Database.Dimtxt = 5.0;

            Run(session, "-DIMSTYLE", "STatus");

            Assert.Contains("DIMSTYLE    ISO-25               Current dimension style (read-only)", session.History);
            Assert.Contains("DIMTXT      5.0000               Text height", session.History);
            Assert.Contains("DIMASZ      2.5000               Arrow size", session.History);
            Assert.Contains("DIMBLK      ClosedFilled         Arrow block name", session.History);
            Assert.Contains("DIMTIH      Off                  Text inside extensions is horizontal", session.History);
            Assert.Contains("DIMFXL      1.0000               Fixed Extension Line", session.History);
            Assert.Contains("DIMTXTDIRECTIONOff                  Dimension text direction", session.History);
            List<string> names = session
                .History.Where(l =>
                    l.StartsWith("DIM") && l.Length > 12 && !l.StartsWith("DIMSTYLE") && !l.StartsWith("DIMASO")
                )
                .Select(l => l.Substring(0, 12).Trim())
                .ToList();
            Assert.True(names.Count > 60);
            Assert.Equal(names.OrderBy(n => n, System.StringComparer.Ordinal).ToList(), names);
        }

        [Fact]
        public void VariablesOfAStyle_ListsItsOwnValues_NotTheOverrides()
        {
            CadDocument document = Metric();
            CadCommandSession session = Session(document);
            document.Database.Dimtxt = 5.0;

            Run(session, "-DIMSTYLE", "Variables", "ISO-25");

            Assert.Contains("DIMTXT      2.5000               Text height", session.History);
        }

        [Fact]
        public void VariablesWithEnter_ShowsTheStyleOfADimensionPickedAndItsOverrides()
        {
            CadDocument document = Metric();
            CadCommandSession session = Session(document);
            string handle = Dimension(document);
            Run(session, "DIMOVERRIDE", "DIMTXT", "7", Enter, new[] { handle }, Enter);

            Run(session, "-DIMSTYLE", "Variables", Enter, new[] { handle });

            Assert.Equal("Dimension style: ISO-25", session.History[session.History.Count - 2]);
            Assert.Equal(" DIMTXT      7.0000", session.History.Last());
        }

        [Fact]
        public void Apply_GivesTheCurrentStyleAndOverridesToTheDimensionsSelected_AsOneUndoStep()
        {
            CadDocument document = Metric();
            CadCommandSession session = Session(document);
            string handle = Dimension(document);
            document.Database.Dimtxt = 6.0;
            Assert.Equal(2.5, Entity<DxfRotatedDimension>(document, handle).Dimtxt);

            Run(session, "-DIMSTYLE", "Apply");
            Assert.Equal("Select objects:", session.PromptText);
            Run(session, new[] { handle }, Enter);

            DxfRotatedDimension dimension = Entity<DxfRotatedDimension>(document, handle);
            Assert.Equal(6.0, dimension.Dimtxt);
            Assert.Equal(6.0, dimension.DimBlock.OfType<DxfMText>().Single().TextHeight, 9);
            Assert.Equal("ISO-25", dimension.DimensionStyleName);

            Run(session, "U");
            Assert.Equal(2.5, Entity<DxfRotatedDimension>(document, handle).Dimtxt);
        }

        [Fact]
        public void DimUpdate_AppliesTheCurrentStyleToTheDimensionsSelected_AnotherStyleToo()
        {
            CadDocument document = Metric();
            CadCommandSession session = Session(document);
            string handle = Dimension(document);
            document.Database.Dimtxt = 6.0;
            Run(session, "-DIMSTYLE", "Save", "Plate");
            Assert.Equal("ISO-25", Entity<DxfRotatedDimension>(document, handle).DimensionStyleName);

            Run(session, "DIMUPDATE", new[] { handle }, Enter);

            DxfRotatedDimension dimension = Entity<DxfRotatedDimension>(document, handle);
            Assert.Equal("Plate", dimension.DimensionStyleName);
            Assert.Equal(6.0, dimension.Dimtxt);
            Assert.Equal(6.0, dimension.DimBlock.OfType<DxfMText>().Single().TextHeight, 9);
        }

        [Fact]
        public void TheQuestionMarkListsTheStylesThatMatch_AndSaysWhenNoneDoes()
        {
            CadDocument document = Metric();

            CadCommandSession session = Run(Session(document), "-DIMSTYLE", "?");
            Assert.Equal("Enter dimension style(s) to list <*>:", session.PromptText);

            Run(session, "ISO*");
            Assert.Contains("Named dimension styles:", session.History);
            Assert.Contains("  ISO-25", session.History);

            Run(session, "-DIMSTYLE", "?", "QQ*");
            Assert.Equal("  No matching dimension styles found.", session.History.Last());
        }
    }
}
