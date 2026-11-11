using System;
using System.IO.Packaging;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// A disabled button of a dialog (GhDialogButton) is dimmed once, as every flat button of the theme is: its words at 45
    /// per cent, not muted as well, which left them at about a quarter of their strength.
    /// </summary>
    public sealed class DisabledDialogButtonTests
    {
        [Fact]
        public void ADisabledDialogButton_IsDimmedOnce_ItsWordsAtFortyFivePerCentAndNotMutedToo()
        {
            OnSta(() =>
            {
                // Registers the pack scheme, which the theme is loaded through, with no Application running.
                _ = PackUriHelper.UriSchemePack;
                var theme = (ResourceDictionary)
                    Application.LoadComponent(
                        new Uri("/GeometryHelper.Dxf.Viewer.Wpf;component/Themes/Dark.xaml", UriKind.Relative)
                    );
                Color text = ((SolidColorBrush)theme["GhText"]).Color;
                var button = new Button
                {
                    Style = (Style)theme["GhDialogButton"],
                    Content = "Apply",
                    IsEnabled = false,
                };
                button.Measure(new Size(400, 100));
                button.Arrange(new Rect(0, 0, 400, 100));
                button.UpdateLayout();

                ContentPresenter content = Descendants(button).OfType<ContentPresenter>().Single();
                Assert.Equal(0.45, content.Opacity, 9);
                Assert.Equal(text, ((SolidColorBrush)button.Foreground).Color);

                button.IsEnabled = true;
                button.UpdateLayout();
                Assert.Equal(1.0, content.Opacity, 9);
                Assert.Equal(text, ((SolidColorBrush)button.Foreground).Color);
            });
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

        private static void OnSta(Action test)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    test();
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
    }
}
