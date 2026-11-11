using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The Block Editor's commands, in AutoCAD 2021's words: BEDIT and -BEDIT open it, BSAVE and BSAVEAS save the block,
    /// BCLOSE closes it, asking whether to save where it has changes. The work is the document's
    /// (<see cref="CadDocument.OpenBlockEditor"/> and the rest); these ask the dialogs and the questions.
    /// </summary>
    internal static partial class CadCommands
    {
        // The commands that leave the block or change another space, which the Block Editor refuses, as the design's notes list them.
        private static readonly HashSet<string> LeaveBlock = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "BEDIT",
            "-BEDIT",
            "XREF",
            "-XREF",
            "XATTACH",
            "IMAGEATTACH",
            "EXTERNALREFERENCES",
            "LAYOUT",
            "PLOT",
            "-PLOT",
            "PREVIEW",
            "PAGESETUP",
            "MSPACE",
            "PSPACE",
            "MVIEW",
            "QSAVE",
            "SAVEAS",
        };

        internal static void AddBlockEditorTo(CadCommandRegistry registry)
        {
            registry.Add(new CadCommand("BEDIT", BlockEdit, true));
            registry.Add(new CadCommand("-BEDIT", BlockEditTyped));
            registry.Add(new CadCommand("BSAVE", BlockSave));
            registry.Add(new CadCommand("BSAVEAS", BlockSaveAs));
            registry.Add(new CadCommand("BCLOSE", BlockClose));
            registry.Add(
                new CadCommand("BAUTHORPALETTE", c => Say(c, "The Block Authoring Palettes are not in this version."))
            );
        }

        /// <summary>Whether the Block Editor refuses a command, "Not allowed in the Block Editor.": BEDIT again, XREF, LAYOUT, PLOT and the others that leave the block.</summary>
        internal static bool LeavesBlockEditor(string name) => name != null && LeaveBlock.Contains(name);

        /// <summary>
        /// BEDIT (BE): the Edit Block Definition dialog, the block of an INSERT selected first offered; OK opens the block chosen,
        /// a new name a new empty block, &lt;Current Drawing&gt; model space. With no window, -BEDIT's prompt.
        /// </summary>
        private static IEnumerable<CadPrompt> BlockEdit(CadCommandContext c)
        {
            string offered = c
                .PreSelected.Select(h => c.Document.FindEntity(h))
                .OfType<DxfBlockReference>()
                .Select(r => r.Name)
                .FirstOrDefault(n =>
                    NamedBlocks(c.Database).Exists(b => string.Equals(b.Name, n, StringComparison.OrdinalIgnoreCase))
                );
            c.ClearsSelection = c.PreSelected.Count > 0;
            var request = new CadBlockEditRequest(
                c.Document,
                "Edit Block Definition",
                offered ?? CadBlockEditor.CurrentDrawing
            );

            if (!c.Session.Request("BEDIT", c.Document, request))
            {
                foreach (CadPrompt prompt in BlockEditTyped(c))
                {
                    yield return prompt;
                }

                yield break;
            }

            if (request.Action == CadDialogAction.Ok)
            {
                OpenBlock(c, request.Name);
            }
        }

        /// <summary>-BEDIT: "Enter block name or [?]:", ? listing the blocks; the name opens the Block Editor.</summary>
        private static IEnumerable<CadPrompt> BlockEditTyped(CadCommandContext c)
        {
            while (true)
            {
                yield return CadPrompt.ForKeywordOrText("Enter block name or", new[] { "?" }, null);

                if (c.Input.Is("?"))
                {
                    WriteBlockList(c);
                    continue;
                }

                if (c.Input.Kind != CadInputKind.Text)
                {
                    yield break;
                }

                OpenBlock(c, c.Input.Text.Trim());
                yield break;
            }
        }

        /// <summary>Opens the Block Editor on a block, saying what it shows or why it cannot.</summary>
        private static void OpenBlock(CadCommandContext c, string name)
        {
            CadEditResult result = c.Document.OpenBlockEditor(name ?? string.Empty);

            if (!result.Succeeded)
            {
                c.Write(result.Message);
                return;
            }

            CadBlockEditor editor = c.Document.BlockEditor;
            int count = editor.Space.Block.Count(e => !e.IsErased);
            c.Write(
                "Block Editor opened: "
                    + editor.Name
                    + " ("
                    + count.ToString(CultureInfo.InvariantCulture)
                    + (count == 1 ? " object)" : " objects)")
            );
        }

        /// <summary>BSAVE (BS): the block saved, every INSERT of it drawn anew.</summary>
        private static IEnumerable<CadPrompt> BlockSave(CadCommandContext c)
        {
            CadEditResult result = c.Document.SaveBlock();

            if (!result.Succeeded)
            {
                c.Write(result.Message);
            }

            yield break;
        }

        /// <summary>
        /// BSAVEAS: the new name asked in the Edit Block Definition window titled Save Block As, or with no window "Enter block
        /// name:"; the content saved as that block, the one opened left as it was opened or last saved.
        /// </summary>
        private static IEnumerable<CadPrompt> BlockSaveAs(CadCommandContext c)
        {
            if (c.Document.BlockEditor == null)
            {
                c.Write("The Block Editor is not open.");
                yield break;
            }

            var request = new CadBlockEditRequest(c.Document, "Save Block As", string.Empty);
            string name;

            if (c.Session.Request("BSAVEAS", c.Document, request))
            {
                if (request.Action != CadDialogAction.Ok)
                {
                    yield break;
                }

                name = request.Name;
            }
            else
            {
                yield return CadPrompt.ForString("Enter block name", null);

                if (c.Input.Kind != CadInputKind.Text || c.Input.Text.Trim().Length == 0)
                {
                    yield break;
                }

                name = c.Input.Text;
            }

            CadEditResult result = c.Document.SaveBlockAs(name ?? string.Empty);

            if (!result.Succeeded)
            {
                c.Write(result.Message);
            }
        }

        /// <summary>
        /// BCLOSE (BC): closes the Block Editor; where the block has changes not saved, the window's question "Save changes to
        /// DOOR_900?" (Save, Discard, Cancel), or with no window the same on the command line, Save by default.
        /// </summary>
        private static IEnumerable<CadPrompt> BlockClose(CadCommandContext c)
        {
            CadBlockEditor editor = c.Document.BlockEditor;

            if (editor == null)
            {
                c.Write("The Block Editor is not open.");
                yield break;
            }

            bool save = false;

            if (editor.HasUnsavedChanges)
            {
                CadUiRequestEventArgs asked = c.Session.Ask("BCLOSE", c.Document, editor.Name);
                string answer = asked.Answer;

                if (!asked.Handled)
                {
                    yield return CadPrompt.ForKeyword(
                        "Save changes to " + editor.Name + "?",
                        new[] { "Save", "Discard", "Cancel" },
                        "Save"
                    );
                    answer =
                        c.Input.Is("Save") ? "Save"
                        : c.Input.Is("Discard") ? "Discard"
                        : null;
                }

                if (answer != "Save" && answer != "Discard")
                {
                    yield break;
                }

                save = answer == "Save";
            }

            CadEditResult result = c.Document.CloseBlockEditor(save);

            if (!result.Succeeded)
            {
                c.Write(result.Message);
            }
        }

        private static IEnumerable<CadPrompt> Say(CadCommandContext c, string line)
        {
            c.Write(line);
            yield break;
        }
    }
}
