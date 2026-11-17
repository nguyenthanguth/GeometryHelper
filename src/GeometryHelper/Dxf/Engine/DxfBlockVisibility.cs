using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Export;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// Edits the visibility states of a dynamic block in place, as AutoCAD's Block Editor does with BVSTATE, BVHIDE and BVSHOW:
    /// a state is added, renamed, deleted or moved, and an entity is shown or hidden in one state or in all of them.
    /// </summary>
    /// <remarks>
    /// The visibility parameter keeps the entities it governs (331) and, for each state, the entities shown in it (332) and the
    /// parameters, grips and actions it lists (333). An entity is hidden in a state where it is governed and not shown; an
    /// entity governed by none is shown in every state. A governed entity not shown in the block's first state is kept
    /// invisible, so that an INSERT that has set no value draws the first state.
    /// </remarks>
    internal static class DxfBlockVisibility
    {
        /// <summary>How a new state shows the entities the block governs.</summary>
        internal enum NewState
        {
            /// <summary>The state shows none.</summary>
            HideAll,

            /// <summary>The state shows every governed entity.</summary>
            ShowAll,

            /// <summary>The state shows what another state shows.</summary>
            Copy,
        }

        private sealed class State
        {
            public string Name;
            public List<string> Shown = new List<string>();
            public List<string> Listed = new List<string>();
            public List<KeyValuePair<int, string>> Other = new List<KeyValuePair<int, string>>();
        }

        /// <summary>The section of the parameter as the file holds it: what comes before the states, the governed entities and the states.</summary>
        private sealed class Model
        {
            public DxfItem Node;
            public List<KeyValuePair<int, string>> Before = new List<KeyValuePair<int, string>>();
            public List<KeyValuePair<int, string>> Head = new List<KeyValuePair<int, string>>();
            public List<string> Governed = new List<string>();
            public List<State> States = new List<State>();
        }

        private static Model Read(DxfBlockTableRecord block)
        {
            DxfItem node = DxfDynamicBlockWriter.NodeOf(block, "BLOCKVISIBILITYPARAMETER");

            if (node == null)
            {
                return null;
            }

            List<DxfTypedValue> tail = node.File.TailOf(node, 0);
            int at = tail.FindIndex(g => g.TypeCode == 100 && g.Value?.Trim() == "AcDbBlockVisibilityParameter");

            if (at < 0)
            {
                return null;
            }

            var model = new Model { Node = node };

            for (int i = 0; i <= at; i++)
            {
                model.Before.Add(Pair(tail[i].TypeCode, tail[i].Value));
            }

            State state = null;

            for (int i = at + 1; i < tail.Count; i++)
            {
                DxfTypedValue group = tail[i];

                if (group.TypeCode == 303)
                {
                    state = new State { Name = group.Value };
                    model.States.Add(state);
                }
                else if (state == null)
                {
                    if (group.TypeCode == 331)
                    {
                        model.Governed.Add(group.Value?.Trim());
                    }
                    else if (group.TypeCode != 93 && group.TypeCode != 92)
                    {
                        model.Head.Add(Pair(group.TypeCode, group.Value));
                    }
                }
                else if (group.TypeCode == 332)
                {
                    state.Shown.Add(group.Value?.Trim());
                }
                else if (group.TypeCode == 333)
                {
                    state.Listed.Add(group.Value?.Trim());
                }
                else if (group.TypeCode != 94 && group.TypeCode != 95)
                {
                    state.Other.Add(Pair(group.TypeCode, group.Value));
                }
            }

            return model;
        }

        private static void Write(Model model)
        {
            var written = new List<KeyValuePair<int, string>>(model.Before);
            written.AddRange(model.Head);
            written.Add(Pair(93, Int(model.Governed.Count)));

            foreach (string handle in model.Governed)
            {
                written.Add(Pair(331, handle));
            }

            written.Add(Pair(92, Int(model.States.Count)));

            foreach (State state in model.States)
            {
                written.Add(Pair(303, state.Name));
                written.Add(Pair(94, Int(state.Shown.Count)));
                state.Shown.ForEach(h => written.Add(Pair(332, h)));
                written.Add(Pair(95, Int(state.Listed.Count)));
                state.Listed.ForEach(h => written.Add(Pair(333, h)));
                written.AddRange(state.Other);
            }

            model.Node.File.ReplaceTail(model.Node, 0, written);
        }

        private static KeyValuePair<int, string> Pair(int code, string value) =>
            new KeyValuePair<int, string>(code, value);

        private static string Int(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static Model Needed(DxfBlockTableRecord block)
        {
            Model model = Read(block);

            if (model == null)
            {
                throw new InvalidOperationException("The block " + block.Name + " has no visibility parameter.");
            }

            return model;
        }

        private static State Find(Model model, string name)
        {
            foreach (State state in model.States)
            {
                if (
                    string.Equals(
                        DxfReader.NameOf(state.Name ?? string.Empty),
                        name?.Trim(),
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    return state;
                }
            }

            return null;
        }

        private static bool Has(List<string> handles, string handle)
        {
            foreach (string each in handles)
            {
                if (string.Equals(each, handle, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static void Take(List<string> handles, string handle) =>
            handles.RemoveAll(h => string.Equals(h, handle, StringComparison.OrdinalIgnoreCase));

        /// <summary>The handles of the entities the block's visibility parameter governs, in its order; none where it has no such parameter.</summary>
        internal static IReadOnlyList<string> Governed(DxfBlockTableRecord block)
        {
            Model model = Read(block);
            return model == null ? (IReadOnlyList<string>)new string[0] : model.Governed.AsReadOnly();
        }

        /// <summary>Whether a name is one a state may have: of one line, at least one letter, no state of it already (letters of either case the same).</summary>
        internal static bool IsFreeName(DxfBlockTableRecord block, string name)
        {
            Model model = Read(block);

            return !string.IsNullOrWhiteSpace(name)
                && name.IndexOfAny(new[] { '\r', '\n' }) < 0
                && (model == null || Find(model, name) == null);
        }

        /// <summary>Adds a state, last, named as given, showing the entities the way asked: none, every governed one, or what the state <paramref name="from"/> shows.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the block has no visibility parameter, the name is not free, or the state to copy is not one.</exception>
        internal static void Add(DxfBlockTableRecord block, string name, NewState how, string from)
        {
            Model model = Needed(block);

            if (!IsFreeName(block, name))
            {
                throw new InvalidOperationException("The visibility state " + name + " already exists.");
            }

            State source = how == NewState.Copy ? Find(model, from) : null;

            if (how == NewState.Copy && source == null)
            {
                throw new InvalidOperationException("The block has no visibility state " + from + ".");
            }

            var state = new State { Name = name.Trim() };

            if (how == NewState.ShowAll)
            {
                state.Shown.AddRange(model.Governed);
            }
            else if (source != null)
            {
                state.Shown.AddRange(source.Shown);
            }

            // Every state lists the same parameters, grips and actions.
            if (model.States.Count > 0)
            {
                state.Listed.AddRange(model.States[0].Listed);
            }

            model.States.Add(state);
            Write(model);
        }

        /// <summary>Renames a state.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the block has no visibility parameter, has no such state, or the new name is another state's.</exception>
        internal static void Rename(DxfBlockTableRecord block, string name, string to)
        {
            Model model = Needed(block);
            State state = Find(model, name)
                ?? throw new InvalidOperationException("The block has no visibility state " + name + ".");

            if (!string.Equals(name?.Trim(), to?.Trim(), StringComparison.OrdinalIgnoreCase) && !IsFreeName(block, to))
            {
                throw new InvalidOperationException("The visibility state " + to + " already exists. Not renaming.");
            }

            if (string.IsNullOrWhiteSpace(to) || to.IndexOfAny(new[] { '\r', '\n' }) >= 0)
            {
                throw new InvalidOperationException(
                    "Invalid name for a visibility state. Please specify a different name."
                );
            }

            state.Name = to.Trim();
            Write(model);
        }

        /// <summary>Takes a state out; the entities only it showed stay governed, hidden in every state that is left.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the block has no visibility parameter, has no such state, or the state is the last.</exception>
        internal static void Delete(DxfBlockTableRecord block, string name)
        {
            Model model = Needed(block);
            State state = Find(model, name)
                ?? throw new InvalidOperationException("The block has no visibility state " + name + ".");

            if (model.States.Count < 2)
            {
                throw new InvalidOperationException("Only one visibility state defined for block.");
            }

            model.States.Remove(state);
            Write(model);
            KeepFirstState(block);
        }

        /// <summary>Moves a state up or down the list by a number of places, kept within it.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the block has no visibility parameter or no such state.</exception>
        internal static void Move(DxfBlockTableRecord block, string name, int places)
        {
            Model model = Needed(block);
            State state = Find(model, name)
                ?? throw new InvalidOperationException("The block has no visibility state " + name + ".");
            int from = model.States.IndexOf(state);
            int to = Math.Max(0, Math.Min(model.States.Count - 1, from + places));

            if (to == from)
            {
                return;
            }

            model.States.RemoveAt(from);
            model.States.Insert(to, state);
            Write(model);
            KeepFirstState(block);
        }

        /// <summary>
        /// Shows or hides entities in a state, or in every state where the name is null, as BVSHOW and BVHIDE do. Hiding governs an
        /// entity no state shows yet and has it shown in every other state; showing an entity in a state makes the state list it.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the block has no visibility parameter or no such state.</exception>
        internal static void SetVisible(
            DxfBlockTableRecord block,
            IEnumerable<DxfEntity> entities,
            string state,
            bool visible
        )
        {
            Model model = Needed(block);
            State only = null;

            if (state != null)
            {
                only = Find(model, state)
                    ?? throw new InvalidOperationException("The block has no visibility state " + state + ".");
            }

            foreach (DxfEntity entity in entities)
            {
                string handle = entity.Handle;
                bool governed = Has(model.Governed, handle);

                if (visible)
                {
                    if (!governed)
                    {
                        // An entity no state governs shows in every state already.
                        continue;
                    }

                    foreach (State each in model.States)
                    {
                        if ((only == null || ReferenceEquals(only, each)) && !Has(each.Shown, handle))
                        {
                            each.Shown.Add(handle);
                        }
                    }

                    continue;
                }

                if (!governed)
                {
                    // The entity was shown in every state: it is now governed, and shown in those not asked to hide it.
                    model.Governed.Add(handle);

                    foreach (State each in model.States)
                    {
                        if (only != null && !ReferenceEquals(only, each))
                        {
                            each.Shown.Add(handle);
                        }
                    }

                    continue;
                }

                foreach (State each in model.States)
                {
                    if (only == null || ReferenceEquals(only, each))
                    {
                        Take(each.Shown, handle);
                    }
                }
            }

            Write(model);
            KeepFirstState(block);
        }

        /// <summary>
        /// Takes out of the governed objects and of every state the handles of entities the block no longer has, as erasing them in
        /// the Block Editor leaves them; nothing is written where none is left over.
        /// </summary>
        internal static void Prune(DxfBlockTableRecord block)
        {
            Model model = Read(block);

            if (model == null)
            {
                return;
            }

            var alive = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (DxfEntity entity in block)
            {
                if (!entity.IsErased && entity.Handle != null)
                {
                    alive.Add(entity.Handle);
                }
            }

            bool changed = model.Governed.RemoveAll(h => !alive.Contains(h ?? string.Empty)) > 0;

            foreach (State state in model.States)
            {
                changed |= state.Shown.RemoveAll(h => !alive.Contains(h ?? string.Empty)) > 0;
            }

            if (changed)
            {
                Write(model);
            }
        }

        /// <summary>Takes the handles of nodes erased out of every state's list of what it lists, and of the entities nothing governs any more.</summary>
        internal static void Unlist(DxfBlockTableRecord block, IReadOnlyList<string> handles)
        {
            Model model = Read(block);

            if (model == null)
            {
                return;
            }

            foreach (State state in model.States)
            {
                foreach (string handle in handles)
                {
                    Take(state.Listed, handle);
                }
            }

            Write(model);
        }

        /// <summary>
        /// Keeps the block in the state its first state shows, as AutoCAD keeps it: a governed entity the first state does not show
        /// is made invisible and the rest visible.
        /// </summary>
        internal static void KeepFirstState(DxfBlockTableRecord block)
        {
            Model model = Read(block);

            if (model == null || model.States.Count == 0)
            {
                return;
            }

            List<string> first = model.States[0].Shown;

            foreach (DxfEntity entity in block)
            {
                if (Has(model.Governed, entity.Handle))
                {
                    bool shown = Has(first, entity.Handle);

                    if (entity.Visible != shown)
                    {
                        entity.Visible = shown;
                    }
                }
            }
        }
    }
}
