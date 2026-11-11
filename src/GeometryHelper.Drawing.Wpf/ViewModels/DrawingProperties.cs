using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Dxf.Viewer;

namespace GeometryHelper.Drawing.Wpf
{
    /// <summary>
    /// What Properties shows of a model drawing (the design's B1, B2 and B3): the drawing itself with nothing selected; a view
    /// with its own groups; a dimension, a mark, a projected line or a part of the sheet with the banner of its owner and its
    /// link to the model after the presenter's own rows. Every row it adds is read-only: the drawing is changed through its
    /// dialogs, and the owner takes what may be edited by hand.
    /// </summary>
    internal static class DrawingProperties
    {
        /// <summary>The banner of a dimension, as the design words it.</summary>
        public const string DimensionBanner =
            "Owned by drawing {0}. You can move it, edit its text and points. Its value follows the model.";

        /// <summary>The banner of a view.</summary>
        public const string ViewBanner =
            "Part of drawing {0}. The lines of a view come from the model: change the view, not the lines.";

        /// <summary>The banner of a mark.</summary>
        public const string MarkBanner = "Owned by drawing {0}. You can move it; its text follows the model.";

        /// <summary>The banner of a projected line or a section's hatch.</summary>
        public const string LineBanner =
            "Part of drawing {0}. This line belongs to the model: it cannot be moved or erased here; change the view instead.";

        /// <summary>The banner of the frame, the title block and the part list.</summary>
        public const string SheetBanner =
            "Part of the sheet of drawing {0}, filled from the model and the settings: change them in Drawing settings.";

        public static CadSelectionInfo Describe(
            ModelDrawing entry,
            CadDocument document,
            IReadOnlyList<string> handles,
            CadSelectionInfo shown,
            out string banner
        )
        {
            banner = null;

            if (handles == null || handles.Count == 0)
            {
                return new CadSelectionInfo("Drawing " + entry.Number, Nothing(entry));
            }

            var elements = new Dictionary<string, DrawingElement>(StringComparer.Ordinal);

            foreach (DrawingElement element in entry.Drawing.Elements)
            {
                elements[element.Id] = element;
            }

            // What each selected entity draws: its element, or the object of the model a projected line is of.
            string id = null;
            string viewId = null;
            bool one = true;
            bool any = false;

            foreach (string handle in handles)
            {
                DxfEntity entity = document.FindEntity(handle);

                if (entity == null || !DrawingEntityTags.TryRead(entity, out string entityId, out string entityView))
                {
                    one = false;
                    continue;
                }

                any = true;

                if (id == null)
                {
                    id = entityId;
                    viewId = entityView;
                }
                else if (!string.Equals(id, entityId, StringComparison.Ordinal))
                {
                    one = false;
                }
            }

            if (!any)
            {
                return shown;
            }

            string number = entry.Number;

            if (!one)
            {
                banner = "Owned by drawing " + number + ": some of the selection comes from the model.";
                return shown;
            }

            if (!elements.TryGetValue(id, out DrawingElement owned))
            {
                // A line or a hatch of a view: tagged with the object of the model it draws.
                banner = string.Format(CultureInfo.InvariantCulture, LineBanner, number);
                var rows = new List<CadPropertyRow> { Row("Object", ObjectText(entry, new[] { id })) };

                if (viewId != null && elements.TryGetValue(viewId, out DrawingElement inView))
                {
                    rows.Add(Row("View", Value(inView, "Name") ?? viewId));
                }

                return With(shown, new CadPropertyGroup("Model link", rows));
            }

            switch (owned.Kind)
            {
                case "View":
                    banner = string.Format(CultureInfo.InvariantCulture, ViewBanner, number);
                    return new CadSelectionInfo(
                        "View (" + handles.Count.ToString(CultureInfo.InvariantCulture) + ")",
                        View(entry, owned)
                    );

                case "Dimension":
                    banner = string.Format(CultureInfo.InvariantCulture, DimensionBanner, number);
                    return With(
                        shown,
                        new CadPropertyGroup(
                            "Model link",
                            new[]
                            {
                                Row("Object", ObjectText(entry, owned.Links.ObjectIds)),
                                Row("Measures", Measures(owned)),
                                Row("Value", Number(Value(owned, "Value"))),
                                Row("Edited", Edited(entry, owned.Id)),
                            }
                        )
                    );

                case "Mark":
                    banner = string.Format(CultureInfo.InvariantCulture, MarkBanner, number);
                    return With(
                        shown,
                        new CadPropertyGroup(
                            "Model link",
                            new[]
                            {
                                Row("Object", ObjectText(entry, owned.Links.ObjectIds)),
                                Row("Text", Value(owned, "Text") ?? string.Empty),
                                Row("Edited", Edited(entry, owned.Id)),
                            }
                        )
                    );

                default:
                    banner = string.Format(CultureInfo.InvariantCulture, SheetBanner, number);
                    return shown;
            }
        }

        /// <summary>The drawing itself: Drawing, Sheet, Title block and Source.</summary>
        private static IReadOnlyList<CadPropertyGroup> Nothing(ModelDrawing entry)
        {
            Drawing drawing = entry.Drawing;
            DrawingSettings settings = drawing.Settings;
            TitleBlock title = entry.TitleBlock;
            return new[]
            {
                new CadPropertyGroup(
                    "Drawing",
                    new[]
                    {
                        Row("Number", entry.Number),
                        Row("Name", entry.Name),
                        Row("Revision", entry.Revision),
                        Row("Preset", entry.PresetName),
                        Row("Status", entry.StatusText),
                    }
                ),
                new CadPropertyGroup(
                    "Sheet",
                    new[]
                    {
                        Row("Size", entry.SheetSize),
                        Row("Orientation", settings.Sheet.Orientation.ToString()),
                        Row(
                            "Scale",
                            drawing.Scale > 0.0 ? "1:" + DialogViewModel.Length(drawing.Scale) : "Chosen to fit"
                        ),
                        Row(
                            "Projection",
                            settings.ProjectionMethod == ProjectionMethod.FirstAngle ? "First angle" : "Third angle"
                        ),
                    }
                ),
                new CadPropertyGroup(
                    "Title block",
                    new[] { Row("Project", title.Project), Row("Drawn by", title.DrawnBy), Row("Date", title.Date) }
                ),
                new CadPropertyGroup(
                    "Source",
                    new[]
                    {
                        // A drawing opened from its file and not made again names the source its file keeps.
                        Row(
                            "Source",
                            drawing.Source == null && !string.IsNullOrEmpty(entry.SourceName)
                                ? entry.SourceName + " (from file)"
                                : entry.Provider.Name
                        ),
                        Row("Objects", drawing.Source == null ? "Not at hand" : Objects(drawing.Source.Objects)),
                        Row("Last update", entry.LastMade.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)),
                    }
                ),
            };
        }

        /// <summary>A view: View, Visible area, Display and Content, as the design's B2 shows them.</summary>
        private static IReadOnlyList<CadPropertyGroup> View(ModelDrawing entry, DrawingElement view)
        {
            string kind = Value(view, "Kind") ?? string.Empty;
            string kindText;

            switch (kind)
            {
                case nameof(ViewKind.Section):
                    kindText = "Section";
                    break;
                case nameof(ViewKind.Detail):
                    kindText = "Detail";
                    break;
                case nameof(ViewKind.Isometric):
                    kindText = "Isometric";
                    break;
                case nameof(ViewKind.Free):
                    kindText = "Along a direction of its own";
                    break;
                default:
                    kindText = "Along an axis (like MVIEW)";
                    break;
            }

            string crop = Value(view, "Crop");
            string[] parts = crop?.Split(',');
            string cropText =
                parts != null && parts.Length == 4
                    ? Number(parts[2]) + " × " + Number(parts[3]) + " mm"
                    : crop ?? string.Empty;
            var objects = new List<DrawingObject>();

            foreach (string objectId in view.Links.ObjectIds)
            {
                if (
                    entry.Drawing.Source != null
                    && entry.Drawing.Source.TryGetObject(objectId, out DrawingObject found)
                )
                {
                    objects.Add(found);
                }
            }

            return new[]
            {
                new CadPropertyGroup(
                    "View",
                    new[]
                    {
                        Row("Name", Value(view, "Name") ?? view.Id),
                        Row("Kind", kindText),
                        Row("Scale", "1:" + Number(Value(view, "Scale"))),
                    }
                ),
                new CadPropertyGroup(
                    "Visible area",
                    new[]
                    {
                        Row("Crop", cropText),
                        Row("Depth in front", Depth(Value(view, "DepthInFront"))),
                        Row("Depth behind", Depth(Value(view, "DepthBehind"))),
                    }
                ),
                new CadPropertyGroup(
                    "Display",
                    new[] { Row("Hidden lines", Value(view, "HiddenLines") == "0" ? "Hidden" : "Shown") }
                ),
                new CadPropertyGroup(
                    "Content",
                    new[]
                    {
                        Row(
                            "Objects",
                            objects.Count == 0
                                ? DrawingWorkspace.Count(view.Links.ObjectIds.Count, "object")
                                : Objects(objects)
                        ),
                        Row("On model change", "Make lines again"),
                        Row("Edited", Edited(entry, view.Id)),
                    }
                ),
            };
        }

        /// <summary>What a dimension measures, in words: its kind and axis, "Overall along X".</summary>
        private static string Measures(DrawingElement dimension)
        {
            string kind = Value(dimension, "Kind") ?? "Dimension";
            string axis = Value(dimension, "Axis");
            string words;

            switch (kind)
            {
                case "HoleGroup":
                    words = "Hole group";
                    break;
                case "HoleEdge":
                    words = "Hole to edge";
                    break;
                default:
                    words = kind;
                    break;
            }

            return words
                + (axis == null ? string.Empty : " along " + axis)
                + " · "
                + DrawingWorkspace.Count(dimension.Links.Points.Count, "point");
        }

        /// <summary>Whether an element has hand edits, as the design says it: "Yes · kept on update".</summary>
        private static string Edited(ModelDrawing entry, string id)
        {
            int edits = entry.Drawing.Edits.Count(e => string.Equals(e.ObjectId, id, StringComparison.Ordinal));
            return edits == 0
                ? "No"
                : "Yes · kept on update"
                    + (edits == 1 ? string.Empty : " (" + edits.ToString(CultureInfo.InvariantCulture) + " edits)");
        }

        /// <summary>The objects of ids as the model names them: "B1 · BEAM", or the id where the source is not at hand.</summary>
        private static string ObjectText(ModelDrawing entry, IReadOnlyList<string> ids)
        {
            var names = new List<string>();

            foreach (string objectId in ids.Distinct(StringComparer.Ordinal))
            {
                if (
                    entry.Drawing.Source != null
                    && entry.Drawing.Source.TryGetObject(objectId, out DrawingObject found)
                )
                {
                    names.Add(
                        (found.Mark.Length > 0 ? found.Mark + " · " : string.Empty)
                            + (found.Name.Length > 0 ? found.Name : found.Id)
                    );
                }
                else
                {
                    names.Add(objectId);
                }
            }

            return names.Count == 0 ? "None" : string.Join(", ", names);
        }

        /// <summary>The objects counted by kind, "11 parts, 4 bolts", parts first.</summary>
        internal static string Objects(IEnumerable<DrawingObject> objects)
        {
            var counts = new SortedDictionary<DrawingObjectKind, int>();

            foreach (DrawingObject item in objects)
            {
                counts.TryGetValue(item.Kind, out int count);
                counts[item.Kind] = count + 1;
            }

            if (counts.Count == 0)
            {
                return "None";
            }

            return string.Join(", ", counts.Select(c => DrawingWorkspace.Count(c.Value, KindName(c.Key))));
        }

        internal static string KindName(DrawingObjectKind kind) =>
            kind == DrawingObjectKind.Rebar ? "bar" : kind.ToString().ToLowerInvariant();

        private static CadSelectionInfo With(CadSelectionInfo shown, CadPropertyGroup group)
        {
            var groups = new List<CadPropertyGroup>(shown.Groups) { group };
            return new CadSelectionInfo(shown.Label, groups);
        }

        private static CadPropertyRow Row(string label, string value) =>
            new CadPropertyRow(label, value ?? string.Empty, true);

        private static string Value(DrawingElement element, string name)
        {
            foreach (KeyValuePair<string, string> value in element.Values)
            {
                if (value.Key == name)
                {
                    return value.Value;
                }
            }

            return null;
        }

        /// <summary>A depth of a view as Properties writes it: "500 mm", or "All the model" for a view that sees everything.</summary>
        private static string Depth(string written) =>
            double.TryParse(written, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
            && double.IsPositiveInfinity(value)
                ? "All the model"
                : Number(written) + " mm";

        /// <summary>A number written by the drawing document, written again as the design writes lengths: 3000 as 3 000.</summary>
        private static string Number(string written) =>
            double.TryParse(written, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
            && !double.IsInfinity(value)
            && !double.IsNaN(value)
                ? DialogViewModel.Length(Math.Round(value, 2))
                : written ?? string.Empty;
    }
}
