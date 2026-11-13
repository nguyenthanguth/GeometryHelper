using System.Collections.Generic;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>BOUNDARY with its Boundary Creation dialog, which -BOUNDARY's Advanced options ask on the command line.</summary>
    internal static partial class CadCommands
    {
        /// <summary>
        /// BOUNDARY (BO): the window's Boundary Creation dialog with the settings -BOUNDARY keeps; Pick Points keeps those
        /// chosen and asks "Pick internal point:" until Enter, Cancel ends with nothing kept. With no window, the points are
        /// asked at once.
        /// </summary>
        private static IEnumerable<CadPrompt> BoundaryDialog(CadCommandContext c)
        {
            var request = new CadBoundaryRequest
            {
                IslandDetection = c.Remembered(BoundaryIslandsKey, 1.0) != 0.0,
                IsRegion = c.Remembered(BoundaryRegionKey, 0.0) != 0.0,
            };

            if (c.Session.Request("BOUNDARY", c.Document, request))
            {
                if (!request.IsPicking)
                {
                    yield break;
                }

                c.Remember(BoundaryIslandsKey, request.IslandDetection ? 1.0 : 0.0);
                c.Remember(BoundaryRegionKey, request.IsRegion ? 1.0 : 0.0);
            }

            foreach (CadPrompt prompt in BoundaryPoints(c, false))
            {
                yield return prompt;
            }
        }
    }
}
