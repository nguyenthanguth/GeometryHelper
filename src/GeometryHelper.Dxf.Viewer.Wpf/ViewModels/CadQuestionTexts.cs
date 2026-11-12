namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The words of the questions the presenter asks in a <see cref="CadQuestionWindow"/>, each as AutoCAD 2021 words its own:
    /// the title of its task dialog, its main instruction, its detail and the labels of its buttons, or, for a message box of
    /// AutoCAD's, its text. The windows take their words from here, so that a test can hold each to what AutoCAD says.
    /// </summary>
    public static class CadQuestionTexts
    {
        /// <summary>The label of the button that gives a question up, as every task dialog of AutoCAD's has it.</summary>
        public const string Cancel = "Cancel";

        /// <summary>The title of Purge's question over an item, AutoCAD's "Purge - Confirm Purge".</summary>
        public const string PurgeTitle = "Purge - Confirm Purge";

        /// <summary>The label of the button that purges the item asked about.</summary>
        public const string PurgeThisItem = "Purge this item";

        /// <summary>The label of the button that leaves the item asked about and goes on.</summary>
        public const string SkipThisItem = "Skip this item";

        /// <summary>The title of the question over a layer state of a name used, AutoCAD's "Layer - Overwrite Layer State".</summary>
        public const string OverwriteLayerStateTitle = "Layer - Overwrite Layer State";

        /// <summary>The title of the Block Attribute Manager's question over changes not saved, the caption of AutoCAD's message.</summary>
        public const string AttributeChangesTitle = "Block Attribute Manager Alert";

        /// <summary>The first line of the Block Attribute Manager's question over changes not saved.</summary>
        public const string AttributeChangesLine = "The attributes in the current block have been changed.";

        /// <summary>The second line of the Block Attribute Manager's question over changes not saved.</summary>
        public const string AttributeChangesQuestion = "Do you want to save these changes?";

        /// <summary>The title of the question over a block of a name used, AutoCAD's "Block - Redefine Block".</summary>
        public const string RedefineBlockTitle = "Block - Redefine Block";

        /// <summary>The label of the button that redefines the block.</summary>
        public const string RedefineBlock = "Redefine block";

        /// <summary>What redefining the block does, the line under its button.</summary>
        public const string RedefineBlockDetail = "All instances of this block will be updated.";

        /// <summary>What not redefining the block does, the line under its button.</summary>
        public const string KeepBlockDetail = "No changes will be made to the block or drawing.";

        /// <summary>The title of the question over a view of a name used, AutoCAD's "%ObjectName% - Already Exists" for a view.</summary>
        public const string ViewExistsTitle = "View - Already Exists";

        /// <summary>The label of the button that replaces the view.</summary>
        public const string ReplaceView = "Replace";

        /// <summary>The title of the message over a layer that cannot be renamed, AutoCAD's "Layer - Rename".</summary>
        public const string RenameRefusedTitle = "Layer - Rename";

        /// <summary>The message over a layer that cannot be renamed.</summary>
        public const string RenameRefused = "The selected layer cannot be renamed.";

        /// <summary>The detail of the message over a layer that cannot be renamed: the layers AutoCAD cannot rename.</summary>
        public const string RenameRefusedDetail =
            "The following layers cannot be renamed:\n Layer 0\n Xref-dependent layers";

        /// <summary>The question when the current text style has been changed and the dialog moves on, AutoCAD's message.</summary>
        public const string TextStyleModified =
            "The current style has been modified.\nDo you want to save your changes?";

        /// <summary>The question Purge asks over an item: "You are about to purge layer TMP. What do you want to do?".</summary>
        /// <param name="kind">The kind of the item, as layer.</param>
        /// <param name="name">The name of the item.</param>
        /// <returns>The question.</returns>
        public static string PurgeQuestion(string kind, string name) =>
            "You are about to purge " + kind + " " + name + ". What do you want to do?";

        /// <summary>The question over a layer state of a name used: "Do you want to overwrite Plan with the drawing's current layer state?".</summary>
        /// <param name="name">The name of the state.</param>
        /// <returns>The question.</returns>
        public static string OverwriteLayerState(string name) =>
            "Do you want to overwrite " + name + " with the drawing's current layer state?";

        /// <summary>The question over a block of a name used: "GB" is already defined as a block in this drawing, ...</summary>
        /// <param name="name">The name of the block.</param>
        /// <returns>The question.</returns>
        public static string RedefineBlockQuestion(string name) =>
            "\""
            + name
            + "\" is already defined as a block in this drawing, would you like to redefine this block reference?";

        /// <summary>The label of the button that leaves the block as it is: Don't redefine "GB".</summary>
        /// <param name="name">The name of the block.</param>
        /// <returns>The label.</returns>
        public static string KeepBlock(string name) => "Don't redefine \"" + name + "\"";

        /// <summary>The question over a view of a name used: "Stair core already exists.  Do you want to replace it?".</summary>
        /// <param name="name">The name of the view.</param>
        /// <returns>The question.</returns>
        public static string ViewExists(string name) => name + " already exists.  Do you want to replace it?";

        /// <summary>The question over a file Write Block would write over: "C:\plan.dwg already exists.  Do you want to replace it?".</summary>
        /// <param name="file">The full name of the file.</param>
        /// <returns>The question.</returns>
        public static string FileExists(string file) => file + " already exists.  Do you want to replace it?";
    }
}
