using System;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The named views and drawing units of the window: the View Manager (VIEW) and Drawing Units (UNITS), each shown through
    /// a function the workspace sets, so that a test drives them with none; and the view a named view restored in a space not
    /// on screen waits to show.
    /// </summary>
    public sealed partial class MainViewModel
    {
        /// <summary>Gets or sets how the workspace shows the View Manager; the action it closed with is in its request.</summary>
        public Func<ViewManagerViewModel, bool> ShowViewManager { get; set; }

        /// <summary>Gets or sets how the workspace shows Drawing Units; the action it closed with is in its request.</summary>
        public Func<DrawingUnitsViewModel, bool> ShowDrawingUnits { get; set; }

        /// <summary>A request of VIEW or UNITS: its dialog shown where the workspace shows it.</summary>
        private void OnViewsRequested(CadUiRequestEventArgs e)
        {
            switch (e.Payload)
            {
                case CadViewManagerRequest views when ShowViewManager != null:
                    e.Handled = ShowViewManager(new ViewManagerViewModel(views));
                    break;
                case CadUnitsRequest units when ShowDrawingUnits != null:
                    e.Handled = ShowDrawingUnits(new DrawingUnitsViewModel(units));
                    break;
            }
        }

        /// <summary>The view of the space now shown zoomed to what a named view restored in it asked, once.</summary>
        private void ShowPendingView()
        {
            CadSpace space = _current?.Document.CurrentSpace;

            if (space?.PendingView == null)
            {
                return;
            }

            ViewState state = _current.ViewOf(space, _canvasWidth, _canvasHeight);
            state.View.ZoomTo(space.PendingView.Value, 0.0);
            state.FitPending = false;
            space.PendingView = null;
        }
    }
}
