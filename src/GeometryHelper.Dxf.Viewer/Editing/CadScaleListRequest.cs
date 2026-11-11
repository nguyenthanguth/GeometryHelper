using System;
using System.Collections.Generic;
using System.Globalization;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// A scale of AutoCAD's Edit Drawing Scales: its name, its paper and drawing units, and whether the drawing keeps it (the
    /// unit scale, the current annotation scale, one an object points at) or an object points at it, which greys Delete and,
    /// for the latter, the units of Edit Scale.
    /// </summary>
    public sealed class CadScaleListRow
    {
        /// <summary>Makes a scale of the list, as Add Scale makes one: 1:25 of one paper unit to 25 drawing units.</summary>
        /// <param name="name">The name, as 1:25.</param>
        /// <param name="paperUnits">The paper units, above nought.</param>
        /// <param name="drawingUnits">The drawing units, above nought.</param>
        public CadScaleListRow(string name, double paperUnits, double drawingUnits)
        {
            Name = name ?? string.Empty;
            PaperUnits = paperUnits;
            DrawingUnits = drawingUnits;
        }

        internal CadScaleListRow(
            string name,
            double paperUnits,
            double drawingUnits,
            string original,
            bool isKept,
            bool isInUse
        )
            : this(name, paperUnits, drawingUnits)
        {
            Original = original;
            IsKept = isKept;
            IsInUse = isInUse;
        }

        /// <summary>Gets the name, as 1:50.</summary>
        public string Name { get; }

        /// <summary>Gets the paper units, as 1.</summary>
        public double PaperUnits { get; }

        /// <summary>Gets the drawing units, as 50.</summary>
        public double DrawingUnits { get; }

        /// <summary>Gets the scale they give: the paper units over the drawing units, 0.02 for 1:50.</summary>
        public double Scale => PaperUnits / DrawingUnits;

        /// <summary>
        /// Gets whether the drawing keeps the scale, as AutoCAD keeps one: the unit scale, the current annotation scale
        /// (CANNOSCALE) and one an object points at. Delete is greyed for it, and Reset keeps it.
        /// </summary>
        public bool IsKept { get; }

        /// <summary>
        /// Gets whether an object of the drawing points at the scale, a viewport or an annotative object: AutoCAD's Edit Scale
        /// does not change its units then ("You cannot change the scale properties of a scale that is referenced by an object
        /// in the drawing").
        /// </summary>
        public bool IsInUse { get; }

        /// <summary>Gets the name the scale has in the drawing; null for one added in the dialog.</summary>
        public string Original { get; }

        /// <summary>The scale with another name and units, the drawing's scale it stands for kept.</summary>
        /// <param name="name">The name.</param>
        /// <param name="paperUnits">The paper units.</param>
        /// <param name="drawingUnits">The drawing units.</param>
        /// <returns>The scale.</returns>
        public CadScaleListRow With(string name, double paperUnits, double drawingUnits) =>
            new CadScaleListRow(name, paperUnits, drawingUnits, Original, IsKept, IsInUse);

        /// <summary>The line Selected scale shows: "1 paper unit = 50 drawing units".</summary>
        /// <returns>The line.</returns>
        public string Sentence() =>
            Number(PaperUnits)
            + (PaperUnits == 1.0 ? " paper unit = " : " paper units = ")
            + Number(DrawingUnits)
            + (DrawingUnits == 1.0 ? " drawing unit" : " drawing units");

        /// <summary>A number as the dialog writes units: up to eight decimals, no zeros after them, 0.0078125 for 1/128.</summary>
        /// <param name="value">The number.</param>
        /// <returns>The text.</returns>
        public static string Number(double value) => value.ToString("0.########", CultureInfo.InvariantCulture);

        /// <inheritdoc/>
        public override string ToString() => Name;
    }

    /// <summary>
    /// AutoCAD's Edit Drawing Scales of SCALELISTEDIT, as data its command and the window share: the drawing's list of scales
    /// (ACAD_SCALELIST) in its order, which the dialog changes as it likes, and the default list Reset puts back. OK writes
    /// the list as it is then into the drawing as one undo step: the scales taken out deleted, the ones changed renamed and
    /// given their units, the new ones added and every one put in its place; Cancel writes nothing.
    /// </summary>
    public sealed class CadScaleListRequest
    {
        internal CadScaleListRequest(
            CadDocument document,
            IReadOnlyList<CadScaleListRow> scales,
            IReadOnlyList<CadScaleListRow> defaults
        )
        {
            Document = document ?? throw new ArgumentNullException(nameof(document));
            Scales = scales;
            Defaults = defaults;
        }

        /// <summary>Gets the document.</summary>
        public CadDocument Document { get; }

        /// <summary>Gets or sets the scales in their order: the drawing's when the dialog opens, the dialog's on OK.</summary>
        public IReadOnlyList<CadScaleListRow> Scales { get; set; }

        /// <summary>
        /// Gets the default list Reset puts back, as AutoCAD 2021's: the metric scales 1:1 to 100:1 (17), or, in a drawing in
        /// imperial units (MEASUREMENT 0), the imperial ones 1/128" = 1'-0" to 1'-0" = 1'-0" (16).
        /// </summary>
        public IReadOnlyList<CadScaleListRow> Defaults { get; }

        /// <summary>Gets or sets what the dialog was closed with: OK writes <see cref="Scales"/>; Cancel until set.</summary>
        public CadDialogAction Action { get; set; } = CadDialogAction.Cancel;
    }
}
