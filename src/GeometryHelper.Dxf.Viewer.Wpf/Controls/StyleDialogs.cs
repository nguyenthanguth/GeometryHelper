using System.Linq;
using System.Windows;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>Shows the style dialogs over a window: the Text Style dialog (STYLE), the Dimension Style Manager (DIMSTYLE) and the Table Style dialog (TABLESTYLE).</summary>
    public static class StyleDialogs
    {
        /// <summary>Shows a style dialog over the drawing shown; nothing with no drawing.</summary>
        /// <param name="owner">The window it is shown over.</param>
        /// <param name="model">The window's view model.</param>
        /// <param name="name">STYLE for the Text Style dialog, DIMSTYLE for the Dimension Style Manager, TABLESTYLE for the Table Style dialog.</param>
        public static void Show(Window owner, MainViewModel model, string name)
        {
            if (model == null)
            {
                return;
            }

            if (name == "STYLE")
            {
                ShowTextStyle(owner, model);
                return;
            }

            if (name == "TABLESTYLE")
            {
                ShowTableStyle(owner, model);
                return;
            }

            DimStyleManagerViewModel manager = model.CreateDimStyleManager();

            if (manager != null)
            {
                new DimStyleManagerWindow(manager, () => ShowTextStyle(owner, model)) { Owner = owner }.ShowDialog();
            }
        }

        /// <summary>Shows the Table Style dialog (TABLESTYLE) over a window, as the table button of the Insert Table dialog does too.</summary>
        /// <param name="owner">The window it is shown over.</param>
        /// <param name="model">The window's view model.</param>
        public static void ShowTableStyle(Window owner, MainViewModel model)
        {
            TableStyleManagerViewModel manager = model?.CreateTableStyleManager();

            if (manager != null)
            {
                new TableStyleWindow(manager) { Owner = owner }.ShowDialog();
            }
        }

        private static void ShowTextStyle(Window owner, MainViewModel model)
        {
            TextStyleViewModel style = model.CreateTextStyle();

            if (style != null)
            {
                // Over the dialog in front, as the "..." of the Text tab shows it over the dimension style's dialog.
                Window active = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
                new TextStyleWindow(style) { Owner = active ?? owner }.ShowDialog();
            }
        }
    }
}
