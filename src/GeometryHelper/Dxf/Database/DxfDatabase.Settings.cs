using System;
using System.Globalization;
using GeometryHelper.Export;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The settings of drafting AutoCAD's <c>Database</c> has as properties, each a variable of the HEADER section read and set
    /// as <see cref="Ltscale"/> is: chamfers and fillets, elevation and thickness, multilines, the limits and extents of paper
    /// space, the user's own variables, the modes of the drawing and what it says of itself. Where the header gives none, the
    /// value a new drawing of ezdxf 1.4.5 is given, but for the extents of paper space, the origin as for model space, the
    /// maintenance release, nought, and the multiline style, null where the drawing has none of the name. A variable R2000
    /// (AC1015) or R2004 (AC1018) brought is refused in a file of an older version, as AutoCAD writes none there; it reads as
    /// where the header gives none. A text is read as the header writes it, its spaces kept.
    /// </summary>
    public sealed partial class DxfDatabase
    {
        private string _filename;
        private string _originalFileName;

        // What a number set has to be: any finite number, one of nought or more, or one above nought.
        private enum Bound
        {
            Finite,
            NonNegative,
            Positive,
        }

        /// <summary>
        /// Gets the full path of the file the drawing was last opened from or saved to, as AutoCAD's <c>Filename</c> gives the
        /// file of a drawing: <c>C:\Plans\plan.dxf</c> once <c>Open(@"C:\Plans\plan.dxf")</c> has read it, the path of a copy
        /// once <see cref="Save(string)"/> has written one; null for a drawing opened from a stream or a text, and not saved to
        /// a path since.
        /// </summary>
        public string Filename => _filename;

        /// <summary>
        /// Gets the full path of the file the drawing was opened from, as AutoCAD's <c>OriginalFileName</c>: kept when the
        /// drawing is saved elsewhere; null for a drawing opened from a stream or a text.
        /// </summary>
        public string OriginalFileName => _originalFileName;

        /// <summary>
        /// Gets the maintenance release of AutoCAD the file is written for (<c>$ACADMAINTVER</c>, group 70, or 90 from R2018), as
        /// AutoCAD's <c>MaintenanceReleaseVersion</c>: nought where the header gives none.
        /// </summary>
        public int MaintenanceReleaseVersion => Maintenance();

        /// <summary>
        /// Gets the maintenance release the file was last saved as (<c>$ACADMAINTVER</c>), as AutoCAD's
        /// <c>LastSavedAsMaintenanceVersion</c> numbers it: the drawing is saved in the release it was read in, so this is
        /// <see cref="MaintenanceReleaseVersion"/>.
        /// </summary>
        public int LastSavedAsMaintenanceVersion => Maintenance();

        /// <summary>
        /// Gets the maintenance release of the file as it was opened (<c>$ACADMAINTVER</c>), as AutoCAD's
        /// <c>OriginalFileMaintenanceVersion</c> numbers it: the drawing keeps the release it was read in, so this is
        /// <see cref="MaintenanceReleaseVersion"/>.
        /// </summary>
        public int OriginalFileMaintenanceVersion => Maintenance();

        /// <summary>Gets the menu file the drawing was last edited with (<c>$MENU</c>), as AutoCAD's <c>Menu</c>: <c>.</c> where the header gives none.</summary>
        public string Menu => TextOf("$MENU", 1, ".");

        /// <summary>
        /// Gets whether the drawing plots by the colours of its entities (<c>$PSTYLEMODE</c> of 1, a colour-dependent plot style
        /// table, CTB) rather than by named plot styles (0, STB), as AutoCAD's <c>PlotStyleMode</c>; true where the header gives
        /// none.
        /// </summary>
        public bool PlotStyleMode => Bool("$PSTYLEMODE", 290, true);

        /// <summary>
        /// Gets or sets whether the names of layers, linetypes and the other records may be up to 255 letters long and hold
        /// spaces (<c>$EXTNAMES</c> of 1), as AutoCAD's <c>AllowExtendedNames</c>; true where the header gives none. Set to
        /// false, the names already given are kept.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2000 (AC1015), which has no such variable.</exception>
        public bool AllowExtendedNames
        {
            get => Bool("$EXTNAMES", 290, true);
            set => PutBool("$EXTNAMES", 290, value, AllowExtendedNames, 1015);
        }

        /// <summary>Gets or sets how attributes are shown (<c>$ATTMODE</c>), as AutoCAD's <c>Attmode</c>: 0 none, 1 those not hidden, 2 all; 1 where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not 0, 1 or 2.</exception>
        public int Attmode
        {
            get => Whole("$ATTMODE", 70, 1);
            set => PutWhole("$ATTMODE", 70, value, Attmode, 0, 2, 1009);
        }

        /// <summary>
        /// Gets or sets the linetype scale new entities are given (<c>$CELTSCALE</c>), as AutoCAD's <c>Celtscale</c>: an entity
        /// of 0.5 has dashes half as long as the drawing's <see cref="Ltscale"/> makes them; 1 where the header gives none.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the scale set is not a finite number above nought.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2000 (AC1015), which has no such variable.</exception>
        public double Celtscale
        {
            get => Real("$CELTSCALE", 40, 1.0);
            set => PutReal("$CELTSCALE", 40, value, Celtscale, Bound.Positive, 1015);
        }

        /// <summary>Gets or sets the first distance of a chamfer (<c>$CHAMFERA</c>), as AutoCAD's <c>Chamfera</c>, in drawing units: a chamfer of 10 by 5 cuts 10 off the first line; nought where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the distance set is not a finite number of nought or more.</exception>
        public double Chamfera
        {
            get => Real("$CHAMFERA", 40, 0.0);
            set => PutReal("$CHAMFERA", 40, value, Chamfera, Bound.NonNegative, 1009);
        }

        /// <summary>Gets or sets the second distance of a chamfer (<c>$CHAMFERB</c>), as AutoCAD's <c>Chamferb</c>, in drawing units; nought where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the distance set is not a finite number of nought or more.</exception>
        public double Chamferb
        {
            get => Real("$CHAMFERB", 40, 0.0);
            set => PutReal("$CHAMFERB", 40, value, Chamferb, Bound.NonNegative, 1009);
        }

        /// <summary>Gets or sets the length of a chamfer made by a length and an angle (<c>$CHAMFERC</c>), as AutoCAD's <c>Chamferc</c>, in drawing units; nought where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the length set is not a finite number of nought or more.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2000 (AC1015), which has no such variable.</exception>
        public double Chamferc
        {
            get => Real("$CHAMFERC", 40, 0.0);
            set => PutReal("$CHAMFERC", 40, value, Chamferc, Bound.NonNegative, 1015);
        }

        /// <summary>Gets or sets the angle of a chamfer made by a length and an angle (<c>$CHAMFERD</c>), as AutoCAD's <c>Chamferd</c>, in radians as the header holds it; nought where it gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the angle set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2000 (AC1015), which has no such variable.</exception>
        public double Chamferd
        {
            get => Real("$CHAMFERD", 40, 0.0);
            set => PutReal("$CHAMFERD", 40, value, Chamferd, Bound.Finite, 1015);
        }

        /// <summary>Gets or sets how new multilines are placed on the points picked (<c>$CMLJUST</c>), as AutoCAD's <c>Cmljust</c>: 0 by their top, 1 their middle, 2 their bottom; 0 where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not 0, 1 or 2.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2000 (AC1015), which has no such variable.</exception>
        public int Cmljust
        {
            get => Whole("$CMLJUST", 70, 0);
            set => PutWhole("$CMLJUST", 70, value, Cmljust, 0, 2, 1015);
        }

        /// <summary>
        /// Gets or sets the scale of new multilines (<c>$CMLSCALE</c>), as AutoCAD's <c>Cmlscale</c>: a style 1 wide drawn 20 wide
        /// at 20, the other way round below nought; 20 where the header gives none.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the scale set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2000 (AC1015), which has no such variable.</exception>
        public double Cmlscale
        {
            get => Real("$CMLSCALE", 40, 20.0);
            set => PutReal("$CMLSCALE", 40, value, Cmlscale, Bound.Finite, 1015);
        }

        /// <summary>
        /// Gets or sets the multiline style new multilines take (<c>$CMLSTYLE</c>), as AutoCAD's <c>CmlstyleID</c> gives its id:
        /// the MLINESTYLE of the drawing's dictionary of multiline styles (<see cref="MLStyleDictionary"/>) the header names,
        /// <c>Standard</c> where it names none; null where the dictionary has no style of the name.
        /// </summary>
        /// <value>Set, a style of <see cref="MLStyleDictionary"/>: the header is given its name there.</value>
        /// <exception cref="ArgumentNullException">Thrown when the style set is null; so <c>db.CmlstyleID = db.CmlstyleID</c>
        /// throws where the header names a style the dictionary has not, which reads as null.</exception>
        /// <exception cref="ArgumentException">Thrown when the style set is not an entry of the drawing's dictionary of multiline styles.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2000 (AC1015), which has no such variable.</exception>
        public DxfDBObject CmlstyleID
        {
            get
            {
                string name = Name("$CMLSTYLE", 2, "Standard");
                DxfDBDictionary styles = MLStyleDictionary;
                return styles != null && styles.Contains(name) ? styles.GetAt(name) : null;
            }

            set
            {
                DxfDBDictionary styles = MLStyleDictionary;

                if (Entered("$CMLSTYLE", value, CmlstyleID, styles, "multiline styles (ACAD_MLINESTYLE)", 1015))
                {
                    Set("$CMLSTYLE", 2, styles.NameAt(value));
                }
            }
        }

        /// <summary>Gets or sets whether the edges of a body are drawn as its outline alone in a wireframe view (<c>$DISPSILH</c> of 1), as AutoCAD's <c>DispSilh</c>; false where the header gives none.</summary>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2000 (AC1015), which has no such variable.</exception>
        public bool DispSilh
        {
            get => Bool("$DISPSILH", 70, false);
            set => PutBool("$DISPSILH", 70, value, DispSilh, 1015);
        }

        /// <summary>Gets or sets the elevation new entities are drawn at in model space (<c>$ELEVATION</c>), as AutoCAD's <c>Elevation</c>, in drawing units; nought where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the elevation set is not a finite number.</exception>
        public double Elevation
        {
            get => Real("$ELEVATION", 40, 0.0);
            set => PutReal("$ELEVATION", 40, value, Elevation, Bound.Finite, 1009);
        }

        /// <summary>Gets or sets the elevation new entities are drawn at in paper space (<c>$PELEVATION</c>), as AutoCAD's <c>Pelevation</c>, in drawing units; nought where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the elevation set is not a finite number.</exception>
        public double Pelevation
        {
            get => Real("$PELEVATION", 40, 0.0);
            set => PutReal("$PELEVATION", 40, value, Pelevation, Bound.Finite, 1009);
        }

        /// <summary>Gets or sets the thickness new entities are given (<c>$THICKNESS</c>), as AutoCAD's <c>Thickness</c>, in drawing units: a circle of 30 is drawn as a tube 30 high; nought where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the thickness set is not a finite number.</exception>
        public double Thickness
        {
            get => Real("$THICKNESS", 40, 0.0);
            set => PutReal("$THICKNESS", 40, value, Thickness, Bound.Finite, 1009);
        }

        /// <summary>Gets or sets how the ends of wide lines are drawn (<c>$ENDCAPS</c>), as AutoCAD's <c>EndCaps</c>; <see cref="DxfEndCap.None"/> where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value set is not one of <see cref="DxfEndCap"/>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2000 (AC1015), which has no such variable.</exception>
        public DxfEndCap EndCaps
        {
            get => (DxfEndCap)Whole("$ENDCAPS", 280, 0);
            set => PutWhole("$ENDCAPS", 280, (int)value, (int)EndCaps, 0, 3, 1015);
        }

        /// <summary>Gets or sets how wide lines that meet are joined (<c>$JOINSTYLE</c>), as AutoCAD's <c>JoinStyle</c>; <see cref="DxfJoinStyle.StyleNone"/> where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value set is not one of <see cref="DxfJoinStyle"/>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2000 (AC1015), which has no such variable.</exception>
        public DxfJoinStyle JoinStyle
        {
            get => (DxfJoinStyle)Whole("$JOINSTYLE", 280, 0);
            set => PutWhole("$JOINSTYLE", 280, (int)value, (int)JoinStyle, 0, 3, 1015);
        }

        /// <summary>Gets or sets the radius of a fillet (<c>$FILLETRAD</c>), as AutoCAD's <c>Filletrad</c>, in drawing units; 10 where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the radius set is not a finite number of nought or more.</exception>
        public double Filletrad
        {
            get => Real("$FILLETRAD", 40, 10.0);
            set => PutReal("$FILLETRAD", 40, value, Filletrad, Bound.NonNegative, 1009);
        }

        /// <summary>
        /// Gets or sets the identity of the drawing (<c>$FINGERPRINTGUID</c>), as AutoCAD's <c>FingerprintGuid</c>: a GUID, kept
        /// by every copy of the drawing; read as the header writes it, the GUID of noughts where it gives none.
        /// </summary>
        /// <value>Set, a GUID in any form <see cref="Guid.TryParse(string, out Guid)"/> reads, written in braces and capitals as
        /// AutoCAD writes one: <c>7021885a-675a-47a4-aa57-30a391b5281b</c> is written <c>{7021885A-675A-47A4-AA57-30A391B5281B}</c>.</value>
        /// <exception cref="ArgumentNullException">Thrown when the text set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the text set is not a GUID.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2000 (AC1015), which has no such variable.</exception>
        public string FingerprintGuid
        {
            get => TextOf("$FINGERPRINTGUID", 2, "{00000000-0000-0000-0000-000000000000}");
            set => PutGuid("$FINGERPRINTGUID", value, FingerprintGuid);
        }

        /// <summary>
        /// Gets or sets the identity of the drawing as last saved (<c>$VERSIONGUID</c>), as AutoCAD's <c>VersionGuid</c>: a GUID
        /// AutoCAD gives the drawing anew each time it saves it; read as the header writes it, the GUID of noughts where it gives
        /// none.
        /// </summary>
        /// <value>Set, a GUID in any form <see cref="Guid.TryParse(string, out Guid)"/> reads, written in braces and capitals as
        /// AutoCAD writes one.</value>
        /// <exception cref="ArgumentNullException">Thrown when the text set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the text set is not a GUID.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2000 (AC1015), which has no such variable.</exception>
        public string VersionGuid
        {
            get => TextOf("$VERSIONGUID", 2, "{00000000-0000-0000-0000-000000000000}");
            set => PutGuid("$VERSIONGUID", value, VersionGuid);
        }

        /// <summary>Gets or sets the path the relative hyperlinks of the drawing start from (<c>$HYPERLINKBASE</c>), as AutoCAD's <c>HyperlinkBase</c>: empty, the folder of the drawing, where the header gives none.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the path set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the path set holds a line break.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2000 (AC1015), which has no such variable.</exception>
        public string HyperlinkBase
        {
            get => TextOf("$HYPERLINKBASE", 1, string.Empty);
            set => PutText("$HYPERLINKBASE", 1, value, HyperlinkBase, 1015);
        }

        /// <summary>Gets or sets the plot style table the drawing plots with (<c>$STYLESHEET</c>), as AutoCAD's <c>StyleSheet</c>, by the name of its file, as <c>monochrome.ctb</c>; empty where the header gives none.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the name set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the name set holds a line break.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2000 (AC1015), which has no such variable.</exception>
        public string StyleSheet
        {
            get => TextOf("$STYLESHEET", 1, string.Empty);
            set => PutText("$STYLESHEET", 1, value, StyleSheet, 1015);
        }

        /// <summary>Gets or sets the name of the project the drawing belongs to (<c>$PROJECTNAME</c>), as AutoCAD's <c>ProjectName</c>, under which AutoCAD looks for its external references; empty where the header gives none.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the name set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the name set holds a line break.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2004 (AC1018), which has no such variable.</exception>
        public string ProjectName
        {
            get => TextOf("$PROJECTNAME", 1, string.Empty);
            set => PutText("$PROJECTNAME", 1, value, ProjectName, 1018);
        }

        /// <summary>Gets or sets whether drawing outside the limits of model space is refused (<c>$LIMCHECK</c> of 1), as AutoCAD's <c>Limcheck</c>; false where the header gives none.</summary>
        public bool Limcheck
        {
            get => Bool("$LIMCHECK", 70, false);
            set => PutBool("$LIMCHECK", 70, value, Limcheck, 1009);
        }

        /// <summary>Gets or sets whether drawing outside the limits of paper space is refused (<c>$PLIMCHECK</c> of 1), as AutoCAD's <c>Plimcheck</c>; false where the header gives none.</summary>
        public bool Plimcheck
        {
            get => Bool("$PLIMCHECK", 70, false);
            set => PutBool("$PLIMCHECK", 70, value, Plimcheck, 1009);
        }

        /// <summary>Gets or sets the least corner of the drawing's limits in paper space (<c>$PLIMMIN</c>), as AutoCAD's <c>Plimmin</c>: (0, 0) where the header gives none.</summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate set is not a finite number.</exception>
        public GeoPoint2 Plimmin
        {
            get => Point2("$PLIMMIN", new GeoPoint2(0.0, 0.0));
            set => SetPoint2("$PLIMMIN", value, Plimmin);
        }

        /// <summary>Gets or sets the greatest corner of the drawing's limits in paper space (<c>$PLIMMAX</c>), as AutoCAD's <c>Plimmax</c>: (420, 297), a sheet of A3, where the header gives none.</summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate set is not a finite number.</exception>
        public GeoPoint2 Plimmax
        {
            get => Point2("$PLIMMAX", new GeoPoint2(420.0, 297.0));
            set => SetPoint2("$PLIMMAX", value, Plimmax);
        }

        /// <summary>
        /// Gets or sets the least corner of the box the drawing's paper space fills (<c>$PEXTMIN</c>), as AutoCAD's
        /// <c>Pextmin</c>; the origin where the header gives none. Like <see cref="Extmin"/>, it is not shrunk by what is erased.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate set is not a finite number.</exception>
        public GeoPoint3 Pextmin
        {
            get => Point("$PEXTMIN");
            set => SetPoint("$PEXTMIN", value);
        }

        /// <summary>Gets or sets the greatest corner of the box the drawing's paper space fills (<c>$PEXTMAX</c>), as AutoCAD's <c>Pextmax</c>; the origin where the header gives none.</summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate set is not a finite number.</exception>
        public GeoPoint3 Pextmax
        {
            get => Point("$PEXTMAX");
            set => SetPoint("$PEXTMAX", value);
        }

        /// <summary>Gets or sets the point paper space is inserted at when the drawing is inserted into another (<c>$PINSBASE</c>), as AutoCAD's <c>Pinsbase</c>: the origin where the header gives none.</summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2000 (AC1015), which has no such variable.</exception>
        public GeoPoint3 Pinsbase
        {
            get => Point("$PINSBASE");

            set
            {
                GeoPoint3 now = Pinsbase;

                if (value.IsValid && !(Same(value.X, now.X) && Same(value.Y, now.Y) && Same(value.Z, now.Z)))
                {
                    Since(1015, "$PINSBASE");
                }

                SetPoint("$PINSBASE", value);
            }
        }

        /// <summary>Gets or sets whether the linetype of a polyline runs on round its vertices (<c>$PLINEGEN</c> of 1) rather than starting again at each, as AutoCAD's <c>Plinegen</c>; false where the header gives none.</summary>
        public bool Plinegen
        {
            get => Bool("$PLINEGEN", 70, false);
            set => PutBool("$PLINEGEN", 70, value, Plinegen, 1009);
        }

        /// <summary>Gets or sets the width new polylines are given (<c>$PLINEWID</c>), as AutoCAD's <c>Plinewid</c>, in drawing units; nought where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the width set is not a finite number of nought or more.</exception>
        public double Plinewid
        {
            get => Real("$PLINEWID", 40, 0.0);
            set => PutReal("$PLINEWID", 40, value, Plinewid, Bound.NonNegative, 1009);
        }

        /// <summary>Gets or sets the width new traces are given (<c>$TRACEWID</c>), as AutoCAD's <c>Tracewid</c>, in drawing units; 1 where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the width set is not a finite number of nought or more.</exception>
        public double Tracewid
        {
            get => Real("$TRACEWID", 40, 1.0);
            set => PutReal("$TRACEWID", 40, value, Tracewid, Bound.NonNegative, 1009);
        }

        /// <summary>
        /// Gets or sets whether the linetypes of paper space scale the dashes of what viewports show by the scale of each
        /// viewport (<c>$PSLTSCALE</c> of 1), as AutoCAD's <c>Psltscale</c>, so that a dash is as long on the sheet whatever the
        /// viewport; true where the header gives none.
        /// </summary>
        public bool Psltscale
        {
            get => Bool("$PSLTSCALE", 70, true);
            set => PutBool("$PSLTSCALE", 70, value, Psltscale, 1009);
        }

        /// <summary>Gets or sets whether texts are drawn as their boxes alone (<c>$QTEXTMODE</c> of 1), as AutoCAD's <c>Qtextmode</c>; false where the header gives none.</summary>
        public bool Qtextmode
        {
            get => Bool("$QTEXTMODE", 70, false);
            set => PutBool("$QTEXTMODE", 70, value, Qtextmode, 1009);
        }

        /// <summary>Gets or sets whether the drawing is regenerated as it changes (<c>$REGENMODE</c> of 1), as AutoCAD's <c>Regenmode</c>; true where the header gives none.</summary>
        public bool Regenmode
        {
            get => Bool("$REGENMODE", 70, true);
            set => PutBool("$REGENMODE", 70, value, Regenmode, 1009);
        }

        /// <summary>Gets or sets the length of each piece a sketch is drawn with (<c>$SKETCHINC</c>), as AutoCAD's <c>Sketchinc</c>, in drawing units; 1 where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the length set is not a finite number above nought.</exception>
        public double Sketchinc
        {
            get => Real("$SKETCHINC", 40, 1.0);
            set => PutReal("$SKETCHINC", 40, value, Sketchinc, Bound.Positive, 1009);
        }

        /// <summary>Gets or sets whether a sketch is drawn as polylines (<c>$SKPOLY</c> of 1) rather than lines, as AutoCAD's <c>Skpoly</c>; false where the header gives none.</summary>
        public bool Skpoly
        {
            get => Bool("$SKPOLY", 70, false);
            set => PutBool("$SKPOLY", 70, value, Skpoly, 1009);
        }

        /// <summary>Gets or sets whether the frame of a polyline fitted with a spline, and the hidden edges of 3D faces, are shown (<c>$SPLFRAME</c> of 1), as AutoCAD's <c>Splframe</c>; false where the header gives none.</summary>
        public bool Splframe
        {
            get => Bool("$SPLFRAME", 70, false);
            set => PutBool("$SPLFRAME", 70, value, Splframe, 1009);
        }

        /// <summary>Gets or sets how many faces a ruled or revolved surface is drawn with along M (<c>$SURFTAB1</c>), as AutoCAD's <c>Surftab1</c>: 2 to 32 766; 6 where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not from 2 to 32 766.</exception>
        public int Surftab1
        {
            get => Whole("$SURFTAB1", 70, 6);
            set => PutWhole("$SURFTAB1", 70, value, Surftab1, 2, 32766, 1009);
        }

        /// <summary>Gets or sets how many faces a revolved or edge surface is drawn with along N (<c>$SURFTAB2</c>), as AutoCAD's <c>Surftab2</c>: 2 to 32 766; 6 where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not from 2 to 32 766.</exception>
        public int Surftab2
        {
            get => Whole("$SURFTAB2", 70, 6);
            set => PutWhole("$SURFTAB2", 70, value, Surftab2, 2, 32766, 1009);
        }

        /// <summary>
        /// Gets or sets whether model space is the space shown (<c>$TILEMODE</c> of 1) rather than a layout of paper space, as
        /// AutoCAD's <c>TileMode</c>; true where the header gives none. Set, the variable alone is written: no layout is made
        /// current.
        /// </summary>
        public bool TileMode
        {
            get => Bool("$TILEMODE", 70, true);
            set => PutBool("$TILEMODE", 70, value, TileMode, 1009);
        }

        /// <summary>Gets or sets the depth of the tree AutoCAD sorts the drawing's entities into (<c>$TREEDEPTH</c>), as AutoCAD's <c>Treedepth</c>: four digits, two for model space and two for paper space; 3020 where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not from -32 768 to 32 767.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2000 (AC1015), which has no such variable.</exception>
        public int Treedepth
        {
            get => Whole("$TREEDEPTH", 70, 3020);
            set => PutWhole("$TREEDEPTH", 70, value, Treedepth, short.MinValue, short.MaxValue, 1015);
        }

        /// <summary>Gets or sets how lengths are shown when typed (<c>$UNITMODE</c>), as AutoCAD's <c>Unitmode</c>: 0 as they are shown, 1 as they are typed in; 0 where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not 0 or 1.</exception>
        public int Unitmode
        {
            get => Whole("$UNITMODE", 70, 0);
            set => PutWhole("$UNITMODE", 70, value, Unitmode, 0, 1, 1009);
        }

        /// <summary>Gets or sets the first whole number kept for programs (<c>$USERI1</c>), as AutoCAD's <c>Useri1</c>: -32 768 to 32 767; nought where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not from -32 768 to 32 767.</exception>
        public int Useri1
        {
            get => Whole("$USERI1", 70, 0);
            set => PutWhole("$USERI1", 70, value, Useri1, short.MinValue, short.MaxValue, 1009);
        }

        /// <summary>Gets or sets the second whole number kept for programs (<c>$USERI2</c>), as AutoCAD's <c>Useri2</c>: -32 768 to 32 767; nought where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not from -32 768 to 32 767.</exception>
        public int Useri2
        {
            get => Whole("$USERI2", 70, 0);
            set => PutWhole("$USERI2", 70, value, Useri2, short.MinValue, short.MaxValue, 1009);
        }

        /// <summary>Gets or sets the third whole number kept for programs (<c>$USERI3</c>), as AutoCAD's <c>Useri3</c>: -32 768 to 32 767; nought where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not from -32 768 to 32 767.</exception>
        public int Useri3
        {
            get => Whole("$USERI3", 70, 0);
            set => PutWhole("$USERI3", 70, value, Useri3, short.MinValue, short.MaxValue, 1009);
        }

        /// <summary>Gets or sets the fourth whole number kept for programs (<c>$USERI4</c>), as AutoCAD's <c>Useri4</c>: -32 768 to 32 767; nought where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not from -32 768 to 32 767.</exception>
        public int Useri4
        {
            get => Whole("$USERI4", 70, 0);
            set => PutWhole("$USERI4", 70, value, Useri4, short.MinValue, short.MaxValue, 1009);
        }

        /// <summary>Gets or sets the fifth whole number kept for programs (<c>$USERI5</c>), as AutoCAD's <c>Useri5</c>: -32 768 to 32 767; nought where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not from -32 768 to 32 767.</exception>
        public int Useri5
        {
            get => Whole("$USERI5", 70, 0);
            set => PutWhole("$USERI5", 70, value, Useri5, short.MinValue, short.MaxValue, 1009);
        }

        /// <summary>Gets or sets the first real number kept for programs (<c>$USERR1</c>), as AutoCAD's <c>Userr1</c>; nought where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not a finite number.</exception>
        public double Userr1
        {
            get => Real("$USERR1", 40, 0.0);
            set => PutReal("$USERR1", 40, value, Userr1, Bound.Finite, 1009);
        }

        /// <summary>Gets or sets the second real number kept for programs (<c>$USERR2</c>), as AutoCAD's <c>Userr2</c>; nought where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not a finite number.</exception>
        public double Userr2
        {
            get => Real("$USERR2", 40, 0.0);
            set => PutReal("$USERR2", 40, value, Userr2, Bound.Finite, 1009);
        }

        /// <summary>Gets or sets the third real number kept for programs (<c>$USERR3</c>), as AutoCAD's <c>Userr3</c>; nought where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not a finite number.</exception>
        public double Userr3
        {
            get => Real("$USERR3", 40, 0.0);
            set => PutReal("$USERR3", 40, value, Userr3, Bound.Finite, 1009);
        }

        /// <summary>Gets or sets the fourth real number kept for programs (<c>$USERR4</c>), as AutoCAD's <c>Userr4</c>; nought where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not a finite number.</exception>
        public double Userr4
        {
            get => Real("$USERR4", 40, 0.0);
            set => PutReal("$USERR4", 40, value, Userr4, Bound.Finite, 1009);
        }

        /// <summary>Gets or sets the fifth real number kept for programs (<c>$USERR5</c>), as AutoCAD's <c>Userr5</c>; nought where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not a finite number.</exception>
        public double Userr5
        {
            get => Real("$USERR5", 40, 0.0);
            set => PutReal("$USERR5", 40, value, Userr5, Bound.Finite, 1009);
        }

        /// <summary>
        /// Gets or sets the scale new viewports are given (<c>$PSVPSCALE</c>), as AutoCAD's <c>ViewportScaleDefault</c>: paper
        /// space units to model space units, so that 0.02 shows a model of millimetres at 1:50 on a sheet of millimetres; nought,
        /// scaled to fit, where the header gives none.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the scale set is not a finite number of nought or more.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2000 (AC1015), which has no such variable.</exception>
        public double ViewportScaleDefault
        {
            get => Real("$PSVPSCALE", 40, 0.0);
            set => PutReal("$PSVPSCALE", 40, value, ViewportScaleDefault, Bound.NonNegative, 1015);
        }

        /// <summary>Gets or sets whether the layers of external references keep the visibility, colour and linetype the drawing gave them (<c>$VISRETAIN</c> of 1), as AutoCAD's <c>Visretain</c>; true where the header gives none.</summary>
        public bool Visretain
        {
            get => Bool("$VISRETAIN", 70, true);
            set => PutBool("$VISRETAIN", 70, value, Visretain, 1009);
        }

        /// <summary>Gets or sets whether a view's direction typed in is read in the world's coordinates (<c>$WORLDVIEW</c> of 1) rather than the current UCS's, as AutoCAD's <c>Worldview</c>; true where the header gives none.</summary>
        public bool Worldview
        {
            get => Bool("$WORLDVIEW", 70, true);
            set => PutBool("$WORLDVIEW", 70, value, Worldview, 1009);
        }

        /// <summary>Gets or sets the most viewports shown at once in a layout (<c>$MAXACTVP</c>), as AutoCAD's <c>Maxactvp</c>: 2 to 64; 64 where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not from 2 to 64.</exception>
        public int Maxactvp
        {
            get => Whole("$MAXACTVP", 70, 64);
            set => PutWhole("$MAXACTVP", 70, value, Maxactvp, 2, 64, 1009);
        }

        /// <summary>Gets or sets whether the drawing takes its hatch patterns and linetypes from the imperial or the metric files (<c>$MEASUREMENT</c>), as AutoCAD's <c>Measurement</c>; <see cref="DxfMeasurementValue.Metric"/> where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value set is not one of <see cref="DxfMeasurementValue"/>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2000 (AC1015), which has no such variable.</exception>
        public DxfMeasurementValue Measurement
        {
            get => (DxfMeasurementValue)Whole("$MEASUREMENT", 70, 1);
            set => PutWhole("$MEASUREMENT", 70, (int)value, (int)Measurement, 0, 1, 1015);
        }

        /// <summary>Gets or sets whether OLE objects start their own applications when the drawing is plotted (<c>$OLESTARTUP</c> of 1), as AutoCAD's <c>OleStartUp</c>; false where the header gives none.</summary>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2000 (AC1015), which has no such variable.</exception>
        public bool OleStartUp
        {
            get => Bool("$OLESTARTUP", 290, false);
            set => PutBool("$OLESTARTUP", 290, value, OleStartUp, 1015);
        }

        /// <summary>Gets or sets whether the drawing may be edited in place when it is an external reference of another (<c>$XEDIT</c> of 1), as AutoCAD's <c>XrefEditEnabled</c>; true where the header gives none.</summary>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2000 (AC1015), which has no such variable.</exception>
        public bool XrefEditEnabled
        {
            get => Bool("$XEDIT", 290, true);
            set => PutBool("$XEDIT", 290, value, XrefEditEnabled, 1015);
        }

        /// <summary>
        /// Gets the lineweight of AutoCAD nearest to a weight, as AutoCAD's <c>GetNearestLineWeight</c>: 0.24 mm, 24, gives
        /// <see cref="DxfLineWeight.LineWeight025"/>; a weight halfway between two the thicker, one above 2.11 mm
        /// <see cref="DxfLineWeight.LineWeight211"/> and one below nought <see cref="DxfLineWeight.LineWeight000"/>.
        /// </summary>
        /// <param name="weight">The weight, in hundredths of a millimetre.</param>
        /// <returns>The lineweight.</returns>
        public DxfLineWeight GetNearestLineWeight(int weight)
        {
            int nearest = 0;

            for (int w = 0; w <= 211; w++)
            {
                if (DxfShape.IsLineweight(w) && Math.Abs((long)weight - w) <= Math.Abs((long)weight - nearest))
                {
                    nearest = w;
                }
            }

            return (DxfLineWeight)nearest;
        }

        /// <summary>
        /// Gets whether a number is a lineweight DXF writes, as AutoCAD's <c>IsValidLineWeight</c>: one of AutoCAD's 24 widths,
        /// 25 for 0.25 mm, or -1, -2 and -3 for by layer, by block and the default; 24 is not.
        /// </summary>
        /// <param name="weight">The weight, in hundredths of a millimetre.</param>
        /// <returns>Whether it is a lineweight.</returns>
        public bool IsValidLineWeight(int weight) => (weight >= -3 && weight <= -1) || DxfShape.IsLineweight(weight);

        /// <summary>The maintenance release of the header, of group 90 or 70; nought where it gives none.</summary>
        private int Maintenance()
        {
            string written = First("$ACADMAINTVER", 90) ?? First("$ACADMAINTVER", 70);
            return int.TryParse(written?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : 0;
        }

        /// <summary>Refuses a variable the file's version has not; one R12 (AC1009) has needs no check.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the file is older than the version the variable came with.</exception>
        private void Since(int version, string name)
        {
            if (version > 1009)
            {
                string release = version == 1015 ? "R2000" : version == 1018 ? "R2004" : "R2007";
                File.Requires(version, name, release + " (AC" + version.ToString(CultureInfo.InvariantCulture) + ")");
            }
        }

        private bool Bool(string name, int code, bool fallback) => Whole(name, code, fallback ? 1 : 0) != 0;

        /// <summary>A text of the header as it is written, its spaces kept, as ezdxf reads it, and its escapes read as letters.</summary>
        private string TextOf(string name, int code, string fallback)
        {
            string written = First(name, code);
            return written == null ? fallback : DxfReader.Decode(written);
        }

        private void PutBool(string name, int code, bool value, bool now, int since)
        {
            if (value != now)
            {
                Since(since, name);
                Set(name, code, value ? "1" : "0");
            }
        }

        private void PutWhole(string name, int code, int value, int now, int least, int most, int since)
        {
            if (value != now)
            {
                InRange(value, least, most, nameof(value));
                Since(since, name);
                Set(name, code, value.ToString(CultureInfo.InvariantCulture));
            }
        }

        private void PutReal(string name, int code, double value, double now, Bound bound, int since)
        {
            if (Same(value, now))
            {
                return;
            }

            string what = name.Substring(1) + " has to be a finite number";

            if (bound == Bound.Positive)
            {
                Guard.Positive(value, nameof(value), what + " above nought.");
            }
            else if (bound == Bound.NonNegative)
            {
                Guard.NonNegative(value, nameof(value), what + " of nought or more.");
            }
            else
            {
                Guard.Finite(value, nameof(value), what + ".");
            }

            Since(since, name);
            Set(name, code, Numbers.Exact(value + 0.0));
        }

        private void PutText(string name, int code, string value, string now, int since)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (string.Equals(value, now, StringComparison.Ordinal))
            {
                return;
            }

            if (value.IndexOf('\n') >= 0 || value.IndexOf('\r') >= 0)
            {
                throw new ArgumentException("A variable of the header is one line: " + name + " cannot hold a line break.", nameof(value));
            }

            Since(since, name);
            Set(name, code, value);
        }

        private void PutGuid(string name, string value, string now)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (string.Equals(value, now, StringComparison.Ordinal))
            {
                return;
            }

            if (!Guid.TryParse(value, out Guid guid))
            {
                throw new ArgumentException(name + " is a GUID, as {7021885A-675A-47A4-AA57-30A391B5281B}: '" + value + "' is not.", nameof(value));
            }

            PutText(name, 2, "{" + guid.ToString("D").ToUpperInvariant() + "}", now, 1015);
        }

        /// <summary>The object of the OBJECTS section a variable of the header names by its handle; null where it names none the drawing has.</summary>
        private DxfDBObject Pointed(string name, int code)
        {
            string handle = First(name, code)?.Trim();
            DxfItem item = string.IsNullOrEmpty(handle) || !File.HoldsObjects ? null : File.ObjectItem(handle);
            return item == null ? null : DxfDBObject.OfObject(item);
        }

        /// <summary>Sets a variable of the header to the handle of an object of a dictionary of the drawing; nothing where it names that object.</summary>
        private void PutPointed(string name, int code, DxfDBObject value, DxfDBObject now, DxfDBDictionary dictionary, string what, int since)
        {
            if (Entered(name, value, now, dictionary, what, since))
            {
                Set(name, code, value.Handle);
            }
        }

        /// <summary>
        /// Whether an object is to be set as a variable: false where it is the object the variable gives; true where it is
        /// another entry of the dictionary and the file's version has the variable.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the object is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the file is older than the version the variable came with.</exception>
        /// <exception cref="ArgumentException">Thrown when the object is not an entry of the dictionary of this drawing.</exception>
        private bool Entered(string name, DxfDBObject value, DxfDBObject now, DxfDBDictionary dictionary, string what, int since)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            bool ours = value.IsResident && !value.IsErased && value.Item.File == File;

            if (ours && now != null && string.Equals(value.Handle, now.Handle, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            Since(since, name);

            if (!ours || dictionary == null || !dictionary.Contains(value))
            {
                throw new ArgumentException("The object set is not one of the drawing's " + what + ".", nameof(value));
            }

            return true;
        }
    }
}
