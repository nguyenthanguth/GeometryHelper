namespace GeometryHelper.Cad.Dcl
{
    /// <summary>What the user did in a dialog.</summary>
    internal enum DclEventKind
    {
        /// <summary>A tile was used: clicked, typed in, chosen in.</summary>
        Tile,

        /// <summary>Enter was pressed.</summary>
        Enter,

        /// <summary>Esc was pressed, or the window's close box clicked.</summary>
        Escape,
    }

    /// <summary>One act of the user in a dialog, as a host reports it to <c>start_dialog</c>.</summary>
    internal sealed class DclEvent
    {
        private DclEvent(DclEventKind kind, string key, string value, int reason, int x, int y)
        {
            Kind = kind;
            Key = key;
            Value = value;
            Reason = reason;
            X = x;
            Y = y;
        }

        /// <summary>Gets what was done.</summary>
        internal DclEventKind Kind { get; }

        /// <summary>Gets the key of the tile used; for Enter the edit box or list box with the focus, or null.</summary>
        internal string Key { get; }

        /// <summary>Gets the tile's value after the act, as <c>get_tile</c> gives it; null for Enter and Esc.</summary>
        internal string Value { get; }

        /// <summary>Gets the reason: 1 picked, 2 an edit box left, 3 a slider dragged, 4 a double click.</summary>
        internal int Reason { get; }

        /// <summary>Gets the X of a click in an image button, in its pixels; 0 otherwise.</summary>
        internal int X { get; }

        /// <summary>Gets the Y of a click in an image button, in its pixels; 0 otherwise.</summary>
        internal int Y { get; }

        /// <summary>A tile used.</summary>
        internal static DclEvent Tile(string key, string value, int reason, int x = 0, int y = 0) =>
            new DclEvent(DclEventKind.Tile, key, value, reason, x, y);

        /// <summary>Enter, with the focus in the edit box or list box <paramref name="key"/>, or elsewhere.</summary>
        internal static DclEvent Enter(string key = null) => new DclEvent(DclEventKind.Enter, key, null, 1, 0, 0);

        /// <summary>Esc, or the close box.</summary>
        internal static DclEvent Escape() => new DclEvent(DclEventKind.Escape, null, null, 1, 0, 0);
    }

    /// <summary>What shows dialogs to the user: the window, or a test's script. Every call comes on the owner thread, one at a time.</summary>
    internal interface IDclHost
    {
        /// <summary>Shows a dialog, a child over its <see cref="DclDialog.Parent"/>, without waiting.</summary>
        /// <param name="dialog">The dialog.</param>
        void Open(DclDialog dialog);

        /// <summary>Shows every tile as the dialog now holds it, waits for the user's next act and gives it.</summary>
        /// <param name="dialog">The dialog.</param>
        /// <returns>What the user did.</returns>
        DclEvent Next(DclDialog dialog);

        /// <summary>Takes a dialog away: it ended, was terminated, or an error ended it.</summary>
        /// <param name="dialog">The dialog.</param>
        void Close(DclDialog dialog);
    }
}
