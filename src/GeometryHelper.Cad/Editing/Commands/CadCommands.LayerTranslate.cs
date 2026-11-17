using System.Collections.Generic;
using System.Globalization;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// LAYTRANS, AutoCAD 2021's Layer Translator: a dialog that maps the layers of the drawing to the layers of a standards
    /// file (or of the drawing, or new ones) and translates them, every object of a layer moved to the layer it is mapped to.
    /// Cancel changes nothing. AutoCAD has no command line form of it, so with no window the command says so.
    /// </summary>
    internal static partial class CadCommands
    {
        private static void AddLayerTranslateTo(CadCommandRegistry registry) =>
            registry.Add(new CadCommand("LAYTRANS", LayerTranslate));

        private static IEnumerable<CadPrompt> LayerTranslate(CadCommandContext c)
        {
            var request = new CadLayerTranslatorRequest(c.Document);
            request.Action = CadDialogAction.Cancel;
            c.OpenUndoStep();
            bool asked = c.Session.Request("LAYTRANS", c.Document, request);
            request.HideContents();

            if (!asked)
            {
                c.Write("LAYTRANS is the Layer Translator dialog of the window; it has no command line form.");
                yield break;
            }

            if (request.Action != CadDialogAction.Ok)
            {
                yield break;
            }

            int mapped = request.Mappings.Count;
            CadEditResult result = request.Translate();

            if (!result.Succeeded)
            {
                c.Write(result.Message);
                yield break;
            }

            c.Write(
                mapped == 1
                    ? "1 layer translated."
                    : mapped.ToString(CultureInfo.InvariantCulture) + " layers translated."
            );

            if (request.LogFile != null)
            {
                c.Write("Transaction log: " + request.LogFile);
            }
        }
    }
}
