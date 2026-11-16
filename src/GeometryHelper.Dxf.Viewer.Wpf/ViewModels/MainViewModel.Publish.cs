using System;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// PUBLISH and -PUBLISH shown through a function the workspace sets, so that a test drives them with none: the Publish dialog
    /// over the sheet list of the drawing open, and the sheet list published by the presenter's writers (<see cref="CadPublisher"/>),
    /// the PDFs through <see cref="CadPdfWriter"/> and the sheets for a printer through the printers of Windows. With no function
    /// set, PUBLISH asks -PUBLISH's question on the command line.
    /// </summary>
    public sealed partial class MainViewModel
    {
        /// <summary>The options of the last Publish dialog closed with Publish, which the next one opens on, as AutoCAD keeps them.</summary>
        private CadPublishOptions _publishOptions;

        /// <summary>Gets or sets how the workspace shows the Publish dialog: true when Publish was pressed.</summary>
        public Func<PublishViewModel, bool> ShowPublish { get; set; }

        /// <summary>
        /// PUBLISH's and -PUBLISH's request: for PUBLISH the dialog over the list, then the sheets published; for -PUBLISH the
        /// list of the file published at once. What was done is told on the command line, a line each.
        /// </summary>
        private void OnPublishRequested(object sender, CadUiRequestEventArgs e)
        {
            var list = e.Payload as CadPublishList;
            var session = sender as CadCommandSession;

            if (list == null || session == null)
            {
                return;
            }

            if (e.Name == "PUBLISH")
            {
                if (ShowPublish == null)
                {
                    return;
                }

                if (_publishOptions != null)
                {
                    list.Options = _publishOptions.Clone();
                    list.RemoveAll();
                    list.AddCurrentDrawing();
                }

                var dialog = new PublishViewModel(list);
                bool publish = ShowPublish(dialog);
                e.Handled = true;

                if (!publish)
                {
                    session.Write("The publishing operation was canceled.");
                    return;
                }

                _publishOptions = list.Options.Clone();
            }

            e.Handled = true;

            try
            {
                CadPublishResult result = CadPublisher.Publish(list, Printers());

                foreach (string message in result.Messages)
                {
                    session.Write(message);
                }

                if (list.Options.OpenInViewer)
                {
                    foreach (string file in result.Files)
                    {
                        OpenInViewer?.Invoke(file);
                    }
                }
            }
            catch (Exception exception)
                when (exception is InvalidOperationException
                    || exception is System.IO.IOException
                    || exception is UnauthorizedAccessException
                    || exception is ArgumentException
                )
            {
                GeometryHelperLog.Warn("MainViewModel: the publish was not carried out.", exception);
                session.Write("The publish failed: " + exception.Message);
            }
        }
    }
}
