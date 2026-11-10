namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The named views of the window: the view a named view restored in a space not on screen waits to show.
    /// </summary>
    public sealed partial class MainViewModel
    {
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
