using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// DIMBREAK Auto breaks a dimension at more than lines, polylines, circles and arcs. AutoCAD 2021 (probed in its console,
    /// a DIMLINEAR of 100 with its line 20 above, the break size 0.125): an xline across the extension lines breaks both of
    /// them at it, an ellipse crossed at 15 and 17 breaks at 15.0001 and 16.9999 (it follows the curve as straight pieces), a
    /// spline at the point where it crosses, a TEXT 2.5 high across an extension line breaks it over its whole box with the
    /// break size either side.
    /// </summary>
    public class DimBreakCuttersTests
    {
        private static CadDocument WithDimension(out CadCommandSession session)
        {
            CadDocument document = Blank();
            document.Database.DimStyleTable["Standard"].Dimtad = 1;
            session = Session(document);
            Run(session, "DLI", P(0, 0), P(100, 0), P(50, -30));
            return document;
        }

        private static List<DxfLine> Picture(CadDocument document) =>
            Assert
                .Single(All<DxfRotatedDimension>(document.ModelSpace))
                .DimBlock.OfType<DxfLine>()
                .Where(l => !l.IsErased && l.Layer != "Defpoints")
                .ToList();

        /// <summary>The gaps along the vertical extension line at x = 0 of the picture, as (from, to) pairs of the y between its pieces.</summary>
        private static List<KeyValuePair<double, double>> Gaps(CadDocument document, double x)
        {
            List<DxfLine> pieces = Picture(document)
                .Where(l => Math.Abs(l.StartPoint.X - x) < 1E-9 && Math.Abs(l.EndPoint.X - x) < 1E-9)
                .OrderBy(l => Math.Min(l.StartPoint.Y, l.EndPoint.Y))
                .ToList();
            var gaps = new List<KeyValuePair<double, double>>();

            for (int i = 0; i + 1 < pieces.Count; i++)
            {
                gaps.Add(
                    new KeyValuePair<double, double>(
                        Math.Max(pieces[i].StartPoint.Y, pieces[i].EndPoint.Y),
                        Math.Min(pieces[i + 1].StartPoint.Y, pieces[i + 1].EndPoint.Y)
                    )
                );
            }

            return gaps;
        }

        [Fact]
        public void AnXlineAcrossTheExtensionLines_BreaksBothOfThemAtIt()
        {
            CadDocument document = WithDimension(out CadCommandSession session);
            DxfDimStyleTableRecord style = Assert
                .Single(All<DxfRotatedDimension>(document.ModelSpace))
                .GetDimstyleData();
            double size = 0.125 * (style.Dimscale > 0.0 ? style.Dimscale : 1.0);
            var xline = new DxfXline { BasePoint = new GeoPoint3(0, -15, 0), UnitDir = new GeoVector3(1, 0, 0) };
            document.Database.ModelSpace.AppendEntity(xline);
            string handle = Assert.Single(All<DxfRotatedDimension>(document.ModelSpace)).Handle;

            Run(session, "DIMBREAK", new[] { handle }, Enter);

            foreach (double x in new[] { 0.0, 100.0 })
            {
                KeyValuePair<double, double> gap = Assert.Single(Gaps(document, x));
                Assert.Equal(-15 - size / 2, gap.Key, 9);
                Assert.Equal(-15 + size / 2, gap.Value, 9);
            }
        }

        [Fact]
        public void ARay_BreaksOnlyWhereItRuns_NotBehindItsBase()
        {
            CadDocument document = WithDimension(out CadCommandSession session);
            // Along +x from (-10, -15): crosses the extension line at x = 0 and the one at x = 100; from (50, -15) it
            // starts between them and crosses only the second.
            var ray = new DxfRay { BasePoint = new GeoPoint3(50, -15, 0), UnitDir = new GeoVector3(1, 0, 0) };
            document.Database.ModelSpace.AppendEntity(ray);
            string handle = Assert.Single(All<DxfRotatedDimension>(document.ModelSpace)).Handle;

            Run(session, "DIMBREAK", new[] { handle }, Enter);

            Assert.Empty(Gaps(document, 0.0));
            Assert.Single(Gaps(document, 100.0));
        }

        [Fact]
        public void AnEllipse_BreaksTheExtensionLineWhereItCrossesIt_FollowedAsStraightPieces()
        {
            CadDocument document = WithDimension(out CadCommandSession session);
            // Centre (0, -15), major axis 3 along x, ratio 1/3: crossed by x = 0 at y = -14 and y = -16.
            document.Database.ModelSpace.AppendEntity(
                new DxfEllipse(
                    new GeoPoint3(0, -15, 0),
                    new GeoVector3(0, 0, 1),
                    new GeoVector3(3, 0, 0),
                    1.0 / 3.0,
                    0.0,
                    2.0 * Math.PI
                )
            );
            string handle = Assert.Single(All<DxfRotatedDimension>(document.ModelSpace)).Handle;

            Run(session, "DIMBREAK", new[] { handle }, Enter);

            List<KeyValuePair<double, double>> gaps = Gaps(document, 0.0);
            Assert.Equal(2, gaps.Count);
            Assert.Equal(-16, (gaps[0].Key + gaps[0].Value) / 2.0, 3);
            Assert.Equal(-14, (gaps[1].Key + gaps[1].Value) / 2.0, 3);
        }
    }
}
