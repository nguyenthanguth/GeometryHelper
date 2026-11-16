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
    /// A TEXT mirrored stays readable, as AutoCAD 2021's <c>TransformBy</c> leaves it, where the drawing's fonts measure its
    /// words (probed in its console, "ABC" of txt.shx 3 high at (20, 40), its ink 8.445 wide, under 6 mirrors and in 4 planes):
    /// it is turned over in its own plane before the mirror, across its upright or across its baseline, so that its point moves
    /// with the width of its ink. The fonts here are written by the tests, a capital inked 4 by 6 and moving the pen 6 as
    /// txt.shx draws it, so that "ABC" 3 high is inked from 0 to 8.
    /// </summary>
    public class DxfTextMirrorTests
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

        private static DxfDBText Text(DxfDatabase db, double rotation)
        {
            var text = new DxfDBText
            {
                Position = new GeoPoint3(20, 40, 0),
                TextString = "ABC",
                Height = 3.0,
                Rotation = rotation,
            };
            db.ModelSpace.AppendEntity(text);
            return text;
        }

        private static GeoTransform3 Across(GeoPoint3 point, GeoVector3 normal) =>
            GeoTransform3.Mirror(new GeoPlane3(point, normal.Normalize()));

        private static void Near(GeoPoint3 expected, GeoPoint3 actual) =>
            Assert.True(actual.IsEqualTo(expected, Tol), actual + " is not " + expected);

        private static void Near(GeoVector3 expected, GeoVector3 actual) =>
            Assert.True(actual.IsEqualTo(expected, Tol), actual + " is not " + expected);

        [Fact]
        public void ATextMirroredAcrossAPlaneAlongItsUpright_StartsWhereTheMirroredInkEnds_ReadingTheSameWay()
        {
            InFolder(folder =>
            {
                var db = new DxfDatabase("AC1032") { FontSearchPath = folder };
                DxfDBText text = Text(db, 0.0);

                text.TransformBy(Across(new GeoPoint3(40, 0, 0), new GeoVector3(1, 0, 0)));

                // AutoCAD 2021 (its "ABC" inked 8.445 wide): (51.555, 40), the normal (0, 0, 1), no rotation. Here the ink is 8 wide.
                Near(new GeoPoint3(52, 40, 0), text.Position);
                Near(new GeoVector3(0, 0, 1), text.Normal);
                Assert.Equal(0.0, text.Rotation, 12);
                Assert.False(text.IsMirroredInX);
            });
        }

        [Fact]
        public void ATextMirroredAcrossAPlaneAlongItsUpright_WhenItIsTurned_ReadsTheOtherWayRound()
        {
            InFolder(folder =>
            {
                var db = new DxfDatabase("AC1032") { FontSearchPath = folder };
                DxfDBText text = Text(db, 0.6);

                text.TransformBy(Across(new GeoPoint3(40, 0, 0), new GeoVector3(1, 0, 0)));

                // AutoCAD 2021: the end of the ink of the text mirrored, (53.03, 44.769) of the text of 34.4 degrees, at 325.6.
                var end = new GeoPoint3(20 + (8.0 * Math.Cos(0.6)), 40 + (8.0 * Math.Sin(0.6)), 0.0);
                Near(new GeoPoint3(80 - end.X, end.Y, 0.0), text.Position);
                Assert.Equal((2.0 * Math.PI) - 0.6, text.Rotation, 9);
                Near(new GeoVector3(0, 0, 1), text.Normal);
            });
        }

        [Fact]
        public void ATextMirroredAcrossItsOwnPlane_MovesItsPointAlongItsInk_AndTakesTheNormalDown()
        {
            InFolder(folder =>
            {
                var db = new DxfDatabase("AC1032") { FontSearchPath = folder };
                DxfDBText text = Text(db, 0.0);

                text.TransformBy(Across(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1)));

                // AutoCAD 2021: the normal (0, 0, -1), the point (-28.445, 40) in its system, which is (28.445, 40) of the
                // world (here (28, 40)), no rotation: seen from below the words read left to right from where they began.
                Near(new GeoVector3(0, 0, -1), text.Normal);
                Near(new GeoPoint3(28, 40, 0), text.Position);
                Assert.Equal(0.0, text.Rotation, 12);
            });
        }

        [Fact]
        public void ATextMirroredAcrossItsBaseline_IsTurnedOverAboutTheMiddleOfItsHeight()
        {
            InFolder(folder =>
            {
                var db = new DxfDatabase("AC1032") { FontSearchPath = folder };
                DxfDBText text = Text(db, 0.0);

                text.TransformBy(Across(new GeoPoint3(0, 30, 0), new GeoVector3(0, 1, 0)));

                // AutoCAD 2021: (20, 17), the normal still (0, 0, 1): the ink, 3 high from 40 to 43, goes to 17 to 20, the
                // point at its foot.
                Near(new GeoPoint3(20, 17, 0), text.Position);
                Near(new GeoVector3(0, 0, 1), text.Normal);
                Assert.Equal(0.0, text.Rotation, 12);
            });
        }

        [Fact]
        public void ATextMirroredWhereNoFontMeasuresIt_IsMirroredInItsPlaneAsBefore()
        {
            var db = new DxfDatabase("AC1032");
            DxfDBText text = Text(db, 0.0);

            text.TransformBy(Across(new GeoPoint3(40, 0, 0), new GeoVector3(1, 0, 0)));

            // With no font its words have no width to turn them over by: the normal turned over, the point mirrored.
            Near(new GeoVector3(0, 0, -1), text.Normal);
            Near(new GeoPoint3(60, 40, 0), text.Position);
        }
    }
}
