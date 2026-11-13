using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    public static partial class CadPropertyEditor
    {
        /// <summary>The rows of an OLE object that can be set; its Type and Source application are read only.</summary>
        private static readonly HashSet<string> OleRowLabels = new HashSet<string>(StringComparer.Ordinal)
        {
            "Position X",
            "Position Y",
            "Width",
            "Height",
            "Scale width",
            "Scale height",
            "Lock aspect",
            "Plot quality",
        };

        /// <summary>
        /// An OLE object's rows: Position X and Position Y move the frame by its lower left corner; Width and Height size it from
        /// its upper left corner, the other side following while Lock aspect is on; Scale width and Scale height take a
        /// percent, with or without the sign, of the size at 100 percent, as the library's setters do; Lock aspect is Yes or No;
        /// Plot quality is Monochrome, Low graphics or High graphics, any text after the first words ignored.
        /// </summary>
        private static string OleRow(string label, string text, ref Action<DxfEntity> write)
        {
            switch (label)
            {
                case "Position X":
                case "Position Y":
                {
                    if (!Number(text, out double at))
                    {
                        return label + " is a number.";
                    }

                    bool x = label == "Position X";
                    write = e =>
                    {
                        var f = (DxfOle2Frame)e;
                        double by = at - (x ? f.Location.X : f.Location.Y);
                        f.TransformBy(GeoTransform3.Translation(new GeoVector3(x ? by : 0.0, x ? 0.0 : by, 0.0)));
                    };
                    return null;
                }

                case "Width":
                case "Height":
                case "Scale width":
                case "Scale height":
                {
                    string number = label.StartsWith("Scale", StringComparison.Ordinal)
                        ? text.TrimEnd('%').Trim()
                        : text;

                    if (!Number(number, out double size) || !(size > 0.0))
                    {
                        return label + " is a number above nought.";
                    }

                    switch (label)
                    {
                        case "Width":
                            write = e => ((DxfOle2Frame)e).WcsWidth = size;
                            break;
                        case "Height":
                            write = e => ((DxfOle2Frame)e).WcsHeight = size;
                            break;
                        case "Scale width":
                            write = e => ((DxfOle2Frame)e).ScaleWidth = size;
                            break;
                        default:
                            write = e => ((DxfOle2Frame)e).ScaleHeight = size;
                            break;
                    }

                    return null;
                }

                case "Lock aspect":
                {
                    bool yes = string.Equals(text, "Yes", StringComparison.OrdinalIgnoreCase);

                    if (!yes && !string.Equals(text, "No", StringComparison.OrdinalIgnoreCase))
                    {
                        return "Lock aspect is Yes or No.";
                    }

                    write = e => ((DxfOle2Frame)e).LockAspect = yes;
                    return null;
                }

                default:
                {
                    int quality =
                        text.StartsWith("Monochrome", StringComparison.OrdinalIgnoreCase) ? 0
                        : text.StartsWith("Low graphics", StringComparison.OrdinalIgnoreCase) ? 1
                        : text.StartsWith("High graphics", StringComparison.OrdinalIgnoreCase) ? 2
                        : -1;

                    if (quality < 0)
                    {
                        return "Plot quality is Monochrome, Low graphics or High graphics.";
                    }

                    write = e => ((DxfOle2Frame)e).OutputQuality = (byte)quality;
                    return null;
                }
            }
        }
    }
}
