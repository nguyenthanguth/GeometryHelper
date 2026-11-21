namespace GeometryHelper.Cad.Wpf
{
    public sealed partial class MainViewModel
    {
        /// <summary>
        /// Writes a line on the command line of the drawing shown and draws the history again: what the window's handler of
        /// unhandled errors uses to report a Python handler that raised outside a run. Nothing where no drawing is shown.
        /// </summary>
        /// <param name="text">The line.</param>
        internal void WriteCommandLine(string text)
        {
            _current?.Session.Write(text);
            SyncHistory();
        }
    }
}
