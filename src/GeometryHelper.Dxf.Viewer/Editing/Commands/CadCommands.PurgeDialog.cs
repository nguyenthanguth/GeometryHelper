using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// PURGE (PU), AutoCAD's Purge dialog: the named objects nothing uses by kind, purged while the dialog stays open, each
    /// asked about first where Confirm says so and those a purge leaves unused purged after them where Purge nested says so,
    /// each purge one undo step; and the objects in use with the reasons they cannot be purged, Select Objects selecting what
    /// uses one. With no window, -PURGE's prompts.
    /// </summary>
    internal static partial class CadCommands
    {
        /// <summary>The kinds of the dialog's tree by -PURGE's keyword, each its name in the tree.</summary>
        private static readonly KeyValuePair<string, string>[] PurgeTree =
        {
            new KeyValuePair<string, string>("Blocks", "Blocks"),
            new KeyValuePair<string, string>("Dimstyles", "Dimension Styles"),
            new KeyValuePair<string, string>("Groups", "Groups"),
            new KeyValuePair<string, string>("LAyers", "Layers"),
            new KeyValuePair<string, string>("LTypes", "Linetypes"),
            new KeyValuePair<string, string>("MUltileaderstyles", "Multileader Styles"),
            new KeyValuePair<string, string>("Plotstyles", "Plot Styles"),
            new KeyValuePair<string, string>("Regapps", "Registered Applications"),
            new KeyValuePair<string, string>("SHapes", "Shapes"),
            new KeyValuePair<string, string>("Tablestyles", "Table Styles"),
            new KeyValuePair<string, string>("textSTyles", "Text Styles"),
            new KeyValuePair<string, string>("Visualstyles", "Visual Styles"),
        };

        /// <summary>The names AutoCAD never purges, by -PURGE's keyword.</summary>
        private static readonly Dictionary<string, string[]> PurgeKept = new Dictionary<string, string[]>(
            StringComparer.Ordinal
        )
        {
            ["LAyers"] = new[] { "0", "Defpoints" },
            ["LTypes"] = new[] { "ByLayer", "ByBlock", "Continuous" },
            ["textSTyles"] = new[] { "Standard" },
            ["Dimstyles"] = new[] { "Standard" },
            ["MUltileaderstyles"] = new[] { "Standard" },
            ["Tablestyles"] = new[] { "Standard" },
            ["Plotstyles"] = new[] { "Normal" },
        };

        /// <summary>
        /// PURGE: the window's Purge dialog, shown again after nothing (it purges with its own buttons); Select Objects of an
        /// object in use selects the objects of the space shown that use it and ends. With no window, -PURGE's prompts.
        /// </summary>
        private static IEnumerable<CadPrompt> Purge(CadCommandContext c)
        {
            var request = new CadPurgeRequest(c.Document);
            ReadPurge(c, request);
            request.Purging = items => PurgeItems(c, request, items);
            bool asked = c.Session.Request("PURGE", c.Document, request);
            request.Purging = null;

            if (!asked)
            {
                foreach (CadPrompt prompt in PurgeTyped(c))
                {
                    yield return prompt;
                }

                yield break;
            }

            if (request.Action == CadDialogAction.SelectObjects && request.Selected != null)
            {
                c.Document.Selection.Set(request.Selected.Objects);
                c.Write(request.Selected.Objects.Count.ToString(CultureInfo.InvariantCulture) + " found");
            }
        }

        /// <summary>The lists of the dialog read from the drawing as it stands.</summary>
        private static void ReadPurge(CadCommandContext c, CadPurgeRequest request)
        {
            Dictionary<string, PurgeUse> uses = PurgeUses(c);
            var purgeable = new List<CadPurgeCategory>();
            var inUse = new List<CadPurgeCategory>();

            foreach (KeyValuePair<string, string> kind in PurgeTree)
            {
                List<KeyValuePair<string, DxfDBObject>> unused = Unused(c, kind.Key, "*");
                purgeable.Add(
                    new CadPurgeCategory(
                        kind.Value,
                        unused.Select(u => new CadPurgeItem(kind.Value, kind.Key, u.Key, null, null)).ToList()
                    )
                );
                var free = new HashSet<string>(unused.Select(u => u.Key), StringComparer.OrdinalIgnoreCase);
                var used = new List<CadPurgeItem>();

                foreach (string name in AllNamed(c, kind.Key))
                {
                    if (!free.Contains(name))
                    {
                        uses.TryGetValue(kind.Key + "|" + name.ToUpperInvariant(), out PurgeUse use);
                        used.Add(
                            new CadPurgeItem(kind.Value, kind.Key, name, Reasons(c, kind.Key, name, use), use?.Objects)
                        );
                    }
                }

                used.Sort(
                    (a, b) =>
                        StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name) != 0
                            ? StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name)
                            : string.CompareOrdinal(a.Name, b.Name)
                );
                inUse.Add(new CadPurgeCategory(kind.Value, used));
            }

            request.Purgeable = purgeable;
            request.InUse = inUse;
            request.ZeroLength = FoundEntities(c, true).Count;
            request.EmptyText = FoundEntities(c, false).Count;
        }

        /// <summary>
        /// Purges the objects the dialog gives, one undo step: the geometry of no length and the empty texts first where the
        /// options say so; then each object asked about where Confirm says so (the window's question PURGEITEM, "You are about
        /// to purge layer TMP. What do you want to do?" as AutoCAD 2021's Confirm Purge puts it, Yes or No, null stopping),
        /// purged; then, with Purge nested, what that left unused, again until a pass purges nothing. The lists are
        /// read again. How many named objects were purged.
        /// </summary>
        private static int PurgeItems(CadCommandContext c, CadPurgeRequest request, IReadOnlyList<CadPurgeItem> items)
        {
            if (request.PurgeZeroLength)
            {
                PurgeEntities(c, true);
            }

            if (request.PurgeEmptyText)
            {
                PurgeEntities(c, false);
            }

            var before = new HashSet<string>(
                request.Purgeable.SelectMany(k => k.Items).Select(i => i.Keyword + "|" + i.Name),
                StringComparer.OrdinalIgnoreCase
            );
            var declined = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var purged = new List<KeyValuePair<string[], string>>();
            var pending = items
                .Select(i => new KeyValuePair<string[], string>(PurgeKinds.First(k => k[0] == i.Keyword), i.Name))
                .ToList();
            bool stopped = false;

            while (pending.Count > 0 && !stopped)
            {
                var chosen = new List<KeyValuePair<string[], string>>();

                foreach (KeyValuePair<string[], string> item in pending)
                {
                    if (request.ConfirmEach)
                    {
                        CadUiRequestEventArgs asked = c.Session.Ask(
                            "PURGEITEM",
                            c.Document,
                            "You are about to purge " + item.Key[1] + " " + item.Value + ". What do you want to do?"
                        );

                        if (asked.Handled && asked.Answer == null)
                        {
                            stopped = true;
                            break;
                        }

                        if (asked.Handled && !string.Equals(asked.Answer, "Yes", StringComparison.OrdinalIgnoreCase))
                        {
                            declined.Add(item.Key[0] + "|" + item.Value);
                            continue;
                        }
                    }

                    chosen.Add(item);
                }

                if (chosen.Count == 0 || !ErasePurged(c, chosen))
                {
                    break;
                }

                purged.AddRange(chosen);

                if (!request.PurgeNested)
                {
                    break;
                }

                // What the purge left unused, nested in what was purged: not what was unused before, nor what was kept.
                pending = new List<KeyValuePair<string[], string>>();

                foreach (string[] kind in PurgeKinds)
                {
                    foreach (KeyValuePair<string, DxfDBObject> item in Unused(c, kind[0], "*"))
                    {
                        string key = kind[0] + "|" + item.Key;

                        if (!before.Contains(key) && !declined.Contains(key))
                        {
                            pending.Add(new KeyValuePair<string[], string>(kind, item.Key));
                        }
                    }
                }
            }

            WritePurged(c, PurgeKinds, purged);
            c.CloseUndoStep();
            ReadPurge(c, request);
            return purged.Count;
        }

        /// <summary>The names of a kind of named object the drawing holds, in ordinal order, as the dialog's Find Non-Purgeable mode lists them.</summary>
        private static List<string> AllNamed(CadCommandContext c, string kind)
        {
            DxfDatabase db = c.Database;
            var names = new List<string>();

            void Dictionary(DxfDBDictionary dictionary)
            {
                if (dictionary != null)
                {
                    foreach (DxfDBDictionaryEntry entry in dictionary)
                    {
                        if (entry.Value != null)
                        {
                            names.Add(entry.Key);
                        }
                    }
                }
            }

            switch (kind)
            {
                case "Blocks":
                    names.AddRange(
                        db.BlockTable.Where(b => !b.IsLayout && !b.Name.StartsWith("*", StringComparison.Ordinal))
                            .Select(b => b.Name)
                    );
                    break;
                case "Dimstyles":
                    names.AddRange(db.DimStyleTable.Select(s => s.Name));
                    break;
                case "LAyers":
                    names.AddRange(db.LayerTable.Select(l => l.Name));
                    break;
                case "LTypes":
                    names.AddRange(db.LinetypeTable.Select(l => l.Name));
                    break;
                case "textSTyles":
                    names.AddRange(
                        db.TextStyleTable.Where(s => !s.IsShapeFile && !string.IsNullOrEmpty(s.Name))
                            .Select(s => s.Name)
                    );
                    break;
                case "MUltileaderstyles":
                    Dictionary(db.MLeaderStyleDictionary);
                    break;
                case "Tablestyles":
                    Dictionary(db.TableStyleDictionary);
                    break;
                case "Plotstyles":
                    Dictionary(db.PlotStyleNameDictionary);
                    break;
                case "Groups":
                    names.AddRange(c.Document.Groups().Select(g => g.Name));
                    break;
            }

            names.Sort(StringComparer.Ordinal);
            return names;
        }

        /// <summary>What uses a named object: how many objects of model space and of the layouts, the blocks that hold others, and the objects of the space shown.</summary>
        private sealed class PurgeUse
        {
            public int Model { get; set; }

            public int Layouts { get; set; }

            public SortedSet<string> Blocks { get; } = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

            public SortedSet<string> Layers { get; } = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

            public List<string> Objects { get; } = new List<string>();
        }

        /// <summary>
        /// What uses each named object, by its kind's keyword and its name in capitals ("LAyers|WALLS"): the layer, linetype,
        /// text style, dimension style, block, multileader style and table style of every entity of the spaces and blocks;
        /// the linetypes of the layers. Read in the order of the block table and of each block.
        /// </summary>
        private static Dictionary<string, PurgeUse> PurgeUses(CadCommandContext c)
        {
            var uses = new Dictionary<string, PurgeUse>(StringComparer.Ordinal);
            DxfBlockTableRecord shown = c.Space.Block;

            PurgeUse Use(string kind, string name)
            {
                string key = kind + "|" + (name ?? string.Empty).ToUpperInvariant();

                if (!uses.TryGetValue(key, out PurgeUse use))
                {
                    use = new PurgeUse();
                    uses.Add(key, use);
                }

                return use;
            }

            foreach (DxfLayerTableRecord layer in c.Database.LayerTable)
            {
                if (!string.IsNullOrEmpty(layer.LinetypeName))
                {
                    Use("LTypes", layer.LinetypeName).Layers.Add(layer.Name);
                }
            }

            foreach (DxfBlockTableRecord owner in c.Database.BlockTable)
            {
                if (owner.IsFromExternalReference)
                {
                    continue;
                }

                bool model = string.Equals(
                    owner.Name,
                    DxfBlockTableRecord.ModelSpace,
                    StringComparison.OrdinalIgnoreCase
                );

                foreach (DxfEntity entity in owner)
                {
                    if (entity.IsErased)
                    {
                        continue;
                    }

                    var names = new List<KeyValuePair<string, string>>
                    {
                        new KeyValuePair<string, string>("LAyers", entity.Layer),
                        new KeyValuePair<string, string>("LTypes", entity.Linetype),
                    };

                    switch (entity)
                    {
                        case DxfDBText text:
                            names.Add(new KeyValuePair<string, string>("textSTyles", text.TextStyleName));
                            break;
                        case DxfMText mtext:
                            names.Add(new KeyValuePair<string, string>("textSTyles", mtext.TextStyleName));
                            break;
                        case DxfDimension dimension:
                            names.Add(new KeyValuePair<string, string>("Dimstyles", dimension.DimensionStyleName));
                            break;
                        case DxfLeader leader:
                            names.Add(new KeyValuePair<string, string>("Dimstyles", leader.DimensionStyleName));
                            break;
                        case DxfBlockReference insert:
                            names.Add(new KeyValuePair<string, string>("Blocks", insert.Name));
                            break;
                        case DxfMLeader multileader:
                            names.Add(new KeyValuePair<string, string>("MUltileaderstyles", multileader.MLeaderStyle));
                            break;
                        case DxfTable table:
                            names.Add(new KeyValuePair<string, string>("Tablestyles", table.TableStyleName));
                            break;
                    }

                    foreach (KeyValuePair<string, string> pair in names)
                    {
                        if (string.IsNullOrEmpty(pair.Value))
                        {
                            continue;
                        }

                        PurgeUse use = Use(pair.Key, pair.Value);

                        if (model)
                        {
                            use.Model++;
                        }
                        else if (owner.IsLayout)
                        {
                            use.Layouts++;
                        }
                        else
                        {
                            use.Blocks.Add(owner.Name);
                        }

                        if (
                            ReferenceEquals(owner, shown)
                            || string.Equals(owner.Name, shown.Name, StringComparison.OrdinalIgnoreCase)
                        )
                        {
                            if (entity.Handle != null && !use.Objects.Contains(entity.Handle))
                            {
                                use.Objects.Add(entity.Handle);
                            }
                        }
                    }
                }
            }

            foreach (PurgeUse use in uses.Values)
            {
                use.Objects.Sort(StringComparer.OrdinalIgnoreCase);
            }

            return uses;
        }

        /// <summary>
        /// Why a named object in use cannot be purged, as AutoCAD's Possible reasons say it: kept always, current, from an
        /// external reference, used by objects of model space and the layouts, by layers, by blocks; a group, that it holds
        /// objects; "It is referenced by another named object." where none of these is found.
        /// </summary>
        private static IReadOnlyList<string> Reasons(CadCommandContext c, string kind, string name, PurgeUse use)
        {
            string what = PurgeKinds.First(k => k[0] == kind)[1];
            string label = char.ToUpperInvariant(what[0]) + what.Substring(1) + " " + name;
            var reasons = new List<string>();
            DxfDatabase db = c.Database;

            if (
                PurgeKept.TryGetValue(kind, out string[] kept)
                && kept.Any(k => string.Equals(k, name, StringComparison.OrdinalIgnoreCase))
            )
            {
                reasons.Add(label + " cannot be purged: AutoCAD keeps it in every drawing.");
            }

            string current =
                kind == "LAyers" ? db.Clayer
                : kind == "LTypes" ? db.Celtype
                : kind == "textSTyles" ? db.Textstyle
                : kind == "Dimstyles" ? db.Dimstyle
                : kind == "MUltileaderstyles" ? db.MLeaderstyle
                : kind == "Tablestyles" ? db.Tablestyle
                : null;

            if (current != null && string.Equals(current, name, StringComparison.OrdinalIgnoreCase))
            {
                reasons.Add(label + " is the current " + what + ".");
            }

            if (name.IndexOf('|') > 0)
            {
                reasons.Add(label + " comes from an external reference.");
            }

            if (use != null)
            {
                if (use.Model + use.Layouts > 0)
                {
                    reasons.Add(
                        "Used by "
                            + use.Model.ToString(CultureInfo.InvariantCulture)
                            + " object"
                            + (use.Model == 1 ? string.Empty : "s")
                            + " in model space and "
                            + use.Layouts.ToString(CultureInfo.InvariantCulture)
                            + " in layouts."
                    );
                }

                foreach (string layer in use.Layers)
                {
                    reasons.Add("Used by layer " + layer + ".");
                }

                foreach (string holder in use.Blocks)
                {
                    reasons.Add("Referenced by block " + holder + ".");
                }
            }

            if (kind == "Groups")
            {
                reasons.Add(label + " holds objects.");
            }

            if (reasons.Count == 0)
            {
                reasons.Add("It is referenced by another named object.");
            }

            return reasons;
        }
    }
}
