using System;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>The Reference Edit dialog of REFEDIT, shown through a function the workspace sets.</summary>
    public sealed partial class MainViewModel
    {
        /// <summary>Gets or sets how the workspace shows the Reference Edit dialog: true when OK was pressed. With none set, REFEDIT asks on the command line.</summary>
        public Func<ReferenceEditViewModel, bool> ShowReferenceEdit { get; set; }

        /// <summary>REFEDIT's request: the dialog is shown over the references the point reached, and what OK chose is given back to the command; with no function set it is left to the command line.</summary>
        private void OnReferenceEditRequested(CadUiRequestEventArgs e)
        {
            if (e.Payload is CadRefEditRequest request && ShowReferenceEdit != null)
            {
                var dialog = new ReferenceEditViewModel(request);

                if (ShowReferenceEdit(dialog))
                {
                    dialog.Accept();
                }

                e.Handled = true;
            }
        }
    }
}
