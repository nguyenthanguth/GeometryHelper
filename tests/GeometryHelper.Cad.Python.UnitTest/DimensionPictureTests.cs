using System;
using System.Linq;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.Cad.Python.UnitTest.PythonDrawings;

namespace GeometryHelper.Cad.Python.UnitTest
{
    /// <summary>
    /// The picture of a dimension a Python command adds or changes (D1675): the app draws it, as the remote server does and
    /// as DIM does, so the block of the dimension holds the lines, arrows and words of its measurement.
    /// </summary>
    public sealed class DimensionPictureTests : PythonHostTestBase
    {
        public DimensionPictureTests(PythonHostFixture fixture)
            : base(fixture) { }

        private static string[] Dimension(params string[] lines) =>
            new[]
            {
                UsesCad,
                "from GeometryHelper.Dxf import DxfRotatedDimension",
                "from GeometryHelper.Geometry import GeoPoint3",
                "d = cad.active_drawing()",
                "dim = DxfRotatedDimension(0.0, GeoPoint3(0, 0, 0), GeoPoint3(100, 0, 0), GeoPoint3(100, 20, 0), '', None)",
            }
                .Concat(lines)
                .ToArray();

        [Fact]
        public void ADimensionAddedFromPython_HasTheDrawnPictureOfItsMeasurement()
        {
            CadCommandSession session = NewSession();
            RunLines(session, Dimension("d.add(dim)", "d.commit()"));

            var made = Assert.IsType<DxfRotatedDimension>(
                session.Document.Database.ModelSpace.Single(e => !e.IsErased)
            );
            Assert.Equal(100.0, made.Measurement, 6);
            Assert.True(
                made.DimBlock.Count(e => !e.IsErased) >= 4,
                "lines, arrows and words: " + made.DimBlock.Count()
            );
            var words = made.DimBlock.OfType<DxfMText>().Single(m => !m.IsErased);
            Assert.Contains("100", words.Contents, StringComparison.Ordinal);
        }

        [Fact]
        public void ADimensionModifiedFromPython_IsDrawnAgainForItsNewMeasurement()
        {
            CadCommandSession session = NewSession();
            RunLines(
                session,
                Dimension(
                    "d.add(dim)",
                    "d.commit()",
                    "e = d.entities()[0]",
                    "e.net.XLine2Point = GeoPoint3(150, 0, 0)",
                    "d.modify(e)",
                    "d.commit()"
                )
            );

            var moved = Assert.IsType<DxfRotatedDimension>(
                session.Document.Database.ModelSpace.Single(e => !e.IsErased)
            );
            Assert.Equal(150.0, moved.Measurement, 6);
            var words = moved.DimBlock.OfType<DxfMText>().Single(m => !m.IsErased);
            Assert.Contains("150", words.Contents, StringComparison.Ordinal);
        }
    }
}
