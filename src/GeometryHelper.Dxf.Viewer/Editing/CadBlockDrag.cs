using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// A block INSERT drags with the cursor while it asks where and how to place it, as AutoCAD drags it: the block's display
    /// items in its own coordinates, and the transformation that places them with the cursor at a point. At the insertion
    /// point the cursor carries the block's base point; at the scale it sets the opposite corner of a unit square, or the
    /// factor as its distance from the insertion point; at the rotation, the angle from the insertion point. A block drawn
    /// about (100, 70), dragged at scale 1 to (500, 500), has its circle about (100, 100) drawn about (500, 530).
    /// </summary>
    public sealed class CadBlockDrag
    {
        private readonly Lazy<IReadOnlyList<DisplayItem2>> _items;
        private readonly CadBlockDragMode _mode;
        private readonly GeoPoint2 _point;
        private readonly GeoVector3 _scale;
        private readonly double _rotation;
        private readonly GeoVector3 _shift;
        private readonly GeoPoint2 _origin;
        private readonly double _factor;

        internal CadBlockDrag(
            string block,
            Lazy<IReadOnlyList<DisplayItem2>> items,
            CadBlockDragMode mode,
            GeoPoint2 point,
            GeoVector3 scale,
            double rotation,
            GeoVector3 shift,
            GeoPoint3 origin,
            double factor
        )
        {
            Block = block;
            _items = items;
            _mode = mode;
            _point = point;
            _scale = scale;
            _rotation = rotation;
            _shift = shift;
            _origin = new GeoPoint2(origin.X, origin.Y);
            _factor = factor;
        }

        /// <summary>Gets the name of the block dragged.</summary>
        public string Block { get; }

        /// <summary>
        /// Gets the display items of the block in its own coordinates, as a space of its own draws them, its attribute
        /// definitions left out, as an INSERT shows none of them.
        /// </summary>
        public IReadOnlyList<DisplayItem2> Items => _items.Value;

        /// <summary>Gets the transformation that takes the block's coordinates to where the INSERT would place it with the cursor at a point.</summary>
        /// <param name="cursor">The cursor, in drawing units.</param>
        /// <returns>The transformation.</returns>
        public Affine2 Transform(GeoPoint2 cursor)
        {
            GeoPoint2 at = _point;
            double sx = _scale.X;
            double sy = _scale.Y;
            double turn = _rotation;
            GeoVector2 way = cursor - _point;

            switch (_mode)
            {
                case CadBlockDragMode.Point:
                    at = cursor;
                    break;
                case CadBlockDragMode.Corner:
                    sx = way.X;
                    sy = way.Y;
                    break;
                case CadBlockDragMode.Scale:
                    sx = way.Length;
                    sy = way.Length;
                    break;
                case CadBlockDragMode.Rotation:
                    turn = Math.Atan2(way.Y, way.X);
                    break;
            }

            // As INSERT places it: the base point picked brought to the insertion point by the shift scaled and turned, then
            // the block's origin there, its scale (and so the shift's) times the factor of its units.
            double cos = Math.Cos(turn);
            double sin = Math.Sin(turn);
            double shiftX = _shift.X * sx * _factor;
            double shiftY = _shift.Y * sy * _factor;
            double px = at.X + (cos * shiftX) - (sin * shiftY);
            double py = at.Y + (sin * shiftX) + (cos * shiftY);
            Affine2 placed = Affine2.ScaleRotate(sx * _factor, sy * _factor, turn);
            return Affine2.Translation(-_origin.X, -_origin.Y).Then(placed).Then(Affine2.Translation(px, py));
        }

        /// <summary>The display items of a block, as a space of its own draws them, its definitions of attributes that are not constant left out.</summary>
        internal static IReadOnlyList<DisplayItem2> ItemsOf(CadDocument document, string name)
        {
            DxfBlockTableRecord record = document.Database.BlockTable[name];
            var definitions = new HashSet<string>(
                record
                    .OfType<DxfAttributeDefinition>()
                    .Where(d => !d.Constant && d.Handle != null)
                    .Select(d => d.Handle),
                StringComparer.OrdinalIgnoreCase
            );
            DisplayList2 list = new DisplayBuilder(document.Database, document).Build(record, false);
            return list.Items.Where(i => i.Handle == null || !definitions.Contains(i.Handle)).ToList();
        }
    }

    /// <summary>What the cursor sets while a block is dragged.</summary>
    internal enum CadBlockDragMode
    {
        /// <summary>The insertion point.</summary>
        Point,

        /// <summary>The scales along X and Y, as the opposite corner of a unit square from the insertion point.</summary>
        Corner,

        /// <summary>One scale for every axis, as the distance from the insertion point.</summary>
        Scale,

        /// <summary>The rotation, as the angle from the insertion point.</summary>
        Rotation,
    }
}
