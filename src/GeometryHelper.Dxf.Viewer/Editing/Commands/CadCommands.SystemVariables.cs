using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// SETVAR and the system variables, in AutoCAD 2021's words: "Enter variable name or [?] &lt;OSMODE&gt;:", then "Enter
    /// new value for OSMODE &lt;4133&gt;:"; a variable's name typed alone asks the same. Each variable reads and sets the value
    /// where the presenter keeps it: the header of the drawing, as AutoCAD writes it, through the typed properties of
    /// <see cref="DxfDatabase"/>, a change one undo step; the document or the session, at once. The window adds those it keeps
    /// itself, OSMODE and the status bar's toggles among them.
    /// </summary>
    internal static partial class CadCommands
    {
        internal static void AddVariablesTo(CadCommandRegistry registry)
        {
            registry.Add(new CadCommand("SETVAR", SetVariable));

            // The layer, colour, linetype, lineweight and linetype scale new objects take.
            registry.AddVariable(CadSystemVariable.Text("CLAYER", true, s => s.Document.Database.Clayer, CurrentLayer));
            registry.AddVariable(
                CadSystemVariable
                    .Text("CECOLOR", true, s => ColorShown(s.Document.Database.Cecolor), CurrentColorTyped)
                    .Asking(CurrentColor)
            );
            registry.AddVariable(
                CadSystemVariable
                    .Text("CELTYPE", true, s => s.Document.Database.Celtype, CurrentLinetypeTyped)
                    .Asking(CurrentLinetype)
            );
            registry.AddVariable(
                CadSystemVariable.Whole(
                    "CELWEIGHT",
                    true,
                    -3,
                    211,
                    s => (int)s.Document.Database.Celweight,
                    (c, v) => c.Database.Celweight = (DxfLineWeight)v,
                    check: LineweightRefused
                )
            );
            registry.AddVariable(
                CadSystemVariable.Number(
                    "CELTSCALE",
                    true,
                    CadSystemVariable.RealBound.Positive,
                    false,
                    s => s.Document.Database.Celtscale,
                    (c, v) => c.Database.Celtscale = v
                )
            );

            // The scales of linetypes.
            registry.AddVariable(
                CadSystemVariable
                    .Number(
                        "LTSCALE",
                        true,
                        CadSystemVariable.RealBound.Positive,
                        false,
                        s => s.Document.Database.Ltscale,
                        (c, v) => c.Database.Ltscale = v
                    )
                    .Asking(LinetypeScale)
            );
            registry.AddVariable(
                Flag("PSLTSCALE", s => s.Document.Database.Psltscale, (c, v) => c.Database.Psltscale = v, "$PSLTSCALE")
            );
            registry.AddVariable(
                Flag("MSLTSCALE", s => s.Document.Database.MsLtScale, (c, v) => c.Database.MsLtScale = v, "MSLTSCALE")
            );

            // Dimensions and text.
            registry.AddVariable(
                CadSystemVariable.Number(
                    "DIMSCALE",
                    true,
                    CadSystemVariable.RealBound.NonNegative,
                    false,
                    s => s.Document.Database.Dimscale,
                    (c, v) => c.Database.Dimscale = v
                )
            );
            registry.AddVariable(
                CadSystemVariable.ReadOnly("DIMSTYLE", CadVariableKind.Text, s =>
                    "\"" + s.Document.Database.Dimstyle + "\""
                )
            );

            // The multileader style new multileaders take, and their scale: the style's until set, nought for one annotative.
            registry.AddVariable(
                CadSystemVariable.Text(
                    "CMLEADERSTYLE",
                    true,
                    s => CadMLeaderStyleEditor.Current(s.Document.Database),
                    CurrentMLeaderStyle
                )
            );
            registry.AddVariable(
                CadSystemVariable.Number(
                    "MLEADERSCALE",
                    true,
                    CadSystemVariable.RealBound.Finite,
                    false,
                    s => s.Document.Database.MLeaderscale,
                    (c, v) => c.Database.MLeaderscale = v,
                    check: v => v < 0.0 ? "Cannot set MLEADERSCALE to that value." : null
                )
            );
            registry.AddVariable(
                CadSystemVariable.Number(
                    "TEXTSIZE",
                    true,
                    CadSystemVariable.RealBound.Positive,
                    false,
                    TextSizeOffered,
                    SetTextSize
                )
            );
            registry.AddVariable(
                CadSystemVariable.Text("TEXTSTYLE", true, s => s.Document.Database.Textstyle, CurrentTextStyle)
            );
            registry.AddVariable(
                Flag("MIRRTEXT", s => s.Document.Database.Mirrtext, (c, v) => c.Database.Mirrtext = v, "$MIRRTEXT")
            );

            // Fillets, chamfers and offsets.
            registry.AddVariable(
                CadSystemVariable.Number(
                    "FILLETRAD",
                    true,
                    CadSystemVariable.RealBound.NonNegative,
                    false,
                    s => s.Document.Database.Filletrad,
                    (c, v) => c.Database.Filletrad = v
                )
            );
            registry.AddVariable(
                CadSystemVariable.Number(
                    "CHAMFERA",
                    true,
                    CadSystemVariable.RealBound.NonNegative,
                    false,
                    s => s.Document.Database.Chamfera,
                    (c, v) => c.Database.Chamfera = v
                )
            );
            registry.AddVariable(
                CadSystemVariable.Number(
                    "CHAMFERB",
                    true,
                    CadSystemVariable.RealBound.NonNegative,
                    false,
                    s => s.Document.Database.Chamferb,
                    (c, v) => c.Database.Chamferb = v
                )
            );
            registry.AddVariable(
                CadSystemVariable.Number(
                    "OFFSETDIST",
                    false,
                    CadSystemVariable.RealBound.Finite,
                    false,
                    s => Kept(s, "offset.distance", -1.0),
                    (c, v) => c.Remember("offset.distance", v)
                )
            );

            // Points.
            registry.AddVariable(
                CadSystemVariable.Whole(
                    "PDMODE",
                    true,
                    short.MinValue,
                    short.MaxValue,
                    s => s.Document.Database.Pdmode,
                    (c, v) => c.Database.Pdmode = v
                )
            );
            registry.AddVariable(
                CadSystemVariable.Number(
                    "PDSIZE",
                    true,
                    CadSystemVariable.RealBound.Finite,
                    false,
                    s => s.Document.Database.Pdsize,
                    (c, v) => c.Database.Pdsize = v
                )
            );

            // Selection, which the document keeps for the presenter, not the drawing.
            registry.AddVariable(
                CadSystemVariable.Whole(
                    "PICKSTYLE",
                    false,
                    0,
                    3,
                    s => s.Document.PickStyle,
                    (c, v) => c.Document.PickStyle = v
                )
            );
            registry.AddVariable(
                CadSystemVariable.Whole(
                    "SELECTIONCYCLING",
                    false,
                    0,
                    2,
                    s => s.Document.SelectionCycling,
                    (c, v) => c.Document.SelectionCycling = v
                )
            );
            registry.AddVariable(
                CadSystemVariable.Whole(
                    "PICKFIRST",
                    false,
                    0,
                    1,
                    s => s.Document.PickFirst,
                    (c, v) => c.Document.PickFirst = v
                )
            );
            registry.AddVariable(
                CadSystemVariable.Whole(
                    "PICKADD",
                    false,
                    0,
                    2,
                    s => s.Document.PickAdd,
                    (c, v) => c.Document.PickAdd = v
                )
            );

            // Whether INSERT asks the values of a block's attributes (1) or gives each its default (0), kept by the session.
            registry.AddVariable(
                CadSystemVariable.Whole(
                    "ATTREQ",
                    false,
                    0,
                    1,
                    s => (int)Kept(s, "attreq", 1.0),
                    (c, v) => c.Remember("attreq", v)
                )
            );

            // Whether INSERT asks those values in the Edit Attributes dialog (1) or on the command line (0), kept by the session.
            registry.AddVariable(
                CadSystemVariable.Whole(
                    "ATTDIA",
                    false,
                    0,
                    1,
                    s => (int)Kept(s, "attdia", 0.0),
                    (c, v) => c.Remember("attdia", v)
                )
            );
            // How -XREF Bind binds an external reference: 0 Bind, its records renamed after it, 1 Insert, kept by the session.
            registry.AddVariable(
                CadSystemVariable.Whole("BINDTYPE", false, 0, 1, s => BindType(s), (c, v) => c.Remember("bindtype", v))
            );

            // Whether a command asks the window for a file (FILEDIA) or shows its dialog (CMDDIA, PLOT), 1, or asks on the command line, 0.
            registry.AddVariable(
                CadSystemVariable.Whole(
                    "FILEDIA",
                    false,
                    0,
                    1,
                    s => s.FileDialogs ? 1 : 0,
                    (c, v) => c.Remember("filedia", v)
                )
            );
            registry.AddVariable(
                CadSystemVariable.Whole(
                    "CMDDIA",
                    false,
                    0,
                    1,
                    s => (int)Kept(s, "cmddia", 1.0),
                    (c, v) => c.Remember("cmddia", v)
                )
            );

            // Whether the Page Setup Manager is shown when a layout is made (1) or not (0), the box at the foot of the manager, kept by the session.
            registry.AddVariable(
                CadSystemVariable.Whole(
                    "SHOWPAGESETUPFORNEWLAYOUTS",
                    false,
                    0,
                    1,
                    s => (int)Kept(s, "showpagesetupfornewlayouts", 0.0),
                    (c, v) => c.Remember("showpagesetupfornewlayouts", v)
                )
            );

            // Whether PEDIT turns a line, an arc or a spline picked into a polyline without asking (1) or asks first (0), and
            // whether PLINE, POLYGON, DONUT and PEDIT make old-style POLYLINEs (0) or LWPOLYLINEs (1 and 2), kept by the session.
            registry.AddVariable(
                CadSystemVariable.Whole(
                    "PEDITACCEPT",
                    false,
                    0,
                    1,
                    s => (int)Kept(s, "peditaccept", 0.0),
                    (c, v) => c.Remember("peditaccept", v)
                )
            );
            registry.AddVariable(
                CadSystemVariable.Whole(
                    "PLINETYPE",
                    false,
                    0,
                    2,
                    s => (int)Kept(s, "plinetype", 2.0),
                    (c, v) => c.Remember("plinetype", v)
                )
            );

            // Whether DIMCONTINUE and DIMBASELINE draw in the style and on the layer of the dimension they go on from (1) or in
            // the current ones (0), kept by the session.
            registry.AddVariable(
                CadSystemVariable.Whole(
                    "DIMCONTINUEMODE",
                    false,
                    0,
                    1,
                    s => (int)Kept(s, "dimcontinuemode", 1.0),
                    (c, v) => c.Remember("dimcontinuemode", v)
                )
            );

            // REVCLOUD's approximate arc length (nought until set), whether its arcs vary about it (1) or keep to it (0), and the
            // type it starts with, 0 Freehand, 1 Rectangular, 2 Polygonal, kept by the session.
            registry.AddVariable(
                CadSystemVariable.Number(
                    "REVCLOUDAPPROXARCLEN",
                    false,
                    CadSystemVariable.RealBound.NonNegative,
                    false,
                    s => Kept(s, "revcloud.approx", 0.0),
                    (c, v) => c.Remember("revcloud.approx", v)
                )
            );
            registry.AddVariable(
                CadSystemVariable.Whole(
                    "REVCLOUDARCVARIANCE",
                    false,
                    0,
                    1,
                    s => (int)Kept(s, "revcloud.variance", 1.0),
                    (c, v) => c.Remember("revcloud.variance", v)
                )
            );
            registry.AddVariable(
                CadSystemVariable.Whole(
                    "REVCLOUDCREATEMODE",
                    false,
                    0,
                    2,
                    s => (int)Kept(s, "revcloud.type", 1.0),
                    (c, v) => c.Remember("revcloud.type", v)
                )
            );

            // What the drawing shows and how it sorts.
            registry.AddVariable(
                Flag("ORTHOMODE", s => s.Document.Database.Orthomode, (c, v) => c.Database.Orthomode = v, "$ORTHOMODE")
            );
            registry.AddVariable(
                CadSystemVariable.Switch(
                    "LWDISPLAY",
                    true,
                    s => s.Document.Database.LineWeightDisplay,
                    (c, v) => c.Database.LineWeightDisplay = v,
                    "$LWDISPLAY"
                )
            );
            registry.AddVariable(
                CadSystemVariable
                    .Whole(
                        "IMAGEFRAME",
                        true,
                        0,
                        2,
                        ImageFrameOf,
                        (c, v) =>
                            (
                                ImageVariables(c.Database) ?? DxfRasterVariables.GetRasterVariables(c.Database)
                            ).ImageFrame = FrameSettingOf(v),
                        "IMAGEFRAME",
                        ImageFrameRefused
                    )
                    .Recorded(CadEditRecordKind.TableChanged)
            );
            registry.AddVariable(
                CadSystemVariable.Whole(
                    "SORTENTS",
                    true,
                    0,
                    127,
                    s => s.Document.Database.SortEnts,
                    (c, v) => c.Database.SortEnts = v
                )
            );
            registry.AddVariable(
                CadSystemVariable.Whole(
                    "TILEMODE",
                    false,
                    0,
                    1,
                    s => s.Document.CurrentSpace.IsModel ? 1 : 0,
                    SetTileMode
                )
            );
            registry.AddVariable(
                CadSystemVariable.Text("CANNOSCALE", true, s => s.Document.Database.Cannoscale, CurrentAnnotationScale)
            );

            // The limits, the base point and the extents.
            registry.AddVariable(
                CadSystemVariable.Point(
                    "INSBASE",
                    true,
                    s => s.Document.Database.Insbase,
                    (c, v) => c.Database.Insbase = new GeoPoint3(v.X, v.Y, c.Database.Insbase.Z),
                    "$INSBASE"
                )
            );
            registry.AddVariable(
                CadSystemVariable.Point(
                    "LIMMIN",
                    false,
                    s => P3(s.Document.Database.Limmin),
                    (c, v) => c.Database.Limmin = v,
                    "$LIMMIN"
                )
            );
            registry.AddVariable(
                CadSystemVariable.Point(
                    "LIMMAX",
                    false,
                    s => P3(s.Document.Database.Limmax),
                    (c, v) => c.Database.Limmax = v,
                    "$LIMMAX"
                )
            );
            registry.AddVariable(
                CadSystemVariable.ReadOnly("EXTMIN", CadVariableKind.Point, s =>
                    CadSystemVariable.Show(s.Document.Database.Extmin, true)
                )
            );
            registry.AddVariable(
                CadSystemVariable.ReadOnly("EXTMAX", CadVariableKind.Point, s =>
                    CadSystemVariable.Show(s.Document.Database.Extmax, true)
                )
            );

            // What the session and the file say of themselves.
            registry.AddVariable(
                CadSystemVariable.ReadOnly("UCSNAME", CadVariableKind.Text, s =>
                    "\"" + (s.Ucs.Name ?? string.Empty) + "\""
                )
            );
            registry.AddVariable(
                CadSystemVariable.ReadOnly("UCSORG", CadVariableKind.Point, s =>
                    CadSystemVariable.Show(s.Ucs.Origin, true)
                )
            );
            registry.AddVariable(
                CadSystemVariable.ReadOnly("DWGNAME", CadVariableKind.Text, s => "\"" + s.Document.Title + "\"")
            );
            registry.AddVariable(
                CadSystemVariable.ReadOnly("DWGPREFIX", CadVariableKind.Text, s => "\"" + Prefix(s.Document) + "\"")
            );
            registry.AddVariable(
                CadSystemVariable.ReadOnly("DATE", CadVariableKind.Real, s => JulianDate(DateTime.Now))
            );
            registry.AddVariable(
                CadSystemVariable.ReadOnly("CDATE", CadVariableKind.Real, s =>
                    DateTime.Now.ToString("yyyyMMdd.HHmmssfff", CultureInfo.InvariantCulture)
                )
            );
        }

        /// <summary>
        /// SETVAR: "Enter variable name or [?] &lt;OSMODE&gt;:", the variable named last offered; then its new value asked as its
        /// name typed alone would ask it in SETVAR's words. ? lists the variables a pattern of wildcards matches, * every one.
        /// </summary>
        private static IEnumerable<CadPrompt> SetVariable(CadCommandContext c)
        {
            string last = c.Session.LastVariable;
            yield return CadPrompt.ForKeywordOrText("Enter variable name or", new[] { "?" }, last);

            if (c.Input.Is("?"))
            {
                yield return CadPrompt.ForString("Enter variable(s) to list", "*");
                string pattern =
                    c.Input.Kind == CadInputKind.Text && c.Input.Text.Trim().Length > 0 ? c.Input.Text.Trim() : "*";
                ListVariables(c, pattern);
                yield break;
            }

            string name =
                c.Input.Kind == CadInputKind.Text ? c.Input.Text.Trim()
                : c.Input.IsNone ? last
                : null;

            if (string.IsNullOrEmpty(name))
            {
                yield break;
            }

            CadSystemVariable variable = c.Session.Registry.FindVariable(name);

            if (variable == null || !string.Equals(variable.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                c.Write("Unknown variable name.  Type SETVAR ? for a list of variables.");
                yield break;
            }

            c.Session.LastVariable = variable.Name;

            foreach (CadPrompt prompt in variable.Ask(c))
            {
                yield return prompt;
            }
        }

        /// <summary>A variable's name typed alone: its own prompts where it has them (CECOLOR, CELTYPE, LTSCALE), else SETVAR's.</summary>
        internal static IEnumerable<CadPrompt> TypedVariable(CadCommandContext c, CadSystemVariable variable)
        {
            c.Session.LastVariable = variable.Name;
            return variable.Own != null ? variable.Own(c) : variable.Ask(c);
        }

        /// <summary>A variable of the registry asked as SETVAR asks it, as PTYPE with no window asks PDMODE and PDSIZE.</summary>
        private static IEnumerable<CadPrompt> AskVariable(CadCommandContext c, string name)
        {
            CadSystemVariable variable = c.Session.Registry.FindVariable(name);
            return variable == null ? Array.Empty<CadPrompt>() : variable.Ask(c);
        }

        /// <summary>The variables a pattern matches, each a line of its name and value, as SETVAR ? lists them.</summary>
        private static void ListVariables(CadCommandContext c, string pattern)
        {
            foreach (CadSystemVariable variable in c.Session.Registry.Variables)
            {
                if (WildcardMatches(variable.Name, pattern))
                {
                    c.Write(
                        variable.Name.PadRight(18)
                            + variable.GetValue(c.Session)
                            + (variable.IsReadOnly ? "  (read only)" : string.Empty)
                    );
                }
            }
        }

        /// <summary>A switch of the drawing, 0 or 1.</summary>
        private static CadSystemVariable Flag(
            string name,
            Func<CadCommandSession, bool> get,
            Action<CadCommandContext, bool> set,
            string record
        ) => CadSystemVariable.Whole(name, true, 0, 1, s => get(s) ? 1 : 0, (c, v) => set(c, v != 0), record);

        private static double Kept(CadCommandSession session, string key, double fallback) =>
            session.Remembered.TryGetValue(key, out double value) ? value : fallback;

        /// <summary>CLAYER: a layer of the drawing made current, as LAYER Set makes it, one undo step; one not found or frozen refused.</summary>
        private static string CurrentLayer(CadCommandContext c, string typed)
        {
            c.OpenUndoStep();
            CadEditResult result = CadLayerEditor.SetCurrent(c.Document, typed);
            return result.Succeeded ? null : result.Message;
        }

        private static string ColorShown(DxfColor color) =>
            color.IsByLayer ? "BYLAYER"
            : color.IsByBlock ? "BYBLOCK"
            : CadPropertyEditor.ColorName(color).ToLowerInvariant();

        /// <summary>CECOLOR set by SETVAR: ByLayer, ByBlock, a name from red to white or an index 1 to 255, as CECOLOR takes it.</summary>
        private static string CurrentColorTyped(CadCommandContext c, string typed)
        {
            string error = CadPropertyEditor.ParseColor(typed, out DxfColor color);

            if (error == null && color.IsByColor)
            {
                error = "The drawing keeps the colour of new objects as an index: ByLayer, ByBlock or 1 to 255.";
            }

            if (error != null)
            {
                return error;
            }

            DxfColor old = c.Database.Cecolor;

            if (!color.Equals(old))
            {
                c.Apply(
                    new[]
                    {
                        new CadEditRecord(
                            CadEditRecordKind.SettingChanged,
                            null,
                            "$CECOLOR",
                            ColorShown(old),
                            CadPropertyEditor.ColorName(color)
                        ),
                    },
                    s => s.Database.Cecolor = color
                );
            }

            return null;
        }

        /// <summary>CELTYPE set by SETVAR: ByLayer, ByBlock or a linetype loaded, as CELTYPE takes it.</summary>
        private static string CurrentLinetypeTyped(CadCommandContext c, string typed)
        {
            string name =
                string.Equals(typed, "ByLayer", StringComparison.OrdinalIgnoreCase) ? "ByLayer"
                : string.Equals(typed, "ByBlock", StringComparison.OrdinalIgnoreCase) ? "ByBlock"
                : c
                    .Database.LinetypeTable.Select(l => l.Name)
                    .FirstOrDefault(n => string.Equals(n, typed, StringComparison.OrdinalIgnoreCase));

            if (name == null)
            {
                return "Linetype " + typed + " not found. Use the Load option to load it.";
            }

            SetCurrentLinetype(c, name);
            return null;
        }

        /// <summary>A lineweight of AutoCAD's in hundredths of a millimetre, or -1 ByLayer, -2 ByBlock, -3 Default.</summary>
        private static string LineweightRefused(CadCommandContext c, int value) =>
            c.Database.IsValidLineWeight(value)
                ? null
                : "Requires a lineweight: -1 ByLayer, -2 ByBlock, -3 Default, or one of AutoCAD's in hundredths of a millimetre, as 25.";

        /// <summary>The height TEXT offers: the one the session remembers, as Global settings gives it, else the drawing's $TEXTSIZE.</summary>
        private static double TextSizeOffered(CadCommandSession session) =>
            Kept(session, "text.height", session.Document.Database.Textsize);

        /// <summary>TEXTSIZE set: the drawing's $TEXTSIZE, and the height TEXT and MTEXT offer next.</summary>
        private static void SetTextSize(CadCommandContext c, double value)
        {
            c.Database.Textsize = value;
            c.Remember("text.height", value);
        }

        /// <summary>TEXTSTYLE: a text style of the drawing made current, one undo step; one not found refused.</summary>
        private static string CurrentTextStyle(CadCommandContext c, string typed)
        {
            string name = c
                .Database.TextStyleTable.Select(r => r.Name)
                .FirstOrDefault(n => string.Equals(n, typed, StringComparison.OrdinalIgnoreCase));

            if (name == null)
            {
                return "Cannot find text style \"" + typed + "\".";
            }

            string old = c.Database.Textstyle;

            if (!string.Equals(old, name, StringComparison.Ordinal))
            {
                c.Apply(
                    new[] { new CadEditRecord(CadEditRecordKind.SettingChanged, null, "$TEXTSTYLE", old, name) },
                    s => s.Database.Textstyle = name
                );
            }

            return null;
        }

        /// <summary>
        /// CMLEADERSTYLE: a multileader style of the drawing made current, letters of either case alike, as Set Current makes it,
        /// one undo step; MLEADERSCALE then the style's scale. A name the drawing has no style of is refused as AutoCAD 2021
        /// refuses it.
        /// </summary>
        private static string CurrentMLeaderStyle(CadCommandContext c, string typed)
        {
            if (CadMLeaderStyleEditor.Find(c.Database, typed) == null)
            {
                return "Cannot set CMLEADERSTYLE to that value.";
            }

            c.OpenUndoStep();
            CadEditResult result = CadMLeaderStyleEditor.SetCurrent(c.Document, typed);
            return result.Succeeded ? null : result.Message;
        }

        /// <summary>CANNOSCALE: a scale of the drawing's list made the annotation scale of model space, one undo step.</summary>
        private static string CurrentAnnotationScale(CadCommandContext c, string typed)
        {
            string old = c.Database.Cannoscale;

            if (string.Equals(old, typed, StringComparison.Ordinal))
            {
                return null;
            }

            CadEditResult result = c.Apply(
                new[] { new CadEditRecord(CadEditRecordKind.SettingChanged, null, "CANNOSCALE", old, typed) },
                s => s.Database.Cannoscale = typed
            );
            return result.Succeeded ? null : string.Empty;
        }

        /// <summary>TILEMODE: 1 shows model space, 0 the first layout, as a tab clicked; the selection, which belongs to a space, cleared.</summary>
        private static void SetTileMode(CadCommandContext c, int value)
        {
            CadSpace space = c.Document.Spaces.FirstOrDefault(s => s.IsModel == (value == 1));

            if (space != null && space != c.Document.CurrentSpace)
            {
                c.Document.CurrentSpace = space;
                c.Document.Selection.Clear();
            }
        }

        private static DxfRasterVariables ImageVariables(DxfDatabase database) =>
            database.NamedObjectsDictionary?.Contains("ACAD_IMAGE_VARS") == true
                ? database.NamedObjectsDictionary.GetAt("ACAD_IMAGE_VARS") as DxfRasterVariables
                : null;

        /// <summary>
        /// IMAGEFRAME as the drawing keeps it: 0 for frames off, 2 for frames shown and not plotted (70 of 3, as AutoCAD 2021
        /// writes IMAGEFRAME 2), 1 otherwise, and where the drawing keeps no variables of images.
        /// </summary>
        internal static int ImageFrameOf(DxfDatabase database)
        {
            DxfRasterVariables variables = ImageVariables(database);
            DxfFrameSetting frame = variables == null ? DxfFrameSetting.ImageFrameAbove : variables.ImageFrame;
            return frame == DxfFrameSetting.ImageFrameOff ? 0
                : frame == DxfFrameSetting.ImageFrameOnNoPlot ? 2
                : 1;
        }

        private static int ImageFrameOf(CadCommandSession session) => ImageFrameOf(session.Document.Database);

        /// <summary>The setting of RASTERVARIABLES AutoCAD 2021 writes for an IMAGEFRAME: 0 off, 1 above, 2 shown and not plotted (3).</summary>
        private static DxfFrameSetting FrameSettingOf(int value) =>
            value == 0 ? DxfFrameSetting.ImageFrameOff
            : value == 2 ? DxfFrameSetting.ImageFrameOnNoPlot
            : DxfFrameSetting.ImageFrameAbove;

        /// <summary>
        /// A drawing with no objects, as one of R12, cannot keep IMAGEFRAME; any other is given its variables of images where it
        /// has none, as AutoCAD 2021 makes them when IMAGEFRAME is first changed.
        /// </summary>
        private static string ImageFrameRefused(CadCommandContext c, int value) =>
            c.Database.NamedObjectsDictionary == null
                ? "IMAGEFRAME is kept in the drawing's objects, which a drawing of R12 has none of."
                : null;

        /// <summary>The folder of the drawing's file with its separator, as AutoCAD's DWGPREFIX; the user's documents for a drawing not saved yet.</summary>
        private static string Prefix(CadDocument document)
        {
            string folder =
                document.HasFile && !string.IsNullOrEmpty(document.FileName)
                    ? Path.GetDirectoryName(Path.GetFullPath(document.FileName))
                    : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return folder.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? folder
                : folder + Path.DirectorySeparatorChar;
        }

        /// <summary>A time as AutoCAD's DATE gives it: the Julian day and the fraction of it, 2461355.60431713.</summary>
        private static string JulianDate(DateTime time) =>
            (time.ToOADate() + 2415018.5).ToString("0.00000000", CultureInfo.InvariantCulture);
    }
}
