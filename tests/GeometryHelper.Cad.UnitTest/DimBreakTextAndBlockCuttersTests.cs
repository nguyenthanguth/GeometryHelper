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
    /// DIMBREAK Auto at TEXT, MTEXT and block references, as AutoCAD 2021's console breaks a DIMLINEAR of 100 whose line is at
    /// y = -30 (the break size 0.125, so 0.0625 each side): a TEXT 2.5 high standing from y = -21 to -18.5 across the extension
    /// line at x = 0 breaks it from -21.0625 to -18.4375, an MTEXT 2.5 high held at its top left at y = -18 from -20.5625 to
    /// -17.9375, a TEXT turned 45 degrees over the stretch of the line inside its turned box (-18.0625 to -14.4020 for words
    /// 15.83 wide), whatever the extents of the box standing upright would say; the shapes of a block are each a cutter in their
    /// place (a rectangle 6 by 4 held at (-3, -12) breaks the line at -12 and at -8, one turned 30 degrees at -10.2679 and
    /// -5.6491, one of scale 2 at -12 and -4, not over its whole box), and a hatch is none, its boundary alone being drawn.
    /// </summary>
    public class DimBreakTextAndBlockCuttersTests : IDisposable
    {
        private readonly FontFolder _folder = new FontFolder();

        public void Dispose() => _folder.Dispose();

        private CadDocument WithDimension(out CadCommandSession session)
        {
            CadDocument document = Blank();
            document.Database.DimStyleTable["Standard"].Dimtad = 1;
            _folder.Write("test.shx", ShxFiles.Unifont());
            document.Fonts = new CadFonts(new[] { _folder.FullPath }, null, null);
            document.Database.FontSearchPath = _folder.FullPath;
            document.Database.TextStyleTable["Standard"].FileName = "test.shx";
            session = Session(document);
            Run(session, "DLI", P(0, 0), P(100, 0), P(50, -30));
            return document;
        }

        private static List<KeyValuePair<double, double>> Gaps(CadDocument document, double x)
        {
            List<DxfLine> pieces = Assert
                .Single(All<DxfRotatedDimension>(document.ModelSpace))
                .DimBlock.OfType<DxfLine>()
                .Where(l => !l.IsErased && l.Layer != "Defpoints")
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

        private static void Break(CadDocument document, CadCommandSession session)
        {
            string handle = Assert.Single(All<DxfRotatedDimension>(document.ModelSpace)).Handle;
            Run(session, "DIMBREAK", new[] { handle }, Enter);
        }

        private static DxfBlockTableRecord Rectangle(CadDocument document)
        {
            var block = new DxfBlockTableRecord { Name = "RECT" };
            document.Database.BlockTable.Add(block);
            GeoPoint3[] corners =
            {
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(6, 0, 0),
                new GeoPoint3(6, 4, 0),
                new GeoPoint3(0, 4, 0),
            };

            for (int i = 0; i < 4; i++)
            {
                block.AppendEntity(new DxfLine(corners[i], corners[(i + 1) % 4]));
            }

            return block;
        }

        [Fact]
        public void ATextAcrossAnExtensionLine_BreaksItOverItsWholeBox_TheBreakSizeBeyondEachEnd()
        {
            CadDocument document = WithDimension(out CadCommandSession session);
            var text = new DxfDBText
            {
                Position = new GeoPoint3(-3, -21, 0),
                Height = 2.5,
                TextString = "OOOOO",
            };
            document.Database.ModelSpace.AppendEntity(text);
            GeoAabb3 box = text.GeometricExtents;

            Break(document, session);

            KeyValuePair<double, double> gap = Assert.Single(Gaps(document, 0.0));
            Assert.Equal(box.Min.Y - 0.0625, gap.Key, 6);
            Assert.Equal(box.Max.Y + 0.0625, gap.Value, 6);
            Assert.Empty(Gaps(document, 100.0));
        }

        [Fact]
        public void AnMtextAcrossAnExtensionLine_BreaksItOverItsWholeBox_TheBreakSizeBeyondEachEnd()
        {
            CadDocument document = WithDimension(out CadCommandSession session);
            var text = new DxfMText
            {
                Location = new GeoPoint3(-3, -18, 0),
                TextHeight = 2.5,
                Contents = "OOOOO",
                Attachment = DxfAttachmentPoint.TopLeft,
            };
            document.Database.ModelSpace.AppendEntity(text);
            GeoAabb3 box = text.GeometricExtents;
            Assert.True(box.Max.X > 0.0 && box.Min.X < 0.0);

            Break(document, session);

            KeyValuePair<double, double> gap = Assert.Single(Gaps(document, 0.0));
            Assert.Equal(box.Min.Y - 0.0625, gap.Key, 6);
            Assert.Equal(box.Max.Y + 0.0625, gap.Value, 6);
        }

        [Fact]
        public void ATextTurned45Degrees_BreaksTheLineOverTheStretchInsideItsTurnedBox_NotItsUprightExtents()
        {
            // AutoCAD 2021: WWWWW 2.5 high turned 45 degrees at (-3, -21), the box 15.83 by 2.5: x = 0 is inside it from
            // y = -18 (the bottom edge) to -14.4645 (the top edge), and the gap runs 0.0625 beyond both.
            CadDocument document = WithDimension(out CadCommandSession session);
            var upright = new DxfDBText
            {
                Position = new GeoPoint3(-3, -21, 0),
                Height = 2.5,
                TextString = "OOOOO",
            };
            document.Database.ModelSpace.AppendEntity(upright);
            double width = upright.GeometricExtents.Max.X - upright.GeometricExtents.Min.X;
            double height = upright.GeometricExtents.Max.Y - upright.GeometricExtents.Min.Y;
            upright.Erase();
            var turned = new DxfDBText
            {
                Position = new GeoPoint3(-3, -21, 0),
                Height = 2.5,
                TextString = "OOOOO",
                Rotation = Math.PI / 4.0,
            };
            document.Database.ModelSpace.AppendEntity(turned);

            Break(document, session);

            double c = Math.Cos(Math.PI / 4.0);
            double s = Math.Sin(Math.PI / 4.0);
            // x = 0 meets the bottom edge at distance 3 / c along it, and the top edge, which starts height up the turned
            // y axis, 3 + height * s over c along it.
            double low = -21.0 + 3.0 / c * s;
            double high = -21.0 + height * c + (3.0 + height * s) / c * s;
            Assert.True(width * c - 3.0 > 0.0);

            KeyValuePair<double, double> gap = Assert.Single(Gaps(document, 0.0));
            Assert.Equal(low - 0.0625, gap.Key, 6);
            Assert.Equal(high + 0.0625, gap.Value, 6);
        }

        [Fact]
        public void ABlockReference_BreaksTheLineAtEachShapeOfIt_NotOverItsWholeBox()
        {
            CadDocument document = WithDimension(out CadCommandSession session);
            DxfBlockTableRecord block = Rectangle(document);
            document.Database.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(-3, -12, 0), block));

            Break(document, session);

            List<KeyValuePair<double, double>> gaps = Gaps(document, 0.0);
            Assert.Equal(2, gaps.Count);
            Assert.Equal(-12.0, (gaps[0].Key + gaps[0].Value) / 2.0, 6);
            Assert.Equal(-8.0, (gaps[1].Key + gaps[1].Value) / 2.0, 6);
            Assert.Equal(0.125, gaps[0].Value - gaps[0].Key, 6);
            Assert.Empty(Gaps(document, 100.0));
        }

        [Fact]
        public void ABlockReferenceOfScale2_BreaksAtWhereItsShapesAreNow()
        {
            // AutoCAD 2021: the rectangle held at (-6, -12) at scale 2 is 12 by 8, its edges at -12 and -4.
            CadDocument document = WithDimension(out CadCommandSession session);
            document.Database.ModelSpace.AppendEntity(
                new DxfBlockReference(new GeoPoint3(-6, -12, 0), Rectangle(document))
                {
                    ScaleFactors = new GeoVector3(2, 2, 2),
                }
            );

            Break(document, session);

            double[] at = Gaps(document, 0.0).Select(g => (g.Key + g.Value) / 2.0).ToArray();
            Assert.Equal(2, at.Length);
            Assert.Equal(-12.0, at[0], 6);
            Assert.Equal(-4.0, at[1], 6);
        }

        [Fact]
        public void ABlockReferenceTurned30Degrees_BreaksAtWhereItsEdgesCrossTheLine()
        {
            // AutoCAD 2021: the rectangle held at (-3, -12) and turned 30 degrees is crossed by x = 0 at -10.2679 (its base,
            // 3 along it) and -5.6491 (its top edge, which starts 4 up the turned y axis, at (-5, -8.5359)).
            CadDocument document = WithDimension(out CadCommandSession session);
            document.Database.ModelSpace.AppendEntity(
                new DxfBlockReference(new GeoPoint3(-3, -12, 0), Rectangle(document))
                {
                    Rotation = 30.0 * Math.PI / 180.0,
                }
            );

            Break(document, session);

            double[] at = Gaps(document, 0.0).Select(g => (g.Key + g.Value) / 2.0).ToArray();
            Assert.Equal(2, at.Length);
            Assert.Equal(-10.2679492, at[0], 6);
            Assert.Equal(-5.6491471, at[1], 6);
        }

        [Fact]
        public void AHatch_IsNoCutter_OnlyItsBoundaryIs()
        {
            CadDocument document = WithDimension(out CadCommandSession session);
            var hatch = new DxfHatch();
            hatch.SetHatchPattern(DxfHatchPatternType.PreDefined, "SOLID");
            hatch.AppendLoop(
                DxfHatchLoopTypes.External,
                new[] { new GeoPoint2(-3, -22), new GeoPoint2(3, -22), new GeoPoint2(3, -18), new GeoPoint2(-3, -18) },
                null
            );
            document.Database.ModelSpace.AppendEntity(hatch);

            Break(document, session);

            Assert.Empty(Gaps(document, 0.0));
        }
    }
}
