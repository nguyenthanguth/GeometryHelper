using System;
using System.Collections.Generic;
using System.Globalization;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>A named view of a drawing as the View Manager lists it.</summary>
    public sealed class CadViewRow
    {
        internal CadViewRow(
            string name,
            bool paper,
            bool snapshot,
            double centerX,
            double centerY,
            double height,
            double width,
            double twist
        )
        {
            Name = name;
            IsPaperspace = paper;
            HasLayerSnapshot = snapshot;
            CenterX = centerX;
            CenterY = centerY;
            Height = height;
            Width = width;
            Twist = twist;
        }

        /// <summary>Gets the name, as Stair core.</summary>
        public string Name { get; }

        /// <summary>Gets whether it is a layout view, of paper space; else a model view.</summary>
        public bool IsPaperspace { get; }

        /// <summary>Gets whether it keeps a layer snapshot, restored with it.</summary>
        public bool HasLayerSnapshot { get; }

        /// <summary>Gets the X of its centre, in the units of its space.</summary>
        public double CenterX { get; }

        /// <summary>Gets the Y of its centre.</summary>
        public double CenterY { get; }

        /// <summary>Gets its height.</summary>
        public double Height { get; }

        /// <summary>Gets its width.</summary>
        public double Width { get; }

        /// <summary>Gets its twist, in degrees.</summary>
        public double Twist { get; }
    }

    /// <summary>
    /// The View Manager of VIEW and its New View dialog, as data their command and the window share: the named views, the one
    /// chosen, the view on screen, and what New View is given. Edit Boundaries… and New View's Define window close the dialog
    /// with <see cref="CadDialogAction.SelectObjects"/> and their words in <see cref="Pick"/>: the command asks two corners on
    /// the command line, then shows the dialog again. New, Delete, Update Layers and the properties change the drawing at
    /// once; Set Current restores the view chosen on Apply or OK; Cancel takes every change back, the whole one undo step.
    /// </summary>
    public sealed class CadViewManagerRequest
    {
        internal CadViewManagerRequest(CadDocument document)
        {
            Document = document ?? throw new ArgumentNullException(nameof(document));
        }

        /// <summary>Gets the document.</summary>
        public CadDocument Document { get; }

        /// <summary>Gets the named views, model views then layout views, each in ordinal order of their names, letters of either case alike.</summary>
        public IReadOnlyList<CadViewRow> Views { get; internal set; } = Array.Empty<CadViewRow>();

        /// <summary>Gets or sets the name of the view chosen; null for Current, the view on screen.</summary>
        public string Selected { get; set; }

        /// <summary>Gets the view on screen, as a row named Current; null where the window shows none.</summary>
        public CadViewRow CurrentView { get; internal set; }

        /// <summary>Gets or sets the view Set Current chose, restored on Apply or OK; null for none.</summary>
        public string ToRestore { get; set; }

        /// <summary>Gets or sets the name New View gives.</summary>
        public string NewName { get; set; } = string.Empty;

        /// <summary>Gets or sets whether New View saves a layer snapshot with the view; true until set.</summary>
        public bool NewLayerSnapshot { get; set; } = true;

        /// <summary>Gets or sets the window New View's Define window took; null for Current display.</summary>
        public DisplayBox? NewWindow { get; set; }

        /// <summary>Gets or sets whether New View is open, so that it is shown again after its Define window.</summary>
        public bool IsNewViewOpen { get; set; }

        /// <summary>Gets or sets the button the dialog was closed with for the command line: Boundaries or NewWindow.</summary>
        public string Pick { get; set; }

        /// <summary>Gets what the last change said where it was refused; null where it was made.</summary>
        public string Message { get; internal set; }

        /// <summary>Gets or sets what the dialog was closed with: OK, Cancel or Select objects; Cancel until set.</summary>
        public CadDialogAction Action { get; set; }

        /// <summary>What the command does on a change made with the dialog open: the change's name and its texts.</summary>
        internal Func<string, string[], string> Changing { get; set; }

        /// <summary>New View's OK: the view saved under <see cref="NewName"/>, of <see cref="NewWindow"/> or the display, with its layer snapshot where asked.</summary>
        /// <param name="redefine">Whether a view of the name is redefined; false refuses the name.</param>
        /// <returns>Whether it was saved; <see cref="Message"/> says why not.</returns>
        public bool New(bool redefine) => Change("New", redefine ? "1" : "0");

        /// <summary>Delete: the view chosen and its layer snapshot taken out.</summary>
        /// <returns>Whether it was.</returns>
        public bool Delete() => Change("Delete");

        /// <summary>Update Layers: the layers as they stand saved as the view chosen's layer snapshot.</summary>
        /// <returns>Whether they were.</returns>
        public bool UpdateLayers() => Change("Layers");

        /// <summary>The view chosen given a new name.</summary>
        /// <param name="name">The name.</param>
        /// <returns>Whether it was renamed.</returns>
        public bool Rename(string name) => Change("Rename", name);

        /// <summary>The view chosen given a height and a width about its centre.</summary>
        /// <param name="height">The height.</param>
        /// <param name="width">The width.</param>
        /// <returns>Whether it was.</returns>
        public bool Resize(double height, double width) =>
            Change(
                "Size",
                height.ToString("R", CultureInfo.InvariantCulture),
                width.ToString("R", CultureInfo.InvariantCulture)
            );

        /// <summary>Apply: the view Set Current chose restored now, the dialog left open.</summary>
        /// <returns>Whether one was restored.</returns>
        public bool Apply() => Change("Apply");

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
