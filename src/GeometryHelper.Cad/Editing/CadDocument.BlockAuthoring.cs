using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// The authoring edits of the Block Editor, as AutoCAD 2021's: the visibility states of the block open (BVSTATE) and the
    /// objects they show (BVHIDE, BVSHOW), and the parameters and actions taken out of it. Each is an edit of the document, one
    /// undo step of its own or a part of the command's that makes it; the work on the evaluation graph is the library's
    /// (<see cref="DxfBlockVisibility"/>, <see cref="DxfBlockAuthoring"/>).
    /// </summary>
    public sealed partial class CadDocument
    {
        /// <summary>Why a visibility edit cannot be made now; null where it can: the editor is not open on a block, or the block has no visibility parameter.</summary>
        private string VisibilityRefused()
        {
            CadBlockEditor editor = _blockEditor;

            if (editor == null || editor.IsCurrentDrawing)
            {
                return "The Block Editor is not open on a block.";
            }

            return !editor.Space.Block.GetVisibilityStates().Any() ? "** No Visibility Parameter Present. **" : null;
        }

        /// <summary>
        /// Adds a visibility state to the block open, as the New of BVSTATE does: named as given, showing no object, every object the
        /// visibility parameter governs, or what the current state shows; it goes last in the list. The state shown is not changed.
        /// </summary>
        /// <param name="name">The name of the new state.</param>
        /// <param name="how">What the new state shows: "Hide all", "Show all" or "Current visibility".</param>
        /// <returns>What came of it: refused where the editor is not open on a block, the block has no visibility parameter, or
        /// the name is empty, of more than a line or another state's.</returns>
        internal CadEditResult AddVisibilityState(string name, string how)
        {
            string refused = VisibilityRefused();

            if (refused != null)
            {
                return CadEditResult.Refuse(refused);
            }

            CadBlockEditor editor = _blockEditor;
            string blockName = editor.Name;
            string current = editor.VisibilityState;
            DxfBlockVisibility.NewState mode =
                string.Equals(how, "Show all", StringComparison.OrdinalIgnoreCase) ? DxfBlockVisibility.NewState.ShowAll
                : string.Equals(how, "Current visibility", StringComparison.OrdinalIgnoreCase)
                    ? DxfBlockVisibility.NewState.Copy
                : DxfBlockVisibility.NewState.HideAll;

            if (!DxfBlockVisibility.IsFreeName(editor.Space.Block, name))
            {
                return CadEditResult.Refuse(
                    string.IsNullOrWhiteSpace(name)
                        ? "Please specify a name for the new visibility state."
                        : "The visibility state " + name.Trim() + " already exists."
                );
            }

            return ApplyEdit(
                "BVSTATE",
                new[]
                {
                    new CadEditRecord(CadEditRecordKind.TableChanged, null, "Visibility state", null, name.Trim()),
                },
                s => DxfBlockVisibility.Add(s.Database.BlockTable[blockName], name, mode, current)
            );
        }

        /// <summary>Renames a visibility state of the block open.</summary>
        /// <param name="name">The state to rename.</param>
        /// <param name="to">Its new name.</param>
        /// <returns>What came of it: refused where there is no such state or the new name is empty or another state's.</returns>
        internal CadEditResult RenameVisibilityState(string name, string to)
        {
            string refused = VisibilityRefused();

            if (refused != null)
            {
                return CadEditResult.Refuse(refused);
            }

            CadBlockEditor editor = _blockEditor;
            string blockName = editor.Name;

            if (!editor.VisibilityStates.Any(n => string.Equals(n, name?.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                return CadEditResult.Refuse("Invalid visibility state.");
            }

            if (string.IsNullOrWhiteSpace(to) || to.IndexOfAny(new[] { '\r', '\n' }) >= 0)
            {
                return CadEditResult.Refuse("Invalid name for a visibility state. Please specify a different name.");
            }

            if (
                !string.Equals(name.Trim(), to.Trim(), StringComparison.OrdinalIgnoreCase)
                && !DxfBlockVisibility.IsFreeName(editor.Space.Block, to)
            )
            {
                return CadEditResult.Refuse("The visibility state " + to.Trim() + " already exists. Not renaming.");
            }

            bool wasCurrent = string.Equals(editor.VisibilityState, name.Trim(), StringComparison.OrdinalIgnoreCase);
            CadEditResult result = ApplyEdit(
                "BVSTATE",
                new[]
                {
                    new CadEditRecord(CadEditRecordKind.TableChanged, null, "Visibility state", name.Trim(), to.Trim()),
                },
                s => DxfBlockVisibility.Rename(s.Database.BlockTable[blockName], name, to)
            );

            if (result.Succeeded && wasCurrent)
            {
                _blockEditor.ShowState(to.Trim());
            }

            return result;
        }

        /// <summary>Deletes visibility states of the block open, as the Delete of BVSTATE does.</summary>
        /// <param name="names">The states to delete.</param>
        /// <returns>What came of it: refused where one is the state shown ("Cannot delete the current visibility state."), is not
        /// one, or would be the last the block has; nothing is deleted then.</returns>
        internal CadEditResult DeleteVisibilityStates(IReadOnlyList<string> names)
        {
            string refused = VisibilityRefused();

            if (refused != null)
            {
                return CadEditResult.Refuse(refused);
            }

            CadBlockEditor editor = _blockEditor;
            string blockName = editor.Name;
            var found = new List<string>();

            foreach (string name in names)
            {
                string state = editor.VisibilityStates.FirstOrDefault(n =>
                    string.Equals(n, name?.Trim(), StringComparison.OrdinalIgnoreCase)
                );

                if (state == null)
                {
                    return CadEditResult.Refuse("Invalid visibility state.");
                }

                if (string.Equals(state, editor.VisibilityState, StringComparison.Ordinal))
                {
                    return CadEditResult.Refuse("Cannot delete the current visibility state.");
                }

                if (!found.Contains(state))
                {
                    found.Add(state);
                }
            }

            if (found.Count >= editor.VisibilityStates.Count)
            {
                return CadEditResult.Refuse("Only one visibility state defined for block.");
            }

            return ApplyEdit(
                "BVSTATE",
                new[]
                {
                    new CadEditRecord(
                        CadEditRecordKind.TableChanged,
                        null,
                        "Visibility state",
                        string.Join(",", found),
                        null
                    ),
                },
                s =>
                {
                    foreach (string state in found)
                    {
                        DxfBlockVisibility.Delete(s.Database.BlockTable[blockName], state);
                    }
                }
            );
        }

        /// <summary>Moves a visibility state of the block open up or down the list, as the Move Up and Move Down of BVSTATE's dialog do.</summary>
        /// <param name="name">The state to move.</param>
        /// <param name="places">How many places: negative up, positive down.</param>
        /// <returns>What came of it: refused where there is no such state.</returns>
        internal CadEditResult MoveVisibilityState(string name, int places)
        {
            string refused = VisibilityRefused();

            if (refused != null)
            {
                return CadEditResult.Refuse(refused);
            }

            CadBlockEditor editor = _blockEditor;
            string blockName = editor.Name;

            if (!editor.VisibilityStates.Any(n => string.Equals(n, name?.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                return CadEditResult.Refuse("Invalid visibility state.");
            }

            return ApplyEdit(
                "BVSTATE",
                new[]
                {
                    new CadEditRecord(
                        CadEditRecordKind.TableChanged,
                        null,
                        "Visibility state order",
                        null,
                        name.Trim()
                    ),
                },
                s => DxfBlockVisibility.Move(s.Database.BlockTable[blockName], name, places)
            );
        }

        /// <summary>
        /// Shows or hides objects of the block open in the state shown or in every state, as BVSHOW and BVHIDE do: an object the
        /// visibility parameter does not govern is governed by hiding it, shown in the states that are not asked to hide it.
        /// </summary>
        /// <param name="handles">The handles of the objects.</param>
        /// <param name="visible">True to show them, false to hide them.</param>
        /// <param name="allStates">Whether the change is made in every state, else in the state shown.</param>
        /// <returns>What came of it: refused where the block has no visibility parameter or an object is not of the block.</returns>
        internal CadEditResult SetObjectVisibility(IReadOnlyList<string> handles, bool visible, bool allStates)
        {
            string refused = VisibilityRefused();

            if (refused != null)
            {
                return CadEditResult.Refuse(refused);
            }

            CadBlockEditor editor = _blockEditor;
            string blockName = editor.Name;
            string state = allStates ? null : editor.VisibilityState;
            var entities = new List<DxfEntity>();

            foreach (string handle in handles)
            {
                DxfEntity entity = FindEntity(handle);

                if (
                    entity == null
                    || !string.Equals(entity.OwnerHandle, editor.Space.Block.Handle, StringComparison.OrdinalIgnoreCase)
                )
                {
                    return CadEditResult.Refuse("The entity " + handle + " is not in the block.");
                }

                entities.Add(entity);
            }

            var records = new List<CadEditRecord>
            {
                new CadEditRecord(
                    CadEditRecordKind.TableChanged,
                    null,
                    "Object visibility",
                    null,
                    visible ? "Visible" : "Invisible"
                ),
            };
            CadEditResult result = ApplyEdit(visible ? "BVSHOW" : "BVHIDE", records, s =>
            {
                DxfBlockTableRecord block = s.Database.BlockTable[blockName];
                DxfBlockVisibility.SetVisible(
                    block,
                    handles.Select(h => FindEntity(h)).Where(e => e != null),
                    state,
                    visible
                );
            });

            if (result.Succeeded)
            {
                // The editor shows the state again, as the entities it hides or shows have changed.
                _displays.Remove(editor.Space);
                _indexes.Remove(editor.Space);
                _stale.Remove(editor.Space);
            }

            return result;
        }

        /// <summary>
        /// Erases parameters and actions of the block open, as ERASE does in AutoCAD's Block Editor: a parameter with its grips and
        /// the actions that follow it, an action alone.
        /// </summary>
        /// <param name="handles">The handles of the elements.</param>
        /// <returns>What came of it: refused where the editor is not open on a block, a handle is of no element, or a parameter is
        /// an input of a lookup table or a block properties table.</returns>
        internal CadEditResult EraseBlockElements(IReadOnlyList<string> handles)
        {
            CadBlockEditor editor = _blockEditor;

            if (editor == null || editor.IsCurrentDrawing)
            {
                return CadEditResult.Refuse("The Block Editor is not open on a block.");
            }

            string blockName = editor.Name;
            var wanted = new List<string>();

            foreach (string handle in handles)
            {
                if (editor.ElementOf(handle) == null)
                {
                    return CadEditResult.Refuse("The block has no parameter or action " + handle + ".");
                }

                wanted.Add(handle);
            }

            if (wanted.Count == 0)
            {
                return CadEditResult.Done(Array.Empty<CadEditRecord>());
            }

            return ApplyEdit(
                "ERASE",
                new[]
                {
                    new CadEditRecord(
                        CadEditRecordKind.TableChanged,
                        null,
                        "Erase parameters and actions",
                        string.Join(",", wanted),
                        null
                    ),
                },
                s =>
                {
                    DxfBlockTableRecord block = s.Database.BlockTable[blockName];

                    foreach (string handle in wanted)
                    {
                        // An element already gone with its parameter is passed over.
                        DxfBlockElement element = CadBlockAuthoring.Find(block, handle);

                        if (element != null)
                        {
                            DxfDynamicBlockWriter.Erase(block, element.Node);
                        }
                    }
                }
            );
        }
    }
}
