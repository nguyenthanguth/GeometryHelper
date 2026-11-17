using System.Collections.Generic;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// LAYWALK, AutoCAD 2021's LayerWalk: a dialog listing the layers; the layers selected in it are the only ones shown
    /// (every other layer is turned off), the walk undone when it closes unless Restore on exit is cleared. Select objects hides
    /// the dialog to pick objects on the canvas, whose layers are selected in the list; Purge Layers deletes the unused layers
    /// selected. AutoCAD has no command line form of it, so with no window the command says so.
    /// </summary>
    internal static partial class CadCommands
    {
        private static void AddLayerWalkTo(CadCommandRegistry registry) =>
            registry.Add(new CadCommand("LAYWALK", LayerWalk));

        private static IEnumerable<CadPrompt> LayerWalk(CadCommandContext c)
        {
            var request = new CadLayerWalkRequest(c.Document);
            c.OpenUndoStep();

            try
            {
                while (true)
                {
                    request.Action = CadDialogAction.Cancel;

                    if (!c.Session.Request("LAYWALK", c.Document, request))
                    {
                        c.Write("LAYWALK is the LayerWalk dialog of the window; it has no command line form.");
                        yield break;
                    }

                    if (request.Action != CadDialogAction.SelectObjects)
                    {
                        break;
                    }

                    yield return CadPrompt.ForSelection("Select objects on the layers to walk");

                    if (c.Input.Kind == CadInputKind.Selection)
                    {
                        request.WalkLayersOf(c.Input.Handles);
                    }
                }
            }
            finally
            {
                // The layers go back as they were unless the dialog was closed with Restore on exit cleared.
                if (request.RestoreOnExit)
                {
                    request.Restore();
                }
            }
        }
    }
}
