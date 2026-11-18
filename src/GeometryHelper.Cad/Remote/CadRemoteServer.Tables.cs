using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Cad.Remote;
using GeometryHelper.Dxf;

namespace GeometryHelper.Cad
{
    /// <summary>The layers and the layouts of a drawing, read and set from outside.</summary>
    public sealed partial class CadRemoteServer
    {
        private static JsonObject Layers(CadDocument document)
        {
            var layers = new JsonArray();

            foreach (DxfLayerTableRecord layer in document.Database.LayerTable)
            {
                var json = new JsonObject()
                    .Set("name", layer.Name)
                    .Set("on", !layer.IsOff)
                    .Set("frozen", layer.IsFrozen)
                    .Set("locked", layer.IsLocked)
                    .Set("plottable", layer.IsPlottable)
                    .Set("colorIndex", (long)layer.ColorIndex)
                    .Set("linetype", layer.LinetypeName)
                    .Set("lineWeight", (long)layer.LineWeight)
                    .Set("description", layer.Description);

                if (layer.TrueColor.HasValue)
                {
                    json.Set("trueColor", (long)layer.TrueColor.Value);
                }

                layers.Add(json);
            }

            return new JsonObject().Set("layers", layers);
        }

        private static JsonObject CurrentLayer(CadDocument document, string name)
        {
            if (name != null)
            {
                CadEditResult result = CadLayerEditor.SetCurrent(document, name);

                if (!result.Succeeded)
                {
                    throw new Refusal(result.Message);
                }
            }

            return new JsonObject().Set("name", document.Database.Clayer);
        }

        private static JsonObject Layouts(CadDocument document)
        {
            var names = new JsonArray();

            foreach (CadSpace space in document.Spaces.Where(s => !s.IsBlockEditor))
            {
                names.Add(space.Name);
            }

            return new JsonObject().Set("layouts", names);
        }

        private JsonObject CurrentLayout(CadDocument document, string name)
        {
            if (name != null)
            {
                CadSpace space = SpaceNamed(document, name);
                CadCommandSession session = _host.SessionOf(document);

                if (document.IsInGroup || (session != null && session.IsRunning))
                {
                    throw new Refusal(
                        "The drawing "
                            + document.Title
                            + " is busy with the command "
                            + (session?.Current?.Name ?? "running in it")
                            + ": end it, then switch the layout again."
                    );
                }

                if (!ReferenceEquals(document, _host.ActiveDocument))
                {
                    _host.Activate(document);
                }

                CadEditResult result = document.ShowSpace(space);

                if (!result.Succeeded)
                {
                    throw new Refusal(result.Message);
                }
            }

            return new JsonObject().Set("name", (document.CurrentSpace ?? document.ModelSpace).Name);
        }

        // ---- Layers in a commit --------------------------------------------------------------------------------------

        private static Step PlanNewLayer(CadDocument document, JsonObject look, HashSet<string> added)
        {
            string name = look?.GetString("name")?.Trim();

            if (!CadLayerEditor.IsLayerName(name))
            {
                throw new Refusal("\"" + name + "\" is an invalid layer name.");
            }

            if (document.Database.LayerTable.Has(name) || !added.Add(name))
            {
                throw new Refusal("Layer \"" + name + "\" already exists.");
            }

            CheckLook(document, look);
            return new Step { Kind = StepKind.AddLayer, Layer = look };
        }

        private static Step PlanLayerLook(CadDocument document, JsonObject look, List<CadEditRecord> planned)
        {
            string name = look?.GetString("name")?.Trim();

            if (string.IsNullOrEmpty(name) || !document.Database.LayerTable.Has(name))
            {
                throw new Refusal("The drawing " + document.Title + " has no layer \"" + name + "\".");
            }

            DxfLayerTableRecord layer = document.Database.LayerTable[name];

            if (layer.IsDependent)
            {
                throw new Refusal(
                    "The layer " + layer.Name + " comes from an external reference, and is changed in its own drawing."
                );
            }

            if (
                look.GetBool("frozen")
                && !layer.IsFrozen
                && string.Equals(layer.Name, document.Database.Clayer, StringComparison.OrdinalIgnoreCase)
            )
            {
                throw new Refusal("Cannot freeze layer \"" + layer.Name + "\".  It is the CURRENT layer.");
            }

            CheckLook(document, look);
            planned.Add(
                new CadEditRecord(CadEditRecordKind.TableChanged, null, "Layer " + layer.Name, null, "Modified")
                {
                    IsLayerLook = true,
                }
            );
            return new Step { Kind = StepKind.ModifyLayer, Layer = look };
        }

        /// <summary>Refuses a look the drawing cannot give a layer, in words, before anything is applied.</summary>
        private static void CheckLook(CadDocument document, JsonObject look)
        {
            string linetype = look.GetString("linetype") ?? "Continuous";

            if (!document.Database.LinetypeTable.Has(linetype))
            {
                throw new Refusal("The linetype " + linetype + " is not in the drawing " + document.Title + ".");
            }
        }

        private static void WriteLayer(CadDocument document, CadEditScope scope, Step step)
        {
            JsonObject look = step.Layer;
            string name = look.GetString("name").Trim();

            if (step.Kind == StepKind.AddLayer)
            {
                // A layer made in code takes its look before it is added.
                var layer = new DxfLayerTableRecord { Name = name };
                WriteLook(layer, look);
                document.Database.LayerTable.Add(layer);
                layer.Description = look.GetString("description") ?? string.Empty;
                scope.Record(new CadEditRecord(CadEditRecordKind.TableChanged, null, "New layer", null, name));
                return;
            }

            DxfLayerTableRecord existing = document.Database.LayerTable[name];
            WriteLook(existing, look);
            existing.Description = look.GetString("description") ?? string.Empty;
        }

        private static void WriteLook(DxfLayerTableRecord layer, JsonObject look)
        {
            if (look.Get("trueColor") != null)
            {
                int rgb = (int)look.GetLong("trueColor");
                layer.Color = DxfColor.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
            }
            else
            {
                layer.Color = DxfColor.FromColorIndex(DxfColorMethod.ByAci, (short)look.GetLong("colorIndex", 7));
            }

            layer.LinetypeName = look.GetString("linetype") ?? "Continuous";
            layer.LineWeight = (DxfLineWeight)look.GetLong("lineWeight", (long)DxfLineWeight.ByLineWeightDefault);
            layer.IsOff = !look.GetBool("on", true);
            layer.IsFrozen = look.GetBool("frozen");
            layer.IsLocked = look.GetBool("locked");
            layer.IsPlottable = look.GetBool("plottable", true);
        }
    }
}
