using System;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The gate a document asks before it changes an entity, made from the owners registered with it: an entity with no
    /// owner tag is edited freely, one of an owner is edited as that owner's <see cref="CadEditRule"/> says (a move only,
    /// nothing at all, or anything), and a refusal says why in the words the command line shows.
    /// </summary>
    public sealed class CadOwnerEditGate : ICadEditGate
    {
        private readonly CadOwnerRegistry _registry;

        /// <summary>Makes a gate over a registry of owners.</summary>
        /// <param name="registry">The registry.</param>
        /// <exception cref="ArgumentNullException">Thrown when the registry is null.</exception>
        public CadOwnerEditGate(CadOwnerRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        /// <inheritdoc/>
        public bool Allows(CadDocument document, DxfEntity entity, CadEditRecord record, out string reason)
        {
            reason = null;

            if (entity == null || record == null || record.Kind == CadEditRecordKind.Added)
            {
                return true;
            }

            CadEditRule rule = _registry.GetEditRule(entity);

            if (rule == CadEditRule.Free || CadOwnerRegistry.Allows(rule, KindOf(record)))
            {
                return true;
            }

            string what = "This " + Describe(entity);
            reason = rule == CadEditRule.MoveOnly
                ? what + " belongs to the model and can only be moved."
                : what + " belongs to the model and cannot be changed here.";
            return false;
        }

        /// <summary>Gets which kind of edit of the owner contract a planned change is: a transformation is the move, rotation, scale or mirror its property names, a grip drag that only translates (an insertion point, a centre, a middle grip) is a move, and reshaping is a grip edit.</summary>
        /// <param name="record">The planned change.</param>
        /// <returns>The kind.</returns>
        public static CadEditKind KindOf(CadEditRecord record)
        {
            if (record == null)
            {
                throw new ArgumentNullException(nameof(record));
            }

            switch (record.Kind)
            {
                case CadEditRecordKind.Erased:
                    return CadEditKind.Erase;
                case CadEditRecordKind.PropertyChanged:
                    return CadEditKind.Property;
                case CadEditRecordKind.Transformed:
                    switch (record.Property)
                    {
                        case "Move":
                            return CadEditKind.Move;
                        case "Rotate":
                            return CadEditKind.Rotate;
                        case "Scale":
                            return CadEditKind.Scale;
                        case "Mirror":
                            return CadEditKind.Mirror;
                        default:
                            return record.Property != null && record.Property.StartsWith("Grip", StringComparison.Ordinal) ? CadEditKind.Move : CadEditKind.Grip;
                    }

                default:
                    return CadEditKind.Grip;
            }
        }

        internal static string Describe(DxfEntity entity)
        {
            string name = entity.GetType().Name;

            if (name.StartsWith("Dxf", StringComparison.Ordinal))
            {
                name = name.Substring(3);
            }

            if (name == "DBText")
            {
                name = "text";
            }

            if (name == "MText")
            {
                name = "multiline text";
            }

            if (name == "BlockReference")
            {
                name = "block";
            }

            return name.ToLowerInvariant();
        }
    }
}
