using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Drawing.Annotation
{
    /// <summary>
    /// Makes the block a DIMENSION written in code is drawn by. The library draws no dimension itself: what AutoCAD and the
    /// presenter show of one is the block it names, which AutoCAD makes when it draws a dimension and a dimension made in
    /// code has not. This makes it as AutoCAD would for the layout of the dimension.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The block holds, on the sheet, the extension lines, the dimension line, an arrow at each end of it as a SOLID (a
    /// closed filled arrow as long as the text is high, a third of that wide) and the text as an MTEXT, its middle on the
    /// text's place; an ordinate dimension holds its leader and text only. Arrows go inside a dimension line at least twice
    /// their length, outside a shorter one, the line then run on by an arrow's length at each end. Every entity is on layer
    /// 0 in the colour BYBLOCK, as AutoCAD makes them, so the dimension's layer gives their colour.
    /// </para>
    /// <para>
    /// The block is an anonymous one named as AutoCAD names its own, <c>*D</c> and the first number no block of the drawing
    /// has (<c>*D1</c>, <c>*D2</c>), a new one each time a dimension is written; the dimension keeps it as its
    /// <see cref="DxfDimension.DimBlock"/>, and an update that erases the dimension erases its block with it
    /// (<see cref="DrawingEntityTags.RemoveAll"/>).
    /// </para>
    /// </remarks>
    internal static class DimensionBlockBuilder
    {
        /// <summary>
        /// Makes the block that draws a dimension, placed on the sheet as the placement puts its view.
        /// </summary>
        internal static DxfBlockTableRecord Build(DxfDatabase database, DrawingDimension dimension, ViewPlacement2 placement)
        {
            var block = new DxfBlockTableRecord { Name = "*D", Origin = new GeoPoint3(0.0, 0.0, 0.0) };
            database.BlockTable.Add(block);
            double arrow = dimension.TextHeight / placement.Scale;

            foreach (GeoLine2 extension in dimension.ExtensionLines)
            {
                Append(block, new DxfLine(placement.ToSheet(extension.StartPoint), placement.ToSheet(extension.EndPoint)));
            }

            if (dimension.DimensionLine.HasValue)
            {
                GeoLine2 line = dimension.DimensionLine.Value;
                GeoPoint3 a = placement.ToSheet(line.StartPoint);
                GeoPoint3 b = placement.ToSheet(line.EndPoint);
                double length = a.DistanceTo(b);
                GeoVector3 along = length > 0.0 ? a.GetVectorTo(b).Multiply(1.0 / length) : GeoVector3.XAxis;
                bool inside = length >= 2.0 * arrow;

                if (inside)
                {
                    Append(block, new DxfLine(a, b));
                    Append(block, Arrow(a, along.Negate(), arrow));
                    Append(block, Arrow(b, along, arrow));
                }
                else
                {
                    Append(block, new DxfLine(a.Subtract(along.Multiply(arrow)), b.Add(along.Multiply(arrow))));
                    Append(block, Arrow(a, along, arrow));
                    Append(block, Arrow(b, along.Negate(), arrow));
                }
            }

            var text = new DxfMText
            {
                Location = placement.ToSheet(dimension.TextPosition),
                TextHeight = dimension.TextHeight / placement.Scale,
                Rotation = dimension.TextRotation,
                Attachment = DxfAttachmentPoint.MiddleCenter,
                Contents = dimension.Text,
            };

            Append(block, text);
            return block;
        }

        /// <summary>A closed filled arrow, its tip at a point and pointing along a unit direction.</summary>
        private static DxfSolid Arrow(GeoPoint3 tip, GeoVector3 pointing, double length)
        {
            GeoPoint3 back = tip.Subtract(pointing.Multiply(length));
            var across = new GeoVector3(-pointing.Y * length / 6.0, pointing.X * length / 6.0, 0.0);
            return new DxfSolid(tip, back.Add(across), back.Subtract(across));
        }

        private static void Append(DxfBlockTableRecord block, DxfEntity entity)
        {
            entity.Layer = "0";
            entity.ColorIndex = 0;
            block.AppendEntity(entity);
        }
    }
}
