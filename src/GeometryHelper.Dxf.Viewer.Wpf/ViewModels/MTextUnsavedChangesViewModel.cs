namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// What Multiline Text - Unsaved Changes asks on Esc in the MTEXT editor with changes, as AutoCAD 2021 asks it: "Do you
    /// want to save your text changes?", Yes, No or Cancel, and whether to always perform the choice made.
    /// </summary>
    public sealed class MTextUnsavedChangesViewModel
    {
        /// <summary>
        /// Gets or sets whether Always perform my current choice is ticked: the Yes or No given with it is kept in Global
        /// settings (<see cref="GlobalSettings.MTextUnsavedChanges"/>) and the question is not asked again; Cancel is not kept.
        /// </summary>
        public bool AlwaysPerform { get; set; }
    }
}
