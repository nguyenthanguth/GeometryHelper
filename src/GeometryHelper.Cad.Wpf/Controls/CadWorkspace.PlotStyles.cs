using System.Windows;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>The Plot Styles folder dialog of STYLESMANAGER, with the Plot Style Table Editor and Add Plot Style Table it opens.</summary>
    public partial class CadWorkspace
    {
        /// <summary>Gives the view model the windows of plot style tables.</summary>
        private void AttachPlotStyles()
        {
            _model.ShowPlotStyles = dialog =>
            {
                Window owner = Window.GetWindow(this);
                dialog.ShowEditor = editor => PlotStyleEditorWindow.Show(owner, editor);
                dialog.ShowNew = made => PlotStyleNewWindow.Show(owner, made);
                return PlotStylesWindow.Show(owner, dialog);
            };
        }
    }
}
