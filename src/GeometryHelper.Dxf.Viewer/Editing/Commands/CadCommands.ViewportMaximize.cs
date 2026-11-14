using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>VPMAX and VPMIN, a viewport of a layout maximized into a temporary model view and back, as AutoCAD 2021's.</summary>
    internal static partial class CadCommands
    {
        /// <summary>How far in from the edge of the canvas a maximized viewport stands, in pixels: AutoCAD 2021 leaves about ten.</summary>
        private const double MaximizedMarginPixels = 10.0;

        internal static void AddViewportMaximizeTo(CadCommandRegistry registry)
        {
            registry.Add(new CadCommand("VPMAX", ViewportMaximize));
            registry.Add(new CadCommand("VPMIN", ViewportMinimize));
        }

        /// <summary>
        /// VPMAX: in a layout, "Select a viewport to maximize:" (asked again, "Invalid selection, please select a single
        /// viewport.", for anything but a viewport the layout can be worked through; Enter says it too), or none asked where one
        /// is worked through already, which is the one maximized; "A viewport is currently maximized." where one is. The viewport
        /// takes the canvas, a margin of ten pixels left, at the scale it had, the other viewports of the layout go off, and
        /// model space is worked through it. A viewport whose display is locked is maximized too. On the Model tab it is
        /// -VPORTS Toggle, as AutoCAD 2021's is. One undo step.
        /// </summary>
        private static IEnumerable<CadPrompt> ViewportMaximize(CadCommandContext c)
        {
            CadDocument document = c.Document;

            if (document.CurrentSpace.IsModel)
            {
                ToggleTiles(c);
                yield break;
            }

            if (document.IsViewportMaximized)
            {
                c.Write("A viewport is currently maximized.");
                yield break;
            }

            DxfViewport viewport = document.ActiveViewport;

            while (viewport == null)
            {
                yield return CadPrompt.ForEntity("Select a viewport to maximize");
                string handle = c.Input.Handles?.FirstOrDefault();
                viewport =
                    handle != null
                    && document.FindEntity(handle) is DxfViewport picked
                    && CadViewports.CanWorkThrough(picked)
                        ? picked
                        : null;

                if (viewport == null)
                {
                    c.Write("Invalid selection, please select a single viewport.");
                }
            }

            c.OpenUndoStep();
            CadEditResult result = document.MaximizeViewport(viewport.Handle, MaximizedWindow(c));

            if (!result.Succeeded)
            {
                c.Write(result.Message);
                yield break;
            }

            document.WorkThrough(viewport.Handle, "VPMAX");
        }

        /// <summary>
        /// VPMIN: the viewport VPMAX maximized put back as it was, its view too (a zoom or a pan made while it was maximized is
        /// given up), the other viewports on again and the paper worked on; "No viewport is currently maximized." where none is.
        /// On the Model tab it is -VPORTS Toggle. One undo step.
        /// </summary>
        private static IEnumerable<CadPrompt> ViewportMinimize(CadCommandContext c)
        {
            CadDocument document = c.Document;

            if (document.CurrentSpace.IsModel)
            {
                ToggleTiles(c);
                yield break;
            }

            if (!document.IsViewportMaximized)
            {
                c.Write("No viewport is currently maximized.");
                yield break;
            }

            c.OpenUndoStep();
            CadEditResult result = document.MinimizeViewport();

            if (!result.Succeeded)
            {
                c.Write(result.Message);
                yield break;
            }

            document.WorkThrough(null, "VPMIN");
        }

        /// <summary>-VPORTS Toggle, which VPMAX and VPMIN are on the Model tab: the current tile alone, or the tiles back.</summary>
        private static void ToggleTiles(CadCommandContext c)
        {
            c.OpenUndoStep();

            if (c.Document.TiledViewports.Toggle())
            {
                c.Write("Regenerating model.");
            }
        }

        /// <summary>
        /// The box of the sheet a viewport is maximized into: the canvas's, a margin of ten pixels in from each edge; where there
        /// is no canvas, the sheet's own area.
        /// </summary>
        private static DisplayBox MaximizedWindow(CadCommandContext c)
        {
            CadView view = c.Session.View;

            if (view == null || !(view.PixelsPerUnit > 0.0))
            {
                return c.Document.CurrentSpace.PaperBounds;
            }

            DisplayBox shown = view.VisibleBox;
            double margin = MaximizedMarginPixels / view.PixelsPerUnit;

            return shown.Width > 2.0 * margin && shown.Height > 2.0 * margin ? shown.Inflate(-margin) : shown;
        }
    }
}
