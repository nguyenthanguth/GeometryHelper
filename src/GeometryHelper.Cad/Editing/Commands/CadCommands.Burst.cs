using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Dxf;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// BURST, the Express Tools command, as AutoCAD 2021's own does it (its source reads it): "Select objects:" for block
    /// references of the space worked in, each exploded as EXPLODE explodes it but with its attributes kept as the TEXT (an
    /// attribute of several lines an MTEXT) of the value each shows, placed and looking as the attribute did, an invisible one
    /// too; a constant attribute of the block's definition becomes a TEXT of its value, a variable one's definition is left out
    /// (its value is the attribute's). What comes out on layer 0 goes to the layer of the reference; what is ByBlock
    /// takes the reference's colour and linetype. A block of 3 lines and the attribute MARK of value B-12 comes out as 3
    /// lines and the text B-12, on the layer of the reference. A block that may not be exploded, a MINSERT of more than one cell
    /// and the part of a model that owns it are passed over as EXPLODE passes them over, with the same count.
    /// </summary>
    internal static partial class CadCommands
    {
        private static IEnumerable<CadPrompt> Burst(CadCommandContext c)
        {
            foreach (CadPrompt prompt in c.SelectObjectsOf("Select objects", e => e is DxfBlockReference))
            {
                yield return prompt;
            }

            var parts = new List<KeyValuePair<string, IReadOnlyList<DxfEntity>>>();
            int refused = 0;

            foreach (string handle in c.Selected)
            {
                var insert = c.Document.FindEntity(handle) as DxfBlockReference;

                if (insert == null)
                {
                    continue;
                }

                if (c.Document.Owners.GetEditRule(insert) != CadEditRule.Free)
                {
                    refused++;
                    continue;
                }

                IReadOnlyList<DxfEntity> pieces = BurstPieces(insert);

                if (pieces == null)
                {
                    refused++;
                    continue;
                }

                parts.Add(new KeyValuePair<string, IReadOnlyList<DxfEntity>>(handle, pieces));
            }

            if (refused > 0)
            {
                c.Write(
                    refused.ToString(CultureInfo.InvariantCulture)
                        + (refused == 1 ? " was" : " were")
                        + " not able to be exploded."
                );
            }

            if (parts.Count == 0)
            {
                yield break;
            }

            var planned = new List<CadEditRecord>();

            foreach (KeyValuePair<string, IReadOnlyList<DxfEntity>> pair in parts)
            {
                planned.Add(new CadEditRecord(CadEditRecordKind.Erased, pair.Key));
            }

            c.Apply(planned, s =>
            {
                foreach (KeyValuePair<string, IReadOnlyList<DxfEntity>> pair in parts)
                {
                    if (s.IsHandled(pair.Key))
                    {
                        continue;
                    }

                    CadSpace space = s.Document.FindSpace(pair.Key);
                    DxfEntity entity = s.Document.FindEntity(pair.Key);

                    foreach (DxfEntity piece in pair.Value)
                    {
                        s.Append(space, piece);
                    }

                    entity.Erase();
                }
            });
        }

        /// <summary>
        /// What a block reference bursts into: the texts of its attributes, then the pieces its block explodes into, a constant
        /// attribute definition made a text and a variable one left out, the look of the reference given to what is on layer 0
        /// or ByBlock; null where the block cannot be exploded.
        /// </summary>
        private static IReadOnlyList<DxfEntity> BurstPieces(DxfBlockReference insert)
        {
            if (insert.BlockTableRecord != null && !insert.BlockTableRecord.Explodable)
            {
                return null;
            }

            IReadOnlyList<DxfEntity> exploded;

            try
            {
                exploded = insert.Explode();
            }
            catch (NotSupportedException)
            {
                return null;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
            catch (ArgumentException)
            {
                return null;
            }

            var made = new List<DxfEntity>();

            foreach (DxfAttributeReference attribute in insert.AttributeCollection)
            {
                made.Add(TextOfAttribute(attribute));
            }

            foreach (DxfEntity piece in exploded)
            {
                if (piece is DxfAttributeDefinition definition)
                {
                    if (definition.Constant)
                    {
                        made.Add(TextOfAttribute(definition));
                    }

                    continue;
                }

                made.Add(piece);
            }

            foreach (DxfEntity piece in made)
            {
                if (string.Equals(piece.Layer, "0", StringComparison.Ordinal))
                {
                    piece.Layer = insert.Layer;
                }

                if (piece.Color.IsByBlock)
                {
                    piece.Color = insert.Color;
                }

                if (string.Equals(piece.Linetype, "ByBlock", StringComparison.OrdinalIgnoreCase))
                {
                    piece.Linetype = insert.Linetype;
                }
            }

            return made;
        }

        /// <summary>
        /// The TEXT an attribute becomes, with its words, place, height, turn, width factor, slant, style, justification, mirroring,
        /// normal, thickness and look; an attribute of several lines the MTEXT it holds, kept as it is.
        /// </summary>
        private static DxfEntity TextOfAttribute(DxfDBText attribute)
        {
            DxfMText lines = null;

            if (attribute is DxfAttributeReference reference && reference.IsMTextAttribute)
            {
                lines = reference.MTextAttribute;
            }
            else if (attribute is DxfAttributeDefinition definition && definition.IsMTextAttributeDefinition)
            {
                lines = definition.MTextAttributeDefinition;
            }

            if (lines != null)
            {
                var block = (DxfMText)lines.Clone();
                block.SetPropertiesFrom(attribute);
                return block;
            }

            var text = new DxfDBText();
            text.SetPropertiesFrom(attribute);
            text.Normal = attribute.Normal;
            text.TextString = attribute.TextString;
            text.Position = attribute.Position;
            text.Height = attribute.Height;
            text.Rotation = attribute.Rotation;
            text.WidthFactor = attribute.WidthFactor;
            text.Oblique = attribute.Oblique;
            text.IsMirroredInX = attribute.IsMirroredInX;
            text.IsMirroredInY = attribute.IsMirroredInY;

            if (!string.IsNullOrEmpty(attribute.TextStyleName))
            {
                text.TextStyleName = attribute.TextStyleName;
            }

            if (!attribute.IsDefaultAlignment)
            {
                text.HorizontalMode = attribute.HorizontalMode;
                text.VerticalMode = attribute.VerticalMode;
                text.AlignmentPoint = attribute.AlignmentPoint;
            }

            return text;
        }
    }
}
