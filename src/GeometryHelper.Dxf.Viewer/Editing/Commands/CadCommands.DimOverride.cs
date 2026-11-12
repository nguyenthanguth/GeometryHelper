using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// DIMOVERRIDE (DOV), as AutoCAD 2021's (its prompts and answers probed in its console): "Enter dimension variable name to
    /// override or [Clear overrides]:", then "Enter new value for dimension variable &lt;current&gt;:" for each name, until Enter,
    /// then "Select objects:". The variables are written as overrides of the dimension style on the dimensions, leaders and
    /// feature control frames selected, each drawn anew; Clear overrides takes every override of those selected away.
    /// </summary>
    internal static partial class CadCommands
    {
        // The variables AutoCAD 2021's DIMOVERRIDE answers 'Variable "..." not found.' to: those that came after R2004 (it knows
        // DIMTXTDIRECTION and DIMADEC, but not the ones below), probed one by one.
        private static readonly string[] NotOverridable =
        {
            "Dimfxlen",
            "DimfxlenOn",
            "Dimjogang",
            "Dimtfill",
            "Dimtfillclr",
            "Dimarcsym",
            "Dimltype",
            "Dimltex1",
            "Dimltex2",
        };

        // The whole numbers a variable takes, from AutoCAD's own limits: "Requires an integer between 0 and 4." for DIMTAD 7.
        private static readonly Dictionary<string, int[]> WholeLimits = new Dictionary<string, int[]>(
            StringComparer.Ordinal
        )
        {
            ["Dimdec"] = new[] { 0, 8 },
            ["Dimtdec"] = new[] { 0, 8 },
            ["Dimaltd"] = new[] { 0, 8 },
            ["Dimalttd"] = new[] { 0, 8 },
            ["Dimadec"] = new[] { -1, 8 },
            ["Dimtad"] = new[] { 0, 4 },
            ["Dimzin"] = new[] { 0, 15 },
            ["Dimazin"] = new[] { 0, 3 },
            ["Dimaltz"] = new[] { 0, 15 },
            ["Dimalttz"] = new[] { 0, 15 },
            ["Dimtzin"] = new[] { 0, 15 },
            ["Dimaltu"] = new[] { 1, 8 },
            ["Dimaunit"] = new[] { 0, 4 },
            ["Dimfrac"] = new[] { 0, 2 },
            ["Dimlunit"] = new[] { 1, 6 },
            ["Dimtmove"] = new[] { 0, 2 },
            ["Dimjust"] = new[] { 0, 4 },
            ["Dimtolj"] = new[] { 0, 2 },
            ["Dimatfit"] = new[] { 0, 3 },
        };

        // The sizes AutoCAD answers "Value must be positive." to when they are below nought (probed for DIMASZ).
        private static readonly string[] SizesAboveNought =
        {
            "Dimscale",
            "Dimasz",
            "Dimexo",
            "Dimexe",
            "Dimdle",
            "Dimdli",
            "Dimtxt",
            "Dimtsz",
        };

        private static IEnumerable<CadPrompt> DimOverride(CadCommandContext c)
        {
            var wanted = new List<KeyValuePair<DxfDimVariable, Func<DxfDatabase, object>>>();
            DxfDimStyleTableRecord current = c.Database.GetDimstyleData();
            bool clear = false;

            while (true)
            {
                yield return wanted.Count == 0
                    ? CadPrompt.ForKeywordOrText(
                        "Enter dimension variable name to override or",
                        new[] { "Clear overrides" },
                        null
                    )
                    : CadPrompt.ForString("Enter dimension variable name to override");

                if (c.Input.Is("Clear overrides"))
                {
                    clear = true;
                    break;
                }

                if (c.Input.Kind != CadInputKind.Text || c.Input.Text.Trim().Length == 0)
                {
                    break;
                }

                string typed = c.Input.Text.Trim();
                DxfDimVariable variable = OverridableVariable(typed);

                if (variable == null)
                {
                    c.Write("Variable \"" + typed + "\" not found.");
                    continue;
                }

                Func<DxfDatabase, object> make = null;

                foreach (CadPrompt prompt in DimValuePrompt(c, variable, current.Get(variable), m => make = m))
                {
                    yield return prompt;
                }

                if (make != null)
                {
                    wanted.Add(new KeyValuePair<DxfDimVariable, Func<DxfDatabase, object>>(variable, make));
                }
            }

            foreach (CadPrompt prompt in c.SelectObjects())
            {
                yield return prompt;
            }

            if (c.Input.Kind != CadInputKind.Selection)
            {
                yield break;
            }

            if (clear)
            {
                ChangeStyled(c, "Clear", (entity, database) => entity.SetFromStyle());
                yield break;
            }

            if (wanted.Count == 0)
            {
                yield break;
            }

            // One variable at a time in the order of the groups, as AutoCAD 2021 writes the list of overrides.
            wanted.Sort(
                (a, b) =>
                {
                    int byCode = a.Key.Code.CompareTo(b.Key.Code);
                    return byCode != 0 ? byCode : string.CompareOrdinal(a.Key.Name, b.Key.Name);
                }
            );
            ChangeStyled(c, string.Join(",", wanted.Select(w => w.Key.Name.ToUpperInvariant())), (entity, database) =>
            {
                foreach (KeyValuePair<DxfDimVariable, Func<DxfDatabase, object>> pair in wanted)
                {
                    Overridden(entity, pair.Key, pair.Value(database));
                }

                return true;
            });
        }

        /// <summary>The variable a typed name names, letters of either case the same; null for a name DIMOVERRIDE does not know.</summary>
        private static DxfDimVariable OverridableVariable(string typed)
        {
            foreach (DxfDimVariable variable in DxfDimVariable.All)
            {
                if (
                    string.Equals(variable.Name, typed, StringComparison.OrdinalIgnoreCase)
                    && Array.IndexOf(NotOverridable, variable.Name) < 0
                )
                {
                    return variable;
                }
            }

            return null;
        }

        /// <summary>
        /// Asks the new value of a variable, in the words AutoCAD 2021 uses for what it holds, again after an answer it
        /// refuses, and gives <paramref name="made"/> the way to make the value (a block is found, or made, in the drawing the
        /// edit is made in). Enter takes the value shown.
        /// </summary>
        private static IEnumerable<CadPrompt> DimValuePrompt(
            CadCommandContext c,
            DxfDimVariable variable,
            object shown,
            Action<Func<DxfDatabase, object>> made
        )
        {
            const string ask = "Enter new value for dimension variable";

            while (true)
            {
                string refusal = null;

                switch (variable.Kind)
                {
                    case DxfDimKind.Real:
                        yield return CadPrompt.ForDistance(ask, null, null, Length((double)shown));

                        if (c.Input.Kind == CadInputKind.Number)
                        {
                            double value = c.Input.Number;

                            if (value < 0.0 && Array.IndexOf(SizesAboveNought, variable.Name) >= 0)
                            {
                                refusal = "Value must be positive.";
                            }
                            else
                            {
                                made(d => value);
                            }
                        }
                        else
                        {
                            made(d => shown);
                        }

                        break;

                    case DxfDimKind.Angle:
                        yield return CadPrompt
                            .ForAngle(ask, null, null, Number((double)shown * 180.0 / Math.PI))
                            .Plain();

                        if (c.Input.Kind == CadInputKind.Number)
                        {
                            double angle = c.Input.Number;
                            made(d => angle);
                        }
                        else
                        {
                            made(d => shown);
                        }

                        break;

                    case DxfDimKind.Whole:
                        yield return CadPrompt
                            .ForInteger(ask, null, ((int)shown).ToString(CultureInfo.InvariantCulture))
                            .Refusing("Requires an integer value.");

                        if (c.Input.Kind == CadInputKind.Number)
                        {
                            int value = (int)c.Input.Number;

                            if (
                                WholeLimits.TryGetValue(variable.Name, out int[] limits)
                                && (value < limits[0] || value > limits[1])
                            )
                            {
                                refusal =
                                    "Requires an integer between "
                                    + limits[0].ToString(CultureInfo.InvariantCulture)
                                    + " and "
                                    + limits[1].ToString(CultureInfo.InvariantCulture)
                                    + ".";
                            }
                            else
                            {
                                made(d => value);
                            }
                        }
                        else
                        {
                            made(d => shown);
                        }

                        break;

                    case DxfDimKind.Flag:
                        yield return CadPrompt.ForString(ask, (bool)shown ? "On" : "Off");

                        if (c.Input.Kind == CadInputKind.Text)
                        {
                            string text = c.Input.Text.Trim();
                            bool? on =
                                string.Equals(text, "On", StringComparison.OrdinalIgnoreCase) || text == "1" ? true
                                : string.Equals(text, "Off", StringComparison.OrdinalIgnoreCase) || text == "0" ? false
                                : (bool?)null;

                            if (on.HasValue)
                            {
                                made(d => on.Value);
                            }
                            else
                            {
                                refusal = "Invalid option keyword.";
                            }
                        }
                        else
                        {
                            made(d => shown);
                        }

                        break;

                    case DxfDimKind.Colour:
                        int index = ((DxfColor)shown).ColorIndex;
                        yield return CadPrompt.ForString(
                            ask,
                            index == 0 ? "BYBLOCK"
                                : index == 256 ? "BYLAYER"
                                : index.ToString(CultureInfo.InvariantCulture)
                        );

                        if (c.Input.Kind == CadInputKind.Text)
                        {
                            string error = CadPropertyEditor.ParseColor(c.Input.Text.Trim(), out DxfColor color);

                            if (error == null && color != null && !color.IsByColor)
                            {
                                made(d => color);
                            }
                            else
                            {
                                refusal = "Requires an integer value or option keyword.";
                            }
                        }
                        else
                        {
                            made(d => shown);
                        }

                        break;

                    case DxfDimKind.Weight:
                        yield return CadPrompt
                            .ForInteger(ask, null, ((int)(DxfLineWeight)shown).ToString(CultureInfo.InvariantCulture))
                            .Refusing("Requires an integer value.");

                        if (c.Input.Kind == CadInputKind.Number)
                        {
                            int value = (int)c.Input.Number;

                            if (Enum.IsDefined(typeof(DxfLineWeight), value))
                            {
                                DxfLineWeight weight = (DxfLineWeight)value;
                                made(d => weight);
                            }
                            else
                            {
                                refusal = "Invalid lineweight value.";
                            }
                        }
                        else
                        {
                            made(d => shown);
                        }

                        break;

                    case DxfDimKind.Letter:
                        yield return CadPrompt.ForString(ask, ((char)shown).ToString());

                        if (c.Input.Kind == CadInputKind.Text && c.Input.Text.Length > 0)
                        {
                            char letter = c.Input.Text[0];
                            made(d => letter);
                        }
                        else
                        {
                            made(d => shown);
                        }

                        break;

                    case DxfDimKind.Text:
                        yield return CadPrompt.ForString(ask, (string)shown);

                        if (c.Input.Kind == CadInputKind.Text)
                        {
                            // A full stop alone takes the text away, as AutoCAD's DIMPOST does.
                            string text = c.Input.Text == "." ? string.Empty : c.Input.Text;
                            made(d => text);
                        }
                        else
                        {
                            made(d => shown);
                        }

                        break;

                    case DxfDimKind.Block:
                        yield return CadPrompt.ForString(ask, CadArrowheads.ListedName(shown as DxfBlockTableRecord));

                        if (c.Input.Kind == CadInputKind.Text)
                        {
                            string name = ArrowheadName(c.Database, c.Input.Text.Trim());

                            if (name == null)
                            {
                                refusal = "Block \"" + c.Input.Text.Trim() + "\" not found.";
                            }
                            else
                            {
                                made(d => CadArrowheads.Ensure(d, name));
                            }
                        }
                        else
                        {
                            made(d => shown);
                        }

                        break;

                    default:
                        yield return CadPrompt.ForString(ask, (shown as DxfSymbolTableRecord)?.Name ?? string.Empty);

                        if (c.Input.Kind == CadInputKind.Text)
                        {
                            string name = c.Input.Text.Trim();

                            if (c.Database.TextStyleTable.Has(name))
                            {
                                made(d => d.TextStyleTable[name]);
                            }
                            else
                            {
                                refusal = "Text style \"" + name + "\" not found.";
                            }
                        }
                        else
                        {
                            made(d => shown);
                        }

                        break;
                }

                if (refusal == null)
                {
                    yield break;
                }

                c.Write(refusal);
            }
        }

        /// <summary>
        /// The name of the arrowhead a typed answer names, as <see cref="CadArrowheads.Names"/> lists it: AutoCAD's name of the
        /// block (_DOT, ARCHTICK), the name of the list (Dot, Architectural tick), a full stop for none, or the name of a block
        /// the drawing has; null where it names none.
        /// </summary>
        private static string ArrowheadName(DxfDatabase database, string typed)
        {
            if (typed == "." || typed.Length == 0)
            {
                return CadArrowheads.ClosedFilled;
            }

            string bare = typed.TrimStart('_').Replace(" ", string.Empty);

            foreach (string name in CadArrowheads.Names)
            {
                string block = CadArrowheads.BlockOf(name);

                if (
                    string.Equals(name.Replace(" ", string.Empty), bare, StringComparison.OrdinalIgnoreCase)
                    || (
                        block.Length > 0
                        && string.Equals(block.TrimStart('_'), bare, StringComparison.OrdinalIgnoreCase)
                    )
                )
                {
                    return name;
                }
            }

            return database.BlockTable.Has(typed) ? typed : null;
        }

        /// <summary>Sets a variable as an override on a dimension, a leader or a feature control frame, as its own SetDimstyleData writes one.</summary>
        private static void Overridden(DxfEntity entity, DxfDimVariable variable, object value)
        {
            switch (entity)
            {
                case DxfDimension dimension:
                {
                    DxfDimStyleTableRecord data = dimension.GetDimstyleData();
                    data.Keep(variable, value);
                    dimension.SetDimstyleData(data);
                    break;
                }

                case DxfLeader leader:
                {
                    DxfDimStyleTableRecord data = leader.GetDimstyleData();
                    data.Keep(variable, value);
                    leader.SetDimstyleData(data);
                    break;
                }

                case DxfTolerance frame:
                {
                    DxfDimStyleTableRecord data = frame.GetDimstyleData();
                    data.Keep(variable, value);
                    frame.SetDimstyleData(data);
                    break;
                }
            }
        }

        /// <summary>
        /// Changes the dimension style overrides of the objects selected as one edit: the dimensions, leaders and feature control
        /// frames among them, those whose owner refuses left out with the owner's words, each dimension drawn anew.
        /// </summary>
        private static void ChangeStyled(CadCommandContext c, string value, Func<DxfEntity, DxfDatabase, bool> change)
        {
            const string property = "Dimension style overrides";
            var targets = new List<DxfEntity>();

            foreach (string handle in c.Input.Handles)
            {
                DxfEntity entity = c.Document.FindEntity(handle);

                if (!(entity is DxfDimension || entity is DxfLeader || entity is DxfTolerance))
                {
                    continue;
                }

                var record = new CadEditRecord(CadEditRecordKind.PropertyChanged, handle, property);

                if (!c.Document.EditGate.Allows(c.Document, entity, record, out string reason))
                {
                    c.Write(reason ?? "The " + CadDocument.KindName(entity) + " " + handle + " may not be changed.");
                    continue;
                }

                targets.Add(entity);
            }

            if (targets.Count == 0)
            {
                return;
            }

            var planned = targets
                .Select(t => new CadEditRecord(CadEditRecordKind.PropertyChanged, t.Handle, property, null, value))
                .ToList();
            string refused = null;
            CadEditResult result = c.Apply(planned, s =>
            {
                foreach (CadEditRecord record in planned)
                {
                    if (s.IsHandled(record.Handle))
                    {
                        continue;
                    }

                    DxfEntity entity = s.Document.FindEntity(record.Handle);

                    if (entity is DxfDimension dimension)
                    {
                        OwnPicture(s.Database, dimension);
                    }

                    try
                    {
                        change(entity, s.Database);
                    }
                    catch (Exception e) when (e is ArgumentException || e is InvalidOperationException)
                    {
                        refused = e.Message;
                        throw;
                    }

                    if (entity is DxfDimension changed)
                    {
                        Redraw(changed);
                    }
                }
            });

            if (!result.Succeeded)
            {
                c.Write(refused ?? result.Message);
            }
        }
    }
}
