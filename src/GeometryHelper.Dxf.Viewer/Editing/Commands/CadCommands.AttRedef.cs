using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// ATTREDEF, as AutoCAD 2021's Express Tools routine does it: "Enter name of the block you wish to redefine:", "Select
    /// objects for new Block...", "Specify insertion base point of new Block:". The block is defined again from the objects
    /// (which are used up, as -BLOCK uses them) and every reference of it in the space shown is put anew, at the same point, scale,
    /// rotation and normal: an attribute whose tag the new block has keeps the value it had, one new to the block gets the default
    /// of its definition, one the new block no longer has is dropped. As AutoCAD's does, the new references are drawn with the
    /// drawing's current layer, colour and linetype, not those of the old ones, and anything else an old reference carried (the
    /// formats ATTEDIT set, extended data) is left behind. A reference on a locked layer is left as it is, and a new block with no
    /// attribute that is not constant is refused.
    /// </summary>
    internal static partial class CadCommands
    {
        private static IEnumerable<CadPrompt> AttRedef(CadCommandContext c)
        {
            DxfBlockTableRecord block = null;

            while (block == null)
            {
                yield return CadPrompt.ForString("Enter name of the block you wish to redefine");

                if (c.Input.Kind != CadInputKind.Text || c.Input.Text.Trim().Length == 0)
                {
                    yield break;
                }

                string typed = c.Input.Text.Trim();

                if (typed.Length > 1 && typed.StartsWith("\"", StringComparison.Ordinal) && typed.EndsWith("\""))
                {
                    typed = typed.Substring(1, typed.Length - 2).Trim();
                }

                if (
                    c.Database.BlockTable.Has(typed)
                    && !c.Database.BlockTable[typed].IsLayout
                    && !c.Database.BlockTable[typed].IsFromExternalReference
                )
                {
                    block = c.Database.BlockTable[typed];
                }
                else
                {
                    c.Write("Block \"" + typed.ToUpperInvariant() + "\" is not defined. Please try again.");
                }
            }

            string name = block.Name;
            var olds = new List<DxfBlockReference>();

            foreach (DxfEntity entity in c.Space.Block)
            {
                if (
                    entity is DxfBlockReference reference
                    && !reference.IsErased
                    && string.Equals(reference.Name, name, StringComparison.OrdinalIgnoreCase)
                    && !CadLayerEditor.IsLocked(c.Database, reference.Layer)
                )
                {
                    olds.Add(reference);
                }
            }

            if (olds.Count == 0)
            {
                c.Write("No insertions of block " + name + " found to redefine.");
                yield break;
            }

            while (true)
            {
                c.Write("Select objects for new Block...");

                foreach (CadPrompt prompt in c.SelectObjects())
                {
                    yield return prompt;
                }

                if (c.Selected.Count > 0)
                {
                    break;
                }

                c.Write("No new Block selected. Please try again.");
            }

            int attributes = c
                .Selected.Select(h => c.Document.FindEntity(h))
                .OfType<DxfAttributeDefinition>()
                .Count(d => !d.Constant);

            if (attributes == 0)
            {
                c.Write("New block has no attributes.");
                yield break;
            }

            yield return CadPrompt.ForPoint("Specify insertion base point of new Block");

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            GeoPoint3 origin = P3(c.Session.Ucs.ToWorld(c.Input.Point));

            // What the references hold before the block changes: where each stands, and each attribute's value by its tag.
            var before = new List<OldReference>();

            foreach (DxfBlockReference old in olds)
            {
                var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach (DxfAttributeReference attribute in old.AttributeCollection)
                {
                    if (!values.ContainsKey(attribute.Tag))
                    {
                        values[attribute.Tag] = attribute.TextString;
                    }
                }

                before.Add(
                    new OldReference
                    {
                        Handle = old.Handle,
                        Position = old.Position,
                        Scale = old.ScaleFactors,
                        Rotation = old.Rotation,
                        Normal = old.Normal,
                        Values = values,
                    }
                );
            }

            var request = new CadBlockDefinitionRequest(c.Document)
            {
                Name = name,
                Redefine = true,
                BasePoint = origin,
                Objects = c.Selected,
                ObjectAction = CadBlockObjectAction.Delete,
                ScaleUniformly = block.BlockScaling == DxfBlockScaling.Uniform,
                AllowExploding = block.Explodable,
                Description = block.Comments,
                Units = block.Units,
            };
            MakeBlock(c, request);

            // The definition made, the old references go and new ones stand in their places.
            DxfBlockTableRecord made = c.Database.BlockTable.Has(name) ? c.Database.BlockTable[name] : null;

            if (made == null || !Definitions(made).Any())
            {
                yield break;
            }

            c.Apply(before.Select(o => new CadEditRecord(CadEditRecordKind.Erased, o.Handle)).ToList(), s =>
            {
                DxfBlockTableRecord record = s.Database.BlockTable[name];
                List<DxfAttributeDefinition> definitions = Definitions(record);

                foreach (OldReference old in before)
                {
                    var values = new List<string>();

                    foreach (DxfAttributeDefinition definition in definitions)
                    {
                        values.Add(old.Values.TryGetValue(definition.Tag, out string kept) ? kept : null);
                    }

                    var insert = new DxfBlockReference(old.Position, record);
                    Current(c, insert);
                    insert.Normal = old.Normal;
                    insert.ScaleFactors = old.Scale;
                    insert.Rotation = old.Rotation;
                    s.Append(c.Space, insert);
                    AddAttributes(insert, record, values);
                    s.Document.FindEntity(old.Handle)?.Erase();
                }
            });
        }

        /// <summary>What ATTREDEF keeps of a reference before its block is defined again.</summary>
        private sealed class OldReference
        {
            public string Handle;
            public GeoPoint3 Position;
            public GeoVector3 Scale;
            public double Rotation;
            public GeoVector3 Normal;
            public Dictionary<string, string> Values;
        }
    }
}
