using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The hyperlinks of objects, in AutoCAD 2021's words: HYPERLINK asks for the objects and shows the Insert Hyperlink dialog,
    /// Edit Hyperlink for an object that has one, with Remove Link; -HYPERLINK asks "Enter an option [Remove/Insert] &lt;Insert&gt;:"
    /// and, for Insert, "Enter hyperlink insert option [Area/Object] &lt;Object&gt;:", the objects, "Overwrite existing hyperlinks?",
    /// the hyperlink, its named location and its description. Insert by Area draws a rectangle on the layer URLLAYER and links
    /// that. Each is one undo step; the links are the extended data of PE_URL as AutoCAD writes them
    /// (<see cref="DxfEntity.Hyperlinks"/>).
    /// </summary>
    internal static partial class CadCommands
    {
        private const string UrlLayer = "URLLAYER";

        private static void AddHyperlinksTo(CadCommandRegistry registry)
        {
            registry.Add(new CadCommand("HYPERLINK", Hyperlink, true));
            registry.Add(new CadCommand("-HYPERLINK", HyperlinkTyped));
        }

        /// <summary>HYPERLINK: the objects, then the dialog (Edit Hyperlink where one has a link); with no window, a pointer to -HYPERLINK.</summary>
        private static IEnumerable<CadPrompt> Hyperlink(CadCommandContext c)
        {
            foreach (CadPrompt prompt in c.SelectObjects())
            {
                yield return prompt;
            }

            if (c.Selected.Count == 0)
            {
                c.Write("Nothing selected.");
                yield break;
            }

            var request = new CadHyperlinkRequest(c.Document, c.Selected);

            if (!c.Session.Request("HYPERLINK", c.Document, request))
            {
                c.Write("HYPERLINK is shown by the window: -HYPERLINK asks its questions on the command line.");
                yield break;
            }

            if (request.Action != CadDialogAction.Ok)
            {
                yield break;
            }

            if (request.Remove)
            {
                int removed = RemoveHyperlinks(c, c.Selected, null);
                c.Write(removed == 0 ? "No hyperlinks deleted." : DeletedText(removed));
                yield break;
            }

            if (!request.TryBuild(out DxfHyperLink link, out string problem))
            {
                c.Write(problem);
                yield break;
            }

            AttachHyperlink(c, c.Selected, link, request.IsEdit);
        }

        /// <summary>-HYPERLINK: Remove or Insert, each in AutoCAD's questions.</summary>
        private static IEnumerable<CadPrompt> HyperlinkTyped(CadCommandContext c)
        {
            yield return CadPrompt.ForKeyword("Enter an option", new[] { "Remove", "Insert" }, "Insert");

            if (c.Input.Kind != CadInputKind.Keyword)
            {
                yield break;
            }

            IEnumerable<CadPrompt> rest = c.Input.Is("Remove") ? HyperlinkRemoveTyped(c) : HyperlinkInsertTyped(c);

            foreach (CadPrompt prompt in rest)
            {
                yield return prompt;
            }
        }

        /// <summary>
        /// -HYPERLINK Remove: the objects, then for one object with several hyperlinks "Enter number, hyperlink, or * for all:",
        /// else every hyperlink they have taken out; "3 hyperlinks deleted." says how many.
        /// </summary>
        private static IEnumerable<CadPrompt> HyperlinkRemoveTyped(CadCommandContext c)
        {
            foreach (CadPrompt prompt in c.SelectObjects())
            {
                yield return prompt;
            }

            if (c.Selected.Count == 0)
            {
                c.Write("Nothing selected.");
                yield break;
            }

            List<DxfEntity> linked = CadHyperlinks.WithLinks(c.Document, c.Selected);

            if (linked.Count == 0)
            {
                c.Write("No hyperlink to remove.");
                yield break;
            }

            string which = null;

            if (linked.Count == 1 && linked[0].Hyperlinks.Count > 1)
            {
                c.Write("Hyperlinks");
                int number = 1;

                foreach (DxfHyperLink each in linked[0].Hyperlinks)
                {
                    c.Write("  " + number.ToString(CultureInfo.InvariantCulture) + ". " + each);
                    number++;
                }

                yield return CadPrompt.ForString("Enter number, hyperlink, or * for all");

                if (c.Input.Kind != CadInputKind.Text)
                {
                    yield break;
                }

                which = c.Input.Text.Trim();
            }

            int removed = RemoveHyperlinks(c, c.Selected, which == "*" ? null : which);
            c.Write(removed == 0 ? "No hyperlinks deleted." : DeletedText(removed));
        }

        /// <summary>-HYPERLINK Insert: by Object (the objects selected) or by Area (a rectangle on URLLAYER), then the hyperlink's three questions.</summary>
        private static IEnumerable<CadPrompt> HyperlinkInsertTyped(CadCommandContext c)
        {
            yield return CadPrompt.ForKeyword("Enter hyperlink insert option", new[] { "Area", "Object" }, "Object");

            if (c.Input.Kind != CadInputKind.Keyword)
            {
                yield break;
            }

            bool area = c.Input.Is("Area");
            GeoPoint2 first = default(GeoPoint2);
            GeoPoint2 other = default(GeoPoint2);

            if (area)
            {
                if (UrlLayerRefusal(c) is string refusal)
                {
                    c.Write(refusal);
                    yield break;
                }

                yield return CadPrompt.ForPoint("First corner");

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                first = c.Input.Point;
                yield return CadPrompt.ForPoint("Other corner", first);

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                other = c.Input.Point;
            }
            else
            {
                foreach (CadPrompt prompt in c.SelectObjects())
                {
                    yield return prompt;
                }

                if (c.Selected.Count == 0)
                {
                    c.Write("Nothing selected.");
                    yield break;
                }
            }

            bool overwrite = true;

            if (!area && CadHyperlinks.WithLinks(c.Document, c.Selected).Count > 0)
            {
                yield return CadPrompt.ForKeyword("Overwrite existing hyperlinks?", new[] { "Yes", "No" }, "Yes");
                overwrite = !c.Input.Is("No");
            }

            yield return CadPrompt.ForString("Enter hyperlink", "current drawing");
            string address = c.Input.Kind == CadInputKind.Text ? c.Input.Text.Trim() : string.Empty;
            yield return CadPrompt.ForString("Enter named location", "none");
            string place = c.Input.Kind == CadInputKind.Text ? c.Input.Text.Trim() : string.Empty;
            yield return CadPrompt.ForString("Enter description", "none");
            string text = c.Input.Kind == CadInputKind.Text ? c.Input.Text.Trim() : string.Empty;
            DxfHyperLink link = CadHyperlinks.Make(address, place, text);

            // The current drawing with no place named is a link to nothing.
            if (link.Name.Length == 0 && link.SubLocation.Length == 0)
            {
                c.Write("No hyperlinks added.");
                yield break;
            }

            string tooLong = CadHyperlinks.TooLong(link);

            if (tooLong != null)
            {
                c.Write("*** Failed, string length exceeds 255 characters. ***");
                yield break;
            }

            if (area)
            {
                InsertHyperlinkArea(c, first, other, link);
            }
            else
            {
                AttachHyperlink(c, c.Selected, link, overwrite);
            }
        }

        /// <summary>Why Insert by Area cannot draw on URLLAYER, in AutoCAD's words; null where it can.</summary>
        private static string UrlLayerRefusal(CadCommandContext c)
        {
            if (!c.Database.LayerTable.Has(UrlLayer))
            {
                return null;
            }

            DxfLayerTableRecord layer = c.Database.LayerTable[UrlLayer];
            return layer.IsFrozen ? "URLLAYER is frozen.  Thaw URLLAYER to use Insert by Area."
                : layer.IsLocked ? "URLLAYER is locked. Unlock URLLAYER to use Insert by Area."
                : null;
        }

        /// <summary>Draws the rectangle of two corners on URLLAYER (made where the drawing has none), a closed polyline, linked.</summary>
        private static void InsertHyperlinkArea(
            CadCommandContext c,
            GeoPoint2 first,
            GeoPoint2 other,
            DxfHyperLink link
        )
        {
            CadUcs ucs = c.Session.Ucs;
            GeoPoint2[] corners = { first, new GeoPoint2(other.X, first.Y), other, new GeoPoint2(first.X, other.Y) };

            c.Apply(null, s =>
            {
                if (!s.Database.LayerTable.Has(UrlLayer))
                {
                    s.Database.LayerTable.Add(new DxfLayerTableRecord { Name = UrlLayer });
                }

                var rectangle = new DxfPolyline();

                for (int i = 0; i < 4; i++)
                {
                    rectangle.AddVertexAt(i, ucs.ToWorld(corners[i]), 0.0, 0.0, 0.0);
                }

                rectangle.Closed = true;
                Current(c, rectangle);
                rectangle.Layer = UrlLayer;
                s.Append(c.Space, rectangle);
                rectangle.Hyperlinks.Add(link);
            });
        }

        /// <summary>
        /// Puts a hyperlink on objects, one edit: it replaces the ones they have, or follows them where
        /// <paramref name="replace"/> is false.
        /// </summary>
        private static void AttachHyperlink(
            CadCommandContext c,
            IReadOnlyList<string> handles,
            DxfHyperLink link,
            bool replace
        )
        {
            var planned = new List<CadEditRecord>();

            foreach (string handle in handles)
            {
                planned.Add(
                    new CadEditRecord(CadEditRecordKind.PropertyChanged, handle, "Hyperlink", null, link.ToString())
                );
            }

            c.Apply(planned, s =>
            {
                foreach (string handle in handles)
                {
                    DxfEntity entity = s.Document.FindEntity(handle);

                    if (entity == null)
                    {
                        continue;
                    }

                    DxfHyperLinkCollection links = entity.Hyperlinks;

                    if (replace)
                    {
                        links.Clear();
                    }

                    links.Add(
                        new DxfHyperLink
                        {
                            Name = link.Name,
                            Description = link.Description,
                            SubLocation = link.SubLocation,
                        }
                    );
                }
            });
        }

        /// <summary>
        /// Takes hyperlinks off objects, one edit: every one of them, or, where <paramref name="which"/> is given, the one it
        /// numbers (from 1) or names, of an object's list; how many were taken out comes back.
        /// </summary>
        private static int RemoveHyperlinks(CadCommandContext c, IReadOnlyList<string> handles, string which)
        {
            int removed = 0;
            var planned = new List<CadEditRecord>();

            foreach (DxfEntity entity in CadHyperlinks.WithLinks(c.Document, handles))
            {
                planned.Add(new CadEditRecord(CadEditRecordKind.PropertyChanged, entity.Handle, "Hyperlink"));
            }

            if (planned.Count == 0)
            {
                return 0;
            }

            CadEditResult result = c.Apply(planned, s =>
            {
                foreach (CadEditRecord record in planned)
                {
                    DxfEntity entity = s.Document.FindEntity(record.Handle);
                    DxfHyperLinkCollection links = entity.Hyperlinks;

                    if (which == null)
                    {
                        removed += links.Count;
                        links.Clear();
                        continue;
                    }

                    int index = -1;

                    if (int.TryParse(which, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number))
                    {
                        index = number - 1;
                    }
                    else
                    {
                        for (int i = 0; i < links.Count && index < 0; i++)
                        {
                            if (
                                string.Equals(links[i].Name, which, StringComparison.OrdinalIgnoreCase)
                                || string.Equals(links[i].DisplayString, which, StringComparison.OrdinalIgnoreCase)
                            )
                            {
                                index = i;
                            }
                        }
                    }

                    if (index >= 0 && index < links.Count)
                    {
                        links.RemoveAt(index);
                        removed++;
                    }
                }
            });

            return result.Succeeded ? removed : 0;
        }

        private static string DeletedText(int count) =>
            count == 1 ? "1 hyperlink deleted." : count.ToString(CultureInfo.InvariantCulture) + " hyperlinks deleted.";
    }
}
