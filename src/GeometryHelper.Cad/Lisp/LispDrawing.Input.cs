using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Cad.Lisp
{
    /// <summary>
    /// <c>getpoint</c>, <c>getcorner</c>, <c>getdist</c>, <c>getangle</c>, <c>getorient</c>, <c>getint</c>, <c>getreal</c>,
    /// <c>getstring</c>, <c>getkword</c>, <c>entsel</c>, <c>nentsel</c> and <c>initget</c>. Each hands its question to the
    /// session with <see cref="LispInterpreter.Ask"/> and gives back the answer the session resumes it with.
    /// </summary>
    internal static partial class LispDrawing
    {
        static partial void RegisterInput(LispInterpreter lisp, CadCommandSession session)
        {
            lisp.Define("INITGET", 1, 2, a => Initget(lisp, a));
            lisp.Define("GETPOINT", 0, 2, a => lisp.Ask(WithPoint(lisp, LispAskKind.Point, a, baseRequired: false)));
            lisp.Define("GETCORNER", 1, 2, a => lisp.Ask(WithPoint(lisp, LispAskKind.Corner, a, baseRequired: true)));
            lisp.Define("GETDIST", 0, 2, a => lisp.Ask(WithPoint(lisp, LispAskKind.Distance, a, baseRequired: false)));
            lisp.Define("GETANGLE", 0, 2, a => lisp.Ask(WithPoint(lisp, LispAskKind.Angle, a, baseRequired: false)));
            lisp.Define("GETORIENT", 0, 2, a => lisp.Ask(WithPoint(lisp, LispAskKind.Orient, a, baseRequired: false)));
            lisp.Define("GETINT", 0, 1, a => lisp.Ask(WithMessage(lisp, LispAskKind.Integer, a)));
            lisp.Define("GETREAL", 0, 1, a => lisp.Ask(WithMessage(lisp, LispAskKind.Real, a)));
            lisp.Define("GETKWORD", 0, 1, a => lisp.Ask(WithMessage(lisp, LispAskKind.Keyword, a)));
            lisp.Define("ENTSEL", 0, 1, a => lisp.Ask(WithMessage(lisp, LispAskKind.Entity, a)));
            lisp.Define("NENTSEL", 0, 1, a =>
                NestedAnswer(lisp, session, lisp.Ask(WithMessage(lisp, LispAskKind.NestedEntity, a)))
            );
            lisp.Define("GETSTRING", 0, 2, a => lisp.Ask(WithText(lisp, a)));
        }

        /// <summary>
        /// What <c>nentsel</c> gives for an object picked: where it is a block reference with an object under the point inside it,
        /// <c>(ename pt matrix (insert ...))</c> of that object: its name, the point picked, the 4 by 4 matrix, row by row, that
        /// carries the block's coordinates into the world (the world images of the three axes in its columns, the origin in the
        /// fourth, <c>(0 0 0 1)</c> under them; AutoCAD 2021, GUI, measured, not the 4 by 3 form its documentation gives) and the
        /// names of the references it is in, the innermost first. Where the point is on
        /// the text of a visible attribute of the reference, the answer is <c>(ename pt)</c> of the ATTRIB itself, nothing after the
        /// point (AutoCAD 2021, GUI, measured), and an attribute wins over the objects of the block where both are under the point
        /// (ours: the attribute is drawn last). Anything else is as <c>entsel</c> gives it.
        /// </summary>
        private static object NestedAnswer(LispInterpreter lisp, CadCommandSession session, object answer)
        {
            if (!(answer is LispCons))
            {
                return answer;
            }

            return lisp.OnOwner(() => Nested(session, (LispCons)answer));
        }

        private static object Nested(CadCommandSession session, LispCons answer)
        {
            List<object> items = LispInterpreter.ItemsOf(answer);
            GeoPoint3? at = items.Count > 1 ? PointOf(items[1]) : null;

            if (!(items[0] is LispEntityName name) || !at.HasValue)
            {
                return answer;
            }

            DxfEntity picked = session.Document.FindEntity(name.Handle);

            if (picked is DxfDimension dimension)
            {
                return NestedInDimension(session, dimension, items[1], at.Value, answer);
            }

            if (!(picked is DxfBlockReference top))
            {
                return answer;
            }

            GeoPoint2 world = session.Ucs.ToWorld(new GeoPoint2(at.Value.X, at.Value.Y));
            DxfAttributeReference attribute = AttributePicked(session, top, world);

            if (attribute != null)
            {
                // AutoCAD 2021 (GUI, L5 X2): a pick on an attribute's text gives the ATTRIB and the point, no matrix and no references.
                return LispInterpreter.ListOf(new object[] { new LispEntityName(attribute.Handle), items[1] });
            }

            DxfEntity inner = CadDocument.FindNestedPick(top, world, session.PickAperture, true);
            var chain = new List<DxfBlockReference>();

            if (inner == null || ReferenceEquals(inner, top) || !PathTo(top, inner, chain, 0))
            {
                return answer;
            }

            GeoTransform3 placing = chain[0].BlockTransform;

            for (int i = 1; i < chain.Count; i++)
            {
                placing = placing.Multiply(chain[i].BlockTransform);
            }

            // AutoCAD 2021 (GUI, L5 X2): a 4 by 4 matrix row by row, the images of the three axes in its columns, the origin in the
            // fourth and (0 0 0 1) under them.
            GeoPoint3 origin = placing.Transform(GeoPoint3.Origin);
            GeoPoint3 xAxis = placing.Transform(new GeoPoint3(1, 0, 0));
            GeoPoint3 yAxis = placing.Transform(new GeoPoint3(0, 1, 0));
            GeoPoint3 zAxis = placing.Transform(new GeoPoint3(0, 0, 1));
            var matrix = new List<object>
            {
                LispInterpreter.ListOf(
                    new object[] { xAxis.X - origin.X, yAxis.X - origin.X, zAxis.X - origin.X, origin.X }
                ),
                LispInterpreter.ListOf(
                    new object[] { xAxis.Y - origin.Y, yAxis.Y - origin.Y, zAxis.Y - origin.Y, origin.Y }
                ),
                LispInterpreter.ListOf(
                    new object[] { xAxis.Z - origin.Z, yAxis.Z - origin.Z, zAxis.Z - origin.Z, origin.Z }
                ),
                LispInterpreter.ListOf(new object[] { 0.0, 0.0, 0.0, 1.0 }),
            };

            var inserts = new List<object>();

            for (int i = chain.Count - 1; i >= 0; i--)
            {
                inserts.Add(new LispEntityName(chain[i].Handle));
            }

            return LispInterpreter.ListOf(
                new object[]
                {
                    new LispEntityName(inner.Handle),
                    items[1],
                    LispInterpreter.ListOf(matrix),
                    LispInterpreter.ListOf(inserts),
                }
            );
        }

        /// <summary>
        /// What <c>nentsel</c> gives for a dimension picked: the primitive of the dimension's block under the point, as AutoCAD 2021
        /// does (feed e1 of the L5 acceptance: a DIMLINEAR picked on its line, near its text and on an extension line gave
        /// <c>(4 "LINE" ("DIMENSION"))</c>), <c>(ename pt matrix (dimension))</c> with the identity matrix, the block being drawn
        /// in the world's coordinates. The primitive is the one whose box is the smallest among those that hold the point, within
        /// the aperture, a pick on the text itself taking the text (not measured), as a block reference's pick does. A dimension
        /// with no primitive under the point is as <c>entsel</c> gives it.
        /// </summary>
        private static object NestedInDimension(
            CadCommandSession session,
            DxfDimension dimension,
            object point,
            GeoPoint3 at,
            object answer
        )
        {
            DxfBlockTableRecord block = dimension.DimBlock;

            if (block == null)
            {
                return answer;
            }

            GeoPoint2 world = session.Ucs.ToWorld(new GeoPoint2(at.X, at.Y));
            double aperture = session.PickAperture;
            DxfEntity best = null;
            double leastArea = double.MaxValue;
            double leastDistance = double.MaxValue;
            bool bestIsText = false;

            foreach (DxfEntity candidate in block)
            {
                // The points of the dimension (its defpoints) are not picked.
                if (candidate is DxfDBPoint)
                {
                    continue;
                }

                GeoAabb3 box;

                try
                {
                    box = candidate.GeometricExtents;
                }
                catch (Exception exception)
                    when (exception is ArgumentException
                        || exception is InvalidOperationException
                        || exception is NotSupportedException
                    )
                {
                    continue;
                }

                if (
                    box.IsEmpty
                    || world.X < box.Min.X - aperture
                    || world.X > box.Max.X + aperture
                    || world.Y < box.Min.Y - aperture
                    || world.Y > box.Max.Y + aperture
                )
                {
                    continue;
                }

                double area = (box.Max.X - box.Min.X) * (box.Max.Y - box.Min.Y);
                double dx = Math.Max(0.0, Math.Max(box.Min.X - world.X, world.X - box.Max.X));
                double dy = Math.Max(0.0, Math.Max(box.Min.Y - world.Y, world.Y - box.Max.Y));
                double distance = dx * dx + dy * dy;

                // The text of the dimension comes last (AutoCAD gave the LINE for a pick 0.9 under the text, which is ours to follow, a
                // pick on the text itself not being measured); then the smallest box wins, the nearer one where they are as small, the
                // first in the block where they are as near.
                bool isText = candidate is DxfMText || candidate is DxfDBText;

                if (
                    best == null
                    || (bestIsText && !isText)
                    || (bestIsText == isText && (area < leastArea || (area == leastArea && distance < leastDistance)))
                )
                {
                    leastArea = area;
                    leastDistance = distance;
                    bestIsText = isText;
                    best = candidate;
                }
            }

            if (best == null || string.IsNullOrEmpty(best.Handle))
            {
                return answer;
            }

            var identity = new List<object>
            {
                LispInterpreter.ListOf(new object[] { 1.0, 0.0, 0.0 }),
                LispInterpreter.ListOf(new object[] { 0.0, 1.0, 0.0 }),
                LispInterpreter.ListOf(new object[] { 0.0, 0.0, 1.0 }),
                LispInterpreter.ListOf(new object[] { 0.0, 0.0, 0.0 }),
            };
            return LispInterpreter.ListOf(
                new object[]
                {
                    new LispEntityName(best.Handle),
                    point,
                    LispInterpreter.ListOf(identity),
                    LispInterpreter.ListOf(new object[] { new LispEntityName(dimension.Handle) }),
                }
            );
        }

        /// <summary>The references from one to the object inside, outermost first; false where the object is in none of them.</summary>
        private static bool PathTo(DxfBlockReference current, DxfEntity inner, List<DxfBlockReference> chain, int depth)
        {
            chain.Add(current);
            DxfBlockTableRecord block = current.BlockTableRecord;

            if (block != null)
            {
                foreach (DxfEntity entity in block)
                {
                    if (
                        !entity.IsErased
                        && string.Equals(entity.Handle, inner.Handle, StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        return true;
                    }
                }

                if (depth < 8)
                {
                    foreach (DxfEntity entity in block)
                    {
                        if (
                            !entity.IsErased
                            && entity is DxfBlockReference reference
                            && PathTo(reference, inner, chain, depth + 1)
                        )
                        {
                            return true;
                        }
                    }
                }
            }

            chain.RemoveAt(chain.Count - 1);
            return false;
        }

        /// <summary>
        /// The attribute of a block reference whose text the point reaches, within the pick aperture, as the display list draws it
        /// (the pick index's own test, so that a pick that finds the reference by its attribute finds the attribute): of the pieces
        /// of text the reference is drawn with, the nearest to the point (the first drawn on a tie), taken for the attribute whose
        /// anchor is nearest to it (the first of the reference on a tie); null where no text of it is reached, as a constant
        /// attribute's, which has no ATTRIB. An erased attribute is out, and so is an invisible one under ATTMODE 1 and every one
        /// under ATTMODE 0, as the display leaves them out (AutoCAD 2021, GUI: an invisible attribute is not picked).
        /// </summary>
        private static DxfAttributeReference AttributePicked(
            CadCommandSession session,
            DxfBlockReference reference,
            GeoPoint2 at
        )
        {
            int mode = session.Document.Database.Attmode;

            if (mode == 0 || reference.Handle == null)
            {
                return null;
            }

            PickIndex index = session.Document.GetPickIndex(session.Document.WorkingSpace);
            DisplayText2 reached = null;
            double nearest = double.MaxValue;

            foreach (int place in index.IndicesOf(reference.Handle))
            {
                if (index.List.Items[place] is DisplayText2 text)
                {
                    double distance = text.DistanceTo(at.X, at.Y);

                    if (distance <= session.PickAperture && distance < nearest)
                    {
                        nearest = distance;
                        reached = text;
                    }
                }
            }

            if (reached == null)
            {
                return null;
            }

            DxfAttributeReference best = null;
            double least = double.MaxValue;

            foreach (DxfAttributeReference attribute in reference.AttributeCollection)
            {
                if (attribute.IsErased || attribute.Handle == null || (mode == 1 && attribute.Invisible))
                {
                    continue;
                }

                GeoPoint3 anchor = attribute.ReadForDisplay().At;
                double gap = Math.Abs(anchor.X - reached.X) + Math.Abs(anchor.Y - reached.Y);

                if (gap < least)
                {
                    least = gap;
                    best = attribute;
                }
            }

            return best;
        }

        /// <summary>
        /// <c>(initget [bits] [keywords])</c>, the two in either order: the bits and the keywords that hold for the next get
        /// function, <c>entsel</c>, <c>nentsel</c> or interactive <c>ssget</c> only. Keywords are separated by spaces or
        /// commas.
        /// </summary>
        private static object Initget(LispInterpreter lisp, object[] a)
        {
            int bits = 0;
            string keywords = null;

            if (a[0] is string first)
            {
                keywords = first;

                if (a.Length > 1)
                {
                    bits = LispInterpreter.Int(a[1]);
                }
            }
            else
            {
                bits = LispInterpreter.Int(a[0]);

                if (a.Length > 1 && a[1] != null)
                {
                    keywords = LispInterpreter.Str(a[1]);
                }
            }

            lisp.SetInitget(
                bits,
                keywords == null
                    ? Array.Empty<string>()
                    : keywords.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries)
            );
            return null;
        }

        /// <summary><c>(getpoint [pt] [msg])</c> and its kin: a point to measure from, then the message.</summary>
        private static LispAsk WithPoint(LispInterpreter lisp, LispAskKind kind, object[] a, bool baseRequired)
        {
            GeoPoint3? basePoint = null;
            string message = null;

            if (a.Length == 1 && a[0] is string text && !baseRequired)
            {
                message = text;
            }
            else if (a.Length > 0)
            {
                // A message in front of a second argument that is a point is the message of that base point, as AutoCAD 2021 takes
                // (getpoint "\nNext: " pt) (the routines' feeds); in front of anything else the second is the point that was wrong.
                if (a[0] is string first && a.Length > 1)
                {
                    GeoPoint3? given = PointOf(a[1]);

                    if (given == null)
                    {
                        throw LispInterpreter.TypeError("point", a[1], colon: true);
                    }

                    LispAsk swapped = lisp.NewAsk(kind, first);
                    swapped.BasePoint = given;
                    return swapped;
                }

                basePoint = PointOf(a[0]);

                if (basePoint == null && (baseRequired || a[0] != null))
                {
                    throw LispInterpreter.TypeError("point", a[0], colon: true);
                }

                if (a.Length > 1)
                {
                    message = LispInterpreter.Str(a[1]);
                }
            }

            LispAsk ask = lisp.NewAsk(kind, message);
            ask.BasePoint = basePoint;
            return ask;
        }

        /// <summary><c>(getint [msg])</c> and its kin.</summary>
        private static LispAsk WithMessage(LispInterpreter lisp, LispAskKind kind, object[] a) =>
            lisp.NewAsk(kind, a.Length > 0 ? LispInterpreter.Str(a[0]) : null);

        /// <summary><c>(getstring [cr] [msg])</c>: with a first argument that is no string, true or nil says whether a space is part of the text.</summary>
        private static LispAsk WithText(LispInterpreter lisp, object[] a)
        {
            if (a.Length == 1 && a[0] is string only)
            {
                return lisp.NewAsk(LispAskKind.String, only);
            }

            LispAsk ask = lisp.NewAsk(LispAskKind.String, a.Length > 1 ? LispInterpreter.Str(a[1]) : null);
            ask.Spaces = a.Length > 0 && a[0] != null;
            return ask;
        }

        /// <summary>A list of two or three numbers as a point, the third coordinate 0 for two; null for anything else.</summary>
        internal static GeoPoint3? PointOf(object value)
        {
            var coordinates = new List<double>(3);
            object cell = value;

            while (cell is LispCons item)
            {
                if (coordinates.Count == 3)
                {
                    return null;
                }

                if (item.Car is int whole)
                {
                    coordinates.Add(whole);
                }
                else if (item.Car is double real)
                {
                    coordinates.Add(real);
                }
                else
                {
                    return null;
                }

                cell = item.Cdr;
            }

            if (cell != null || coordinates.Count < 2)
            {
                return null;
            }

            return new GeoPoint3(coordinates[0], coordinates[1], coordinates.Count == 3 ? coordinates[2] : 0.0);
        }
    }
}
