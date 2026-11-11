using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The draw order, in AutoCAD's words: DRAWORDER puts the objects selected above or under others, or at the front or the
    /// back of their space; TEXTTOFRONT brings every text, dimension or leader of the drawing to the front. Both write the
    /// sort table AutoCAD writes (a SORTENTSTABLE in the extension dictionary of the space's block), which the display
    /// follows, and each run is one undo step.
    /// </summary>
    internal static partial class CadCommands
    {
        private static readonly string[] OrderOptions = { "Above objects", "Under objects", "Front", "Back" };

        /// <summary>DRAWORDER: "Enter object ordering option [Above objects/Under objects/Front/Back] &lt;Back&gt;:", then for Above and Under the objects to order against.</summary>
        private static IEnumerable<CadPrompt> DrawOrder(CadCommandContext c)
        {
            foreach (CadPrompt prompt in c.SelectObjects())
            {
                yield return prompt;
            }

            List<DxfEntity> moving = InSpace(c, c.Selected);

            if (moving.Count == 0)
            {
                yield break;
            }

            yield return CadPrompt.ForKeyword("Enter object ordering option", OrderOptions, "Back");

            if (c.Input.Kind != CadInputKind.Keyword)
            {
                yield break;
            }

            string option = c.Input.Text;
            DxfEntity target = null;

            if (option == "Above objects" || option == "Under objects")
            {
                yield return CadPrompt.ForSelection("Select reference objects");

                var picked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (DxfEntity entity in moving)
                {
                    picked.Add(entity.Handle);
                }

                var references = new List<string>();

                foreach (string handle in c.Input.Handles ?? Array.Empty<string>())
                {
                    if (!picked.Contains(handle))
                    {
                        references.Add(handle);
                    }
                }

                // Above the topmost of the objects given, or under the lowest, as AutoCAD orders against several.
                target = Extreme(c.Space.Block, InSpace(c, references), option == "Above objects");

                if (target == null)
                {
                    c.Write("No reference objects selected.");
                    yield break;
                }
            }

            c.Apply(new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "Draw order", null, option) }, s =>
            {
                DxfDrawOrderTable table = c.Space.Block.CreateDrawOrderTable();

                switch (option)
                {
                    case "Front":
                        table.MoveToTop(moving);
                        break;
                    case "Back":
                        table.MoveToBottom(moving);
                        break;
                    case "Above objects":
                        table.MoveAbove(moving, target);
                        break;
                    default:
                        table.MoveBelow(moving, target);
                        break;
                }
            });
        }

        /// <summary>TEXTTOFRONT: "Bring to front [Text/Dimensions/Leaders/All] &lt;All&gt;:", every one of the kind in every space brought to the front.</summary>
        private static IEnumerable<CadPrompt> TextToFront(CadCommandContext c)
        {
            yield return CadPrompt.ForKeyword(
                "Bring to front",
                new[] { "Text", "Dimensions", "Leaders", "All" },
                "All"
            );

            if (c.Input.Kind != CadInputKind.Keyword)
            {
                yield break;
            }

            string option = c.Input.Text;
            var spaces = new List<KeyValuePair<DxfBlockTableRecord, List<DxfEntity>>>();
            int count = 0;

            foreach (CadSpace space in c.Document.Spaces)
            {
                var found = new List<DxfEntity>();

                foreach (DxfEntity entity in space.Block)
                {
                    bool text = entity is DxfDBText || entity is DxfMText;
                    bool dimension = entity is DxfDimension;
                    bool leader = entity is DxfLeader || entity is DxfMLeader;

                    if (
                        (option == "Text" && text)
                        || (option == "Dimensions" && dimension)
                        || (option == "Leaders" && leader)
                        || (option == "All" && (text || dimension || leader))
                    )
                    {
                        found.Add(entity);
                    }
                }

                if (found.Count > 0)
                {
                    spaces.Add(new KeyValuePair<DxfBlockTableRecord, List<DxfEntity>>(space.Block, found));
                    count += found.Count;
                }
            }

            if (count > 0)
            {
                CadEditResult result = c.Apply(
                    new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "Draw order", null, "Front") },
                    s =>
                    {
                        foreach (KeyValuePair<DxfBlockTableRecord, List<DxfEntity>> pair in spaces)
                        {
                            pair.Key.CreateDrawOrderTable().MoveToTop(pair.Value);
                        }
                    }
                );

                if (!result.Succeeded)
                {
                    yield break;
                }
            }

            c.Write(count.ToString(CultureInfo.InvariantCulture) + " object(s) brought to front.");
        }

        /// <summary>The entities of handles that are in the current space, in the order of the handles.</summary>
        private static List<DxfEntity> InSpace(CadCommandContext c, IReadOnlyList<string> handles)
        {
            var found = new List<DxfEntity>();

            foreach (string handle in handles)
            {
                DxfEntity entity = c.Document.FindEntity(handle);

                if (entity != null && c.Document.FindSpace(handle) == c.Space)
                {
                    found.Add(entity);
                }
            }

            return found;
        }

        /// <summary>The topmost or the lowest in the draw order of a block of some of its entities; null for none.</summary>
        private static DxfEntity Extreme(DxfBlockTableRecord block, List<DxfEntity> among, bool top)
        {
            if (among.Count == 0)
            {
                return null;
            }

            var wanted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (DxfEntity entity in among)
            {
                wanted.Add(entity.Handle);
            }

            DxfDrawOrderTable table = block.DrawOrderTable;
            IEnumerable<DxfEntity> order = table != null ? table.GetFullDrawOrder() : (IEnumerable<DxfEntity>)block;
            DxfEntity found = null;

            foreach (DxfEntity entity in order)
            {
                if (wanted.Contains(entity.Handle))
                {
                    found = entity;

                    if (!top)
                    {
                        break;
                    }
                }
            }

            return found;
        }
    }
}
