using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Cad
{
    internal static partial class CadCommands
    {
        /// <summary>
        /// ATTDISP: "Enter attribute visibility setting [Normal/ON/OFF] &lt;Normal&gt;:", as AutoCAD 2021's. Normal shows the
        /// attributes that are not invisible, ON shows all of them and OFF none (ATTMODE 1, 2 and 0); a setting changed says
        /// "Regenerating model." and is one undo step, the display of every space drawn again; the same setting changes nothing.
        /// </summary>
        private static IEnumerable<CadPrompt> AttributeDisplay(CadCommandContext c)
        {
            int now = c.Database.Attmode;
            yield return CadPrompt.ForKeyword(
                "Enter attribute visibility setting",
                new[] { "Normal", "ON", "OFF" },
                now == 0 ? "OFF"
                    : now == 2 ? "ON"
                    : "Normal"
            );

            int mode =
                c.Input.Is("ON") ? 2
                : c.Input.Is("OFF") ? 0
                : c.Input.Is("Normal") ? 1
                : now;

            if (mode == now)
            {
                yield break;
            }

            CadEditResult result = c.Apply(
                new[]
                {
                    new CadEditRecord(CadEditRecordKind.SettingChanged, null, "$ATTMODE", Values(now), Values(mode)),
                },
                s => s.Database.Attmode = mode
            );

            if (result.Succeeded)
            {
                c.Write("Regenerating model.");
            }
        }

        /// <summary>
        /// -ATTEDIT's "No" branch, global editing of attribute values, as AutoCAD 2021's: "Performing global editing of
        /// attribute values.", "Edit only attributes visible on screen? [Yes/No] &lt;Y&gt;:" (No says "Drawing must be regenerated
        /// afterwards." and takes every INSERT of the drawing's spaces, an invisible attribute too), the block, tag and value
        /// specifications, "Select Attributes:" for the visible ones, "n attributes selected.", then "Enter string to change:" and
        /// "Enter new string:". Every occurrence of the string, its case kept apart, is replaced in the value of each attribute
        /// that matches; an empty string to change puts the new one at the front of every value, an empty new string takes the
        /// old one out. Each INSERT changed is one edit of the command's step.
        /// </summary>
        private static IEnumerable<CadPrompt> AttributeEditGlobal(CadCommandContext c)
        {
            c.Write("Performing global editing of attribute values.");
            yield return CadPrompt.ForKeyword("Edit only attributes visible on screen?", new[] { "Yes", "No" }, "Y");
            bool visible = !c.Input.Is("No");

            if (!visible)
            {
                c.Write("Drawing must be regenerated afterwards.");
            }

            yield return CadPrompt.ForString("Enter block name specification", "*");
            Regex blocks = Wildcard(c.Input.Kind == CadInputKind.Text ? c.Input.Text : "*");
            yield return CadPrompt.ForString("Enter attribute tag specification", "*");
            Regex tags = Wildcard(c.Input.Kind == CadInputKind.Text ? c.Input.Text : "*");
            yield return CadPrompt.ForString("Enter attribute value specification", "*");
            Regex values = Wildcard(c.Input.Kind == CadInputKind.Text ? c.Input.Text : "*");
            List<KeyValuePair<string, string>> found;

            if (visible)
            {
                yield return CadPrompt.ForSelection("Select Attributes");
                found = MatchingAttributes(c, c.Input.Handles, blocks, tags, values, true, true);
            }
            else
            {
                found = MatchingAttributes(c, InsertsOfSpaces(c), blocks, tags, values, false);
            }

            c.Write(found.Count.ToString(CultureInfo.InvariantCulture) + " attributes selected.");

            if (found.Count == 0)
            {
                yield break;
            }

            yield return CadPrompt.ForString("Enter string to change");
            string from = c.Input.Kind == CadInputKind.Text ? c.Input.Text : string.Empty;
            yield return CadPrompt.ForString("Enter new string");
            string to = c.Input.Kind == CadInputKind.Text ? c.Input.Text : string.Empty;

            // The attributes of each INSERT together, the INSERTs in the order they were found.
            var inserts = new List<string>();

            foreach (KeyValuePair<string, string> pair in found)
            {
                if (!inserts.Contains(pair.Key))
                {
                    inserts.Add(pair.Key);
                }
            }

            foreach (string insert in inserts)
            {
                var named = new HashSet<string>(
                    found.Where(p => p.Key == insert).Select(p => p.Value),
                    StringComparer.OrdinalIgnoreCase
                );
                CadAttributeEditRequest request = Rows(c.Document, insert);
                List<CadAttributeRow> before = request.Rows.Select(r => r.Clone()).ToList();

                foreach (CadAttributeRow row in request.Rows)
                {
                    if (named.Contains(row.Handle))
                    {
                        row.Value = from.Length == 0 ? to + row.Value : row.Value.Replace(from, to);
                    }
                }

                WriteAttributes(c, request, before);
            }
        }

        /// <summary>
        /// The attributes of the INSERTs among the handles whose block, tag and value match the specifications, each as the
        /// handle of its INSERT and its own. Where <paramref name="picked"/> is true the attributes are those -ATTEDIT's
        /// "Select Attributes:" took: of an INSERT a click picked, the attribute clicked alone; of one a window took, those
        /// inside it, or those a crossing box touches; of one taken otherwise, all. Where <paramref name="shownOnly"/> is true
        /// those the display leaves out (invisible ones under ATTDISP Normal, all under Off) are not among them.
        /// </summary>
        private static List<KeyValuePair<string, string>> MatchingAttributes(
            CadCommandContext c,
            IEnumerable<string> handles,
            Regex blocks,
            Regex tags,
            Regex values,
            bool picked,
            bool shownOnly = false
        )
        {
            var found = new List<KeyValuePair<string, string>>();
            int mode = c.Database.Attmode;

            foreach (string handle in handles)
            {
                if (c.Document.FindEntity(handle) is DxfBlockReference insert && blocks.IsMatch(insert.Name))
                {
                    // An INSERT a click picked gives the attribute clicked alone, as AutoCAD's; one a window took, those inside the
                    // window (or touched by a crossing box); one another option took whole, all of them.
                    HashSet<string> clicked = null;

                    if (picked)
                    {
                        List<GeoPoint2> clicks = c
                            .Input.Clicks.Where(p => string.Equals(p.Key, handle, StringComparison.OrdinalIgnoreCase))
                            .Select(p => p.Value)
                            .ToList();
                        List<Tuple<DisplayBox, bool>> windows = c
                            .Input.Windows.Where(w =>
                                string.Equals(w.Item1, handle, StringComparison.OrdinalIgnoreCase)
                            )
                            .Select(w => Tuple.Create(w.Item2, w.Item3))
                            .ToList();
                        clicked =
                            clicks.Count > 0 ? ClickedAttributes(c, insert, clicks)
                            : windows.Count > 0 ? WindowedAttributes(c, insert, windows)
                            : null;
                    }

                    foreach (DxfAttributeReference attribute in insert.AttributeCollection)
                    {
                        if (
                            (clicked == null || clicked.Contains(attribute.Handle))
                            && (!shownOnly || mode == 2 || (mode == 1 && !attribute.Invisible))
                            && tags.IsMatch(attribute.Tag)
                            && values.IsMatch(attribute.TextString ?? string.Empty)
                        )
                        {
                            found.Add(new KeyValuePair<string, string>(handle, attribute.Handle));
                        }
                    }
                }
            }

            return found;
        }

        /// <summary>The INSERTs of the drawing's model space and layouts, in the order of the block table and of each space.</summary>
        private static List<string> InsertsOfSpaces(CadCommandContext c)
        {
            var handles = new List<string>();

            foreach (DxfBlockTableRecord owner in c.Database.BlockTable)
            {
                if (owner.IsFromExternalReference)
                {
                    continue;
                }

                foreach (DxfEntity entity in owner)
                {
                    if (
                        entity is DxfBlockReference insert
                        && !insert.IsErased
                        && c.Document.FindSpace(insert.Handle) != null
                    )
                    {
                        handles.Add(insert.Handle);
                    }
                }
            }

            return handles;
        }
    }
}
