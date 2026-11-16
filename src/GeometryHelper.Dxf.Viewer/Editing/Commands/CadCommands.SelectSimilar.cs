using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>The mode of SELECTSIMILAR handed to the window's Select Similar Settings dialog, which sets it before it closes.</summary>
    internal sealed class CadSelectSimilarRequest
    {
        /// <summary>Gets or sets the bits of SELECTSIMILARMODE.</summary>
        internal int Mode { get; set; }
    }

    /// <summary>
    /// SELECTSIMILAR and ADDSELECTED of AutoCAD 2021, and the variable SELECTSIMILARMODE that says what SELECTSIMILAR matches
    /// (<see cref="CadSelectSimilar"/>).
    /// </summary>
    internal static partial class CadCommands
    {
        /// <summary>The key of SELECTSIMILARMODE in <see cref="CadCommandSession.Remembered"/>.</summary>
        private const string SelectSimilarModeKey = "selectsimilarmode";

        private static void AddSelectSimilarTo(CadCommandRegistry registry)
        {
            registry.Add(new CadCommand("SELECTSIMILAR", SelectSimilar, true));
            registry.Add(new CadCommand("ADDSELECTED", AddSelected, true));
            registry.AddVariable(
                CadSystemVariable.Whole(
                    "SELECTSIMILARMODE",
                    false,
                    0,
                    CadSelectSimilar.All,
                    s => SelectSimilarMode(s),
                    (c, v) => c.Remember(SelectSimilarModeKey, v)
                )
            );
        }

        /// <summary>The bits of SELECTSIMILARMODE: 130, the layer and the name, until set.</summary>
        internal static int SelectSimilarMode(CadCommandSession session) =>
            (int)Kept(session, SelectSimilarModeKey, CadSelectSimilar.Default);

        /// <summary>
        /// SELECTSIMILAR: "Select objects or [SEttings]:" until Enter (the objects selected before it are taken instead), then
        /// every object of the space like one of them selected, by the properties SELECTSIMILARMODE names. SEttings shows the
        /// Select Similar Settings dialog, which sets the mode; with no window it says so.
        /// </summary>
        private static IEnumerable<CadPrompt> SelectSimilar(CadCommandContext c)
        {
            IReadOnlyList<string> seeds = c.PreSelected;

            while (seeds.Count == 0)
            {
                yield return CadPrompt.ForSelection("Select objects", new[] { "SEttings" });

                if (c.Input.Is("SEttings"))
                {
                    var request = new CadSelectSimilarRequest { Mode = SelectSimilarMode(c.Session) };

                    if (c.Session.Request("SELECTSIMILAR", c.Document, request))
                    {
                        c.Remember(SelectSimilarModeKey, request.Mode);
                    }
                    else
                    {
                        c.Write("Select Similar Settings is shown by the window: set SELECTSIMILARMODE here.");
                    }

                    continue;
                }

                if (c.Input.Kind != CadInputKind.Selection || c.Input.Handles.Count == 0)
                {
                    yield break;
                }

                seeds = c.Input.Handles;
            }

            c.Document.Selection.Set(CadSelectSimilar.Find(c.Document, c.Space, seeds, SelectSimilarMode(c.Session)));
        }

        /// <summary>
        /// ADDSELECTED: "Select object:", then the command that draws an object of its kind, started with the layer, colour,
        /// linetype, linetype scale and lineweight of the object picked as those of the drawing, which come back when it ends.
        /// An object selected alone before it is taken without asking. A colour of true colour is drawn as its nearest index,
        /// and an object of a kind with no command to draw it is refused.
        /// </summary>
        private static IEnumerable<CadPrompt> AddSelected(CadCommandContext c)
        {
            string handle;

            if (c.PreSelected.Count == 1)
            {
                handle = c.PreSelected[0];
            }
            else
            {
                yield return CadPrompt.ForEntity("Select object");

                if (c.Input.Kind != CadInputKind.Selection || c.Input.Handles.Count == 0)
                {
                    yield break;
                }

                handle = c.Input.Handles[0];
            }

            DxfEntity source = c.Document.FindEntity(handle);
            CadCommand draw = source == null ? null : c.Session.Registry.Find(DrawingCommandOf(source) ?? string.Empty);

            if (draw == null)
            {
                c.Write("This kind of object cannot be added.");
                yield break;
            }

            c.Write("_." + draw.Name.ToLowerInvariant());
            DxfDatabase database = c.Database;
            string layer = database.Clayer;
            DxfColor color = database.Cecolor;
            string linetype = database.Celtype;
            double scale = database.Celtscale;
            DxfLineWeight weight = database.Celweight;

            try
            {
                if (database.LayerTable.Has(source.Layer) && !database.LayerTable[source.Layer].IsFrozen)
                {
                    database.Clayer = database.LayerTable[source.Layer].Name;
                }

                DxfColor sourceColor = source.Color;
                database.Cecolor =
                    sourceColor.ColorMethod == DxfColorMethod.ByColor
                        ? DxfColor.OfIndex(sourceColor.ColorIndex)
                        : sourceColor;
                database.Celtype = source.Linetype;
                database.Celtscale = source.LinetypeScale;
                database.Celweight = source.LineWeight;

                foreach (CadPrompt prompt in draw.Body(c))
                {
                    yield return prompt;
                }
            }
            finally
            {
                database.Clayer = layer;
                database.Cecolor = color;
                database.Celtype = linetype;
                database.Celtscale = scale;
                database.Celweight = weight;
            }
        }

        /// <summary>The command that draws an object of the kind of an entity, or null where there is none.</summary>
        private static string DrawingCommandOf(DxfEntity entity)
        {
            switch (entity)
            {
                case DxfLine _:
                    return "LINE";
                case DxfCircle _:
                    return "CIRCLE";
                case DxfArc _:
                    return "ARC";
                case DxfEllipse _:
                    return "ELLIPSE";
                case DxfPolyline _:
                    return "PLINE";
                case DxfDBPoint _:
                    return "POINT";
                case DxfXline _:
                    return "XLINE";
                case DxfRay _:
                    return "RAY";
                case DxfSolid _:
                    return "SOLID";
                case DxfHatch _:
                    return "HATCH";
                case DxfSpline _:
                    return "SPLINE";
                case DxfTolerance _:
                    return "TOLERANCE";
                case DxfLeader _:
                    return "LEADER";
                case DxfMLeader _:
                    return "MLEADER";
                case DxfMText _:
                    return "MTEXT";
                case DxfTable _:
                    return "TABLE";
                case DxfRotatedDimension _:
                    return "DIMLINEAR";
                case DxfAlignedDimension _:
                    return "DIMALIGNED";
                case DxfRadialDimension _:
                case DxfRadialDimensionLarge _:
                    return "DIMRADIUS";
                case DxfDiametricDimension _:
                    return "DIMDIAMETER";
                case DxfLineAngularDimension2 _:
                case DxfPoint3AngularDimension _:
                    return "DIMANGULAR";
                case DxfOrdinateDimension _:
                    return "DIMORDINATE";
                case DxfArcDimension _:
                    return "DIMARC";
                case DxfAttributeDefinition _:
                case DxfAttributeReference _:
                    return null;
                case DxfDBText _:
                    return "TEXT";
                default:
                    return null;
            }
        }
    }
}
