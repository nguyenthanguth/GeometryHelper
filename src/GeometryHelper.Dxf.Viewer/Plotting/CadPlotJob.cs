using System;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// A plot a command hands to the window (<see cref="CadUiRequestEventArgs.Plot"/>): the space, how it is plotted, what the
    /// view showed when it was asked, and, from -PLOT, the file to write. PLOT and PAGESETUP hand one to their dialog, which
    /// asks for a window on the canvas by <see cref="PicksWindow"/>, as the Window&lt; button of AutoCAD's Plot dialog does.
    /// </summary>
    public sealed class CadPlotJob
    {
        private CadPlotSettings _settings;

        /// <summary>Makes a plot to hand to the window.</summary>
        /// <param name="space">The space plotted.</param>
        /// <param name="settings">How it is plotted; a copy is kept.</param>
        /// <param name="display">What the view showed, in drawing units, for a plot of the display; empty for none.</param>
        /// <param name="fileName">The file to write, from -PLOT; null where the window asks for one.</param>
        /// <exception cref="ArgumentNullException">Thrown when the space or the settings are null.</exception>
        public CadPlotJob(CadSpace space, CadPlotSettings settings, DisplayBox display, string fileName)
        {
            Space = space ?? throw new ArgumentNullException(nameof(space));
            Settings = (settings ?? throw new ArgumentNullException(nameof(settings))).Clone();
            Display = display;
            FileName = fileName;
        }

        /// <summary>Gets the space plotted.</summary>
        public CadSpace Space { get; }

        /// <summary>Gets or sets how it is plotted: a dialog that asks for a window sets what it shows here, to be shown again after.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the settings set are null.</exception>
        public CadPlotSettings Settings
        {
            get => _settings;
            set => _settings = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>Gets what the view showed, in drawing units, for a plot of the display; empty for none.</summary>
        public DisplayBox Display { get; }

        /// <summary>Gets the file to write, from -PLOT; null where the window asks for one.</summary>
        public string FileName { get; }

        /// <summary>
        /// Gets or sets whether the dialog closed to have a window picked on the canvas: the command then asks "Specify first
        /// corner:" and "Specify opposite corner:", sets the window as the area plotted, and shows the dialog again.
        /// </summary>
        public bool PicksWindow { get; set; }
    }
}
