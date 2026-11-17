using System;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// The text window of TEXTSCR and F2, shown through a function the workspace sets, so that a test drives it with none; with
    /// none, TEXTSCR says the window is shown by the window.
    /// </summary>
    public sealed partial class MainViewModel
    {
        private TextWindowViewModel _textWindow;

        /// <summary>Gets or sets how the workspace shows the text window, which stays open beside the drawing; one is open at a time.</summary>
        public Action<TextWindowViewModel> ShowTextWindow { get; set; }

        /// <summary>TEXTSCR: the text window over the history, brought to the front where it is open.</summary>
        private void OnTextWindowRequested(CadUiRequestEventArgs e) => e.Handled = OpenTextWindow();

        /// <summary>Shows the text window, or brings the one open to the front; it stays open beside a command running, which F2 does not end.</summary>
        /// <returns>False where the workspace shows none.</returns>
        private bool OpenTextWindow()
        {
            if (ShowTextWindow == null)
            {
                return false;
            }

            _textWindow = _textWindow ?? new TextWindowViewModel(History);
            ShowTextWindow(_textWindow);
            return true;
        }
    }
}
