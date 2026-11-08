using System;
using System.Collections.Generic;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The owners a document knows, found by the name in an entity's <see cref="CadOwnerTag"/>, and asked before an owned
    /// entity changes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An entity with no tag is the presenter's own (<see cref="CadEditRule.Free"/>). One whose tag names a registered owner
    /// gets the rule that owner gives. One whose tag names an owner not registered, as a model drawing opened where the
    /// drawing module is not loaded, is <see cref="CadEditRule.ReadOnly"/>: there is nobody to record the edit, and the
    /// owner's next update would write over it.
    /// </para>
    /// <para>
    /// An owner that throws is not let stop the presenter: its rule is taken as <see cref="CadEditRule.ReadOnly"/>, its
    /// edit as refused, and the exception is written as a Warn line of <see cref="GeometryHelperLog"/>.
    /// </para>
    /// </remarks>
    public sealed class CadOwnerRegistry
    {
        private readonly Dictionary<string, ICadEntityOwner> _owners = new Dictionary<string, ICadEntityOwner>(StringComparer.Ordinal);

        /// <summary>Gets the owners registered, in the order of their names, character by character.</summary>
        public IReadOnlyList<ICadEntityOwner> Owners
        {
            get
            {
                var owners = new List<ICadEntityOwner>(_owners.Values);
                owners.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
                return owners;
            }
        }

        /// <summary>Registers an owner under its name.</summary>
        /// <param name="owner">The owner.</param>
        /// <exception cref="ArgumentNullException">Thrown when the owner is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the owner's name is null or empty, or an owner of that name is
        /// registered already.</exception>
        public void Register(ICadEntityOwner owner)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            string name = owner.Name;

            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("An owner needs a name.", nameof(owner));
            }

            if (_owners.ContainsKey(name))
            {
                throw new ArgumentException("An owner named " + name + " is registered already.", nameof(owner));
            }

            _owners.Add(name, owner);
        }

        /// <summary>Takes an owner out; its entities are read-only from then on.</summary>
        /// <param name="name">The owner's name.</param>
        /// <returns>Whether an owner of the name was registered.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the name is null.</exception>
        public bool Unregister(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            return _owners.Remove(name);
        }

        /// <summary>Finds the registered owner an entity's tag names.</summary>
        /// <param name="entity">The entity.</param>
        /// <returns>The owner; null where the entity has no tag, or its owner is not registered.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the entity is null.</exception>
        public ICadEntityOwner FindOwner(DxfEntity entity)
        {
            if (!CadOwnerTag.TryRead(entity, out CadOwnerTag tag))
            {
                return null;
            }

            return _owners.TryGetValue(tag.Owner, out ICadEntityOwner owner) ? owner : null;
        }

        /// <summary>
        /// Says what the presenter may do to an entity: <see cref="CadEditRule.Free"/> for one with no tag,
        /// <see cref="CadEditRule.ReadOnly"/> for one whose owner is not registered, and otherwise what its owner answers.
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// <returns>The rule.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the entity is null.</exception>
        public CadEditRule GetEditRule(DxfEntity entity)
        {
            if (!CadOwnerTag.TryRead(entity, out CadOwnerTag tag))
            {
                return CadEditRule.Free;
            }

            if (!_owners.TryGetValue(tag.Owner, out ICadEntityOwner owner))
            {
                return CadEditRule.ReadOnly;
            }

            try
            {
                return owner.GetEditRule(entity);
            }
            catch (Exception exception)
            {
                GeometryHelperLog.Warn("CadOwnerRegistry: the owner " + tag.Owner + " failed to give the rule of entity " + entity.Handle + "; it is taken as read-only.", exception);
                return CadEditRule.ReadOnly;
            }
        }

        /// <summary>
        /// Says whether a rule lets an edit of a kind through: <see cref="CadEditRule.Free"/> and
        /// <see cref="CadEditRule.Edit"/> every kind, <see cref="CadEditRule.MoveOnly"/> a <see cref="CadEditKind.Move"/>
        /// only, <see cref="CadEditRule.ReadOnly"/> none.
        /// </summary>
        /// <param name="rule">The rule.</param>
        /// <param name="kind">The kind of edit.</param>
        /// <returns>Whether the edit may go ahead.</returns>
        public static bool Allows(CadEditRule rule, CadEditKind kind)
        {
            switch (rule)
            {
                case CadEditRule.Free:
                case CadEditRule.Edit:
                    return true;
                case CadEditRule.MoveOnly:
                    return kind == CadEditKind.Move;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Hands an edit of an owned entity to its owner, as the presenter does before it changes one: refused where the
        /// entity's rule does not allow the kind (<see cref="Allows"/>), and otherwise the owner's answer. An entity whose
        /// rule is <see cref="CadEditRule.Free"/> is edited by the presenter itself and not handed over: the answer for it is
        /// false, and so is the answer for a handle the document has no entity of.
        /// </summary>
        /// <param name="document">The document the entity is in.</param>
        /// <param name="edit">The edit.</param>
        /// <returns>Whether the owner took the edit.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the document or the edit is null.</exception>
        public bool TryApply(CadDocument document, CadEdit edit)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (edit == null)
            {
                throw new ArgumentNullException(nameof(edit));
            }

            DxfEntity entity = document.FindEntity(edit.Handle);

            if (entity == null)
            {
                return false;
            }

            CadEditRule rule = GetEditRule(entity);

            if (rule == CadEditRule.Free || !Allows(rule, edit.Kind))
            {
                return false;
            }

            ICadEntityOwner owner = FindOwner(entity);

            // While the owner applies, the document knows the edits it makes are the owner's own, not to be handed back to it.
            document.EnterOwner();

            try
            {
                return owner.TryApply(document, edit);
            }
            catch (Exception exception)
            {
                GeometryHelperLog.Warn("CadOwnerRegistry: the owner " + owner.Name + " failed to take the edit " + edit + "; it is refused.", exception);
                return false;
            }
            finally
            {
                document.LeaveOwner();
            }
        }
    }
}
