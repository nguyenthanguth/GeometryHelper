using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The kinds of Properties row an extension or a plugin can make: Number, Length, Angle, Point, Color, Layer, Linetype
    /// and Lineweight, what each shows for its value, how each reads the text typed, and the words that refuse it. The setter
    /// of a row is never called with text that did not read.
    /// </summary>
    public class PropertyRowKindsTests
    {
        private const string ColourWords =
            "A colour is ByLayer, ByBlock, a name from Red to White, an index 1 to 255, or red, green and blue as 255,128,0.";

        /// <summary>What a setter was given, and what it answers: null for done, else the words of a refusal.</summary>
        private sealed class Calls<T>
        {
            public List<T> Given { get; } = new List<T>();

            public string Reply { get; set; }

            public string Take(T value)
            {
                Given.Add(value);
                return Reply;
            }
        }

        /// <summary>A blank drawing with LUNITS, LUPREC, AUNITS and AUPREC as given.</summary>
        private static CadDocument Units(int lunits = 2, int luprec = 4, int aunits = 0, int auprec = 0)
        {
            CadDocument document = Blank();
            document.Database.Lunits = lunits;
            document.Database.Luprec = luprec;
            document.Database.Aunits = aunits;
            document.Database.Auprec = auprec;
            return document;
        }

        [Fact]
        public void TheKindsAreTextChoiceCheckNumberLengthAngleColorLayerLinetypeAndLineweightInThatOrder()
        {
            Assert.Equal(
                new[]
                {
                    "Text",
                    "Choice",
                    "Check",
                    "Number",
                    "Length",
                    "Angle",
                    "Color",
                    "Layer",
                    "Linetype",
                    "Lineweight",
                },
                Enum.GetNames(typeof(CadPropertyKind))
            );
        }

        // ---- the rows that were there before ----

        [Fact]
        public void TheOldConstructorsMakeATextRowOrAChoiceRowWhereChoicesAreGiven()
        {
            var choices = new[] { "Top", "Middle" };

            Assert.Equal(CadPropertyKind.Text, new CadPropertyRow("Label", "v", false).Kind);
            Assert.Equal(CadPropertyKind.Text, new CadPropertyRow("Label", "v", true, null).Kind);
            Assert.Equal(CadPropertyKind.Choice, new CadPropertyRow("Label", "Top", false, choices).Kind);
            Assert.Equal(CadPropertyKind.Text, new CadPropertyRow("Label", "v", text => null).Kind);
            Assert.Equal(CadPropertyKind.Choice, new CadPropertyRow("Label", "Top", text => null, choices).Kind);
        }

        [Fact]
        public void ACheckRowWithASetterIsACheckAndOneWithoutIsAReadOnlyTextRowOfYesOrNo()
        {
            CadPropertyRow editable = CadPropertyRow.Check("Mark", true, text => null);
            CadPropertyRow shown = CadPropertyRow.Check("Mark", false, null);

            Assert.Equal(CadPropertyKind.Check, editable.Kind);
            Assert.True(editable.IsCheck);
            Assert.Equal("Yes", editable.Value);

            // D527: the same read-only text row as an extension's has always had.
            Assert.Equal(CadPropertyKind.Text, shown.Kind);
            Assert.False(shown.IsCheck);
            Assert.True(shown.IsReadOnly);
            Assert.Equal("No", shown.Value);
        }

        [Fact]
        public void ThePresentersOwnRowsOfALineAreOnlyTextOrChoiceRows()
        {
            CadDocument document = CadDocument.From(
                TestDrawings.Drawing(null, null, TestDrawings.Line(0, 0, 100, 0, handle: "A1")),
                "line.dxf"
            );

            CadSelectionInfo info = CadProperties.Describe(document, null, new[] { "A1" });

            IEnumerable<CadPropertyRow> rows = info.Groups.SelectMany(g => g.Rows);
            Assert.NotEmpty(rows);
            Assert.All(rows, r => Assert.Contains(r.Kind, new[] { CadPropertyKind.Text, CadPropertyKind.Choice }));
        }

        // ---- Number ----

        [Fact]
        public void ANumberShowsAsTypedAndReadsTheSameInTheInvariantCultureWhateverTheMachinesIs()
        {
            CultureInfo before = Thread.CurrentThread.CurrentCulture;
            var calls = new Calls<double>();

            try
            {
                Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
                CadPropertyRow row = CadPropertyRow.Number("Factor", 12.5, calls.Take);

                Assert.Equal("12.5", row.Value);
                Assert.Equal(CadPropertyKind.Number, row.Kind);
                Assert.False(row.IsReadOnly);
                Assert.Null(row.Setter("7.25"));
                Assert.Equal("Factor is a number.", row.Setter("7,25"));
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = before;
            }

            Assert.Equal(new[] { 7.25 }, calls.Given);
        }

        [Theory]
        [InlineData("abc")]
        [InlineData("")]
        [InlineData("1.2.3")]
        [InlineData("NaN")]
        [InlineData("Infinity")]
        [InlineData("12 mm")]
        public void NumberTextThatDoesNotReadIsRefusedByLabelAndTheSetterIsNotCalled(string typed)
        {
            var calls = new Calls<double>();
            CadPropertyRow row = CadPropertyRow.Number("Diameter", 12.5, calls.Take);

            Assert.Equal("Diameter is a number.", row.Setter(typed));

            Assert.Empty(calls.Given);
        }

        [Fact]
        public void ANumberWithSpacesAroundItIsReadAndAWholeNumberAndANegativeOneToo()
        {
            var calls = new Calls<double>();
            CadPropertyRow row = CadPropertyRow.Number("Count", 3, calls.Take);

            Assert.Equal("3", row.Value);
            Assert.Null(row.Setter("  12.5 "));
            Assert.Null(row.Setter("-4"));
            Assert.Null(row.Setter("2E2"));

            Assert.Equal(new[] { 12.5, -4.0, 200.0 }, calls.Given);
        }

        [Fact]
        public void ASettersWordsOfRefusalAreGivenBackAsTheyAreWrittenAndTheRowStaysEditable()
        {
            var calls = new Calls<double> { Reply = "Circle tools: a numbered circle is 6 to 60 across." };
            CadPropertyRow row = CadPropertyRow.Number("Diameter", 12.5, calls.Take);

            Assert.Equal("Circle tools: a numbered circle is 6 to 60 across.", row.Setter("100"));
            calls.Reply = null;
            Assert.Null(row.Setter("20"));

            Assert.Equal(new[] { 100.0, 20.0 }, calls.Given);
        }

        // ---- Length ----

        [Fact]
        public void ALengthInDecimalUnitsToFourPlacesShowsTwelvePointFiveAsTwelvePointFiveThousand()
        {
            var calls = new Calls<double>();
            CadDocument document = Units(lunits: 2, luprec: 4);

            CadPropertyRow row = CadPropertyRow.Length("Diameter", 12.5, document, calls.Take);

            Assert.Equal("12.5000", row.Value);
            Assert.Equal(CadPropertyKind.Length, row.Kind);
            Assert.Null(row.Setter("12.5"));
            Assert.Equal(new[] { 12.5 }, calls.Given);
        }

        [Fact]
        public void TheLengthFollowsTheDrawingsPrecisionWhenTheRowIsMade()
        {
            CadPropertyRow two = CadPropertyRow.Length("L", 12.5, Units(luprec: 2), null);
            CadPropertyRow zero = CadPropertyRow.Length("L", 12.5, Units(luprec: 0), null);

            Assert.Equal("12.50", two.Value);
            Assert.Equal("13", zero.Value);
        }

        [Fact]
        public void ALengthInArchitecturalUnitsIsWrittenAsFeetAndInchesAndReadsOneInchesTwoTyped()
        {
            var calls = new Calls<double>();
            CadDocument document = Units(lunits: 4, luprec: 4);

            CadPropertyRow row = CadPropertyRow.Length("Width", 14.0, document, calls.Take);

            Assert.Equal(CadUnits.Length(14.0, 4, 4), row.Value);
            Assert.Null(row.Setter("1'2\""));
            Assert.Null(row.Setter("1'-2\""));
            Assert.Null(row.Setter("14"));
            Assert.Equal(new[] { 14.0, 14.0, 14.0 }, calls.Given);
        }

        [Fact]
        public void FeetAndInchesTypedInDecimalUnitsAreRefusedAndSoIsText()
        {
            var calls = new Calls<double>();
            CadPropertyRow row = CadPropertyRow.Length("Width", 14.0, Units(lunits: 2), calls.Take);

            Assert.Equal("Width is a number.", row.Setter("1'2\""));
            Assert.Equal("Width is a number.", row.Setter("abc"));
            Assert.Equal("Width is a number.", row.Setter(""));
            Assert.Equal("Width is a number.", row.Setter("Infinity"));

            Assert.Empty(calls.Given);
        }

        [Fact]
        public void ALengthRowMadeWithoutADrawingIsRefused()
        {
            Assert.Throws<ArgumentNullException>(() => CadPropertyRow.Length("L", 1.0, null, null));
            Assert.Throws<ArgumentNullException>(() => CadPropertyRow.Angle("A", 1.0, null, null));
            Assert.Throws<ArgumentNullException>(() => CadPropertyRow.Point("P", new GeoPoint3(0, 0, 0), null, null));
        }

        // ---- Angle ----

        [Fact]
        public void AnAngleInDegreesToNoPlacesShowsAQuarterTurnAsNinetyAndReadsFortyFiveAsAnEighthOfATurn()
        {
            var calls = new Calls<double>();
            CadDocument document = Units(aunits: 0, auprec: 0);

            CadPropertyRow row = CadPropertyRow.Angle("Rotation", Math.PI / 2.0, document, calls.Take);

            Assert.Equal("90", row.Value);
            Assert.Equal(CadPropertyKind.Angle, row.Kind);
            Assert.Null(row.Setter("45"));
            Assert.Equal(Math.PI / 4.0, Assert.Single(calls.Given), 12);
        }

        [Fact]
        public void AnAngleTextThatDoesNotReadIsRefusedByLabelAndTheSetterIsNotCalled()
        {
            var calls = new Calls<double>();
            CadPropertyRow row = CadPropertyRow.Angle("Rotation", 0.0, Units(), calls.Take);

            Assert.Equal("Rotation is a number.", row.Setter("abc"));
            Assert.Equal("Rotation is a number.", row.Setter("1.2.3"));
            Assert.Equal("Rotation is a number.", row.Setter(""));

            Assert.Empty(calls.Given);
        }

        [Fact]
        public void AnAngleInDegreesMinutesAndSecondsIsWrittenAndReadInThem()
        {
            var calls = new Calls<double>();
            CadDocument document = Units(aunits: 1, auprec: 0);

            CadPropertyRow row = CadPropertyRow.Angle("Rotation", Math.PI / 2.0, document, calls.Take);

            Assert.Equal(CadUnits.Angle(Math.PI / 2.0, 1, 0), row.Value);
            Assert.Null(row.Setter(row.Value));
            Assert.Equal(Math.PI / 2.0, Assert.Single(calls.Given), 9);
        }

        // ---- Point ----

        [Fact]
        public void APointMakesThreeLengthRowsNamedByTheLabelWithXYAndZInThatOrder()
        {
            CadDocument document = Units(lunits: 2, luprec: 4);

            IReadOnlyList<CadPropertyRow> rows = CadPropertyRow.Point(
                "Center",
                new GeoPoint3(1.5, 2.5, 0.0),
                document,
                p => null
            );

            Assert.Equal(new[] { "Center X", "Center Y", "Center Z" }, rows.Select(r => r.Label));
            Assert.Equal(new[] { "1.5000", "2.5000", "0.0000" }, rows.Select(r => r.Value));
            Assert.All(rows, r => Assert.Equal(CadPropertyKind.Length, r.Kind));
        }

        [Fact]
        public void TypingOneCoordinateOfAPointGivesTheSetterThePointWithThatOneChangedAndTheOthersAsTheyWere()
        {
            var calls = new Calls<GeoPoint3>();
            IReadOnlyList<CadPropertyRow> rows = CadPropertyRow.Point(
                "Center",
                new GeoPoint3(1.5, 2.5, 4.0),
                Units(),
                calls.Take
            );

            Assert.Null(rows[0].Setter("10"));
            Assert.Null(rows[1].Setter("20"));
            Assert.Null(rows[2].Setter("30"));

            Assert.Equal(3, calls.Given.Count);
            Assert.True(calls.Given[0].IsEqualTo(new GeoPoint3(10, 2.5, 4), Tight));
            Assert.True(calls.Given[1].IsEqualTo(new GeoPoint3(1.5, 20, 4), Tight));
            Assert.True(calls.Given[2].IsEqualTo(new GeoPoint3(1.5, 2.5, 30), Tight));
        }

        [Fact]
        public void ACoordinateThatDoesNotReadIsRefusedNamingItAndTheSetterIsNotCalled()
        {
            var calls = new Calls<GeoPoint3>();
            IReadOnlyList<CadPropertyRow> rows = CadPropertyRow.Point(
                "Center",
                new GeoPoint3(1, 2, 3),
                Units(),
                calls.Take
            );

            Assert.Equal("Center X is a number.", rows[0].Setter("abc"));
            Assert.Equal("Center Y is a number.", rows[1].Setter("abc"));
            Assert.Equal("Center Z is a number.", rows[2].Setter("abc"));

            Assert.Empty(calls.Given);
        }

        [Fact]
        public void EachCoordinateRowKeepsThePointItWasMadeWithAfterAnotherRowIsSet()
        {
            // The three rows hold the point as it was when they were made; the window makes them again after a change,
            // so the Y row, set after the X row, still gives X as 1 and not as the 10 just typed.
            var calls = new Calls<GeoPoint3>();
            IReadOnlyList<CadPropertyRow> rows = CadPropertyRow.Point(
                "Center",
                new GeoPoint3(1, 2, 3),
                Units(),
                calls.Take
            );

            rows[0].Setter("10");
            rows[1].Setter("20");

            Assert.True(calls.Given[1].IsEqualTo(new GeoPoint3(1, 20, 3), Tight));
        }

        // ---- read-only ----

        [Fact]
        public void EveryKindMadeWithoutASetterIsAReadOnlyRowWithNoSetter()
        {
            CadDocument document = Units();
            var rows = new List<CadPropertyRow>
            {
                CadPropertyRow.Number("n", 1.0, null),
                CadPropertyRow.Length("l", 1.0, document, null),
                CadPropertyRow.Angle("a", 1.0, document, null),
                CadPropertyRow.Color("c", DxfColor.ByLayer, null),
                CadPropertyRow.Layer("y", "0", null),
                CadPropertyRow.Linetype("t", "Continuous", null),
                CadPropertyRow.Lineweight("w", DxfLineWeight.ByLayer, null),
            };
            rows.AddRange(CadPropertyRow.Point("p", new GeoPoint3(1, 2, 3), document, null));

            Assert.Equal(10, rows.Count);
            Assert.All(rows, r => Assert.True(r.IsReadOnly));
            Assert.All(rows, r => Assert.Null(r.Setter));
        }

        [Fact]
        public void TheKindsOfTheRowsEachFactoryMakesAreTheirOwn()
        {
            CadDocument document = Units();

            Assert.Equal(CadPropertyKind.Number, CadPropertyRow.Number("n", 1.0, null).Kind);
            Assert.Equal(CadPropertyKind.Color, CadPropertyRow.Color("c", DxfColor.ByLayer, null).Kind);
            Assert.Equal(CadPropertyKind.Layer, CadPropertyRow.Layer("y", "0", null).Kind);
            Assert.Equal(CadPropertyKind.Linetype, CadPropertyRow.Linetype("t", "Continuous", null).Kind);
            Assert.Equal(CadPropertyKind.Lineweight, CadPropertyRow.Lineweight("w", DxfLineWeight.ByLayer, null).Kind);
            Assert.Equal(CadPropertyKind.Angle, CadPropertyRow.Angle("a", 1.0, document, null).Kind);
        }

        // ---- Color ----

        [Fact]
        public void AColourIsWrittenAsThePaletteWritesOne()
        {
            Assert.Equal("ByLayer", CadPropertyRow.Color("c", DxfColor.ByLayer, null).Value);
            Assert.Equal("ByBlock", CadPropertyRow.Color("c", DxfColor.ByBlock, null).Value);
            Assert.Equal(
                "Red",
                CadPropertyRow.Color("c", DxfColor.FromColorIndex(DxfColorMethod.ByAci, 1), null).Value
            );
            Assert.Equal(
                "40",
                CadPropertyRow.Color("c", DxfColor.FromColorIndex(DxfColorMethod.ByAci, 40), null).Value
            );
            Assert.Equal("255,128,0", CadPropertyRow.Color("c", DxfColor.FromRgb(255, 128, 0), null).Value);
        }

        [Fact]
        public void TheNameRedIsIndexOneAndTwoFiftyFiveOneTwentyEightNoughtIsATrueColour()
        {
            var calls = new Calls<DxfColor>();
            CadPropertyRow row = CadPropertyRow.Color("Mark color", DxfColor.ByLayer, calls.Take);

            Assert.Null(row.Setter("Red"));
            Assert.Null(row.Setter("255,128,0"));
            Assert.Null(row.Setter("ByBlock"));
            Assert.Null(row.Setter("5"));
            Assert.Null(row.Setter("bylayer"));

            Assert.Equal(5, calls.Given.Count);
            Assert.True(calls.Given[0].IsByAci);
            Assert.Equal(1, calls.Given[0].ColorIndex);
            Assert.True(calls.Given[1].IsByColor);
            Assert.Equal(
                new byte[] { 255, 128, 0 },
                new[] { calls.Given[1].Red, calls.Given[1].Green, calls.Given[1].Blue }
            );
            Assert.True(calls.Given[2].IsByBlock);
            Assert.True(calls.Given[3].IsByAci);
            Assert.Equal(5, calls.Given[3].ColorIndex);
            Assert.True(calls.Given[4].IsByLayer);
        }

        [Theory]
        [InlineData("purple", ColourWords)]
        [InlineData("", ColourWords)]
        [InlineData("1,2", ColourWords)]
        [InlineData("256,0,0", ColourWords)]
        [InlineData("300", "A colour's index is 1 to 255.")]
        [InlineData("0", "A colour's index is 1 to 255.")]
        [InlineData("-3", "A colour's index is 1 to 255.")]
        public void AColourTextThatDoesNotReadIsRefusedWithThePalettesWordsAndTheSetterIsNotCalled(
            string typed,
            string words
        )
        {
            var calls = new Calls<DxfColor>();
            CadPropertyRow row = CadPropertyRow.Color("Mark color", DxfColor.ByLayer, calls.Take);

            Assert.Equal(words, row.Setter(typed));

            Assert.Empty(calls.Given);
        }

        [Fact]
        public void AColourSettersOwnRefusalIsGivenBackUnchanged()
        {
            var calls = new Calls<DxfColor> { Reply = "Circle tools: not white on white." };
            CadPropertyRow row = CadPropertyRow.Color("Mark color", DxfColor.ByLayer, calls.Take);

            Assert.Equal("Circle tools: not white on white.", row.Setter("White"));
            Assert.Single(calls.Given);
        }

        // ---- Layer, Linetype, Lineweight ----

        [Fact]
        public void ALayerAndALinetypeShowTheirNameAndGiveTheSetterTheTextTypedUnchanged()
        {
            var layers = new Calls<string> { Reply = "Circle tools: there is no layer Walls." };
            var types = new Calls<string>();
            CadPropertyRow layer = CadPropertyRow.Layer("Mark layer", "Mark", layers.Take);
            CadPropertyRow type = CadPropertyRow.Linetype("Mark linetype", "Continuous", types.Take);

            Assert.Equal("Mark", layer.Value);
            Assert.Equal("Continuous", type.Value);
            Assert.Equal("Circle tools: there is no layer Walls.", layer.Setter("Walls"));
            Assert.Null(type.Setter("DASHED"));

            Assert.Equal(new[] { "Walls" }, layers.Given);
            Assert.Equal(new[] { "DASHED" }, types.Given);
        }

        [Fact]
        public void ALineweightIsWrittenAsThePaletteWritesOneAndReadsTheSameWithOrWithoutMm()
        {
            var calls = new Calls<DxfLineWeight>();
            CadPropertyRow row = CadPropertyRow.Lineweight("Lineweight", DxfLineWeight.LineWeight025, calls.Take);

            Assert.Equal("0.25 mm", row.Value);
            Assert.Null(row.Setter("0.35 mm"));
            Assert.Null(row.Setter("0.35"));
            Assert.Null(row.Setter("ByBlock"));
            Assert.Null(row.Setter("Default"));
            Assert.Null(row.Setter("ByLayer"));

            Assert.Equal(
                new[]
                {
                    DxfLineWeight.LineWeight035,
                    DxfLineWeight.LineWeight035,
                    DxfLineWeight.ByBlock,
                    DxfLineWeight.ByLineWeightDefault,
                    DxfLineWeight.ByLayer,
                },
                calls.Given
            );
        }

        [Theory]
        [InlineData("0.37 mm")]
        [InlineData("thick")]
        [InlineData("")]
        [InlineData("-0.25")]
        public void ALineweightAutoCADHasNotIsRefusedWithThePalettesWordsAndTheSetterIsNotCalled(string typed)
        {
            var calls = new Calls<DxfLineWeight>();
            CadPropertyRow row = CadPropertyRow.Lineweight("Lineweight", DxfLineWeight.ByLayer, calls.Take);

            Assert.Equal(
                "A lineweight is ByLayer, ByBlock, Default, or one of AutoCAD's widths in millimetres, as 0.25 mm.",
                row.Setter(typed)
            );

            Assert.Empty(calls.Given);
        }
    }
}
