using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// In-place editing of a block reference, as AutoCAD 2021's REFEDIT: the objects of the reference's block copied into the
    /// drawing where the reference draws them (the working set), edited there with every command, REFSET adding objects to the
    /// set and taking them out, REFCLOSE writing them back into the block or discarding them. The other objects of the drawing
    /// are locked while it lasts and faded.
    /// </summary>
    /// <remarks>
    /// <para>
    /// How it works. The block keeps its objects while the session lasts; the copies in the drawing are new objects, each tied to
    /// the object of the block it came from in an XRECORD of the named object dictionary (<c>GH_REFEDIT</c>), which also holds the
    /// reference chosen, the settings and the first handle the session gave. Everything the session needs to go on is in the
    /// drawing, so that an undo, a redo, a save and an opening of the file find it as it stood: a drawing saved in the middle of
    /// an edit opens with the edit still open. The references of the block draw it without the objects the copies stand for; REFCLOSE
    /// Save writes each copy into the block as a new object, put through the reference's transformation the other way, and
    /// erases the object it came from; Discard erases the copies. A copy erased in the session leaves the block when saved.
    /// </para>
    /// <para>
    /// The steps of the session are the steps of the commands run in it. Undo stops where the session opened, and once it is
    /// closed its steps are one, "REFEDIT", which one undo takes back whole, as BEDIT's are.
    /// </para>
    /// <para>
    /// AutoCAD 2021's accoreconsole has none of REFEDIT, REFSET and REFCLOSE (they are of the drawing editor), so their words
    /// are the presenter's; the variables XFADECTL and REFEDITNAME were probed there.
    /// </para>
    /// </remarks>
    public sealed partial class CadDocument
    {
        private const string RefEditKey = "GH_REFEDIT";

        private CadRefEdit _refEdit;

        /// <summary>Raised when an in-place edit opens or closes.</summary>
        public event EventHandler RefEditChanged;

        /// <summary>Gets the in-place edit open on this drawing (REFEDIT); null where none is.</summary>
        public CadRefEdit RefEdit => _refEdit;

        /// <summary>
        /// Gets or sets XFADECTL, as AutoCAD's: how far the objects outside the working set are faded while a reference is edited
        /// in place, from 0 to 90 per cent, 70 as AutoCAD 2021 gives it. Kept by the presenter, not in the drawing, as AutoCAD
        /// keeps it in the registry.
        /// </summary>
        public int XFadeControl { get; set; } = 70;

        /// <summary>
        /// Gets or sets whether REFEDIT brings the attribute definitions of the block into the working set, as the Reference Edit
        /// dialog's "Display attribute definitions for editing" does; false, the default, leaves them in the block.
        /// </summary>
        public bool RefEditAttributeDefinitions { get; set; }

        /// <summary>
        /// Gets or sets whether REFEDIT locks the objects outside the working set, as the Reference Edit dialog's "Lock objects not
        /// in working set" does; true by default. A session keeps the setting it opened with.
        /// </summary>
        public bool RefEditLocksObjects { get; set; } = true;

        /// <summary>
        /// The references a point on a reference of the drawing can mean, the outermost first: the reference itself, then each
        /// reference nested in its block whose objects reach the point, and each nested in those, as the Reference Edit dialog
        /// lists them. A reference with nothing nested at the point gives its own level alone.
        /// </summary>
        /// <param name="handle">The handle of the reference in a space.</param>
        /// <param name="at">The point it was picked at, in the drawing; null for none, which gives no nested level.</param>
        /// <param name="aperture">How far from an object's box a point still reaches it.</param>
        /// <returns>The levels, none where the handle is not a block reference of a space.</returns>
        public IReadOnlyList<CadRefEditLevel> FindRefEditLevels(string handle, GeoPoint2? at, double aperture)
        {
            var levels = new List<CadRefEditLevel>();

            if (!(FindEntity(handle) is DxfBlockReference top) || FindSpace(handle) == null)
            {
                return levels;
            }

            levels.Add(new CadRefEditLevel(top.Name, new[] { top.Handle }, top.Name));

            if (at != null && !(top is DxfMInsertBlock))
            {
                Descend(top, new List<string> { top.Handle }, top.Name, top.BlockTransform, at.Value, aperture, levels);
            }

            return levels;
        }

        /// <summary>Lists the references nested in a reference's block that reach a point, and those nested in them, depth first.</summary>
        private static void Descend(
            DxfBlockReference parent,
            List<string> chain,
            string path,
            GeoTransform3 placing,
            GeoPoint2 at,
            double aperture,
            List<CadRefEditLevel> levels
        )
        {
            DxfBlockTableRecord block = parent.BlockTableRecord;

            if (block == null || chain.Count >= 8)
            {
                return;
            }

            foreach (DxfEntity entity in block)
            {
                if (
                    !(entity is DxfBlockReference child)
                    || child is DxfMInsertBlock
                    || child.IsErased
                    || child.Handle == null
                    || child.BlockTableRecord == null
                    || !Reaches(child, placing, at, aperture)
                )
                {
                    continue;
                }

                var deeper = new List<string>(chain) { child.Handle };
                string below = path + " > " + child.Name;
                levels.Add(new CadRefEditLevel(child.Name, deeper.ToArray(), below));
                Descend(child, deeper, below, placing.Multiply(child.BlockTransform), at, aperture, levels);
            }
        }

        /// <summary>Whether the box of the shapes a nested reference places, moved by the transformation of the references outside it, reaches a point within an aperture.</summary>
        private static bool Reaches(DxfBlockReference child, GeoTransform3 placing, GeoPoint2 at, double aperture)
        {
            try
            {
                GeoAabb3 box = child.GeometryExtentsBestFit(placing);
                return !box.IsEmpty
                    && at.X >= box.Min.X - aperture
                    && at.X <= box.Max.X + aperture
                    && at.Y >= box.Min.Y - aperture
                    && at.Y <= box.Max.Y + aperture;
            }
            catch (Exception exception)
                when (exception is ArgumentException
                    || exception is InvalidOperationException
                    || exception is NotSupportedException
                )
            {
                return false;
            }
        }

        /// <summary>
        /// The object of the block a reference chain edits that a point reaches: the one whose box, put where the reference draws
        /// it, holds the point within the aperture, the smallest where several do, the first of the block on a tie; null for none.
        /// This is how REFEDIT's "Prompt to select nested objects" takes an object, as the block's objects are not picked in the
        /// drawing.
        /// </summary>
        /// <param name="chain">The handles of the references, outermost first, as <see cref="CadRefEditLevel.Chain"/> has them.</param>
        /// <param name="at">The point, in the drawing.</param>
        /// <param name="aperture">How far from an object's box the point still reaches it.</param>
        /// <returns>The handle of the object of the block, or null.</returns>
        public string FindRefEditObject(IReadOnlyList<string> chain, GeoPoint2 at, double aperture)
        {
            if (!ResolveChain(chain, out List<DxfBlockReference> inserts, out _))
            {
                return null;
            }

            GeoTransform3 placing = ChainTransform(inserts);
            string found = null;
            double least = double.MaxValue;

            foreach (
                DxfEntity entity in Candidates(inserts[inserts.Count - 1].BlockTableRecord, RefEditAttributeDefinitions)
            )
            {
                try
                {
                    GeoAabb3 box = entity.GetTransformedCopy(placing).GeometricExtents;

                    if (
                        box.IsEmpty
                        || at.X < box.Min.X - aperture
                        || at.X > box.Max.X + aperture
                        || at.Y < box.Min.Y - aperture
                        || at.Y > box.Max.Y + aperture
                    )
                    {
                        continue;
                    }

                    double area = (box.Max.X - box.Min.X) * (box.Max.Y - box.Min.Y);

                    if (area < least)
                    {
                        least = area;
                        found = entity.Handle;
                    }
                }
                catch (Exception exception)
                    when (exception is ArgumentException
                        || exception is InvalidOperationException
                        || exception is NotSupportedException
                    )
                {
                    // An object that cannot be placed is not one the pick can reach.
                }
            }

            return found;
        }

        /// <summary>
        /// Opens the in-place edit of a reference, as REFEDIT: the objects of its block copied into the drawing where the
        /// reference draws them, the working set. One undo step while the session lasts. The objects the reference's placing
        /// cannot hold (a circle under a scale unlike along its axes) are left in the block.
        /// </summary>
        /// <param name="chain">The handles of the references: the one in a space first, then, to edit a nested reference, each
        /// reference nested in the block of the one before it, as <see cref="FindRefEditLevels"/> gives them.</param>
        /// <param name="objects">The handles of the objects of the block to take, as "Prompt to select nested objects" does; null
        /// takes every object, as "Automatically select all nested objects" does.</param>
        /// <returns>What came of it: refused where an edit is open already, the Block Editor is, a command's group of edits is, the
        /// chain names no reference, or the block is an external reference, anonymous (a dynamic block's, a dimension's), an array
        /// or has no object to edit.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public CadEditResult OpenRefEdit(IReadOnlyList<string> chain, IReadOnlyList<string> objects)
        {
            if (chain == null)
            {
                throw new ArgumentNullException(nameof(chain));
            }

            if (_blockEditor != null)
            {
                return CadEditResult.Refuse(CadBlockEditor.NotAllowed);
            }

            if (_refEdit != null)
            {
                return CadEditResult.Refuse(_refEdit.NotAllowedFor("REFEDIT"));
            }

            if (_groupDepth > 0)
            {
                return CadEditResult.Refuse("A command is running: end it first.");
            }

            if (!ResolveChain(chain, out List<DxfBlockReference> inserts, out string refusal))
            {
                return CadEditResult.Refuse(refusal);
            }

            CadSpace host = FindSpace(inserts[0].Handle);
            DxfBlockReference last = inserts[inserts.Count - 1];
            DxfBlockTableRecord block = last.BlockTableRecord;

            if (host == null || host.IsBlockEditor)
            {
                return CadEditResult.Refuse(CadRefEdit.NotAValidReference);
            }

            if (inserts.Exists(i => i is DxfMInsertBlock))
            {
                return CadEditResult.Refuse("A block array cannot be edited in place.");
            }

            if (inserts.Exists(i => i.BlockTableRecord == null || i.BlockTableRecord.IsFromExternalReference))
            {
                return CadEditResult.Refuse("External references cannot be edited in place in this version.");
            }

            if (inserts.Exists(i => i.BlockTableRecord.IsAnonymous))
            {
                return CadEditResult.Refuse(
                    block.Name.StartsWith("*D", StringComparison.OrdinalIgnoreCase)
                    || block.Name.StartsWith("*T", StringComparison.OrdinalIgnoreCase)
                        ? CadRefEdit.DimensionOrTable
                        : CadRefEdit.Anonymous
                );
            }

            if (inserts.Exists(i => !IsUniform(i.ScaleFactors)))
            {
                return CadEditResult.Refuse(CadRefEdit.NonUniform);
            }

            if (Database.NamedObjectsDictionary == null)
            {
                return CadEditResult.Refuse("This drawing cannot hold an in-place edit.");
            }

            GeoTransform3 placing = ChainTransform(inserts);

            if (!placing.TryGetInverse(out _))
            {
                return CadEditResult.Refuse("The reference's scale cannot be undone.");
            }

            List<DxfEntity> taken = Candidates(block, RefEditAttributeDefinitions).ToList();

            if (objects != null)
            {
                var wanted = new HashSet<string>(objects, StringComparer.OrdinalIgnoreCase);
                taken = taken.Where(e => wanted.Contains(e.Handle)).ToList();
            }

            if (taken.Count == 0)
            {
                return CadEditResult.Refuse(CadRefEdit.NoObjects);
            }

            EnsureStart();
            int start = _position;
            ulong first = HandseedOf(Database);
            var header = new RefEditHeader(
                block.Name,
                first,
                chain.ToArray(),
                RefEditLocksObjects,
                RefEditAttributeDefinitions
            );
            var pairs = new List<RefEditPair>();
            int skipped = 0;
            CadEditResult result = ApplyEdit(
                "REFEDIT",
                new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "Reference edit", null, block.Name) },
                s =>
                {
                    pairs.Clear();
                    skipped = 0;

                    foreach (DxfEntity source in taken)
                    {
                        DxfEntity copy;

                        try
                        {
                            copy = source.GetTransformedCopy(placing);
                        }
                        catch (Exception exception)
                            when (exception is ArgumentException
                                || exception is InvalidOperationException
                                || exception is NotSupportedException
                            )
                        {
                            skipped++;
                            continue;
                        }

                        s.Append(host, copy);
                        pairs.Add(new RefEditPair(copy.Handle, source.Handle, false));
                    }

                    if (pairs.Count == 0)
                    {
                        throw new InvalidOperationException(CadRefEdit.NoObjects);
                    }

                    WriteRefEdit(s.Database, header, pairs);
                }
            );

            if (!result.Succeeded)
            {
                return result;
            }

            _refEdit = new CadRefEdit(this, header, start, _position) { Skipped = skipped };
            Selection.Clear();

            // The step announced itself before the edit was open, so the spaces are drawn again now that the references draw the block without the copies.
            Announce(
                CadChangeKind.Edit,
                "REFEDIT",
                new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "Reference edit", null, block.Name) }
            );
            GeometryHelperLog.Info(
                "CadDocument: REFEDIT opened "
                    + block.Name
                    + " with "
                    + pairs.Count.ToString(CultureInfo.InvariantCulture)
                    + " objects."
            );
            RefEditChanged?.Invoke(this, EventArgs.Empty);
            return result;
        }

        /// <summary>
        /// Adds objects of the drawing to the working set, as REFSET Add: they are written into the block when the edit is
        /// saved, and the objects of the block that were taken out of the set (<see cref="RemoveFromRefEdit"/>) go back into it.
        /// Objects that are in the set already, are not in the space the reference stands in, or are the reference itself are
        /// left out. One undo step.
        /// </summary>
        /// <param name="handles">The handles of the objects.</param>
        /// <returns>What came of it: refused where no edit is open or a command's group of edits is; done with no record where no
        /// object could be added.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the handles are null.</exception>
        public CadEditResult AddToRefEdit(IReadOnlyList<string> handles)
        {
            string refused = RefEditRefused(handles);

            if (refused != null)
            {
                return CadEditResult.Refuse(refused);
            }

            RefEditHeader header = _refEdit.Header;
            List<RefEditPair> pairs = ReadRefEditPairs(Database, out _);
            CadSpace host = FindSpace(header.Chain[0]);
            var added = new List<RefEditPair>();
            var back = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string handle in handles.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                DxfEntity entity = FindEntity(handle);

                if (
                    entity == null
                    || host == null
                    || !ReferenceEquals(FindSpace(handle), host)
                    || string.Equals(handle, header.Chain[0], StringComparison.OrdinalIgnoreCase)
                )
                {
                    continue;
                }

                int at = pairs.FindIndex(p => string.Equals(p.Host, handle, StringComparison.OrdinalIgnoreCase));

                if (at < 0)
                {
                    added.Add(new RefEditPair(entity.Handle, RefEditPair.None, false));
                }
                else if (pairs[at].Released)
                {
                    back.Add(handle);
                }
            }

            if (added.Count == 0 && back.Count == 0)
            {
                return CadEditResult.Done(Array.Empty<CadEditRecord>());
            }

            return ApplyEdit(
                "REFSET",
                new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "Working set", null, header.Name) },
                s =>
                {
                    List<RefEditPair> now = ReadRefEditPairs(s.Database, out _);

                    for (int i = 0; i < now.Count; i++)
                    {
                        if (back.Contains(now[i].Host))
                        {
                            now[i] = new RefEditPair(now[i].Host, now[i].Original, false);
                        }
                    }

                    now.AddRange(added);
                    WriteRefEdit(s.Database, header, now);
                }
            );
        }

        /// <summary>
        /// Takes objects out of the working set, as REFSET Remove: they stay in the drawing as its own objects, and the objects of
        /// the block they came from leave the block when the edit is saved, and come back to it if the edit is discarded; an object
        /// added to the set from the drawing is simply the drawing's again. Objects not in the set are left out. One undo step.
        /// </summary>
        /// <param name="handles">The handles of the objects.</param>
        /// <returns>What came of it: refused where no edit is open or a command's group of edits is; done with no record where no
        /// object was in the set.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the handles are null.</exception>
        public CadEditResult RemoveFromRefEdit(IReadOnlyList<string> handles)
        {
            string refused = RefEditRefused(handles);

            if (refused != null)
            {
                return CadEditResult.Refuse(refused);
            }

            RefEditHeader header = _refEdit.Header;
            var leaving = new HashSet<string>(handles, StringComparer.OrdinalIgnoreCase);
            List<RefEditPair> pairs = ReadRefEditPairs(Database, out _);

            if (!pairs.Exists(p => !p.Released && leaving.Contains(p.Host)))
            {
                return CadEditResult.Done(Array.Empty<CadEditRecord>());
            }

            return ApplyEdit(
                "REFSET",
                new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "Working set", null, header.Name) },
                s =>
                {
                    var kept = new List<RefEditPair>();

                    foreach (RefEditPair pair in ReadRefEditPairs(s.Database, out _))
                    {
                        if (pair.Released || !leaving.Contains(pair.Host))
                        {
                            kept.Add(pair);
                        }
                        else if (pair.Original != RefEditPair.None)
                        {
                            kept.Add(new RefEditPair(pair.Host, pair.Original, true));
                        }
                    }

                    WriteRefEdit(s.Database, header, kept);
                }
            );
        }

        /// <summary>Whether the scale factors of a reference are equal in size, as REFEDIT asks; a factor below nought only mirrors.</summary>
        private static bool IsUniform(GeoVector3 scale)
        {
            double x = Math.Abs(scale.X);
            double y = Math.Abs(scale.Y);
            double z = Math.Abs(scale.Z);
            double most = Math.Max(x, Math.Max(y, z));
            return Math.Abs(x - y) <= 1E-9 * most && Math.Abs(x - z) <= 1E-9 * most;
        }

        /// <summary>Whether the block of the reference a chain ends in holds attribute definitions, which REFEDIT asks about.</summary>
        internal bool RefEditBlockHasAttributeDefinitions(IReadOnlyList<string> chain) =>
            ResolveChain(chain, out List<DxfBlockReference> inserts, out _)
            && inserts[inserts.Count - 1].BlockTableRecord.OfType<DxfAttributeDefinition>().Any(e => !e.IsErased);

        /// <summary>
        /// What REFCLOSE Save will write into the block, for the lines AutoCAD says after it: the objects of the working set that
        /// came from the drawing (added), the objects of the block erased or taken out of the set (removed), and the references of
        /// the block drawn anew (updated).
        /// </summary>
        internal void RefEditSaveCounts(out int added, out int removed, out int updated)
        {
            added = 0;
            removed = 0;
            updated = 0;

            if (_refEdit == null || !ResolveChain(_refEdit.Header.Chain, out List<DxfBlockReference> inserts, out _))
            {
                return;
            }

            DxfBlockTableRecord block = inserts[inserts.Count - 1].BlockTableRecord;
            var originals = new HashSet<string>(
                block.Where(e => !e.IsErased && e.Handle != null).Select(e => e.Handle),
                StringComparer.OrdinalIgnoreCase
            );

            foreach (RefEditPair pair in ReadRefEditPairs(Database, out _))
            {
                bool there = !pair.Released && FindEntity(pair.Host) != null;

                if (pair.Original == RefEditPair.None)
                {
                    added += there ? 1 : 0;
                }
                else if (!there && originals.Contains(pair.Original))
                {
                    removed++;
                }
            }

            foreach (DxfBlockTableRecord record in Database.BlockTable)
            {
                updated += record
                    .OfType<DxfBlockReference>()
                    .Count(r => !r.IsErased && ReferenceEquals(r.BlockTableRecord, block));
            }
        }

        /// <summary>Why the working set cannot be changed now; null where it can.</summary>
        private string RefEditRefused(IReadOnlyList<string> handles)
        {
            if (handles == null)
            {
                throw new ArgumentNullException(nameof(handles));
            }

            if (_refEdit == null)
            {
                return CadRefEdit.NotEditing;
            }

            return _groupDepth > 0 ? "A command is running: end it first." : null;
        }

        /// <summary>
        /// Closes the in-place edit, as REFCLOSE: saved, each object of the working set written into the block as a new object put
        /// through the reference's transformation the other way and the object it came from erased (an object erased in the
        /// drawing leaves the block, one taken out of the set leaves it too), every reference of the block drawn anew; or
        /// discarded, the copies erased and the block as it was. The objects drawn in the drawing that were never added stay in it. The
        /// session's steps become one, "REFEDIT", which one undo takes back.
        /// </summary>
        /// <param name="save">Whether the changes are saved; false discards them.</param>
        /// <returns>What came of it: refused where no edit is open or a command's group of edits is; failed, the session still
        /// open, where an object cannot be put back through the reference's placing.</returns>
        public CadEditResult CloseRefEdit(bool save)
        {
            if (_refEdit == null)
            {
                return CadEditResult.Refuse(CadRefEdit.NotEditing);
            }

            if (_groupDepth > 0)
            {
                return CadEditResult.Refuse("A command is running: end it first.");
            }

            CadRefEdit edit = _refEdit;
            RefEditHeader header = edit.Header;
            string name = header.Name;
            CadEditResult result = ApplyEdit(
                "REFCLOSE",
                new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "Reference edit", name, null) },
                s => WriteBack(s.Database, header, save)
            );

            if (!result.Succeeded)
            {
                return result;
            }

            var redrawn = new CadEditRecord(CadEditRecordKind.TableChanged, null, "Redefine block", null, name);
            Merge(edit.Start, "REFEDIT", redrawn);
            _refEdit = null;
            Selection.Clear();
            Announce(CadChangeKind.Edit, "REFCLOSE", new[] { redrawn });
            GeometryHelperLog.Info("CadDocument: REFCLOSE closed " + name + (save ? ", saved." : ", discarded."));
            RefEditChanged?.Invoke(this, EventArgs.Empty);
            return result;
        }

        /// <summary>Writes the working set back into the block, or erases its copies, and takes the XRECORD out; run inside the closing edit.</summary>
        private void WriteBack(DxfDatabase database, RefEditHeader header, bool save)
        {
            if (!ResolveChain(header.Chain, out List<DxfBlockReference> inserts, out string refusal))
            {
                throw new InvalidOperationException(refusal);
            }

            DxfBlockTableRecord block = inserts[inserts.Count - 1].BlockTableRecord;
            GeoTransform3 inverse = ChainTransform(inserts).Inverse();
            var originals = new Dictionary<string, DxfEntity>(StringComparer.OrdinalIgnoreCase);

            foreach (DxfEntity entity in block)
            {
                if (entity.Handle != null)
                {
                    originals[entity.Handle] = entity;
                }
            }

            foreach (RefEditPair pair in ReadRefEditPairs(database, out _))
            {
                DxfEntity host = FindEntity(pair.Host);
                DxfEntity original = null;

                if (pair.Original != RefEditPair.None)
                {
                    originals.TryGetValue(pair.Original, out original);
                }

                if (save)
                {
                    if (!pair.Released && host != null)
                    {
                        block.AppendEntity(host.GetTransformedCopy(inverse));
                        host.Erase();
                    }

                    if (original != null && !original.IsErased)
                    {
                        original.Erase();
                    }
                }
                else if (pair.Original != RefEditPair.None && host != null)
                {
                    host.Erase();
                }
            }

            database.NamedObjectsDictionary.Remove(RefEditKey);
        }

        /// <summary>Resolves a chain of handles into the references: the first in a space, each next nested in the block of the one before.</summary>
        private bool ResolveChain(IReadOnlyList<string> chain, out List<DxfBlockReference> inserts, out string refusal)
        {
            inserts = new List<DxfBlockReference>();
            refusal = CadRefEdit.NotAValidReference;

            if (chain == null || chain.Count == 0 || !(FindEntity(chain[0]) is DxfBlockReference top))
            {
                return false;
            }

            inserts.Add(top);

            for (int i = 1; i < chain.Count; i++)
            {
                DxfBlockTableRecord block = inserts[i - 1].BlockTableRecord;
                DxfBlockReference next = block
                    ?.OfType<DxfBlockReference>()
                    .FirstOrDefault(r =>
                        !r.IsErased && string.Equals(r.Handle, chain[i], StringComparison.OrdinalIgnoreCase)
                    );

                if (next == null)
                {
                    return false;
                }

                inserts.Add(next);
            }

            refusal = null;
            return true;
        }

        /// <summary>The transformation from the coordinates of the last block of a chain to the drawing: each reference's placing, the outer one applied last.</summary>
        private static GeoTransform3 ChainTransform(List<DxfBlockReference> inserts)
        {
            GeoTransform3 placing = inserts[0].BlockTransform;

            for (int i = 1; i < inserts.Count; i++)
            {
                placing = placing.Multiply(inserts[i].BlockTransform);
            }

            return placing;
        }

        /// <summary>The objects of a block the working set can take: those not erased, the attribute definitions only where asked.</summary>
        private static IEnumerable<DxfEntity> Candidates(DxfBlockTableRecord block, bool attributeDefinitions) =>
            block.Where(e =>
                !e.IsErased && e.Handle != null && (attributeDefinitions || !(e is DxfAttributeDefinition))
            );

        /// <summary>The first handle a drawing will give next, <c>$HANDSEED</c>; 0 where it gives none.</summary>
        private static ulong HandseedOf(DxfDatabase database) =>
            ulong.TryParse(
                database.Handseed ?? string.Empty,
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture,
                out ulong seed
            )
                ? seed
                : 0UL;

        /// <summary>Picks the edit up again where a drawing holds one: opened from a file saved in the middle of it. The session's steps are those since.</summary>
        private CadRefEdit ResumeRefEdit()
        {
            RefEditHeader header = ReadRefEditHeader(Database);
            return header == null ? null : new CadRefEdit(this, header, 0, 0);
        }

        /// <summary>
        /// The test of whether the REFEDIT lock holds an object, by its handle: true for an object that is not in the working set and
        /// was in the drawing before the session opened; null where nothing is locked.
        /// </summary>
        internal Func<string, bool> RefEditLockTest()
        {
            CadRefEdit edit = _refEdit;

            if (edit == null || !edit.LocksObjects)
            {
                return null;
            }

            var open = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (RefEditPair pair in ReadRefEditPairs(Database, out _))
            {
                if (!pair.Released)
                {
                    open.Add(pair.Host);
                }
            }

            ulong first = edit.Header.FirstNewHandle;
            return handle =>
                handle != null
                && !open.Contains(handle)
                && !(
                    ulong.TryParse(handle, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong made)
                    && made >= first
                );
        }

        /// <summary>
        /// The handles of the objects the canvas fades while an edit is open, in ordinal order: those the lock holds (the ones in
        /// the spaces that are not in the working set and were there before the session). Empty where no edit is open or the lock is off.
        /// </summary>
        /// <returns>The handles.</returns>
        public IReadOnlyList<string> RefEditLockedHandles()
        {
            Func<string, bool> locked = RefEditLockTest();
            var handles = new List<string>();

            if (locked == null)
            {
                return handles;
            }

            foreach (CadSpace space in _spaces)
            {
                foreach (DxfEntity entity in space.Block)
                {
                    string handle = entity.IsErased ? null : entity.Handle;

                    if (handle != null && locked(handle))
                    {
                        handles.Add(handle);
                    }
                }
            }

            handles.Sort(StringComparer.Ordinal);
            return handles;
        }

        /// <summary>The handles of the working set's objects still in the drawing, in ordinal order.</summary>
        internal IReadOnlyList<string> RefEditWorkingSet()
        {
            var set = new List<string>();

            if (_refEdit == null)
            {
                return set;
            }

            foreach (RefEditPair pair in ReadRefEditPairs(Database, out _))
            {
                if (!pair.Released && FindEntity(pair.Host) != null)
                {
                    set.Add(pair.Host);
                }
            }

            set.Sort(StringComparer.Ordinal);
            return set;
        }

        /// <summary>
        /// The handles of the objects of blocks the references draw without while an edit is open: those the copies in the
        /// drawing stand for. Null where no edit is open.
        /// </summary>
        internal HashSet<string> RefEditHiddenOriginals()
        {
            if (_refEdit == null)
            {
                return null;
            }

            var hidden = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (RefEditPair pair in ReadRefEditPairs(Database, out _))
            {
                if (pair.Original != RefEditPair.None)
                {
                    hidden.Add(pair.Original);
                }
            }

            return hidden.Count == 0 ? null : hidden;
        }

        /// <summary>The XRECORD of the edit in a drawing, null where it has none.</summary>
        private static DxfXrecord RefEditRecord(DxfDatabase database)
        {
            DxfDBDictionary named = database.NamedObjectsDictionary;
            return named != null && named.Contains(RefEditKey) ? named.GetAt(RefEditKey) as DxfXrecord : null;
        }

        /// <summary>The settings of the edit a drawing holds; null where it holds none.</summary>
        private static RefEditHeader ReadRefEditHeader(DxfDatabase database)
        {
            DxfXrecord record = RefEditRecord(database);

            if (record == null)
            {
                return null;
            }

            IReadOnlyList<DxfTypedValue> data = record.Data;

            if (
                data.Count < 4
                || !ulong.TryParse(data[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong first)
            )
            {
                return null;
            }

            string flags = data[3].Value ?? string.Empty;
            return new RefEditHeader(
                data[0].Value,
                first,
                data[2].Value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries),
                flags.IndexOf('L') >= 0,
                flags.IndexOf('A') >= 0
            );
        }

        /// <summary>The copies of the working set and what each stands for, in the order the session made them.</summary>
        private static List<RefEditPair> ReadRefEditPairs(DxfDatabase database, out RefEditHeader header)
        {
            header = null;
            var pairs = new List<RefEditPair>();
            DxfXrecord record = RefEditRecord(database);

            if (record == null)
            {
                return pairs;
            }

            header = ReadRefEditHeader(database);
            IReadOnlyList<DxfTypedValue> data = record.Data;

            for (int i = 4; i + 2 < data.Count; i += 3)
            {
                pairs.Add(new RefEditPair(data[i].Value, data[i + 1].Value, data[i + 2].Value == "R"));
            }

            return pairs;
        }

        /// <summary>Writes the XRECORD of the edit: its settings, then three groups for each copy.</summary>
        private static void WriteRefEdit(DxfDatabase database, RefEditHeader header, List<RefEditPair> pairs)
        {
            var data = new List<DxfTypedValue>(4 + pairs.Count * 3)
            {
                new DxfTypedValue(1, header.Name),
                new DxfTypedValue(2, header.FirstNewHandle.ToString("X", CultureInfo.InvariantCulture)),
                new DxfTypedValue(3, string.Join(",", header.Chain)),
                new DxfTypedValue(
                    4,
                    (header.LocksObjects ? "L" : string.Empty)
                        + (header.AttributeDefinitions ? "A" : string.Empty)
                        + "-"
                ),
            };

            foreach (RefEditPair pair in pairs)
            {
                data.Add(new DxfTypedValue(1, pair.Host));
                data.Add(new DxfTypedValue(2, pair.Original));
                data.Add(new DxfTypedValue(3, pair.Released ? "R" : "M"));
            }

            database.NamedObjectsDictionary.SetAt(RefEditKey, new DxfXrecord { Data = data });
        }
    }

    /// <summary>One copy of the working set: the handle of the object in the drawing, the one of the block it came from, and whether it was taken out of the set.</summary>
    internal readonly struct RefEditPair
    {
        /// <summary>The text for an object added from the drawing, which no object of the block stands behind.</summary>
        internal const string None = "0";

        internal RefEditPair(string host, string original, bool released)
        {
            Host = host;
            Original = original;
            Released = released;
        }

        internal string Host { get; }

        internal string Original { get; }

        internal bool Released { get; }
    }

    /// <summary>What an in-place edit is kept as in the drawing: the block, the handle its objects were numbered from, the references, the settings.</summary>
    internal sealed class RefEditHeader
    {
        internal RefEditHeader(string name, ulong first, string[] chain, bool locks, bool attributeDefinitions)
        {
            Name = name;
            FirstNewHandle = first;
            Chain = chain;
            LocksObjects = locks;
            AttributeDefinitions = attributeDefinitions;
        }

        internal string Name { get; }

        internal ulong FirstNewHandle { get; }

        internal string[] Chain { get; }

        internal bool LocksObjects { get; }

        internal bool AttributeDefinitions { get; }
    }

    /// <summary>One reference the Reference Edit dialog lists: its block's name and the chain of references that reach it.</summary>
    public sealed class CadRefEditLevel
    {
        internal CadRefEditLevel(string name, string[] chain, string path)
        {
            Name = name;
            Chain = chain;
            Path = path;
        }

        /// <summary>Gets the name of the reference's block.</summary>
        public string Name { get; }

        /// <summary>Gets the handles of the references, the one in a space first and the reference itself last.</summary>
        public IReadOnlyList<string> Chain { get; }

        /// <summary>Gets the level as the command line writes it: "A" for a reference in a space, "A &gt; B" for B nested in it.</summary>
        public string Path { get; }

        /// <inheritdoc/>
        public override string ToString() => Path;
    }

    /// <summary>
    /// The in-place edit open on a drawing (<see cref="CadDocument.RefEdit"/>): the block being edited, the references that
    /// lead to it, the working set and the settings the edit opened with.
    /// </summary>
    public sealed class CadRefEdit
    {
        /// <summary>What REFSET and REFCLOSE say where no reference is being edited, in AutoCAD 2021's words.</summary>
        public const string NotEditing =
            "** Command not allowed unless a reference is checked out with REFEDIT command **";

        internal const string NotAValidReference = "Object is not a valid reference";
        internal const string NoObjects = "** At least one object must be selected to edit a reference **";
        internal const string Anonymous = "** Cannot REFEDIT anonymous block **";
        internal const string DimensionOrTable = "** Cannot REFEDIT dimension or table blocks **";
        internal const string NonUniform = "** Cannot REFEDIT a non-uniformly scaled block **";

        private readonly CadDocument _document;

        internal CadRefEdit(CadDocument document, RefEditHeader header, int start, int floor)
        {
            _document = document;
            Header = header;
            Start = start;
            Floor = floor;
        }

        internal RefEditHeader Header { get; }

        /// <summary>The undo position before REFEDIT, which the session's steps are merged from.</summary>
        internal int Start { get; }

        /// <summary>The undo position the session opened at, which undo stops at.</summary>
        internal int Floor { get; }

        /// <summary>Gets how many objects the reference's placing could not hold, left in the block, when the edit opened.</summary>
        public int Skipped { get; internal set; }

        /// <summary>Gets the name of the block being edited: REFEDITNAME.</summary>
        public string Name => Header.Name;

        /// <summary>
        /// What a command that cannot run while a reference is edited says, in AutoCAD 2021's words: a second REFEDIT names the
        /// block checked out, QSAVE and SAVEAS say the drawing cannot be saved, any other (BEDIT) is named.
        /// </summary>
        /// <param name="command">The command, "-" of its typed form left off if given.</param>
        /// <returns>The line.</returns>
        public string NotAllowedFor(string command)
        {
            string name = (command ?? string.Empty).TrimStart('-').ToUpperInvariant();

            if (name == "REFEDIT")
            {
                return "** Command not allowed, " + Name + " already checked out for editing **";
            }

            return name == "QSAVE" || name == "SAVEAS"
                ? "** Drawing save not allowed during reference editing **"
                : "** " + name + " command not allowed during reference editing **";
        }

        /// <summary>Gets the handles of the references that lead to the block, the one in a space first; one for a reference of a space, two for a reference nested in it.</summary>
        public IReadOnlyList<string> Chain => Header.Chain;

        /// <summary>Gets whether the objects outside the working set are locked, as the edit opened.</summary>
        public bool LocksObjects => Header.LocksObjects;

        /// <summary>Gets whether the attribute definitions of the block are in the working set, as the edit opened.</summary>
        public bool ShowsAttributeDefinitions => Header.AttributeDefinitions;

        /// <summary>
        /// Gets the handles of the objects in the working set that are still in the drawing, in ordinal order: the copies of the
        /// block's objects and the objects added since, those erased or taken out left out.
        /// </summary>
        public IReadOnlyList<string> WorkingSet => _document.RefEditWorkingSet();
    }
}
