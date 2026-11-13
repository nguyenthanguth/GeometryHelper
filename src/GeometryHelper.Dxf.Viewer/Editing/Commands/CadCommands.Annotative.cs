using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// -OBJECTSCALE, AIOBJECTSCALEADD and AIOBJECTSCALEREMOVE: the scales an annotative object is shown at, added and taken
    /// away, in the words AutoCAD 2021's console answers them (probed).
    /// </summary>
    internal static partial class CadCommands
    {
        /// <summary>
        /// The scale of the drawing's list of a name, letters of either case the same; null where the list has none of it.
        /// </summary>
        internal static DxfAnnotationScale ScaleNamed(DxfDatabase database, string name)
        {
            DxfObjectContextCollection list = ScaleList(database);
            string typed = (name ?? string.Empty).Trim();

            if (list == null || typed.Length == 0)
            {
                return null;
            }

            if (list.GetContext(typed) is DxfAnnotationScale found)
            {
                return found;
            }

            return null;
        }

        /// <summary>
        /// The scale annotative objects of a space are made at: the annotation scale of model space (CANNOSCALE), 1:1 in a
        /// layout; added to the drawing's list where the list has none of its name and its name is a ratio. Null where there is
        /// none.
        /// </summary>
        internal static DxfAnnotationScale CurrentScale(DxfDatabase database, bool isModel)
        {
            string name = isModel ? database.Cannoscale : "1:1";
            DxfAnnotationScale found = ScaleNamed(database, name);

            if (found != null || !TryReadRatio(name, out double paper, out double drawing))
            {
                return found;
            }

            try
            {
                ScaleList(database)
                    .AddContext(
                        new DxfAnnotationScale
                        {
                            Name = name,
                            PaperUnits = paper,
                            DrawingUnits = drawing,
                        }
                    );
                return ScaleNamed(database, name);
            }
            catch (Exception e) when (e is InvalidOperationException || e is ArgumentException)
            {
                return null;
            }
        }

        /// <summary>Whether the presenter makes an entity annotative: a TEXT, an MTEXT, an attribute definition, a hatch or a multileader.</summary>
        internal static bool CanMakeAnnotative(DxfEntity entity) =>
            entity is DxfDBText text && !(entity is DxfAttributeReference)
            || entity is DxfMText
            || entity is DxfHatch
            || entity is DxfMLeader;

        /// <summary>
        /// Makes an entity annotative at the scale of its space, as the palette's Annotative row and -HATCH ANnotative do: the
        /// drawing's list given the scale where it has none. The reason it cannot be made, null where it was.
        /// </summary>
        internal static string MakeAnnotative(DxfDatabase database, DxfEntity entity)
        {
            try
            {
                if (!CanMakeAnnotative(entity))
                {
                    return "A " + CadDocument.KindName(entity) + " is not made annotative here.";
                }

                bool isModel = !(entity.BlockName ?? string.Empty).StartsWith(
                    "*Paper_Space",
                    StringComparison.OrdinalIgnoreCase
                );

                if (CurrentScale(database, isModel) == null)
                {
                    return "The drawing has no annotation scale to make it annotative at.";
                }

                entity.Annotative = DxfAnnotativeStates.True;
                return null;
            }
            catch (Exception e) when (e is InvalidOperationException || e is NotSupportedException)
            {
                return e.Message;
            }
        }

        private static string Objects(int count, string rest) =>
            count.ToString(CultureInfo.InvariantCulture) + (count == 1 ? " object " : " objects ") + rest;

        /// <summary>The annotative objects among the handles, as AutoCAD's "Select annotative objects" takes them.</summary>
        private static List<string> AnnotativeAmong(CadCommandContext c, IReadOnlyList<string> handles)
        {
            var annotative = new List<string>();

            foreach (string handle in handles)
            {
                DxfEntity entity = c.Document.FindEntity(handle);

                if (entity != null && entity.Annotative == DxfAnnotativeStates.True)
                {
                    annotative.Add(handle);
                }
            }

            return annotative;
        }

        /// <summary>
        /// -OBJECTSCALE: "Select annotative objects:", then "Enter an option [Add/Delete/?] &lt;Add&gt;:" and the named scale to
        /// add or delete, asked again until Enter, each answered as AutoCAD says it ("1 object updated to support annotation
        /// scale &lt;1:100&gt;.", "1 object scale removed."). Delete answers a scale the object is not shown at, and the last
        /// scale it is, as AutoCAD does: "0 objects scale removed." with the objects that do not support it. Each answer is one
        /// undo step.
        /// </summary>
        private static IEnumerable<CadPrompt> ObjectScale(CadCommandContext c)
        {
            IReadOnlyList<string> picked;

            if (c.PreSelected.Count > 0)
            {
                picked = c.PreSelected;
            }
            else
            {
                yield return CadPrompt.ForSelection("Select annotative objects");
                picked = c.Input.Handles;
            }

            List<string> handles = AnnotativeAmong(c, picked);

            if (handles.Count == 0)
            {
                c.Write("No annotative objects found.");
                yield break;
            }

            yield return CadPrompt.ForKeyword("Enter an option", new[] { "Add", "Delete", "?" }, "Add");
            bool add = !c.Input.Is("Delete");

            if (!c.Input.IsNone && !c.Input.Is("Add") && !c.Input.Is("Delete"))
            {
                if (!c.Input.Is("?"))
                {
                    yield break;
                }

                add = true;
            }

            bool first = true;

            while (true)
            {
                string offered = first ? ScaleOffered(c) : null;
                yield return CadPrompt.ForKeywordOrText(
                    "Enter named scale to " + (add ? "add" : "delete") + " or",
                    new[] { "?" },
                    offered
                );
                first = false;
                string typed = c.Input.IsNone ? offered : (c.Input.Text ?? string.Empty).Trim();

                if (string.IsNullOrEmpty(typed))
                {
                    yield break;
                }

                if (typed == "?")
                {
                    c.Write("Annotation scales: " + string.Join(", ", Scales(c.Database).Select(s => s.Name)));
                    continue;
                }

                DxfAnnotationScale scale = ScaleNamed(c.Database, typed);

                if (scale == null)
                {
                    c.Write("Scale " + typed + " not found.");
                    c.Write(string.Empty);
                    continue;
                }

                ChangeScale(c, handles, scale, add);
            }
        }

        /// <summary>The scale -OBJECTSCALE offers first: the annotation scale of the space.</summary>
        private static string ScaleOffered(CadCommandContext c) => c.Space.IsModel ? c.Database.Cannoscale : "1:1";

        /// <summary>AIOBJECTSCALEADD: "Select annotative objects:", each made to support the current annotation scale.</summary>
        private static IEnumerable<CadPrompt> AnnotationScaleAdd(CadCommandContext c) => CurrentScaleChange(c, true);

        /// <summary>AIOBJECTSCALEREMOVE: "Select annotative objects:", the current annotation scale taken from each.</summary>
        private static IEnumerable<CadPrompt> AnnotationScaleRemove(CadCommandContext c) =>
            CurrentScaleChange(c, false);

        private static IEnumerable<CadPrompt> CurrentScaleChange(CadCommandContext c, bool add)
        {
            IReadOnlyList<string> picked;

            if (c.PreSelected.Count > 0)
            {
                picked = c.PreSelected;
            }
            else
            {
                yield return CadPrompt.ForSelection("Select annotative objects");
                picked = c.Input.Handles;
            }

            List<string> handles = AnnotativeAmong(c, picked);

            if (handles.Count == 0)
            {
                yield break;
            }

            DxfAnnotationScale scale = CurrentScale(c.Database, c.Space.IsModel);

            if (scale == null)
            {
                c.Write("The drawing has no annotation scale.");
                yield break;
            }

            ChangeScale(c, handles, scale, add);
        }

        /// <summary>
        /// Adds a scale to the objects, or takes it from them, in one undo step, and says what came of it in AutoCAD's words:
        /// the number updated or removed and, for Delete, those that do not support the scale and so cannot remove it.
        /// </summary>
        private static void ChangeScale(
            CadCommandContext c,
            IReadOnlyList<string> handles,
            DxfAnnotationScale scale,
            bool add
        )
        {
            var planned = handles
                .Select(h => new CadEditRecord(
                    CadEditRecordKind.PropertyChanged,
                    h,
                    "Annotation scale",
                    null,
                    scale.Name
                ))
                .ToList();
            int done = 0;
            int refused = 0;
            string reason = null;
            string name = scale.Name;

            CadEditResult result = c.Apply(planned, s =>
            {
                foreach (string handle in handles)
                {
                    DxfEntity entity = s.Document.FindEntity(handle);
                    DxfAnnotationScale own = ScaleNamed(s.Database, name);

                    try
                    {
                        if (add)
                        {
                            entity.AddContext(own);
                            done++;
                        }
                        else if (entity.HasContext(own) && entity.GetContexts().Count > 1)
                        {
                            entity.RemoveContext(own);
                            done++;
                        }
                        else
                        {
                            refused++;
                        }
                    }
                    catch (Exception e) when (e is InvalidOperationException || e is NotSupportedException)
                    {
                        refused++;
                        reason = e.Message;
                    }
                }
            });

            if (!result.Succeeded)
            {
                return;
            }

            if (add)
            {
                c.Write(Objects(done, "updated to support annotation scale <" + name + ">."));

                if (refused > 0)
                {
                    c.Write(Objects(refused, "cannot be updated: " + reason));
                }

                return;
            }

            c.Write(Objects(done, "scale removed."));

            if (done == 0 && refused > 0)
            {
                c.Write(Objects(refused, "doesn't support annotation scale <" + name + ">."));
                c.Write(refused.ToString(CultureInfo.InvariantCulture) + " objects cannot remove scale.");
            }
        }
    }
}
