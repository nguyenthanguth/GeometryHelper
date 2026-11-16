using System;
using System.Collections.Generic;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>What the command hands the window's Plot Styles dialog: the folder of plot style tables and the kind of table New… makes.</summary>
    public sealed class CadStylesManagerRequest
    {
        /// <summary>Makes the request.</summary>
        /// <param name="folder">The folder of plot style tables.</param>
        /// <param name="newIsNamed">Whether New… makes a named table (.stb), as in a drawing that plots by name.</param>
        /// <exception cref="ArgumentNullException">Thrown when the folder is null.</exception>
        public CadStylesManagerRequest(string folder, bool newIsNamed)
        {
            Folder = folder ?? throw new ArgumentNullException(nameof(folder));
            NewIsNamed = newIsNamed;
        }

        /// <summary>Gets the folder of plot style tables; empty where the document has none.</summary>
        public string Folder { get; }

        /// <summary>Gets whether New… makes a named table (.stb).</summary>
        public bool NewIsNamed { get; }
    }

    /// <summary>
    /// STYLESMANAGER, AutoCAD's Plot Styles folder: the window shows the tables of the document's plot style folder to edit,
    /// make and delete (<see cref="CadStylesManagerRequest"/>). With no window, or no folder, the command line names the tables.
    /// </summary>
    internal static partial class CadCommands
    {
        private static IEnumerable<CadPrompt> StylesManager(CadCommandContext c) =>
            !string.IsNullOrWhiteSpace(c.Document.PlotStyleFolder)
            && c.Session.Request(
                "STYLESMANAGER",
                c.Document,
                new CadStylesManagerRequest(c.Document.PlotStyleFolder, !c.Document.Database.PlotStyleMode)
            )
                ? Array.Empty<CadPrompt>()
                : StylesManagerTyped(c);

        /// <summary>The tables the document's folder holds, one to a line, .ctb then .stb; the built-in ones where it has no folder.</summary>
        private static IEnumerable<CadPrompt> StylesManagerTyped(CadCommandContext c)
        {
            string folder = c.Document.PlotStyleFolder;
            c.Write(
                string.IsNullOrWhiteSpace(folder)
                    ? "Plot style tables (built in):"
                    : "Plot style tables in " + folder + ":"
            );

            foreach (bool named in new[] { false, true })
            {
                foreach (string name in CadPlotStyleFolder.Names(folder, named))
                {
                    c.Write(name);
                }
            }

            yield break;
        }
    }
}
