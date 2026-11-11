using System.Collections.Generic;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// QSELECT and FIND, each shown by the window as AutoCAD's Quick Select and Find and Replace dialogs
    /// (<see cref="CadQuickSelect"/>, <see cref="CadFind"/>): with no window, the command says so.
    /// </summary>
    internal static partial class CadCommands
    {
        /// <summary>
        /// QSELECT: the Quick Select dialog, over the objects selected before it where there are any. Its Select objects
        /// button, the window's answer "Select objects", hides it while "Select objects:" asks on the command line; the objects
        /// picked become the selection, and the dialog comes back over them, as AutoCAD's does.
        /// </summary>
        private static IEnumerable<CadPrompt> QuickSelect(CadCommandContext c)
        {
            while (true)
            {
                CadUiRequestEventArgs asked = c.Session.Ask("QSELECT", c.Document);

                if (!asked.Handled)
                {
                    c.Write("Quick Select is shown by the window.");
                    yield break;
                }

                if (asked.Answer != "Select objects")
                {
                    yield break;
                }

                yield return CadPrompt.ForSelection();

                if (c.Input.Kind == CadInputKind.Selection && c.Input.Handles.Count > 0)
                {
                    c.Document.Selection.Set(c.Input.Handles);
                }
            }
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
