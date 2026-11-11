using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// AutoCAD's Material Condition dialog of the Geometric Tolerance dialog, as board GdtMaterial draws it: M, L and S in
    /// their circles and the blank, the one in use framed; a pick chooses and closes, the close button leaves the condition
    /// as it was.
    /// </summary>
    public partial class GdtMaterialWindow : Window
    {
        /// <summary>Makes the dialog with the condition in use framed.</summary>
        /// <param name="current">The condition in use.</param>
        public GdtMaterialWindow(CadMaterialCondition current)
        {
            InitializeComponent();
            Choices.ItemsSource = GeometricToleranceViewModel
                .Materials.Select(m => new GdtChoiceViewModel(
                    GdtGlyphViewModel.Of(GeometricToleranceViewModel.CodeOf(m)),
                    LabelOf(m),
                    m == current && m != CadMaterialCondition.None
                ))
                .ToList();
        }

        /// <summary>Gets the condition picked; null until one is picked.</summary>
        public CadMaterialCondition? Picked { get; private set; }

        /// <summary>Gets the dialog's content, so that a screenshot can draw it with no window shown.</summary>
        public FrameworkElement Body => Root;

        /// <summary>
        /// Shows the dialog over a window, at a place of it, as board GeomToleranceMcOpen puts it, and gives the condition
        /// picked.
        /// </summary>
        /// <param name="owner">The window it is shown over.</param>
        /// <param name="at">Where its top left stands in the owner.</param>
        /// <param name="current">The condition in use.</param>
        /// <param name="picked">The condition picked; <see cref="CadMaterialCondition.None"/> for the blank.</param>
        /// <returns>Whether one was picked: false where the dialog was closed.</returns>
        public static bool Pick(Window owner, Point at, CadMaterialCondition current, out CadMaterialCondition picked)
        {
            var dialog = new GdtMaterialWindow(current) { Owner = owner };

            if (owner != null)
            {
                dialog.Left = owner.Left + at.X;
                dialog.Top = owner.Top + at.Y;
            }

            dialog.ShowDialog();
            picked = dialog.Picked ?? current;
            return dialog.Picked.HasValue;
        }

        /// <summary>The words of a condition, as its tooltip says them.</summary>
        internal static string LabelOf(CadMaterialCondition material)
        {
            switch (material)
            {
                case CadMaterialCondition.Maximum:
                    return "M: maximum material condition (MMC)";
                case CadMaterialCondition.Least:
                    return "L: least material condition (LMC)";
                case CadMaterialCondition.RegardlessOfSize:
                    return "S: regardless of feature size (RFS)";
                default:
                    return "Blank: no material condition";
            }
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
                Picked = GeometricToleranceViewModel.Materials.First(m =>
                    GeometricToleranceViewModel.CodeOf(m) == choice.Glyph.Code
                );
                DialogResult = true;
            }
        }

        private void OnClose(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
