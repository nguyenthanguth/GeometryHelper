using System;
using System.Collections.Generic;
using System.Globalization;

namespace GeometryHelper.Dxf
{
    /// <summary>Copying entities of the drawing together, as AutoCAD's <c>Database.WblockCloneObjects</c> copies the objects of ids.</summary>
    public sealed partial class DxfDatabase
    {
        /// <summary>
        /// Copies entities of this drawing into a block of another drawing, or of this one, together, as AutoCAD's
        /// <c>WblockCloneObjects</c> copies the objects of ids: each as <see cref="DxfEntity.Clone"/> copies it, added to the
        /// block as <see cref="DxfBlockTableRecord.AppendEntity(DxfEntity)"/> adds one, with what it brings (its layer, linetype,
        /// text and dimension styles, the blocks it is drawn by); and what one of them points to among the others pointing to the
        /// copy of that other, as AutoCAD's map of ids keeps it across the objects copied together.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A HATCH copied with every object its loops follow follows their copies, and is associative, as the HATCH is; with
        /// some of them left behind it follows none, as a HATCH copied alone. A MULTILEADER of a block copied with an INSERT places
        /// the copy of its block and draws its arrows by the copies of theirs, where those blocks come along, as a block W holding
        /// a multileader of the block GKTAG and an INSERT of GKTAG brings GKTAG; so too an ACAD_TABLE points to the copies of its
        /// picture and of the blocks of its cells, and a LEADER or a dimension whose dimension style is overridden with an arrow
        /// block (DIMLDRBLK, DIMBLK, DIMBLK1, DIMBLK2) names the copy of that block, whichever of the two is copied first: a
        /// block W holding the leader with DIMLDRBLK GKTAG brings GKTAG after W, and the copy of the leader names it. A LEADER copied with the MTEXT, TOLERANCE or INSERT
        /// it points to points to its copy; copied alone into another drawing, it brings that annotation with it, which it points
        /// at by a hard pointer (340), as AutoCAD 2021's WblockCloneObjects brought the MTEXT of a leader copied alone, the copy
        /// of the annotation not among those given back; copied alone within this drawing, it points to none
        /// (<see cref="DxfLeader.AnnoType"/> <see cref="DxfAnnotationType.NoAnnotation"/>). A handle of extended data (1005)
        /// naming an entity copied with it names that entity's copy; one naming anything else is null (0) in a copy into
        /// another drawing, in its place, as AutoCAD keeps it. No GROUP is copied, as AutoCAD 2021's WblockCloneObjects copies
        /// none: <see cref="Insert(GeometryHelper.Geometry.GeoTransform3, DxfDatabase, bool)"/> copies them.
        /// </para>
        /// <para>
        /// Every copy is checked to be one the block's drawing can hold before any is added, so that a copy refused changes
        /// nothing: a SPLINE into a drawing of R12 refuses the whole set, and so does a block the copies bring that would insert
        /// itself once written, by its own INSERTs or theirs, through the blocks of the drawing copied into: that drawing's P
        /// holding an INSERT of M, a block it has not (as a drawing read from a file may), and this drawing's M an INSERT of P,
        /// an INSERT of M copied would bring an M that places P, which places that M, without end. A set whose INSERTs would
        /// bring blocks that come back to the block they are added to is refused too. A block AutoCAD names itself that one
        /// entity of the set brings takes no name an INSERT in a block another brings gives of a block neither drawing has, as
        /// a block one entity brings takes none of its own: this drawing having *U1 and *U2, and the other's Q an INSERT of
        /// *U3, which it has not, an INSERT of its *U1 copied with one of Q brings *U1 as *U4, and Q's INSERT of *U3 places
        /// nothing, as where it came from. INSERTs of blocks their drawing has not, in the blocks the set brings, are copied as
        /// they are and said in one Debug line for the set. An MTEXT added to a drawing of R12 is written as TEXTs,
        /// as <see cref="DxfBlockTableRecord.AppendEntity(DxfEntity)"/> writes it, and its place in the list given back is the
        /// MTEXT itself, in no file.
        /// </para>
        /// <para>
        /// A rectangle of four LINEs with a HATCH that follows them, copied together into another drawing, comes out as four
        /// LINEs and a HATCH that follows those four, each LINE listing the HATCH among its reactors.
        /// </para>
        /// </remarks>
        /// <param name="source">The entities, of this drawing and not erased, each once.</param>
        /// <param name="owner">The block of model space, paper space or a block of a drawing the copies are added to.</param>
        /// <returns>The copies, in the order of the entities given.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the entities or the block are null.</exception>
        /// <exception cref="ArgumentException">Thrown when an entity is null, given twice, erased, of another drawing, or a record
        /// that follows another (a VERTEX or an ATTRIB); or when the block's drawing cannot hold a copy, as
        /// <see cref="DxfBlockTableRecord.AppendEntity(DxfEntity)"/> says; or when a block the copies bring would insert itself,
        /// or make the block insert itself, at any depth, which AutoCAD's AUDIT takes out. Nothing is added then.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the block is in no drawing.</exception>
        public IReadOnlyList<DxfEntity> WblockCloneObjects(IEnumerable<DxfEntity> source, DxfBlockTableRecord owner) => CloneObjects(source, owner, false);

        /// <summary>
        /// Copies entities together as <see cref="WblockCloneObjects"/> does; with <paramref name="groups"/>, as
        /// <see cref="Insert(GeometryHelper.Geometry.GeoTransform3, DxfDatabase, bool)"/> copies a drawing, every GROUP of this drawing all of whose
        /// entities are copied into model space or a layout comes too, as <see cref="CopyGroups"/> says.
        /// </summary>
        internal IReadOnlyList<DxfEntity> CloneObjects(IEnumerable<DxfEntity> source, DxfBlockTableRecord owner, bool groups)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            var entities = new List<DxfEntity>(source);
            var given = new HashSet<DxfItem>();

            foreach (DxfEntity entity in entities)
            {
                if (entity == null)
                {
                    throw new ArgumentException("The entities to copy hold a null.", nameof(source));
                }

                if (!entity.IsResident || entity.IsErased || entity.Item.File != File || entity.Item.Parent != null)
                {
                    throw new ArgumentException("The " + entity + " is not an entity of this drawing to copy: erased, of another drawing, made in code, or a record that follows another.", nameof(source));
                }

                if (!given.Add(entity.Item))
                {
                    throw new ArgumentException("The " + entity + " is given twice.", nameof(source));
                }
            }

            DxfFile target = owner.File;

            if (target == null || target.Database == null)
            {
                throw new InvalidOperationException("The block " + owner.Name + " is in no drawing to copy into.");
            }

            // A LEADER copied into another drawing brings the annotation it points at by a hard pointer (340), as AutoCAD's
            // WblockCloneObjects follows one; the annotation's copy is not among those given back.
            int count = entities.Count;

            for (int i = 0; target != File && i < count; i++)
            {
                if (entities[i] is DxfLeader leader && leader.Annotation is DxfEntity annotation && annotation.IsResident && !annotation.IsErased
                    && annotation.Item.File == File && annotation.Item.Parent == null && given.Add(annotation.Item))
                {
                    entities.Add(annotation);
                }
            }

            // Every copy is checked before any is added: a copy refused changes nothing.
            var copies = new List<DxfEntity>(entities.Count);
            var copyOf = new Dictionary<DxfItem, DxfEntity>();

            // The blocks the copies bring, and the blocks their INSERTs place, walked together for a round before any is added.
            var brought = new List<DxfFileBlock>();
            var placed = new List<string>();
            // The records of the tables of the drawing copied into are kept by name for the copy, and what is read of this one
            // with them: this drawing is only read, as several threads may copy from it at once.
            target.HoldTableNames();
            bool keeping = false;

            try
            {
                foreach (DxfEntity entity in entities)
                {
                    DxfEntity copy = entity.Clone();
                    copy.Appending(owner);

                    if (copy is DxfMText mtext && !target.HasMarkers)
                    {
                        foreach (DxfEntity text in mtext.R12Texts(target))
                        {
                            target.CheckAdopt(text.Item);
                        }
                    }
                    else
                    {
                        target.CheckAdopt(copy.Item, null, brought);
                    }

                    if (copy is DxfBlockReference reference)
                    {
                        placed.Add(reference.Name);
                    }

                    copies.Add(copy);
                }

                // One copy alone is walked so as it is added, before anything of it is written.
                if (entities.Count > 1 && brought.Count > 0)
                {
                    target.CheckRounds(File, brought, owner.IsLayout ? null : owner.Name, placed, nameof(source));

                    // No block one copy brings takes a name an INSERT in a block another brings gives, as the walk takes it.
                    keeping = target.KeepAnonymousNames(File, brought);
                }

                for (int i = 0; i < entities.Count; i++)
                {
                    owner.AppendEntity(copies[i]);
                    copyOf.Add(entities[i].Item, copies[i]);
                }
            }
            finally
            {
                if (keeping)
                {
                    target.ReleaseAnonymousNames();
                }

                target.ReleaseTableNames();
            }

            for (int i = 0; i < entities.Count; i++)
            {
                if (!copies[i].IsResident)
                {
                    continue;
                }

                MapDataHandles(entities[i], copies[i], copyOf);

                if (entities[i] is DxfHatch hatch && hatch.Associative)
                {
                    FollowCopies(hatch, (DxfHatch)copies[i], copyOf);
                }
                else if (entities[i] is DxfLeader leader)
                {
                    DxfEntity annotation = leader.Annotation;
                    var copied = (DxfLeader)copies[i];
                    copied.Annotation = annotation != null && copyOf.TryGetValue(annotation.Item, out DxfEntity made) && made.IsResident ? made : null;
                }
            }

            if (groups)
            {
                CopyGroups(target, copyOf, owner.IsLayout);
            }

            GeometryHelperLog.Info("DxfDatabase: " + entities.Count.ToString(CultureInfo.InvariantCulture) + " entities were copied together into the block " + owner.Name + ".");
            return copies.GetRange(0, count).AsReadOnly();
        }

        /// <summary>
        /// Names in each handle of the copy's extended data (1005) that names an entity copied with it the copy of that entity,
        /// as AutoCAD's map of ids takes it: a LINE whose data names a CIRCLE copied with it names the CIRCLE's copy. Groups
        /// that do not stand in the same places in the copy, as overrides of a dimension style taken to another drawing, are
        /// left as the copy has them.
        /// </summary>
        private void MapDataHandles(DxfEntity entity, DxfEntity copy, Dictionary<DxfItem, DxfEntity> copyOf)
        {
            IReadOnlyList<DxfTypedValue> data = entity.XData;
            List<DxfTypedValue> copied = null;

            for (int k = 0; k < data.Count; k++)
            {
                if (data[k].TypeCode != 1005)
                {
                    continue;
                }

                DxfItem named = ItemByHandle(data[k].Value.Trim());

                if (named == null || !copyOf.TryGetValue(named, out DxfEntity made) || !made.IsResident)
                {
                    continue;
                }

                copied = copied ?? new List<DxfTypedValue>(copy.XData);

                if (copied.Count != data.Count || copied[k].TypeCode != 1005)
                {
                    return;
                }

                copied[k] = new DxfTypedValue(1005, made.Handle);
            }

            if (copied != null)
            {
                copy.XData = copied;
            }
        }

        /// <summary>
        /// Makes the copy of an associative HATCH follow the copies of the objects its loops follow, where every one of them was
        /// copied with it; else it follows none, as a HATCH copied alone.
        /// </summary>
        private void FollowCopies(DxfHatch hatch, DxfHatch copy, Dictionary<DxfItem, DxfEntity> copyOf)
        {
            var loops = new List<IReadOnlyList<DxfEntity>>();
            bool any = false;

            foreach (List<string> handles in hatch.LoopSources())
            {
                var loop = new List<DxfEntity>();

                foreach (string handle in handles)
                {
                    DxfItem followed = ItemByHandle(handle);

                    if (followed == null || !copyOf.TryGetValue(followed, out DxfEntity made) || !made.IsResident)
                    {
                        GeometryHelperLog.Debug("DxfDatabase: the HATCH " + hatch.Handle + " was copied without the object " + handle + " it follows, and its copy follows none.");
                        return;
                    }

                    loop.Add(made);
                    any = true;
                }

                loops.Add(loop);
            }

            if (any)
            {
                copy.Follow(loops);
            }
        }

        /// <summary>
        /// Copies each GROUP of this drawing whose entities were all copied into model space or a layout of paper space into the
        /// group dictionary of the drawing they were copied into: in the order of the dictionary, a group with no entity left
        /// out; none where they were copied into a block that is no layout, whose entities a group does not hold (as ezdxf's
        /// recover drops a group that holds one), nor into a drawing with no group dictionary, as one of R12. Its entities'
        /// copies in its order, its description and selectability kept, under its own name, or that name and <c>_1</c>,
        /// <c>_2</c> and on, the first the dictionary has not, where it has a group of the name; an anonymous group as the next
        /// <c>*A1</c>, <c>*A2</c> and on.
        /// </summary>
        private void CopyGroups(DxfFile target, Dictionary<DxfItem, DxfEntity> copyOf, bool layout)
        {
            DxfDBDictionary groups = GroupDictionary;

            if (groups == null)
            {
                return;
            }

            DxfDBDictionary into = target.Database.GroupDictionary;

            foreach (DxfDBDictionaryEntry entry in groups)
            {
                if (!(entry.Value is DxfGroup group) || group.IsErased)
                {
                    continue;
                }

                DxfEntity[] members = group.GetAllEntityIds();
                var made = new List<DxfEntity>(members.Length);

                foreach (DxfEntity member in members)
                {
                    if (copyOf.TryGetValue(member.Item, out DxfEntity copy) && copy.IsResident)
                    {
                        made.Add(copy);
                    }
                }

                if (members.Length == 0 || made.Count != members.Length)
                {
                    continue;
                }

                if (into == null || !layout)
                {
                    GeometryHelperLog.Debug("DxfDatabase: the group " + entry.Key + " was not copied, " + (into == null ? "the drawing copied into having no group dictionary." : "its entities' copies being in a block, which a group does not hold entities of."));
                    continue;
                }

                var copied = new DxfGroup(group.Description, group.Selectable);

                if (group.IsAnonymous)
                {
                    copied.SetAnonymous();
                }

                into.SetAt(group.IsAnonymous ? "*" : FreeGroupName(into, entry.Key), copied);
                copied.Append(made);
            }
        }

        /// <summary>A name for a group copied into a dictionary: its own where the dictionary has none of it, else the first of it and <c>_1</c>, <c>_2</c> and on it has not.</summary>
        private static string FreeGroupName(DxfDBDictionary dictionary, string name)
        {
            if (!dictionary.Contains(name))
            {
                return name;
            }

            for (int n = 1; ; n++)
            {
                string candidate = name + "_" + n.ToString(CultureInfo.InvariantCulture);

                if (!dictionary.Contains(candidate))
                {
                    return candidate;
                }
            }
        }
    }
}
