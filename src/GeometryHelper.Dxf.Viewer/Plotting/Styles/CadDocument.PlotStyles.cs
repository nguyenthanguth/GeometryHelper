namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>The folder a document's plot style tables are read from.</summary>
    public sealed partial class CadDocument
    {
        private string _plotStyleFolder = string.Empty;

        /// <summary>
        /// Gets or sets the folder the plot style tables a plot names (<see cref="CadPlotSettings.StyleSheet"/>) are read from,
        /// as <see cref="CadPlotStyleFolder"/> lists them: empty, the default, for no folder, when the tables the presenter
        /// makes (<see cref="CadPlotStyleFolder.BuiltIn"/>) stand in and nothing is read from disk. The window sets Global
        /// settings' plot style folder, <see cref="CadPlotStyleFolder.DefaultPath"/> where that is empty.
        /// </summary>
        public string PlotStyleFolder
        {
            get => _plotStyleFolder;
            set => _plotStyleFolder = (value ?? string.Empty).Trim();
        }
    }
}
