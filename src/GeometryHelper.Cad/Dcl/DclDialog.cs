namespace GeometryHelper.Cad.Dcl
{
    /// <summary>
    /// A dialog that <c>new_dialog</c> made: its tiles (a copy of the definition, holding the values), where it is shown, its
    /// focus and how it ended. A host shows it and reports what the user does; the AutoLISP functions change it.
    /// </summary>
    internal sealed class DclDialog
    {
        internal DclDialog(DclTile root, int id, string name, DclDialog parent)
        {
            Root = root;
            Id = id;
            Name = name;
            Parent = parent;
        }

        /// <summary>Gets the dialog's top tile.</summary>
        internal DclTile Root { get; }

        /// <summary>Gets the id <c>load_dialog</c> gave the file the dialog came from.</summary>
        internal int Id { get; }

        /// <summary>Gets the dialog's name in its file.</summary>
        internal string Name { get; }

        /// <summary>Gets the dialog this one was opened over; null for none.</summary>
        internal DclDialog Parent { get; }

        /// <summary>Gets or sets the X of the screen point <c>new_dialog</c> gave, in pixels; null to centre the dialog.</summary>
        internal int? ScreenX { get; set; }

        /// <summary>Gets or sets the Y of the screen point <c>new_dialog</c> gave, in pixels; null to centre the dialog.</summary>
        internal int? ScreenY { get; set; }

        /// <summary>Gets or sets the left of the dialog where the host shows it, in screen pixels; <c>done_dialog</c> gives it.</summary>
        internal int PositionX { get; set; }

        /// <summary>Gets or sets the top of the dialog where the host shows it, in screen pixels; <c>done_dialog</c> gives it.</summary>
        internal int PositionY { get; set; }

        /// <summary>Gets or sets the key of the tile with the keyboard focus: <c>initial_focus</c>, then <c>mode_tile</c> 2.</summary>
        internal string FocusKey { get; set; }

        /// <summary>Gets or sets the key of the edit box whose text <c>mode_tile</c> 3 selected; null for none.</summary>
        internal string SelectTextKey { get; set; }

        /// <summary>Gets or sets whether the dialog has ended.</summary>
        internal bool IsDone { get; set; }

        /// <summary>Gets or sets the status it ended with, the value of <c>start_dialog</c>.</summary>
        internal int Status { get; set; }

        /// <summary>Finds the first tile with the key; null for none.</summary>
        internal DclTile Find(string key) => Root.Find(key);
    }
}
