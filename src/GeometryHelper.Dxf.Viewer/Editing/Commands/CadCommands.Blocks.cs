using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The block commands, in AutoCAD's words: BLOCK and -BLOCK, INSERT and -INSERT, WBLOCK and -WBLOCK, ATTDEF and -ATTDEF,
    /// EATTEDIT, ATTEDIT and -ATTEDIT, and BASE. A dialog of the window is asked through a request the command and the window
    /// share (<see cref="CadBlockDefinitionRequest"/> and the others); with no window the typed form runs. Each run is one
    /// undo step; WBLOCK's file, once written, is not taken back, as AutoCAD's.
    /// </summary>
    internal static partial class CadCommands
    {
        private static readonly string[] InsertKeywords = { "Basepoint", "Scale", "X", "Y", "Z", "Rotate" };

        private static readonly KeyValuePair<string, DxfAttachmentPoint>[] Justifications =
        {
            new KeyValuePair<string, DxfAttachmentPoint>("Left", DxfAttachmentPoint.BaseLeft),
            new KeyValuePair<string, DxfAttachmentPoint>("Center", DxfAttachmentPoint.BaseCenter),
            new KeyValuePair<string, DxfAttachmentPoint>("Right", DxfAttachmentPoint.BaseRight),
            new KeyValuePair<string, DxfAttachmentPoint>("Middle", DxfAttachmentPoint.BaseMid),
            new KeyValuePair<string, DxfAttachmentPoint>("TL", DxfAttachmentPoint.TopLeft),
            new KeyValuePair<string, DxfAttachmentPoint>("TC", DxfAttachmentPoint.TopCenter),
            new KeyValuePair<string, DxfAttachmentPoint>("TR", DxfAttachmentPoint.TopRight),
            new KeyValuePair<string, DxfAttachmentPoint>("ML", DxfAttachmentPoint.MiddleLeft),
            new KeyValuePair<string, DxfAttachmentPoint>("MC", DxfAttachmentPoint.MiddleCenter),
            new KeyValuePair<string, DxfAttachmentPoint>("MR", DxfAttachmentPoint.MiddleRight),
            new KeyValuePair<string, DxfAttachmentPoint>("BL", DxfAttachmentPoint.BottomLeft),
            new KeyValuePair<string, DxfAttachmentPoint>("BC", DxfAttachmentPoint.BottomCenter),
            new KeyValuePair<string, DxfAttachmentPoint>("BR", DxfAttachmentPoint.BottomRight),
        };

        internal static void AddBlocksTo(CadCommandRegistry registry)
        {
            registry.Add(new CadCommand("BLOCK", Block, true));
            registry.Add(new CadCommand("-BLOCK", BlockTyped));
            registry.Add(new CadCommand("INSERT", Insert));
            registry.Add(new CadCommand("-INSERT", InsertTyped));
            registry.Add(new CadCommand("WBLOCK", WriteBlock, true));
            registry.Add(new CadCommand("-WBLOCK", WriteBlockTyped));
            registry.Add(new CadCommand("ATTDEF", AttributeDefinition));
            registry.Add(new CadCommand("-ATTDEF", AttributeDefinitionTyped));
            registry.Add(new CadCommand("EATTEDIT", c => AttributeEdit(c, "Select a block")));
            registry.Add(new CadCommand("ATTEDIT", c => AttributeEdit(c, "Select block reference")));
            registry.Add(new CadCommand("-ATTEDIT", AttributeEditTyped));
            registry.Add(new CadCommand("ATTSYNC", AttributeSync));
            registry.Add(new CadCommand("BASE", Base));
        }

        /// <summary>The justifications of a text by AutoCAD's keyword, Left to BR, as -ATTDEF's Justify offers them; Align and Fit are not among them.</summary>
        internal static IReadOnlyList<KeyValuePair<string, DxfAttachmentPoint>> TextJustifications => Justifications;

        /// <summary>
        /// BLOCK (B): the window's Block Definition dialog, shown again after each Pick point or Select objects asked on the
        /// command line; OK makes the block. With no window, -BLOCK's prompts.
        /// </summary>
        private static IEnumerable<CadPrompt> Block(CadCommandContext c)
        {
            var request = new CadBlockDefinitionRequest(c.Document) { Objects = Unlocked(c, c.PreSelected) };
            c.ClearsSelection = c.PreSelected.Count > 0;

            while (true)
            {
                request.Action = CadDialogAction.Cancel;

                if (!c.Session.Request("BLOCK", c.Document, request))
                {
                    foreach (CadPrompt prompt in BlockTyped(c))
                    {
                        yield return prompt;
                    }

                    yield break;
                }

                if (request.Action == CadDialogAction.PickPoint)
                {
                    yield return CadPrompt.ForPoint("Specify insertion base point");

                    if (c.Input.IsPoint)
                    {
                        request.BasePoint = P3(c.Input.Point);
                    }
                }
                else if (request.Action == CadDialogAction.SelectObjects)
                {
                    yield return CadPrompt.ForSelection();
                    request.Objects = Unlocked(c, c.Input.Handles);
                }
                else if (request.Action == CadDialogAction.Ok)
                {
                    break;
                }
                else
                {
                    yield break;
                }
            }

            if (request.SpecifyBasePointOnScreen)
            {
                yield return CadPrompt.ForPoint("Specify insertion base point");

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                request.BasePoint = P3(c.Input.Point);
            }

            if (request.SpecifyObjectsOnScreen)
            {
                yield return CadPrompt.ForSelection();
                request.Objects = Unlocked(c, c.Input.Handles);
            }

            MakeBlock(c, request);
        }

        /// <summary>-BLOCK (-B): "Enter block name or [?]:", the question of redefining, the base point and the objects, which are erased, as AutoCAD's -BLOCK erases them.</summary>
        private static IEnumerable<CadPrompt> BlockTyped(CadCommandContext c)
        {
            var request = new CadBlockDefinitionRequest(c.Document) { ObjectAction = CadBlockObjectAction.Delete };

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

                string name = c.Input.Text.Trim();

                if (!IsBlockName(name))
                {
                    c.Write("Invalid block name.");
                    continue;
                }

                request.Name = name;
                break;
            }

            if (c.Database.BlockTable.Has(request.Name))
            {
                yield return CadPrompt.ForKeyword("Block \"" + request.Name + "\" already exists. Redefine it?", new[] { "Yes", "No" }, "No");

                if (!c.Input.Is("Yes"))
                {
                    yield break;
                }

                request.Redefine = true;
            }

            yield return CadPrompt.ForPoint("Specify insertion base point");

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            request.BasePoint = P3(c.Input.Point);

            foreach (CadPrompt prompt in c.SelectObjects())
            {
                yield return prompt;
            }

            request.Objects = c.Selected;
            MakeBlock(c, request);
        }

        /// <summary>The names of the blocks of the drawing, as "?" lists them: those AutoCAD names itself and the layouts' left out.</summary>
        private static void WriteBlockList(CadCommandContext c)
        {
            List<string> names = NamedBlocks(c.Database).Select(b => b.Name).ToList();
            c.Write("Defined blocks.");
            names.ForEach(c.Write);
            c.Write("User blocks: " + names.Count.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>The blocks a user names, in ordinal order of their names: no layout, no anonymous block, no external reference.</summary>
        internal static List<DxfBlockTableRecord> NamedBlocks(DxfDatabase database)
        {
            var blocks = new List<DxfBlockTableRecord>();

            foreach (DxfBlockTableRecord block in database.BlockTable)
            {
                if (!block.IsLayout && !block.IsAnonymous && !block.IsFromExternalReference && !block.Name.StartsWith("*", StringComparison.Ordinal))
                {
                    blocks.Add(block);
                }
            }

            blocks.Sort((a, b) => string.CompareOrdinal(a.Name.ToUpperInvariant(), b.Name.ToUpperInvariant()) != 0
                ? string.CompareOrdinal(a.Name.ToUpperInvariant(), b.Name.ToUpperInvariant())
                : string.CompareOrdinal(a.Name, b.Name));
            return blocks;
        }

        /// <summary>Whether AutoCAD takes a name for a block: 1 to 255 letters, none of &lt;&gt;/\":;?*|,=` and not starting with a space.</summary>
        internal static bool IsBlockName(string name)
            => !string.IsNullOrEmpty(name) && name.Length <= 255 && name.Trim().Length == name.Length && name.IndexOfAny("<>/\\\":;?*|,=`".ToCharArray()) < 0;

        /// <summary>
        /// Makes the block a request describes, one edit: a new block, or the block of the name defined again where
        /// <see cref="CadBlockDefinitionRequest.Redefine"/> says so, holding copies of the objects at the coordinates they
        /// have, its origin the base point; the objects kept, erased, or erased and an INSERT of the block put where they were.
        /// </summary>
        internal static void MakeBlock(CadCommandContext c, CadBlockDefinitionRequest request)
        {
            string name = (request.Name ?? string.Empty).Trim();

            if (!IsBlockName(name))
            {
                c.Write("Invalid block name.");
                return;
            }

            bool exists = c.Database.BlockTable.Has(name);

            if (exists && (!request.Redefine || c.Database.BlockTable[name].IsLayout || c.Database.BlockTable[name].IsFromExternalReference))
            {
                c.Write("Block \"" + name + "\" already exists.");
                return;
            }

            var handles = new List<string>();

            foreach (string handle in request.Objects ?? Array.Empty<string>())
            {
                DxfEntity entity = c.Document.FindEntity(handle);

                if (entity == null)
                {
                    continue;
                }

                if (entity is DxfBlockReference reference && Places(c.Database, reference.Name, name, new HashSet<string>(StringComparer.OrdinalIgnoreCase)))
                {
                    c.Write("Block \"" + name + "\" references itself.");
                    return;
                }

                handles.Add(handle);
            }

            if (handles.Count == 0)
            {
                c.Write("No objects selected.");
            }

            var planned = new List<CadEditRecord> { new CadEditRecord(CadEditRecordKind.TableChanged, null, exists ? "Redefine block" : "New block", null, name) };

            if (request.ObjectAction != CadBlockObjectAction.Retain)
            {
                planned.AddRange(handles.Select(h => new CadEditRecord(CadEditRecordKind.Erased, h)));
            }

            CadSpace space = c.Space;
            GeoPoint3 origin = request.BasePoint;
            CadEditResult result = c.Apply(planned, s =>
            {
                DxfBlockTableRecord record;

                if (exists)
                {
                    record = s.Database.BlockTable[name];

                    foreach (DxfEntity old in record.ToList())
                    {
                        old.Erase();
                    }

                    record.Origin = origin;
                }
                else
                {
                    record = new DxfBlockTableRecord { Name = name, Origin = origin };
                    s.Database.BlockTable.Add(record);
                }

                // A drawing older than R2000 holds no description, one older than R2007 no unit, explodability or scaling, as
                // AutoCAD writes none there: what it cannot hold is left out, the block made all the same.
                Keep(() => record.Comments = request.Description ?? string.Empty, record.Comments != (request.Description ?? string.Empty));
                Keep(() => record.Units = request.Units, record.Units != request.Units);
                Keep(() => record.Explodable = request.AllowExploding, record.Explodable != request.AllowExploding);
                Keep(() => record.BlockScaling = request.ScaleUniformly ? DxfBlockScaling.Uniform : DxfBlockScaling.Any, record.BlockScaling != (request.ScaleUniformly ? DxfBlockScaling.Uniform : DxfBlockScaling.Any));
                List<DxfEntity> entities = handles.Select(h => s.Document.FindEntity(h)).ToList();

                if (entities.Count > 0)
                {
                    s.Database.WblockCloneObjects(entities, record);
                }

                if (request.ObjectAction == CadBlockObjectAction.Retain)
                {
                    return;
                }

                foreach (DxfEntity entity in entities)
                {
                    entity.Erase();
                }

                if (request.ObjectAction == CadBlockObjectAction.ConvertToBlock)
                {
                    var insert = new DxfBlockReference(origin, record);
                    s.Append(space, Current(c, insert));
                    AddAttributes(insert, record, null);
                }
            });

            if (result.Succeeded && !exists)
            {
                GeometryHelperLog.Info("CadCommands: the block " + name + " was made of " + handles.Count.ToString(CultureInfo.InvariantCulture) + " objects.");
            }
        }

        /// <summary>Sets a value of a block where it differs, leaving it out where the drawing's version holds none of it.</summary>
        private static void Keep(Action set, bool differs)
        {
            if (!differs)
            {
                return;
            }

            try
            {
                set();
            }
            catch (InvalidOperationException exception)
            {
                GeometryHelperLog.Debug("CadCommands: a value of a block was left out: " + exception.Message);
            }
        }

        /// <summary>Whether a block places another, itself or through the blocks it inserts, at any depth.</summary>
        private static bool Places(DxfDatabase database, string block, string target, HashSet<string> seen)
        {
            if (string.Equals(block, target, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!seen.Add(block) || !database.BlockTable.Has(block))
            {
                return false;
            }

            foreach (DxfEntity entity in database.BlockTable[block])
            {
                if (entity is DxfBlockReference inner && Places(database, inner.Name, target, seen))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The handles of entities not on a locked layer, in ordinal order, saying how many were.</summary>
        private static IReadOnlyList<string> Unlocked(CadCommandContext c, IReadOnlyList<string> handles)
        {
            var kept = new List<string>();
            int locked = 0;

            foreach (string handle in handles ?? Array.Empty<string>())
            {
                DxfEntity entity = c.Document.FindEntity(handle);

                if (entity == null)
                {
                    continue;
                }

                if (CadLayerEditor.IsLocked(c.Database, entity.Layer))
                {
                    locked++;
                    continue;
                }

                kept.Add(handle);
            }

            if (locked > 0)
            {
                c.Write(locked.ToString(CultureInfo.InvariantCulture) + (locked == 1 ? " was" : " were") + " on a locked layer.");
            }

            kept.Sort(StringComparer.OrdinalIgnoreCase);
            return kept;
        }

        /// <summary>
        /// Gives an INSERT just placed its attributes: an ATTRIB for each definition of its block that is not constant, in
        /// the order of the block, filled from the definition as AutoCAD fills one, with the value given or the default; one of a
        /// definition on layer 0 takes the INSERT's layer, as AutoCAD's does.
        /// </summary>
        private static void AddAttributes(DxfBlockReference insert, DxfBlockTableRecord record, IReadOnlyList<string> values)
        {
            int i = 0;

            foreach (DxfAttributeDefinition definition in Definitions(record))
            {
                var attribute = new DxfAttributeReference();
                attribute.SetAttributeFromBlock(definition, insert.BlockTransform);

                if (values != null && i < values.Count && values[i] != null)
                {
                    attribute.TextString = values[i];
                }

                if (string.Equals(definition.Layer, "0", StringComparison.Ordinal))
                {
                    attribute.Layer = insert.Layer;
                }

                insert.AttributeCollection.AppendAttribute(attribute);
                i++;
            }
        }

        /// <summary>The definitions of a block that give an INSERT an ATTRIB, those not constant, in the order of the block.</summary>
        private static List<DxfAttributeDefinition> Definitions(DxfBlockTableRecord record)
            => record.OfType<DxfAttributeDefinition>().Where(d => !d.Constant).ToList();

        /// <summary>
        /// INSERT (I): the window's Blocks palette; the block chosen is placed with its insertion options, each asked on the
        /// command line where it is to be specified on screen, again and again with Repeat Placement. With no window,
        /// -INSERT's prompts.
        /// </summary>
        private static IEnumerable<CadPrompt> Insert(CadCommandContext c)
        {
            var request = new CadInsertRequest(c.Document);

            if (!c.Session.Request("INSERT", c.Document, request))
            {
                foreach (CadPrompt prompt in InsertTyped(c))
                {
                    yield return prompt;
                }

                yield break;
            }

            if (request.Action != CadDialogAction.Ok)
            {
                yield break;
            }

            string name = null;

            foreach (CadPrompt prompt in BringIn(c, request.FileName, request.Name, n => name = n))
            {
                yield return prompt;
            }

            if (name == null)
            {
                yield break;
            }

            do
            {
                var placing = new Placing { Point = request.Point, Scale = request.Scale, Rotation = request.Rotation };

                foreach (CadPrompt prompt in Place(c, name, placing, request.SpecifyPointOnScreen, request.SpecifyScaleOnScreen, request.SpecifyRotationOnScreen, request.Explode))
                {
                    yield return prompt;
                }

                if (!placing.Placed)
                {
                    yield break;
                }
            }
            while (request.Repeat && request.SpecifyPointOnScreen);
        }

        /// <summary>
        /// -INSERT (-I): "Enter block name or [?] &lt;last&gt;:", a block of the drawing or a DXF file (a path, or name=path to give
        /// the block a name of its own), a * before it to explode it; then the insertion point, scale and rotation asked.
        /// </summary>
        private static IEnumerable<CadPrompt> InsertTyped(CadCommandContext c)
        {
            string last = c.Session.LastBlock;
            string typed;

            while (true)
            {
                yield return CadPrompt.ForKeywordOrText("Enter block name or", new[] { "?" }, last);

                if (c.Input.Is("?"))
                {
                    WriteBlockList(c);
                    continue;
                }

                typed = c.Input.Kind == CadInputKind.Text ? c.Input.Text.Trim() : last;

                if (string.IsNullOrEmpty(typed))
                {
                    yield break;
                }

                break;
            }

            bool explode = typed.StartsWith("*", StringComparison.Ordinal);
            typed = explode ? typed.Substring(1) : typed;
            string file = null;
            string block = typed;
            int equals = typed.IndexOf('=');

            if (equals > 0)
            {
                block = typed.Substring(0, equals);
                file = typed.Substring(equals + 1);
            }
            else if (!c.Database.BlockTable.Has(typed) && (typed.EndsWith(".dxf", StringComparison.OrdinalIgnoreCase) || typed.IndexOfAny(new[] { '\\', '/' }) >= 0))
            {
                file = typed;
                block = Path.GetFileNameWithoutExtension(typed);
            }

            if (file != null && !Path.HasExtension(file))
            {
                file += ".dxf";
            }

            string name = typed;

            if (file != null)
            {
                name = null;

                foreach (CadPrompt prompt in BringInFile(c, file, block, false, n => name = n))
                {
                    yield return prompt;
                }
            }

            if (name == null)
            {
                yield break;
            }

            if (!c.Database.BlockTable.Has(name))
            {
                c.Write("Block \"" + name + "\" not found.");
                yield break;
            }

            var placing = new Placing { Scale = new GeoVector3(1.0, 1.0, 1.0) };

            foreach (CadPrompt prompt in Place(c, name, placing, true, true, true, explode))
            {
                yield return prompt;
            }
        }

        /// <summary>Where and how an INSERT is placed, asked or given, and whether it was.</summary>
        private sealed class Placing
        {
            public GeoPoint3 Point { get; set; }

            public GeoVector3 Scale { get; set; }

            public double Rotation { get; set; }

            public bool Placed { get; set; }
        }

        /// <summary>
        /// The block a request names, brought into the drawing where it comes from another drawing's file: a block of that
        /// drawing copied in with what it places, as AutoCAD's DesignCenter brings one; or the whole file made one block of its
        /// name. Where the drawing has a block of the name, the window is asked whether to redefine it, the command line where
        /// no window answers. The name handed on is null, said why, where it cannot be.
        /// </summary>
        private static IEnumerable<CadPrompt> BringIn(CadCommandContext c, string file, string name, Action<string> brought)
        {
            if (!string.IsNullOrEmpty(file))
            {
                return BringInFile(c, file, string.IsNullOrEmpty(name) ? null : name, true, brought);
            }

            if (string.IsNullOrEmpty(name) || !c.Database.BlockTable.Has(name))
            {
                c.Write("Block \"" + name + "\" not found.");
                brought(null);
            }
            else
            {
                brought(c.Database.BlockTable[name].Name);
            }

            return Array.Empty<CadPrompt>();
        }

        /// <summary>
        /// A block of another drawing's file, or the whole file as one block of the file's name, brought into the drawing. Where
        /// the drawing has a block of the name, "Block "GB" already exists. Redefine it? [Yes/No] &lt;No&gt;:" is asked, as AutoCAD's
        /// -INSERT asks it, the window first where <paramref name="window"/> says so (the request REDEFINE, the name its
        /// payload, Yes its answer): Yes gives the drawing's block the other's entities, base point and unit, as one edit; No
        /// places the drawing's own.
        /// </summary>
        private static IEnumerable<CadPrompt> BringInFile(CadCommandContext c, string file, string block, bool window, Action<string> brought)
        {
            brought(null);
            DxfDatabase other = OpenOther(c, file);

            if (other == null)
            {
                yield break;
            }

            string whole = block ?? Path.GetFileNameWithoutExtension(file);
            bool ofFile = block == null || !other.BlockTable.Has(block) || other.BlockTable[block].IsLayout;
            bool redefine = false;

            if (c.Database.BlockTable.Has(whole))
            {
                DxfBlockTableRecord own = c.Database.BlockTable[whole];

                if (own.IsLayout || own.IsFromExternalReference)
                {
                    c.Write("Block \"" + whole + "\" already exists: the drawing's own is placed.");
                    brought(own.Name);
                    yield break;
                }

                CadUiRequestEventArgs asked = window ? c.Session.Ask("REDEFINE", c.Document, own.Name) : null;

                if (asked != null && asked.Handled)
                {
                    redefine = string.Equals(asked.Answer, "Yes", StringComparison.OrdinalIgnoreCase);
                }
                else
                {
                    yield return CadPrompt.ForKeyword("Block \"" + own.Name + "\" already exists. Redefine it?", new[] { "Yes", "No" }, "No");
                    redefine = c.Input.Is("Yes");
                }

                if (!redefine)
                {
                    brought(c.Database.BlockTable[whole].Name);
                    yield break;
                }
            }
            else if (!IsBlockName(whole))
            {
                c.Write("Invalid block name.");
                yield break;
            }

            CadSpace space = c.Space;
            CadEditResult result = c.Apply(new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, redefine ? "Redefine block" : "Insert block", null, whole) }, s =>
            {
                if (redefine)
                {
                    Redefine(s.Database.BlockTable[whole], other, ofFile ? null : other.BlockTable[block]);
                    return;
                }

                if (ofFile)
                {
                    s.Database.Insert(whole, other, true);
                    return;
                }

                // An INSERT of the block made in the other drawing and copied over brings the block with what it places, as
                // AutoCAD's deep clone does; the copy itself is taken out again, the block staying.
                var carrier = new DxfBlockReference(GeoPoint3.Origin, other.BlockTable[block]);
                other.ModelSpace.AppendEntity(carrier);
                IReadOnlyList<DxfEntity> copies = other.WblockCloneObjects(new[] { carrier }, space.Block);

                foreach (DxfEntity copy in copies)
                {
                    copy.Erase();
                }
            });

            brought(result.Succeeded && c.Database.BlockTable.Has(whole) ? c.Database.BlockTable[whole].Name : null);
        }

        /// <summary>Reads another drawing's file, null, said why, where it cannot be read.</summary>
        private static DxfDatabase OpenOther(CadCommandContext c, string file)
        {
            try
            {
                return DxfDatabase.Open(file);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException || exception is NotSupportedException || exception is FormatException || exception is InvalidDataException)
            {
                c.Write("Cannot read " + file + ": " + exception.Message);
                return null;
            }
        }

        /// <summary>
        /// Defines a block of the drawing again from another drawing's block, or from its model space where none is given: the
        /// old entities erased, the other's copied in with what they bring, its base point (the other's $INSBASE for a whole
        /// drawing) and unit taken. A block the copies bring that the drawing has already stays the drawing's own.
        /// </summary>
        private static void Redefine(DxfBlockTableRecord record, DxfDatabase other, DxfBlockTableRecord from)
        {
            foreach (DxfEntity old in record.ToList())
            {
                old.Erase();
            }

            List<DxfEntity> source = from == null ? other.ModelSpace.ToList() : from.ToList();
            DxfUnits units = from == null ? other.Insunits : from.Units;
            record.Origin = from == null ? other.Insbase : from.Origin;
            Keep(() => record.Units = units, record.Units != units);

            if (source.Count > 0)
            {
                other.WblockCloneObjects(source, record);
            }
        }

        /// <summary>
        /// Places one INSERT: the point, scale and rotation asked where they are to be specified on screen, as AutoCAD's INSERT
        /// asks them, then the values of its attributes (ATTREQ 1), then the INSERT made, exploded where asked.
        /// </summary>
        private static IEnumerable<CadPrompt> Place(CadCommandContext c, string name, Placing placing, bool askPoint, bool askScale, bool askRotation, bool explode)
        {
            DxfBlockTableRecord record = c.Database.BlockTable[name];
            bool uniform = record.BlockScaling == DxfBlockScaling.Uniform || explode;
            GeoVector3 shift = new GeoVector3(0.0, 0.0, 0.0);
            bool scaleGiven = false;
            bool rotationGiven = false;
            double unitFactor = UnitFactor(c.Database, record);
            GeoPoint3 origin = record.Origin;
            var items = new Lazy<IReadOnlyList<DisplayItem2>>(() => CadBlockDrag.ItemsOf(c.Document, name));

            // The block dragged with the cursor as each of the point, scale and rotation is asked, as AutoCAD drags it.
            CadBlockDrag DragOf(CadBlockDragMode mode)
                => new CadBlockDrag(name, items, mode, new GeoPoint2(placing.Point.X, placing.Point.Y), placing.Scale, c.Session.Ucs.AngleToWorld(placing.Rotation), shift, origin, unitFactor);

            c.Write("Units: " + UnitName(record.Units) + "   Conversion:   " + Length(UnitFactor(c.Database, record)));

            if (askPoint)
            {
                while (true)
                {
                    yield return CadPrompt.ForPoint("Specify insertion point", null, InsertKeywords).Dragging(DragOf(CadBlockDragMode.Point));

                    if (c.Input.IsPoint)
                    {
                        placing.Point = P3(c.Input.Point);
                        break;
                    }

                    if (c.Input.Is("Basepoint"))
                    {
                        yield return CadPrompt.ForPoint("Specify base point");

                        if (c.Input.IsPoint)
                        {
                            // The point of the block that comes to the insertion point, in place of its origin.
                            shift = record.Origin - P3(c.Input.Point);
                        }

                        continue;
                    }

                    if (c.Input.Is("Rotate"))
                    {
                        yield return CadPrompt.ForAngle("Specify rotation angle", null, null, Number(0.0));

                        if (c.Input.Kind == CadInputKind.Number)
                        {
                            placing.Rotation = c.Input.Number;
                            rotationGiven = true;
                        }

                        continue;
                    }

                    if (c.Input.Is("Scale") || c.Input.Is("X") || c.Input.Is("Y") || c.Input.Is("Z"))
                    {
                        string axis = c.Input.Is("Scale") ? null : c.Input.Text;
                        yield return CadPrompt.ForDistance(axis == null ? "Specify scale factor for XYZ axes" : "Specify " + axis + " scale factor", null, null, Length(1.0));

                        if (c.Input.Kind == CadInputKind.Number && c.Input.Number != 0.0)
                        {
                            double f = c.Input.Number;
                            GeoVector3 now = placing.Scale;
                            placing.Scale = axis == null ? new GeoVector3(f, f, f) : axis == "X" ? new GeoVector3(f, now.Y, now.Z) : axis == "Y" ? new GeoVector3(now.X, f, now.Z) : new GeoVector3(now.X, now.Y, f);
                            scaleGiven = true;
                        }

                        continue;
                    }

                    yield break;
                }
            }

            if (askScale && !scaleGiven)
            {
                if (uniform)
                {
                    yield return CadPrompt.ForDistance("Specify scale factor for XYZ axes", null, null, Length(1.0)).Dragging(DragOf(CadBlockDragMode.Scale));
                    double f = c.Input.Kind == CadInputKind.Number && c.Input.Number != 0.0 ? c.Input.Number : 1.0;
                    placing.Scale = new GeoVector3(f, f, f);
                }
                else
                {
                    GeoPoint2 at = new GeoPoint2(placing.Point.X, placing.Point.Y);
                    // No rubber band: a number typed is a scale, not a distance along it; a point picked is the opposite corner all the same.
                    yield return CadPrompt.ForPoint("Enter X scale factor, specify opposite corner,", null, new[] { "Corner", "XYZ" }, "1", true).Dragging(DragOf(CadBlockDragMode.Corner));
                    double? x = null;
                    double? y = null;
                    double? z = null;

                    if (c.Input.Is("Corner"))
                    {
                        yield return CadPrompt.ForPoint("Specify opposite corner", at).Dragging(DragOf(CadBlockDragMode.Corner));
                    }
                    else if (c.Input.Is("XYZ"))
                    {
                        yield return CadPrompt.ForDistance("Specify X scale factor", null, null, Length(1.0));
                        x = c.Input.Kind == CadInputKind.Number ? c.Input.Number : 1.0;
                        yield return CadPrompt.ForDistance("Specify Y scale factor", null, null, "use X scale factor");
                        y = c.Input.Kind == CadInputKind.Number ? c.Input.Number : x;
                        yield return CadPrompt.ForDistance("Specify Z scale factor", null, null, "use X scale factor");
                        z = c.Input.Kind == CadInputKind.Number ? c.Input.Number : x;
                    }

                    if (c.Input.IsPoint)
                    {
                        // The opposite corner of a unit square: its sides the scales along X and Y.
                        GeoVector2 corner = c.Input.Point - at;
                        x = corner.X;
                        y = corner.Y;
                    }
                    else if (x == null && c.Input.Kind == CadInputKind.Text)
                    {
                        if (!double.TryParse(c.Input.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double typed) || typed == 0.0 || double.IsInfinity(typed) || double.IsNaN(typed))
                        {
                            c.Write("Requires a nonzero number, a point or an option keyword.");
                            yield break;
                        }

                        x = typed;
                    }

                    x = x ?? 1.0;

                    if (y == null)
                    {
                        yield return CadPrompt.ForDistance("Enter Y scale factor", null, null, "use X scale factor");
                        y = c.Input.Kind == CadInputKind.Number && c.Input.Number != 0.0 ? c.Input.Number : x;
                    }

                    if (x.Value == 0.0 || y.Value == 0.0)
                    {
                        c.Write("Value must be nonzero.");
                        yield break;
                    }

                    placing.Scale = new GeoVector3(x.Value, y.Value, z ?? x.Value);
                }
            }

            if (askRotation && !rotationGiven)
            {
                yield return CadPrompt.ForAngle("Specify rotation angle", new GeoPoint2(placing.Point.X, placing.Point.Y), null, Number(0.0)).Dragging(DragOf(CadBlockDragMode.Rotation));

                if (c.Input.Kind == CadInputKind.Number)
                {
                    placing.Rotation = c.Input.Number;
                }
            }

            var values = new List<string>();

            if (!explode)
            {
                foreach (CadPrompt prompt in AttributeValues(c, record, values))
                {
                    yield return prompt;
                }
            }

            double angle = c.Session.Ucs.AngleToWorld(placing.Rotation);
            GeoPoint3 point = placing.Point;
            GeoVector3 scale = placing.Scale;

            // The base point picked comes to the insertion point: the INSERT moved by where the block's origin then lands, the
            // shift scaled as the block is, its units made the drawing's too.
            if (shift.X != 0.0 || shift.Y != 0.0 || shift.Z != 0.0)
            {
                GeoTransform3 turn = GeoTransform3.RotationAxis(GeoPoint3.Origin, Up, angle);
                GeoVector3 scaled = new GeoVector3(shift.X * scale.X * unitFactor, shift.Y * scale.Y * unitFactor, shift.Z * scale.Z * unitFactor);
                point = point + turn.Transform(scaled);
            }

            CadSpace space = c.Space;
            CadEditResult result = c.Apply(null, s =>
            {
                DxfBlockTableRecord block = s.Database.BlockTable[name];
                var insert = new DxfBlockReference(point, block);
                s.Append(space, Current(c, insert));
                double factor = UnitFactor(s.Database, block);
                insert.ScaleFactors = new GeoVector3(scale.X * factor, scale.Y * factor, scale.Z * factor);
                insert.Rotation = angle;

                if (!explode)
                {
                    AddAttributes(insert, block, values);
                    return;
                }

                if (!block.Explodable)
                {
                    throw new InvalidOperationException("The block " + name + " may not be exploded.");
                }

                foreach (DxfEntity piece in insert.Explode())
                {
                    s.Append(space, piece);
                }

                insert.Erase();
            });

            placing.Placed = result.Succeeded;

            if (result.Succeeded)
            {
                c.Session.LastBlock = name;
            }
        }

        /// <summary>
        /// Asks the value of each attribute an INSERT of a block gets, as AutoCAD asks with ATTREQ 1 and ATTDIA 0: "Enter
        /// attribute values", then the prompt of each definition that is not preset with its default, in the order of the block;
        /// then "Verify attribute values" and each of those that are to be verified again.
        /// </summary>
        private static IEnumerable<CadPrompt> AttributeValues(CadCommandContext c, DxfBlockTableRecord record, List<string> values)
        {
            List<DxfAttributeDefinition> definitions = Definitions(record);

            foreach (DxfAttributeDefinition definition in definitions)
            {
                values.Add(definition.TextString);
            }

            if (definitions.All(d => d.Preset))
            {
                yield break;
            }

            c.Write("Enter attribute values");

            for (int i = 0; i < definitions.Count; i++)
            {
                if (definitions[i].Preset)
                {
                    continue;
                }

                yield return CadPrompt.ForString(PromptOf(definitions[i]), definitions[i].TextString);

                if (c.Input.Kind == CadInputKind.Text)
                {
                    values[i] = c.Input.Text;
                }
            }

            if (!definitions.Any(d => d.Verifiable && !d.Preset))
            {
                yield break;
            }

            c.Write("Verify attribute values");

            for (int i = 0; i < definitions.Count; i++)
            {
                if (definitions[i].Preset || !definitions[i].Verifiable)
                {
                    continue;
                }

                yield return CadPrompt.ForString(PromptOf(definitions[i]), values[i]);

                if (c.Input.Kind == CadInputKind.Text)
                {
                    values[i] = c.Input.Text;
                }
            }
        }

        private static string PromptOf(DxfAttributeDefinition definition) => string.IsNullOrEmpty(definition.Prompt) ? definition.Tag : definition.Prompt;

        /// <summary>The factor that takes a block's units to the drawing's, as AutoCAD's INSERT scales by it: 1 where either gives none.</summary>
        private static double UnitFactor(DxfDatabase database, DxfBlockTableRecord record)
        {
            double from = UnitMillimetres(record.Units);
            double to = UnitMillimetres(database.Insunits);
            return from > 0.0 && to > 0.0 ? from / to : 1.0;
        }

        internal static double UnitMillimetres(DxfUnits units)
        {
            switch (units)
            {
                case DxfUnits.Inches: return 25.4;
                case DxfUnits.Feet: return 304.8;
                case DxfUnits.Millimeters: return 1.0;
                case DxfUnits.Centimeters: return 10.0;
                case DxfUnits.Meters: return 1000.0;
                case DxfUnits.Decimeters: return 100.0;
                case DxfUnits.Kilometers: return 1E6;
                case DxfUnits.Yards: return 914.4;
                default: return 0.0;
            }
        }

        /// <summary>A unit as AutoCAD's dialogs name it: Millimeters, Inches; Unitless for none.</summary>
        internal static string UnitName(DxfUnits units) => units == DxfUnits.Undefined ? "Unitless" : units.ToString();

        /// <summary>
        /// WBLOCK (W): the window's Write Block dialog, shown again after each Pick point or Select objects; OK writes the
        /// new drawing. With no window, -WBLOCK's prompts.
        /// </summary>
        private static IEnumerable<CadPrompt> WriteBlock(CadCommandContext c)
        {
            var request = new CadWriteBlockRequest(c.Document) { Objects = Unlocked(c, c.PreSelected) };
            c.ClearsSelection = c.PreSelected.Count > 0;

            while (true)
            {
                request.Action = CadDialogAction.Cancel;

                if (!c.Session.Request("WBLOCK", c.Document, request))
                {
                    foreach (CadPrompt prompt in WriteBlockTyped(c))
                    {
                        yield return prompt;
                    }

                    yield break;
                }

                if (request.Action == CadDialogAction.PickPoint)
                {
                    yield return CadPrompt.ForPoint("Specify insertion base point");

                    if (c.Input.IsPoint)
                    {
                        request.BasePoint = P3(c.Input.Point);
                    }
                }
                else if (request.Action == CadDialogAction.SelectObjects)
                {
                    yield return CadPrompt.ForSelection();
                    request.Objects = Unlocked(c, c.Input.Handles);
                }
                else if (request.Action == CadDialogAction.Ok)
                {
                    break;
                }
                else
                {
                    yield break;
                }
            }

            WriteBlockFile(c, request);
        }

        /// <summary>
        /// -WBLOCK (-W): "Enter name of output file:", then "Enter name of existing block or [= (block=output file)/* (whole
        /// drawing)] &lt;define new drawing&gt;:"; a new drawing asks its base point and objects, which are erased, as AutoCAD's
        /// -WBLOCK erases them.
        /// </summary>
        private static IEnumerable<CadPrompt> WriteBlockTyped(CadCommandContext c)
        {
            yield return CadPrompt.ForString("Enter name of output file");

            if (c.Input.Kind != CadInputKind.Text || c.Input.Text.Trim().Length == 0)
            {
                yield break;
            }

            var request = new CadWriteBlockRequest(c.Document) { FileName = c.Input.Text.Trim() };

            if (!Path.HasExtension(request.FileName))
            {
                request.FileName += ".dxf";
            }

            yield return CadPrompt.ForString("Enter name of existing block or [= (block=output file)/* (whole drawing)]", "define new drawing");
            string answer = c.Input.Kind == CadInputKind.Text ? c.Input.Text.Trim() : string.Empty;

            if (answer == "*")
            {
                request.Source = CadWriteBlockSource.EntireDrawing;
            }
            else if (answer.Length > 0)
            {
                request.Source = CadWriteBlockSource.Block;
                request.BlockName = answer == "=" ? Path.GetFileNameWithoutExtension(request.FileName) : answer;
            }
            else
            {
                request.ObjectAction = CadBlockObjectAction.Delete;
                yield return CadPrompt.ForPoint("Specify insertion base point");

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                request.BasePoint = P3(c.Input.Point);

                foreach (CadPrompt prompt in c.SelectObjects())
                {
                    yield return prompt;
                }

                request.Objects = c.Selected;
            }

            WriteBlockFile(c, request);
        }

        /// <summary>
        /// Writes the new drawing a request describes, from the presenter's metric template: the objects with the base point
        /// at its origin, a block with its origin there, or the whole of model space as it is; then, for objects, what becomes of
        /// them, as one edit of this drawing. The file is written first and stays, as AutoCAD's.
        /// </summary>
        internal static void WriteBlockFile(CadCommandContext c, CadWriteBlockRequest request)
        {
            string path = (request.FileName ?? string.Empty).Trim();

            if (path.Length == 0)
            {
                c.Write("No file name given.");
                return;
            }

            List<DxfEntity> entities;
            GeoVector3 shift;
            DxfDatabase target = CadTemplate.CreateMetric();
            DxfUnits units = request.Units;

            switch (request.Source)
            {
                case CadWriteBlockSource.Block:
                    if (string.IsNullOrEmpty(request.BlockName) || !c.Database.BlockTable.Has(request.BlockName))
                    {
                        c.Write("Block \"" + request.BlockName + "\" not found.");
                        return;
                    }

                    DxfBlockTableRecord record = c.Database.BlockTable[request.BlockName];
                    entities = record.ToList();
                    shift = GeoPoint3.Origin - record.Origin;
                    units = record.Units == DxfUnits.Undefined ? units : record.Units;
                    break;
                case CadWriteBlockSource.EntireDrawing:
                    entities = c.Database.ModelSpace.ToList();
                    shift = new GeoVector3(0.0, 0.0, 0.0);
                    target.Insbase = c.Database.Insbase;
                    break;
                default:
                    entities = (request.Objects ?? Array.Empty<string>()).Select(h => c.Document.FindEntity(h)).Where(e => e != null).ToList();
                    shift = GeoPoint3.Origin - request.BasePoint;

                    if (entities.Count == 0)
                    {
                        c.Write("No objects selected.");
                        return;
                    }

                    break;
            }

            try
            {
                if (entities.Count > 0)
                {
                    IReadOnlyList<DxfEntity> copies = c.Database.WblockCloneObjects(entities, target.ModelSpace);

                    if (shift.X != 0.0 || shift.Y != 0.0 || shift.Z != 0.0)
                    {
                        GeoTransform3 move = GeoTransform3.Translation(shift);

                        foreach (DxfEntity copy in copies)
                        {
                            copy.TransformBy(move);
                        }
                    }
                }

                target.Insunits = units;
                string full = Path.GetFullPath(path);
                Directory.CreateDirectory(Path.GetDirectoryName(full));
                target.Save(full);
                GeometryHelperLog.Info("CadCommands: WBLOCK wrote " + entities.Count.ToString(CultureInfo.InvariantCulture) + " objects to " + full + ".");
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException || exception is NotSupportedException || exception is InvalidOperationException)
            {
                GeometryHelperLog.Warn("CadCommands: WBLOCK did not write " + path + ".", exception);
                c.Write("Cannot write " + path + ": " + exception.Message);
                return;
            }

            if (request.Source != CadWriteBlockSource.Objects || request.ObjectAction == CadBlockObjectAction.Retain)
            {
                return;
            }

            // Convert to block makes a block of the file's name of the objects in this drawing too, and places it where they were.
            if (request.ObjectAction == CadBlockObjectAction.ConvertToBlock)
            {
                string name = Path.GetFileNameWithoutExtension(path);
                MakeBlock(c, new CadBlockDefinitionRequest(c.Document)
                {
                    Name = name,
                    BasePoint = request.BasePoint,
                    Objects = request.Objects,
                    ObjectAction = CadBlockObjectAction.ConvertToBlock,
                    Units = units,
                    Redefine = c.Database.BlockTable.Has(name),
                });
                return;
            }

            EraseAll(c, entities.Select(e => e.Handle).ToList());
        }

        /// <summary>
        /// ATTDEF (ATT): the window's Attribute Definition dialog, shown again after the height or rotation is picked; OK asks
        /// the start point where it is to be specified on screen and makes the definition. With no window, -ATTDEF's prompts.
        /// </summary>
        private static IEnumerable<CadPrompt> AttributeDefinition(CadCommandContext c)
        {
            string style = TextStyle(c);
            var request = new CadAttributeDefinitionRequest(c.Document, style, AttributeHeight(c, style));

            while (true)
            {
                request.Action = CadDialogAction.Cancel;

                if (!c.Session.Request("ATTDEF", c.Document, request))
                {
                    foreach (CadPrompt prompt in AttributeDefinitionTyped(c))
                    {
                        yield return prompt;
                    }

                    yield break;
                }

                GeoPoint2? from = request.SpecifyOnScreen ? (GeoPoint2?)null : new GeoPoint2(request.Point.X, request.Point.Y);

                if (request.Action == CadDialogAction.PickHeight)
                {
                    yield return CadPrompt.ForDistance("Specify text height", from, null, Length(request.Height));

                    if (c.Input.Kind == CadInputKind.Number && c.Input.Number > 0.0)
                    {
                        request.Height = c.Input.Number;
                    }
                }
                else if (request.Action == CadDialogAction.PickRotation)
                {
                    yield return CadPrompt.ForAngle("Specify rotation angle of text", from, null, Number(request.Rotation * 180.0 / Math.PI));

                    if (c.Input.Kind == CadInputKind.Number)
                    {
                        request.Rotation = c.Input.Number;
                    }
                }
                else if (request.Action == CadDialogAction.Ok)
                {
                    break;
                }
                else
                {
                    yield break;
                }
            }

            if (request.SpecifyOnScreen)
            {
                yield return CadPrompt.ForPoint("Specify start point");

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                request.Point = P3(c.Input.Point);
            }

            MakeAttributeDefinition(c, request);
        }

        /// <summary>The height a new attribute definition offers: that of a style of fixed height, else the one TEXT used last, else TEXTSIZE.</summary>
        private static double AttributeHeight(CadCommandContext c, string style)
        {
            DxfTextStyleTableRecord record = StyleRecord(c, style);
            return record != null && record.TextSize > 0.0 ? record.TextSize : c.Remembered("text.height", TextSize(c));
        }

        /// <summary>
        /// -ATTDEF (-ATT): the modes toggled until Enter, the tag, prompt and default, then the start point with Justify and
        /// Style, the height and the rotation, as AutoCAD's -ATTDEF asks them.
        /// </summary>
        private static IEnumerable<CadPrompt> AttributeDefinitionTyped(CadCommandContext c)
        {
            string style = TextStyle(c);
            var request = new CadAttributeDefinitionRequest(c.Document, style, AttributeHeight(c, style)) { SpecifyOnScreen = false };
            string[] modes = { "Invisible", "Constant", "Verify", "Preset", "Lock position", "Multiple lines" };

            while (true)
            {
                c.Write("Current attribute modes: Invisible=" + YesNo(request.Invisible) + " Constant=" + YesNo(request.Constant) + " Verify=" + YesNo(request.Verify)
                    + " Preset=" + YesNo(request.Preset) + " Lock position=" + YesNo(request.LockPosition) + " Annotative=N Multiple lines=" + YesNo(request.MultipleLines));
                yield return CadPrompt.ForKeyword("Enter an option to change", modes, "done");

                if (c.Input.Is("Invisible"))
                {
                    request.Invisible = !request.Invisible;
                }
                else if (c.Input.Is("Constant"))
                {
                    request.Constant = !request.Constant;
                }
                else if (c.Input.Is("Verify"))
                {
                    request.Verify = !request.Verify;
                }
                else if (c.Input.Is("Preset"))
                {
                    request.Preset = !request.Preset;
                }
                else if (c.Input.Is("Lock position"))
                {
                    request.LockPosition = !request.LockPosition;
                }
                else if (c.Input.Is("Multiple lines"))
                {
                    request.MultipleLines = !request.MultipleLines;
                }
                else
                {
                    break;
                }
            }

            while (true)
            {
                yield return CadPrompt.ForString("Enter attribute tag name");

                if (c.Input.Kind != CadInputKind.Text)
                {
                    yield break;
                }

                if (IsTag(c.Input.Text.Trim()))
                {
                    request.Tag = c.Input.Text.Trim();
                    break;
                }

                c.Write("Invalid tag: a tag is one word, with no space.");
            }

            if (!request.Constant)
            {
                yield return CadPrompt.ForString("Enter attribute prompt");
                request.Prompt = c.Input.Kind == CadInputKind.Text ? c.Input.Text : string.Empty;
            }

            yield return CadPrompt.ForString(request.Constant ? "Enter attribute value" : "Enter default attribute value");
            request.Default = c.Input.Kind == CadInputKind.Text ? c.Input.Text : string.Empty;

            while (true)
            {
                c.Write("Current text style:  \"" + (request.TextStyle ?? "Standard") + "\"  Text height:  " + Length(request.Height) + "  Annotative:  No");
                yield return CadPrompt.ForPoint("Specify start point of text", null, new[] { "Justify", "Style" });

                if (c.Input.IsPoint)
                {
                    request.Point = P3(c.Input.Point);
                    break;
                }

                if (c.Input.Is("Justify"))
                {
                    yield return CadPrompt.ForKeyword("Enter an option", Justifications.Select(j => j.Key).ToArray());
                    KeyValuePair<string, DxfAttachmentPoint> picked = Justifications.FirstOrDefault(j => c.Input.Is(j.Key));

                    if (picked.Key != null)
                    {
                        request.Justification = picked.Value;
                    }

                    continue;
                }

                if (c.Input.Is("Style"))
                {
                    yield return CadPrompt.ForString("Enter style name", request.TextStyle ?? "Standard");

                    if (c.Input.Kind == CadInputKind.Text)
                    {
                        string name = c.Input.Text.Trim();

                        if (c.Database.TextStyleTable.Has(name))
                        {
                            request.TextStyle = c.Database.TextStyleTable[name].Name;
                        }
                        else
                        {
                            c.Write("Cannot find text style \"" + name + "\".");
                        }
                    }

                    continue;
                }

                yield break;
            }

            GeoPoint2 at = new GeoPoint2(request.Point.X, request.Point.Y);
            yield return CadPrompt.ForDistance("Specify text height", at, null, Length(request.Height));

            if (c.Input.Kind == CadInputKind.Number)
            {
                if (!(c.Input.Number > 0.0))
                {
                    c.Write("Value must be positive and nonzero.");
                    yield break;
                }

                request.Height = c.Input.Number;
            }

            yield return CadPrompt.ForAngle("Specify rotation angle of text", at, null, Number(0.0));

            if (c.Input.Kind == CadInputKind.Number)
            {
                request.Rotation = c.Input.Number;
            }

            MakeAttributeDefinition(c, request);
        }

        private static string YesNo(bool value) => value ? "Y" : "N";

        /// <summary>Whether a tag is one AutoCAD takes: one word, with no space or line break.</summary>
        internal static bool IsTag(string tag) => !string.IsNullOrEmpty(tag) && tag.IndexOfAny(new[] { ' ', '\t', '\r', '\n' }) < 0;

        /// <summary>Makes the attribute definition a request describes, one edit: its tag in capitals, as AutoCAD writes one.</summary>
        internal static void MakeAttributeDefinition(CadCommandContext c, CadAttributeDefinitionRequest request)
        {
            string tag = (request.Tag ?? string.Empty).Trim().ToUpperInvariant();

            if (!IsTag(tag))
            {
                c.Write("Invalid tag: a tag is one word, with no space.");
                return;
            }

            if (!(request.Height > 0.0))
            {
                c.Write("Value must be positive and nonzero.");
                return;
            }

            CadSpace space = c.Space;
            double angle = c.Session.Ucs.AngleToWorld(request.Rotation);
            string value = (request.Default ?? string.Empty).Replace("\r", string.Empty).Replace("\n", " ");
            string prompt = (request.Prompt ?? string.Empty).Replace("\r", string.Empty).Replace("\n", " ");
            c.Apply(null, s =>
            {
                DxfTextStyleTableRecord style = request.TextStyle != null && s.Database.TextStyleTable.Has(request.TextStyle) ? s.Database.TextStyleTable[request.TextStyle] : null;
                var definition = new DxfAttributeDefinition(request.Point, value, tag, prompt, style);
                s.Append(space, Current(c, definition));
                definition.Height = request.Height;
                definition.Rotation = angle;

                if (request.Justification != DxfAttachmentPoint.BaseLeft)
                {
                    definition.Justify = request.Justification;
                    definition.AlignmentPoint = request.Point;
                }

                definition.Invisible = request.Invisible;
                definition.Constant = request.Constant;
                definition.Verifiable = request.Verify;
                definition.Preset = request.Preset;
                definition.LockPositionInBlock = request.LockPosition;

                if (request.MultipleLines)
                {
                    definition.IsMTextAttributeDefinition = true;
                }
            });
            c.Remember("text.height", request.Height);
        }

        /// <summary>
        /// EATTEDIT and ATTEDIT: a block with attributes picked, then the window's Enhanced Attribute Editor over its attributes,
        /// what is typed shown on the canvas at once (<see cref="CadAttributeEditRequest.Preview"/>), Apply keeping it with the
        /// editor open, OK keeping it and ending, Cancel taking back what was not applied, Select block taking it back and taking
        /// another; the values asked in turn with no window. One undo step for the whole command.
        /// </summary>
        private static IEnumerable<CadPrompt> AttributeEdit(CadCommandContext c, string ask)
        {
            CadAttributeEditRequest request = null;

            foreach (CadPrompt prompt in PickAttributed(c, ask, r => request = r))
            {
                yield return prompt;
            }

            while (request != null)
            {
                List<CadAttributeRow> before = request.Rows.Select(r => r.Clone()).ToList();
                request.Action = CadDialogAction.Cancel;

                // The rows as the drawing shows them, written as they are typed, and the savepoint Cancel reads the drawing back
                // to: as the editor opened, or as Apply last left it, inside the command's one undo step.
                List<CadAttributeRow> shown = before.Select(r => r.Clone()).ToList();
                bool unapplied = false;
                c.OpenUndoStep();
                CadGroupMark mark = c.Document.MarkGroup();
                CadAttributeEditRequest editing = request;
                editing.Previewing = () => unapplied |= Show(c, editing, shown, true);
                editing.Applying = () =>
                {
                    Show(c, editing, shown, false);
                    mark = c.Document.MarkGroup();
                    unapplied = false;
                };

                bool asked = c.Session.Request("EATTEDIT", c.Document, request);
                editing.Previewing = null;
                editing.Applying = null;

                if (!asked)
                {
                    for (int i = 0; i < request.Rows.Count; i++)
                    {
                        CadAttributeRow row = request.Rows[i];
                        yield return CadPrompt.ForString("Enter new value for attribute \"" + row.Tag + "\"", row.Value);

                        if (c.Input.Kind == CadInputKind.Text)
                        {
                            row.Value = c.Input.Text;
                        }
                    }

                    WriteAttributes(c, request, before);
                    yield break;
                }

                if (request.Action == CadDialogAction.Ok || request.Action == CadDialogAction.Apply)
                {
                    Show(c, request, shown, false);

                    if (request.Action == CadDialogAction.Ok)
                    {
                        yield break;
                    }

                    request = Rows(c.Document, request.Insert);
                    continue;
                }

                // Cancel and Select block take back what was shown as typed and not applied.
                if (unapplied)
                {
                    c.Document.BackTo(mark);
                }

                if (request.Action == CadDialogAction.SelectBlock)
                {
                    CadAttributeEditRequest other = null;

                    foreach (CadPrompt prompt in PickAttributed(c, ask, r => other = r))
                    {
                        yield return prompt;
                    }

                    request = other ?? Rows(c.Document, request.Insert);
                }
                else
                {
                    yield break;
                }
            }
        }

        /// <summary>Asks for a block with attributes until one is picked, Enter or Esc, as EATTEDIT asks: one of none says so.</summary>
        private static IEnumerable<CadPrompt> PickAttributed(CadCommandContext c, string ask, Action<CadAttributeEditRequest> picked)
        {
            while (true)
            {
                yield return CadPrompt.ForEntity(ask);

                if (c.Input.Kind != CadInputKind.Selection)
                {
                    yield break;
                }

                DxfEntity entity = c.Document.FindEntity(c.Input.Handles[0]);

                if (!(entity is DxfBlockReference insert))
                {
                    c.Write("That object is not a block.");
                    continue;
                }

                if (insert.AttributeCollection.Count == 0)
                {
                    c.Write("That block has no editable attributes.");
                    continue;
                }

                picked(Rows(c.Document, insert.Handle));
                yield break;
            }
        }

        /// <summary>A request of the Enhanced Attribute Editor over an INSERT's attributes as the drawing has them.</summary>
        internal static CadAttributeEditRequest Rows(CadDocument document, string handle)
        {
            var insert = (DxfBlockReference)document.FindEntity(handle);
            DxfBlockTableRecord record = insert.BlockTableRecord;
            var rows = new List<CadAttributeRow>();

            foreach (DxfAttributeReference attribute in insert.AttributeCollection)
            {
                DxfAttributeDefinition definition = record?.OfType<DxfAttributeDefinition>().FirstOrDefault(d => string.Equals(d.Tag, attribute.Tag, StringComparison.OrdinalIgnoreCase));
                rows.Add(new CadAttributeRow
                {
                    Handle = attribute.Handle,
                    Tag = attribute.Tag,
                    Prompt = definition?.Prompt ?? string.Empty,
                    Value = attribute.TextString,
                    TextStyle = attribute.TextStyleName,
                    Justification = attribute.Justify,
                    Height = attribute.Height,
                    Rotation = attribute.Rotation,
                    Backwards = attribute.IsMirroredInX,
                    UpsideDown = attribute.IsMirroredInY,
                    WidthFactor = attribute.WidthFactor,
                    Oblique = attribute.Oblique,
                    Layer = attribute.Layer,
                    Linetype = attribute.Linetype,
                    ColorIndex = attribute.Color.IsByLayer ? 256 : attribute.Color.IsByBlock ? 0 : attribute.Color.ColorIndex,
                    LineWeight = (int)attribute.LineWeight,
                });
            }

            return new CadAttributeEditRequest(document, insert.Handle, insert.Name, rows);
        }

        /// <summary>
        /// Writes the rows of the editor that differ from what the drawing shows (<paramref name="shown"/>), which then shows
        /// them; whether any was written. Quiet, as while a value is typed, a height not above nought is passed over unsaid.
        /// </summary>
        private static bool Show(CadCommandContext c, CadAttributeEditRequest request, List<CadAttributeRow> shown, bool quiet)
        {
            if (WriteAttributes(c, request, shown, quiet) == 0)
            {
                return false;
            }

            shown.Clear();
            shown.AddRange(request.Rows.Select(r => r.Clone()));
            return true;
        }

        /// <summary>Writes what the rows changed into the INSERT's attributes, one edit; nothing where nothing changed.</summary>
        private static void WriteAttributes(CadCommandContext c, CadAttributeEditRequest request, List<CadAttributeRow> before) => WriteAttributes(c, request, before, false);

        /// <summary>Writes what the rows changed into the INSERT's attributes, one edit; how many rows it wrote, none where nothing changed or the edit was refused.</summary>
        private static int WriteAttributes(CadCommandContext c, CadAttributeEditRequest request, List<CadAttributeRow> before, bool quiet)
        {
            var changed = new List<KeyValuePair<CadAttributeRow, CadAttributeRow>>();

            for (int i = 0; i < request.Rows.Count && i < before.Count; i++)
            {
                if (!Same(request.Rows[i], before[i]))
                {
                    changed.Add(new KeyValuePair<CadAttributeRow, CadAttributeRow>(before[i], request.Rows[i]));
                }
            }

            if (changed.Count == 0)
            {
                return 0;
            }

            if (changed.Any(p => !(p.Value.Height > 0.0) || !(p.Value.WidthFactor > 0.0)))
            {
                if (!quiet)
                {
                    c.Write("Value must be positive and nonzero.");
                }

                return 0;
            }

            // A style typed that the drawing has not would fail the edit, and with it the command's step: while a value is
            // typed it is passed over, and said only on Apply or OK.
            if (quiet && changed.Any(p => p.Value.TextStyle != p.Key.TextStyle && p.Value.TextStyle != null && !c.Database.TextStyleTable.Has(p.Value.TextStyle)))
            {
                return 0;
            }

            string handle = request.Insert;
            CadEditResult result = c.Apply(new[] { new CadEditRecord(CadEditRecordKind.PropertyChanged, handle, "Attributes", null, string.Join(",", changed.Select(p => p.Value.Tag))) }, s =>
            {
                var insert = (DxfBlockReference)s.Document.FindEntity(handle);

                foreach (KeyValuePair<CadAttributeRow, CadAttributeRow> pair in changed)
                {
                    DxfAttributeReference attribute = insert.AttributeCollection.First(a => string.Equals(a.Handle, pair.Value.Handle, StringComparison.OrdinalIgnoreCase));
                    Write(attribute, pair.Key, pair.Value, s.Database);
                }
            });

            return result.Succeeded ? changed.Count : 0;
        }

        private static bool Same(CadAttributeRow a, CadAttributeRow b)
            => a.Value == b.Value && a.TextStyle == b.TextStyle && a.Justification == b.Justification && a.Height.Equals(b.Height) && a.Rotation.Equals(b.Rotation)
               && a.Backwards == b.Backwards && a.UpsideDown == b.UpsideDown && a.WidthFactor.Equals(b.WidthFactor) && a.Oblique.Equals(b.Oblique)
               && a.Layer == b.Layer && a.Linetype == b.Linetype && a.ColorIndex == b.ColorIndex && a.LineWeight == b.LineWeight;

        /// <summary>Sets on an attribute each value of a row that differs from what it was.</summary>
        private static void Write(DxfAttributeReference attribute, CadAttributeRow old, CadAttributeRow row, DxfDatabase database)
        {
            if (row.Value != old.Value)
            {
                attribute.TextString = (row.Value ?? string.Empty).Replace("\r", string.Empty).Replace("\n", " ");
            }

            if (row.TextStyle != old.TextStyle && row.TextStyle != null)
            {
                attribute.TextStyleName = database.TextStyleTable.Has(row.TextStyle) ? database.TextStyleTable[row.TextStyle].Name : throw new ArgumentException("Cannot find text style \"" + row.TextStyle + "\".");
            }

            if (row.Justification != old.Justification)
            {
                // The text keeps the point it stands at: the new justification hangs it there.
                GeoPoint3 at = old.Justification == DxfAttachmentPoint.BaseLeft ? attribute.Position : attribute.AlignmentPoint;
                attribute.Justify = row.Justification;

                if (row.Justification == DxfAttachmentPoint.BaseLeft)
                {
                    attribute.Position = at;
                }
                else
                {
                    attribute.AlignmentPoint = at;
                }
            }

            if (!row.Height.Equals(old.Height))
            {
                attribute.Height = row.Height;
            }

            if (!row.Rotation.Equals(old.Rotation))
            {
                attribute.Rotation = row.Rotation;
            }

            if (row.Backwards != old.Backwards)
            {
                attribute.IsMirroredInX = row.Backwards;
            }

            if (row.UpsideDown != old.UpsideDown)
            {
                attribute.IsMirroredInY = row.UpsideDown;
            }

            if (!row.WidthFactor.Equals(old.WidthFactor))
            {
                attribute.WidthFactor = row.WidthFactor;
            }

            if (!row.Oblique.Equals(old.Oblique))
            {
                attribute.Oblique = row.Oblique;
            }

            if (row.Layer != old.Layer && !string.IsNullOrEmpty(row.Layer))
            {
                attribute.Layer = row.Layer;
            }

            if (row.Linetype != old.Linetype && !string.IsNullOrEmpty(row.Linetype))
            {
                attribute.Linetype = row.Linetype;
            }

            if (row.ColorIndex != old.ColorIndex)
            {
                attribute.Color = row.ColorIndex == 256 ? DxfColor.ByLayer : row.ColorIndex == 0 ? DxfColor.ByBlock : DxfColor.FromColorIndex(DxfColorMethod.ByAci, (short)row.ColorIndex);
            }

            if (row.LineWeight != old.LineWeight)
            {
                attribute.LineWeight = (DxfLineWeight)row.LineWeight;
            }
        }

        /// <summary>
        /// -ATTEDIT (-ATE): attributes edited one at a time, as AutoCAD's: the block, tag and value specifications (* and ?
        /// wildcards), the attributes that match them of the INSERTs selected (of one clicked, the attribute clicked alone; of one
        /// a window took, those inside it, or those a crossing box touches; of one taken otherwise, all), and for each, marked with a cross on the canvas, "Enter an option
        /// [Value/Position/Height/Angle/Style/Layer/Color/Next] &lt;N&gt;:" until Next; each change one edit of the command's step.
        /// </summary>
        private static IEnumerable<CadPrompt> AttributeEditTyped(CadCommandContext c)
        {
            yield return CadPrompt.ForKeyword("Edit attributes one at a time?", new[] { "Yes", "No" }, "Yes");

            if (c.Input.Is("No"))
            {
                c.Write("Global editing of attribute values is not built here: edit them one at a time.");
                yield break;
            }

            if (!c.Input.Is("Yes"))
            {
                yield break;
            }

            yield return CadPrompt.ForString("Enter block name specification", "*");
            Regex blocks = Wildcard(c.Input.Kind == CadInputKind.Text ? c.Input.Text : "*");
            yield return CadPrompt.ForString("Enter attribute tag specification", "*");
            Regex tags = Wildcard(c.Input.Kind == CadInputKind.Text ? c.Input.Text : "*");
            yield return CadPrompt.ForString("Enter attribute value specification", "*");
            Regex values = Wildcard(c.Input.Kind == CadInputKind.Text ? c.Input.Text : "*");
            yield return CadPrompt.ForSelection("Select Attributes");
            var found = new List<KeyValuePair<string, string>>();

            foreach (string handle in c.Input.Handles)
            {
                if (c.Document.FindEntity(handle) is DxfBlockReference insert && blocks.IsMatch(insert.Name))
                {
                    // An INSERT a click picked gives the attribute clicked alone, as AutoCAD's; one a window took, those inside the
                    // window (or touched by a crossing box); one another option took whole, all of them.
                    List<GeoPoint2> clicks = c.Input.Clicks.Where(p => string.Equals(p.Key, handle, StringComparison.OrdinalIgnoreCase)).Select(p => p.Value).ToList();
                    List<Tuple<DisplayBox, bool>> windows = c.Input.Windows.Where(w => string.Equals(w.Item1, handle, StringComparison.OrdinalIgnoreCase)).Select(w => Tuple.Create(w.Item2, w.Item3)).ToList();
                    HashSet<string> clicked = clicks.Count > 0 ? ClickedAttributes(c, insert, clicks) : windows.Count > 0 ? WindowedAttributes(c, insert, windows) : null;

                    foreach (DxfAttributeReference attribute in insert.AttributeCollection)
                    {
                        if ((clicked == null || clicked.Contains(attribute.Handle)) && tags.IsMatch(attribute.Tag) && values.IsMatch(attribute.TextString ?? string.Empty))
                        {
                            found.Add(new KeyValuePair<string, string>(handle, attribute.Handle));
                        }
                    }
                }
            }

            c.Write(found.Count.ToString(CultureInfo.InvariantCulture) + " attributes selected.");
            string[] options = { "Value", "Position", "Height", "Angle", "Style", "Layer", "Color", "Next" };

            foreach (KeyValuePair<string, string> pair in found)
            {
                while (true)
                {
                    // The attribute edited marked with a cross where it stands, as AutoCAD marks it.
                    yield return CadPrompt.ForKeyword("Enter an option", options, "Next").Marking(HeldAt(c, pair.Key, pair.Value));

                    if (c.Input.IsNone || c.Input.Is("Next"))
                    {
                        break;
                    }

                    CadAttributeRow row = Rows(c.Document, pair.Key).Rows.First(r => string.Equals(r.Handle, pair.Value, StringComparison.OrdinalIgnoreCase));
                    CadAttributeRow old = row.Clone();
                    string option = c.Input.Text;

                    if (option == "Value")
                    {
                        yield return CadPrompt.ForKeyword("Enter type of value modification", new[] { "Change", "Replace" }, "Replace");

                        if (c.Input.Is("Change"))
                        {
                            yield return CadPrompt.ForString("Enter string to change");
                            string from = c.Input.Kind == CadInputKind.Text ? c.Input.Text : string.Empty;
                            yield return CadPrompt.ForString("Enter new string");
                            string to = c.Input.Kind == CadInputKind.Text ? c.Input.Text : string.Empty;
                            int at = from.Length == 0 ? -1 : row.Value.IndexOf(from, StringComparison.Ordinal);

                            if (at < 0)
                            {
                                c.Write("String not found.");
                                continue;
                            }

                            row.Value = row.Value.Substring(0, at) + to + row.Value.Substring(at + from.Length);
                        }
                        else if (c.Input.Is("Replace"))
                        {
                            yield return CadPrompt.ForString("Enter new attribute value");
                            row.Value = c.Input.Kind == CadInputKind.Text ? c.Input.Text : string.Empty;
                        }
                    }
                    else if (option == "Position")
                    {
                        yield return CadPrompt.ForPoint("Specify new text insertion point", null, null, "no change");

                        if (c.Input.IsPoint)
                        {
                            GeoPoint2 to = c.Input.Point;
                            string insert = pair.Key;
                            string attribute = pair.Value;
                            c.Apply(new[] { new CadEditRecord(CadEditRecordKind.PropertyChanged, insert, "Attribute position", null, Values(to.X, to.Y)) }, s =>
                            {
                                DxfAttributeReference a = ((DxfBlockReference)s.Document.FindEntity(insert)).AttributeCollection.First(x => string.Equals(x.Handle, attribute, StringComparison.OrdinalIgnoreCase));
                                GeoPoint3 now = a.Justify == DxfAttachmentPoint.BaseLeft ? a.Position : a.AlignmentPoint;
                                a.TransformBy(GeoTransform3.Translation(new GeoVector3(to.X - now.X, to.Y - now.Y, 0.0)));
                            });
                        }

                        continue;
                    }
                    else if (option == "Height")
                    {
                        yield return CadPrompt.ForDistance("Specify new height", null, null, Length(row.Height));

                        if (c.Input.Kind == CadInputKind.Number)
                        {
                            row.Height = c.Input.Number;
                        }
                    }
                    else if (option == "Angle")
                    {
                        yield return CadPrompt.ForAngle("Specify new rotation angle", null, null, Number(row.Rotation * 180.0 / Math.PI));

                        if (c.Input.Kind == CadInputKind.Number)
                        {
                            row.Rotation = c.Session.Ucs.AngleToWorld(c.Input.Number);
                        }
                    }
                    else if (option == "Style")
                    {
                        yield return CadPrompt.ForString("Enter new text style", row.TextStyle);

                        if (c.Input.Kind == CadInputKind.Text)
                        {
                            row.TextStyle = c.Input.Text.Trim();
                        }
                    }
                    else if (option == "Layer")
                    {
                        yield return CadPrompt.ForString("Enter new layer name", row.Layer);

                        if (c.Input.Kind == CadInputKind.Text)
                        {
                            string layer = c.Input.Text.Trim();

                            if (!c.Database.LayerTable.Has(layer))
                            {
                                c.Write("Cannot find layer \"" + layer + "\".");
                                continue;
                            }

                            row.Layer = c.Database.LayerTable[layer].Name;
                        }
                    }
                    else if (option == "Color")
                    {
                        yield return CadPrompt.ForString("Enter new color", row.ColorIndex == 256 ? "BYLAYER" : row.ColorIndex == 0 ? "BYBLOCK" : row.ColorIndex.ToString(CultureInfo.InvariantCulture));

                        if (c.Input.Kind == CadInputKind.Text)
                        {
                            string typed = c.Input.Text.Trim();

                            if (string.Equals(typed, "BYLAYER", StringComparison.OrdinalIgnoreCase))
                            {
                                row.ColorIndex = 256;
                            }
                            else if (string.Equals(typed, "BYBLOCK", StringComparison.OrdinalIgnoreCase))
                            {
                                row.ColorIndex = 0;
                            }
                            else if (int.TryParse(typed, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index) && index >= 1 && index <= 255)
                            {
                                row.ColorIndex = index;
                            }
                            else
                            {
                                c.Write("Requires a colour from 1 to 255, BYLAYER or BYBLOCK.");
                                continue;
                            }
                        }
                    }

                    if (!Same(row, old))
                    {
                        WriteAttributes(c, new CadAttributeEditRequest(c.Document, pair.Key, null, new[] { row }), new List<CadAttributeRow> { old });
                    }
                }
            }
        }

        /// <summary>The point an attribute of an INSERT is held by: its start on the baseline where it is aligned left there, else its alignment point.</summary>
        private static GeoPoint2 HeldAt(CadCommandContext c, string insert, string attribute)
        {
            DxfAttributeReference a = ((DxfBlockReference)c.Document.FindEntity(insert)).AttributeCollection.First(x => string.Equals(x.Handle, attribute, StringComparison.OrdinalIgnoreCase));
            GeoPoint3 at = a.Justify == DxfAttachmentPoint.BaseLeft ? a.Position : a.AlignmentPoint;
            return new GeoPoint2(at.X, at.Y);
        }

        /// <summary>
        /// The handles of the attributes of an INSERT that clicks picked, as AutoCAD's -ATTEDIT takes the attribute under the
        /// pick box: each shown attribute is the text of the INSERT drawn nearest the point it is held by, and each click takes
        /// the one of those it falls nearest within the pick aperture, the first of the INSERT where two are as near; a click on
        /// the block's own lines takes none.
        /// </summary>
        private static HashSet<string> ClickedAttributes(CadCommandContext c, DxfBlockReference insert, IReadOnlyList<GeoPoint2> clicks)
        {
            var clicked = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<KeyValuePair<string, DisplayText2>> shown = Shown(c, insert);

            foreach (GeoPoint2 click in clicks)
            {
                string picked = null;
                double nearest = double.PositiveInfinity;

                foreach (KeyValuePair<string, DisplayText2> pair in shown)
                {
                    double d = pair.Value.DistanceTo(click.X, click.Y);

                    if (d <= c.Session.PickAperture && d < nearest)
                    {
                        nearest = d;
                        picked = pair.Key;
                    }
                }

                if (picked != null)
                {
                    clicked.Add(picked);
                }
            }

            return clicked;
        }

        /// <summary>
        /// The handles of the attributes of an INSERT that windows took, as AutoCAD's -ATTEDIT takes those a window encloses or
        /// a crossing box touches and not the INSERT's others: each shown attribute judged by its text as drawn.
        /// </summary>
        private static HashSet<string> WindowedAttributes(CadCommandContext c, DxfBlockReference insert, IReadOnlyList<Tuple<DisplayBox, bool>> windows)
        {
            var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (KeyValuePair<string, DisplayText2> pair in Shown(c, insert))
            {
                if (windows.Any(w => w.Item2 ? pair.Value.Touches(w.Item1) : pair.Value.IsInside(w.Item1)))
                {
                    taken.Add(pair.Key);
                }
            }

            return taken;
        }

        /// <summary>Each shown attribute of an INSERT with its text as drawn: the text of the INSERT drawn nearest the point the attribute is held by.</summary>
        private static List<KeyValuePair<string, DisplayText2>> Shown(CadCommandContext c, DxfBlockReference insert)
        {
            var shown = new List<KeyValuePair<string, DisplayText2>>();
            CadSpace space = c.Document.FindSpace(insert.Handle);

            if (space == null)
            {
                return shown;
            }

            List<DisplayText2> texts = c.Document.GetDisplay(space).Items.OfType<DisplayText2>().Where(t => string.Equals(t.Handle, insert.Handle, StringComparison.OrdinalIgnoreCase)).ToList();
            var used = new bool[texts.Count];

            foreach (DxfAttributeReference attribute in insert.AttributeCollection)
            {
                if (attribute.IsErased || attribute.Invisible)
                {
                    continue;
                }

                GeoPoint3 at = attribute.Justify == DxfAttachmentPoint.BaseLeft ? attribute.Position : attribute.AlignmentPoint;
                int best = -1;
                double nearest = double.PositiveInfinity;

                for (int i = 0; i < texts.Count; i++)
                {
                    double d = Math.Sqrt(((texts[i].X - at.X) * (texts[i].X - at.X)) + ((texts[i].Y - at.Y) * (texts[i].Y - at.Y)));

                    if (!used[i] && d < nearest)
                    {
                        nearest = d;
                        best = i;
                    }
                }

                if (best >= 0)
                {
                    used[best] = true;
                    shown.Add(new KeyValuePair<string, DisplayText2>(attribute.Handle, texts[best]));
                }
            }

            return shown;
        }

        /// <summary>A pattern of AutoCAD's wildcards, * and ?, matched whole, letters of either case alike; commas separate several.</summary>
        private static Regex Wildcard(string pattern)
        {
            string[] parts = (string.IsNullOrWhiteSpace(pattern) ? "*" : pattern.Trim()).Split(',');
            string joined = string.Join("|", parts.Select(p => Regex.Escape(p.Trim()).Replace("\\*", ".*").Replace("\\?", ".")));
            return new Regex("^(?:" + joined + ")$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        /// <summary>
        /// ATTSYNC: "Enter an option [?/Name/Select] &lt;Select&gt;:", a block picked or named, "ATTSYNC block GB? [Yes/No]
        /// &lt;Yes&gt;:", then every INSERT of it brought in line with its attribute definitions, as AutoCAD's: one attribute for
        /// each definition not constant, in the block's order, its value kept where the INSERT had the tag; those of tags the
        /// block no longer defines removed; each placed and styled from its definition as the INSERT places it. "?" lists the
        /// blocks with attributes. One undo step.
        /// </summary>
        private static IEnumerable<CadPrompt> AttributeSync(CadCommandContext c)
        {
            string name = null;

            while (name == null)
            {
                yield return CadPrompt.ForKeyword("Enter an option", new[] { "?", "Name", "Select" }, "Select");

                if (c.Input.Is("?"))
                {
                    WriteAttributedBlocks(c);
                }
                else if (c.Input.Is("Name"))
                {
                    while (true)
                    {
                        yield return CadPrompt.ForKeywordOrText("Enter name of block to sync or", new[] { "?" }, null);

                        if (c.Input.Is("?"))
                        {
                            WriteAttributedBlocks(c);
                            continue;
                        }

                        if (c.Input.Kind != CadInputKind.Text || c.Input.Text.Trim().Length == 0)
                        {
                            yield break;
                        }

                        string typed = c.Input.Text.Trim();

                        if (!c.Database.BlockTable.Has(typed) || !NamedBlocks(c.Database).Any(b => string.Equals(b.Name, typed, StringComparison.OrdinalIgnoreCase)))
                        {
                            c.Write("Block \"" + typed + "\" not found.");
                            continue;
                        }

                        name = c.Database.BlockTable[typed].Name;
                        break;
                    }
                }
                else if (c.Input.Is("Select"))
                {
                    while (true)
                    {
                        yield return CadPrompt.ForEntity("Select a block");

                        if (c.Input.Kind != CadInputKind.Selection)
                        {
                            yield break;
                        }

                        if (c.Document.FindEntity(c.Input.Handles[0]) is DxfBlockReference picked && picked.BlockTableRecord != null)
                        {
                            name = picked.BlockTableRecord.Name;
                            break;
                        }

                        c.Write("That object is not a block.");
                    }

                    yield return CadPrompt.ForKeyword("ATTSYNC block " + name + "?", new[] { "Yes", "No" }, "Yes");

                    if (c.Input.Is("No"))
                    {
                        yield break;
                    }
                }
                else
                {
                    yield break;
                }
            }

            if (!c.Database.BlockTable[name].OfType<DxfAttributeDefinition>().Any())
            {
                c.Write("This block has no attribute definitions.");
                yield break;
            }

            SyncAttributes(c, name);
        }

        /// <summary>The names of the blocks with attribute definitions, as ATTSYNC's "?" lists them.</summary>
        private static void WriteAttributedBlocks(CadCommandContext c)
        {
            List<string> names = NamedBlocks(c.Database).Where(b => b.OfType<DxfAttributeDefinition>().Any()).Select(b => b.Name).ToList();
            c.Write("Blocks with attributes:");
            names.ForEach(c.Write);
        }

        /// <summary>
        /// Brings every INSERT of a block in line with its attribute definitions, one edit: those of the layouts named as
        /// changed, those nested in other blocks with those blocks; in the order of the block table and of each block.
        /// </summary>
        internal static void SyncAttributes(CadCommandContext c, string name)
        {
            var planned = new List<CadEditRecord>();
            int count = 0;

            foreach (KeyValuePair<DxfBlockTableRecord, DxfBlockReference> pair in InsertsOf(c.Database, name))
            {
                count++;

                if (pair.Key.IsLayout && c.Document.FindEntity(pair.Value.Handle) != null)
                {
                    planned.Add(new CadEditRecord(CadEditRecordKind.PropertyChanged, pair.Value.Handle, "Attributes", null, name));
                }
                else if (!planned.Exists(r => r.Kind == CadEditRecordKind.TableChanged && string.Equals(r.NewValue, pair.Key.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    planned.Add(new CadEditRecord(CadEditRecordKind.TableChanged, null, "Attributes synchronized", null, pair.Key.Name));
                }
            }

            CadEditResult result = c.Apply(planned, s =>
            {
                List<DxfAttributeDefinition> definitions = Definitions(s.Database.BlockTable[name]);

                foreach (KeyValuePair<DxfBlockTableRecord, DxfBlockReference> pair in InsertsOf(s.Database, name))
                {
                    Sync(pair.Value, definitions);
                }
            });

            if (result.Succeeded)
            {
                c.Write("ATTSYNC complete.");
                GeometryHelperLog.Info("CadCommands: ATTSYNC brought " + count.ToString(CultureInfo.InvariantCulture) + " INSERTs of " + name + " in line with the block.");
            }
        }

        /// <summary>The INSERTs of a block with the block or layout each stands in, in the order of the block table and of each block; external references left out.</summary>
        private static List<KeyValuePair<DxfBlockTableRecord, DxfBlockReference>> InsertsOf(DxfDatabase database, string name)
        {
            var inserts = new List<KeyValuePair<DxfBlockTableRecord, DxfBlockReference>>();

            foreach (DxfBlockTableRecord owner in database.BlockTable)
            {
                if (owner.IsFromExternalReference)
                {
                    continue;
                }

                foreach (DxfEntity entity in owner)
                {
                    if (entity is DxfBlockReference insert && !insert.IsErased && string.Equals(insert.Name, name, StringComparison.OrdinalIgnoreCase))
                    {
                        inserts.Add(new KeyValuePair<DxfBlockTableRecord, DxfBlockReference>(owner, insert));
                    }
                }
            }

            return inserts;
        }

        /// <summary>
        /// Brings the attributes of one INSERT in line with the definitions of its block, in their order: an attribute of a
        /// tag the INSERT has (the first of it not yet taken) is placed and styled again from its definition, its value kept,
        /// where it stands in order; one out of order, or of a tag it has not, is made anew after the others, a definition on
        /// layer 0 giving it the INSERT's layer, as INSERT gives it; one of a tag no longer defined is erased.
        /// </summary>
        private static void Sync(DxfBlockReference insert, List<DxfAttributeDefinition> definitions)
        {
            List<DxfAttributeReference> old = insert.AttributeCollection.ToList();
            var taken = new bool[old.Count];
            GeoTransform3 transform = insert.BlockTransform;
            int last = -1;
            bool appending = false;

            foreach (DxfAttributeDefinition definition in definitions)
            {
                int at = -1;

                for (int i = 0; i < old.Count; i++)
                {
                    if (!taken[i] && string.Equals(old[i].Tag, definition.Tag, StringComparison.OrdinalIgnoreCase))
                    {
                        at = i;
                        break;
                    }
                }

                string value = at >= 0 ? old[at].TextString : null;

                if (at >= 0)
                {
                    taken[at] = true;
                }

                DxfAttributeReference attribute;
                bool made = false;

                if (at >= 0 && !appending && at > last)
                {
                    attribute = old[at];
                    last = at;
                    attribute.SetAttributeFromBlock(definition, transform);
                }
                else
                {
                    appending = true;

                    if (at >= 0)
                    {
                        old[at].Erase();
                    }

                    attribute = new DxfAttributeReference();
                    made = true;
                    attribute.SetAttributeFromBlock(definition, transform);
                }

                if (value != null && attribute.TextString != value)
                {
                    attribute.TextString = value;
                }

                if (string.Equals(definition.Layer, "0", StringComparison.Ordinal) && attribute.Layer != insert.Layer)
                {
                    attribute.Layer = insert.Layer;
                }

                if (made)
                {
                    insert.AttributeCollection.AppendAttribute(attribute);
                }
            }

            for (int i = 0; i < old.Count; i++)
            {
                if (!taken[i])
                {
                    old[i].Erase();
                }
            }
        }

        /// <summary>BASE: "Enter base point &lt;0.0000,0.0000,0.0000&gt;:", the point an INSERT of the drawing as a file puts at its insertion point ($INSBASE).</summary>
        private static IEnumerable<CadPrompt> Base(CadCommandContext c)
        {
            GeoPoint3 now = c.Database.Insbase;
            yield return CadPrompt.ForPoint("Enter base point", null, null, Length(now.X) + "," + Length(now.Y) + "," + Length(now.Z));

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            GeoPoint3 to = new GeoPoint3(c.Input.Point.X, c.Input.Point.Y, now.Z);
            c.Apply(new[] { new CadEditRecord(CadEditRecordKind.SettingChanged, null, "$INSBASE", Values(now.X, now.Y, now.Z), Values(to.X, to.Y, to.Z)) }, s => s.Database.Insbase = to);
        }
    }
}
