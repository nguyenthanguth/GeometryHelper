namespace GeometryHelper.Cad.Lisp
{
    /// <summary>
    /// <c>grread</c>: waits for one key, one click or one move of the cursor and gives it as a list, the way the loops that
    /// draw with <c>grdraw</c> and <c>grvecs</c> read the mouse. The wait is a prompt of the session that shows no words
    /// (<see cref="CadPrompt.ReadsInput"/>); the window answers it through <see cref="CadCommandSession.FeedKey"/>,
    /// <see cref="CadCommandSession.FeedMove"/> and <see cref="CadCommandSession.FeedRightClick"/>, a click through
    /// <see cref="CadCommandSession.Feed(GeometryHelper.Geometry.GeoPoint2)"/>, and each answer runs the loop once.
    /// </summary>
    internal static partial class LispDrawing
    {
        static partial void RegisterGrRead(LispInterpreter lisp, CadCommandSession session)
        {
            lisp.Define("GRREAD", 0, 3, a => GrRead(lisp, a));
        }

        /// <summary>
        /// <c>(grread [track [allkeys [curtype]]])</c>: <c>(2 code)</c> for a key (13 Enter, 32 Space, 8 Backspace, 9 Tab, the
        /// character's code for a printable one), <c>(3 (x y z))</c> for a click, <c>(5 (x y z))</c> for a move, and
        /// <c>(25 x)</c> for a right click with the shortcut menu on, <c>(11 0)</c> then <c>(12 (x y z))</c> with SHORTCUTMENU 0. Moves are
        /// given only where <c>track</c> is not nil or <c>allkeys</c> has the bit 1.
        /// <c>allkeys</c> and <c>curtype</c> must be whole numbers; their other bits are not used: the function keys keep their
        /// meaning in the window, except F3, which gives 6, and the crosshair stays. Esc gives <c>Function cancelled</c>.
        /// </summary>
        private static object GrRead(LispInterpreter lisp, object[] a)
        {
            int allKeys = a.Length > 1 ? LispInterpreter.Int(a[1]) : 0;

            if (a.Length > 2)
            {
                LispInterpreter.Int(a[2]);
            }

            return lisp.Ask(
                new LispAsk { Kind = LispAskKind.Input, Moves = (a.Length > 0 && a[0] != null) || (allKeys & 1) != 0 }
            );
        }
    }
}
