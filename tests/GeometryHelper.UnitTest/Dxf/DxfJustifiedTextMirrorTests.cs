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
    /// A TEXT held by a second point, mirrored, stays readable and holds its second point, as AutoCAD 2021's
    /// <c>TransformBy</c> leaves it (probed in its console on 196 texts of 14 modes, in 4 planes at 3 angles, under 7 mirrors,
    /// "ABC" 3 high in txt.shx). The fonts here are written by the tests, a capital inked 4 by 6 and moving the pen 6 as
    /// txt.shx draws it, so that "ABC" 3 high is inked from 0 to 8, where the console's own measure of the turn-over was 8.445.
    /// </summary>
    public class DxfJustifiedTextMirrorTests
    {
        private static readonly Tolerance Tol = new Tolerance(1E-9, 1E-9);

        /// <summary>A capital as txt.shx draws one: the pen down from the origin up 6 and along 4, lifted, down 6 and along 2.</summary>
        private static readonly byte[] Capital = { 1, 0x64, 0x40, 2, 0x6C, 0x20, 0 };

        /// <summary>The bytes of a unifont of AutoCAD's form: its header, then each capital A to Z and the space.</summary>
        private static byte[] Unifont()
        {
            var letters = new SortedDictionary<int, byte[]> { { ' ', new byte[] { 2, 0x60, 0 } } };

            for (char c = 'A'; c <= 'Z'; c++)
            {
                letters.Add(c, Capital);
            }

            var bytes = new List<byte>(Encoding.ASCII.GetBytes("AutoCAD-86 unifont 1.0\r\n\u001A"));
            byte[] header = Encoding.ASCII.GetBytes("GH TEST").Concat(new byte[] { 0, 6, 2, 2, 0, 0, 0 }).ToArray();
            bytes.AddRange(BitConverter.GetBytes((uint)(letters.Count + 1)));
            bytes.AddRange(BitConverter.GetBytes((ushort)header.Length));
            bytes.AddRange(header);

            foreach (KeyValuePair<int, byte[]> letter in letters)
            {
                byte[] record = new byte[] { 0 }
                    .Concat(letter.Value)
                    .ToArray();
                bytes.AddRange(BitConverter.GetBytes((ushort)letter.Key));
                bytes.AddRange(BitConverter.GetBytes((ushort)record.Length));
                bytes.AddRange(record);
            }

            return bytes.ToArray();
        }

        private static void InFolder(Action<string> test)
        {
            string folder = Path.Combine(Path.GetTempPath(), "gh-fonts-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);

            try
            {
                File.WriteAllBytes(Path.Combine(folder, "txt.shx"), Unifont());
                test(folder);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        private static DxfDBText Text(
            DxfDatabase db,
            DxfTextHorizontalMode horizontal,
            DxfTextVerticalMode vertical,
            GeoPoint3 first,
            GeoPoint3 second,
            double rotation
        )
        {
            var text = new DxfDBText
            {
                Position = first,
                TextString = "ABC",
                Height = 3.0,
                Rotation = rotation,
            };
            db.ModelSpace.AppendEntity(text);
            text.HorizontalMode = horizontal;
            text.VerticalMode = vertical;
            text.AlignmentPoint = second;
            return text;
        }

        private static GeoTransform3 Across(GeoPoint3 point, GeoVector3 normal) =>
            GeoTransform3.Mirror(new GeoPlane3(point, normal.Normalize()));

        private static void Near(GeoPoint3 expected, GeoPoint3 actual) =>
            Assert.True(actual.IsEqualTo(expected, Tol), actual + " is not " + expected);

        private static void Near(GeoVector3 expected, GeoVector3 actual) =>
            Assert.True(actual.IsEqualTo(expected, Tol), actual + " is not " + expected);

        [Fact]
        public void ATextCentredOnAPoint_MirroredAcrossAPlaneAlongItsUpright_IsTurnedOverAboutTheMiddleOfItsInk()
        {
            InFolder(folder =>
            {
                var db = new DxfDatabase("AC1032") { FontSearchPath = folder };
                DxfDBText text = Text(
                    db,
                    DxfTextHorizontalMode.TextCenter,
                    DxfTextVerticalMode.TextBase,
                    new GeoPoint3(20, 40, 0),
                    new GeoPoint3(30, 45, 0),
                    0.0
                );
                Near(new GeoPoint3(26, 45, 0), text.Position);

                text.TransformBy(Across(new GeoPoint3(40, 0, 0), new GeoVector3(1, 0, 0)));

                // AutoCAD 2021 (its ink 8.445 wide): (45.555, 45) held at (49.555, 45), no angle, the normal (0, 0, 1). Here the ink
                // is 8 wide.
                Near(new GeoPoint3(46, 45, 0), text.Position);
                Near(new GeoPoint3(50, 45, 0), text.AlignmentPoint);
                Assert.Equal(0.0, text.Rotation, 12);
                Near(new GeoVector3(0, 0, 1), text.Normal);
                Assert.Equal(DxfTextHorizontalMode.TextCenter, text.HorizontalMode);
            });
        }

        [Fact]
        public void ATextCentredOnAPoint_MirroredAcrossAPlaneAlongItsBaseline_IsTurnedOverAboutTheMiddleOfItsHeight()
        {
            InFolder(folder =>
            {
                var db = new DxfDatabase("AC1032") { FontSearchPath = folder };
                DxfDBText text = Text(
                    db,
                    DxfTextHorizontalMode.TextCenter,
                    DxfTextVerticalMode.TextBase,
                    new GeoPoint3(20, 40, 0),
                    new GeoPoint3(30, 45, 0),
                    0.0
                );

                text.TransformBy(Across(new GeoPoint3(0, 30, 0), new GeoVector3(0, 1, 0)));

                // AutoCAD 2021: the first point (26, 12), the second (30, 12): the words turned over about the middle of their
                // height, 46.5, which takes the baseline at 45 to 48, and mirrored across y = 30 to 12.
                Near(new GeoPoint3(26, 12, 0), text.Position);
                Near(new GeoPoint3(30, 12, 0), text.AlignmentPoint);
                Assert.Equal(0.0, text.Rotation, 12);
            });
        }

        [Fact]
        public void ATextHeldByItsRightEnd_MirroredAcrossAPlaneAlongItsUpright_HasItsTwoPointsSwapped()
        {
            var db = new DxfDatabase("AC1032");
            var text = new DxfDBText
            {
                Position = new GeoPoint3(22, 45, 0),
                TextString = "ABC",
                Height = 3.0,
            };
            db.ModelSpace.AppendEntity(text);
            text.HorizontalMode = DxfTextHorizontalMode.TextRight;
            text.AlignmentPoint = new GeoPoint3(30, 45, 0);

            text.TransformBy(Across(new GeoPoint3(40, 0, 0), new GeoVector3(1, 0, 0)));

            // AutoCAD 2021: (50, 45) held at (58, 45), the first point the second mirrored and the second the first, the ink not
            // asked (so no font is needed), no angle, the normal (0, 0, 1).
            Near(new GeoPoint3(50, 45, 0), text.Position);
            Near(new GeoPoint3(58, 45, 0), text.AlignmentPoint);
            Assert.Equal(0.0, text.Rotation, 12);
            Near(new GeoVector3(0, 0, 1), text.Normal);
        }

        [Fact]
        public void ATextAlignedOnTwoPoints_MirroredAcrossAPlaneAlongItsUpright_RunsFromTheSecondMirroredToTheFirst()
        {
            var db = new DxfDatabase("AC1032");
            var text = new DxfDBText
            {
                Position = new GeoPoint3(20, 40, 0),
                TextString = "ABC",
                Height = 3.0,
                Rotation = Math.Atan2(8.0, 20.0),
            };
            db.ModelSpace.AppendEntity(text);
            text.HorizontalMode = DxfTextHorizontalMode.TextAlign;
            text.AlignmentPoint = new GeoPoint3(40, 48, 0);

            text.TransformBy(Across(new GeoPoint3(40, 0, 0), new GeoVector3(1, 0, 0)));

            // AutoCAD 2021: from (60, 40) to (40, 48) turned 338.2 degrees, its words reading from the first to the second as they
            // did (it wrote (40.399, 43.973) and (60, 40) of the points (20, 40) and (39.601, 43.973)).
            Near(new GeoPoint3(40, 48, 0), text.Position);
            Near(new GeoPoint3(60, 40, 0), text.AlignmentPoint);
            Assert.Equal((2.0 * Math.PI) - Math.Atan2(8.0, 20.0), text.Rotation, 9);
            Near(new GeoVector3(0, 0, 1), text.Normal);
        }

        [Fact]
        public void ATextFittedOnTwoPoints_MirroredAcrossAPlaneAlongItsBaseline_KeepsItsPointsInOrder()
        {
            InFolder(folder =>
            {
                var db = new DxfDatabase("AC1032") { FontSearchPath = folder };
                DxfDBText text = Text(
                    db,
                    DxfTextHorizontalMode.TextFit,
                    DxfTextVerticalMode.TextBase,
                    new GeoPoint3(20, 40, 0),
                    new GeoPoint3(40, 40, 0),
                    0.0
                );

                text.TransformBy(Across(new GeoPoint3(0, 30, 0), new GeoVector3(0, 1, 0)));

                // AutoCAD 2021: the points (20, 17) and (40, 17), the words turned over about the middle of their height, the
                // width factor 2.5 as it was.
                Near(new GeoPoint3(20, 17, 0), text.Position);
                Near(new GeoPoint3(40, 17, 0), text.AlignmentPoint);
                Assert.Equal(2.5, text.WidthFactor, 9);
            });
        }

        [Fact]
        public void ATextHeldByItsMiddle_MirroredAcrossAPlaneAlongItsUpright_IsMirroredAsItStands()
        {
            var db = new DxfDatabase("AC1032");
            var text = new DxfDBText
            {
                Position = new GeoPoint3(26, 43.5, 0),
                TextString = "ABC",
                Height = 3.0,
            };
            db.ModelSpace.AppendEntity(text);
            text.HorizontalMode = DxfTextHorizontalMode.TextMid;
            text.AlignmentPoint = new GeoPoint3(30, 45, 0);

            text.TransformBy(Across(new GeoPoint3(40, 0, 0), new GeoVector3(1, 0, 0)));

            // AutoCAD 2021: the second point (50, 45) the middle of its words again, the first left where the mirror takes it,
            // (54, 43.5), no angle.
            Near(new GeoPoint3(54, 43.5, 0), text.Position);
            Near(new GeoPoint3(50, 45, 0), text.AlignmentPoint);
            Assert.Equal(0.0, text.Rotation, 12);
            Near(new GeoVector3(0, 0, 1), text.Normal);
        }

        [Fact]
        public void ATextCentredOnAPoint_MirroredAcrossItsOwnPlane_StandsOnTheNormalDown_ItsPointsMirroredWithIt()
        {
            InFolder(folder =>
            {
                var db = new DxfDatabase("AC1032") { FontSearchPath = folder };
                DxfDBText text = Text(
                    db,
                    DxfTextHorizontalMode.TextCenter,
                    DxfTextVerticalMode.TextBase,
                    new GeoPoint3(20, 40, 0),
                    new GeoPoint3(30, 45, 0),
                    0.0
                );

                text.TransformBy(Across(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1)));

                // AutoCAD 2021 (its ink 8.445 wide): the normal (0, 0, -1), the first point (34.445, 45) of the world held at
                // (30.445, 45), no angle (its points (-34.445, 45) and (-30.445, 45) in the system of the normal): seen from below
                // the words read from the left as they did. Here the ink is 8 wide.
                Near(new GeoVector3(0, 0, -1), text.Normal);
                Near(new GeoPoint3(34, 45, 0), text.Position);
                Near(new GeoPoint3(30, 45, 0), text.AlignmentPoint);
                Assert.Equal(0.0, text.Rotation, 12);
                Assert.False(text.IsMirroredInX);
            });
        }

        [Fact]
        public void ATextCentredOnAPoint_WithNoFontToMeasureItsInk_IsMirroredAsItWas_ItsNormalTurnedOver()
        {
            var db = new DxfDatabase("AC1032");
            var text = new DxfDBText
            {
                Position = new GeoPoint3(26, 45, 0),
                TextString = "ABC",
                Height = 3.0,
            };
            db.ModelSpace.AppendEntity(text);
            text.HorizontalMode = DxfTextHorizontalMode.TextCenter;
            text.AlignmentPoint = new GeoPoint3(30, 45, 0);

            text.TransformBy(Across(new GeoPoint3(40, 0, 0), new GeoVector3(1, 0, 0)));

            // Where no font measures the ink the width it is turned over about is not known (AutoCAD measures it, 8.445 here): the
            // text is mirrored as it was, its normal turned over, its points at the mirror's image.
            Near(new GeoPoint3(54, 45, 0), text.Position);
            Near(new GeoPoint3(50, 45, 0), text.AlignmentPoint);
            Near(new GeoVector3(0, 0, -1), text.Normal);
        }
    }
}
