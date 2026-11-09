using System;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The Point Style dialog of PTYPE (DDPTYPE), shown through a function the workspace sets, so that a test drives it with
    /// none; with none, PTYPE asks PDMODE and PDSIZE on the command line.
    /// </summary>
    public sealed partial class MainViewModel
    {
        /// <summary>Gets or sets how the workspace shows the Point Style dialog: true when OK was pressed, the dialog then applied.</summary>
        public Func<PointStyleViewModel, bool> ShowPointStyle { get; set; }

        /// <summary>Makes the Point Style dialog of the drawing shown; null with none open.</summary>
        /// <returns>The dialog.</returns>
        public PointStyleViewModel CreatePointStyle() => _current == null ? null : new PointStyleViewModel(_current.Document);

        /// <summary>PTYPE's request: the dialog shown where the workspace shows it, OK writing PDMODE and PDSIZE as one undo step.</summary>
        private void OnPointStyleRequested(CadUiRequestEventArgs e)
        {
            CadDocument document = e.Document ?? _current?.Document;

            if (document == null || ShowPointStyle == null)
            {
                return;
            }

            var dialog = new PointStyleViewModel(document);

            if (ShowPointStyle(dialog))
            {
                CadEditResult result = dialog.Apply();

                if (result != null)
                {
                    SayRefused(result);
                }
            }

            e.Handled = true;
        }
    }
}
