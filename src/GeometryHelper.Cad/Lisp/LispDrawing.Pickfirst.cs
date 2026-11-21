using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Geometry;

namespace GeometryHelper.Cad.Lisp
{
    /// <summary>
    /// <c>sssetfirst</c>, <c>ssgetfirst</c> and <c>nentselp</c>. The pick-first selection is the selection of the document, the
    /// one a click or a window makes and a command that works on a selection takes instead of asking "Select objects:" (unless
    /// PICKFIRST is 0). <c>nentselp</c> is <c>nentsel</c> with the point given by the routine, so that nothing is asked.
    /// </summary>
    internal static partial class LispDrawing
    {
        static partial void RegisterPickfirst(LispInterpreter lisp, CadCommandSession session)
        {
            lisp.Define("SSSETFIRST", 1, 2, a => SsSetFirst(lisp, session, a));
            lisp.Define("SSGETFIRST", 0, 0, a => SsGetFirst(lisp, session));
            lisp.Define("NENTSELP", 0, 2, a => NentselP(lisp, session, a));
        }

        /// <summary>
        /// <c>(sssetfirst gripset [pickset])</c>: the objects of the pick set, the live ones, become the selection, and nothing is
        /// selected for nil or no pick set. The grip set is only checked to be a set or nil, as AutoCAD 2021 takes it (r9). It
        /// gives <c>(nil pickset)</c> with the pick set as it was given, <c>(nil nil)</c> for nil, and <c>(nil)</c> for one
        /// argument.
        /// </summary>
        private static object SsSetFirst(LispInterpreter lisp, CadCommandSession session, object[] a)
        {
            if (a[0] != null)
            {
                SetOf(a[0]);
            }

            LispSelectionSet pick = a.Length > 1 && a[1] != null ? SetOf(a[1]) : null;

            lisp.OnOwner(() =>
            {
                var live = new List<string>();

                if (pick != null)
                {
                    foreach (string handle in pick.Handles)
                    {
                        if (session.Document.FindEntity(handle) != null)
                        {
                            live.Add(handle);
                        }
                    }
                }

                session.Document.Selection.Set(live);
                return null;
            });

            return a.Length > 1
                ? LispInterpreter.ListOf(new object[] { null, pick })
                : LispInterpreter.ListOf(new object[] { null });
        }

        /// <summary>
        /// <c>(ssgetfirst)</c>: <c>(nil set)</c> with a new set of the live objects selected, in the order of their handle numbers
        /// (the order of <c>(ssget "I")</c>), or <c>(nil nil)</c> where none is selected. Each call makes a new set.
        /// </summary>
        private static object SsGetFirst(LispInterpreter lisp, CadCommandSession session) =>
            lisp.OnOwner(() =>
            {
                var numbered = new List<KeyValuePair<ulong, string>>();

                foreach (string handle in session.Document.Selection.Handles)
                {
                    if (session.Document.FindEntity(handle) == null)
                    {
                        continue;
                    }

                    ulong.TryParse(handle, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong value);
                    numbered.Add(new KeyValuePair<ulong, string>(value, handle));
                }

                numbered.Sort(
                    (x, y) =>
                    {
                        int order = x.Key.CompareTo(y.Key);
                        return order != 0 ? order : string.CompareOrdinal(x.Value, y.Value);
                    }
                );

                if (numbered.Count == 0)
                {
                    return LispInterpreter.ListOf(new object[] { null, null });
                }

                var handles = new List<string>(numbered.Count);

                foreach (KeyValuePair<ulong, string> pair in numbered)
                {
                    handles.Add(pair.Value);
                }

                return LispInterpreter.ListOf(new object[] { null, new LispSelectionSet(handles) });
            });

        /// <summary>
        /// <c>(nentselp [msg] [pt])</c>, the two in either order: with a point, the object picked at it, as <c>nentsel</c> gives
        /// it and with nothing asked, where the point is as given (r9: <c>(ename (5.0 0.0 0.0))</c>) and nil where it is on no
        /// object; with no point it asks as <c>nentsel</c> does.
        /// </summary>
        private static object NentselP(LispInterpreter lisp, CadCommandSession session, object[] a)
        {
            GeoPoint3? at = null;
            string message = null;

            foreach (object argument in a)
            {
                if (argument == null)
                {
                    continue;
                }

                if (argument is string text && message == null)
                {
                    message = text;
                }
                else if (!at.HasValue && PointOf(argument) is GeoPoint3 point)
                {
                    at = point;
                }
                else
                {
                    throw LispInterpreter.TypeError("point", argument, colon: true);
                }
            }

            if (!at.HasValue)
            {
                return NestedAnswer(lisp, session, lisp.Ask(lisp.NewAsk(LispAskKind.NestedEntity, message)));
            }

            GeoPoint3 given = at.Value;

            object answer = lisp.OnOwner(() =>
            {
                GeoPoint2 world = session.Ucs.ToWorld(new GeoPoint2(given.X, given.Y));
                IReadOnlyList<string> picked = session
                    .Document.GetPickIndex(session.Document.WorkingSpace)
                    .Pick(world.X, world.Y, session.PickAperture);

                if (picked.Count == 0 || session.Document.FindEntity(picked[0]) == null)
                {
                    return null;
                }

                return LispInterpreter.ListOf(
                    new object[]
                    {
                        new LispEntityName(picked[0]),
                        LispInterpreter.ListOf(new object[] { given.X, given.Y, given.Z }),
                    }
                );
            });

            return NestedAnswer(lisp, session, answer);
        }
    }
}
