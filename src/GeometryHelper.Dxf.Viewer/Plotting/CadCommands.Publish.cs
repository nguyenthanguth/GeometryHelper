using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// PUBLISH and -PUBLISH, as AutoCAD 2021's. PUBLISH hands the window the list of the drawing open (its Model and layouts, each
    /// as the drawing's name and the space's joined by a hyphen) for the Publish dialog, which lists, adds, removes and moves
    /// sheets, sets their page setups, loads and saves the list as a .dsd file and publishes it. -PUBLISH asks "Enter name of
    /// sheet list &lt;file.dsd&gt;:" and has the window publish the list the file holds, with no dialog; the words are those of
    /// AutoCAD's own. With no window to show the dialog, or CMDDIA 0, PUBLISH asks -PUBLISH's question. The window's writers
    /// plot the sheets: the request is <c>PUBLISH</c> for the dialog and <c>-PUBLISH</c> for a list to publish at once, its
    /// payload the <see cref="CadPublishList"/>.
    /// </summary>
    internal static partial class CadCommands
    {
        internal static void AddPublishTo(CadCommandRegistry registry)
        {
            registry.Add(new CadCommand("PUBLISH", PublishDialog));
            registry.Add(new CadCommand("-PUBLISH", PublishTyped));
        }

        /// <summary>The list a publish starts from: empty, given the session's plot stamp where it is on and the PDFSHX text.</summary>
        private static CadPublishList NewPublishList(CadCommandContext c)
        {
            var list = new CadPublishList(c.Document);
            WithOutputOptions(c, list);
            return list;
        }

        private static void WithOutputOptions(CadCommandContext c, CadPublishList list)
        {
            list.PlotStamp = c.Session.PlotStamp.On ? c.Session.PlotStamp.Clone() : null;
            list.PdfShx = Kept(c.Session, "pdfshx", 0.0) != 0.0;
        }

        /// <summary>
        /// PUBLISH: the window's Publish dialog over the sheets of the drawing open. With no window, or CMDDIA 0, the question
        /// of -PUBLISH.
        /// </summary>
        private static IEnumerable<CadPrompt> PublishDialog(CadCommandContext c)
        {
            CadPublishList list = NewPublishList(c);
            list.AddCurrentDrawing();

            if (Kept(c.Session, "cmddia", 1.0) != 0.0 && c.Session.Request("PUBLISH", c.Document, list))
            {
                yield break;
            }

            foreach (CadPrompt prompt in PublishTyped(c))
            {
                yield return prompt;
            }
        }

        /// <summary>
        /// -PUBLISH: "Enter name of sheet list &lt;plan-level2.dsd&gt;:", the list the drawing's name gives beside the drawing; the
        /// file read, and its sheets handed to the window to publish, where AutoCAD runs the job. A file that is not there says
        /// "Could not locate sheet list file."; a list with no sheet that can be plotted says "There were no plottable sheets in
        /// the current operation."
        /// </summary>
        private static IEnumerable<CadPrompt> PublishTyped(CadCommandContext c)
        {
            string folder = c.Document.HasFile
                ? Path.GetDirectoryName(c.Document.FileName)
                : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string offered = Path.Combine(
                folder ?? string.Empty,
                Path.GetFileNameWithoutExtension(c.Document.Title) + ".dsd"
            );
            yield return CadPrompt.ForString("Enter name of sheet list", offered);
            string file =
                c.Input.Kind == CadInputKind.Text && c.Input.Text.Trim().Length > 0
                    ? c.Input.Text.Trim().Trim('"')
                    : offered;

            if (!Path.IsPathRooted(file))
            {
                file = Path.Combine(Path.GetDirectoryName(offered) ?? string.Empty, file);
            }

            if (!File.Exists(file))
            {
                c.Write(file);
                c.Write("Could not locate sheet list file.");
                yield break;
            }

            CadPublishList list = NewPublishList(c);

            try
            {
                foreach (string note in list.Load(file, false))
                {
                    c.Write(note);
                }
            }
            catch (Exception exception) when (exception is IOException || exception is FormatException)
            {
                GeometryHelperLog.Warn("CadCommands: the sheet list " + file + " could not be read.", exception);
                c.Write("Error reading list file.");
                yield break;
            }

            if (!list.Sheets.Any(s => s.IsLoaded))
            {
                c.Write("There were no plottable sheets in the current operation.");
                yield break;
            }

            if (!c.Session.Request("-PUBLISH", c.Document, list))
            {
                c.Write("Publishing needs the window's writers: nothing was written.");
            }
        }
    }
}
