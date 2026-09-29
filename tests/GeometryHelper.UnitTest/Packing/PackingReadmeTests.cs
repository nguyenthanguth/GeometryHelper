using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Geometry;
using GeometryHelper.Packing;
using Xunit;

namespace GeometryHelper.UnitTest.Packing
{
    /// <summary>
    /// Runs the code shown in docs/packing.md and checks what it says, so that renaming or removing an API breaks the
    /// build rather than leaving a reader with instructions that cannot work.
    /// </summary>
    public class PackingReadmeTests
    {
        /// <summary>A drawing view, as the guide moves one: its origin, and its box in the same coordinates.</summary>
        private sealed class View
        {
            public GeoPoint2 Origin { get; set; }

            public GeoRectangle2 Box { get; set; }
        }

        [Fact]
        public void QuickStart_KeepsTheViewAndItsSectionsTogether()
        {
            var plan = new GeoRectangle2(-900.0, 400.0, 180.0, 120.0);
            var sectionA = new GeoRectangle2(-600.0, 400.0, 60.0, 120.0);
            var sectionB = new GeoRectangle2(-500.0, 400.0, 60.0, 120.0);
            var detail = new GeoRectangle2(-300.0, 400.0, 90.0, 60.0);

            var sheet = new Sheet(PaperSize.A3)          // 420 x 297 mm, landscape
            {
                OffsetLeft   = 20.0,
                OffsetRight  = 10.0,
                OffsetTop    = 10.0,
                OffsetBottom = 10.0,
            };

            var groups = new List<GeoRectangle2[]>
            {
                new[] { plan, sectionA, sectionB },      // a view and its sections: kept together
                new[] { detail },
            };

            PackResult result = SheetPacker.Pack(groups, sheet, new PackOptions { Spacing = 5.0, GroupSpacing = 20.0 });

            PackPlacement placement = result.Placements[0][1];   // sectionA: the same shape as the groups given
            Assert.True(placement.Placed);
            Assert.Equal(0, placement.SheetIndex);
            Assert.Equal(sectionA.Translate(placement.Translation), placement.ViewBox);

            // The view, the first of its group, at the upper left of the usable area.
            Assert.True(result.Placements[0][0].ViewBox.UpperLeft.IsEqualTo(new GeoPoint2(20.0, 287.0)));
            Assert.All(result.Placements.SelectMany(g => g), p => Assert.True(p.Placed));
        }

        [Fact]
        public void TheSheet_AtAScale_PlacedByACorner()
        {
            var sheet = new Sheet(PaperSize.A1) { Scale = 50.0 };
            sheet.PlaceCorner(SheetCorner.UpperLeft, new GeoPoint2(0.0, 0.0));   // its upper left corner at the origin

            SheetFrame second = sheet.GetFrame(1);   // the next sheet: to the right of the first
            GeoPoint2 corner = second.Bounds.LowerLeft;                  // (42050, -29700)
            GeoRectangle2 usable = second.UsableArea;                    // the part inside the offsets

            Assert.Equal(42050.0, sheet.Width);
            Assert.Equal(29700.0, sheet.Height);
            Assert.True(corner.IsEqualTo(new GeoPoint2(42050.0, -29700.0)));
            Assert.Equal(second.Bounds, usable);
        }

        /// <summary>
        /// Adding the translation to any point of a view moves it where its box goes: here its origin, a point of its
        /// own away from the corners of its box.
        /// </summary>
        [Fact]
        public void MovingTheViews_ByTheirTranslations()
        {
            var views = new List<View>
            {
                new View { Origin = new GeoPoint2(-930.0, 350.0), Box = new GeoRectangle2(-900.0, 400.0, 180.0, 120.0) },
                new View { Origin = new GeoPoint2(-570.0, 410.0), Box = new GeoRectangle2(-600.0, 400.0, 60.0, 120.0) },
            };
            PackResult result = SheetPacker.Pack(new List<GeoRectangle2[]> { views.Select(v => v.Box).ToArray() }, new Sheet(PaperSize.A3));

            int index = 0;
            foreach (View view in views)   // the views of one drawing, their boxes in the same order
            {
                PackPlacement placement = result.Placements[0][index++];
                view.Origin = view.Origin + placement.Translation;
            }

            // Each origin stands to its box, moved, as it stood to the box given.
            for (int i = 0; i < views.Count; i++)
            {
                GeoVector2 before = new GeoPoint2(-930.0 + 360.0 * i, 350.0 + 60.0 * i).GetVectorTo(new GeoPoint2(-900.0 + 300.0 * i, 400.0));
                GeoVector2 after = views[i].Origin.GetVectorTo(result.Placements[0][i].ViewBox.LowerLeft);
                Assert.True(before.IsEqualTo(after), $"view {i}: {before} and {after}");
            }
        }
    }
}
