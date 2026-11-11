using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// AutoCAD's Symbol dialog of the Geometric Tolerance dialog, as board GdtSymbol draws it: the fourteen symbols and the
    /// blank, the one in use framed; a pick chooses and closes, the close button leaves the symbol as it was.
    /// </summary>
    public partial class GdtSymbolWindow : Window
    {
        /// <summary>Makes the dialog with the symbol in use framed.</summary>
        /// <param name="current">The symbol in use, its letter of the gdt font; null for none.</param>
        public GdtSymbolWindow(char? current)
        {
            InitializeComponent();
            Choices.ItemsSource = GeometricToleranceViewModel
                .Symbols.Select(g => new GdtChoiceViewModel(
                    g,
                    g.Code == null ? "Blank: no symbol" : g.Name,
                    g.Code == current && current != null
                ))
                .ToList();
        }

        /// <summary>Gets the symbol picked: its glyph, the blank for none; null until one is picked.</summary>
        public GdtGlyphViewModel Picked { get; private set; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>
        /// Shows the dialog over a window, at a place of it, as board GeomToleranceSymbolOpen puts it, and gives the symbol
        /// picked.
        /// </summary>
        /// <param name="owner">The window it is shown over.</param>
        /// <param name="at">Where its top left stands in the owner.</param>
        /// <param name="current">The symbol in use; null for none.</param>
        /// <param name="picked">The symbol picked; null for the blank.</param>
        /// <returns>Whether one was picked: false where the dialog was closed.</returns>
        public static bool Pick(Window owner, Point at, char? current, out char? picked)
        {
            var dialog = new GdtSymbolWindow(current) { Owner = owner };

            if (owner != null)
            {
                dialog.Left = owner.Left + at.X;
                dialog.Top = owner.Top + at.Y;
            }

            dialog.ShowDialog();
            picked = dialog.Picked?.Code;
            return dialog.Picked != null;
        }

        private void OnHeaderDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed && IsLoaded)
            {
                DragMove();
            }
        }

        private void OnPick(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is GdtChoiceViewModel choice)
            {
                Picked = choice.Glyph;
                DialogResult = true;
            }
        }

        private void OnClose(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
