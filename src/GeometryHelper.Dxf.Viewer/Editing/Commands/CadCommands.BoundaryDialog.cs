using System.Collections.Generic;
using System.Linq;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>BOUNDARY with its Boundary Creation dialog, which -BOUNDARY's Advanced options ask on the command line.</summary>
    internal static partial class CadCommands
    {
        /// <summary>
        /// BOUNDARY (BO): the window's Boundary Creation dialog with the settings -BOUNDARY keeps; Pick Points keeps those
        /// chosen and asks "Pick internal point:" until Enter, Cancel ends with nothing kept. New of Boundary set asks "Select
        /// objects:" and shows the dialog again, Existing set then chosen: the areas are found among those objects alone, as
        /// -HATCH's Boundary set New finds them. With no window, the points are asked at once.
        /// </summary>
        private static IEnumerable<CadPrompt> BoundaryDialog(CadCommandContext c)
        {
            var request = new CadBoundaryRequest
            {
                IslandDetection = c.Remembered(BoundaryIslandsKey, 1.0) != 0.0,
                IsRegion = c.Remembered(BoundaryRegionKey, 0.0) != 0.0,
            };

            while (true)
            {
                request.IsSelectingSet = false;
                request.IsPicking = false;

                if (!c.Session.Request("BOUNDARY", c.Document, request))
                {
                    break;
                }

                if (!request.IsSelectingSet)
                {
                    if (!request.IsPicking)
                    {
                        yield break;
                    }

                    c.Remember(BoundaryIslandsKey, request.IslandDetection ? 1.0 : 0.0);
                    c.Remember(BoundaryRegionKey, request.IsRegion ? 1.0 : 0.0);
                    break;
                }

                yield return CadPrompt.ForSelection("Select objects");

                if (c.Input.Kind == CadInputKind.Selection && c.Input.Handles != null && c.Input.Handles.Count > 0)
                {
                    request.ExistingSet = c.Input.Handles.ToList();
                    request.UsesExistingSet = true;
                }
            }

            IReadOnlyCollection<string> candidates =
                request.UsesExistingSet && request.ExistingSet != null ? request.ExistingSet.ToList() : null;

            foreach (CadPrompt prompt in BoundaryPoints(c, false, candidates))
            {
                yield return prompt;
            }
        }
    }
}
