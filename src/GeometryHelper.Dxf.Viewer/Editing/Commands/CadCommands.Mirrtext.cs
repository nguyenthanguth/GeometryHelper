using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>MIRROR's texts and multileaders: kept readable with MIRRTEXT 0, mirrored in their plane with 1, as AutoCAD 2021 leaves them.</summary>
    internal static partial class CadCommands
    {
        /// <summary>
        /// How MIRROR with MIRRTEXT 0 moves an object so that its words stay readable, as AutoCAD 2021 moves them (probed in its
        /// console): a TEXT or attribute definition, an MTEXT, and the attributes of an INSERT; null for any other object, which
        /// is mirrored. The words keep reading within the half of the circle they read in before, the half from just past
        /// -90 degrees to 90 or from just past 90 to 270: "ABC" turned 30 degrees and mirrored across an upright reads at 330,
        /// and across a level line at 330 too; turned 120 and mirrored across an upright, at 240. To do so the object is turned
        /// over in its own plane before the mirror, across its upright where the mirror would reverse it, else across its
        /// baseline: a TEXT about the middle of the ink of its words as <see cref="Middle"/> finds it (of its two points where
        /// it is aligned or fitted), or about the middle of its height, the copy then written turned from nought to a whole turn; an MTEXT about its point, its attachment then turned over too, left for right or top for
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

                    if (flip == null)
                    {
                        return null;
                    }

                    // Copied as it is, then put where the turn-over and the mirror take it, so that its turn is written from
                    // nought to a whole turn and its width as it was, as AutoCAD writes them.
                    TextState was = TextState.Of(text);
                    GeoTransform3 moved = mirror.Multiply(flip);
                    return new ReadableMirror(GeoTransform3.Identity, null, null, then: m =>
                        was.Moved((DxfDBText)m, moved)
                    );
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

        /// <summary>
        /// How MIRROR with MIRRTEXT 1 mirrors a TEXT or attribute definition, or the attributes of an INSERT, as AutoCAD 2021
        /// mirrors them (probed in its console, mirror-mleader GHMM1): in their plane, their normal still the drawing's, their
        /// points mirrored, turned to their mirrored upright less a quarter turn, and written backwards, or forwards again
        /// (flag 2 of 71 turned; 4 left as it is). "ABC" at the origin mirrored across x = 100 stands at (200, 0) turned
        /// nought, backwards; turned 30 degrees, at 330; mirrored across y = 100, at (0, 200) turned 180, backwards, not upside
        /// down. Null for any other object, and for a text whose plane is not the drawing's, which is mirrored as it is.
        /// </summary>
        private static ReadableMirror InPlane(DxfEntity entity, GeoTransform3 mirror, Tolerance tolerance)
        {
            switch (entity)
            {
                case DxfDBText text:
                    if (!Flat(text.Normal, tolerance))
                    {
                        return null;
                    }

                    // Copied as it is, then put in place, so that its normal is never written turned over.
                    TextState was = TextState.Of(text);
                    return new ReadableMirror(GeoTransform3.Identity, null, null, then: moved =>
                        was.Mirrored((DxfDBText)moved, mirror)
                    );
                case DxfBlockReference insert:
                    var states = new List<TextState>();

                    // An attribute upside down, its normal down the Z axis, as one of an INSERT already mirrored, keeps it: mirrored in
                    // its plane as one up the axis is (mirror-insert GHMI1, the INSERT scaled -1 by 1 mirrored across x = 100).
                    foreach (DxfAttributeReference attribute in insert.AttributeCollection)
                    {
                        states.Add(
                            Flat(attribute.Normal, tolerance) || Flat(attribute.Normal.Negate(), tolerance)
                                ? TextState.Of(attribute)
                                : null
                        );
                    }

                    if (states.TrueForAll(s => s == null))
                    {
                        return null;
                    }

                    return new ReadableMirror(mirror, null, null, then: moved =>
                    {
                        int i = 0;

                        foreach (
                            DxfAttributeReference attribute in new List<DxfAttributeReference>(
                                ((DxfBlockReference)moved).AttributeCollection
                            )
                        )
                        {
                            if (i < states.Count && states[i] != null)
                            {
                                // Mirrored with its INSERT, its normal turned over: put back, then in place.
                                attribute.Normal = states[i].Normal;
                                states[i].Mirrored(attribute, mirror);
                            }

                            i++;
                        }
                    });
                default:
                    return null;
            }
        }

        /// <summary>
        /// How MIRROR moves a multileader, under MIRRTEXT 0 or 1, as AutoCAD 2021 moves one (probed in its console,
        /// mirror-mleader GHMM1 and GHMM3): mirrored, then its plane, words and base point put as AutoCAD's MIRROR puts them
        /// (<see cref="DxfMLeader.FinishMirror"/>), its words measured as the display lays them out. Null for one whose plane is
        /// not the drawing's, which is mirrored as it is.
        /// </summary>
        private static ReadableMirror MLeaderMirror(
            CadDocument document,
            DxfMLeader mleader,
            GeoTransform3 mirror,
            bool mirrtext,
            Tolerance tolerance
        )
        {
            if (!Flat(mleader.Normal, tolerance))
            {
                return null;
            }

            var source = (DxfMLeader)mleader.Clone();
            double width = 0.0;
            double height = 0.0;
            DxfMText words = mleader.ContentType == DxfContentType.MTextContent ? mleader.MText : null;

            if (words != null)
            {
                CadMTextLayout layout = document.LayoutOf(words);
                width = layout.ActualWidth;
                height = layout.ActualHeight;
            }

            return new ReadableMirror(mirror, null, null, then: moved =>
                ((DxfMLeader)moved).FinishMirror(source, mirror, mirrtext, width, height)
            );
        }

        /// <summary>
        /// How MIRROR moves an INSERT, under MIRRTEXT 0 or 1, as AutoCAD 2021 moves one (probed in its console, mirror-insert
        /// GHMI1): its attributes as <see cref="Readable(CadDocument, DxfEntity, GeoTransform3, Tolerance)"/> or
        /// <see cref="InPlane"/> leave them, and the INSERT written with its
        /// x factor turned the other way rather than its y, turned to its mirrored y axis less a quarter turn. One turned nought
        /// mirrored across a level line is written x factor -1 turned 180 degrees; across an upright, -1 turned nought; one
        /// scaled 2 by 3, -2 by 3. A MINSERT, and an INSERT whose plane is not the drawing's, is mirrored as it is.
        /// </summary>
        private static ReadableMirror InsertMirror(
            CadDocument document,
            DxfBlockReference insert,
            GeoTransform3 mirror,
            bool mirrtext,
            Tolerance tolerance
        )
        {
            ReadableMirror attributes = mirrtext
                ? InPlane(insert, mirror, tolerance)
                : Readable(document, insert, mirror, tolerance);

            if (insert is DxfMInsertBlock || !Flat(insert.Normal, tolerance))
            {
                return attributes;
            }

            GeoVector3 scale = insert.ScaleFactors;
            double turn = insert.Rotation;
            GeoVector3 up = mirror.Transform(new GeoVector3(-Math.Sin(turn), Math.Cos(turn), 0.0));
            double degrees = (Math.Atan2(up.Y, up.X) * (180.0 / Math.PI)) - 90.0;
            return new ReadableMirror(attributes?.Transform ?? mirror, null, null, then: moved =>
            {
                attributes?.Finish(moved);
                ((DxfBlockReference)moved).FinishMirror(scale, degrees);
            });
        }

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

            // Across the middle of the ink of its words, where the display's fonts read their letters, as AutoCAD 2021 turns it
            // over (mirror-insert GHMI1): "Q1" of arial.ttf 1.5 high, whose Q reaches below the baseline, about a line 0.705 above
            // it, not 0.75; else across the middle of its height.
            GeoPoint3 at = text.Position;
            double middle = document.TextInkHeight(text, out double bottom, out double top)
                ? (bottom + top) / 2.0
                : text.Height / 2.0;
            return GeoTransform3.Mirror(new GeoPlane3(at.Add(up.Multiply(middle)), up));
        }

        /// <summary>
        /// The middle of a text's words along its baseline, which a turn-over across its upright keeps, as AutoCAD 2021 keeps it
        /// (probed in its console, mirror-mleader GHMM1 and GHMM2): of its two points where it is aligned or fitted, as AutoCAD
        /// keeps the one the mirror of the other; its point where it is in the middle; on the right of its baseline, the middle
        /// from where its words start to its point; else the middle of the ink of its words, where the display's fonts read
        /// their letters, so that the ink of the copy is the mirror of the ink: "ABC" of txt.shx 2.5 high at the origin, inked
        /// to 6.667, mirrored across x = 100 starts at 193.333. Where the letters are not read, the middle of the words as the
        /// display measures them about the point it places them by.
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

            if (
                mode != DxfTextHorizontalMode.TextMid
                && document.TextInk(text, out double start, out double left, out double right)
            )
            {
                bool box = mode == DxfTextHorizontalMode.TextRight && text.VerticalMode == DxfTextVerticalMode.TextBase;
                return at.Add(along.Multiply(box ? start / 2.0 : (left + right) / 2.0));
            }

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

        /// <summary>What MIRROR does to one object it keeps readable or mirrors in its plane: the transformation, the attachment of an MTEXT, the transformation of each attribute of an INSERT after it, and what is put right after.</summary>
        private sealed class ReadableMirror
        {
            private readonly DxfAttachmentPoint? _attachment;
            private readonly List<GeoTransform3> _attributes;
            private readonly bool _upright;
            private readonly Action<DxfEntity> _then;

            public ReadableMirror(
                GeoTransform3 transform,
                DxfAttachmentPoint? attachment,
                List<GeoTransform3> attributes,
                bool upright = false,
                Action<DxfEntity> then = null
            )
            {
                Transform = transform;
                _attachment = attachment;
                _attributes = attributes;
                _upright = upright;
                _then = then;
            }

            /// <summary>The transformation the object is moved by.</summary>
            public GeoTransform3 Transform { get; }

            /// <summary>
            /// Finishes the object once moved: an MTEXT's attachment set, an INSERT's attributes turned over in turn, a
            /// dimension's plane put back to the drawing's and its picture drawn anew, then whatever else is put right.
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

                _then?.Invoke(moved);
            }
        }

        /// <summary>A TEXT, attribute or definition as it was before MIRROR moved it: its points, turn, lean and flags.</summary>
        private sealed class TextState
        {
            private GeoPoint3 _position;
            private GeoPoint3 _alignment;
            private bool _aligned;
            private double _rotation;
            private double _oblique;
            private bool _backward;
            private bool _upsideDown;

            /// <summary>Its normal, up or down the Z axis: (0, 0, 1) for one up, (0, 0, -1) for one down.</summary>
            public GeoVector3 Normal { get; private set; }

            public static TextState Of(DxfDBText text) =>
                new TextState
                {
                    Normal = new GeoVector3(0.0, 0.0, text.Normal.Z > 0.0 ? 1.0 : -1.0),
                    _position = text.Position,
                    _alignment = text.AlignmentPoint,
                    _aligned = !text.IsDefaultAlignment,
                    _rotation = text.Rotation,
                    _oblique = text.Oblique,
                    _backward = text.IsMirroredInX,
                    _upsideDown = text.IsMirroredInY,
                };

            /// <summary>
            /// Puts a copy of the text, still as it was, where a turn in the drawing's plane takes it, as MIRROR with MIRRTEXT 0
            /// leaves it: its points and its baseline turned with it.
            /// </summary>
            public void Moved(DxfDBText text, GeoTransform3 turn)
            {
                GeoVector3 along = turn.Transform(new GeoVector3(Math.Cos(_rotation), Math.Sin(_rotation), 0.0));
                Place(text, turn, Math.Atan2(along.Y, along.X));
            }

            /// <summary>
            /// Puts a copy of the text, still as it was, where MIRROR with MIRRTEXT 1 leaves it (<see cref="InPlane"/>): its
            /// points the mirrored ones, turned to its mirrored upright less a quarter turn, its lean as it was, written backwards
            /// or forwards again.
            /// </summary>
            public void Mirrored(DxfDBText text, GeoTransform3 mirror)
            {
                GeoVector3 up = new GeoVector3(-Math.Sin(_rotation), Math.Cos(_rotation), 0.0);

                // One down the Z axis is turned in the system of its plane, whose x axis runs against the world's.
                double flip = Normal.Z > 0.0 ? 1.0 : -1.0;
                up = mirror.Transform(new GeoVector3(flip * up.X, up.Y, 0.0));
                Place(text, mirror, Math.Atan2(up.Y, flip * up.X) - (Math.PI / 2.0));
                text.Oblique = _oblique;
                text.IsMirroredInX = !_backward;
                text.IsMirroredInY = _upsideDown;
            }

            /// <summary>
            /// Its points carried by a transformation, and its turn, written from nought to a whole turn; one within a hair of
            /// nought or a whole turn written nought, as AutoCAD 2021 leaves it out.
            /// </summary>
            private void Place(DxfDBText text, GeoTransform3 transform, double turn)
            {
                turn %= 2.0 * Math.PI;
                turn = turn < 0.0 ? turn + (2.0 * Math.PI) : turn;
                text.Rotation = turn <= 1E-12 || turn >= (2.0 * Math.PI) - 1E-12 ? 0.0 : turn;
                text.Position = transform.Transform(_position);

                if (_aligned)
                {
                    text.AlignmentPoint = transform.Transform(_alignment);
                }
            }
        }
    }
}
