using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Cad;
using GeometryHelper.Cad.Plugins;
using GeometryHelper.Dxf;

[assembly: CadPluginApi("1.0")]

namespace GeometryHelper.Cad.Plugins.DrawingInfo
{
    /// <summary>
    /// The sample plugin that ships with the app: GHDWGINFO writes what the drawing holds, and changes nothing. It is the
    /// smallest useful plugin, one command that reads the document and writes lines on the command line.
    /// </summary>
    public sealed class DrawingInfoCommands
    {
        /// <summary>
        /// Writes the drawing's name, the objects of model space counted by type, the number of layers and of blocks, and the
        /// extents. A drawing called Plan.dwg with two circles and a line in model space, two layers and one block gives
        /// <c>Drawing: Plan.dwg</c>, <c>Model space: 3 objects</c>, <c>  Circle   2</c>, <c>  Line     1</c>,
        /// <c>Layers: 2, blocks: 1</c> and <c>Extents: (0.00,0.00) to (120.00,80.00)</c>.
        /// </summary>
        /// <param name="c">The command's context.</param>
        /// <returns>No prompts: the command asks nothing.</returns>
        [CadCommand("GHDWGINFO", Description = "Write the name, objects, layers, blocks and extents of the drawing")]
        public IEnumerable<CadPrompt> DrawingInfo(CadCommandContext c)
        {
            DxfDatabase database = c.Database;
            c.Write("Drawing: " + c.Document.Title);

            // Model space, counted by the type of each object: DxfCircle is written Circle. The names are sorted, so the
            // lines come in the same order every time.
            var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
            int objects = 0;

            foreach (DxfEntity entity in c.Document.ModelSpace.Block)
            {
                string name = entity.GetType().Name;
                name = name.StartsWith("Dxf", StringComparison.Ordinal) ? name.Substring(3) : name;
                counts.TryGetValue(name, out int count);
                counts[name] = count + 1;
                objects++;
            }

            c.Write("Model space: " + objects.ToString(CultureInfo.InvariantCulture) + " objects");

            foreach (KeyValuePair<string, int> pair in counts)
            {
                c.Write(
                    "  "
                        + pair.Key
                        + new string(' ', Math.Max(1, 9 - pair.Key.Length))
                        + pair.Value.ToString(CultureInfo.InvariantCulture)
                );
            }

            // A block whose name starts with * is a space or an anonymous block, and is not counted.
            int layers = 0;
            int blocks = 0;

            foreach (DxfLayerTableRecord layer in database.LayerTable)
            {
                layers++;
            }

            foreach (DxfBlockTableRecord block in database.BlockTable)
            {
                if (!block.Name.StartsWith("*", StringComparison.Ordinal))
                {
                    blocks++;
                }
            }

            c.Write(
                "Layers: "
                    + layers.ToString(CultureInfo.InvariantCulture)
                    + ", blocks: "
                    + blocks.ToString(CultureInfo.InvariantCulture)
            );

            // The extents are an empty box, the least corner above the greatest, until something is drawn.
            var least = database.Extmin;
            var greatest = database.Extmax;
            c.Write(
                least.X > greatest.X
                    ? "Extents: none"
                    : "Extents: ("
                        + Number(least.X)
                        + ","
                        + Number(least.Y)
                        + ") to ("
                        + Number(greatest.X)
                        + ","
                        + Number(greatest.Y)
                        + ")"
            );
            yield break;
        }

        private static string Number(double value) => value.ToString("F2", CultureInfo.InvariantCulture);
    }
}
