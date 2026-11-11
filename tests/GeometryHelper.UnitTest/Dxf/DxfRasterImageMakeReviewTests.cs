using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// What the review of the images made in code found: a copy associated with a definition of the drawing it goes into, a
    /// wipeout copied as another writer left it, a definition added under a name taken, and the extents of an image worked
    /// out in one reading of its record, to the bit as before.
    /// </summary>
    public class DxfRasterImageMakeReviewTests
    {
        private static string Record(string text, string handle) => Records(text).First(r => HandleOf(r) == handle);

        private static string Group(string record, int code)
        {
            string[] lines = record.Split('\n');

            for (int i = 0; i + 1 < lines.Length; i += 2)
            {
                if (lines[i].Trim() == code.ToString(CultureInfo.InvariantCulture))
                {
                    return lines[i + 1].Trim();
                }
            }

            return null;
        }

        private static DxfRasterImageDef Definition(DxfDatabase db, string path, double across, double up)
        {
            DxfDBDictionary images = DxfRasterImageDef.CreateImageDictionary(db);
            var definition = new DxfRasterImageDef(path, new GeoVector2(across, up));
            images.SetAt(DxfRasterImageDef.SuggestName(images, path), definition);
            return definition;
        }

        [Fact]
        public void ACopyAssociatedWithADefinitionOfTheDrawingItGoesInto_ShowsIt_WithAReactorOfItsOwn()
        {
            var one = new DxfDatabase("AC1032");
            DxfRasterImageDef small = Definition(one, @"C:\pictures\px43.png", 4, 3);
            var image = new DxfRasterImage(small);
            one.ModelSpace.AppendEntity(image);
            var two = new DxfDatabase("AC1032");
            Definition(two, @"C:\pictures\first.png", 2, 2);
            DxfRasterImageDef big = Definition(two, @"C:\pictures\big.png", 8, 6);

            var copy = (DxfRasterImage)image.Clone();
            copy.AssociateRasterDef(big);
            two.ModelSpace.AppendEntity(copy);
            string text = two.ToText();
            string record = Record(text, copy.Handle);

            Assert.Equal(big.Handle, Group(record, 340));
            string reactor = Group(record, 360);
            Assert.StartsWith("  0\nIMAGEDEF_REACTOR\n", Record(text, reactor), StringComparison.Ordinal);
            Assert.Equal(1, big.GetEntityCount(out _));
            Assert.DoesNotContain("px43.png", text, StringComparison.Ordinal);
            Assert.Single(Records(text), r => r.StartsWith("  0\nIMAGEDEF_REACTOR\n", StringComparison.Ordinal));
        }

        [Fact]
        public void ACopyAssociatedWithADefinitionOfTheSameHandleAsOneOfItsOwnDrawing_ShowsTheOneOfItsNewDrawing()
        {
            // Two drawings made alike give their first definitions the same handle: the copy's 340 is the new drawing's, not
            // the namesake of the old one's, and the old one's picture is not brought.
            var one = new DxfDatabase("AC1032");
            DxfRasterImageDef small = Definition(one, @"C:\pictures\px43.png", 4, 3);
            var image = new DxfRasterImage(small);
            one.ModelSpace.AppendEntity(image);
            var two = new DxfDatabase("AC1032");
            DxfRasterImageDef other = Definition(two, @"C:\pictures\other.png", 8, 6);
            Assert.Equal(small.Handle, other.Handle);

            var copy = (DxfRasterImage)image.Clone();
            copy.AssociateRasterDef(other);
            two.ModelSpace.AppendEntity(copy);
            string text = two.ToText();

            Assert.Equal(other.Handle, Group(Record(text, copy.Handle), 340));
            Assert.Same(other, copy.ImageDef);
            Assert.Equal(1, other.GetEntityCount(out _));
            Assert.DoesNotContain("px43.png", text, StringComparison.Ordinal);
            Assert.Single(Records(text), r => r.StartsWith("  0\nIMAGEDEF\n", StringComparison.Ordinal));
            Assert.Single(Records(text), r => r.StartsWith("  0\nIMAGEDEF_REACTOR\n", StringComparison.Ordinal));
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void ACopyOfACopyAssociatedWithADefinitionOfTheDrawingItGoesInto_ShowsIt_ClonedOrMoved(
            bool moved,
            bool sameHandle
        )
        {
            // The copy of the copy keeps the definition AssociateRasterDef gave, whether or not the drawing it came from has
            // one of the same handle.
            var one = new DxfDatabase("AC1032");
            DxfRasterImageDef small = Definition(one, @"C:\pictures\px43.png", 4, 3);
            var image = new DxfRasterImage(small);
            one.ModelSpace.AppendEntity(image);
            var two = new DxfDatabase("AC1032");

            if (!sameHandle)
            {
                Definition(two, @"C:\pictures\first.png", 2, 2);
            }

            DxfRasterImageDef big = Definition(two, @"C:\pictures\big.png", 8, 6);
            Assert.Equal(sameHandle, small.Handle == big.Handle);

            var copy = (DxfRasterImage)image.Clone();
            copy.AssociateRasterDef(big);
            var again = (DxfRasterImage)(
                moved ? copy.GetTransformedCopy(GeoTransform3.Translation(new GeoVector3(0, 100, 0))) : copy.Clone()
            );
            two.ModelSpace.AppendEntity(again);
            string text = two.ToText();

            Assert.Equal(big.Handle, Group(Record(text, again.Handle), 340));
            Assert.Same(big, again.ImageDef);
            Assert.Equal(1, big.GetEntityCount(out _));
            Assert.DoesNotContain("px43.png", text, StringComparison.Ordinal);
            Assert.Single(Records(text), r => r.StartsWith("  0\nIMAGEDEF_REACTOR\n", StringComparison.Ordinal));
        }

        [Fact]
        public void AWipeoutCopiedAsAnotherWriterLeftIt_OfFourCornersNotClosed_IsAppendedAndInserted()
        {
            // C14 with its closing corner left out, 91 of 4, as another writer may hold it: read, moved, copied and inserted.
            string text = DxfRasterImageDrawings.R2018;
            int at = text.IndexOf("  5\nC14\n", StringComparison.Ordinal);
            int end = text.IndexOf("  0\nIMAGE\n", at, StringComparison.Ordinal);
            string record = text.Substring(at, end - at).Replace(" 91\n5\n", " 91\n4\n");
            record = record.Substring(0, record.LastIndexOf(" 14\n-0.5\n 24\n0.5\n", StringComparison.Ordinal));
            DxfDatabase source = DxfDatabase.Parse(text.Substring(0, at) + record + text.Substring(end));
            var held = ByHandle<DxfWipeout>(source, "C14");
            Assert.Equal(4, held.GetClipBoundary().Count);
            Assert.Equal(5, held.GetVertices().Count);

            var into = new DxfDatabase("AC1032");
            into.ModelSpace.AppendEntity(held.Clone());
            Assert.Equal(new GeoPoint3(-30, -10, 0), into.ModelSpace.OfType<DxfWipeout>().Single().GetVertices()[0]);

            var alone = new DxfDatabase("AC1032");
            alone.ModelSpace.AppendEntity(held.Clone());
            var placed = new DxfDatabase("AC1032");
            placed.Insert(GeoTransform3.Translation(new GeoVector3(100, 0, 0)), alone, true);
            Assert.Equal(new GeoPoint3(70, -10, 0), placed.ModelSpace.OfType<DxfWipeout>().Single().GetVertices()[0]);

            // One of no boundary, which AutoCAD 2021 crashes opening, is refused all the same.
            string none =
                text.Substring(0, at)
                + text.Substring(at, end - at).Replace(" 71\n2\n 91\n5\n", " 71\n0\n 91\n0\n")
                + text.Substring(end);
            DxfWipeout broken = ByHandle<DxfWipeout>(
                DxfDatabase.Parse(
                    none.Replace(
                        " 14\n-0.5\n 24\n0.5\n 14\n0.5\n 24\n0.5\n 14\n0.5\n 24\n-0.25\n 14\n-0.5\n 24\n-0.25\n 14\n-0.5\n 24\n0.5\n",
                        string.Empty
                    )
                ),
                "C14"
            );
            Assert.Throws<InvalidOperationException>(() =>
                new DxfDatabase("AC1032").ModelSpace.AppendEntity(broken.Clone())
            );
        }

        // The drawing with the record of a handle changed.
        private static string WithRecord(string text, string handle, Func<string, string> edit)
        {
            int at = text.IndexOf("  5\n" + handle + "\n", StringComparison.Ordinal);
            int end = text.IndexOf("  0\n", at, StringComparison.Ordinal);
            return text.Substring(0, at) + edit(text.Substring(at, end - at)) + text.Substring(end);
        }

        // A record with its last corner left out and its count one less.
        private static string LastCornerOut(string record, int count, string corner)
        {
            record = record.Replace(
                " 91\n" + count.ToString(CultureInfo.InvariantCulture) + "\n",
                " 91\n" + (count - 1).ToString(CultureInfo.InvariantCulture) + "\n"
            );
            int last = record.LastIndexOf(corner, StringComparison.Ordinal);
            return record.Substring(0, last) + record.Substring(last + corner.Length);
        }

        private static string Corners(DxfRasterImage image) => string.Join(" ", image.GetVertices().Select(Bits));

        [Fact]
        public void AWipeoutCopiedOfAPolygonNotClosed_IsWrittenClosed_TheDrawingItCameFromWrittenAsItWas()
        {
            // C14 with its closing corner left out, 91 of 4: AutoCAD 2021 reads such a wipeout as of no corners and shows
            // nothing, and the library reads it closed; its copies are written closed, its first corner again at the end.
            string text = WithRecord(DxfRasterImageDrawings.R2018, "C14", r =>
                LastCornerOut(r, 5, " 14\n-0.5\n 24\n0.5\n")
            );
            const string Closed =
                " 91\n5\n 14\n-0.5\n 24\n0.5\n 14\n0.5\n 24\n0.5\n 14\n0.5\n 24\n-0.25\n 14\n-0.5\n 24\n-0.25\n 14\n-0.5\n 24\n0.5\n";
            DxfDatabase source = DxfDatabase.Parse(text);
            Assert.Equal(text, source.ToText());
            var held = ByHandle<DxfWipeout>(source, "C14");

            var into = new DxfDatabase("AC1032");
            var copy = (DxfWipeout)held.Clone();
            into.ModelSpace.AppendEntity(copy);
            Assert.Contains(Closed, Record(into.ToText(), copy.Handle), StringComparison.Ordinal);
            Assert.Equal(Corners(held), Corners(copy));

            // The drawing holds a MESH, which the library moves not: inserted where it is.
            var placed = new DxfDatabase("AC1032");
            placed.Insert(GeoTransform3.Identity, source, true);
            Assert.Contains(
                Closed,
                Record(placed.ToText(), placed.ModelSpace.OfType<DxfWipeout>().Single().Handle),
                StringComparison.Ordinal
            );

            DxfEntity moved = held.GetTransformedCopy(GeoTransform3.Translation(new GeoVector3(0, 100, 0)));
            source.ModelSpace.AppendEntity(moved);
            string after = source.ToText();
            Assert.Contains(Closed, Record(after, moved.Handle), StringComparison.Ordinal);
            Assert.Equal(Record(text, "C14"), Record(after, "C14"));
        }

        [Fact]
        public void AnImageCopiedOfAPolygonNotClosed_OrEndingAMillionthOfAPixelOffItsFirstCorner_IsWrittenClosed()
        {
            // C11's triangle with its closing corner left out, and with it moved 1E-6 pixel along x: AutoCAD 2021 crashes opening
            // a drawing that holds either. The copies end on the first corner, to the bit.
            string open = WithRecord(DxfRasterImageDrawings.R2018, "C11", r =>
                LastCornerOut(r, 4, " 14\n0.0\n 24\n0.0\n")
            );
            var held = ByHandle<DxfRasterImage>(DxfDatabase.Parse(open), "C11");
            var into = new DxfDatabase("AC1032");
            var copy = (DxfRasterImage)held.Clone();
            into.ModelSpace.AppendEntity(copy);
            Assert.Contains(
                " 91\n4\n 14\n0.0\n 24\n0.0\n 14\n3.0\n 24\n0.0\n 14\n1.5\n 24\n2.5\n 14\n0.0\n 24\n0.0\n",
                Record(into.ToText(), copy.Handle),
                StringComparison.Ordinal
            );
            Assert.Equal(Corners(held), Corners(copy));

            string near = WithRecord(DxfRasterImageDrawings.R2018, "C11", r =>
            {
                int last = r.LastIndexOf(" 14\n0.0\n", StringComparison.Ordinal);
                return r.Substring(0, last) + " 14\n0.000001\n" + r.Substring(last + " 14\n0.0\n".Length);
            });
            var nearly = (DxfRasterImage)ByHandle<DxfRasterImage>(DxfDatabase.Parse(near), "C11").Clone();
            into.ModelSpace.AppendEntity(nearly);
            Assert.Contains(
                " 91\n5\n 14\n0.0\n 24\n0.0\n 14\n3.0\n 24\n0.0\n 14\n1.5\n 24\n2.5\n 14\n0.000001\n 24\n0.0\n 14\n0.0\n 24\n0.0\n",
                Record(into.ToText(), nearly.Handle),
                StringComparison.Ordinal
            );

            // One closed is written as it is.
            var closed = (DxfRasterImage)
                ByHandle<DxfRasterImage>(DxfDatabase.Parse(DxfRasterImageDrawings.R2018), "C11").Clone();
            into.ModelSpace.AppendEntity(closed);
            Assert.Contains(
                " 91\n4\n 14\n0.0\n 24\n0.0\n 14\n3.0\n 24\n0.0\n 14\n1.5\n 24\n2.5\n 14\n0.0\n 24\n0.0\n",
                Record(into.ToText(), closed.Handle),
                StringComparison.Ordinal
            );
        }

        [Fact]
        public void AnImageOfAPolygonNotClosed_GivenAnotherDefinition_IsWrittenClosed()
        {
            // AssociateRasterDef writes the boundary anew, scaled to the new picture: closed, as AutoCAD 2021 cannot open one not.
            string open = WithRecord(DxfRasterImageDrawings.R2018, "C11", r =>
                LastCornerOut(r, 4, " 14\n0.0\n 24\n0.0\n")
            );
            DxfDatabase db = DxfDatabase.Parse(open);
            var image = ByHandle<DxfRasterImage>(db, "C11");
            DxfDBDictionary images = DxfRasterImageDef.GetImageDictionary(db);
            var big = new DxfRasterImageDef(@"C:\pictures\big.png", new GeoVector2(8, 6));
            images.SetAt(DxfRasterImageDef.SuggestName(images, big.SourceFileName), big);

            image.AssociateRasterDef(big);

            Assert.Contains(" 91\n4\n", Record(db.ToText(), "C11"), StringComparison.Ordinal);
            IReadOnlyList<GeoPoint2> boundary = image.GetClipBoundary();
            Assert.Equal(4, boundary.Count);
            Assert.Equal(Bits(boundary[0].X) + Bits(boundary[0].Y), Bits(boundary[3].X) + Bits(boundary[3].Y));
            Assert.Equal(Bits(new GeoPoint3(15, 45, 0)), Bits(image.GetVertices()[0]));
        }

        [Fact]
        public void ADefinitionAddedUnderANameTaken_IsRefused_TheOneThereAndItsImagesKept()
        {
            var db = new DxfDatabase("AC1032");
            DxfRasterImageDef first = Definition(db, @"C:\pictures\px43.png", 4, 3);
            var image = new DxfRasterImage(first);
            db.ModelSpace.AppendEntity(image);
            DxfDBDictionary images = DxfRasterImageDef.GetImageDictionary(db);
            string before = db.ToText();

            var again = new DxfRasterImageDef(@"D:\other\px43.png", new GeoVector2(8, 6));
            Assert.Contains(
                "SuggestName",
                Assert.Throws<ArgumentException>(() => images.SetAt("PX43", again)).Message,
                StringComparison.Ordinal
            );
            Assert.Equal(before, db.ToText());
            Assert.Same(first, images.GetAt("px43"));
            Assert.False(again.IsResident);
            images.SetAt(DxfRasterImageDef.SuggestName(images, again.SourceFileName), again);
            Assert.Same(again, images.GetAt("px43_1"));
        }

        // The extents and vertices of the images below, as the bits of each double, worked out by the reading of the record
        // group by group before the one-pass reading: the one pass gives them to the bit.
        private static readonly string[] Golden =
        {
            "40244a3d70a3d70a,c01e000000000000,400a000000000000 404a38f5c28f5c29,4039b33333333333,4014333333333334 |4028b0a3d70a3d71,c01e000000000000,400a000000000000 40244a3d70a3d70a,4036666666666666,4011666666666666 40491f5c28f5c28f,4039b33333333333,4014333333333334 404a38f5c28f5c29,c010cccccccccccd,400f99999999999a 4028b0a3d70a3d71,c01e000000000000,400a000000000000",
            "402f0c5f92c5f92d,c02357e4b17e4b18,400be81b4e81b4e8 4048570a3d70a3d7,40338e147ae147ae,40132e147ae147ae |402f0c5f92c5f92d,4031d44444444444,4011044444444444 4048570a3d70a3d7,40338e147ae147ae,40132e147ae147ae 404158bf258bf25a,c02357e4b17e4b18,400be81b4e81b4e8 402f0c5f92c5f92d,4031d44444444444,4011044444444444",
            "4032d33333333334,c00eccccccccccd0,400c000000000000 40481b851eb851ec,4030666666666666,401299999999999a |4032d33333333334,402c2e147ae147ae,4010a3d70a3d70a4 4034251eb851eb86,c00eccccccccccd0,400c000000000000 40481b851eb851ec,bff8a3d70a3d70ac,400feb851eb851eb 4047728f5c28f5c3,4030666666666666,401299999999999a 4032d33333333334,402c2e147ae147ae,4010a3d70a3d70a4",
            "40244a3d70a3d70a,c01e000000000000,400a000000000000 404a38f5c28f5c29,4039b33333333333,4014333333333334 |402f0c5f92c5f92d,4031d44444444444,4011044444444444 4048570a3d70a3d7,40338e147ae147ae,40132e147ae147ae 404158bf258bf25a,c02357e4b17e4b18,400be81b4e81b4e8 402f0c5f92c5f92d,4031d44444444444,4011044444444444",
            "40244a3d70a3d70a,c01e000000000000,400a000000000000 404a38f5c28f5c29,4039b33333333333,4014333333333334 |4028b0a3d70a3d71,c01e000000000000,400a000000000000 40244a3d70a3d70a,4036666666666666,4011666666666666 40491f5c28f5c28f,4039b33333333333,4014333333333334 404a38f5c28f5c29,c010cccccccccccd,400f99999999999a 4028b0a3d70a3d71,c01e000000000000,400a000000000000",
            "4015a3b490174094,40250aca926f3278,c024b2081588ed9f 4044e15812b5c0ef,404a01ef24314284,401e97b28d48536c |4015a3b490174094,403a7cb2c2bc56dc,401e97b28d48536c 404325b4c3abe12c,404a01ef24314284,c00e204a47206c18 4044e15812b5c0ef,40250aca926f3278,c024b2081588ed9f 4015a3b490174094,403a7cb2c2bc56dc,401e97b28d48536c",
            "c0d75def60f45f94,41083d1c2c496b04,40f86a253382368c c0d7569dce901730,41083e04b000ab6a,40f86aca34701dda |c0d75c8109b88218,41083d1c2c496b04,40f86a253382368c c0d7569dce901730,41083db44d1803dd,40f86a2f3390a1a8 c0d75a4220040480,41083e04b000ab6a,40f86aca34701dda c0d75def60f45f94,41083d7de57e0fee,40f86a9f34321e15 c0d75c8109b88218,41083d1c2c496b04,40f86a253382368c",
            "c034000000000000,c024000000000000,0 4024000000000000,403e000000000000,0 |c034000000000000,403e000000000000,0 c034000000000000,c024000000000000,0 4024000000000000,c024000000000000,0 4024000000000000,403e000000000000,0 c034000000000000,403e000000000000,0",
        };

        [Fact]
        public void TheBoxAndCornersOfImagesAndWipeouts_AreToTheBitThoseOfTheReadingGroupByGroup()
        {
            var db = new DxfDatabase("AC1032");
            DxfDBDictionary images = DxfRasterImageDef.CreateImageDictionary(db);
            var definition = new DxfRasterImageDef(@"C:\pictures\px43.png", new GeoVector2(4, 3));
            images.SetAt("px43", definition);
            var made = new List<DxfRasterImage>();

            DxfRasterImage Placed()
            {
                var image = new DxfRasterImage(definition);
                image.SetOrientation(
                    new GeoPoint3(12.345, -7.5, 3.25),
                    new GeoVector3(40.1, 3.3, 0.7),
                    new GeoVector3(-2.2, 29.9, 1.1)
                );
                db.ModelSpace.AppendEntity(image);
                made.Add(image);
                return image;
            }

            GeoPoint2[] triangle =
            {
                new GeoPoint2(0, 0),
                new GeoPoint2(3.3, 0.1),
                new GeoPoint2(1.7, 2.9),
                new GeoPoint2(0, 0),
            };
            Placed();
            Placed().SetClipBoundary(DxfClipBoundaryType.Poly, triangle);
            Placed()
                .SetClipBoundary(
                    DxfClipBoundaryType.Rectangle,
                    new[]
                    {
                        new GeoPoint2(0.3, 0.4),
                        new GeoPoint2(0.3, 2.2),
                        new GeoPoint2(3.1, 2.2),
                        new GeoPoint2(3.1, 0.4),
                        new GeoPoint2(0.3, 0.4),
                    }
                );
            DxfRasterImage inverted = Placed();
            inverted.SetClipBoundary(DxfClipBoundaryType.Poly, triangle);
            inverted.IsClipInverted = true;
            DxfRasterImage off = Placed();
            off.SetClipBoundary(DxfClipBoundaryType.Poly, triangle);
            off.IsClipped = false;
            DxfRasterImage turned = Placed();
            turned.SetClipBoundary(DxfClipBoundaryType.Poly, triangle);
            turned.TransformBy(
                GeoTransform3.RotationAxis(new GeoPoint3(5, -3, 2), new GeoVector3(1, 2, 3), 0.7)
                    * GeoTransform3.Scaling(new GeoPoint3(1, 1, 1), 1.3)
            );
            var wipeout = new DxfWipeout();
            wipeout.SetFrom(
                new[]
                {
                    new GeoPoint2(100000.5, 200000.25),
                    new GeoPoint2(100030.75, 200001.5),
                    new GeoPoint2(100025.125, 200020.875),
                    new GeoPoint2(100003.25, 200015.5),
                    new GeoPoint2(100000.5, 200000.25),
                },
                new GeoVector3(0.3, -0.4, 0.866)
            );
            db.ModelSpace.AppendEntity(wipeout);
            made.Add(wipeout);
            var square = new DxfWipeout();
            square.SetFrom(
                new[]
                {
                    new GeoPoint2(-30, -10),
                    new GeoPoint2(10, -10),
                    new GeoPoint2(10, 20),
                    new GeoPoint2(-30, 20),
                    new GeoPoint2(-30, -10),
                },
                new GeoVector3(0, 0, 1)
            );
            db.ModelSpace.AppendEntity(square);
            square.SetClipBoundary(
                DxfClipBoundaryType.Rectangle,
                new[]
                {
                    new GeoPoint2(-0.25, 0.5),
                    new GeoPoint2(-0.25, -0.5),
                    new GeoPoint2(0.5, -0.5),
                    new GeoPoint2(0.5, 0.5),
                    new GeoPoint2(-0.25, 0.5),
                }
            );
            made.Add(square);

            Assert.Equal(Golden.Length, made.Count);

            for (int i = 0; i < made.Count; i++)
            {
                GeoAabb3 box = made[i].GeometricExtents;
                string corners = string.Join(" ", made[i].GetVertices().Select(Bits));
                Assert.Equal(Golden[i], Bits(box.Min) + " " + Bits(box.Max) + " |" + corners);
            }
        }
    }
}
