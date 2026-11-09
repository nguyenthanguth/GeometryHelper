using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// -PURGE, in AutoCAD's words: the named objects of a kind nothing uses, those whose names match a pattern, each asked
    /// about where the names are to be verified, then deleted in one undo step; or the lines, arcs and polylines of no length
    /// and the texts of no content. What AutoCAD keeps is never purged: layer 0 and Defpoints, the linetypes ByLayer, ByBlock
    /// and Continuous, the styles Standard and the current ones, the blocks of the spaces and layouts, the plot style Normal
    /// and every visual style, as <see cref="DxfDatabase.Purge"/> keeps them.
    /// </summary>
    internal static partial class CadCommands
    {
        private static readonly string[] PurgeOptions =
        {
            "Blocks", "Dimstyles", "LAyers", "LTypes", "MUltileaderstyles", "Plotstyles", "SHapes", "textSTyles", "Tablestyles",
            "Visualstyles", "Regapps", "Zero-length geometry", "Empty text objects", "All",
        };

        /// <summary>The named kinds -PURGE All purges, in the order AutoCAD's purges them, each its keyword, its name and its name of several.</summary>
        private static readonly string[][] PurgeKinds =
        {
            new[] { "Blocks", "block", "blocks" },
            new[] { "Dimstyles", "dimension style", "dimension styles" },
            new[] { "LAyers", "layer", "layers" },
            new[] { "LTypes", "linetype", "linetypes" },
            new[] { "MUltileaderstyles", "multileader style", "multileader styles" },
            new[] { "Plotstyles", "plot style", "plot styles" },
            new[] { "SHapes", "shape file", "shape files" },
            new[] { "textSTyles", "text style", "text styles" },
            new[] { "Tablestyles", "table style", "table styles" },
            new[] { "Visualstyles", "visual style", "visual styles" },
            new[] { "Regapps", "registered application", "registered applications" },
        };

        /// <summary>
        /// -PURGE: "Enter type of unused objects to purge [...]:", then for a named kind "Enter name(s) to purge &lt;*&gt;:" and
        /// "Verify each name to be purged? [Yes/No] &lt;Y&gt;:", each name then asked "Purge layer "X"? [Yes/No] &lt;N&gt;:".
        /// </summary>
        private static IEnumerable<CadPrompt> PurgeTyped(CadCommandContext c)
        {
            yield return CadPrompt.ForKeyword("Enter type of unused objects to purge", PurgeOptions);

            if (c.Input.Kind != CadInputKind.Keyword)
            {
                yield break;
            }

            string option = c.Input.Text;

            if (option == "Zero-length geometry" || option == "Empty text objects")
            {
                PurgeEntities(c, option == "Zero-length geometry");
                yield break;
            }

            yield return CadPrompt.ForString("Enter name(s) to purge", "*");
            string pattern = c.Input.Kind == CadInputKind.Text && c.Input.Text.Trim().Length > 0 ? c.Input.Text.Trim() : "*";
            yield return CadPrompt.ForKeyword("Verify each name to be purged?", new[] { "Yes", "No" }, "Yes");

            if (c.Input.Kind != CadInputKind.Keyword)
            {
                yield break;
            }

            bool verify = c.Input.Is("Yes");
            var kinds = new List<string[]>();

            foreach (string[] kind in PurgeKinds)
            {
                if (option == "All" || option == kind[0])
                {
                    kinds.Add(kind);
                }
            }

            // Each name to purge, its kind, its own and the object: found before any is asked about.
            var chosen = new List<KeyValuePair<string[], KeyValuePair<string, DxfDBObject>>>();

            foreach (string[] kind in kinds)
            {
                List<KeyValuePair<string, DxfDBObject>> unused = Unused(c, kind[0], pattern);

                foreach (KeyValuePair<string, DxfDBObject> item in unused)
                {
                    if (verify)
                    {
                        yield return CadPrompt.ForKeyword("Purge " + kind[1] + " \"" + item.Key + "\"?", new[] { "Yes", "No" }, "No");

                        if (c.Input.Kind != CadInputKind.Keyword)
                        {
                            yield break;
                        }

                        if (!c.Input.Is("Yes"))
                        {
                            continue;
                        }
                    }

                    chosen.Add(new KeyValuePair<string[], KeyValuePair<string, DxfDBObject>>(kind, item));
                }

                if (unused.Count == 0 && option != "All")
                {
                    c.Write("No unreferenced " + kind[2] + " found.");
                }
            }

            if (chosen.Count == 0)
            {
                if (option == "All")
                {
                    c.Write("No unreferenced objects found.");
                }

                yield break;
            }

            var records = new List<CadEditRecord>();

            foreach (KeyValuePair<string[], KeyValuePair<string, DxfDBObject>> pair in chosen)
            {
                records.Add(new CadEditRecord(CadEditRecordKind.TableChanged, null, "Purge " + pair.Key[1], pair.Value.Key, null));
            }

            CadEditResult result = c.Apply(records, s =>
            {
                foreach (KeyValuePair<string[], KeyValuePair<string, DxfDBObject>> pair in chosen)
                {
                    EraseNamed(s.Database, pair.Key[0], pair.Value.Key);
                }
            });

            if (!result.Succeeded)
            {
                yield break;
            }

            foreach (string[] kind in kinds)
            {
                int count = 0;

                foreach (KeyValuePair<string[], KeyValuePair<string, DxfDBObject>> pair in chosen)
                {
                    if (pair.Key == kind)
                    {
                        c.Write("Deleting " + kind[1] + " \"" + pair.Value.Key + "\".");
                        count++;
                    }
                }

                if (count > 0)
                {
                    c.Write(count.ToString(CultureInfo.InvariantCulture) + " " + (count == 1 ? kind[1] : kind[2]) + " deleted.");
                }
            }
        }

        /// <summary>The objects of a kind nothing uses whose names match a pattern, in ordinal order of their names, those AutoCAD keeps left out.</summary>
        private static List<KeyValuePair<string, DxfDBObject>> Unused(CadCommandContext c, string kind, string pattern)
        {
            DxfDatabase db = c.Database;
            var candidates = new List<KeyValuePair<string, DxfDBObject>>();

            void Dictionary(DxfDBDictionary dictionary, string current)
            {
                if (dictionary == null)
                {
                    return;
                }

                foreach (DxfDBDictionaryEntry entry in dictionary)
                {
                    if (entry.Value != null && !string.Equals(entry.Key, current, StringComparison.OrdinalIgnoreCase))
                    {
                        candidates.Add(new KeyValuePair<string, DxfDBObject>(entry.Key, entry.Value));
                    }
                }
            }

            switch (kind)
            {
                case "Blocks":
                    foreach (DxfBlockTableRecord block in db.BlockTable)
                    {
                        if (!block.IsLayout && !block.Name.StartsWith("*", StringComparison.Ordinal))
                        {
                            candidates.Add(new KeyValuePair<string, DxfDBObject>(block.Name, block));
                        }
                    }

                    break;
                case "Dimstyles":
                    foreach (DxfDimStyleTableRecord style in db.DimStyleTable)
                    {
                        if (!string.Equals(style.Name, db.Dimstyle, StringComparison.OrdinalIgnoreCase))
                        {
                            candidates.Add(new KeyValuePair<string, DxfDBObject>(style.Name, style));
                        }
                    }

                    break;
                case "LAyers":
                    foreach (DxfLayerTableRecord layer in db.LayerTable)
                    {
                        if (!string.Equals(layer.Name, db.Clayer, StringComparison.OrdinalIgnoreCase))
                        {
                            candidates.Add(new KeyValuePair<string, DxfDBObject>(layer.Name, layer));
                        }
                    }

                    break;
                case "LTypes":
                    foreach (DxfLinetypeTableRecord linetype in db.LinetypeTable)
                    {
                        if (!string.Equals(linetype.Name, db.Celtype, StringComparison.OrdinalIgnoreCase))
                        {
                            candidates.Add(new KeyValuePair<string, DxfDBObject>(linetype.Name, linetype));
                        }
                    }

                    break;
                case "textSTyles":
                    foreach (DxfTextStyleTableRecord style in db.TextStyleTable)
                    {
                        // A style of a file of shapes is purged as a shape file, by SHapes, which AutoCAD keeps while loaded.
                        if (!style.IsShapeFile && !string.IsNullOrEmpty(style.Name) && !string.Equals(style.Name, db.Textstyle, StringComparison.OrdinalIgnoreCase))
                        {
                            candidates.Add(new KeyValuePair<string, DxfDBObject>(style.Name, style));
                        }
                    }

                    break;
                case "MUltileaderstyles":
                    Dictionary(db.MLeaderStyleDictionary, db.MLeaderstyle);
                    break;
                case "Tablestyles":
                    Dictionary(db.TableStyleDictionary, db.Tablestyle);
                    break;
                case "Plotstyles":
                    Dictionary(db.PlotStyleNameDictionary, null);
                    break;
            }

            var unused = new List<KeyValuePair<string, DxfDBObject>>();

            if (candidates.Count == 0)
            {
                return unused;
            }

            var objects = new List<DxfDBObject>();

            foreach (KeyValuePair<string, DxfDBObject> candidate in candidates)
            {
                objects.Add(candidate.Value);
            }

            var free = new HashSet<DxfDBObject>(db.Purge(objects));

            foreach (KeyValuePair<string, DxfDBObject> candidate in candidates)
            {
                if (free.Contains(candidate.Value) && CadWildcard.IsMatch(candidate.Key, pattern, true))
                {
                    unused.Add(candidate);
                }
            }

            unused.Sort((a, b) =>
            {
                int byName = StringComparer.OrdinalIgnoreCase.Compare(a.Key, b.Key);
                return byName != 0 ? byName : string.CompareOrdinal(a.Value.Handle, b.Value.Handle);
            });
            return unused;
        }

        /// <summary>Erases a named object of a kind, found again by its name in the drawing as it stands.</summary>
        private static void EraseNamed(DxfDatabase db, string kind, string name)
        {
            switch (kind)
            {
                case "Blocks":
                    db.BlockTable[name].Erase();
                    break;
                case "Dimstyles":
                    db.DimStyleTable[name].Erase();
                    break;
                case "LAyers":
                    db.LayerTable[name].Erase();
                    break;
                case "LTypes":
                    db.LinetypeTable[name].Erase();
                    break;
                case "textSTyles":
                    db.TextStyleTable[name].Erase();
                    break;
                case "MUltileaderstyles":
                    db.MLeaderStyleDictionary.Remove(name);
                    break;
                case "Tablestyles":
                    db.TableStyleDictionary.Remove(name);
                    break;
                case "Plotstyles":
                    db.PlotStyleNameDictionary.Remove(name);
                    break;
            }
        }

        /// <summary>-PURGE Zero-length geometry and Empty text objects: the entities of every space and block of no length, or of no text, erased.</summary>
        private static void PurgeEntities(CadCommandContext c, bool geometry)
        {
            double tolerance = c.Session.Tolerance.EqualPoint;
            var found = new List<string>();

            foreach (DxfBlockTableRecord block in c.Database.BlockTable)
            {
                if (block.IsFromExternalReference)
                {
                    continue;
                }

                foreach (DxfEntity entity in block)
                {
                    if (entity.Handle != null && (geometry ? IsZeroLength(entity, tolerance) : IsEmptyText(entity)))
                    {
                        found.Add(entity.Handle);
                    }
                }
            }

            found.Sort(StringComparer.OrdinalIgnoreCase);
            string what = geometry ? "zero-length geometry object" : "empty text object";

            if (found.Count > 0)
            {
                var records = new List<CadEditRecord>();

                foreach (string handle in found)
                {
                    // An entity of a block is no entity of a space, which the gate asks about; it goes as a change of the tables.
                    records.Add(c.Document.FindEntity(handle) != null
                        ? new CadEditRecord(CadEditRecordKind.Erased, handle)
                        : new CadEditRecord(CadEditRecordKind.TableChanged, null, "Purge " + what, handle, null));
                }

                CadEditResult result = c.Apply(records, s =>
                {
                    foreach (string handle in found)
                    {
                        if (s.IsHandled(handle))
                        {
                            continue;
                        }

                        if (s.Database.TryGetObject(handle, out DxfDBObject value) && value is DxfEntity entity && !entity.IsErased)
                        {
                            entity.Erase();
                        }
                    }
                });

                if (!result.Succeeded)
                {
                    return;
                }
            }

            c.Write(found.Count.ToString(CultureInfo.InvariantCulture) + " " + what + (found.Count == 1 ? string.Empty : "s") + " deleted.");
        }

        private static bool IsZeroLength(DxfEntity entity, double tolerance)
        {
            switch (entity)
            {
                case DxfLine line:
                    return line.StartPoint.DistanceTo(line.EndPoint) <= tolerance;
                case DxfArc arc:
                    return arc.Radius <= tolerance || arc.Length <= tolerance;
                case DxfCircle circle:
                    return circle.Radius <= tolerance;
                case DxfPolyline polyline:
                    return polyline.Length <= tolerance;
                default:
                    return false;
            }
        }

        private static bool IsEmptyText(DxfEntity entity)
        {
            switch (entity)
            {
                case DxfAttributeDefinition _:
                    return false;
                case DxfDBText text:
                    return string.IsNullOrWhiteSpace(text.TextString);
                case DxfMText mtext:
                    return string.IsNullOrWhiteSpace(mtext.Text);
                default:
                    return false;
            }
        }
    }
}
