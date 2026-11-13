using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// The snap Intersection on a feature control frame, as AutoCAD 2021's console gave it (13 November, a frame of
    /// <c>{\Fgdt;j}%%v{\Fgdt;n}0.1{\Fgdt;m}%%vA</c> at DIMTXT 0.18 crossed by a line 0.05 above its middle, and one at x 0.3):
    /// a line through the frame meets the sides of the rows, whose top and bottom it gave at (0.3, 0.18) and (0.3, -0.18) and
    /// whose left side at (0, 0.05), and meets no wall between cells (none at (0.45, 0.05), where the wall is).
    /// </summary>
    public class ToleranceFollowUps3Tests
    {
        /// <summary>A metric drawing (ISO-25: DIMTXT 2.5), its style's DIMTAD nought.</summary>
        private static CadDocument Metric()
        {
            DxfDatabase database = CadTemplate.CreateMetric();
            database.DimStyleTable[database.Dimstyle].Dimtad = 0;
            database.Dimtad = 0;
            return CadDocument.From(database, "metric.dxf");
        }

        private static CadSnapResult SnapAt(CadDocument document, CadSnapModes modes, GeoPoint2 cursor) =>
            new ObjectSnap(Tight) { Modes = modes }.Snap(document.GetPickIndex(document.ModelSpace), cursor, 0.2);

        /// <summary>
        /// The frame of two rows 5 high, <c>{\Fgdt;c}</c> (7.678571 wide) over <c>{\Fgdt;j}%%v{\Fgdt;c}</c> (13.928571 wide, a
        /// wall at x 6.25), from (0, 0), and a line along y -5 and one along x 3 across it.
        /// </summary>
        private static CadDocument Crossed()
        {
            CadDocument document = Metric();
            document.ApplyEdit("frame", null, s =>
            {
                var frame = new DxfTolerance
                {
                    Text = "{\\Fgdt;c}\n{\\Fgdt;j}%%v{\\Fgdt;c}",
                    Location = new GeoPoint3(0, 0, 0),
                };
                s.Append(document.ModelSpace, frame);
                frame.DimensionStyleName = s.Database.Dimstyle;
                s.Append(document.ModelSpace, new DxfLine(new GeoPoint3(-5, -5, 0), new GeoPoint3(25, -5, 0)));
                s.Append(document.ModelSpace, new DxfLine(new GeoPoint3(3, -12, 0), new GeoPoint3(3, 8, 0)));
            });
            return document;
        }

        [Fact]
        public void Intersection_OfALineAndTheSidesOfTheRows_IsGiven()
        {
            CadDocument document = Crossed();

            CadSnapResult left = SnapAt(document, CadSnapModes.Intersection, P(0.1, -4.95));
            Assert.Equal(CadSnapKind.Intersection, left.Kind);
            Assert.Equal(0.0, left.Point.X, 6);
            Assert.Equal(-5.0, left.Point.Y, 6);

            CadSnapResult top = SnapAt(document, CadSnapModes.Intersection, P(3.05, 2.45));
            Assert.Equal(CadSnapKind.Intersection, top.Kind);
            Assert.Equal(3.0, top.Point.X, 6);
            Assert.Equal(2.5, top.Point.Y, 6);

            CadSnapResult bottom = SnapAt(document, CadSnapModes.Intersection, P(3.05, -7.45));
            Assert.Equal(CadSnapKind.Intersection, bottom.Kind);
            Assert.Equal(-7.5, bottom.Point.Y, 6);
        }

        [Fact]
        public void Intersection_OfALineAndAWallBetweenCells_IsNotGiven()
        {
            CadDocument document = Crossed();

            Assert.Equal(CadSnapKind.None, SnapAt(document, CadSnapModes.Intersection, P(6.25, -4.95)).Kind);
            Assert.Equal(CadSnapKind.None, SnapAt(document, CadSnapModes.Intersection, P(6.3, -5.0)).Kind);
        }

        [Fact]
        public void Intersection_OfTheFramesOwnParts_IsNotGiven_WhereAWallMeetsTheSide()
        {
            CadDocument document = Metric();
            document.ApplyEdit("frame", null, s =>
            {
                var frame = new DxfTolerance { Text = "{\\Fgdt;j}%%v{\\Fgdt;c}", Location = new GeoPoint3(0, 0, 0) };
                s.Append(document.ModelSpace, frame);
                frame.DimensionStyleName = s.Database.Dimstyle;
            });

            Assert.Equal(CadSnapKind.None, SnapAt(document, CadSnapModes.Intersection, P(6.25, 2.5)).Kind);
            Assert.Equal(CadSnapKind.None, SnapAt(document, CadSnapModes.Intersection, P(6.25, -2.5)).Kind);
        }
    }
}
