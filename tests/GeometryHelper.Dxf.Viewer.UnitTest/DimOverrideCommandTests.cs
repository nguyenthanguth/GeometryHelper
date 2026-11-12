using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// DIMOVERRIDE (DOV): the prompts, the refusals and the answers of AutoCAD 2021's (probed in its console: DIMSCALE 2 and
    /// DIMTAD 1 gave "Enter new value for dimension variable &lt;1.0000&gt;:" and "&lt;0&gt;:", DIMTAD 7 "Requires an integer between 0
    /// and 4.", DIMASZ -3 "Value must be positive.", DIMJOGANG and DIMLTYPE 'Variable "..." not found.'), written as overrides
    /// on the dimensions, leaders and frames selected, each drawn anew; Clear overrides takes them away.
    /// </summary>
    public class DimOverrideCommandTests
    {
        private static CadDocument Metric() => CadDocument.From(CadTemplate.CreateMetric(), "metric.dxf");

        /// <summary>A linear dimension of 100 in a metric drawing, with its handle.</summary>
        private static string Dimension(CadDocument document)
        {
            Run(Session(document), "DLI", P(0, 0), P(100, 0), P(50, 20));
            return Assert.Single(All<DxfRotatedDimension>(document.ModelSpace)).Handle;
        }

        /// <summary>The groups the dimension's overrides name, in the order they are written.</summary>
        private static string Groups(DxfDimension dimension)
        {
            IReadOnlyList<DxfTypedValue> data = dimension.GetXDataForApplication("ACAD");

            if (data == null)
            {
                return string.Empty;
            }

            int open = data.ToList().FindIndex(v => v.TypeCode == 1002 && v.Value == "{");
            var groups = new List<string>();

            for (int i = open + 1; i < data.Count && data[i].TypeCode == 1070; i += 2)
            {
                groups.Add(data[i].Value.Trim());
            }

            return string.Join(" ", groups);
        }

        [Fact]
        public void TheCommandAsksInAutoCadsWords_FromTheNameToTheObjects()
        {
            CadDocument document = Metric();
            CadCommandSession session = Run(Session(document), "DIMOVERRIDE");
            Assert.Equal("Enter dimension variable name to override or [Clear overrides]:", session.PromptText);

            Run(session, "DIMSCALE");
            Assert.Equal("Enter new value for dimension variable <1.0000>:", session.PromptText);

            Run(session, "2");
            Assert.Equal("Enter dimension variable name to override:", session.PromptText);

            Run(session, "DIMTAD");
            Assert.Equal("Enter new value for dimension variable <1>:", session.PromptText);

            Run(session, "0", Enter);
            Assert.Equal("Select objects:", session.PromptText);
        }

        [Fact]
        public void TheAliasesDovAndDimover_StartTheCommand()
        {
            Assert.Equal(
                "Enter dimension variable name to override or [Clear overrides]:",
                Run(Session(Metric()), "DOV").PromptText
            );
            Assert.Equal(
                "Enter dimension variable name to override or [Clear overrides]:",
                Run(Session(Metric()), "DIMOVER").PromptText
            );
        }

        [Fact]
        public void DimscaleTwoAndDimtadZero_AreWrittenInTheOrderOfTheirGroups_AndTheDimensionIsDrawnByThem_InOneUndoStep()
        {
            CadDocument document = Metric();
            string handle = Dimension(document);
            DxfRotatedDimension before = Entity<DxfRotatedDimension>(document, handle);
            Assert.Equal(string.Empty, Groups(before));
            Assert.Equal(2.5, before.DimBlock.OfType<DxfMText>().Single().TextHeight, 9);

            // Typed out of the order of their groups: DIMTAD (77) before DIMSCALE (40).
            Run(Session(document), "DIMOVERRIDE", "DIMTAD", "0", "DIMSCALE", "2", Enter, new[] { handle }, Enter);

            DxfRotatedDimension dimension = Entity<DxfRotatedDimension>(document, handle);
            Assert.Equal("40 77", Groups(dimension));
            Assert.Equal(2.0, dimension.Dimscale);
            Assert.Equal(0, dimension.Dimtad);
            Assert.Equal(5.0, dimension.DimBlock.OfType<DxfMText>().Single().TextHeight, 9);
            Assert.Equal("DIMOVERRIDE", document.NextUndo.Name);

            Assert.True(document.Undo());
            DxfRotatedDimension undone = Entity<DxfRotatedDimension>(document, handle);
            Assert.Equal(string.Empty, Groups(undone));
            Assert.Equal(1.0, undone.Dimscale);
        }

        [Fact]
        public void AnUnknownName_IsRefusedInAutoCadsWords_AndTheNamesAskedAgainWithClear()
        {
            CadDocument document = Metric();
            CadCommandSession session = Run(Session(document), "DIMOVERRIDE", "XYZ");

            Assert.Contains("Variable \"XYZ\" not found.", session.History);
            Assert.Equal("Enter dimension variable name to override or [Clear overrides]:", session.PromptText);

            // The variables that came after R2004 are not found either, as AutoCAD 2021 says.
            foreach (string name in new[] { "DIMJOGANG", "DIMLTYPE", "DIMTFILL", "DIMFXL", "DIMARCSYM" })
            {
                Run(session, name);
                Assert.Contains("Variable \"" + name + "\" not found.", session.History);
            }
        }

        [Fact]
        public void AWholeNumberOutOfItsRange_IsRefusedWithItsLimits_AndAskedAgain()
        {
            CadDocument document = Metric();
            string handle = Dimension(document);
            CadCommandSession session = Run(Session(document), "DIMOVERRIDE", "DIMTAD", "7");

            Assert.Contains("Requires an integer between 0 and 4.", session.History);
            Assert.Equal("Enter new value for dimension variable <1>:", session.PromptText);

            Run(session, "2", "DIMDEC");
            Assert.Equal("Enter new value for dimension variable <2>:", session.PromptText);
            Run(session, "9");
            Assert.Contains("Requires an integer between 0 and 8.", session.History);
            Run(session, "1", Enter, new[] { handle }, Enter);
            Assert.Equal(1, Entity<DxfRotatedDimension>(document, handle).Dimdec);
        }

        [Fact]
        public void ASizeBelowNought_IsRefused_AndOneAboveIsWritten()
        {
            CadDocument document = Metric();
            string handle = Dimension(document);
            CadCommandSession session = Run(Session(document), "DIMOVERRIDE", "DIMASZ", "-3");

            Assert.Contains("Value must be positive.", session.History);
            Assert.Equal("Enter new value for dimension variable <2.5000>:", session.PromptText);

            Run(session, "4", Enter, new[] { handle }, Enter);
            Assert.Equal(4.0, Entity<DxfRotatedDimension>(document, handle).Dimasz);
        }

        [Fact]
        public void AFlagTakesOnOrOff_AndAnythingElseIsAnInvalidKeyword()
        {
            CadDocument document = Metric();
            string handle = Dimension(document);
            CadCommandSession session = Run(Session(document), "DIMOVERRIDE", "DIMSE1");
            Assert.Equal("Enter new value for dimension variable <Off>:", session.PromptText);

            Run(session, "x");
            Assert.Contains("Invalid option keyword.", session.History);
            Assert.Equal("Enter new value for dimension variable <Off>:", session.PromptText);

            Run(session, "ON", Enter, new[] { handle }, Enter);
            Assert.True(Entity<DxfRotatedDimension>(document, handle).Dimse1);
        }

        [Fact]
        public void EnterAtTheValue_TakesTheValueShown_AndWritesNoOverride()
        {
            CadDocument document = Metric();
            string handle = Dimension(document);

            Run(Session(document), "DIMOVERRIDE", "DIMSCALE", Enter, Enter, new[] { handle }, Enter);

            Assert.Equal(string.Empty, Groups(Entity<DxfRotatedDimension>(document, handle)));
        }

        [Fact]
        public void AColourTakesAnIndexOrAName_AndRefusesWhatIsNeither()
        {
            CadDocument document = Metric();
            string handle = Dimension(document);
            CadCommandSession session = Run(Session(document), "DIMOVERRIDE", "DIMCLRD", "x");

            Assert.Contains("Requires an integer value or option keyword.", session.History);

            Run(session, "red", Enter, new[] { handle }, Enter);
            Assert.Equal(1, Entity<DxfRotatedDimension>(document, handle).Dimclrd.ColorIndex);
        }

        [Fact]
        public void AnArrowheadByItsBlocksName_IsMadeInTheDrawing_AndAFullStopIsTheClosedFilledOne()
        {
            CadDocument document = Metric();
            string handle = Dimension(document);

            Run(Session(document), "DIMOVERRIDE", "DIMBLK", "_DOT", Enter, new[] { handle }, Enter);

            DxfBlockTableRecord dot = Entity<DxfRotatedDimension>(document, handle).Dimblk;
            Assert.NotNull(dot);
            Assert.Equal("_DOT", dot.Name.ToUpperInvariant());

            Run(Session(document), "DIMOVERRIDE", "DIMBLK", ".", Enter, new[] { handle }, Enter);
            Assert.Null(Entity<DxfRotatedDimension>(document, handle).Dimblk);
        }

        [Fact]
        public void ATextStyleTheDrawingHasNot_IsRefused_AndOneItHasIsWritten()
        {
            CadDocument document = Metric();
            document.Database.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = "Plan", FileName = "arial.ttf" });
            string handle = Dimension(document);
            CadCommandSession session = Run(Session(document), "DIMOVERRIDE", "DIMTXSTY", "Nosuch");

            Assert.Contains("Text style \"Nosuch\" not found.", session.History);

            Run(session, "Plan", Enter, new[] { handle }, Enter);
            Assert.Equal("Plan", Entity<DxfRotatedDimension>(document, handle).TextStyle.Name);
        }

        [Fact]
        public void ClearOverrides_TakesEveryOverrideOfTheDimensionsAway_AndDrawsThemByTheirStyleAgain()
        {
            CadDocument document = Metric();
            string handle = Dimension(document);
            Run(Session(document), "DIMOVERRIDE", "DIMSCALE", "2", Enter, new[] { handle }, Enter);
            Assert.Equal("40", Groups(Entity<DxfRotatedDimension>(document, handle)));

            CadCommandSession session = Run(Session(document), "DOV", "C");
            Assert.Equal("Select objects:", session.PromptText);
            Run(session, new[] { handle }, Enter);

            DxfRotatedDimension dimension = Entity<DxfRotatedDimension>(document, handle);
            Assert.Equal(string.Empty, Groups(dimension));
            Assert.Equal(2.5, dimension.DimBlock.OfType<DxfMText>().Single().TextHeight, 9);
            Assert.Equal("DIMOVERRIDE", document.NextUndo.Name);
        }

        [Fact]
        public void ObjectsThatAreNotDimensions_AreLeftAsTheyAre()
        {
            CadDocument document = Plan();
            string before = document.Database.ToText();

            Run(Session(document), "DIMOVERRIDE", "DIMSCALE", "2", Enter, new[] { "A1", "A3", "A6" }, Enter);

            Assert.Equal(before, document.Database.ToText());
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void ALeaderSelected_TakesTheOverrideToo()
        {
            CadDocument document = Metric();
            Run(Session(document), "LEADER", P(0, 0), P(10, 10), P(20, 10), Enter, "Hello", Enter);
            string handle = Assert.Single(All<DxfLeader>(document.ModelSpace)).Handle;

            Run(Session(document), "DIMOVERRIDE", "DIMASZ", "5", Enter, new[] { handle }, Enter);

            Assert.Equal(5.0, Entity<DxfLeader>(document, handle).Dimasz);
        }
    }
}
