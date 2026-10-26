using System;
using System.Collections.Generic;
using System.Globalization;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The settings of display and modelling in 3D AutoCAD's <c>Database</c> has as properties, each a variable of the HEADER
    /// section read and set as <see cref="Ltscale"/> is: shading, hidden lines and their colours, the frames of underlays and
    /// clipped references, cameras and walking, lofting, the place under the sun, shadows, materials and visual styles. Where
    /// the header gives none, the value a new drawing of ezdxf 1.4.5 is given, but for the size of a polysolid, which is that
    /// of AutoCAD's metric drawing, and the objects named, which are null. A variable R2004 (AC1018) or R2007 (AC1021) brought
    /// is refused in a file of an older version, as AutoCAD writes none there.
    /// </summary>
    public sealed partial class DxfDatabase
    {
        /// <summary>Gets or sets how faces are shaded when the drawing is shaded the old way (<c>$SHADEDGE</c>), as AutoCAD's <c>Shadedge</c>: 0 faces lit, no edges; 1 faces lit, edges in the background colour; 2 faces in the background colour, edges in their own; 3 faces in their own colour, edges in the background colour; 3 where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not from 0 to 3.</exception>
        public int Shadedge
        {
            get => Whole("$SHADEDGE", 70, 3);
            set => PutWhole("$SHADEDGE", 70, value, Shadedge, 0, 3, 1009);
        }

        /// <summary>Gets or sets how much of the light of the old way of shading is diffuse rather than ambient (<c>$SHADEDIF</c>), as AutoCAD's <c>Shadedif</c>, in per cent: 0 to 100; 70 where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not from 0 to 100.</exception>
        public int Shadedif
        {
            get => Whole("$SHADEDIF", 70, 70);
            set => PutWhole("$SHADEDIF", 70, value, Shadedif, 0, 100, 1009);
        }

        /// <summary>Gets or sets which operations sort the entities by their draw order (<c>$SORTENTS</c>), as AutoCAD's <c>SortEnts</c>: a sum of bits, 1 picking, 2 snapping, 4 redrawing, 8 slides, 16 regenerating, 32 plotting, 64 output to PostScript; 127 where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not from 0 to 127.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2004 (AC1018), which has no such variable.</exception>
        public int SortEnts
        {
            get => Whole("$SORTENTS", 280, 127);
            set => PutWhole("$SORTENTS", 280, value, SortEnts, 0, 127, 1018);
        }

        /// <summary>Gets or sets which indexes the drawing keeps for partial opening (<c>$INDEXCTL</c>), as AutoCAD's <c>Indexctl</c>: 0 none, 1 of layers, 2 of space, 3 both; 0 where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not from 0 to 3.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2004 (AC1018), which has no such variable.</exception>
        public int Indexctl
        {
            get => Whole("$INDEXCTL", 280, 0);
            set => PutWhole("$INDEXCTL", 280, value, Indexctl, 0, 3, 1018);
        }

        /// <summary>Gets or sets whether texts are hidden by what stands in front of them when hidden lines are removed (<c>$HIDETEXT</c>), as AutoCAD's <c>HideText</c>: 0 not, 1 they are; 1 where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not 0 or 1.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2004 (AC1018), which has no such variable.</exception>
        public int HideText
        {
            get => Whole("$HIDETEXT", 280, 1);
            set => PutWhole("$HIDETEXT", 280, value, HideText, 0, 1, 1018);
        }

        /// <summary>Gets or sets whether the frames that clip external references are shown and plotted (<c>$XCLIPFRAME</c>), as AutoCAD's <c>XclipFrame</c>: 0 hidden, 1 shown and plotted, 2 shown; 1 where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not from 0 to 2.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2004 (AC1018), which has no such variable.</exception>
        public int XclipFrame
        {
            get => Whole("$XCLIPFRAME", 280, 1);
            set => PutWhole("$XCLIPFRAME", 280, value, XclipFrame, 0, 2, 1018);
        }

        /// <summary>Gets or sets the gap left where a line passes behind another when hidden lines are removed (<c>$HALOGAP</c>), as AutoCAD's <c>HaloGap</c>, in per cent of a unit: 0 to 100; 0 where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not from 0 to 100.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2004 (AC1018), which has no such variable.</exception>
        public int HaloGap
        {
            get => Whole("$HALOGAP", 280, 0);
            set => PutWhole("$HALOGAP", 280, value, HaloGap, 0, 100, 1018);
        }

        /// <summary>Gets or sets the colour lines hidden by others are drawn in (<c>$OBSCOLOR</c>), as AutoCAD's <c>ObscuredColor</c>: 0 by block, 1 to 255 a colour of the index, 256 by layer, 257 by entity; 257 where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not from 0 to 257.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2004 (AC1018), which has no such variable.</exception>
        public int ObscuredColor
        {
            get => Whole("$OBSCOLOR", 70, 257);
            set => PutWhole("$OBSCOLOR", 70, value, ObscuredColor, 0, 257, 1018);
        }

        /// <summary>Gets or sets the linetype lines hidden by others are drawn with (<c>$OBSLTYPE</c>), as AutoCAD's <c>ObscuredLineType</c>: 0 they are not drawn, 1 to 11 the dashes and dots of AutoCAD's list; 0 where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not from 0 to 11.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2004 (AC1018), which has no such variable.</exception>
        public int ObscuredLineType
        {
            get => Whole("$OBSLTYPE", 280, 0);
            set => PutWhole("$OBSLTYPE", 280, value, ObscuredLineType, 0, 11, 1018);
        }

        /// <summary>Gets or sets whether the lines where the faces of bodies cross are drawn (<c>$INTERSECTIONDISPLAY</c>), as AutoCAD's <c>IntersectDisplay</c>: 0 not, 1 they are; 0 where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not 0 or 1.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2004 (AC1018), which has no such variable.</exception>
        public int IntersectDisplay
        {
            get => Whole("$INTERSECTIONDISPLAY", 280, 0);
            set => PutWhole("$INTERSECTIONDISPLAY", 280, value, IntersectDisplay, 0, 1, 1018);
        }

        /// <summary>Gets or sets the colour of the lines where the faces of bodies cross (<c>$INTERSECTIONCOLOR</c>), as AutoCAD's <c>IntersectColor</c>: 0 by block, 1 to 255 a colour of the index, 256 by layer, 257 by entity; 257 where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not from 0 to 257.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2004 (AC1018), which has no such variable.</exception>
        public int IntersectColor
        {
            get => Whole("$INTERSECTIONCOLOR", 70, 257);
            set => PutWhole("$INTERSECTIONCOLOR", 70, value, IntersectColor, 0, 257, 1018);
        }

        /// <summary>Gets or sets whether cameras are shown (<c>$CAMERADISPLAY</c> of 1), as AutoCAD's <c>CameraDisplay</c>; false where the header gives none.</summary>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2007 (AC1021), which has no such variable.</exception>
        public bool CameraDisplay
        {
            get => Bool("$CAMERADISPLAY", 290, false);
            set => PutBool("$CAMERADISPLAY", 290, value, CameraDisplay, 1021);
        }

        /// <summary>Gets or sets the height new cameras are placed at (<c>$CAMERAHEIGHT</c>), as AutoCAD's <c>CameraHeight</c>, in drawing units; nought where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the height set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2007 (AC1021), which has no such variable.</exception>
        public double CameraHeight
        {
            get => Real("$CAMERAHEIGHT", 40, 0.0);
            set => PutReal("$CAMERAHEIGHT", 40, value, CameraHeight, Bound.Finite, 1021);
        }

        /// <summary>Gets or sets the length of the lens of a view in perspective (<c>$LENSLENGTH</c>), as AutoCAD's <c>LensLength</c>, in millimetres: 50 where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the length set is not a finite number above nought.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2007 (AC1021), which has no such variable.</exception>
        public double LensLength
        {
            get => Real("$LENSLENGTH", 40, 50.0);
            set => PutReal("$LENSLENGTH", 40, value, LensLength, Bound.Positive, 1021);
        }

        /// <summary>Gets or sets the length of a step when walking or flying through the drawing (<c>$STEPSIZE</c>), as AutoCAD's <c>StepSize</c>, in drawing units: 1E-6 to 1E+6; 100 where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the length set is not from 1E-6 to 1E+6.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2007 (AC1021), which has no such variable.</exception>
        public double StepSize
        {
            get => Real("$STEPSIZE", 40, 100.0);

            set
            {
                if (!Same(value, StepSize) && !(value >= 1E-6 && value <= 1E6))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "STEPSIZE is from 1E-6 to 1E+6.");
                }

                PutReal("$STEPSIZE", 40, value, StepSize, Bound.Positive, 1021);
            }
        }

        /// <summary>Gets or sets how many steps are taken each second when walking or flying through the drawing (<c>$STEPSPERSEC</c>), as AutoCAD's <c>StepsPerSec</c>: 1 to 30; 24 where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not from 1 to 30.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2007 (AC1021), which has no such variable.</exception>
        public double StepsPerSec
        {
            get => Real("$STEPSPERSEC", 40, 24.0);

            set
            {
                if (!Same(value, StepsPerSec) && !(value >= 1.0 && value <= 30.0))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "STEPSPERSEC is from 1 to 30.");
                }

                PutReal("$STEPSPERSEC", 40, value, StepsPerSec, Bound.Positive, 1021);
            }
        }

        /// <summary>Gets or sets the height new polysolids are given (<c>$PSOLHEIGHT</c>), as AutoCAD's <c>PsolHeight</c>, in drawing units; 80 where the header gives none, as AutoCAD's metric drawing (acadiso.dwt) has it, where a new drawing of ezdxf has 0.08.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the height set is not a finite number above nought.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2007 (AC1021), which has no such variable.</exception>
        public double PsolHeight
        {
            get => Real("$PSOLHEIGHT", 40, 80.0);
            set => PutReal("$PSOLHEIGHT", 40, value, PsolHeight, Bound.Positive, 1021);
        }

        /// <summary>Gets or sets the width new polysolids are given (<c>$PSOLWIDTH</c>), as AutoCAD's <c>PsolWidth</c>, in drawing units; 5 where the header gives none, as AutoCAD's metric drawing (acadiso.dwt) has it, where a new drawing of ezdxf has 0.005.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the width set is not a finite number above nought.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2007 (AC1021), which has no such variable.</exception>
        public double PsolWidth
        {
            get => Real("$PSOLWIDTH", 40, 5.0);
            set => PutReal("$PSOLWIDTH", 40, value, PsolWidth, Bound.Positive, 1021);
        }

        /// <summary>Gets or sets the draft angle of a loft through its first section (<c>$LOFTANG1</c>), as AutoCAD's <c>LoftAng1</c>, in radians: nought to less than two pi, as AutoCAD takes 0 to less than 360 degrees; a quarter turn where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the angle set is not from nought to less than two pi.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2007 (AC1021), which has no such variable.</exception>
        public double LoftAng1
        {
            get => Real("$LOFTANG1", 40, Math.PI / 2.0);
            set => PutLoftAngle("$LOFTANG1", value, LoftAng1);
        }

        /// <summary>Gets or sets the draft angle of a loft through its last section (<c>$LOFTANG2</c>), as AutoCAD's <c>LoftAng2</c>, in radians: nought to less than two pi, as AutoCAD takes 0 to less than 360 degrees; a quarter turn where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the angle set is not from nought to less than two pi.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2007 (AC1021), which has no such variable.</exception>
        public double LoftAng2
        {
            get => Real("$LOFTANG2", 40, Math.PI / 2.0);
            set => PutLoftAngle("$LOFTANG2", value, LoftAng2);
        }

        /// <summary>Gets or sets how far the draft angle of a loft reaches from its first section (<c>$LOFTMAG1</c>), as AutoCAD's <c>LoftMag1</c>: 0 to 10; nought where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not from 0 to 10.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2007 (AC1021), which has no such variable.</exception>
        public double LoftMag1
        {
            get => Real("$LOFTMAG1", 40, 0.0);
            set => PutLoftMagnitude("$LOFTMAG1", value, LoftMag1);
        }

        /// <summary>Gets or sets how far the draft angle of a loft reaches from its last section (<c>$LOFTMAG2</c>), as AutoCAD's <c>LoftMag2</c>: 0 to 10; nought where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not from 0 to 10.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2007 (AC1021), which has no such variable.</exception>
        public double LoftMag2
        {
            get => Real("$LOFTMAG2", 40, 0.0);
            set => PutLoftMagnitude("$LOFTMAG2", value, LoftMag2);
        }

        /// <summary>Gets or sets the shape of a lofted body (<c>$LOFTPARAM</c>), as AutoCAD's <c>LoftParam</c>: a sum of bits, 1 fewest twists, 2 the sections' directions lined up, 4 simple surfaces, 8 closed; 7 where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not from 0 to 15.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2007 (AC1021), which has no such variable.</exception>
        public int LoftParam
        {
            get => Whole("$LOFTPARAM", 70, 7);
            set => PutWhole("$LOFTPARAM", 70, value, LoftParam, 0, 15, 1021);
        }

        /// <summary>Gets or sets how a loft passes through its sections (<c>$LOFTNORMALS</c>), as AutoCAD's <c>LoftNormals</c>: 0 ruled, 1 smooth, 2 to 6 square to the first, last, both or all sections or by draft angles; 1 where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not from 0 to 6.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2007 (AC1021), which has no such variable.</exception>
        public int LoftNormals
        {
            get => Whole("$LOFTNORMALS", 280, 1);
            set => PutWhole("$LOFTNORMALS", 280, value, LoftNormals, 0, 6, 1021);
        }

        /// <summary>Gets or sets the latitude of the drawing's place (<c>$LATITUDE</c>), as AutoCAD's <c>Latitude</c>, in degrees north: -90 to 90; 37.795, San Francisco, where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the latitude set is not from -90 to 90.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2007 (AC1021), which has no such variable.</exception>
        public double Latitude
        {
            get => Real("$LATITUDE", 40, 37.795);

            set
            {
                if (!Same(value, Latitude) && !(value >= -90.0 && value <= 90.0))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "A latitude is from -90 to 90 degrees.");
                }

                PutReal("$LATITUDE", 40, value, Latitude, Bound.Finite, 1021);
            }
        }

        /// <summary>Gets or sets the longitude of the drawing's place (<c>$LONGITUDE</c>), as AutoCAD's <c>Longitude</c>, in degrees east: -180 to 180; -122.394, San Francisco, where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the longitude set is not from -180 to 180.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2007 (AC1021), which has no such variable.</exception>
        public double Longitude
        {
            get => Real("$LONGITUDE", 40, -122.394);

            set
            {
                if (!Same(value, Longitude) && !(value >= -180.0 && value <= 180.0))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "A longitude is from -180 to 180 degrees.");
                }

                PutReal("$LONGITUDE", 40, value, Longitude, Bound.Finite, 1021);
            }
        }

        /// <summary>Gets or sets the angle between the drawing's Y axis and north (<c>$NORTHDIRECTION</c>), as AutoCAD's <c>NorthDirection</c>, in radians as the header holds it; nought where it gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the angle set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2007 (AC1021), which has no such variable.</exception>
        public double NorthDirection
        {
            get => Real("$NORTHDIRECTION", 40, 0.0);
            set => PutReal("$NORTHDIRECTION", 40, value, NorthDirection, Bound.Finite, 1021);
        }

        /// <summary>Gets or sets whether the glyphs of lights are shown (<c>$LIGHTGLYPHDISPLAY</c>), as AutoCAD's <c>LightGlyphDisplay</c>: 0 not, 1 they are; 1 where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not 0 or 1.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2007 (AC1021), which has no such variable.</exception>
        public int LightGlyphDisplay
        {
            get => Whole("$LIGHTGLYPHDISPLAY", 280, 1);
            set => PutWhole("$LIGHTGLYPHDISPLAY", 280, value, LightGlyphDisplay, 0, 1, 1021);
        }

        /// <summary>Gets or sets whether the lighting of model space is that of the layouts (<c>$TILEMODELIGHTSYNCH</c>), as AutoCAD's <c>TileModeLightSynch</c>: 0 not, 1 it is; 1 where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not 0 or 1.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2007 (AC1021), which has no such variable.</exception>
        public int TileModeLightSynch
        {
            get => Whole("$TILEMODELIGHTSYNCH", 280, 1);
            set => PutWhole("$TILEMODELIGHTSYNCH", 280, value, TileModeLightSynch, 0, 1, 1021);
        }

        /// <summary>Gets or sets whether new bodies keep the history of how they were made (<c>$SOLIDHIST</c>), as AutoCAD's <c>SolidHist</c>: 0 not, 1 they do; 0 where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not 0 or 1.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2007 (AC1021), which has no such variable.</exception>
        public int SolidHist
        {
            get => Whole("$SOLIDHIST", 280, 0);
            set => PutWhole("$SOLIDHIST", 280, value, SolidHist, 0, 1, 1021);
        }

        /// <summary>Gets or sets whether the history of bodies is shown (<c>$SHOWHIST</c>), as AutoCAD's <c>ShowHist</c>: 0 never, 1 as each body says, 2 always; 1 where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not from 0 to 2.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2007 (AC1021), which has no such variable.</exception>
        public int ShowHist
        {
            get => Whole("$SHOWHIST", 280, 1);
            set => PutWhole("$SHOWHIST", 280, value, ShowHist, 0, 2, 1021);
        }

        /// <summary>Gets or sets whether the frames of DWF underlays are shown and plotted (<c>$DWFFRAME</c>), as AutoCAD's <c>DwfFrame</c>: 0 hidden, 1 shown and plotted, 2 shown; 2 where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not from 0 to 2.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2007 (AC1021), which has no such variable.</exception>
        public int DwfFrame
        {
            get => Whole("$DWFFRAME", 280, 2);
            set => PutWhole("$DWFFRAME", 280, value, DwfFrame, 0, 2, 1021);
        }

        /// <summary>Gets or sets whether the frames of DGN underlays are shown and plotted (<c>$DGNFRAME</c>), as AutoCAD's <c>DgnFrame</c>: 0 hidden, 1 shown and plotted, 2 shown; 2 where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not from 0 to 2.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2007 (AC1021), which has no such variable.</exception>
        public int DgnFrame
        {
            get => Whole("$DGNFRAME", 280, 2);
            set => PutWhole("$DGNFRAME", 280, value, DgnFrame, 0, 2, 1021);
        }

        /// <summary>Gets or sets which shadows new entities cast and receive (<c>$CSHADOW</c>), as AutoCAD's <c>Cshadow</c>: 0 both, 1 cast, 2 received, 3 none; 0 where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not from 0 to 3.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2007 (AC1021), which has no such variable.</exception>
        public int Cshadow
        {
            get => Whole("$CSHADOW", 280, 0);
            set => PutWhole("$CSHADOW", 280, value, Cshadow, 0, 3, 1021);
        }

        /// <summary>Gets or sets the height of the plane shadows fall on when the ground casts them (<c>$SHADOWPLANELOCATION</c>), as AutoCAD's <c>ShadowPlaneLocation</c>, in drawing units; nought where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the height set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2007 (AC1021), which has no such variable.</exception>
        public double ShadowPlaneLocation
        {
            get => Real("$SHADOWPLANELOCATION", 40, 0.0);
            set => PutReal("$SHADOWPLANELOCATION", 40, value, ShadowPlaneLocation, Bound.Finite, 1021);
        }

        /// <summary>
        /// Gets or sets the colour the bodies where two interfere are drawn in (<c>$INTERFERECOLOR</c>), as AutoCAD's
        /// <c>Interferecolor</c>: BYLAYER where the header gives none.
        /// </summary>
        /// <value>Set, BYLAYER, BYBLOCK or a colour of the index: the header holds an index alone.</value>
        /// <exception cref="ArgumentNullException">Thrown when the colour set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the colour set is a true colour or another the header does not hold.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2007 (AC1021), which has no such variable.</exception>
        public DxfColor Interferecolor
        {
            get => DxfColor.OfIndex(Whole("$INTERFERECOLOR", 62, 256));

            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                if (value.ColorMethod != DxfColorMethod.ByLayer && value.ColorMethod != DxfColorMethod.ByBlock && value.ColorMethod != DxfColorMethod.ByAci)
                {
                    throw new ArgumentException("The header holds the colour of interference as an index: by layer, by block or 1 to 255.", nameof(value));
                }

                if (!value.Equals(Interferecolor))
                {
                    Since(1021, "$INTERFERECOLOR");
                    Set("$INTERFERECOLOR", 62, value.ColorIndex.ToString(CultureInfo.InvariantCulture));
                }
            }
        }

        /// <summary>
        /// Gets or sets the material new entities are given (<c>$CMATERIAL</c>, a handle), as AutoCAD's <c>Cmaterial</c> gives
        /// its id: the MATERIAL of the drawing's dictionary of materials (<see cref="MaterialDictionary"/>), ByLayer in a new
        /// drawing; null where the header names none the drawing has.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the material set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the material set is not an entry of the drawing's dictionary of materials.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2007 (AC1021), which has no such variable.</exception>
        public DxfDBObject Cmaterial
        {
            get => Pointed("$CMATERIAL", 347);
            set => PutPointed("$CMATERIAL", 347, value, Cmaterial, MaterialDictionary, "materials (ACAD_MATERIAL)", 1021);
        }

        /// <summary>
        /// Gets or sets the visual style an object is shown in while it is dragged (<c>$DRAGVS</c>, a handle), as AutoCAD's
        /// <c>dragvs</c> gives its id: a VISUALSTYLE of the drawing's dictionary of visual styles
        /// (<see cref="VisualStyleDictionary"/>); null where the header names none the drawing has.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the style set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the style set is not an entry of the drawing's dictionary of visual styles.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2007 (AC1021), which has no such variable.</exception>
        public DxfDBObject dragvs
        {
            get => Pointed("$DRAGVS", 349);
            set => PutPointed("$DRAGVS", 349, value, dragvs, VisualStyleDictionary, "visual styles (ACAD_VISUALSTYLE)", 1021);
        }

        /// <summary>Gets or sets the visual style the bodies where two interfere are shown in (<c>$INTERFEREOBJVS</c>, a handle), as AutoCAD's <c>Interfereobjvs</c> gives its id: a VISUALSTYLE of <see cref="VisualStyleDictionary"/>; null where the header names none the drawing has.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the style set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the style set is not an entry of the drawing's dictionary of visual styles.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2007 (AC1021), which has no such variable.</exception>
        public DxfDBObject Interfereobjvs
        {
            get => Pointed("$INTERFEREOBJVS", 345);
            set => PutPointed("$INTERFEREOBJVS", 345, value, Interfereobjvs, VisualStyleDictionary, "visual styles (ACAD_VISUALSTYLE)", 1021);
        }

        /// <summary>Gets or sets the visual style of the viewport while interference is checked (<c>$INTERFEREVPVS</c>, a handle), as AutoCAD's <c>Interferevpvs</c> gives its id: a VISUALSTYLE of <see cref="VisualStyleDictionary"/>; null where the header names none the drawing has.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the style set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the style set is not an entry of the drawing's dictionary of visual styles.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2007 (AC1021), which has no such variable.</exception>
        public DxfDBObject Interferevpvs
        {
            get => Pointed("$INTERFEREVPVS", 346);
            set => PutPointed("$INTERFEREVPVS", 346, value, Interferevpvs, VisualStyleDictionary, "visual styles (ACAD_VISUALSTYLE)", 1021);
        }

        /// <summary>
        /// Gets the names of the drawing's visual styles, as AutoCAD's <c>GetVisualStyleList</c>: the entries of its dictionary of
        /// visual styles (ACAD_VISUALSTYLE) in the order the dictionary holds them, <c>2dWireframe</c>, <c>Conceptual</c> and
        /// the rest in a drawing of AutoCAD; empty where the drawing has none.
        /// </summary>
        /// <returns>The names, a new list.</returns>
        public IReadOnlyList<string> GetVisualStyleList()
        {
            var names = new List<string>();
            DxfDBDictionary styles = VisualStyleDictionary;

            if (styles != null)
            {
                foreach (DxfDBDictionaryEntry entry in styles)
                {
                    names.Add(entry.Key);
                }
            }

            return names.AsReadOnly();
        }

        private void PutLoftMagnitude(string name, double value, double now)
        {
            if (!Same(value, now) && !(value >= 0.0 && value <= 10.0))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, name.Substring(1) + " is from 0 to 10.");
            }

            PutReal(name, 40, value, now, Bound.Finite, 1021);
        }

        private void PutLoftAngle(string name, double value, double now)
        {
            if (!Same(value, now) && !(value >= 0.0 && value < 2.0 * Math.PI))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, name.Substring(1) + " is from nought to less than two pi radians.");
            }

            PutReal(name, 40, value, now, Bound.Finite, 1021);
        }
    }
}
