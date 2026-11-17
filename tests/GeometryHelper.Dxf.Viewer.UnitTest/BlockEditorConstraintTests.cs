using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// The constraints in the Block Editor, which the Geometric and Dimensional panels of its tab start: a geometric or a
    /// dimensional constraint made on the entities of the block, kept in the block, saved with it and read again.
    /// </summary>
    public class BlockEditorConstraintTests
    {
        /// <summary>A metric drawing with the block PLATE, a line from (0, 0) to (100, 10), placed once.</summary>
        private static CadDocument Plates()
        {
            DxfDatabase database = CadTemplate.CreateMetric();
            var plate = new DxfBlockTableRecord { Name = "PLATE", Origin = new GeoPoint3(0, 0, 0) };
            database.BlockTable.Add(plate);
            plate.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 10, 0)));
            database.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(0, 0, 0), plate));
            return CadDocument.From(database, "plates.dxf");
        }

        private static CadCommandSession Start(CadDocument document, params object[] inputs)
        {
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            return Run(session, inputs);
        }

        [Fact]
        public void AHorizontalConstraintInTheBlockEditor_LevelsTheLine_AndStaysInTheBlockWhenSaved()
        {
            CadDocument document = Plates();
            CadCommandSession session = Start(document, "-BEDIT", "PLATE");

            Run(session, "GCHORIZONTAL", P(50, 5));

            DxfLine line = document.Database.BlockTable["PLATE"].OfType<DxfLine>().Single();
            Assert.Equal(line.StartPoint.Y, line.EndPoint.Y, 9);
            Assert.Single(CadConstraints.Open(document, document.CurrentSpace).Set.Geometric);

            Run(session, "BSAVE", "BCLOSE");
            Run(session, "-BEDIT", "PLATE");

            Assert.Single(CadConstraints.Open(document, document.CurrentSpace).Set.Geometric);
        }

        [Fact]
        public void ADimensionalConstraintInTheBlockEditor_MakesAParameter_ThatMovesTheLine()
        {
            CadDocument document = Plates();
            CadCommandSession session = Start(document, "-BEDIT", "PLATE");

            Run(session, "DCHORIZONTAL", P(0, 0), P(100, 10), P(50, 30), Enter);
            CadConstraintSet set = CadConstraints.Open(document, document.CurrentSpace).Set;
            Assert.Equal("d1", Assert.Single(set.Dimensional).Name);

            Run(session, "-PARAMETERS", "Edit", "d1", "250");

            DxfLine line = document.Database.BlockTable["PLATE"].OfType<DxfLine>().Single();
            Assert.Equal(250.0, line.EndPoint.X - line.StartPoint.X, 6);
        }
    }
}
