using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The name format of a dimensional constraint (CONSTRAINTNAMEFORMAT: 0 the name, 1 the value, 2 the name and the value):
    /// a new constraint shows the words the setting asks, each one keeps its own once changed, and the format of a drawing read
    /// back is that of the words its dimension shows.
    /// </summary>
    public class ConstraintNameFormatTests
    {
        private static CadCommandSession Start(CadDocument document, params object[] inputs)
        {
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            return Run(session, inputs);
        }

        private static CadDocument LineOfTen(int format, out CadCommandSession session)
        {
            CadDocument document = Blank();
            session = Start(document, "CONSTRAINTNAMEFORMAT", format.ToString());
            Run(session, "LINE", P(0, 0), P(10, 1), Enter, "DCHORIZONTAL", P(0, 0), P(10, 1), P(5, 4), Enter);
            return document;
        }

        private static DxfDimension DimensionOf(CadDocument document) =>
            All<DxfDimension>(document.ModelSpace).Single();

        [Theory]
        [InlineData(0, "d1")]
        [InlineData(1, "10.0000")]
        [InlineData(2, "d1=10.0000")]
        public void ANewConstraint_ShowsTheWordsOfTheNameFormatSet(int format, string words)
        {
            CadDocument document = LineOfTen(format, out _);

            Assert.Equal(words, DimensionOf(document).DimensionText);
            Assert.Equal(
                format,
                CadConstraints.Open(document, document.ModelSpace).Set.Dimensional.Single().NameFormat
            );
        }

        [Fact]
        public void TheFormatOfAFileRead_IsThatOfTheWordsItsDimensionShows()
        {
            foreach (int format in new[] { 0, 1, 2 })
            {
                CadDocument document = LineOfTen(format, out _);
                var again = CadDocument.From(DxfDatabase.Parse(document.Database.ToText()), "again.dxf");

                Assert.Equal(format, CadConstraints.Open(again, again.ModelSpace).Set.Dimensional.Single().NameFormat);
            }
        }

        [Fact]
        public void ANameFormatChanged_IsOneUndoStep_AndIsRefusedForADimensionThatIsNoConstraint()
        {
            CadDocument document = LineOfTen(2, out CadCommandSession session);
            string handle = DimensionOf(document).Handle;

            Assert.True(CadParameters.SetNameFormat(document, new[] { handle }, 0).Succeeded);
            Assert.Equal("d1", DimensionOf(document).DimensionText);
            Assert.Equal("d1", CadParameters.ConstraintNameOf(document, handle));

            CadEditResult same = CadParameters.SetNameFormat(document, new[] { handle }, 0);
            Assert.False(same.Succeeded);

            Run(session, "DIMLINEAR", P(0, 20), P(10, 20), P(5, 25));
            string plain = All<DxfDimension>(document.ModelSpace).Single(d => d.Handle != handle).Handle;
            Assert.Null(CadParameters.ConstraintNameOf(document, plain));
            Assert.False(CadParameters.SetNameFormat(document, new[] { plain }, 1).Succeeded);

            Assert.True(document.Undo());
            Assert.Equal("d1", DimensionOf(document).DimensionText);
            Assert.True(document.Undo());
            Assert.Equal("d1=10.0000", DimensionOf(document).DimensionText);
        }

        [Fact]
        public void ARenamedParameter_KeepsTheFormatOfItsDimension()
        {
            CadDocument document = LineOfTen(0, out CadCommandSession session);

            Run(session, "-PARAMETERS", "Rename", "d1", "width");

            Assert.Equal("width", DimensionOf(document).DimensionText);
        }
    }
}
