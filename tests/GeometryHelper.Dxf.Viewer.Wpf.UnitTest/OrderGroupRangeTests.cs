using System.Globalization;
using System.Linq;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>Order Group (design OrderGroup): the ranges of its boxes follow the group's size and the positions typed, and Highlight steps.</summary>
    public class OrderGroupRangeTests
    {
        /// <summary>A new drawing with a group STAIR_CORE of twelve lines and a group TITLE of one, Object Grouping open on STAIR_CORE.</summary>
        private static GroupManagerViewModel StairCore(MainViewModel model)
        {
            model.NewDrawing();

            for (int i = 0; i < 12; i++)
            {
                string x = (i * 100).ToString(CultureInfo.InvariantCulture);
                model.Execute("LINE");
                model.Execute(x + ",0");
                model.Execute(x + ",500");
                model.Execute(string.Empty);
            }

            foreach (string line in new[] { "-GROUP", string.Empty, "STAIR_CORE", "Stair core walls and landing", "ALL", string.Empty })
            {
                model.Execute(line);
            }

            model.Execute("LINE");
            model.Execute("0,1000");
            model.Execute("500,1000");
            model.Execute(string.Empty);

            foreach (string line in new[] { "-GROUP", string.Empty, "TITLE", "Title", "L", string.Empty })
            {
                model.Execute(line);
            }

            GroupManagerViewModel shown = null;
            model.ShowGroupManager = d => { shown = (GroupManagerViewModel)d; return true; };
            model.Execute("CLASSICGROUP");
            shown.Selected = shown.Groups.First(g => g.Name == "STAIR_CORE");
            return shown;
        }

        [Fact]
        public void TwelveObjectsMovedFromThreeToNought_ReadZeroToEleven_AndOneToNine_AndTheRangesFollowTheGroupChosen()
        {
            var model = new MainViewModel();
            var order = new OrderGroupViewModel(StairCore(model).Request);

            Assert.Equal(new[] { "STAIR_CORE", "TITLE" }, order.Groups);
            Assert.Equal("STAIR_CORE", order.SelectedGroup);
            Assert.Equal("Stair core walls and landing", order.Description);
            Assert.Equal("Remove from position (0-11):", order.RemoveFromLabel);
            Assert.Equal("Enter new position number for the object (0-11):", order.NewPositionLabel);
            Assert.Equal("Number of objects (1-12):", order.CountLabel);

            order.RemoveFrom = "3";
            order.NewPosition = "0";

            Assert.Equal(9, order.MostObjects);
            Assert.Equal("Number of objects (1-9):", order.CountLabel);
            Assert.StartsWith("STAIR_CORE has 12 objects, numbered 0 to 11", order.Summary);

            // The group of one: every range shrinks to it.
            order.SelectedGroup = "TITLE";

            Assert.Equal(0, order.LastPosition);
            Assert.Equal("Remove from position (0-0):", order.RemoveFromLabel);
            Assert.Equal("Title", order.Description);
            Assert.StartsWith("TITLE has 1 object, numbered 0 to 0", order.Summary);
        }

        [Fact]
        public void HighlightClosesBothDialogs_ForTheCommandLineToStepThroughTheMembers()
        {
            var model = new MainViewModel();
            GroupManagerViewModel grouping = StairCore(model);
            grouping.ShowOrder = order => order.Highlight();

            Assert.True(grouping.Reorder());
            Assert.Equal("Order Highlight", grouping.Request.Pick);
        }
    }
}
