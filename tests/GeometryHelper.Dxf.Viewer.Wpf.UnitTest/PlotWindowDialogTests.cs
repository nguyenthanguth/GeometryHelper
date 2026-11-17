using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The Plot dialog's window, laid out on an STA thread and never shown: Preview… must leave the window shown while the
    /// preview is open. A window shown with ShowDialog that is hidden stops being a dialog (WPF ends its ShowDialog at
    /// once), and OK then fails with "DialogResult can be set only after Window is created and shown as dialog".
    /// </summary>
    public class PlotWindowDialogTests
    {
        private static void OnSta(Action action)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (failure != null)
            {
                throw new InvalidOperationException(failure.Message, failure);
            }
        }

        [Fact]
        public void PreviewOnThePlotDialog_DoesNotHideTheDialogWhileThePreviewIsOpen()
        {
            OnSta(() =>
            {
                CadDocument document = PlotPdfTests.Plan();
                var job = new CadPlotJob(
                    document.ModelSpace,
                    CadPlotSettings.Read(document, document.ModelSpace),
                    DisplayBox.Empty,
                    null
                );
                var window = new PlotWindow(new PlotDialogViewModel(document, job, false, null));
                // A window never shown reads Collapsed; what matters is that Preview… leaves it as it was.
                Visibility before = window.Visibility;
                Visibility? seen = null;
                bool asked = false;
                window.Model.ShowPreview = preview =>
                {
                    asked = true;
                    seen = window.Visibility;
                    return false;
                };

                var button = (ButtonBase)FindByContent(window.Body, "Preview…");
                button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

                Assert.True(asked);
                Assert.Equal(before, seen);
                Assert.Equal(before, window.Visibility);
            });
        }

        private static DependencyObject FindByContent(DependencyObject parent, string content)
        {
            foreach (object child in LogicalTreeHelper.GetChildren(parent))
            {
                if (child is ContentControl found && Equals(found.Content, content))
                {
                    return found;
                }

                if (child is DependencyObject each && FindByContent(each, content) is DependencyObject match)
                {
                    return match;
                }
            }

            return null;
        }
    }
}
