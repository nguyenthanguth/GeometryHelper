using System.Collections.Generic;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// QSELECT and FIND, each shown by the window as AutoCAD's Quick Select and Find and Replace dialogs
    /// (<see cref="CadQuickSelect"/>, <see cref="CadFind"/>): with no window, the command says so.
    /// </summary>
    internal static partial class CadCommands
    {
        /// <summary>QSELECT: the Quick Select dialog, over the objects selected before it where there are any.</summary>
        private static IEnumerable<CadPrompt> QuickSelect(CadCommandContext c)
        {
            if (!c.Session.Request("QSELECT", c.Document))
            {
                c.Write("Quick Select is shown by the window.");
            }

            yield break;
        }

        /// <summary>FIND: the Find and Replace dialog; the objects selected before it are those of Find where: Selected objects.</summary>
        private static IEnumerable<CadPrompt> Find(CadCommandContext c)
        {
            if (!c.Session.Request("FIND", c.Document, c.PreSelected))
            {
                c.Write("Find and Replace is shown by the window.");
            }

            yield break;
        }
    }
}
