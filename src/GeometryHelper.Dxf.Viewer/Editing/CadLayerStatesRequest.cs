using System;
using System.Collections.Generic;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The Layer States Manager of LAYERSTATE, as data its command and the window share: the states of the drawing, the one
    /// chosen, the restore options and the properties it restores. New, Update, Rename, Delete, Import, Export and the
    /// properties ticked change the drawing while the dialog stays open, each as an edit of the command's one undo step;
    /// Restore restores the state chosen and closes it, Close keeps every change.
    /// </summary>
    public sealed class CadLayerStatesRequest
    {
        internal CadLayerStatesRequest(CadDocument document)
        {
            Document = document ?? throw new ArgumentNullException(nameof(document));
        }

        /// <summary>Gets the document.</summary>
        public CadDocument Document { get; }

        /// <summary>Gets the states listed, in ordinal order of their names, letters of either case alike; a named view's snapshot is not among them.</summary>
        public IReadOnlyList<CadLayerStateRow> States { get; internal set; } = Array.Empty<CadLayerStateRow>();

        /// <summary>Gets or sets the name of the state chosen; null for none.</summary>
        public string Selected { get; set; }

        /// <summary>Gets or sets whether the states of xrefs are left out of the list (Don't list layer states in Xrefs); true until set.</summary>
        public bool HideXrefStates { get; set; } = true;

        /// <summary>Gets or sets whether Restore turns off the layers the state does not name (Turn off layers not found in layer state).</summary>
        public bool TurnOffMissing { get; set; }

        /// <summary>Gets or sets whether Restore puts the properties on the current viewport as overrides; only in a layout.</summary>
        public bool AsViewportOverrides { get; set; }

        /// <summary>Gets whether the drawing shows model space, where Apply properties as viewport overrides is greyed.</summary>
        public bool IsModelSpace => Document.CurrentSpace == null || Document.CurrentSpace.IsModel;

        /// <summary>Gets the name of the state the drawing is as, or *UNSAVED* where no state is the same as the drawing.</summary>
        public string Current { get; internal set; } = "*UNSAVED*";

        /// <summary>Gets what the last change said where it was refused, as "A layer state named Plan already exists."; null where it was made.</summary>
        public string Message { get; internal set; }

        /// <summary>Gets or sets what the dialog was closed with: OK for Restore, Cancel for Close; Cancel until set.</summary>
        public CadDialogAction Action { get; set; }

        /// <summary>What the command does on a change made with the dialog open: the change's name and its texts.</summary>
        internal Func<string, string[], string> Changing { get; set; }

        /// <summary>New: the layers as they stand saved as a new state.</summary>
        /// <param name="name">The name.</param>
        /// <param name="description">The description.</param>
        /// <returns>Whether it was saved; <see cref="Message"/> says why not.</returns>
        public bool New(string name, string description) => Change("New", name, description);

        /// <summary>Update: the state chosen saved again from the layers as they stand, its properties kept.</summary>
        /// <returns>Whether it was.</returns>
        public bool Update() => Change("Update");

        /// <summary>Rename: the state chosen given a new name.</summary>
        /// <param name="name">The new name.</param>
        /// <returns>Whether it was renamed.</returns>
        public bool Rename(string name) => Change("Rename", name);

        /// <summary>Delete: the state chosen taken out.</summary>
        /// <returns>Whether it was.</returns>
        public bool Delete() => Change("Delete");

        /// <summary>The properties the state chosen restores, as AutoCAD's mask (1 on, 2 frozen, 4 locked, 8 plot, 0x20 colour and on).</summary>
        /// <param name="mask">The mask.</param>
        /// <returns>Whether it was written.</returns>
        public bool SetMask(int mask) => Change("Mask", mask.ToString(System.Globalization.CultureInfo.InvariantCulture));

        /// <summary>Import: the states of a .las file read into the drawing.</summary>
        /// <param name="path">The file.</param>
        /// <returns>Whether it was read.</returns>
        public bool Import(string path) => Change("Import", path);

        /// <summary>Export: the state chosen written to a .las file.</summary>
        /// <param name="path">The file.</param>
        /// <returns>Whether it was written.</returns>
        public bool Export(string path) => Change("Export", path);

        /// <summary>The states read again from the drawing, as <see cref="HideXrefStates"/> switched lists them.</summary>
        public void Relist() => Change("List");

        private bool Change(string what, params string[] texts)
        {
            if (Changing == null)
            {
                return false;
            }

            Message = Changing(what, texts);
            return Message == null;
        }
    }
}
