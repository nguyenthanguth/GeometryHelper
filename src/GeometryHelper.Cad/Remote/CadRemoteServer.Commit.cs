using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Cad.Remote;
using GeometryHelper.Dxf;

namespace GeometryHelper.Cad
{
    /// <summary>Writing objects: the queued inserts, modifies and deletes of a client, applied as one undo step.</summary>
    public sealed partial class CadRemoteServer
    {
        /// <summary>The name of the undo step a commit makes, as the Undo list and U show it.</summary>
        private const string StepName = "REMOTE";

        private enum StepKind
        {
            Insert,
            Modify,
            Delete,
        }

        private sealed class Step
        {
            public StepKind Kind;
            public string Handle;
            public DxfEntity Entity;
            public CadSpace Space;
        }

        private JsonObject Commit(CadDocument document, JsonObject request)
        {
            string text = request.GetString("fragment");
            DxfDatabase fragment = string.IsNullOrEmpty(text) ? null : DxfDatabase.Parse(text);
            var byReference = new Dictionary<string, DxfEntity>(StringComparer.Ordinal);

            if (fragment != null)
            {
                foreach (DxfEntity entity in fragment.ModelSpace)
                {
                    if (!string.IsNullOrEmpty(entity.Handle))
                    {
                        byReference[entity.Handle] = entity;
                    }
                }
            }

            // A command running holds the drawing's undo group open: an edit now would be taken back with the command's Esc.
            CadCommandSession session = _host.SessionOf(document);

            if (document.IsInGroup || (session != null && session.IsRunning))
            {
                throw new Refusal(
                    "The drawing "
                        + document.Title
                        + " is busy with the command "
                        + (session?.Current?.Name ?? "running in it")
                        + ": end it, then commit the changes again."
                );
            }

            // Every step is checked before any is applied, so that one that cannot be made changes nothing.
            var steps = new List<Step>();
            var planned = new List<CadEditRecord>();

            foreach (object item in request.GetArray("operations"))
            {
                var operation = (JsonObject)item;
                string kind = operation.GetString("op");
                string handle = operation.GetString("handle");

                switch (kind)
                {
                    case "insert":
                        DxfEntity added = Sent(byReference, operation);
                        CheckBlock(document, added);
                        steps.Add(
                            new Step
                            {
                                Kind = StepKind.Insert,
                                Entity = added,
                                Space = SpaceNamed(document, operation.GetString("layout")),
                            }
                        );
                        break;
                    case "modify":
                        Existing(document, handle);
                        DxfEntity changed = Sent(byReference, operation);
                        CheckBlock(document, changed);
                        steps.Add(
                            new Step
                            {
                                Kind = StepKind.Modify,
                                Handle = handle,
                                Entity = changed,
                            }
                        );
                        planned.Add(new CadEditRecord(CadEditRecordKind.Reshaped, handle, "Remote"));
                        break;
                    case "delete":
                        Existing(document, handle);
                        steps.Add(new Step { Kind = StepKind.Delete, Handle = handle });
                        planned.Add(new CadEditRecord(CadEditRecordKind.Erased, handle, "Remote"));
                        break;
                    default:
                        throw new Refusal("GeometryHelper CAD does not know the change " + kind + ".");
                }
            }

            var inserted = new JsonArray();
            CadEditResult result = document.ApplyEdit(StepName, planned, scope =>
                Apply(document, scope, steps, fragment, inserted)
            );

            if (!result.Succeeded)
            {
                throw new Refusal(result.Message);
            }

            return Shown(document).Set("handles", inserted);
        }

        private static DxfEntity Sent(Dictionary<string, DxfEntity> byReference, JsonObject operation)
        {
            string reference = operation.GetString("ref");

            if (reference == null || !byReference.TryGetValue(reference, out DxfEntity entity))
            {
                throw new Refusal("A change names an object that did not come with it.");
            }

            return entity;
        }

        private static DxfEntity Existing(CadDocument document, string handle)
        {
            DxfEntity entity = handle == null ? null : document.FindEntity(handle);

            if (entity == null || entity.IsErased)
            {
                throw new Refusal("The drawing " + document.Title + " has no object " + handle + ".");
            }

            if (document.FindSpace(handle) == null)
            {
                throw new Refusal(
                    "The object "
                        + handle
                        + " is inside a block of "
                        + document.Title
                        + ", not in a space, and is not changed from outside."
                );
            }

            return entity;
        }

        private static CadSpace SpaceNamed(CadDocument document, string layout)
        {
            if (string.IsNullOrEmpty(layout))
            {
                return document.ModelSpace;
            }

            CadSpace space = document.Spaces.FirstOrDefault(s =>
                !s.IsBlockEditor && string.Equals(s.Name, layout, StringComparison.OrdinalIgnoreCase)
            );
            return space ?? throw new Refusal("The drawing " + document.Title + " has no layout named " + layout + ".");
        }

        /// <summary>Refuses an INSERT of a block the drawing does not have: the blocks of a drawing are its own, and none is carried in yet.</summary>
        private static void CheckBlock(CadDocument document, DxfEntity entity)
        {
            if (entity is DxfBlockReference insert)
            {
                string name = insert.Name;

                if (!name.StartsWith("*", StringComparison.Ordinal) && !document.Database.BlockTable.Has(name))
                {
                    throw new Refusal(
                        "The drawing "
                            + document.Title
                            + " has no block "
                            + name
                            + " for the INSERT to place: only blocks the drawing already has can be inserted from outside."
                    );
                }
            }
        }

        private void Apply(
            CadDocument document,
            CadEditScope scope,
            List<Step> steps,
            DxfDatabase fragment,
            JsonArray inserted
        )
        {
            int i = 0;

            while (i < steps.Count)
            {
                Step step = steps[i];

                if (step.Kind == StepKind.Insert)
                {
                    // Objects inserted one after another into one space are copied together, so that what points to another
                    // among them (a hatch to its boundary) points to its copy.
                    var group = new List<DxfEntity>();

                    while (
                        i < steps.Count
                        && steps[i].Kind == StepKind.Insert
                        && ReferenceEquals(steps[i].Space, step.Space)
                    )
                    {
                        group.Add(steps[i].Entity);
                        i++;
                    }

                    InsertGroup(document, scope, fragment, step.Space, group, inserted);
                    continue;
                }

                // An object whose owner took the change itself is left alone.
                if (!scope.IsHandled(step.Handle))
                {
                    DxfEntity old = document.FindEntity(step.Handle);

                    if (step.Kind == StepKind.Delete)
                    {
                        old.Erase();
                    }
                    else
                    {
                        Replace(document, fragment, old, step);
                    }
                }

                i++;
            }
        }

        private static void InsertGroup(
            CadDocument document,
            CadEditScope scope,
            DxfDatabase fragment,
            CadSpace space,
            List<DxfEntity> group,
            JsonArray inserted
        )
        {
            // A layer the drawing lacks is made by the copy; the layer box and the Layer manager are told.
            List<string> missing = group
                .Select(e => e.Layer)
                .Where(l => !string.IsNullOrEmpty(l) && !document.Database.LayerTable.Has(l))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(l => l, StringComparer.OrdinalIgnoreCase)
                .ThenBy(l => l, StringComparer.Ordinal)
                .ToList();
            IReadOnlyList<DxfEntity> copies = fragment.WblockCloneObjects(group, space.Block);

            foreach (DxfEntity copy in copies)
            {
                // An MTEXT copied into a drawing of R12 is written as TEXTs and is in no space itself.
                if (string.IsNullOrEmpty(copy.Handle) || copy.IsErased)
                {
                    inserted.Add(null);
                    continue;
                }

                // A dimension made in code comes with a picture block that is empty: the app draws it, as DIM would.
                if (copy is DxfDimension dimension && !dimension.DimBlock.Any(e => !e.IsErased))
                {
                    CadCommands.Redraw(dimension);
                }

                scope.Added(space, copy);
                inserted.Add(copy.Handle);
            }

            foreach (string layer in missing)
            {
                scope.Record(new CadEditRecord(CadEditRecordKind.TableChanged, null, "Layer", null, layer));
            }
        }

        /// <summary>
        /// Writes a changed object over the one of the drawing, which keeps its handle (AutoCAD's <c>HandOverTo</c>): the
        /// object is copied into the drawing first, so that the layers, styles and blocks it needs are brought, and that copy
        /// is what takes the place. A dimension is drawn again, in the picture block it had.
        /// </summary>
        private static void Replace(CadDocument document, DxfDatabase fragment, DxfEntity old, Step step)
        {
            CadSpace space = document.FindSpace(step.Handle);
            DxfEntity made = fragment.WblockCloneObjects(new[] { step.Entity }, space.Block)[0];
            DxfEntity fresh = made.Clone();
            made.Erase();
            DxfBlockTableRecord spare = null;

            // The copy was brought with a picture block of its own; the object keeps the one it had, drawn again below.
            if (fresh is DxfDimension dimension && old is DxfDimension was && was.DimBlock != null)
            {
                spare = dimension.DimBlock;
                dimension.DimBlock = was.DimBlock;
            }

            old.HandOverTo(fresh, true, true);

            if (spare != null && spare.IsAnonymous)
            {
                spare.Erase();
            }

            if (document.FindEntity(step.Handle) is DxfDimension drawn)
            {
                CadCommands.Redraw(drawn);
            }
        }
    }
}
