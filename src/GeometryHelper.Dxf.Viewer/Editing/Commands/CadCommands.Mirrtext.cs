using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>MIRROR with MIRRTEXT 0: texts kept readable, as AutoCAD 2021 keeps them.</summary>
    internal static partial class CadCommands
    {
        /// <summary>
        /// How MIRROR with MIRRTEXT 0 moves an object so that its words stay readable, as AutoCAD 2021 moves them (probed in its
        /// console): a TEXT or attribute definition, an MTEXT, and the attributes of an INSERT; null for any other object, which
        /// is mirrored. The words keep reading within the half of the circle they read in before, the half from just past
        /// -90 degrees to 90 or from just past 90 to 270: "ABC" turned 30 degrees and mirrored across an upright reads at 330,
        /// and across a level line at 330 too; turned 120 and mirrored across an upright, at 240. To do so the object is turned
        /// over in its own plane before the mirror, across its upright where the mirror would reverse it, else across its
        /// baseline: a TEXT about the middle of its words (of its two points where it is aligned or fitted), or about the
        /// middle of its height; an MTEXT about its point, its attachment then turned over too, left for right or top for
        /// bottom. A text whose plane is not the drawing's is mirrored as it is.
        /// </summary>
        private static ReadableMirror Readable(
            CadDocument document,
            DxfEntity entity,
            GeoTransform3 mirror,
            Tolerance tolerance
        )
        {
            switch (entity)
            {
                case DxfDBText text:
                    GeoTransform3 flip = TextFlip(document, text, mirror, tolerance);
                    return flip == null ? null : new ReadableMirror(mirror.Multiply(flip), null, null);
                case DxfMText text:
                    return MTextFlip(text, mirror, tolerance);
                case DxfBlockReference insert:
                    var attributes = new List<GeoTransform3>();
                    bool any = false;

                    foreach (DxfAttributeReference attribute in insert.AttributeCollection)
                    {
                        // The attribute is mirrored with its INSERT, then turned over across the mirror of the line it is turned over across.
                        GeoTransform3 over = TextFlip(document, attribute, mirror, tolerance);
                        attributes.Add(over == null ? null : mirror.Multiply(over).Multiply(mirror));
                        any |= over != null;
                    }

                    return any ? new ReadableMirror(mirror, null, attributes) : null;
                default:
                    return null;
            }
        }

        /// <summary>
        /// How MIRROR moves a dimension, under MIRRTEXT 0 or 1, as AutoCAD 2021 moves one (probed in its console): its points
        /// mirrored, its plane kept the drawing's rather than turned over, and its picture drawn anew, so that its words read
        /// as before. A linear dimension along x mirrored across an upright then runs at 180 degrees, its normal still (0, 0, 1).
        /// Null for one whose plane is not the drawing's, which is mirrored as it is.
        /// </summary>
        private static ReadableMirror ReadableDimension(
            DxfDimension dimension,
            GeoTransform3 mirror,
            Tolerance tolerance
        ) => Flat(dimension.Normal, tolerance) ? new ReadableMirror(mirror, null, null, true) : null;

        /// <summary>The turn-over of a TEXT, attribute or definition in its own plane before the mirror; null where its plane is not the drawing's.</summary>
        private static GeoTransform3 TextFlip(
            CadDocument document,
            DxfDBText text,
            GeoTransform3 mirror,
            Tolerance tolerance
        )
        {
            if (!Flat(text.Normal, tolerance))
            {
                return null;
            }

            double turn = text.Rotation;
            var along = new GeoVector3(Math.Cos(turn), Math.Sin(turn), 0.0);
            var up = new GeoVector3(-Math.Sin(turn), Math.Cos(turn), 0.0);

            if (AcrossUpright(along, mirror, tolerance))
            {
                return GeoTransform3.Mirror(new GeoPlane3(Middle(document, text, along), along));
            }

            GeoPoint3 at = text.Position;
            return GeoTransform3.Mirror(new GeoPlane3(at.Add(up.Multiply(text.Height / 2.0)), up));
        }

        /// <summary>
        /// The middle of a text's words along its baseline, which a turn-over across its upright keeps: of its two points where
        /// it is aligned or fitted, as AutoCAD keeps the one the mirror of the other; else of the words as the display measures
        /// them about the point it places them by.
        /// </summary>
        private static GeoPoint3 Middle(CadDocument document, DxfDBText text, GeoVector3 along)
        {
            DxfTextHorizontalMode mode = text.HorizontalMode;

            if (
                (mode == DxfTextHorizontalMode.TextAlign || mode == DxfTextHorizontalMode.TextFit)
                && text.VerticalMode == DxfTextVerticalMode.TextBase
            )
            {
                GeoPoint3 first = text.Position;
                GeoPoint3 second = text.AlignmentPoint;
                return new GeoPoint3(
                    (first.X + second.X) / 2.0,
                    (first.Y + second.Y) / 2.0,
                    (first.Z + second.Z) / 2.0
                );
            }

            GeoPoint3 at = text.IsDefaultAlignment ? text.Position : text.AlignmentPoint;
            double width = document.TextWidth(text);
            double from =
                mode == DxfTextHorizontalMode.TextCenter || mode == DxfTextHorizontalMode.TextMid ? 0.0
                : mode == DxfTextHorizontalMode.TextRight ? -width / 2.0
                : width / 2.0;
            return at.Add(along.Multiply(from));
        }

        /// <summary>The turn-over of an MTEXT about its point, with its attachment turned over as it is; null where its plane is not the drawing's.</summary>
        private static ReadableMirror MTextFlip(DxfMText text, GeoTransform3 mirror, Tolerance tolerance)
        {
            if (!Flat(text.Normal, tolerance))
            {
                return null;
            }

            GeoVector3 along = text.Direction;
            along = new GeoVector3(along.X, along.Y, 0.0);

            if (!(along.Length > tolerance.EqualVector))
            {
                return null;
            }

            along = along.Normalize();
            var up = new GeoVector3(-along.Y, along.X, 0.0);
            bool upright = AcrossUpright(along, mirror, tolerance);
            int at = (int)text.Attachment;
            DxfAttachmentPoint? attachment = null;

            if (at >= 1 && at <= 9)
            {
                int column = (at - 1) % 3;
                int row = (at - 1) / 3;
                attachment = (DxfAttachmentPoint)(upright ? row * 3 + (2 - column) + 1 : (2 - row) * 3 + column + 1);
            }

            GeoTransform3 flip = GeoTransform3.Mirror(new GeoPlane3(text.Location, upright ? along : up));
            return new ReadableMirror(mirror.Multiply(flip), attachment, null);
        }

        /// <summary>
        /// Whether the turn-over is across the text's upright, reversing its baseline: where its baseline mirrored and reversed
        /// reads in the half of the circle it reads in now; else it is across its baseline.
        /// </summary>
        private static bool AcrossUpright(GeoVector3 along, GeoTransform3 mirror, Tolerance tolerance)
        {
            GeoVector3 mirrored = mirror.Transform(along);
            return RightHalf(along, tolerance) == RightHalf(new GeoVector3(-mirrored.X, -mirrored.Y, 0.0), tolerance);
        }

        /// <summary>
        /// Whether a direction reads in the right half of the circle, from just past -90 degrees to 90, as AutoCAD 2021 halves it:
        /// one straight up is in it, one straight down is not.
        /// </summary>
        private static bool RightHalf(GeoVector3 along, Tolerance tolerance) =>
            along.X > tolerance.EqualVector || (Math.Abs(along.X) <= tolerance.EqualVector && along.Y > 0.0);

        /// <summary>Whether a normal is the drawing's, up the Z axis, to the tolerance.</summary>
        private static bool Flat(GeoVector3 normal, Tolerance tolerance) =>
            Math.Abs(normal.X) <= tolerance.EqualVector
            && Math.Abs(normal.Y) <= tolerance.EqualVector
            && normal.Z > 0.0;

        /// <summary>What MIRROR does to one object it keeps readable: the transformation, the attachment of an MTEXT, and the transformation of each attribute of an INSERT after it.</summary>
        private sealed class ReadableMirror
        {
            private readonly DxfAttachmentPoint? _attachment;
            private readonly List<GeoTransform3> _attributes;
            private readonly bool _upright;

            public ReadableMirror(
                GeoTransform3 transform,
                DxfAttachmentPoint? attachment,
                List<GeoTransform3> attributes,
                bool upright = false
            )
            {
                Transform = transform;
                _attachment = attachment;
                _attributes = attributes;
                _upright = upright;
            }

            /// <summary>The transformation the object is moved by.</summary>
            public GeoTransform3 Transform { get; }

            /// <summary>
            /// Finishes the object once moved: an MTEXT's attachment set, an INSERT's attributes turned over in turn, a
            /// dimension's plane put back to the drawing's and its picture drawn anew.
            /// </summary>
            public void Finish(DxfEntity moved)
            {
                if (_upright && moved is DxfDimension dimension)
                {
                    dimension.Normal = new GeoVector3(0.0, 0.0, 1.0);
                    Redraw(dimension);
                }

                if (_attachment.HasValue && moved is DxfMText text)
                {
                    text.Attachment = _attachment.Value;
                }

                if (_attributes != null && moved is DxfBlockReference insert)
                {
                    int i = 0;

                    foreach (
                        DxfAttributeReference attribute in new List<DxfAttributeReference>(insert.AttributeCollection)
                    )
                    {
                        if (i < _attributes.Count && _attributes[i] != null)
                        {
                            attribute.TransformBy(_attributes[i]);
                        }

                        i++;
                    }
                }
            }
        }
    }
}
