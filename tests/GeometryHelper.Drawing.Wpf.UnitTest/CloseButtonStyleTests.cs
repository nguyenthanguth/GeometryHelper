using System;
using System.IO.Packaging;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GeometryHelper.Dxf.Viewer.Wpf;
using Xunit;

namespace GeometryHelper.Drawing.Wpf.UnitTest
{
    /// <summary>The close cross of the model drawing's dialogs, on the orange-red title cross of the theme (design v70).</summary>
    public class CloseButtonStyleTests
    {
        [Fact]
        public void TheDialogsCloseCross_IsTheThemesOrangeRedTitleCross()
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    // Registers the pack scheme, which the dictionary is loaded through, with no Application running.
                    _ = PackUriHelper.UriSchemePack;
                    var dialogs = (ResourceDictionary)Application.LoadComponent(new Uri("/GeometryHelper.Drawing.Wpf;component/Themes/Dialogs.xaml", UriKind.Relative));
                    var style = (Style)dialogs["CloseButton"];
                    Assert.Same(dialogs["GhTitleCloseButton"], style.BasedOn);

                    var button = new Button { Style = style };
                    button.Measure(new Size(100, 100));
                    button.Arrange(new Rect(0, 0, 24, 24));
                    button.UpdateLayout();
                    Assert.Equal("Close", button.ToolTip);
                    Assert.Equal(24.0, button.ActualWidth);
                    IconView cross = Descendants(button).OfType<IconView>().Single();
                    Assert.Equal(Color.FromRgb(0xF2, 0x6B, 0x3A), ((SolidColorBrush)cross.Stroke).Color);
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            Assert.Null(failure);
        }

        private static System.Collections.Generic.IEnumerable<DependencyObject> Descendants(DependencyObject parent)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                yield return child;

                foreach (DependencyObject below in Descendants(child))
                {
                    yield return below;
                }
            }
        }
    }
}
