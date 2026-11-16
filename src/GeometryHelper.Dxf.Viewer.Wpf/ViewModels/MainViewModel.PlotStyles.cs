using System;
using System.Collections.Generic;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>The folder of plot style tables the documents are given.</summary>
    public sealed partial class MainViewModel
    {
        private readonly HashSet<string> _plotStyleFoldersMade = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets the folder of plot style tables: Global settings' (<see cref="GlobalSettings.PlotStyleFolder"/>), else
        /// <c>PlotStyles</c> beside the settings file. The folder is made, and the tables the presenter makes written to it
        /// where it lacks them, the first time it is asked for.
        /// </summary>
        /// <returns>The folder's path.</returns>
        public string PlotStyleFolder
        {
            get
            {
                string folder = CadPlotStyleFolder.Resolve(_settings.PlotStyleFolder, SettingsPath);

                if (_plotStyleFoldersMade.Add(folder))
                {
                    CadPlotStyleFolder.EnsureBuiltIns(folder);
                }

                return folder;
            }
        }

        /// <summary>Gets or sets how the workspace shows the Plot Styles dialog of STYLESMANAGER; the dialog edits the files itself. Null for none, when STYLESMANAGER lists the tables on the command line.</summary>
        public Func<PlotStylesManagerViewModel, bool> ShowPlotStyles { get; set; }

        /// <summary>STYLESMANAGER's dialog, over the document's plot style folder.</summary>
        private void OnPlotStylesRequested(CadUiRequestEventArgs e)
        {
            if (ShowPlotStyles == null || !(e.Payload is CadStylesManagerRequest request))
            {
                return;
            }

            ShowPlotStyles(new PlotStylesManagerViewModel(request.Folder, request.NewIsNamed));
            e.Handled = true;
        }
    }
}
