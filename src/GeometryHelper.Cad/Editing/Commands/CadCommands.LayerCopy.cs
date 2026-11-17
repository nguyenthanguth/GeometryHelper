using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// LAYCUR and COPYTOLAYER, as AutoCAD 2021 asks them on the command line (COPYTOLAYER as -COPYTOLAYER does; its dialog is
    /// not built). LAYCUR puts the objects selected on the current layer. COPYTOLAYER copies them onto a layer, named, listed
    /// with ?, taken from an object with =, or made where the drawing has none, and then asks the base point to move the copies
    /// by (Displacement, or the second point) or eXit to leave them where the originals stand.
    /// </summary>
    internal static partial class CadCommands
    {
        private static void AddLayerCopyTo(CadCommandRegistry registry)
        {
            registry.Add(new CadCommand("LAYCUR", LayerToCurrent, true));
            registry.Add(new CadCommand("COPYTOLAYER", CopyToLayer, true));
            registry.Add(new CadCommand("-COPYTOLAYER", CopyToLayer, true));
        }

        /// <summary>LAYCUR: "Select objects to be changed to the current layer:", then "2 objects changed to layer "B" (the current layer)."</summary>
        private static IEnumerable<CadPrompt> LayerToCurrent(CadCommandContext c)
        {
            foreach (
                CadPrompt prompt in c.SelectObjectsOf("Select objects to be changed to the current layer", e => true)
            )
            {
                yield return prompt;
            }

            if (c.Selected.Count == 0)
            {
                yield break;
            }

            string layer = c.Database.Clayer;
            c.OpenUndoStep();
            CadEditResult result = CadPropertyEditor.Set(c.Document, c.Selected, "Layer", layer);

            if (!result.Succeeded)
            {
                c.Write(result.Message);
                yield break;
            }

            // AutoCAD 2021 words one object differently: no quotes round the layer.
            c.Write(
                c.Selected.Count == 1
                    ? "One object changed to layer " + layer + " (the current layer)."
                    : c.Selected.Count.ToString(CultureInfo.InvariantCulture)
                        + " objects changed to layer \""
                        + layer
                        + "\" (the current layer)."
            );
        }

        private static IEnumerable<CadPrompt> CopyToLayer(CadCommandContext c)
        {
            foreach (CadPrompt prompt in c.SelectObjectsOf("Select objects to copy", e => true))
            {
                yield return prompt;
            }

            if (c.Selected.Count == 0)
            {
                yield break;
            }

            string layer = null;
            bool make = false;

            while (layer == null)
            {
                yield return CadPrompt
                    .ForKeywordOrText("Specify the destination layer name or", new[] { "?", "=" }, c.Database.Clayer)
                    .WithKeywordNote(" (select object)");

                if (c.Input.IsNone)
                {
                    layer = c.Database.Clayer;
                }
                else if (c.Input.Is("?"))
                {
                    foreach (CadPrompt prompt in ListLayerNames(c))
                    {
                        yield return prompt;
                    }
                }
                else if (c.Input.Is("="))
                {
                    yield return CadPrompt.ForEntity("Select an object with the desired layer name").Expecting("Last");
                    layer = LayerOf(c);
                }
                else if (c.Input.Kind == CadInputKind.Text)
                {
                    string typed = c.Input.Text.Trim();

                    if (!CadLayerEditor.IsLayerName(typed))
                    {
                        c.Write("Invalid characters in layer name.");
                        c.Write(string.Empty);
                        c.Write("The following characters are not valid in layer names:");
                        c.Write("        <>/\\\"\":;?*|,=`");
                    }
                    else if (c.Database.LayerTable.Has(typed))
                    {
                        if (c.Database.LayerTable[typed].IsDependent)
                        {
                            c.Write("Invalid Layer name.");
                        }
                        else
                        {
                            layer = c.Database.LayerTable[typed].Name;
                        }
                    }
                    else
                    {
                        yield return CadPrompt.ForKeyword(
                            "The layer '" + typed + "' does not exist. Do you want to create it?",
                            new[] { "Yes", "No" }
                        );

                        if (c.Input.Is("Yes"))
                        {
                            layer = typed;
                            make = true;
                        }
                    }
                }
            }

            c.OpenUndoStep();

            if (make)
            {
                CadEditResult added = CadLayerEditor.Add(c.Document, layer);

                if (!added.Succeeded)
                {
                    c.Write(added.Message);
                    yield break;
                }
            }

            List<string> copies = Transform(c, c.Selected, GeoTransform3.Identity, true);
            CadEditResult placed = CadPropertyEditor.Set(c.Document, copies, "Layer", layer);

            if (!placed.Succeeded)
            {
                c.Write(placed.Message);
                yield break;
            }

            c.Write(string.Empty);
            c.Write(
                c.Selected.Count.ToString(CultureInfo.InvariantCulture)
                    + " object(s) copied and placed on layer \""
                    + layer
                    + "\"."
            );
            yield return CadPrompt.ForPoint("Specify base point", null, new[] { "Displacement", "eXit" }, "eXit");
            GeoVector2? move = null;

            if (c.Input.IsPoint)
            {
                GeoPoint2 from = c.Input.Point;
                yield return CadPrompt.ForPoint(
                    "Specify second point of displacement or",
                    from,
                    null,
                    "use first point as displacement"
                );
                move = c.Input.IsPoint ? c.Input.Point - from : AsDisplacement(c, from);
            }
            else if (c.Input.Is("Displacement"))
            {
                yield return CadPrompt.ForPoint(
                    "Specify displacement",
                    null,
                    null,
                    Length(0.0) + ", " + Length(0.0) + ", " + Length(0.0)
                );
                move = c.Input.IsPoint ? AsDisplacement(c, c.Input.Point) : (GeoVector2?)null;
            }

            if (move.HasValue)
            {
                Transform(c, copies, GeoTransform3.Translation(new GeoVector3(move.Value.X, move.Value.Y, 0.0)), false);
            }
        }
    }
}
