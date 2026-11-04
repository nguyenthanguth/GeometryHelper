using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// IMAGEs and WIPEOUTs as entities, through what every <see cref="DxfEntity"/> has: moved, turned, scaled, mirrored and
    /// sheared as AutoCAD 2021 moves them (where they were kept as the file had them and refused), exploded into the lines of
    /// their frame or boundary, given their boundary as their shape and box, and copied into another drawing with their
    /// IMAGEDEF. The numbers are those AutoCAD 2021 gave for the same records.
    /// </summary>
    public class DxfRasterImageMovedTests
    {
        private static readonly double Close = 1E-9;

        private static DxfEntity Of(DxfDatabase db, string handle) => Everything(db).First(e => e.Handle == handle);

        private static string Group(string text, string handle, int code, int nth = 0)
        {
            string record = Records(text).First(r => HandleOf(r) == handle);
            string[] lines = record.Split('\n');
            int seen = 0;

            for (int i = 0; i + 1 < lines.Length; i += 2)
            {
                if (lines[i].Trim() == code.ToString(System.Globalization.CultureInfo.InvariantCulture) && seen++ == nth)
                {
                    return lines[i + 1].Trim();
                }
            }

            return null;
        }

        private static double Number(string text, string handle, int code, int nth = 0)
            => double.Parse(Group(text, handle, code, nth), System.Globalization.CultureInfo.InvariantCulture);

        private static void Near(double expected, double actual) => Assert.InRange(actual, expected - Close, expected + Close);

        private static void Near(GeoPoint3 expected, GeoPoint3 actual)
        {
            Near(expected.X, actual.X);
            Near(expected.Y, actual.Y);
            Near(expected.Z, actual.Z);
        }

        private static GeoPoint3 Point(string text, string handle, int code)
            => new GeoPoint3(Number(text, handle, code), Number(text, handle, code + 10), Number(text, handle, code + 20));

        [Fact]
        public void AnImageMovedTurnedScaledMirroredOrSheared_LandsWhereAutoCad2021MovesIt()
        {
            // AutoCAD 2021's TransformBy of the picture 40 by 30 at (10, 20): the corner (10) moved, the pixel across (11) and up
            // (12) turned and scaled, whatever the transformation, as it gave them.
            var cases = new[]
            {
                Tuple.Create(GeoTransform3.Translation(new GeoVector3(5, 7, 1)), new GeoPoint3(15, 27, 1), new GeoPoint3(10, 0, 0), new GeoPoint3(0, 10, 0)),
                Tuple.Create(GeoTransform3.RotationZ(Math.PI / 2), new GeoPoint3(-20, 10, 0), new GeoPoint3(0, 10, 0), new GeoPoint3(-10, 0, 0)),
                Tuple.Create(GeoTransform3.Scaling(2, 1, 1), new GeoPoint3(20, 20, 0), new GeoPoint3(20, 0, 0), new GeoPoint3(0, 10, 0)),
                Tuple.Create(new GeoTransform3(new[,] { { 1.0, 0.5, 0, 0 }, { 0, 1.0, 0, 0 }, { 0, 0, 1.0, 0 }, { 0, 0, 0, 1.0 } }), new GeoPoint3(20, 20, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(5, 10, 0)),
                Tuple.Create(GeoTransform3.Scaling(-1, 1, 1), new GeoPoint3(-10, 20, 0), new GeoPoint3(-10, 0, 0), new GeoPoint3(0, 10, 0)),
            };

            foreach (var c in cases)
            {
                string text = DxfRasterImageDrawings.R2018;
                DxfDatabase db = DxfDatabase.Parse(text);
                Of(db, "C10").TransformBy(c.Item1);
                string after = db.ToText();

                Near(c.Item2, Point(after, "C10", 10));
                Near(c.Item3, Point(after, "C10", 11));
                Near(c.Item4, Point(after, "C10", 12));
                OnlyTheseChanged(text, after, "C10");
            }
        }

        [Fact]
        public void AWipeoutMoved_TakesItsFrame_AndItsMaskGoesWithIt()
        {
            string text = DxfRasterImageDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfEntity wipeout = Of(db, "C14");

            wipeout.TransformBy(GeoTransform3.RotationZ(Math.PI / 2));
            string after = db.ToText();

            // The rectangle (-30, -10) to (10, 20) turned a quarter about the origin: (-20, 10) to (10, -30)... its corner first.
            Near(new GeoPoint3(10, -30, 0), Point(after, "C14", 10));
            Near(new GeoPoint3(0, 40, 0), Point(after, "C14", 11));
            Near(new GeoPoint3(-40, 0, 0), Point(after, "C14", 12));
            Assert.Equal(Group(text, "C14", 14, 2), Group(after, "C14", 14, 2));
            OnlyTheseChanged(text, after, "C14");

            GeoAabb3 box = wipeout.GeometricExtents;
            Near(new GeoPoint3(-20, -30, 0), box.Min);
            Near(new GeoPoint3(10, 10, 0), box.Max);
        }

        [Fact]
        public void ACopyOfAWipeoutScaledMoreOneWayOrSheared_IsRefused_ButTheImagesIsMade()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfRasterImageDrawings.R2018);
            var stretch = GeoTransform3.Scaling(2, 1, 1);
            var shear = new GeoTransform3(new[,] { { 1.0, 0.5, 0, 0 }, { 0, 1.0, 0, 0 }, { 0, 0, 1.0, 0 }, { 0, 0, 0, 1.0 } });

            // AutoCAD 2021's GetTransformedCopy of a Wipeout answers eCannotScaleNonUniformly; of a RasterImage it gives the copy.
            Assert.Throws<ArgumentException>(() => Of(db, "C14").GetTransformedCopy(stretch));
            Assert.Throws<ArgumentException>(() => Of(db, "C14").GetTransformedCopy(shear));
            Assert.NotNull(Of(db, "C14").GetTransformedCopy(GeoTransform3.Scaling(-2, 2, 2)));
            Assert.NotNull(Of(db, "C10").GetTransformedCopy(stretch));
            Assert.NotNull(Of(db, "C10").GetTransformedCopy(shear));
        }

        [Fact]
        public void AnImageExploded_GivesALineAlongEachSideOfItsFrame_OrOfItsBoundaryWhereClipped()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfRasterImageDrawings.R2018);

            // AutoCAD 2021: four LINEs from (10, 20, 0) up the left and round; three along the triangle; four along the wipeout.
            DxfLine[] frame = Of(db, "C10").Explode().Cast<DxfLine>().ToArray();
            Assert.Equal(4, frame.Length);
            Near(new GeoPoint3(10, 20, 0), frame[0].StartPoint);
            Near(new GeoPoint3(10, 50, 0), frame[0].EndPoint);
            Near(new GeoPoint3(50, 20, 0), frame[3].StartPoint);
            Assert.All(frame, l => Assert.Equal("pictures", l.Layer));

            DxfLine[] triangle = Of(db, "C11").Explode().Cast<DxfLine>().ToArray();
            Assert.Equal(3, triangle.Length);
            Near(new GeoPoint3(15, 45, 0), triangle[0].StartPoint);
            Near(new GeoPoint3(45, 45, 0), triangle[1].StartPoint);
            Near(new GeoPoint3(30, 20, 0), triangle[2].StartPoint);

            DxfLine[] mask = Of(db, "C14").Explode().Cast<DxfLine>().ToArray();
            Assert.Equal(4, mask.Length);
            Near(new GeoPoint3(-30, -10, 0), mask[0].StartPoint);
            Near(new GeoPoint3(10, -10, 0), mask[1].StartPoint);
            Near(new GeoPoint3(10, 20, 0), mask[2].StartPoint);
        }

        [Fact]
        public void TheShapeAndBoxOfAnImage_AreItsFrameOrBoundary_ThoughTheReaderReadsNone()
        {
            string text = DxfRasterImageDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);

            var frame = (GeoPolyline3)Assert.Single(Of(db, "C10").GetGeometry());
            Assert.Equal(5, frame.Vertices.Count);
            Near(frame.Vertices[0], frame.Vertices[4]);
            Near(new GeoPoint3(50, 50, 0), frame.Vertices[2]);

            GeoAabb3 clipped = Of(db, "C11").GeometricExtents;
            Near(new GeoPoint3(15, 20, 0), clipped.Min);
            Near(new GeoPoint3(45, 45, 0), clipped.Max);

            // Standing upright at (0, 0, 50), 40 along x and 30 up z.
            GeoAabb3 upright = Of(db, "C13").GeometricExtents;
            Near(new GeoPoint3(0, 0, 50), upright.Min);
            Near(new GeoPoint3(40, 0, 80), upright.Max);

            // The reader is left as it was: it reads no IMAGE and no WIPEOUT.
            DxfDocument read = DxfReader.Parse(text, new DxfReadOptions { Tolerance = Tolerance.Default });
            Assert.DoesNotContain(read.Shapes, s => s.Handle == "C10" || s.Handle == "C14");
        }

        [Fact]
        public void AnImageCopiedIntoADrawingOfNoImages_BringsItsImageDefUnderItsName_OnceForTwoImages()
        {
            DxfDatabase source = DxfDatabase.Parse(DxfRasterImageDrawings.R2018);
            var target = new DxfDatabase("AC1032");
            DxfEntity one = Of(source, "C10").Clone();
            DxfEntity two = Of(source, "C11").GetTransformedCopy(GeoTransform3.Translation(new GeoVector3(100, 0, 0)));

            target.ModelSpace.AppendEntity(one);
            target.ModelSpace.AppendEntity(two);
            string text = target.ToText();
            DxfDatabase reopened = DxfDatabase.Parse(text);

            // The IMAGEDEF of photo.jpg under its name in a dictionary of images made for it, both copies pointing at it.
            List<string> definitions = Records(text).Where(r => r.StartsWith("  0\nIMAGEDEF\n", StringComparison.Ordinal)).ToList();
            string definition = HandleOf(Assert.Single(definitions));
            Assert.Contains("\nphoto.jpg\n", definitions[0], StringComparison.Ordinal);
            Assert.Equal(definition, Group(text, one.Handle, 340));
            Assert.Equal(definition, Group(text, two.Handle, 340));
            Assert.Contains(Records(text), r => r.StartsWith("  0\nDICTIONARY\n", StringComparison.Ordinal) && r.Contains("\nphoto.jpg\n350\n" + definition + "\n"));
            Assert.Equal(2, reopened.ModelSpace.Count());

            // Listed in the CLASSES, of one object each, as AutoCAD 2021 reads an IMAGE only where its class is listed and counted.
            Assert.Contains(Records(text), r => r.StartsWith("  0\nCLASS\n  1\nIMAGE\n", StringComparison.Ordinal) && r.Contains("\n 91\n1\n") && r.Contains("\n281\n1\n"));
            Assert.Contains(Records(text), r => r.StartsWith("  0\nCLASS\n  1\nIMAGEDEF\n", StringComparison.Ordinal) && r.Contains("\n 91\n1\n"));
            Near(new GeoPoint3(115, 45, 0), reopened.ModelSpace.Last().Explode().Cast<DxfLine>().First().StartPoint);
        }

        [Fact]
        public void AnImageCopiedIntoItsOwnDrawing_ShowsTheSameImageDef_WithNoReactorOfItsOriginal()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfEntity copy = Of(db, "8E").GetTransformedCopy(GeoTransform3.Translation(new GeoVector3(0, 100, 0)));

            db.ModelSpace.AppendEntity(copy);
            string text = db.ToText();

            // As AutoCAD 2021's Clone appended: the IMAGEDEF it shows, and none of the reactor 8F of the image it was copied from.
            Assert.Equal("8D", Group(text, copy.Handle, 340));
            Assert.NotEqual("8F", Group(text, copy.Handle, 360));
            Near(new GeoPoint3(0, 100, 0), Point(text, copy.Handle, 10));
        }

        [Fact]
        public void ATagOfWordsOnAWipeout_ExplodesWithTheWipeoutPlaced_AndInsertsWithAPlacing()
        {
            DxfDatabase source = DxfDatabase.Parse(DxfRasterImageDrawings.R2018);
            var drawing = new DxfDatabase("AC1032");
            var tag = new DxfBlockTableRecord { Name = "TAG" };
            drawing.BlockTable.Add(tag);
            tag.AppendEntity(Of(source, "C14").Clone());
            tag.AppendEntity(new DxfLine(new GeoPoint3(-30, -10, 0), new GeoPoint3(10, 20, 0)));
            var insert = new DxfBlockReference(new GeoPoint3(100, 0, 0), tag);
            drawing.ModelSpace.AppendEntity(insert);

            Assert.Contains(Records(drawing.ToText()), r => r.StartsWith("  0\nCLASS\n  1\nWIPEOUT\n", StringComparison.Ordinal) && r.Contains("\n 91\n1\n"));

            // The wipeout comes out of the INSERT moved with the line, where it was left out as a type the library did not move.
            IReadOnlyList<DxfEntity> parts = insert.Explode();
            Assert.Equal(2, parts.Count);
            Near(new GeoPoint3(70, -10, 0), parts[0].GeometricExtents.Min);
            Near(new GeoPoint3(110, 20, 0), parts[0].GeometricExtents.Max);

            // The drawing of the tag inserted into another, placed 1 000 along x: the wipeout lands with its tag.
            var into = new DxfDatabase("AC1032");
            into.Insert(GeoTransform3.Translation(new GeoVector3(1000, 0, 0)), drawing, true);
            DxfEntity placed = into.ModelSpace.Single();
            Near(new GeoPoint3(1100, 0, 0), ((DxfBlockReference)placed).Position);

            var loose = new DxfDatabase("AC1032");
            loose.ModelSpace.AppendEntity(Of(source, "C14").Clone());
            into.Insert(GeoTransform3.Translation(new GeoVector3(0, 500, 0)), loose, true);
            GeoAabb3 box = into.ModelSpace.Last().GeometricExtents;
            Near(new GeoPoint3(-30, 490, 0), box.Min);
            Near(new GeoPoint3(10, 520, 0), box.Max);
        }

        [Fact]
        public void AnImageOrAWipeout_IsNotAddedToADrawingOfR12()
        {
            DxfDatabase source = DxfDatabase.Parse(DxfRasterImageDrawings.R2018);
            DxfDatabase r12 = DxfDatabase.Parse(DxfFileEzdxfDrawings.R12);

            Assert.Throws<ArgumentException>(() => r12.ModelSpace.AppendEntity(Of(source, "C10").Clone()));
            Assert.Throws<ArgumentException>(() => r12.ModelSpace.AppendEntity(Of(source, "C14").Clone()));
        }
    }
}
