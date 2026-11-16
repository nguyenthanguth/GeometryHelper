using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The <c>GeometricExtents</c> of a dimension and of a multileader, as AutoCAD 2021 gives them (probed in its console,
    /// 16 November): the box of the picture, its points of definition among it, for a dimension; the box of the leader lines,
    /// the arrowheads and the words, for a multileader. A level dimension from (0, 0) to (100, 0), its line at y 20, filled (0, 0)
    /// to (100, 20.18); a multileader from (0, 0) to a landing at (100, -1.25), words "TEXT" 2.5 high, arrowheads of 0.18, filled
    /// (0, -2.5) to (109.706667, 0.027748).
    /// </summary>
    public class DxfDimensionAndMLeaderExtentsTests
    {
        private static readonly Tolerance Close = new Tolerance(1E-6, 1E-6);

        private static GeoPoint3 P(double x, double y) => new GeoPoint3(x, y, 0);

        private static void Near(GeoPoint3 expected, GeoPoint3 actual) =>
            Assert.True(expected.IsEqualTo(actual, Close), "expected " + expected + ", got " + actual);

        /// <summary>A level dimension from (0, 0) to (100, 0), its line at y 20, drawn by a block of its own as AutoCAD draws it, with the points of definition on the layer Defpoints.</summary>
        private static DxfRotatedDimension Level(DxfDatabase db)
        {
            var picture = new DxfBlockTableRecord { Name = "*D" };
            db.BlockTable.Add(picture);
            picture.AppendEntity(new DxfLine(P(0, 0.0625), P(0, 20.18)));
            picture.AppendEntity(new DxfLine(P(100, 0.0625), P(100, 20.18)));
            picture.AppendEntity(new DxfLine(P(0.18, 20), P(99.82, 20)));

            foreach (GeoPoint3 point in new[] { P(0, 0), P(100, 0), P(100, 20) })
            {
                picture.AppendEntity(new DxfDBPoint(point) { Layer = "Defpoints" });
            }

            var dimension = new DxfRotatedDimension(0.0, P(0, 0), P(100, 0), P(100, 20), string.Empty, null)
            {
                DimBlock = picture,
            };
            db.ModelSpace.AppendEntity(dimension);
            return dimension;
        }

        [Fact]
        public void ADimension_FillsTheBoxOfItsPicture_ItsPointsOfDefinitionAmongIt()
        {
            // The extension lines reach 20.18, and the points at the ends of the length reach down to 0, where the lines begin at 0.0625.
            GeoAabb3 box = Level(new DxfDatabase("AC1027")).GeometricExtents;

            Near(P(0, 0), box.Min);
            Near(P(100, 20.18), box.Max);
        }

        [Fact]
        public void ADimensionMoved_HasTheBoxOfItsPictureMovedWithIt()
        {
            DxfRotatedDimension dimension = Level(new DxfDatabase("AC1027"));

            dimension.TransformBy(GeoTransform3.Translation(new GeoVector3(5, 20, 0)));

            GeoAabb3 box = dimension.GeometricExtents;
            Near(P(5, 20), box.Min);
            Near(P(105, 40.18), box.Max);
        }

        [Fact]
        public void ADimensionWhoseBlockIsEmpty_HasNoExtents_AsAutoCadThrowsForAnEntityWithNone()
        {
            var db = new DxfDatabase("AC1027");
            var picture = new DxfBlockTableRecord { Name = "*D" };
            db.BlockTable.Add(picture);
            var dimension = new DxfRotatedDimension(0.0, P(0, 0), P(100, 0), P(100, 20), string.Empty, null)
            {
                DimBlock = picture,
            };
            db.ModelSpace.AppendEntity(dimension);

            Assert.Throws<InvalidOperationException>(() => dimension.GeometricExtents);
        }

        /// <summary>A multileader of the drawing: words "A" 4 high at (100, 0), a leader line from (0, 0), its arrowhead of 4.</summary>
        private static DxfMLeader Words(DxfDatabase db)
        {
            var mleader = new DxfMLeader
            {
                MText = new DxfMText
                {
                    Contents = "A",
                    Location = P(100, 0),
                    TextHeight = 4.0,
                },
            };
            mleader.AddLeaderLine(P(0, 0));
            db.ModelSpace.AppendEntity(mleader);
            return mleader;
        }

        [Fact]
        public void AMultileader_FillsTheBoxOfItsArrowhead_WhichStandsOutOfTheBoxOfItsPoints()
        {
            // The line runs from (0, 0) to the landing nearly level, so that the corners of the arrowhead, a third as wide as
            // long, 4 long, stand 0.667 either side of it: the box of the points alone is as high as they are, the arrowhead's
            // corners lie above and below it.
            DxfMLeader mleader = Words(DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018));
            GeoPoint3 landing = mleader.GetLastVertex(0);
            GeoVector3 way = P(0, 0).GetVectorTo(landing);
            way = way.Multiply(1.0 / way.Length);
            double top = Math.Max(0.0, Math.Max(landing.Y, mleader.TextLocation.Y));
            double corner = way.Y * 4.0 + Math.Abs(way.X) * 4.0 / 6.0;

            GeoAabb3 box = mleader.GeometricExtents;

            Assert.True(corner > top + 0.1, "the arrowhead stands out of the box of the points");
            Assert.Equal(corner, box.Max.Y, 6);
            Assert.Equal(0.0, box.Min.X, 6);
        }

        /// <summary>A folder of its own holding txt.shx, a unifont of capitals each inked 4 by 6, so that words are measured; deleted after the test.</summary>
        private static void InFolder(Action<string> test)
        {
            string folder = Path.Combine(Path.GetTempPath(), "gh-fonts-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);

            try
            {
                var bytes = new List<byte>(Encoding.ASCII.GetBytes("AutoCAD-86 unifont 1.0\r\n\u001A"));
                byte[] header = Encoding.ASCII.GetBytes("GH TEST").Concat(new byte[] { 0, 6, 2, 2, 0, 0, 0 }).ToArray();
                byte[] capital = { 0, 1, 0x64, 0x40, 2, 0x6C, 0x20, 0 };
                bytes.AddRange(BitConverter.GetBytes((uint)27));
                bytes.AddRange(BitConverter.GetBytes((ushort)header.Length));
                bytes.AddRange(header);

                for (char c = 'A'; c <= 'Z'; c++)
                {
                    bytes.AddRange(BitConverter.GetBytes((ushort)c));
                    bytes.AddRange(BitConverter.GetBytes((ushort)capital.Length));
                    bytes.AddRange(capital);
                }

                File.WriteAllBytes(Path.Combine(folder, "txt.shx"), bytes.ToArray());
                test(folder);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [Fact]
        public void AMultileader_FillsTheBoxOfItsWords_WhereTheFontsMeasureThem()
        {
            InFolder(folder =>
            {
                DxfDatabase db = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
                db.FontSearchPath = folder;
                DxfMLeader mleader = Words(db);
                DxfMText words = mleader.Explode().OfType<DxfMText>().Single();
                GeoAabb3 wordsBox = words.GeometricExtents;

                GeoAabb3 box = mleader.GeometricExtents;

                // The words reach to the right of the point they are held by, which is all the points of the multileader reach, and
                // the landing gap more beyond them, on the side away from the leader (AutoCAD 2021: 0.09 at 109.62, 109.71).
                Assert.True(wordsBox.Max.X > mleader.TextLocation.X + 1.0);
                Assert.True(mleader.LandingGap > 0.0);
                Assert.Equal(wordsBox.Max.X + mleader.LandingGap, box.Max.X, 6);
                Assert.True(box.Min.Y <= wordsBox.Min.Y + 1E-6);
            });
        }
    }
}
