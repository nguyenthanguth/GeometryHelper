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
        /// <c>(ename pt matrix (insert ...))</c> of that object (reference, as AutoCAD 2021's documentation words it, not measured):
        /// its name, the point picked, the 4 by 3 matrix that carries the block's coordinates into the world (the world images
        /// of the three axes, then the origin) and the names of the references it is in, the innermost first. Anything else is as
        /// <c>entsel</c> gives it.
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

            if (!(session.Document.FindEntity(name.Handle) is DxfBlockReference top))
            {
                return answer;
            }

            GeoPoint2 world = session.Ucs.ToWorld(new GeoPoint2(at.Value.X, at.Value.Y));
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

            GeoPoint3 origin = placing.Transform(GeoPoint3.Origin);
            var matrix = new List<object>();

            foreach (GeoPoint3 axis in new[] { new GeoPoint3(1, 0, 0), new GeoPoint3(0, 1, 0), new GeoPoint3(0, 0, 1) })
            {
                GeoPoint3 moved = placing.Transform(axis);
                matrix.Add(
                    LispInterpreter.ListOf(new object[] { moved.X - origin.X, moved.Y - origin.Y, moved.Z - origin.Z })
                );
            }

            matrix.Add(LispInterpreter.ListOf(new object[] { origin.X, origin.Y, origin.Z }));

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
