using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// What a tool of a palette does when it runs: the command it starts, wrapped so that the layer, colour, linetype and
    /// lineweight it sets (and, for a hatch tool, the pattern, angle and scale) are the current ones while it runs and the
    /// drawing's own are put back when it ends, however it ends; and the answers a block tool gives INSERT, which asks nothing
    /// of a palette. The session starts it (<see cref="CadCommandSession.StartTool"/>).
    /// </summary>
    internal static class CadToolRun
    {
        /// <summary>The words of a command tool's line: a leading <c>^C^C</c> and underscore left off, parted by blanks and semicolons.</summary>
        internal static IReadOnlyList<string> Words(string command)
        {
            string text = (command ?? string.Empty).Trim();

            while (text.StartsWith("^C", StringComparison.OrdinalIgnoreCase))
            {
                text = text.Substring(2).TrimStart();
            }

            text = text.TrimStart('_');
            return text.Split(new[] { ' ', '\t', ';' }, StringSplitOptions.RemoveEmptyEntries);
        }

        /// <summary>
        /// Wraps a command so that the overrides of a tool hold while it runs. A command a tool with no overrides starts is not
        /// wrapped, which makes it the command typed.
        /// </summary>
        internal static CadCommand Wrap(CadCommand inner, CadTool tool) =>
            tool.HasOverrides || tool.Kind == CadToolKind.Hatch
                ? new CadCommand(inner.Name, c => Overridden(c, inner, tool), inner.UsesSelection)
                : inner;

        private static IEnumerable<CadPrompt> Overridden(CadCommandContext c, CadCommand inner, CadTool tool)
        {
            string layer = c.Database.Clayer;
            DxfColor color = c.Database.Cecolor;
            string linetype = c.Database.Celtype;
            DxfLineWeight weight = c.Database.Celweight;
            CadHatchSettings hatch = c.Document.HatchSettings;

            try
            {
                Apply(c, tool);

                foreach (CadPrompt prompt in inner.Body(c))
                {
                    yield return prompt;
                }
            }
            finally
            {
                // The drawing may be another object by now (an undo makes a new one): what is put back is read from this one.
                DxfDatabase database = c.Database;

                if (tool.Layer != null && database.LayerTable.Has(layer) && !string.Equals(database.Clayer, layer))
                {
                    database.Clayer = layer;
                }

                if (tool.Color != null && !database.Cecolor.Equals(color))
                {
                    database.Cecolor = color;
                }

                if (tool.Linetype != null && !string.Equals(database.Celtype, linetype, StringComparison.Ordinal))
                {
                    database.Celtype = linetype;
                }

                if (tool.LineWeight != null && database.Celweight != weight)
                {
                    database.Celweight = weight;
                }

                if (tool.Kind == CadToolKind.Hatch)
                {
                    c.Document.HatchSettings = hatch;
                }
            }
        }

        /// <summary>Makes the overrides current; one that cannot be said on the command line and left as it is.</summary>
        private static void Apply(CadCommandContext c, CadTool tool)
        {
            DxfDatabase database = c.Database;

            if (tool.Layer != null)
            {
                SetLayer(c, tool.Layer);
            }

            if (tool.Color != null)
            {
                string refused = CadPropertyEditor.ParseColor(tool.Color, out DxfColor color);

                if (refused == null && color.IsByColor)
                {
                    refused = "The drawing keeps the colour of new objects as an index: ByLayer, ByBlock or 1 to 255.";
                }

                if (refused != null)
                {
                    c.Write("The colour " + tool.Color + " of the tool is not used: " + refused);
                }
                else
                {
                    database.Cecolor = color;
                }
            }

            if (tool.Linetype != null)
            {
                string name =
                    string.Equals(tool.Linetype, "ByLayer", StringComparison.OrdinalIgnoreCase) ? "ByLayer"
                    : string.Equals(tool.Linetype, "ByBlock", StringComparison.OrdinalIgnoreCase) ? "ByBlock"
                    : database
                        .LinetypeTable.Select(l => l.Name)
                        .FirstOrDefault(n => string.Equals(n, tool.Linetype, StringComparison.OrdinalIgnoreCase));

                if (name == null)
                {
                    c.Write("Linetype " + tool.Linetype + " of the tool is not loaded: the current linetype is used.");
                }
                else
                {
                    database.Celtype = name;
                }
            }

            if (tool.LineWeight != null)
            {
                try
                {
                    database.Celweight = (DxfLineWeight)tool.LineWeight.Value;
                }
                catch (Exception exception)
                    when (exception is ArgumentException || exception is InvalidOperationException)
                {
                    c.Write(
                        "The lineweight " + tool.LineWeight.Value + " of the tool is not used: " + exception.Message
                    );
                }
            }

            if (tool.Kind == CadToolKind.Hatch)
            {
                c.Document.HatchSettings = HatchSettingsOf(tool, c.Document.HatchSettings);
            }
        }

        /// <summary>Makes a layer current, the layer made first where the drawing has none; a frozen one, or a name no layer has, is said and left.</summary>
        private static void SetLayer(CadCommandContext c, string name)
        {
            if (!c.Database.LayerTable.Has(name))
            {
                c.OpenUndoStep();
                CadEditResult made = CadLayerEditor.Add(c.Document, name);

                if (!made.Succeeded)
                {
                    c.Write("The layer " + name + " of the tool is not used: " + made.Message);
                    return;
                }
            }

            DxfLayerTableRecord layer = c.Database.LayerTable[name];

            if (layer.IsFrozen || layer.IsDependent)
            {
                c.Write("The layer " + layer.Name + " of the tool cannot be current: the current layer is used.");
                return;
            }

            c.Database.Clayer = layer.Name;
        }

        /// <summary>The settings HATCH makes a tool's hatch with: the document's, the type, pattern, angle, scale and association of the tool put over them.</summary>
        internal static CadHatchSettings HatchSettingsOf(CadTool tool, CadHatchSettings basis)
        {
            CadHatchSettings settings = basis.Clone();
            settings.Type = tool.HatchType;
            settings.PatternName = tool.Pattern;
            settings.PatternFile = null;
            settings.Angle = tool.Angle;
            settings.Scale = tool.Scale;
            settings.Spacing = tool.Spacing;
            settings.GradientName = tool.Gradient;
            settings.Associative = tool.Associative;

            // The layer and colour are the tool's overrides, the current ones while it runs.
            settings.Layer = null;
            settings.Color = null;
            return settings;
        }

        /// <summary>
        /// What is wrong with a block tool before it starts, null where it can: the block is not in the drawing open and the
        /// tool's drawing is no file. <paramref name="file"/> is the drawing INSERT takes the block from, null for the drawing open.
        /// </summary>
        internal static string BlockProblem(CadDocument document, CadTool tool, out string file)
        {
            file = null;

            // A block the drawing has is placed as it is, whatever file the tool names: no question of redefining it.
            if (tool.BlockName.Length > 0 && document.Database.BlockTable.Has(tool.BlockName))
            {
                return null;
            }

            if (string.IsNullOrEmpty(tool.File))
            {
                return "The drawing has no block " + tool.BlockName + ".";
            }

            if (!System.IO.File.Exists(tool.File))
            {
                return tool.File + " cannot be found.";
            }

            file = Path.GetFullPath(tool.File);
            return null;
        }

        /// <summary>Gives INSERT's request the answers of a block tool: its block, scale, rotation, whether they are asked, and where it is put.</summary>
        internal static void Answer(CadInsertRequest request, CadTool tool, string file, GeoPoint2? at)
        {
            request.FileName = file;
            request.Name = tool.BlockName;
            request.Scale = new GeoVector3(tool.Scale, tool.Scale, tool.Scale);
            request.SpecifyScaleOnScreen = tool.PromptForScale;
            request.Rotation = tool.Angle * (Math.PI / 180.0);
            request.SpecifyRotationOnScreen = tool.PromptForRotation;
            request.Explode = tool.Explode;
            request.Repeat = false;
            request.SpecifyPointOnScreen = !at.HasValue;

            if (at.HasValue)
            {
                request.Point = new GeoPoint3(at.Value.X, at.Value.Y, 0.0);
            }

            request.Action = CadDialogAction.Ok;
        }
    }
}
