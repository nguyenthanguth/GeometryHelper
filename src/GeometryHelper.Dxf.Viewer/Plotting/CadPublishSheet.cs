using System;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// One row of the Publish dialog's sheet list: a space of a drawing, Model or a layout, with the name it goes by in the
    /// list, the page setup it is plotted with and how far it is ready to be. A drawing that is open is plotted as it stands,
    /// with its unsaved edits; any other is read from its file when the sheet is added.
    /// </summary>
    public sealed class CadPublishSheet
    {
        /// <summary>The status of a sheet that is ready to publish.</summary>
        public const string Loaded = "Loaded";

        /// <summary>The status of a sheet whose drawing file is not there.</summary>
        public const string DrawingNotFound = "Drawing not found";

        /// <summary>The status of a sheet whose drawing file could not be read.</summary>
        public const string DrawingFailedToLoad = "Drawing failed to load";

        /// <summary>The status of a sheet whose drawing has no Model or layout of the name the sheet gives.</summary>
        public const string LayoutNotFound = "Layout not found";

        /// <summary>The status of a sheet whose page setup is not in the drawing it is taken from.</summary>
        public const string PageSetupNotFound = "Page setup not found";

        /// <summary>The name of model space as a sheet names it.</summary>
        public const string ModelName = "Model";

        internal CadPublishSheet(string name, string drawingFile, string layoutName)
        {
            Name = name;
            DrawingFile = drawingFile ?? string.Empty;
            LayoutName = layoutName;
            PageSetup = string.Empty;
            PageSetupFile = string.Empty;
            Status = DrawingNotFound;
        }

        /// <summary>Gets the name in the list, the drawing's name and the layout's joined by a hyphen: <c>plan-level2-Layout1</c>.</summary>
        public string Name { get; internal set; }

        /// <summary>Gets the full path of the drawing's file; empty for a drawing that is open and has no file yet.</summary>
        public string DrawingFile { get; }

        /// <summary>Gets the name of the space: <c>Model</c>, or the layout's name.</summary>
        public string LayoutName { get; }

        /// <summary>Gets whether the sheet is model space.</summary>
        public bool IsModel => string.Equals(LayoutName, ModelName, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Gets the name of the page setup the sheet is plotted with; empty for its own, the page setup its layout is set to,
        /// which the list shows as <c>&lt;Default: …&gt;</c>.
        /// </summary>
        public string PageSetup { get; internal set; }

        /// <summary>Gets the file the page setup was imported from; empty for one of the sheet's own drawing.</summary>
        public string PageSetupFile { get; internal set; }

        /// <summary>Gets the drawing the sheet is of, null where it was not read.</summary>
        public CadDocument Document { get; internal set; }

        /// <summary>Gets the space the sheet plots, null where its drawing was not read or has no such space.</summary>
        public CadSpace Space { get; internal set; }

        /// <summary>Gets how far the sheet is ready: <see cref="Loaded"/>, or the reason it is not.</summary>
        public string Status { get; internal set; }

        /// <summary>Gets whether the sheet can be published: its space was read and its page setup is there.</summary>
        public bool IsLoaded => Space != null && Status == Loaded;

        /// <inheritdoc/>
        public override string ToString() => Name;
    }
}
