using System;
using System.Collections.Generic;
using System.Linq;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// RENAME and -RENAME, as AutoCAD 2021 asks them: the kind of thing, its old name, its new name; the layer, block,
    /// linetype, styles, UCS, view and viewport configuration renamed with every reference to them, in one undo step.
    /// </summary>
    internal static partial class CadCommands
    {
        // The kinds -RENAME offers in AutoCAD 2021, as its prompt writes them, and the noun its questions use for each.
        private static readonly string[] RenameKinds =
        {
            "Block",
            "dEtailviewstyle",
            "Dimstyle",
            "LAyer",
            "LType",
            "Material",
            "multileadeRstyle",
            "seCtionviewstyle",
            "Style",
            "Tablestyle",
            "Ucs",
            "VIew",
            "VPort",
        };

        private static string RenameNoun(string kind)
        {
            switch (kind)
            {
                case "Block":
                    return "block";
                case "Dimstyle":
                    return "dimension style";
                case "LAyer":
                    return "layer";
                case "LType":
                    return "linetype";
                case "multileadeRstyle":
                    return "multileader style";
                case "Style":
                    return "text style";
                case "Tablestyle":
                    return "table style";
                case "Ucs":
                    return "ucs";
                case "VIew":
                    return "view";
                default:
                    return "viewport configuration";
            }
        }

        /// <summary>
        /// RENAME: the window's Rename dialog; with no window, or none designed yet for it, -RENAME on the command line with a
        /// line saying so.
        /// </summary>
        private static IEnumerable<CadPrompt> RenameDialog(CadCommandContext c)
        {
            if (c.Session.Request("RENAME"))
            {
                yield break;
            }

            c.Write("The Rename dialog is not designed yet: -RENAME asks on the command line.");

            foreach (CadPrompt prompt in RenameTyped(c))
            {
                yield return prompt;
            }
        }

        /// <summary>
        /// -RENAME: asks the kind (Block, Dimstyle, LAyer, LType, multileadeRstyle, Style, Tablestyle, Ucs, VIew, VPort; Material,
        /// dEtailviewstyle and seCtionviewstyle say they are not held), the old name, asked again where nothing has it or it has a
        /// wildcard in it, and the new name, asked again where it is refused. One undo step.
        /// </summary>
        private static IEnumerable<CadPrompt> RenameTyped(CadCommandContext c)
        {
            yield return CadPrompt.ForKeyword("Enter object type to rename", RenameKinds);

            if (c.Input.Kind != CadInputKind.Keyword)
            {
                yield break;
            }

            string kind = RenameKinds.FirstOrDefault(k => c.Input.Is(k));

            if (kind == "Material" || kind == "dEtailviewstyle" || kind == "seCtionviewstyle")
            {
                c.Write(
                    kind == "Material" ? "This drawing holds no materials."
                    : kind == "dEtailviewstyle" ? "This drawing holds no detail view styles."
                    : "This drawing holds no section view styles."
                );
                yield break;
            }

            if (kind == null)
            {
                yield break;
            }

            string noun = RenameNoun(kind);
            string old;

            while (true)
            {
                yield return CadPrompt.ForString("Enter old " + noun + " name");

                if (c.Input.Kind != CadInputKind.Text || c.Input.Text.Trim().Length == 0)
                {
                    yield break;
                }

                old = c.Input.Text.Trim();

                if (old.IndexOfAny(new[] { '*', '?' }) >= 0)
                {
                    c.Write("Invalid " + noun + " name.");
                    continue;
                }

                if (!RenameExists(c, kind, old))
                {
                    c.Write("Cannot find " + noun + " \"" + old + "\".");
                    continue;
                }

                break;
            }

            while (true)
            {
                yield return CadPrompt.ForString("Enter new " + noun + " name");

                if (c.Input.Kind != CadInputKind.Text || c.Input.Text.Trim().Length == 0)
                {
                    yield break;
                }

                string refused = Rename(c, kind, old, c.Input.Text.Trim());

                if (refused == null)
                {
                    yield break;
                }

                c.Write(refused);
            }
        }

        private static bool RenameExists(CadCommandContext c, string kind, string name)
        {
            DxfDatabase db = c.Database;

            switch (kind)
            {
                case "Block":
                    return CadBlocks
                        .Named(db)
                        .Any(b => string.Equals(b.Name, name, StringComparison.OrdinalIgnoreCase));
                case "Dimstyle":
                    return db.DimStyleTable.Has(name);
                case "LAyer":
                    return db.LayerTable.Has(name);
                case "LType":
                    return db.LinetypeTable.Has(name);
                case "multileadeRstyle":
                    return CadMLeaderStyleEditor.Find(db, name) != null;
                case "Style":
                    return db.TextStyleTable.Has(name);
                case "Tablestyle":
                    return CadTableStyleEditor.Find(db, name) != null;
                case "Ucs":
                    return c.Document.Ucs.Find(name) != null;
                case "VIew":
                    return FindView(c, name) != null;
                default:
                    return ConfigurationTiles(c, name).Count > 0;
            }
        }

        /// <summary>The records of a named viewport configuration, one per tile; none for *Active, which AutoCAD does not rename.</summary>
        private static List<DxfViewportTableRecord> ConfigurationTiles(CadCommandContext c, string name) =>
            name.StartsWith("*", StringComparison.Ordinal)
                ? new List<DxfViewportTableRecord>()
                : c
                    .Database.ViewportTable.Where(v => string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase))
                    .ToList();

        /// <summary>Renames what the kind names, joining the command's undo step; null where it was done, else the line saying why not.</summary>
        private static string Rename(CadCommandContext c, string kind, string old, string wanted)
        {
            CadEditResult result;
            c.OpenUndoStep();

            switch (kind)
            {
                case "Block":
                    result = CadBlocks.Rename(c.Document, old, wanted);
                    break;
                case "Dimstyle":
                    result = CadDimStyleEditor.Rename(c.Document, old, wanted);
                    break;
                case "LAyer":
                    result = CadLayerEditor.Rename(c.Document, old, wanted);
                    break;
                case "LType":
                    result = CadLinetypes.Rename(c.Document, old, wanted);
                    break;
                case "multileadeRstyle":
                    result = CadMLeaderStyleEditor.Rename(c.Document, old, wanted);
                    break;
                case "Style":
                    result = CadTextStyleEditor.Rename(c.Document, old, wanted);
                    break;
                case "Tablestyle":
                    result = CadTableStyleEditor.Rename(c.Document, old, wanted);
                    break;
                case "Ucs":
                    try
                    {
                        c.Document.Ucs.Rename(old, wanted);
                        return null;
                    }
                    catch (ArgumentException)
                    {
                        return "Invalid ucs name.";
                    }

                case "VIew":
                    if (!IsViewName(wanted))
                    {
                        return "Invalid view name.";
                    }

                    DxfViewTableRecord other = FindView(c, wanted);
                    string was = FindView(c, old).Name;

                    if (other != null && !string.Equals(other.Name, was, StringComparison.OrdinalIgnoreCase))
                    {
                        return "A view named " + other.Name + " already exists.";
                    }

                    result = c.Apply(
                        new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "Rename view", was, wanted) },
                        s => RenameView(c, was, wanted)
                    );
                    break;
                default:
                    result = RenameConfiguration(c, old, wanted);
                    break;
            }

            return result.Succeeded ? null : result.Message;
        }

        private static CadEditResult RenameConfiguration(CadCommandContext c, string old, string wanted)
        {
            if (wanted.Length > 255 || wanted.IndexOfAny("<>/\\\":;?*|,=`".ToCharArray()) >= 0)
            {
                return CadEditResult.Refuse("Invalid viewport configuration name.");
            }

            List<DxfViewportTableRecord> tiles = ConfigurationTiles(c, old);

            if (
                !string.Equals(wanted, old, StringComparison.OrdinalIgnoreCase)
                && c.Database.ViewportTable.Any(v => string.Equals(v.Name, wanted, StringComparison.OrdinalIgnoreCase))
            )
            {
                return CadEditResult.Refuse("Viewport configuration \"" + wanted + "\" already exists.");
            }

            string was = tiles[0].Name;
            return c.Apply(
                new[]
                {
                    new CadEditRecord(
                        CadEditRecordKind.TableChanged,
                        null,
                        "Rename viewport configuration",
                        was,
                        wanted
                    ),
                },
                s =>
                {
                    foreach (DxfViewportTableRecord tile in ConfigurationTiles(c, was))
                    {
                        tile.Name = wanted;
                    }
                }
            );
        }
    }
}
