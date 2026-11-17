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
    /// An external reference is edited the same way: the objects of its drawing's model space are copied into the drawing
    /// (the drawing the reference names is only read, and stays as it is while the session lasts), and REFCLOSE Save writes
    /// them back into the file, the names <c>XREF|NAME</c> and <c>$0$NAME</c> (see <see cref="RefEditUniqueNames"/>) becoming
    /// the names the file gave. The file is written whole, DWG or DXF, in the version it had, and read again, so that every
    /// reference of it shows the change at once.
    /// </para>
    /// <para>
    /// AutoCAD 2021's accoreconsole has none of REFEDIT, REFSET and REFCLOSE (they are of the drawing editor), so their words
    /// are those of the string table of AcRefEdRes.dll, its own resource file; the variables XFADECTL and REFEDITNAME were probed there.
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
        /// Gets or sets whether REFEDIT of an external reference gives the layers of the working set names of their own, as the
        /// Reference Edit dialog's "Enable unique layer and symbol names" does; true by default. On, the layer WALL of the
        /// reference's drawing is the layer <c>$0$WALL</c> in this drawing while the edit lasts (the number raised past a name
        /// this drawing has), a copy of it no layer of this drawing can clash with, and goes back to WALL in the file when
        /// saved; off, it stays WALL and shares the layer of this drawing of that name, if there is one. A session keeps the
        /// setting it opened with; the edit of a block, whose objects stand on this drawing's own layers, is not changed by it.
        /// </summary>
        public bool RefEditUniqueNames { get; set; } = true;

        /// <summary>
        /// Gets the lines AutoCAD 2021 says after REFCLOSE Save of an external reference (the symbols added to the reference's
        /// file, how many references were updated, that it was reloaded), those of the last one; empty before one.
        /// </summary>
        internal IReadOnlyList<string> RefEditNotes { get; private set; } = Array.Empty<string>();

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

            string file = ExternalFileOf(top, DrawingFolder);
            levels.Add(
                new CadRefEditLevel(
                    top.Name,
                    new[] { top.Handle },
                    top.Name,
                    top.BlockTableRecord != null && top.BlockTableRecord.IsFromExternalReference,
                    file
                )
            );

            if (at != null && !(top is DxfMInsertBlock))
            {
                DxfBlockTableRecord topBlock = top.BlockTableRecord;
                CadXrefContent content =
                    topBlock != null && topBlock.IsFromExternalReference
                        ? Xrefs.ContentOf(topBlock, DrawingFolder, out _)
                        : null;
                Descend(
                    top,
                    new List<string> { top.Handle },
                    top.Name,
                    PlacingOf(top, content),
                    content?.FullPath,
                    content,
                    at.Value,
                    aperture,
                    levels
                );
            }

            return levels;
        }

        /// <summary>
        /// The full path of the file the INSERT of an external reference names, found as AutoCAD looks for it from a folder; null
        /// for an INSERT of any other block and for a reference whose file is not found.
        /// </summary>
        private string ExternalFileOf(DxfBlockReference insert, string folder)
        {
            DxfBlockTableRecord block = insert.BlockTableRecord;
            return block != null && block.IsFromExternalReference ? Xrefs.Locate(block.PathName, folder) : null;
        }

        /// <summary>
        /// The transformation an INSERT puts its block's coordinates through, <see cref="DxfBlockReference.BlockTransform"/>; for
        /// an external reference the drawing it names as well, which draws with its base point (<c>$INSBASE</c>) at the
        /// block's origin, so that a point of it moves by the block's origin less that base point first.
        /// </summary>
        private static GeoTransform3 PlacingOf(DxfBlockReference insert, CadXrefContent xref)
        {
            if (xref == null)
            {
                return insert.BlockTransform;
            }

            GeoPoint3 origin = insert.BlockTableRecord.Origin;
            GeoPoint3 basePoint = GeoPoint3.Origin;

            try
            {
                basePoint = xref.Database.Insbase;
            }
            catch (Exception exception)
                when (exception is ArgumentException
                    || exception is InvalidOperationException
                    || exception is FormatException
                )
            {
                basePoint = GeoPoint3.Origin;
            }

            return insert.BlockTransform.Multiply(GeoTransform3.Translation(origin - basePoint));
        }

        /// <summary>
        /// Lists the references nested in a reference's block, or in the drawing an external reference names, that reach a point,
        /// and those nested in them, depth first. The blocks of a drawing an external reference names are not listed: they are
        /// not edited in place.
        /// </summary>
        private void Descend(
            DxfBlockReference parent,
            List<string> chain,
            string path,
            GeoTransform3 placing,
            string file,
            CadXrefContent content,
            GeoPoint2 at,
            double aperture,
            List<CadRefEditLevel> levels
        )
        {
            DxfBlockTableRecord block = content != null ? content.Database.ModelSpace : parent.BlockTableRecord;

            if (block == null || chain.Count >= 8)
            {
                return;
            }

            string folder = content != null ? System.IO.Path.GetDirectoryName(content.FullPath) : DrawingFolder;

            foreach (DxfEntity entity in block)
            {
                if (
                    !(entity is DxfBlockReference child)
                    || child is DxfMInsertBlock
                    || child.IsErased
                    || child.Handle == null
                    || child.BlockTableRecord == null
                )
                {
                    continue;
                }

                CadXrefContent below = null;

                if (child.BlockTableRecord.IsFromExternalReference)
                {
                    below = Xrefs.ContentOf(child.BlockTableRecord, folder, out _);
                }
                else if (content != null)
                {
                    continue;
                }

                GeoTransform3 placed = placing.Multiply(PlacingOf(child, below));

                if (!(below != null ? XrefReaches(below, placed, at, aperture) : Reaches(child, placing, at, aperture)))
                {
                    continue;
                }

                var deeper = new List<string>(chain) { child.Handle };
                string under = path + " > " + child.Name;
                string childFile = ExternalFileOf(child, folder);
                levels.Add(
                    new CadRefEditLevel(
                        child.Name,
                        deeper.ToArray(),
                        under,
                        child.BlockTableRecord.IsFromExternalReference,
                        childFile
                    )
                );
                Descend(child, deeper, under, placed, childFile, below, at, aperture, levels);
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

        /// <summary>Whether the box of the objects of a drawing an external reference names, moved by a transformation, reaches a point within an aperture.</summary>
        private static bool XrefReaches(CadXrefContent xref, GeoTransform3 placing, GeoPoint2 at, double aperture)
        {
            foreach (DxfEntity entity in xref.Database.ModelSpace)
            {
                if (entity.IsErased)
                {
                    continue;
                }

                try
                {
                    GeoAabb3 box = entity.GetTransformedCopy(placing).GeometricExtents;

                    if (
                        !box.IsEmpty
                        && at.X >= box.Min.X - aperture
                        && at.X <= box.Max.X + aperture
                        && at.Y >= box.Min.Y - aperture
                        && at.Y <= box.Max.Y + aperture
                    )
                    {
                        return true;
                    }
                }
                catch (Exception exception)
                    when (exception is ArgumentException
                        || exception is InvalidOperationException
                        || exception is NotSupportedException
                    )
                {
                    // An object that cannot be placed does not reach the point.
                }
            }

            return false;
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
            if (!ResolveChain(chain, out List<RefLevel> levels, out _) || levels[levels.Count - 1].Content == null)
            {
                return null;
            }

            GeoTransform3 placing = ChainTransform(levels);
            string found = null;
            double least = double.MaxValue;

            foreach (DxfEntity entity in Candidates(levels[levels.Count - 1].Content, RefEditAttributeDefinitions))
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

            if (!ResolveChain(chain, out List<RefLevel> levels, out string refusal))
            {
                return CadEditResult.Refuse(refusal);
            }

            CadSpace host = FindSpace(levels[0].Insert.Handle);
            RefLevel last = levels[levels.Count - 1];
            DxfBlockTableRecord block = last.Insert.BlockTableRecord;

            if (host == null || host.IsBlockEditor)
            {
                return CadEditResult.Refuse(CadRefEdit.NotAValidReference);
            }

            if (levels.Exists(l => l.Insert is DxfMInsertBlock))
            {
                return CadEditResult.Refuse("A block array cannot be edited in place.");
            }

            if (levels.Exists(l => l.Insert.BlockTableRecord == null))
            {
                return CadEditResult.Refuse(CadRefEdit.NotAValidReference);
            }

            string notEditable = XrefRefusal(levels);

            if (notEditable != null)
            {
                return CadEditResult.Refuse(notEditable);
            }

            if (levels.Exists(l => l.Insert.BlockTableRecord.IsAnonymous))
            {
                return CadEditResult.Refuse(
                    block.Name.StartsWith("*D", StringComparison.OrdinalIgnoreCase)
                    || block.Name.StartsWith("*T", StringComparison.OrdinalIgnoreCase)
                        ? CadRefEdit.DimensionOrTable
                        : CadRefEdit.Anonymous
                );
            }

            if (levels.Exists(l => !IsUniform(l.Insert.ScaleFactors)))
            {
                return CadEditResult.Refuse(CadRefEdit.NonUniform);
            }

            if (Database.NamedObjectsDictionary == null)
            {
                return CadEditResult.Refuse("This drawing cannot hold an in-place edit.");
            }

            GeoTransform3 placing = ChainTransform(levels);

            if (!placing.TryGetInverse(out _))
            {
                return CadEditResult.Refuse("The reference's scale cannot be undone.");
            }

            List<DxfEntity> taken = Candidates(last.Content, RefEditAttributeDefinitions).ToList();

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
            bool unique = last.IsExternal && RefEditUniqueNames;
            var header = new RefEditHeader(
                block.Name,
                first,
                chain.ToArray(),
                RefEditLocksObjects,
                RefEditAttributeDefinitions,
                unique,
                last.IsExternal
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

                    if (last.IsExternal)
                    {
                        CopyFromXref(s, host, last, taken, placing, unique, pairs, out skipped);
                    }
                    else
                    {
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

            _refEdit = new CadRefEdit(this, header, start, _position)
            {
                Skipped = skipped,
                XrefFile = last.Xref?.FullPath,
                FormatNotice = last.IsExternal ? FormatNoticeOf(last.Xref.Database) : null,
            };
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
            ResolveChain(chain, out List<RefLevel> levels, out _)
            && levels[levels.Count - 1].Content != null
            && levels[levels.Count - 1].Content.OfType<DxfAttributeDefinition>().Any(e => !e.IsErased);

        /// <summary>
        /// The full path of the file a chain's external reference was read from, when its drawing is not as it was read: changed
        /// on disk since, which REFEDIT of it says must be reloaded first (AutoCAD's "Xref may have changed and must be
        /// reloaded"); null for a chain that ends in a block, in a reference whose file is as it was read, or in none.
        /// </summary>
        internal string RefEditXrefToReload(IReadOnlyList<string> chain) =>
            ResolveChain(chain, out List<RefLevel> levels, out _)
            && levels[levels.Count - 1].Xref != null
            && levels[levels.Count - 1].Xref.HasChanged
                ? levels[levels.Count - 1].Xref.FullPath
                : null;

        /// <summary>
        /// Why the chain's references cannot be edited in place, in AutoCAD 2021's words, as far as external references go; null
        /// where they can. A reference whose file is unloaded, not found or cannot be read is not editable, and neither is a block
        /// of a drawing an external reference names; the one the edit is on must be no more changed on disk since it was read, and
        /// the file must have no lock of another AutoCAD (a <c>.dwl</c> file beside it).
        /// </summary>
        private string XrefRefusal(List<RefLevel> levels)
        {
            for (int i = 0; i < levels.Count; i++)
            {
                RefLevel level = levels[i];

                if (level.IsExternal && level.Xref == null)
                {
                    return CadRefEdit.XrefNotEditable(level.Insert.BlockTableRecord.PathName);
                }

                if (i > 0 && levels[i - 1].IsExternal && !level.IsExternal)
                {
                    return CadRefEdit.XrefBlockNotEditable;
                }
            }

            RefLevel last = levels[levels.Count - 1];

            if (!last.IsExternal)
            {
                return null;
            }

            if (last.Xref.HasChanged)
            {
                return CadRefEdit.XrefChanged(last.Xref.FullPath);
            }

            foreach (string extension in new[] { ".dwl", ".dwl2" })
            {
                string locked = System.IO.Path.ChangeExtension(last.Xref.FullPath, extension);

                if (System.IO.File.Exists(locked))
                {
                    return CadRefEdit.XrefLocked(last.Xref.FullPath);
                }
            }

            return null;
        }

        /// <summary>
        /// What AutoCAD 2021 says when the drawing an external reference names is of an older release than this one: that its
        /// file is saved in its own release. Null for a drawing of the same or a later release.
        /// </summary>
        private string FormatNoticeOf(DxfDatabase referenced)
        {
            int theirs = DxfVersions.Number(referenced.Version);
            int ours = DxfVersions.Number(Database.Version);
            return theirs > 0 && ours > 0 && theirs < ours ? CadRefEdit.XrefOlderFormat : null;
        }

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

            if (_refEdit == null || !ResolveChain(_refEdit.Header.Chain, out List<RefLevel> levels, out _))
            {
                return;
            }

            RefLevel last = levels[levels.Count - 1];
            DxfBlockTableRecord block = last.Insert.BlockTableRecord;
            var originals = new HashSet<string>(
                (last.Content ?? block).Where(e => !e.IsErased && e.Handle != null).Select(e => e.Handle),
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

            foreach (DxfBlockTableRecord record in last.Owner.BlockTable)
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
        /// <remarks>
        /// An external reference saved is written to its file, whole and in the version the file had, DWG or DXF, and read again,
        /// so that every reference of it draws the change; the objects of the file that are not in the working set are written as
        /// they were. Once its file is written the session cannot be taken back by undo, as the file cannot, so undo stops where
        /// it closed. A file that is gone, read only, locked or changed on disk since it was read refuses the save, the session
        /// still open to be saved again or discarded.
        /// </remarks>
        /// <param name="save">Whether the changes are saved; false discards them.</param>
        /// <returns>What came of it: refused where no edit is open or a command's group of edits is, or where the file of an
        /// external reference cannot be written (<c>** Failed to Saveback Xref file **</c>); failed, the session still
        /// open, where an object cannot be put back through the reference's placing or the file cannot be written.</returns>
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
            bool toFile = save && header.External;

            if (toFile)
            {
                string unwritable = XrefSaveRefusal(header);

                if (unwritable != null)
                {
                    return CadEditResult.Refuse(unwritable);
                }
            }

            List<string> notes = null;
            CadEditResult result = ApplyEdit(
                "REFCLOSE",
                new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "Reference edit", name, null) },
                s => notes = WriteBack(s.Database, header, save)
            );

            if (!result.Succeeded)
            {
                return result;
            }

            var redrawn = new CadEditRecord(CadEditRecordKind.TableChanged, null, "Redefine block", null, name);
            Merge(edit.Start, "REFEDIT", redrawn);

            if (toFile)
            {
                // The file written cannot be taken back, so undo does not go back to before it.
                _undoLimit = Math.Max(_undoLimit, _position);
            }

            RefEditNotes = (IReadOnlyList<string>)notes ?? Array.Empty<string>();
            _refEdit = null;
            Selection.Clear();
            Announce(CadChangeKind.Edit, "REFCLOSE", new[] { redrawn });
            GeometryHelperLog.Info("CadDocument: REFCLOSE closed " + name + (save ? ", saved." : ", discarded."));
            RefEditChanged?.Invoke(this, EventArgs.Empty);
            return result;
        }

        /// <summary>Writes the working set back into the block, or erases its copies, and takes the XRECORD out; run inside the closing edit.</summary>
        private List<string> WriteBack(DxfDatabase database, RefEditHeader header, bool save)
        {
            if (!ResolveChain(header.Chain, out List<RefLevel> levels, out string refusal))
            {
                // An edit of an external reference is discarded even where the file it came from is gone from the way to it.
                if (header.External && !save)
                {
                    return DiscardXref(database, header);
                }

                throw new InvalidOperationException(refusal);
            }

            if (levels[levels.Count - 1].IsExternal)
            {
                return WriteBackXref(database, header, levels, save);
            }

            DxfBlockTableRecord block = levels[levels.Count - 1].Insert.BlockTableRecord;
            GeoTransform3 inverse = ChainTransform(levels).Inverse();
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
            return null;
        }

        /// <summary>
        /// Why the file of the external reference being edited cannot be written, in AutoCAD 2021's words
        /// (<c>** Failed to Saveback Xref file **</c>); null where it can: the file is gone, read only, held by another program,
        /// or changed on disk since the edit read it (which the words of REFEDIT say must be reloaded).
        /// </summary>
        private string XrefSaveRefusal(RefEditHeader header)
        {
            if (!ResolveChain(header.Chain, out List<RefLevel> levels, out _))
            {
                return CadRefEdit.NotAValidReference;
            }

            CadXrefContent xref = levels[levels.Count - 1].Xref;

            if (xref == null)
            {
                return CadRefEdit.XrefSaveFailed;
            }

            if (xref.HasChanged)
            {
                return CadRefEdit.XrefChanged(xref.FullPath);
            }

            try
            {
                using (
                    new System.IO.FileStream(
                        xref.FullPath,
                        System.IO.FileMode.Open,
                        System.IO.FileAccess.ReadWrite,
                        System.IO.FileShare.Read
                    )
                )
                {
                    return null;
                }
            }
            catch (Exception exception)
                when (exception is System.IO.IOException
                    || exception is UnauthorizedAccessException
                    || exception is NotSupportedException
                )
            {
                GeometryHelperLog.Warn("CadDocument: REFCLOSE cannot write " + xref.FullPath + ".", exception);
                return CadRefEdit.XrefSaveFailed;
            }
        }

        /// <summary>
        /// Copies the objects of the drawing an external reference names into this drawing's space where the reference draws
        /// them (<see cref="DxfDatabase.WblockCloneObjects"/>, with their layers, linetypes, styles and blocks, and the drawing
        /// they come from left as it is), each put through the placing of the chain; with unique names the layers the copies
        /// stand on are copies of their own, <c>$0$WALL</c> for WALL. An object the placing cannot hold is not copied, and
        /// counted.
        /// </summary>
        private static void CopyFromXref(
            CadEditScope s,
            CadSpace host,
            RefLevel last,
            List<DxfEntity> taken,
            GeoTransform3 placing,
            bool unique,
            List<RefEditPair> pairs,
            out int skipped
        )
        {
            skipped = 0;
            DxfDatabase hostDatabase = s.Database;
            DxfDatabase from = last.Xref.Database;
            var layersBefore = new HashSet<string>(
                hostDatabase.LayerTable.Select(l => l.Name),
                StringComparer.OrdinalIgnoreCase
            );
            IReadOnlyList<DxfEntity> copies = from.WblockCloneObjects(taken, host.Block);
            var kept = new List<DxfEntity>(copies.Count);

            for (int i = 0; i < copies.Count && i < taken.Count; i++)
            {
                DxfEntity copy = copies[i];

                if (!copy.IsResident)
                {
                    skipped++;
                    continue;
                }

                try
                {
                    copy.TransformBy(placing);
                }
                catch (Exception exception)
                    when (exception is ArgumentException
                        || exception is InvalidOperationException
                        || exception is NotSupportedException
                    )
                {
                    copy.Erase();
                    skipped++;
                    continue;
                }

                s.Added(host, copy);
                kept.Add(copy);
                pairs.Add(new RefEditPair(copy.Handle, taken[i].Handle, false));
            }

            if (unique)
            {
                GiveUniqueLayers(hostDatabase, from, kept, layersBefore);
            }
        }

        /// <summary>
        /// Puts each layer the copies stand on onto a layer of its own name, <c>$0$WALL</c> for WALL, the number raised past a
        /// name this drawing's layer table has: a copy of the reference's layer, its colour, linetype (where this drawing has it),
        /// lineweight, switches and plotting. Layer 0 and Defpoints stay as they are; so does a layer of a reference nested in the
        /// drawing (it has a | in it). The layer the copies brought under its own name is erased where no object stands on it now
        /// and this drawing had none before.
        /// </summary>
        private static void GiveUniqueLayers(
            DxfDatabase host,
            DxfDatabase from,
            List<DxfEntity> copies,
            HashSet<string> layersBefore
        )
        {
            var given = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (DxfEntity copy in copies)
            {
                string own = copy.Layer;

                if (
                    own == "0"
                    || string.Equals(own, "Defpoints", StringComparison.OrdinalIgnoreCase)
                    || own.IndexOf('|') >= 0
                )
                {
                    continue;
                }

                if (!given.TryGetValue(own, out string unique))
                {
                    unique = UniqueName(host.LayerTable, own);
                    given[own] = unique;
                    var layer = new DxfLayerTableRecord { Name = unique };
                    host.LayerTable.Add(layer);

                    if (from.LayerTable.Has(own))
                    {
                        CopyLayerLook(from.LayerTable[own], layer, host);
                    }
                }

                copy.Layer = unique;
            }

            foreach (string own in given.Keys.OrderBy(n => n, StringComparer.Ordinal))
            {
                if (!layersBefore.Contains(own) && host.LayerTable.Has(own))
                {
                    try
                    {
                        DxfLayerTableRecord brought = host.LayerTable[own];

                        if (!brought.IsUsed)
                        {
                            brought.Erase();
                        }
                    }
                    catch (Exception exception)
                        when (exception is ArgumentException || exception is InvalidOperationException)
                    {
                        // A layer that cannot be erased stays, as a layer an INSERT of the copies still draws on does.
                    }
                }
            }
        }

        /// <summary>The first of <c>$0$WALL</c>, <c>$1$WALL</c> and on that the table has no record of.</summary>
        private static string UniqueName(DxfSymbolTable table, string own)
        {
            for (int i = 0; ; i++)
            {
                string name = "$" + i.ToString(CultureInfo.InvariantCulture) + "$" + own;

                if (!table.Has(name))
                {
                    return name;
                }
            }
        }

        /// <summary>Gives a layer made in the drawing the look of the layer it copies: colour, linetype, lineweight, off, frozen, locked and plotted.</summary>
        private static void CopyLayerLook(DxfLayerTableRecord from, DxfLayerTableRecord to, DxfDatabase host)
        {
            try
            {
                to.Color = from.Color;
                to.LineWeight = from.LineWeight;
                to.IsFrozen = from.IsFrozen;
                to.IsLocked = from.IsLocked;
                to.IsOff = from.IsOff;
                to.IsPlottable = from.IsPlottable;

                if (host.LinetypeTable.Has(from.LinetypeName))
                {
                    to.LinetypeName = from.LinetypeName;
                }
            }
            catch (Exception exception)
                when (exception is ArgumentException
                    || exception is InvalidOperationException
                    || exception is NotSupportedException
                )
            {
                // A look this drawing's version cannot hold (a true colour in an old file) is left as the layer made it.
                GeometryHelperLog.Debug("CadDocument: a layer's look was not copied: " + exception.Message);
            }
        }

        /// <summary>
        /// Saves the working set of an edit of an external reference into the file it names, or discards it, as the closing edit
        /// of <see cref="CloseRefEdit"/>. Saved, the file is read afresh, each object of the working set copied into its model
        /// space put through the chain's placing the other way and the names it stands on (<c>$0$WALL</c>, <c>XREF|WALL</c>)
        /// turned into the file's own, the objects they came from erased, and the file written whole, as the version it had, over
        /// the old one; the references of it are read again. Either way the copies are erased from this drawing, the layers made
        /// for the session that nothing stands on now with them.
        /// </summary>
        /// <returns>The lines AutoCAD says after it: the symbols added to the file, how many references were updated, that it was reloaded.</returns>
        private List<string> WriteBackXref(DxfDatabase database, RefEditHeader header, List<RefLevel> levels, bool save)
        {
            RefLevel last = levels[levels.Count - 1];
            List<RefEditPair> pairs = ReadRefEditPairs(database, out _);
            var notes = new List<string>();
            var hostEntities = new List<DxfEntity>();

            foreach (RefEditPair pair in pairs)
            {
                DxfEntity host = FindEntity(pair.Host);

                if (save && !pair.Released && host != null)
                {
                    hostEntities.Add(host);
                }
            }

            if (save)
            {
                string path = last.Xref.FullPath;
                DxfDatabase file = CadDrawingFile.Open(path);
                var originals = new Dictionary<string, DxfEntity>(StringComparer.OrdinalIgnoreCase);

                foreach (DxfEntity entity in file.ModelSpace)
                {
                    if (entity.Handle != null)
                    {
                        originals[entity.Handle] = entity;
                    }
                }

                var before = SymbolNames(file);
                GeoTransform3 inverse = ChainTransform(levels).Inverse();
                IReadOnlyList<DxfEntity> copies =
                    hostEntities.Count == 0
                        ? Array.Empty<DxfEntity>()
                        : database.WblockCloneObjects(hostEntities, file.ModelSpace);

                foreach (DxfEntity copy in copies)
                {
                    if (copy.IsResident)
                    {
                        copy.TransformBy(inverse);
                    }
                }

                GiveOwnNames(file, copies, before);
                notes.AddRange(AddedSymbolLines(file, before));

                foreach (RefEditPair pair in pairs)
                {
                    if (
                        pair.Original != RefEditPair.None
                        && originals.TryGetValue(pair.Original, out DxfEntity original)
                        && !original.IsErased
                    )
                    {
                        original.Erase();
                    }
                }

                foreach (DxfEntity host in hostEntities)
                {
                    host.Erase();
                }

                WriteXrefFile(file, path);
                string name = header.Name;
                Xrefs.Forget(path);
                CadXrefContent reread = Xrefs.Read(path);

                // The layers of the reference the drawing holds as it brought them come anew from the file saved.
                if (levels.Count == 1 && reread.Database != null)
                {
                    CadXrefLayers.Bring(database, name, reread.Database, true);
                }
            }
            else
            {
                return DiscardXref(database, header);
            }

            ErasePassingLayers(database, header);
            database.NamedObjectsDictionary.Remove(RefEditKey);
            return notes;
        }

        /// <summary>Discards the edit of an external reference: the copies of its objects erased, the layers made for it with them, the XRECORD out. The file is not read.</summary>
        private List<string> DiscardXref(DxfDatabase database, RefEditHeader header)
        {
            foreach (RefEditPair pair in ReadRefEditPairs(database, out _))
            {
                DxfEntity host = FindEntity(pair.Host);

                if (pair.Original != RefEditPair.None && host != null)
                {
                    host.Erase();
                }
            }

            ErasePassingLayers(database, header);
            database.NamedObjectsDictionary.Remove(RefEditKey);
            return new List<string>();
        }

        /// <summary>
        /// The layers an edit with unique names made, <c>$0$WALL</c> and the like, that no object stands on once the copies are
        /// gone, are erased: those the table gave a handle past the first the session gave.
        /// </summary>
        private static void ErasePassingLayers(DxfDatabase database, RefEditHeader header)
        {
            if (!header.UniqueNames)
            {
                return;
            }

            foreach (DxfLayerTableRecord layer in database.LayerTable.ToList())
            {
                if (
                    layer.Handle != null
                    && ulong.TryParse(
                        layer.Handle,
                        NumberStyles.HexNumber,
                        CultureInfo.InvariantCulture,
                        out ulong made
                    )
                    && made >= header.FirstNewHandle
                    && OwnNameOf(layer.Name) != null
                )
                {
                    try
                    {
                        if (!layer.IsUsed)
                        {
                            layer.Erase();
                        }
                    }
                    catch (Exception exception)
                        when (exception is ArgumentException || exception is InvalidOperationException)
                    {
                        // A layer that cannot be erased stays.
                    }
                }
            }
        }

        /// <summary>The name a layer, linetype, style or block of the session goes by in the file of the reference: <c>WALL</c> for <c>$0$WALL</c> and for <c>XREF|WALL</c>; null where it is its own.</summary>
        internal static string OwnNameOf(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            if (name.Length > 3 && name[0] == '$')
            {
                int close = name.IndexOf('$', 1);

                if (close > 1 && close < name.Length - 1 && name.Substring(1, close - 1).All(char.IsDigit))
                {
                    return name.Substring(close + 1);
                }
            }

            int bar = name.LastIndexOf('|');
            return bar >= 0 && bar < name.Length - 1 ? name.Substring(bar + 1) : null;
        }

        /// <summary>The names in the tables of layers, linetypes, text styles, dimension styles and (named) blocks of a drawing.</summary>
        private static Dictionary<string, HashSet<string>> SymbolNames(DxfDatabase database)
        {
            var names = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
            {
                { "Layers", NamesOf(database.LayerTable) },
                { "Linetypes", NamesOf(database.LinetypeTable) },
                { "Text Styles", NamesOf(database.TextStyleTable) },
                { "Dim Styles", NamesOf(database.DimStyleTable) },
                {
                    "Blocks",
                    NamesOf(
                        database
                            .BlockTable.Cast<DxfBlockTableRecord>()
                            .Where(b => !b.IsLayout && !b.Name.StartsWith("*", StringComparison.Ordinal))
                    )
                },
            };
            return names;
        }

        private static HashSet<string> NamesOf(System.Collections.IEnumerable records) =>
            new HashSet<string>(
                records.Cast<DxfSymbolTableRecord>().Select(r => r.Name),
                StringComparer.OrdinalIgnoreCase
            );

        /// <summary>
        /// Turns the names the copies stand on in the file into the file's own: the layer, linetype, text style, dimension style
        /// or block <c>$0$WALL</c> or <c>XREF|WALL</c> of a copy becomes WALL where the file has one, or the record the copy
        /// brought is renamed to it where it has none; a record the copies brought under a name of this drawing's, and no
        /// object stands on now, is erased.
        /// </summary>
        private static void GiveOwnNames(
            DxfDatabase file,
            IReadOnlyList<DxfEntity> copies,
            Dictionary<string, HashSet<string>> before
        )
        {
            foreach (DxfEntity copy in copies)
            {
                if (!copy.IsResident)
                {
                    continue;
                }

                string layer = OwnNameOf(copy.Layer);

                if (layer != null)
                {
                    if (!file.LayerTable.Has(layer) && file.LayerTable.Has(copy.Layer))
                    {
                        try
                        {
                            file.LayerTable[copy.Layer].Name = layer;
                        }
                        catch (Exception exception)
                            when (exception is ArgumentException || exception is InvalidOperationException)
                        {
                            GeometryHelperLog.Debug(
                                "CadDocument: the layer " + copy.Layer + " is kept: " + exception.Message
                            );
                        }
                    }

                    if (!string.Equals(copy.Layer, layer, StringComparison.Ordinal))
                    {
                        copy.Layer = layer;
                    }
                }

                string linetype = OwnNameOf(copy.Linetype);

                if (linetype != null && file.LinetypeTable.Has(linetype))
                {
                    copy.Linetype = linetype;
                }

                if (
                    copy is DxfDBText text
                    && OwnNameOf(text.TextStyleName) is string style
                    && file.TextStyleTable.Has(style)
                )
                {
                    text.TextStyleName = style;
                }
                else if (
                    copy is DxfMText mtext
                    && OwnNameOf(mtext.TextStyleName) is string mstyle
                    && file.TextStyleTable.Has(mstyle)
                )
                {
                    mtext.TextStyleName = mstyle;
                }
                else if (
                    copy is DxfDimension dimension
                    && OwnNameOf(dimension.DimensionStyleName) is string dim
                    && file.DimStyleTable.Has(dim)
                )
                {
                    dimension.DimensionStyleName = dim;
                }
                else if (
                    copy is DxfBlockReference insert
                    && OwnNameOf(insert.Name) is string block
                    && file.BlockTable.Has(block)
                )
                {
                    insert.BlockTableRecord = file.BlockTable[block];
                }
            }

            // What the copies brought under the names of this drawing's session no longer stands on is taken out.
            foreach (DxfLayerTableRecord layer in file.LayerTable.ToList())
            {
                if (OwnNameOf(layer.Name) != null && !before["Layers"].Contains(layer.Name))
                {
                    TryErase(layer);
                }
            }

            foreach (DxfLinetypeTableRecord linetype in file.LinetypeTable.ToList())
            {
                if (OwnNameOf(linetype.Name) != null && !before["Linetypes"].Contains(linetype.Name))
                {
                    TryErase(linetype);
                }
            }
        }

        private static void TryErase(DxfSymbolTableRecord record)
        {
            try
            {
                record.Erase();
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            {
                // A record something still stands on stays.
            }
        }

        /// <summary>
        /// The lines of the symbols a file gained from the copies, as AutoCAD 2021 lists them before it writes them (<c>The
        /// following symbols will be added to Xref file:</c>, then <c>Layers: A, B</c> and the like), in ordinal order; none where
        /// it gained none.
        /// </summary>
        private static IEnumerable<string> AddedSymbolLines(
            DxfDatabase file,
            Dictionary<string, HashSet<string>> before
        )
        {
            var lines = new List<string>();
            Dictionary<string, HashSet<string>> now = SymbolNames(file);

            foreach (string table in new[] { "Layers", "Linetypes", "Text Styles", "Dim Styles", "Blocks" })
            {
                List<string> added = now[table]
                    .Where(n => !before[table].Contains(n))
                    .OrderBy(n => n, StringComparer.Ordinal)
                    .ToList();

                if (added.Count > 0)
                {
                    lines.Add(table + ": " + string.Join(", ", added));
                }
            }

            if (lines.Count > 0)
            {
                lines.Insert(0, CadRefEdit.SymbolsAdded);
            }

            return lines;
        }

        /// <summary>
        /// Writes the drawing of an external reference over its file: to a file beside it first, in the file's own version (a DWG
        /// as the version its first bytes name, a DXF as its own), then over the old one, so that a write that fails leaves the old
        /// file as it was. A DWG the writer cannot write whole (it leaves out an object type it has no writer for) is not
        /// written, rather than losing those objects.
        /// </summary>
        private static void WriteXrefFile(DxfDatabase file, string path)
        {
            string temporary = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(path),
                "gh-refedit-" + Guid.NewGuid().ToString("N") + System.IO.Path.GetExtension(path)
            );

            try
            {
                string version = null;

                if (CadDrawingFile.IsDwg(path))
                {
                    using (
                        var stream = new System.IO.FileStream(
                            path,
                            System.IO.FileMode.Open,
                            System.IO.FileAccess.Read,
                            System.IO.FileShare.ReadWrite
                        )
                    )
                    {
                        var start = new byte[6];
                        int read = stream.Read(start, 0, start.Length);
                        string named = System.Text.Encoding.ASCII.GetString(start, 0, read);
                        version = CadDrawingFile.IsDwgVersion(named) ? named : null;
                    }
                }

                string left = CadDrawingFile.Save(file, temporary, version, null, null);

                if (left.Length > 0)
                {
                    throw new NotSupportedException("The DWG writer leaves out " + left + ".");
                }

                System.IO.File.Copy(temporary, path, true);
            }
            catch (Exception exception)
                when (exception is System.IO.IOException
                    || exception is UnauthorizedAccessException
                    || exception is NotSupportedException
                )
            {
                GeometryHelperLog.Warn("CadDocument: REFCLOSE could not write " + path + ".", exception);
                throw new InvalidOperationException(CadRefEdit.XrefSaveFailed + " " + exception.Message, exception);
            }
            finally
            {
                try
                {
                    System.IO.File.Delete(temporary);
                }
                catch (Exception exception)
                    when (exception is System.IO.IOException || exception is UnauthorizedAccessException)
                {
                    GeometryHelperLog.Debug("CadDocument: " + temporary + " was not deleted: " + exception.Message);
                }
            }
        }

        /// <summary>One reference of a chain: the INSERT, what it places (its block, or the drawing an external reference names) and how.</summary>
        private sealed class RefLevel
        {
            internal RefLevel(
                DxfBlockReference insert,
                DxfDatabase owner,
                DxfBlockTableRecord content,
                CadXrefContent xref,
                GeoTransform3 placing
            )
            {
                Insert = insert;
                Owner = owner;
                Content = content;
                Xref = xref;
                Placing = placing;
            }

            /// <summary>The INSERT.</summary>
            internal DxfBlockReference Insert { get; }

            /// <summary>The drawing the INSERT stands in: this one, or the drawing of an external reference above it.</summary>
            internal DxfDatabase Owner { get; }

            /// <summary>The objects it places: its block's, or those of the model space of the drawing its external reference names; null for a reference whose file is not read.</summary>
            internal DxfBlockTableRecord Content { get; }

            /// <summary>The drawing read where the INSERT places an external reference; null for a block and for a reference not read.</summary>
            internal CadXrefContent Xref { get; }

            /// <summary>Whether the INSERT places an external reference.</summary>
            internal bool IsExternal =>
                Insert.BlockTableRecord != null && Insert.BlockTableRecord.IsFromExternalReference;

            /// <summary>The transformation from the coordinates of what it places to those of the drawing it stands in.</summary>
            internal GeoTransform3 Placing { get; }
        }

        /// <summary>
        /// Resolves a chain of handles into the references: the first in a space, each next nested in the block of the one before,
        /// or in the drawing of the external reference before it.
        /// </summary>
        private bool ResolveChain(IReadOnlyList<string> chain, out List<RefLevel> levels, out string refusal)
        {
            levels = new List<RefLevel>();
            refusal = CadRefEdit.NotAValidReference;

            if (chain == null || chain.Count == 0 || !(FindEntity(chain[0]) is DxfBlockReference top))
            {
                return false;
            }

            DxfDatabase owner = Database;
            string folder = DrawingFolder;
            DxfBlockReference insert = top;

            for (int i = 0; ; i++)
            {
                DxfBlockTableRecord block = insert.BlockTableRecord;
                CadXrefContent xref = null;
                DxfBlockTableRecord content = block;

                if (block != null && block.IsFromExternalReference)
                {
                    xref = Xrefs.ContentOf(block, folder, out _);
                    content = xref?.Database.ModelSpace;
                }

                levels.Add(new RefLevel(insert, owner, content, xref, PlacingOf(insert, xref)));

                if (i + 1 == chain.Count)
                {
                    refusal = null;
                    return true;
                }

                string handle = chain[i + 1];
                DxfBlockReference next = content
                    ?.OfType<DxfBlockReference>()
                    .FirstOrDefault(r =>
                        !r.IsErased && string.Equals(r.Handle, handle, StringComparison.OrdinalIgnoreCase)
                    );

                if (next == null)
                {
                    return false;
                }

                if (xref != null)
                {
                    owner = xref.Database;
                    folder = System.IO.Path.GetDirectoryName(xref.FullPath);
                }

                insert = next;
            }
        }

        /// <summary>The transformation from the coordinates of the last block of a chain to the drawing: each reference's placing, the outer one applied last.</summary>
        private static GeoTransform3 ChainTransform(List<RefLevel> levels)
        {
            GeoTransform3 placing = levels[0].Placing;

            for (int i = 1; i < levels.Count; i++)
            {
                placing = placing.Multiply(levels[i].Placing);
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

        /// <summary>The full path of the file the external reference an edit is on names, found now; null where it is not found (or the edit is on a block).</summary>
        internal string RefEditFileOf(IReadOnlyList<string> chain) =>
            ResolveChain(chain, out List<RefLevel> levels, out _) ? levels[levels.Count - 1].Xref?.FullPath : null;

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
            // The objects of an external reference's drawing are hidden where that drawing is drawn, not among this drawing's own.
            if (_refEdit == null || _refEdit.Header.External)
            {
                return null;
            }

            return RefEditOriginals();
        }

        /// <summary>
        /// The handles of the objects of the drawing an external reference names that the references draw without while an edit
        /// of it is open: those the copies in this drawing stand for, by the full path of its file. Null where no edit is open,
        /// it is of another file or of a block.
        /// </summary>
        internal HashSet<string> RefEditHiddenInFile(string fullPath) =>
            _refEdit != null
            && _refEdit.Header.External
            && string.Equals(_refEdit.XrefFile, fullPath, StringComparison.OrdinalIgnoreCase)
                ? RefEditOriginals()
                : null;

        /// <summary>The handles of the objects the copies of the working set stand for, in the object's own drawing; null where none.</summary>
        private HashSet<string> RefEditOriginals()
        {
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
                flags.IndexOf('A') >= 0,
                flags.IndexOf('U') >= 0,
                flags.IndexOf('X') >= 0
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
                        + (header.UniqueNames ? "U" : string.Empty)
                        + (header.External ? "X" : string.Empty)
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
        internal RefEditHeader(
            string name,
            ulong first,
            string[] chain,
            bool locks,
            bool attributeDefinitions,
            bool uniqueNames,
            bool external
        )
        {
            Name = name;
            FirstNewHandle = first;
            Chain = chain;
            LocksObjects = locks;
            AttributeDefinitions = attributeDefinitions;
            UniqueNames = uniqueNames;
            External = external;
        }

        internal string Name { get; }

        internal ulong FirstNewHandle { get; }

        internal string[] Chain { get; }

        internal bool LocksObjects { get; }

        internal bool AttributeDefinitions { get; }

        internal bool UniqueNames { get; }

        internal bool External { get; }
    }

    /// <summary>One reference the Reference Edit dialog lists: its block's name and the chain of references that reach it.</summary>
    public sealed class CadRefEditLevel
    {
        internal CadRefEditLevel(string name, string[] chain, string path, bool isExternalReference, string fileName)
        {
            Name = name;
            Chain = chain;
            Path = path;
            IsExternalReference = isExternalReference;
            FileName = fileName;
        }

        /// <summary>Gets the name of the reference's block.</summary>
        public string Name { get; }

        /// <summary>Gets whether the reference places an external reference rather than a block.</summary>
        public bool IsExternalReference { get; }

        /// <summary>
        /// Gets the full path of the file the reference names, found as AutoCAD looks for it, which the dialog's "Reference
        /// name" and path columns show for an external reference; null for a block, and for an external reference whose file
        /// is not found.
        /// </summary>
        public string FileName { get; }

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
        internal const string XrefSaveFailed = "** Failed to Saveback Xref file **";
        internal const string XrefBlockNotEditable = "Selected Xref Block is NOT editable.";
        internal const string SymbolsAdded = "The following symbols will be added to Xref file:";
        internal const string XrefOlderFormat =
            "The Xref selected references a previous drawing file format.\nEdits saved back to the Xref file will be in the release format\nof the referenced drawing file.";

        /// <summary>What REFEDIT says of an external reference whose drawing it cannot read, in AutoCAD 2021's words.</summary>
        internal static string XrefNotEditable(string path) => "Selected Xref Block is NOT editable: " + path;

        /// <summary>What REFEDIT says of an external reference whose file changed on disk since it was read.</summary>
        internal static string XrefChanged(string path) => "Xref may have changed and must be reloaded: " + path;

        /// <summary>What REFEDIT says of an external reference whose file another AutoCAD holds open (a .dwl file beside it).</summary>
        internal static string XrefLocked(string path) =>
            "Xref cannot be checked out for editing:\n" + path + "\nA file lock already exists on the drawing.";

        internal static string XrefInstancesUpdated(int count) =>
            count.ToString(CultureInfo.InvariantCulture)
            + " xref instance"
            + (count == 1 ? string.Empty : "s")
            + " updated";

        internal static string XrefRedefined(string name) => name + " redefined and reloaded.";

        private readonly CadDocument _document;
        private string _xrefFile;

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

        /// <summary>
        /// Gets the full path of the file REFCLOSE Save writes, where the reference edited is an external reference; null for a
        /// block.
        /// </summary>
        public string XrefFile
        {
            get
            {
                if (_xrefFile == null && Header.External)
                {
                    _xrefFile = _document.RefEditFileOf(Header.Chain);
                }

                return _xrefFile;
            }
            internal set => _xrefFile = value;
        }

        /// <summary>Gets whether the reference edited is an external reference, whose file REFCLOSE Save writes.</summary>
        public bool IsExternalReference => Header.External;

        /// <summary>
        /// Gets what AutoCAD 2021 says when the edit opened on an external reference whose drawing is of an older release than the
        /// drawing: that the edits saved back are in the release of the file; null otherwise.
        /// </summary>
        public string FormatNotice { get; internal set; }

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
