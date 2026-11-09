using System.Collections.Generic;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>MSPACE and PSPACE: model space worked in through a viewport of the layout shown, and the paper again.</summary>
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
