using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// TEXTEDIT (ED, DDEDIT; a double click on a text) and MTEDIT, as AutoCAD 2021's: a TEXT or a dimension's text edited in
    /// a box over it on the canvas, an MTEXT in the MTEXT editor, each through a prompt the window answers from its editor
    /// (<see cref="CadPrompt.InPlace"/>), a TOLERANCE in the Geometric Tolerance dialog filled from its words. TEXTEDIT asks for the next object until Enter on nothing, unless its Mode is Single;
    /// each text changed is one undo step, which its Undo takes back. The attributes of a block, and an attribute definition,
    /// are edited in Properties.
    /// </summary>
    internal static partial class CadCommands
    {
        private static IEnumerable<CadPrompt> TextEdit(CadCommandContext c) => Editing(c, false);

        private static IEnumerable<CadPrompt> MTextEdit(CadCommandContext c) => Editing(c, true);

        private static IEnumerable<CadPrompt> Editing(CadCommandContext c, bool mtextOnly)
        {
            bool single = mtextOnly || c.Remembered("textedit.single", 0.0) != 0.0;

            if (!mtextOnly)
            {
                c.Write("Current settings: Edit mode = " + (single ? "Single" : "Multiple"));
            }

            int made = 0;

            while (true)
            {
                if (mtextOnly)
                {
                    yield return CadPrompt.ForEntity("Select an MTEXT object");
                }
                else
                {
                    yield return CadPrompt.ForEntity("Select an annotation object", new[] { "Undo", "Mode" });
                }

                if (c.Input.Is("Undo"))
                {
                    if (made > 0 && c.Document.Undo())
                    {
                        made--;
                    }
                    else
                    {
                        c.Write("Everything has been undone.");
                    }

                    continue;
                }

                if (c.Input.Is("Mode"))
                {
                    yield return CadPrompt.ForKeyword(
                        "Enter a text edit mode option",
                        new[] { "Single", "Multiple" },
                        single ? "Single" : "Multiple"
                    );

                    if (c.Input.Is("Single") || c.Input.Is("Multiple"))
                    {
                        single = c.Input.Is("Single");
                        c.Remember("textedit.single", single ? 1.0 : 0.0);
                    }

                    continue;
                }

                if (c.Input.Kind != CadInputKind.Selection)
                {
                    yield break;
                }

                string handle = c.Input.Handles[0];
                DxfEntity entity = c.Document.FindEntity(handle);

                // A feature control frame is edited in the Geometric Tolerance dialog, as AutoCAD 2021 edits one.
                if (!mtextOnly && entity is DxfTolerance frame)
                {
                    if (EditFrame(c, frame))
                    {
                        made++;
                    }

                    if (single)
                    {
                        yield break;
                    }

                    continue;
                }

                CadInPlaceEdit edit = InPlaceOf(entity, mtextOnly);

                if (edit == null)
                {
                    if (!mtextOnly && (entity is DxfBlockReference || entity is DxfAttributeDefinition))
                    {
                        // The attributes of a block and an attribute definition have their editor in Properties.
                        c.Document.Selection.Set(new[] { handle });

                        if (!c.Session.Request("PROPERTIES"))
                        {
                            c.Write("Properties edits the attributes: select the object and open it.");
                        }
                    }
                    else
                    {
                        c.Write(
                            mtextOnly
                                ? "The object selected is not an MTEXT."
                                : "The object selected is not a text, an MTEXT or a dimension."
                        );
                    }

                    if (single)
                    {
                        yield break;
                    }

                    continue;
                }

                yield return CadPrompt.ForInPlaceEdit(
                    edit.Kind == CadInPlaceKind.MText ? "Edit multiline text" : "Edit text",
                    edit
                );

                if (c.Input.Kind == CadInputKind.Text && WriteEdit(c, entity, edit, c.Input.Text))
                {
                    made++;
                }

                if (single)
                {
                    yield break;
                }
            }
        }

        /// <summary>What of an object is edited in place: a TEXT's value, an MTEXT's words, a dimension's text override (<c>&lt;&gt;</c> the measurement); null for anything else.</summary>
        private static CadInPlaceEdit InPlaceOf(DxfEntity entity, bool mtextOnly)
        {
            switch (entity)
            {
                case DxfMText mtext:
                    return new CadInPlaceEdit(
                        CadInPlaceKind.MText,
                        mtext.Handle,
                        mtext.Contents,
                        P2(mtext.Location),
                        mtext.TextHeight,
                        mtext.Rotation,
                        mtext.Width,
                        mtext.TextStyleName,
                        (int)mtext.Attachment
                    );
                case DxfAttributeDefinition _:
                    return null;
                case DxfDBText text when !mtextOnly:
                    return new CadInPlaceEdit(
                        CadInPlaceKind.Text,
                        text.Handle,
                        text.TextString,
                        P2(text.Position),
                        text.Height,
                        text.Rotation,
                        0.0,
                        text.TextStyleName,
                        1
                    );
                case DxfDimension dimension when !mtextOnly:
                    string value = string.IsNullOrEmpty(dimension.DimensionText) ? "<>" : dimension.DimensionText;
                    GeoPoint3 at = dimension.TextPosition;
                    return new CadInPlaceEdit(
                        CadInPlaceKind.Text,
                        dimension.Handle,
                        value,
                        P2(at),
                        DimensionTextHeight(dimension),
                        dimension.TextRotation,
                        0.0,
                        null,
                        5
                    );
                default:
                    return null;
            }
        }

        /// <summary>
        /// Edits a feature control frame in the Geometric Tolerance dialog, as AutoCAD 2021's TEXTEDIT and a double click edit
        /// one: the dialog filled from the frame's words (<see cref="CadToleranceRequest.Read"/>), OK writing those it ends
        /// with, one undo step; false where nothing was written: Cancel, the words unchanged or every box left empty, the
        /// frame on a locked layer, or no window to show the dialog.
        /// </summary>
        private static bool EditFrame(CadCommandContext c, DxfTolerance frame)
        {
            string handle = frame.Handle;
            string was = frame.Text;
            var request = new CadToleranceRequest(c.Document);
            request.Read(was);

            if (!c.Session.Request("TOLERANCE", c.Document, request))
            {
                c.Write("Geometric Tolerance is shown by the window: the frame is not edited without it.");
                return false;
            }

            string text = request.Text;

            if (
                request.Action != CadDialogAction.Ok
                || text.Length == 0
                || string.Equals(text, was, StringComparison.Ordinal)
            )
            {
                return false;
            }

            if (CadLayerEditor.IsLocked(c.Database, frame.Layer))
            {
                c.Write("The " + CadDocument.KindName(frame) + " " + handle + " is on a locked layer.");
                return false;
            }

            var planned = new[] { new CadEditRecord(CadEditRecordKind.PropertyChanged, handle, "Text", was, text) };
            CadEditResult result = c.Apply(planned, s =>
            {
                if (!s.IsHandled(handle))
                {
                    ((DxfTolerance)s.Document.FindEntity(handle)).Text = text;
                }
            });

            if (!result.Succeeded)
            {
                c.Write(result.Message);
            }

            return result.Succeeded;
        }

        /// <summary>Writes the words an edit ended with, one undo step, where they differ from what was there; false where nothing was written.</summary>
        private static bool WriteEdit(CadCommandContext c, DxfEntity entity, CadInPlaceEdit edit, string words)
        {
            bool boxChanged = edit.Kind == CadInPlaceKind.MText && edit.ChangesBox;

            if ((string.Equals(words, edit.Value, StringComparison.Ordinal) && !boxChanged) || words.Length == 0)
            {
                return false;
            }

            c.OpenUndoStep();
            bool done = true;

            if (!string.Equals(words, edit.Value, StringComparison.Ordinal))
            {
                string label = entity is DxfDimension ? "Text override" : "Contents";
                string value = entity is DxfDimension && words == "<>" ? string.Empty : words;
                CadEditResult result = CadPropertyEditor.Set(c.Document, new[] { edit.Handle }, label, value);

                if (!result.Succeeded)
                {
                    c.Write(result.Message);
                    done = false;
                }
            }

            if (done && boxChanged)
            {
                var mtext = (DxfMText)entity;
                var planned = new List<CadEditRecord>();

                if (edit.NewWidth.HasValue && edit.NewWidth.Value >= 0.0 && edit.NewWidth.Value != edit.Width)
                {
                    planned.Add(
                        new CadEditRecord(
                            CadEditRecordKind.PropertyChanged,
                            edit.Handle,
                            "Width",
                            Values(edit.Width),
                            Values(edit.NewWidth.Value)
                        )
                    );
                }

                if (edit.NewAttachment.HasValue)
                {
                    planned.Add(
                        new CadEditRecord(
                            CadEditRecordKind.PropertyChanged,
                            edit.Handle,
                            "Attachment",
                            ((int)mtext.Attachment).ToString(System.Globalization.CultureInfo.InvariantCulture),
                            edit.NewAttachment.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        )
                    );
                }

                if (edit.NewLineSpacing.HasValue)
                {
                    planned.Add(
                        new CadEditRecord(
                            CadEditRecordKind.PropertyChanged,
                            edit.Handle,
                            "Line space factor",
                            Values(mtext.LineSpacingFactor),
                            Values(edit.NewLineSpacing.Value)
                        )
                    );
                }

                if (edit.NewFrame != null)
                {
                    planned.Add(
                        new CadEditRecord(CadEditRecordKind.PropertyChanged, edit.Handle, "Columns", null, null)
                    );
                }

                string refused = null;
                CadEditResult result = c.Apply(planned, s =>
                {
                    if (!s.IsHandled(edit.Handle))
                    {
                        var target = (DxfMText)s.Document.FindEntity(edit.Handle);
                        ApplyBox(target, edit);
                        refused = ApplyFrame(target, edit);
                    }
                });

                if (refused != null)
                {
                    c.Write(refused);
                }

                done = result.Succeeded;
            }

            c.CloseUndoStep();
            return done;
        }

        /// <summary>Gives an MTEXT the width, attachment and line spacing its editor set, the attachment point kept where it is.</summary>
        internal static void ApplyBox(DxfMText mtext, CadInPlaceEdit edit)
        {
            if (edit.NewWidth.HasValue && edit.NewWidth.Value >= 0.0)
            {
                mtext.Width = edit.NewWidth.Value;
            }

            if (edit.NewAttachment.HasValue && edit.NewAttachment.Value >= 1 && edit.NewAttachment.Value <= 9)
            {
                mtext.Attachment = (DxfAttachmentPoint)edit.NewAttachment.Value;
            }

            if (edit.NewLineSpacing.HasValue && edit.NewLineSpacing.Value >= 0.25 && edit.NewLineSpacing.Value <= 4.0)
            {
                mtext.LineSpacingFactor = edit.NewLineSpacing.Value;
            }
        }

        /// <summary>
        /// Gives an MTEXT the defined height and columns its editor set (<see cref="CadInPlaceEdit.NewFrame"/>), as AutoCAD's
        /// own calls set them; the message of a file that keeps no such columns, null where all was set.
        /// </summary>
        internal static string ApplyFrame(DxfMText mtext, CadInPlaceEdit edit)
        {
            if (edit.NewFrame == null)
            {
                return null;
            }

            try
            {
                edit.NewFrame.ApplyTo(mtext);
                return null;
            }
            catch (InvalidOperationException exception)
            {
                GeometryHelperLog.Warn("CadCommands: the MTEXT editor's columns were not written.", exception);
                return "The columns were not written: " + exception.Message;
            }
        }

        /// <summary>The height of a dimension's text as its style gives it, for the edit box: 2.5 where it gives none.</summary>
        private static double DimensionTextHeight(DxfDimension dimension)
        {
            try
            {
                foreach (DxfEntity part in dimension.DimBlock)
                {
                    if (part is DxfMText mtext && mtext.TextHeight > 0.0)
                    {
                        return mtext.TextHeight;
                    }
                }
            }
            catch (Exception)
            {
                // A dimension with no block to read gives the default.
            }

            return 2.5;
        }
    }
}
