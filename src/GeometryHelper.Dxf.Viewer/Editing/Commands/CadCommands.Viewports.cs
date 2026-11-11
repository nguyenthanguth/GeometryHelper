using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>MSPACE and PSPACE: model space worked in through a viewport of the layout shown, and the paper again; VPSCALE, a viewport's scale.</summary>
    internal static partial class CadCommands
    {
        /// <summary>
        /// MSPACE (MS): works in model space through the viewport last worked through, else the layout's first, as AutoCAD's;
        /// refused on the Model tab and in a layout with no viewport to work through.
        /// </summary>
        private static IEnumerable<CadPrompt> ModelSpaceThrough(CadCommandContext c)
        {
            if (c.Document.CurrentSpace.IsModel)
            {
                c.Write("** Command not allowed in Model Tab **");
                yield break;
            }

            if (c.Document.ActiveViewport != null)
            {
                yield break;
            }

            DxfViewport viewport = c.Document.ViewportToEnter();

            if (viewport == null)
            {
                c.Write("There are no active model space viewports.");
                yield break;
            }

            c.Document.EnterViewport(viewport.Handle);
        }

        /// <summary>
        /// VPSCALE: the scale of the viewport worked through or selected alone, else of one picked, written as AutoCAD writes it
        /// ("PS:MS == 1:20", "Viewport Scale Factor = 0.0500"); then a new scale asked, as 1:50 or 2:1, Enter keeping it, one
        /// undo step.
        /// </summary>
        private static IEnumerable<CadPrompt> ViewportScale(CadCommandContext c)
        {
            if (c.Document.CurrentSpace.IsModel)
            {
                c.Write("** Command not allowed in Model Tab **");
                yield break;
            }

            DxfViewport viewport = CadViewportLayerEditor.TargetViewport(c.Document);

            if (viewport == null)
            {
                yield return CadPrompt.ForEntity("Select a viewport");
                viewport = c.Input.Kind == CadInputKind.Selection ? c.Document.FindEntity(c.Input.Handles[0]) as DxfViewport : null;
            }

            if (viewport == null || viewport.Number == 1 || !(viewport.Height > 0.0) || !(viewport.ViewHeight > 0.0))
            {
                c.Write("The object selected is not a viewport.");
                yield break;
            }

            string name = CadViewports.ScaleName(viewport.CustomScale);
            c.Write("PS:MS == " + name);
            c.Write("Viewport Scale Factor = " + CadCommandContext.Length(viewport.CustomScale));
            yield return CadPrompt.ForString("Enter a new viewport scale (1:50, 2:1)", name);
            string typed = (c.Input.Text ?? string.Empty).Trim();

            if (c.Input.IsNone || typed.Length == 0 || string.Equals(typed, name, StringComparison.Ordinal))
            {
                yield break;
            }

            if (!CadViewports.TryReadScale(typed, out double scale))
            {
                c.Write("A scale is written as 1:50 or 2:1.");
                yield break;
            }

            c.OpenUndoStep();
            CadEditResult result = CadPropertyEditor.Set(c.Document, new[] { viewport.Handle }, "Custom scale", scale.ToString("R", System.Globalization.CultureInfo.InvariantCulture));

            if (!result.Succeeded)
            {
                c.Write(result.Message);
            }
        }

        /// <summary>PSPACE (PS): works on the paper of the layout shown again; refused on the Model tab.</summary>
        private static IEnumerable<CadPrompt> PaperSpace(CadCommandContext c)
        {
            if (c.Document.CurrentSpace.IsModel)
            {
                c.Write("** Command not allowed in Model Tab **");
                yield break;
            }

            c.Document.LeaveViewport();
        }
    }
}
