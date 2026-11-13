using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The clipboard commands as AutoCAD's: COPYCLIP (Ctrl+C), CUTCLIP (Ctrl+X) and COPYBASE (Ctrl+Shift+C) put the objects
    /// selected on the session's clipboard (<see cref="CadCommandSession.Clipboard"/>), PASTECLIP (Ctrl+V) pastes them at an
    /// insertion point, PASTEBLOCK (Ctrl+Shift+V) as one block, PASTEORIG where they were, and PASTESPEC asks the window how.
    /// </summary>
    /// <remarks>
    /// The objects go as a small DXF of their own (<see cref="CadClipboardData"/>): copied with the layers, linetypes, text
    /// and dimension styles and blocks they need, as AutoCAD's WBLOCK copies them, its base point (<c>$INSBASE</c>) the
    /// lower left corner of their extents, or the point COPYBASE was given. Pasted, they bring those along where the drawing
    /// has none of the name, and take the drawing's own where it has one, as AutoCAD keeps the target's definitions: a line
    /// on a layer Steel of colour 1 pasted into a drawing whose Steel is of colour 5 is drawn in 5. Text another app copied
    /// is pasted as an MTEXT. Each paste and each cut is one undo step; a copy changes nothing.
    /// </remarks>
    internal static partial class CadCommands
    {
        private static readonly ConditionalWeakTable<CadDocument, string> Sources =
            new ConditionalWeakTable<CadDocument, string>();

        /// <summary>What tells a document open from any other on the clipboard: made once for it while it is open.</summary>
        internal static string SourceOf(CadDocument document) =>
            Sources.GetValue(document, d => Guid.NewGuid().ToString("N"));

        /// <summary>
        /// The objects of a drawing copied as the clipboard holds them: a DXF of the drawing's version with copies of them and
        /// what they need, its base point the point given or the lower left corner of their extents, and the words of the
        /// TEXTs and MTEXTs among them as plain text, one a line.
        /// </summary>
        internal static CadClipboardData CopyToClipboard(
            CadDocument document,
            IReadOnlyList<string> handles,
            GeoPoint2? basePoint
        )
        {
            var entities = new List<DxfEntity>();
            var words = new List<string>();
            GeoAabb3 box = GeoAabb3.Empty;

            foreach (string handle in handles)
            {
                DxfEntity entity = document.FindEntity(handle);

                if (entity == null || (entity is DxfViewport viewport && viewport.Number == 1))
                {
                    continue;
                }

                entities.Add(entity);

                try
                {
                    box = box.Union(entity.GeometricExtents);
                }
                catch (Exception)
                {
                    // An object with no extents brings no corner.
                }

                if (entity is DxfMText mtext)
                {
                    words.Add(mtext.Text);
                }
                else if (entity is DxfDBText text && !(entity is DxfAttributeDefinition))
                {
                    words.Add(text.TextString);
                }
            }

            if (entities.Count == 0)
            {
                return null;
            }

            var fragment = new DxfDatabase(document.Database.Version);
            DxfBlockTableRecord into = fragment.ModelSpace;

            // The viewports of a layout go with what is copied beside them into a layout of the fragment, as model space holds none.
            if (entities.Exists(e => e is DxfViewport))
            {
                DxfLayout sheet = fragment.Layouts.FirstOrDefault(l => l.LayoutName == "Layout1");

                if (sheet != null)
                {
                    sheet.Initialize();
                    into = sheet.BlockTableRecord;
                }
            }

            document.Database.WblockCloneObjects(entities, into);
            GeoPoint2 corner = basePoint ?? (box.IsEmpty ? GeoPoint2.Origin : new GeoPoint2(box.Min.X, box.Min.Y));
            fragment.Insbase = new GeoPoint3(corner.X, corner.Y, 0.0);
            string plain = words.Count == 0 ? null : string.Join("\r\n", words);
            return new CadClipboardData(
                fragment.ToText(),
                plain,
                SourceOf(document),
                document.Title,
                document.WorkingSpace.Name,
                entities.Count
            );
        }

        private static IEnumerable<CadPrompt> CopyClip(CadCommandContext c) => Copying(c, false, false);

        private static IEnumerable<CadPrompt> CutClip(CadCommandContext c) => Copying(c, true, false);

        private static IEnumerable<CadPrompt> CopyBase(CadCommandContext c) => Copying(c, false, true);

        /// <summary>COPYCLIP, CUTCLIP and COPYBASE: the base point first for COPYBASE, then the objects, as AutoCAD asks them.</summary>
        private static IEnumerable<CadPrompt> Copying(CadCommandContext c, bool cut, bool askBase)
        {
            GeoPoint2? basePoint = null;

            if (askBase)
            {
                yield return CadPrompt.ForPoint("Specify base point");

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                basePoint = c.Input.Point;
            }

            foreach (CadPrompt prompt in c.SelectObjects())
            {
                yield return prompt;
            }

            if (c.Selected.Count == 0)
            {
                yield break;
            }

            CadClipboardData data;

            try
            {
                data = CopyToClipboard(c.Document, c.Selected, basePoint);

                if (data != null)
                {
                    c.Session.Clipboard.SetData(data);
                }
            }
            catch (Exception exception) when (!(exception is OutOfMemoryException))
            {
                GeometryHelperLog.Warn(
                    "CadCommands: " + c.Command.Name + " could not put the objects on the Clipboard.",
                    exception
                );
                c.Write("The objects could not be copied to the Clipboard: " + exception.Message);
                yield break;
            }

            if (cut && data != null)
            {
                EraseAll(c, c.Selected);
            }
        }

        private static IEnumerable<CadPrompt> PasteClip(CadCommandContext c) => Pasting(c, PasteMode.Clip);

        private static IEnumerable<CadPrompt> PasteBlock(CadCommandContext c) => Pasting(c, PasteMode.Block);

        private static IEnumerable<CadPrompt> PasteOrig(CadCommandContext c) => Pasting(c, PasteMode.Original);

        /// <summary>
        /// PASTESPEC: the window's Paste Special dialog, then the paste it chose, as PASTECLIP, PASTEBLOCK or PASTEORIG, or the
        /// Clipboard's text as an MTEXT (TEXT), or its picture as an OLE object (PICTURE); nothing where it was cancelled.
        /// </summary>
        private static IEnumerable<CadPrompt> PasteSpecial(CadCommandContext c)
        {
            CadUiRequestEventArgs asked = c.Session.Ask("PASTESPEC");

            if (!asked.Handled)
            {
                c.Write(
                    "PASTESPEC is shown by the window: PASTECLIP, PASTEBLOCK and PASTEORIG paste from the command line."
                );
                yield break;
            }

            IEnumerable<CadPrompt> paste;

            switch (asked.Answer)
            {
                case "PASTECLIP":
                    paste = Pasting(c, PasteMode.Clip);
                    break;
                case "PASTEBLOCK":
                    paste = Pasting(c, PasteMode.Block);
                    break;
                case "PASTEORIG":
                    paste = Pasting(c, PasteMode.Original);
                    break;
                case "TEXT":
                    CadClipboardData data = c.Session.Clipboard.GetData();

                    if (data?.Text == null)
                    {
                        c.Write("The Clipboard holds no text.");
                        yield break;
                    }

                    paste = PasteText(c, data.Text);
                    break;
                case "PICTURE":
                    CadClipboardData held = c.Session.Clipboard.GetData();

                    if (held?.Picture == null)
                    {
                        c.Write("The Clipboard holds no picture.");
                        yield break;
                    }

                    CadOleFrames.TryInsert(c, held.Picture);
                    yield break;
                default:
                    yield break;
            }

            foreach (CadPrompt prompt in paste)
            {
                yield return prompt;
            }
        }

        private enum PasteMode
        {
            Clip,
            Block,
            Original,
        }

        private static IEnumerable<CadPrompt> Pasting(CadCommandContext c, PasteMode mode)
        {
            CadClipboardData data;

            try
            {
                data = c.Session.Clipboard.GetData();
            }
            catch (Exception exception) when (!(exception is OutOfMemoryException))
            {
                GeometryHelperLog.Warn("CadCommands: " + c.Command.Name + " could not read the Clipboard.", exception);
                data = null;
            }

            if (data == null)
            {
                c.Write("The Clipboard holds nothing to paste.");
                yield break;
            }

            if (!data.HasObjects)
            {
                // As AutoCAD pastes it: words as multiline text before a picture, which is an OLE object where it comes alone.
                bool words = data.Text != null;

                if (mode != PasteMode.Clip)
                {
                    c.Write(
                        words
                            ? "The Clipboard holds text, not drawing objects: PASTECLIP pastes it as multiline text."
                            : "The Clipboard holds a picture, not drawing objects: PASTECLIP pastes it as an OLE object."
                    );
                    yield break;
                }

                if (!words)
                {
                    CadOleFrames.TryInsert(c, data.Picture);
                    yield break;
                }

                foreach (CadPrompt prompt in PasteText(c, data.Text))
                {
                    yield return prompt;
                }

                yield break;
            }

            DxfDatabase fragment;

            try
            {
                fragment = DxfDatabase.Parse(data.Drawing);
            }
            catch (Exception exception) when (!(exception is OutOfMemoryException))
            {
                GeometryHelperLog.Warn("CadCommands: the objects on the Clipboard could not be read.", exception);
                c.Write("The objects on the Clipboard could not be read: " + exception.Message);
                yield break;
            }

            GeoPoint3 basePoint = fragment.Insbase;
            GeoPoint2 at = new GeoPoint2(basePoint.X, basePoint.Y);

            if (mode == PasteMode.Original)
            {
                if (string.Equals(data.Source, SourceOf(c.Document), StringComparison.Ordinal))
                {
                    c.Write("PASTEORIG pastes objects copied from another drawing: these were copied from this one.");
                    yield break;
                }
            }
            else
            {
                // The objects follow the cursor by their base point, as they are put at the point picked.
                yield return CadPrompt
                    .ForPoint("Specify insertion point")
                    .Previewing(
                        CadPreview.Bringing(c, fragment, input =>
                            Affine2.Translation(input.Point.X - basePoint.X, input.Point.Y - basePoint.Y)
                        )
                    );

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                at = c.Input.Point;
            }

            List<DxfEntity> held = Held(fragment);

            if (mode == PasteMode.Block && held.Exists(e => e is DxfViewport))
            {
                c.Write("Viewports cannot be pasted as a block: PASTECLIP pastes them in a layout.");
                yield break;
            }

            if (mode == PasteMode.Block)
            {
                c.Apply(null, s =>
                {
                    string name = FreeBlockName(s.Database, data.Drawing);
                    DxfBlockTableRecord block = s.Database.Insert(name, fragment, true);
                    s.Append(c.Space, Current(c, new DxfBlockReference(P3(at), block)));
                });
                yield break;
            }

            var move = new GeoVector3(at.X - basePoint.X, at.Y - basePoint.Y, 0.0);
            c.Apply(null, s =>
            {
                // A layout never shown has no viewport 1 for its sheet yet; AutoCAD gives it one before the first viewport it gets
                // (the file of a layout without one made AUDIT say "Invalid linetype eNullObjectId changed to ByLayer").
                if (held.Exists(e => e is DxfViewport) && c.Space.Layout != null)
                {
                    int before = c.Space.Layout.GetViewports().Count;
                    DxfViewport sheet = c.Space.Layout.Initialize();

                    if (c.Space.Layout.GetViewports().Count > before)
                    {
                        s.Added(c.Space, sheet);
                    }
                }

                IReadOnlyList<DxfEntity> copies = fragment.WblockCloneObjects(held, c.Space.Block);
                GeoTransform3 transform = move.X == 0.0 && move.Y == 0.0 ? null : GeoTransform3.Translation(move);

                foreach (DxfEntity copy in copies)
                {
                    // An MTEXT copied into a drawing of R12 is written as TEXTs and is in no space itself.
                    if (string.IsNullOrEmpty(copy.Handle) || copy.IsErased)
                    {
                        continue;
                    }

                    if (transform != null)
                    {
                        copy.TransformBy(transform);
                    }

                    s.Added(c.Space, copy);
                }
            });
        }

        /// <summary>
        /// The objects a clipboard's drawing holds to paste: those of its model space, or, where that holds none, those of its
        /// layout (a copy that held viewports), without the viewport that shows the layout itself.
        /// </summary>
        private static List<DxfEntity> Held(DxfDatabase fragment)
        {
            var held = new List<DxfEntity>(fragment.ModelSpace);
            DxfBlockTableRecord sheet = fragment
                .Layouts.FirstOrDefault(l => l.LayoutName == "Layout1")
                ?.BlockTableRecord;

            if (held.Count == 0 && sheet != null)
            {
                foreach (DxfEntity entity in sheet)
                {
                    if (!entity.IsErased && !(entity is DxfViewport viewport && viewport.Number == 1))
                    {
                        held.Add(entity);
                    }
                }
            }

            return held;
        }

        /// <summary>Text of another app pasted as an MTEXT at the point given, in the current text style and height, its lines its paragraphs.</summary>
        private static IEnumerable<CadPrompt> PasteText(CadCommandContext c, string text)
        {
            string style = TextStyle(c);
            DxfTextStyleTableRecord record = StyleRecord(c, style);
            double height =
                record != null && record.TextSize > 0.0 ? record.TextSize : c.Remembered("text.height", TextSize(c));
            string words = MTextOfPlain(text);

            DxfEntity Made(GeoPoint2 at)
            {
                if (words.Length == 0)
                {
                    return null;
                }

                var mtext = new DxfMText
                {
                    Location = P3(at),
                    TextHeight = height,
                    Width = 0.0,
                    Attachment = DxfAttachmentPoint.TopLeft,
                    Contents = words,
                };

                if (style != null)
                {
                    mtext.TextStyleName = style;
                }

                return Current(c, mtext);
            }

            yield return CadPrompt
                .ForPoint("Specify insertion point")
                .Previewing(CadPreview.MakingOne(c, input => Made(input.Point)));

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            DxfEntity made = Made(c.Input.Point);

            if (made == null)
            {
                c.Write("The Clipboard's text is empty.");
                yield break;
            }

            c.Apply(null, s => s.Append(c.Space, made));
        }

        /// <summary>Plain text as an MTEXT's words: its backslashes and braces escaped, each line a paragraph (<c>\P</c>), the blank lines at the end left out.</summary>
        internal static string MTextOfPlain(string text)
        {
            string[] lines = (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').TrimEnd('\n').Split('\n');
            var builder = new StringBuilder();

            for (int i = 0; i < lines.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append("\\P");
                }

                builder.Append(CadMTextContent.Escape(lines[i].Replace('\t', ' ')));
            }

            return builder.ToString();
        }

        /// <summary>The name of a block PASTEBLOCK makes, as AutoCAD names one: A$C and eight hexadecimal digits, the first of a number from the objects that the drawing has no block of.</summary>
        private static string FreeBlockName(DxfDatabase database, string drawing)
        {
            // FNV-1a of the objects' DXF: the same objects give the same name, so that a test reads it, and the next number where it is taken.
            uint hash = 2166136261;

            foreach (char c in drawing)
            {
                hash = (hash ^ c) * 16777619;
            }

            while (true)
            {
                string name = "A$C" + hash.ToString("X8", CultureInfo.InvariantCulture);

                if (!database.BlockTable.Has(name))
                {
                    return name;
                }

                hash++;
            }
        }
    }
}
