using System;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The Drawing Units dialog of UNITS and its Direction Control, as data their command and the window share: LUNITS,
    /// LUPREC, AUNITS, AUPREC, ANGDIR, INSUNITS and ANGBASE as the dialog has them, read from the drawing's header and
    /// written there on OK as one undo step, and the Sample Output they give. Direction Control's pick closes the dialogs
    /// with <see cref="CadDialogAction.SelectObjects"/>: the command asks two points on the command line, then shows them
    /// again with the angle between them.
    /// </summary>
    public sealed class CadUnitsRequest
    {
        internal CadUnitsRequest(DxfDatabase database)
        {
            if (database == null)
            {
                throw new ArgumentNullException(nameof(database));
            }

            LengthUnits = Math.Max(1, Math.Min(5, database.Lunits));
            LengthPrecision = Math.Max(0, Math.Min(8, database.Luprec));
            AngleUnits = Math.Max(0, Math.Min(4, database.Aunits));
            AnglePrecision = Math.Max(0, Math.Min(8, database.Auprec));
            Clockwise = database.Angdir;
            InsertionUnits = database.Insunits;
            BaseAngle = database.Angbase;
        }

        /// <summary>Gets or sets LUNITS: 1 scientific, 2 decimal, 3 engineering, 4 architectural, 5 fractional.</summary>
        public int LengthUnits { get; set; }

        /// <summary>Gets or sets LUPREC, 0 to 8: places, or the power of two of the smallest fraction in 4 and 5.</summary>
        public int LengthPrecision { get; set; }

        /// <summary>Gets or sets AUNITS: 0 decimal degrees, 1 degrees, minutes and seconds, 2 grads, 3 radians, 4 surveyor's units.</summary>
        public int AngleUnits { get; set; }

        /// <summary>Gets or sets AUPREC, 0 to 8.</summary>
        public int AnglePrecision { get; set; }

        /// <summary>Gets or sets ANGDIR: whether angles grow clockwise.</summary>
        public bool Clockwise { get; set; }

        /// <summary>Gets or sets INSUNITS: the units content inserted is scaled to.</summary>
        public DxfUnits InsertionUnits { get; set; }

        /// <summary>Gets or sets ANGBASE: the direction of angle nought, in radians from east.</summary>
        public double BaseAngle { get; set; }

        /// <summary>Gets or sets whether Direction Control is open, so that it is shown again after its pick.</summary>
        public bool IsDirectionOpen { get; set; }

        /// <summary>Gets or sets what the dialog was closed with: OK, Cancel, or Select objects for Direction Control's pick; Cancel until set.</summary>
        public CadDialogAction Action { get; set; }

        /// <summary>Gets the first line of Sample Output, the point (1.5, 2, 0) in the units chosen: 1.50,2.00,0.00 in decimal to two places.</summary>
        public string SampleLength => Length(1.5) + "," + Length(2.0) + "," + Length(0.0);

        /// <summary>Gets the second line of Sample Output, 3 at 45 degrees: 3.00&lt;45,0.00 in decimal to two places and whole degrees.</summary>
        public string SampleAngle =>
            Length(3.0) + "<" + CadUnits.Angle(Math.PI / 4.0, AngleUnits, AnglePrecision) + "," + Length(0.0);

        private string Length(double value) => CadUnits.Length(value, LengthUnits, LengthPrecision);
    }
}
