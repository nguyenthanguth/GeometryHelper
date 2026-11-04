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
    /// What the review of IMAGE and WIPEOUT found: a wipeout framed at site coordinates, the extents of the header grown by
    /// an image appended or moved, the CLASS rows of an image's classes counted 0 raised, the inverted clip kept out of a
    /// drawing before R2010, a boundary closed in the world's units, and the box of an image clipped inverted.
    /// </summary>
    public class DxfRasterImageReviewTests
    {
        private static readonly double Close = 1E-6;

        private static GeoPoint2 Q(double x, double y) => new GeoPoint2(x, y);

        private static void Near(GeoPoint3 expected, GeoPoint3 actual)
            => Assert.True(expected.DistanceTo(actual) <= Close * Math.Max(1.0, expected.DistanceTo(new GeoPoint3(0, 0, 0))), expected + " was " + actual);

        private static string Record(string text, string handle) => Records(text).First(r => HandleOf(r) == handle);

        private static string ClassRecord(string text, string name)
            => Records(text).FirstOrDefault(r => r.StartsWith("  0\nCLASS\n", StringComparison.Ordinal) && r.Contains("\n  1\n" + name + "\n"));

        [Fact]
        public void AWipeoutSetFromAtSiteCoordinates_IsFramedWithTheOrigin_AndHidesItsPolygon()
        {
            DxfWipeout wipeout = ByHandle<DxfWipeout>(DxfDatabase.Parse(DxfRasterImageDrawings.R2018), "C14");
            var cases = new[]
            {
                new[] { Q(100000, 100000), Q(100010, 100000), Q(100010, 100010), Q(100000, 100010), Q(100000, 100000) },
                new[] { Q(500000, 6000000), Q(500020, 6000000), Q(500020, 6000005), Q(500000, 6000005), Q(500000, 6000000) },
            };

            foreach (GeoPoint2[] square in cases)
            {
                // The frame as AutoCAD 2021 lays it, on the box of the corners and the origin; the polygon judged by its own box.
                wipeout.SetFrom(square, new GeoVector3(0, 0, 1));
                IReadOnlyList<GeoPoint3> corners = wipeout.GetVertices();
                Assert.Equal(5, corners.Count);

                for (int i = 0; i < 5; i++)
                {
                    Near(new GeoPoint3(square[i].X, square[i].Y, 0), corners[i]);
                }

                wipeout.GetOrientation(out GeoPoint3 origin, out GeoVector3 u, out _);
                Near(new GeoPoint3(0, 0, 0), origin);
                Assert.Equal(Math.Max(square[2].X, square[2].Y), u.X, 6);
            }
        }

        [Fact]
        public void AnImageAppendedOrMoved_GrowsTheExtentsOfTheHeader_AsAnyEntityDoes()
        {
            DxfDatabase source = DxfDatabase.Parse(DxfRasterImageDrawings.R2018);
            var db = new DxfDatabase("AC1032");
            db.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 1, 0)));
            Near(new GeoPoint3(1, 1, 0), db.Extmax);

            // The picture 40 by 30 at (10, 20) copied to (1010, 1020): the header holds it to (1050, 1050).
            DxfEntity image = ByHandle<DxfEntity>(source, "C10").GetTransformedCopy(GeoTransform3.Translation(new GeoVector3(1000, 1000, 0)));
            db.ModelSpace.AppendEntity(image);
            Assert.Same(image, db.ModelSpace.Single(e => e.Handle == image.Handle));
            Near(new GeoPoint3(1050, 1050, 0), db.Extmax);
            Near(new GeoPoint3(0, 0, 0), db.Extmin);

            image.TransformBy(GeoTransform3.Translation(new GeoVector3(5000, 0, 0)));
            Near(new GeoPoint3(6050, 1050, 0), db.Extmax);

            DxfEntity mask = ByHandle<DxfEntity>(source, "C14").Clone();
            db.ModelSpace.AppendEntity(mask);
            Assert.Same(mask, db.ModelSpace.Single(e => e.Handle == mask.Handle));
            Near(new GeoPoint3(-30, -10, 0), db.Extmin);
        }

        [Fact]
        public void AnImageCopiedIntoADrawingThatCountsItsClass0_IsCountedOnce_SoAutoCadShowsIt()
        {
            // ezdxf 1.4.5 counts its classes 0 (91), and AutoCAD 2021 passes over the IMAGEs of a class counted 0.
            string text = DxfFileEzdxfDrawings.R2018;
            Assert.Contains("\n 91\n0\n", ClassRecord(text, "IMAGE"));
            DxfDatabase source = DxfDatabase.Parse(DxfRasterImageDrawings.R2018);
            DxfDatabase db = DxfDatabase.Parse(text);

            db.ModelSpace.AppendEntity(ByHandle<DxfEntity>(source, "C10").Clone());
            db.ModelSpace.AppendEntity(ByHandle<DxfEntity>(source, "C14").Clone());
            string after = db.ToText();

            Assert.Contains("\n 91\n1\n", ClassRecord(after, "IMAGE"));
            Assert.Contains("\n 91\n1\n", ClassRecord(after, "WIPEOUT"));

            // Only the rows of what was written are raised: no IMAGEDEF was brought, its own photo.jpg shown.
            Assert.Equal(ClassRecord(text, "IMAGEDEF"), ClassRecord(after, "IMAGEDEF"));
            Assert.Equal(ClassRecord(text, "IMAGEDEF_REACTOR"), ClassRecord(after, "IMAGEDEF_REACTOR"));

            // A drawing read and written untouched keeps its counts of 0.
            Assert.Equal(text, DxfDatabase.Parse(text).ToText());
        }

        [Fact]
        public void AnInvertedClipCopiedIntoADrawingBeforeR2010_IsLeftBehind_AndCanBeTakenOffThere()
        {
            DxfDatabase source = DxfDatabase.Parse(DxfRasterImageDrawings.R2018);
            ByHandle<DxfRasterImage>(source, "C11").IsClipInverted = true;
            DxfDatabase r2000 = DxfDatabase.Parse(DxfRasterImageDrawings.R2000);
            DxfEntity copy = ByHandle<DxfEntity>(source, "C11").Clone();

            r2000.ModelSpace.AppendEntity(copy);
            Assert.DoesNotContain("\n290\n", Record(r2000.ToText(), copy.Handle));
            Assert.False(((DxfRasterImage)copy).IsClipInverted);

            // An R2000 file another writer gave a 290 of 1 has it taken off by false, which needs no later version.
            string held = DxfRasterImageDrawings.R2000.Replace("  5\nC11\n", "  5\nC11\n").Replace(" 71\n2\n 91\n4\n 14\n0.0\n 24\n0.0\n 14\n3.0", "290\n1\n 71\n2\n 91\n4\n 14\n0.0\n 24\n0.0\n 14\n3.0");
            DxfDatabase odd = DxfDatabase.Parse(held);
            DxfRasterImage image = ByHandle<DxfRasterImage>(odd, "C11");
            Assert.True(image.IsClipInverted);
            image.IsClipInverted = false;
            Assert.False(image.IsClipInverted);
            Assert.DoesNotContain("\n290\n", Record(odd.ToText(), "C11"));
            Assert.Throws<InvalidOperationException>(() => image.IsClipInverted = true);
        }

        [Fact]
        public void ABoundaryIsClosedByItsLastCorner_ToThePointToleranceInTheWorld_NotInPixels()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfRasterImageDrawings.R2018);
            DxfWipeout wipeout = ByHandle<DxfWipeout>(db, "C14");
            wipeout.SetOrientation(new GeoPoint3(0, 0, 0), new GeoVector3(10000, 0, 0), new GeoVector3(0, 10000, 0));

            // On a frame of 10 000, a last corner 0.0009 of the frame off the first is 9 mm off: not closed.
            Assert.Throws<ArgumentException>(() => wipeout.SetClipBoundary(DxfClipBoundaryType.Poly, new[] { Q(-0.5, 0.5), Q(0.5, 0.5), Q(0, -0.5), Q(-0.4991, 0.5) }));

            // 0.0005 mm off, 5E-8 of the frame, it closes, written as the first.
            wipeout.SetClipBoundary(DxfClipBoundaryType.Poly, new[] { Q(-0.5, 0.5), Q(0.5, 0.5), Q(0, -0.5), Q(-0.49999995, 0.5) });
            IReadOnlyList<GeoPoint2> boundary = wipeout.GetClipBoundary();
            Assert.Equal(boundary[0], boundary[3]);
        }

        [Fact]
        public void AnImageClippedInverted_HasTheBoxOfItsFrame_AndTheVerticesAndLinesOfItsBoundary()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfRasterImageDrawings.R2018);
            DxfRasterImage image = ByHandle<DxfRasterImage>(db, "C11");
            image.IsClipInverted = true;

            // AutoCAD 2021: the triangle as its vertices and the three lines it explodes into, the frame's box as its extents.
            GeoAabb3 box = image.GeometricExtents;
            Near(new GeoPoint3(10, 20, 0), box.Min);
            Near(new GeoPoint3(50, 50, 0), box.Max);
            Assert.Equal(4, image.GetVertices().Count);
            Assert.Equal(3, image.Explode().Count);
        }

        [Fact]
        public void AnEntityOfATypeWithNoClass_IsCopiedIntoADrawingOfAnOlderVersion_AsItIs()
        {
            // A SHAPE, which R2000 holds too, kept raw: copied from a drawing of R2018 into one of R2000.
            string shape = "  0\nSHAPE\n  5\nC30\n330\n17\n100\nAcDbEntity\n  8\n0\n100\nAcDbShape\n 10\n1.0\n 20\n2.0\n 30\n0.0\n 40\n2.5\n  2\nBOX\n";
            string text = DxfFileEzdxfDrawings.R2018;
            int end = text.IndexOf("  0\nENDSEC\n", text.IndexOf("\nENTITIES\n", StringComparison.Ordinal), StringComparison.Ordinal);
            DxfDatabase source = DxfDatabase.Parse(text.Insert(end, shape));
            var raw = (DxfUnknownEntity)ByHandle<DxfEntity>(source, "C30");
            DxfDatabase target = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);

            DxfEntity clone = raw.Clone();
            Assert.IsType<DxfUnknownEntity>(clone);
            Assert.Throws<NotSupportedException>(() => raw.GetTransformedCopy(GeoTransform3.Translation(new GeoVector3(1, 0, 0))));
            target.ModelSpace.AppendEntity(clone);

            var reopened = (DxfUnknownEntity)DxfDatabase.Parse(target.ToText()).ModelSpace.Single(e => e.Handle == clone.Handle);
            Assert.Equal("SHAPE", reopened.DxfName);
            Assert.Contains(reopened.Groups, g => g.TypeCode == 2 && g.Value == "BOX");
        }
    }
}
