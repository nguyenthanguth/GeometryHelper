using System;
using System.Collections.Generic;
using System.Globalization;

namespace GeometryHelper.Export
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
        /// some of them left behind it follows none, as a HATCH copied alone. A LEADER copied with the MTEXT, TOLERANCE or INSERT
        /// it points to points to its copy; without it, it points to none (<see cref="DxfLeader.AnnoType"/>
        /// <see cref="DxfAnnotationType.NoAnnotation"/>). A GROUP of this drawing every entity of which is copied into model space
        /// or a layout of paper space is copied too (a group holds no entity of a block, as ezdxf's recover drops one that does),
        /// into the group dictionary of the block's drawing, its entities' copies in its order, its description and selectability
        /// kept: under its own name, or that name and <c>_1</c>, <c>_2</c> and on, the first the dictionary has not, where it has
        /// a group of the name; an anonymous group as the next <c>*A1</c>, <c>*A2</c> and on. A drawing with no group dictionary,
        /// as one of R12, is given none.
        /// </para>
        /// <para>
        /// Every copy is checked to be one the block's drawing can hold before any is added, so that a copy refused changes
        /// nothing: a SPLINE into a drawing of R12 refuses the whole set. An MTEXT added to a drawing of R12 is written as TEXTs,
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
        /// <see cref="DxfBlockTableRecord.AppendEntity(DxfEntity)"/> says. Nothing is added then.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the block is in no drawing.</exception>
        public IReadOnlyList<DxfEntity> WblockCloneObjects(IEnumerable<DxfEntity> source, DxfBlockTableRecord owner)
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

            // Every copy is checked before any is added: a copy refused changes nothing.
            var copies = new List<DxfEntity>(entities.Count);

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
                    target.CheckAdopt(copy.Item);
                }

                copies.Add(copy);
            }

            var copyOf = new Dictionary<DxfItem, DxfEntity>();

            for (int i = 0; i < entities.Count; i++)
            {
                owner.AppendEntity(copies[i]);
                copyOf.Add(entities[i].Item, copies[i]);
            }

            for (int i = 0; i < entities.Count; i++)
            {
                if (!copies[i].IsResident)
                {
                    continue;
                }

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

            CopyGroups(target, copyOf, owner.IsLayout);
            GeometryHelperLog.Info("DxfDatabase: " + entities.Count.ToString(CultureInfo.InvariantCulture) + " entities were copied together into the block " + owner.Name + ".");
            return copies.AsReadOnly();
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
        /// Copies each GROUP of this drawing whose entities were all copied into the group dictionary of the drawing they were
        /// copied into, as <see cref="WblockCloneObjects"/> says: in the order of the dictionary, a group with no entity left out;
        /// none where they were copied into a block that is no layout, whose entities a group does not hold.
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
