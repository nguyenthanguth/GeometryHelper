using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// The text window of TEXTSCR and F2, as AutoCAD 2021 shows its own: every line the command line has written, the oldest
    /// first, in one resizable list that follows the command line while it is open, and a way to copy the lines chosen, or all
    /// of them where none is chosen.
    /// </summary>
    public sealed class TextWindowViewModel : ObservableObject
    {
        internal TextWindowViewModel(ObservableCollection<string> lines) => Lines = lines;

        /// <summary>Occurs when AutoLISP's <c>graphscr</c> asks for the text window to be put away; the window that shows it closes itself.</summary>
        public event System.EventHandler CloseRequested;

        /// <summary>Asks the window that shows this one to close, as <c>(graphscr)</c> does; nothing happens where none is shown.</summary>
        public void RequestClose() => CloseRequested?.Invoke(this, System.EventArgs.Empty);

        /// <summary>Gets the lines of the command line's history, the oldest first.</summary>
        public ObservableCollection<string> Lines { get; }

        /// <summary>Gets the title of the window.</summary>
        public string Title => "Text Window";

        /// <summary>Gets the text a copy puts on the clipboard: the lines given one to a row, or every line where none is given.</summary>
        /// <param name="chosen">The lines chosen in the list; none or null for all.</param>
        /// <returns>The lines joined by line breaks.</returns>
        public string TextToCopy(IEnumerable<string> chosen)
        {
            List<string> picked = chosen?.ToList();
            return string.Join("\r\n", picked != null && picked.Count > 0 ? picked : Lines.ToList());
        }
    }
}
