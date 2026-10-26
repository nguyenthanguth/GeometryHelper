using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The variables of the header AutoCAD's <c>Database</c> has as properties, read and set by name: each read from the
    /// drawings of R12, R2000 and R2018 ezdxf 1.4.5 wrote, the value of ezdxf's new drawing where the header gives none, set,
    /// saved and read again, refused where the file's version has no such variable, and set to the value it has without a
    /// byte changing. ezdxf 1.4.5 read every value set back, its recover and audit finding nothing.
    /// </summary>
    public class DxfDatabaseHeaderVariablesTests
    {
        private const string NoGuid = "{00000000-0000-0000-0000-000000000000}";

        private static readonly string[] Drawings = { DxfFileEzdxfDrawings.R12, DxfFileEzdxfDrawings.R2000, DxfFileEzdxfDrawings.R2018 };

        private static readonly Row[] Rows =
        {
            Of("$EXTNAMES", 290, 1015, db => db.AllowExtendedNames, (db, v) => db.AllowExtendedNames = v, true, false),
            Of("$ATTMODE", 70, 1009, db => db.Attmode, (db, v) => db.Attmode = v, 1, 2),
            Of("$CELTSCALE", 40, 1015, db => db.Celtscale, (db, v) => db.Celtscale = v, 1.0, 0.5),
            Of("$CHAMFERA", 40, 1009, db => db.Chamfera, (db, v) => db.Chamfera = v, 0.0, 10.0),
            Of("$CHAMFERB", 40, 1009, db => db.Chamferb, (db, v) => db.Chamferb = v, 0.0, 5.0),
            Of("$CHAMFERC", 40, 1015, db => db.Chamferc, (db, v) => db.Chamferc = v, 0.0, 12.5),
            Of("$CHAMFERD", 40, 1015, db => db.Chamferd, (db, v) => db.Chamferd = v, 0.0, 0.5),
            Of("$CMLJUST", 70, 1015, db => db.Cmljust, (db, v) => db.Cmljust = v, 0, 1),
            Of("$CMLSCALE", 40, 1015, db => db.Cmlscale, (db, v) => db.Cmlscale = v, 20.0, -10.0),
            Of("$DISPSILH", 70, 1015, db => db.DispSilh, (db, v) => db.DispSilh = v, false, true),
            Of("$ELEVATION", 40, 1009, db => db.Elevation, (db, v) => db.Elevation = v, 0.0, 300.0),
            Of("$PELEVATION", 40, 1009, db => db.Pelevation, (db, v) => db.Pelevation = v, 0.0, -5.0),
            Of("$THICKNESS", 40, 1009, db => db.Thickness, (db, v) => db.Thickness = v, 0.0, 30.0),
            Of("$ENDCAPS", 280, 1015, db => db.EndCaps, (db, v) => db.EndCaps = v, DxfEndCap.None, DxfEndCap.Round),
            Of("$JOINSTYLE", 280, 1015, db => db.JoinStyle, (db, v) => db.JoinStyle = v, DxfJoinStyle.StyleNone, DxfJoinStyle.StyleFlat),
            Of("$FILLETRAD", 40, 1009, db => db.Filletrad, (db, v) => db.Filletrad = v, 10.0, 2.5),
            Of("$FINGERPRINTGUID", 2, 1015, db => db.FingerprintGuid, (db, v) => db.FingerprintGuid = v, NoGuid, "{11111111-2222-3333-4444-555555555555}", written: false),
            Of("$VERSIONGUID", 2, 1015, db => db.VersionGuid, (db, v) => db.VersionGuid = v, NoGuid, "{66666666-7777-8888-9999-AAAAAAAAAAAA}", written: false),
            Of("$HYPERLINKBASE", 1, 1015, db => db.HyperlinkBase, (db, v) => db.HyperlinkBase = v, string.Empty, "https://example.com/plans/"),
            Of("$STYLESHEET", 1, 1015, db => db.StyleSheet, (db, v) => db.StyleSheet = v, string.Empty, "monochrome.ctb"),
            Of("$PROJECTNAME", 1, 1018, db => db.ProjectName, (db, v) => db.ProjectName = v, string.Empty, "Bridge B-12"),
            Of("$LIMCHECK", 70, 1009, db => db.Limcheck, (db, v) => db.Limcheck = v, false, true),
            Of("$PLIMCHECK", 70, 1009, db => db.Plimcheck, (db, v) => db.Plimcheck = v, false, true),
            Of("$PLIMMIN", 10, 1009, db => db.Plimmin, (db, v) => db.Plimmin = v, new GeoPoint2(0.0, 0.0), new GeoPoint2(-10.0, -20.0)),
            Of("$PLIMMAX", 10, 1009, db => db.Plimmax, (db, v) => db.Plimmax = v, new GeoPoint2(420.0, 297.0), new GeoPoint2(841.0, 594.0)),
            Of("$PEXTMIN", 10, 1009, db => db.Pextmin, (db, v) => db.Pextmin = v, new GeoPoint3(0.0, 0.0, 0.0), new GeoPoint3(-1.0, -2.0, -3.0), written: false),
            Of("$PEXTMAX", 10, 1009, db => db.Pextmax, (db, v) => db.Pextmax = v, new GeoPoint3(0.0, 0.0, 0.0), new GeoPoint3(420.0, 297.0, 0.0), written: false),
            Of("$PINSBASE", 10, 1015, db => db.Pinsbase, (db, v) => db.Pinsbase = v, new GeoPoint3(0.0, 0.0, 0.0), new GeoPoint3(1.0, 2.0, 3.0)),
            Of("$PLINEGEN", 70, 1009, db => db.Plinegen, (db, v) => db.Plinegen = v, false, true),
            Of("$PLINEWID", 40, 1009, db => db.Plinewid, (db, v) => db.Plinewid = v, 0.0, 0.7),
            Of("$TRACEWID", 40, 1009, db => db.Tracewid, (db, v) => db.Tracewid = v, 1.0, 0.5),
            Of("$PSLTSCALE", 70, 1009, db => db.Psltscale, (db, v) => db.Psltscale = v, true, false),
            Of("$QTEXTMODE", 70, 1009, db => db.Qtextmode, (db, v) => db.Qtextmode = v, false, true),
            Of("$REGENMODE", 70, 1009, db => db.Regenmode, (db, v) => db.Regenmode = v, true, false),
            Of("$SKETCHINC", 40, 1009, db => db.Sketchinc, (db, v) => db.Sketchinc = v, 1.0, 0.25),
            Of("$SKPOLY", 70, 1009, db => db.Skpoly, (db, v) => db.Skpoly = v, false, true),
            Of("$SPLFRAME", 70, 1009, db => db.Splframe, (db, v) => db.Splframe = v, false, true),
            Of("$SURFTAB1", 70, 1009, db => db.Surftab1, (db, v) => db.Surftab1 = v, 6, 24),
            Of("$SURFTAB2", 70, 1009, db => db.Surftab2, (db, v) => db.Surftab2 = v, 6, 12),
            Of("$TILEMODE", 70, 1009, db => db.TileMode, (db, v) => db.TileMode = v, true, false),
            Of("$TREEDEPTH", 70, 1015, db => db.Treedepth, (db, v) => db.Treedepth = v, 3020, 2020),
            Of("$UNITMODE", 70, 1009, db => db.Unitmode, (db, v) => db.Unitmode = v, 0, 1),
            Of("$USERI1", 70, 1009, db => db.Useri1, (db, v) => db.Useri1 = v, 0, 11),
            Of("$USERI2", 70, 1009, db => db.Useri2, (db, v) => db.Useri2 = v, 0, 12),
            Of("$USERI3", 70, 1009, db => db.Useri3, (db, v) => db.Useri3 = v, 0, 13),
            Of("$USERI4", 70, 1009, db => db.Useri4, (db, v) => db.Useri4 = v, 0, 14),
            Of("$USERI5", 70, 1009, db => db.Useri5, (db, v) => db.Useri5 = v, 0, -15),
            Of("$USERR1", 40, 1009, db => db.Userr1, (db, v) => db.Userr1 = v, 0.0, 1.5),
            Of("$USERR2", 40, 1009, db => db.Userr2, (db, v) => db.Userr2 = v, 0.0, 2.5),
            Of("$USERR3", 40, 1009, db => db.Userr3, (db, v) => db.Userr3 = v, 0.0, 0.1),
            Of("$USERR4", 40, 1009, db => db.Userr4, (db, v) => db.Userr4 = v, 0.0, 1E-7),
            Of("$USERR5", 40, 1009, db => db.Userr5, (db, v) => db.Userr5 = v, 0.0, -12345.678),
            Of("$PSVPSCALE", 40, 1015, db => db.ViewportScaleDefault, (db, v) => db.ViewportScaleDefault = v, 0.0, 0.02),
            Of("$VISRETAIN", 70, 1009, db => db.Visretain, (db, v) => db.Visretain = v, true, false),
            Of("$WORLDVIEW", 70, 1009, db => db.Worldview, (db, v) => db.Worldview = v, true, false),
            Of("$MAXACTVP", 70, 1009, db => db.Maxactvp, (db, v) => db.Maxactvp = v, 64, 16),
            Of("$MEASUREMENT", 70, 1015, db => db.Measurement, (db, v) => db.Measurement = v, DxfMeasurementValue.Metric, DxfMeasurementValue.English),
            Of("$OLESTARTUP", 290, 1015, db => db.OleStartUp, (db, v) => db.OleStartUp = v, false, true),
            Of("$XEDIT", 290, 1015, db => db.XrefEditEnabled, (db, v) => db.XrefEditEnabled = v, true, false),
            Of("$SHADEDGE", 70, 1009, db => db.Shadedge, (db, v) => db.Shadedge = v, 3, 1),
            Of("$SHADEDIF", 70, 1009, db => db.Shadedif, (db, v) => db.Shadedif = v, 70, 40),
            Of("$SORTENTS", 280, 1018, db => db.SortEnts, (db, v) => db.SortEnts = v, 127, 96),
            Of("$INDEXCTL", 280, 1018, db => db.Indexctl, (db, v) => db.Indexctl = v, 0, 3),
            Of("$HIDETEXT", 280, 1018, db => db.HideText, (db, v) => db.HideText = v, 1, 0),
            Of("$XCLIPFRAME", 280, 1018, db => db.XclipFrame, (db, v) => db.XclipFrame = v, 1, 2),
            Of("$HALOGAP", 280, 1018, db => db.HaloGap, (db, v) => db.HaloGap = v, 0, 5),
            Of("$OBSCOLOR", 70, 1018, db => db.ObscuredColor, (db, v) => db.ObscuredColor = v, 257, 8),
            Of("$OBSLTYPE", 280, 1018, db => db.ObscuredLineType, (db, v) => db.ObscuredLineType = v, 0, 3),
            Of("$INTERSECTIONDISPLAY", 280, 1018, db => db.IntersectDisplay, (db, v) => db.IntersectDisplay = v, 0, 1),
            Of("$INTERSECTIONCOLOR", 70, 1018, db => db.IntersectColor, (db, v) => db.IntersectColor = v, 257, 1),
            Of("$CAMERADISPLAY", 290, 1021, db => db.CameraDisplay, (db, v) => db.CameraDisplay = v, false, true),
            Of("$CAMERAHEIGHT", 40, 1021, db => db.CameraHeight, (db, v) => db.CameraHeight = v, 0.0, 1700.0),
            Of("$LENSLENGTH", 40, 1021, db => db.LensLength, (db, v) => db.LensLength = v, 50.0, 35.0),
            Of("$STEPSIZE", 40, 1021, db => db.StepSize, (db, v) => db.StepSize = v, 100.0, 600.0),
            Of("$STEPSPERSEC", 40, 1021, db => db.StepsPerSec, (db, v) => db.StepsPerSec = v, 24.0, 2.0),
            Of("$PSOLHEIGHT", 40, 1021, db => db.PsolHeight, (db, v) => db.PsolHeight = v, 80.0, 100.0, written: false),
            Of("$PSOLWIDTH", 40, 1021, db => db.PsolWidth, (db, v) => db.PsolWidth = v, 5.0, 10.0, written: false),
            Of("$LOFTANG1", 40, 1021, db => db.LoftAng1, (db, v) => db.LoftAng1 = v, Math.PI / 2.0, 1.0, written: false),
            Of("$LOFTANG2", 40, 1021, db => db.LoftAng2, (db, v) => db.LoftAng2 = v, Math.PI / 2.0, 0.5, written: false),
            Of("$LOFTMAG1", 40, 1021, db => db.LoftMag1, (db, v) => db.LoftMag1 = v, 0.0, 2.0),
            Of("$LOFTMAG2", 40, 1021, db => db.LoftMag2, (db, v) => db.LoftMag2 = v, 0.0, 3.0),
            Of("$LOFTPARAM", 70, 1021, db => db.LoftParam, (db, v) => db.LoftParam = v, 7, 15),
            Of("$LOFTNORMALS", 280, 1021, db => db.LoftNormals, (db, v) => db.LoftNormals = v, 1, 6),
            Of("$LATITUDE", 40, 1021, db => db.Latitude, (db, v) => db.Latitude = v, 37.795, 21.0285),
            Of("$LONGITUDE", 40, 1021, db => db.Longitude, (db, v) => db.Longitude = v, -122.394, 105.8542),
            Of("$NORTHDIRECTION", 40, 1021, db => db.NorthDirection, (db, v) => db.NorthDirection = v, 0.0, 0.25),
            Of("$LIGHTGLYPHDISPLAY", 280, 1021, db => db.LightGlyphDisplay, (db, v) => db.LightGlyphDisplay = v, 1, 0),
            Of("$TILEMODELIGHTSYNCH", 280, 1021, db => db.TileModeLightSynch, (db, v) => db.TileModeLightSynch = v, 1, 0),
            Of("$SOLIDHIST", 280, 1021, db => db.SolidHist, (db, v) => db.SolidHist = v, 0, 1),
            Of("$SHOWHIST", 280, 1021, db => db.ShowHist, (db, v) => db.ShowHist = v, 1, 2),
            Of("$DWFFRAME", 280, 1021, db => db.DwfFrame, (db, v) => db.DwfFrame = v, 2, 0),
            Of("$DGNFRAME", 280, 1021, db => db.DgnFrame, (db, v) => db.DgnFrame = v, 2, 1),
            Of("$CSHADOW", 280, 1021, db => db.Cshadow, (db, v) => db.Cshadow = v, 0, 3),
            Of("$SHADOWPLANELOCATION", 40, 1021, db => db.ShadowPlaneLocation, (db, v) => db.ShadowPlaneLocation = v, 0.0, -100.0),
            Of("$INTERFERECOLOR", 62, 1021, db => db.Interferecolor, (db, v) => db.Interferecolor = v, DxfColor.ByLayer, DxfColor.OfIndex(1)),
            Of("$USRTIMER", 70, 1009, db => db.Usrtimer, (db, v) => db.Usrtimer = v, true, false),
            Of("$TIMEZONE", 70, 1021, db => db.TimeZone, (db, v) => db.TimeZone = v, DxfTimeZone.Pacific, DxfTimeZone.Bangkok),
        };

        [Fact]
        public void EveryVariableOfTheDrawingsOfEzdxf_ReadsAsEzdxfWroteIt()
        {
            foreach (string text in Drawings)
            {
                DxfDatabase db = DxfDatabase.Parse(text);

                foreach (Row row in Rows.Where(r => r.Written))
                {
                    Assert.True(db.GetHeader(row.Variable) != null || !db.File.Supports(row.Since), db.Version + " " + row.Variable + " is not in the drawing.");
                    Assert.Equal(db.Version + " " + row.Variable + " " + Show(row.Fallback), db.Version + " " + row.Variable + " " + Show(row.Get(db)));
                }
            }
        }

        [Fact]
        public void AVariableTheHeaderGivesNone_IsTheValueOfANewDrawingOfEzdxf()
        {
            foreach (string text in Drawings)
            {
                string bare = Rows.Aggregate(text, (t, row) => Without(t, row.Variable));
                DxfDatabase db = DxfDatabase.Parse(bare);

                foreach (Row row in Rows)
                {
                    Assert.Null(db.GetHeader(row.Variable));
                    Assert.Equal(row.Variable + " " + Show(row.Fallback), row.Variable + " " + Show(row.Get(db)));
                }

                Assert.Equal(new GeoVector3(1.0, 0.0, 0.0), DxfDatabase.Parse(Without(Without(bare, "$UCSXDIR"), "$PUCSXDIR")).Ucsxdir);
                Assert.Equal(new GeoVector3(0.0, 1.0, 0.0), DxfDatabase.Parse(Without(bare, "$PUCSYDIR")).Pucsydir);
            }
        }

        [Fact]
        public void EveryVariableSet_IsWrittenInItsGroup_AndReadsBackAsSet()
        {
            foreach (string text in Drawings)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                Row[] held = Rows.Where(r => db.File.Supports(r.Since)).ToArray();

                foreach (Row row in held)
                {
                    row.Set(db);
                }

                string saved = db.ToText();
                OnlyTheseChanged(text, saved);
                DxfDatabase again = DxfDatabase.Parse(saved);

                foreach (Row row in held)
                {
                    Assert.Equal(row.Variable + " " + Show(row.Changed), row.Variable + " " + Show(row.Get(again)));
                    Assert.Equal(row.Code, again.GetHeader(row.Variable)[0].TypeCode);
                }

                // A header that had none of them has each added at its end, and reads the same.
                string bare = held.Aggregate(text, (t, row) => Without(t, row.Variable));
                DxfDatabase added = DxfDatabase.Parse(bare);

                foreach (Row row in held)
                {
                    row.Set(added);
                }

                DxfDatabase addedAgain = DxfDatabase.Parse(added.ToText());

                foreach (Row row in held)
                {
                    Assert.Equal(row.Variable + " " + Show(row.Changed), row.Variable + " " + Show(row.Get(addedAgain)));
                }
            }
        }

        [Fact]
        public void EveryVariableSetToTheValueItHas_ChangesNoByte()
        {
            foreach (string text in Drawings.Concat(Drawings.Select(t => Rows.Aggregate(t, (u, row) => Without(u, row.Variable)))))
            {
                DxfDatabase db = DxfDatabase.Parse(text);

                foreach (Row row in Rows)
                {
                    row.Again(db);
                }

                db.Ucs = new GeoCoordinateSystem3(db.Ucsorg, db.Ucsxdir, db.Ucsydir);
                db.Ucsname = db.Ucsname;
                db.SetWorldUcsBaseOrigin(db.WorldUcsBaseOrigin(DxfOrthographicView.TopView), DxfOrthographicView.TopView);

                if (db.Cmaterial != null)
                {
                    db.Cmaterial = db.Cmaterial;
                }

                Assert.Equal(text, db.ToText());
            }
        }

        [Fact]
        public void AVariableTheFilesVersionHasNot_IsRefused_AndNothingIsWritten()
        {
            foreach (string text in new[] { DxfFileEzdxfDrawings.R12, DxfFileEzdxfDrawings.R2000 })
            {
                DxfDatabase db = DxfDatabase.Parse(text);

                foreach (Row row in Rows.Where(r => !db.File.Supports(r.Since)))
                {
                    Assert.Throws<InvalidOperationException>(() => row.Set(db));
                }

                Assert.Equal(text, db.ToText());
            }

            DxfDatabase r12 = DxfDatabase.Parse(DxfFileEzdxfDrawings.R12);
            Assert.Throws<InvalidOperationException>(() => r12.SetWorldUcsBaseOrigin(new GeoPoint3(1.0, 0.0, 0.0), DxfOrthographicView.BackView));
            Assert.Throws<InvalidOperationException>(() => r12.SetTimeZoneAsUtcOffset(7.0));
            Assert.Equal(DxfFileEzdxfDrawings.R12, r12.ToText());
        }

        [Fact]
        public void AValueAutoCADDoesNotTake_IsRefused_AndNothingIsWritten()
        {
            string text = Styled();
            DxfDatabase db = DxfDatabase.Parse(text);
            var refusals = new Action[]
            {
                () => db.Attmode = 3,
                () => db.Celtscale = 0.0,
                () => db.Chamfera = -1.0,
                () => db.Chamferd = double.NaN,
                () => db.Cmljust = -1,
                () => db.Elevation = double.PositiveInfinity,
                () => db.EndCaps = (DxfEndCap)4,
                () => db.JoinStyle = (DxfJoinStyle)(-1),
                () => db.Filletrad = -0.5,
                () => db.Sketchinc = 0.0,
                () => db.Surftab1 = 1,
                () => db.Surftab2 = 32767,
                () => db.Unitmode = 2,
                () => db.Useri1 = 40000,
                () => db.Maxactvp = 1,
                () => db.Measurement = (DxfMeasurementValue)2,
                () => db.ViewportScaleDefault = -1.0,
                () => db.Shadedge = 4,
                () => db.Shadedif = 101,
                () => db.SortEnts = 128,
                () => db.Indexctl = 4,
                () => db.HideText = 2,
                () => db.XclipFrame = 3,
                () => db.HaloGap = -1,
                () => db.ObscuredColor = 258,
                () => db.ObscuredLineType = 12,
                () => db.IntersectDisplay = 2,
                () => db.IntersectColor = -1,
                () => db.LensLength = 0.0,
                () => db.StepSize = 2E6,
                () => db.StepsPerSec = 31.0,
                () => db.PsolHeight = -1.0,
                () => db.LoftAng1 = 7.0,
                () => db.LoftMag2 = -1.0,
                () => db.LoftParam = 16,
                () => db.LoftNormals = 7,
                () => db.Latitude = 90.5,
                () => db.Longitude = -181.0,
                () => db.LightGlyphDisplay = 2,
                () => db.SolidHist = 2,
                () => db.ShowHist = 3,
                () => db.DwfFrame = 3,
                () => db.Cshadow = 4,
                () => db.TimeZone = (DxfTimeZone)1234,
                () => db.SetTimeZoneAsUtcOffset(2.25),
                () => db.SetTimeZoneAsUtcOffset(double.NaN),
                () => db.WorldUcsBaseOrigin(DxfOrthographicView.NonOrthoView),
                () => db.SetWorldPaperspaceUcsBaseOrigin(new GeoPoint3(1.0, 2.0, 3.0), (DxfOrthographicView)7),
                () => db.TimeZoneOffset((DxfTimeZone)5),
            };

            foreach (Action refusal in refusals)
            {
                Assert.Throws<ArgumentOutOfRangeException>(refusal);
            }

            Assert.Throws<ArgumentNullException>(() => db.HyperlinkBase = null);
            Assert.Throws<ArgumentException>(() => db.ProjectName = "two\nlines");
            Assert.Throws<ArgumentException>(() => db.FingerprintGuid = "not a guid");
            Assert.Throws<ArgumentNullException>(() => db.Interferecolor = null);
            Assert.Throws<ArgumentException>(() => db.Interferecolor = DxfColor.FromRgb(1, 2, 3));
            Assert.Throws<ArgumentException>(() => db.Pinsbase = new GeoPoint3(double.NaN, 0.0, 0.0));
            Assert.Throws<ArgumentException>(() => db.Ucs = default(GeoCoordinateSystem3));
            Assert.Throws<ArgumentException>(() => db.SetWorldUcsBaseOrigin(new GeoPoint3(0.0, double.PositiveInfinity, 0.0), DxfOrthographicView.TopView));
            Assert.Throws<ArgumentException>(() => db.Ucsname = "GH_NONE");
            Assert.Throws<ArgumentNullException>(() => db.Cmaterial = null);
            Assert.Throws<ArgumentException>(() => db.Cmaterial = db.VisualStyleDictionary.First().Value);
            Assert.Throws<ArgumentException>(() => db.dragvs = DxfDatabase.Parse(text).VisualStyleDictionary.First().Value);
            Assert.Throws<ArgumentException>(() => db.CmlstyleID = db.MaterialDictionary.First().Value);
            Assert.Equal(text, db.ToText());
        }

        [Fact]
        public void TheReadOnlyVariables_ReadAsTheHeaderGivesThem()
        {
            DxfDatabase r12 = DxfDatabase.Parse(DxfFileEzdxfDrawings.R12);
            DxfDatabase r2000 = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            DxfDatabase r2018 = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);

            Assert.Equal(".", r12.Menu);
            Assert.Equal(".", r2018.Menu);
            Assert.True(r12.PlotStyleMode);
            Assert.True(r2018.PlotStyleMode);
            Assert.False(DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018.Replace("$PSTYLEMODE\n290\n1\n", "$PSTYLEMODE\n290\n0\n")).PlotStyleMode);

            // $ACADMAINTVER is a group 70 up to R2017 and a group 90 from R2018.
            Assert.Equal(0, r12.MaintenanceReleaseVersion);
            Assert.Equal(6, r2000.MaintenanceReleaseVersion);
            Assert.Equal(4, r2018.MaintenanceReleaseVersion);
            Assert.Equal(4, r2018.LastSavedAsMaintenanceVersion);
            Assert.Equal(4, r2018.OriginalFileMaintenanceVersion);

            Assert.Equal(new DateTime(2026, 10, 23, 14, 30, 30, 0), r2018.Tdcreate);
            Assert.Equal(DateTimeKind.Unspecified, r2018.Tdcreate.Kind);

            // ezdxf writes the polysolid's size in metres; read as written, AutoCAD's millimetres where the header gives none.
            Assert.Equal(0.08, r2018.PsolHeight);
            Assert.Equal(0.005, r2018.PsolWidth);

            // The last day DateTime holds is read whole; only a part of it rounded to midnight after it is past its end.
            string last = DxfFileEzdxfDrawings.R2018.Replace("$TDCREATE\n 40\n2461337.6045138887\n", "$TDCREATE\n 40\n5373484.5\n");
            Assert.Equal(new DateTime(9999, 12, 31, 12, 0, 0), DxfDatabase.Parse(last).Tdcreate);
            Assert.Equal(new DateTime(9999, 12, 31), DxfDatabase.Parse(last.Replace("5373484.5", "5373484.0")).Tdcreate);
            Assert.Equal(default(DateTime), DxfDatabase.Parse(last.Replace("5373484.5", "5373484.9999999995")).Tdcreate);
            Assert.Equal(default(DateTime), DxfDatabase.Parse(last.Replace("5373484.5", "5373485.0")).Tdcreate);
            Assert.Equal(new DateTime(1, 1, 1, 6, 0, 0), DxfDatabase.Parse(last.Replace("5373484.5", "1721426.25")).Tdcreate);
            Assert.Equal(default(DateTime), DxfDatabase.Parse(last.Replace("5373484.5", "1721425.75")).Tdcreate);
            Assert.Equal(new DateTime(2026, 10, 23, 14, 30, 30, 0), r2018.Tdupdate);
            Assert.Equal(new DateTime(2019, 2, 17, 3, 41, 45, 332, DateTimeKind.Utc), r2018.Tducreate);
            Assert.Equal(DateTimeKind.Utc, r2018.Tducreate.Kind);
            Assert.Equal(new DateTime(2019, 2, 17, 3, 42, 22, 847, DateTimeKind.Utc), r2018.Tduupdate);
            Assert.Equal(TimeSpan.Zero, r2018.Tdindwg);
            Assert.Equal(TimeSpan.Zero, r2018.Tdusrtimer);
            Assert.Equal(default(DateTime), r12.Tducreate);

            // 2451545.75 is 1 January 2000 at 18:00; an edit of half a day and an hour's timer.
            string text = DxfFileEzdxfDrawings.R2018.Replace("$TDCREATE\n 40\n2461337.6045138887\n", "$TDCREATE\n 40\n2451545.75\n")
                .Replace("$TDINDWG\n 40\n0.0\n", "$TDINDWG\n 40\n0.5\n").Replace("$TDUSRTIMER\n 40\n0.0\n", "$TDUSRTIMER\n 40\n0.041666666666666664\n");
            DxfDatabase timed = DxfDatabase.Parse(text);
            Assert.Equal(new DateTime(2000, 1, 1, 18, 0, 0), timed.Tdcreate);
            Assert.Equal(TimeSpan.FromHours(12), timed.Tdindwg);
            Assert.Equal(TimeSpan.FromHours(1), timed.Tdusrtimer);
            Assert.Equal(default(DateTime), DxfDatabase.Parse(text.Replace("2451545.75", "1E+300")).Tdcreate);
        }

        [Fact]
        public void ResetTimes_MakesTheDrawingNow_AndItsTimersNought()
        {
            foreach (string text in new[] { DxfFileEzdxfDrawings.R12, DxfFileEzdxfDrawings.R2018 })
            {
                string worked = text.Replace("$TDINDWG\n 40\n0.0\n", "$TDINDWG\n 40\n0.5\n");
                DxfDatabase db = DxfDatabase.Parse(worked);
                DateTime before = DateTime.Now.AddMilliseconds(-1);
                DateTime beforeUtc = DateTime.UtcNow.AddMilliseconds(-1);

                db.ResetTimes();

                DateTime after = DateTime.Now.AddMilliseconds(1);
                DateTime afterUtc = DateTime.UtcNow.AddMilliseconds(1);
                DxfDatabase again = DxfDatabase.Parse(db.ToText());
                Assert.InRange(again.Tdcreate, before, after);
                Assert.InRange(again.Tdupdate, before, after);
                Assert.Equal(TimeSpan.Zero, again.Tdindwg);
                Assert.Equal(TimeSpan.Zero, again.Tdusrtimer);

                if (again.File.Supports(1015))
                {
                    Assert.InRange(again.Tducreate, beforeUtc, afterUtc);
                    Assert.InRange(again.Tduupdate, beforeUtc, afterUtc);
                }
                else
                {
                    Assert.Null(again.GetHeader("$TDUCREATE"));
                    Assert.Null(again.GetHeader("$TDUUPDATE"));
                }
            }
        }

        [Fact]
        public void TheTimeZones_AreAutoCADs_ByTheirOffsets()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);

            Assert.Equal(-8.0, db.TimeZoneOffset(DxfTimeZone.Pacific));
            Assert.Equal(5.75, db.TimeZoneOffset(DxfTimeZone.Kathmandu));
            Assert.Equal(-3.5, db.TimeZoneOffset(DxfTimeZone.Newfoundland));
            Assert.Equal(0.0, db.TimeZoneOffset(DxfTimeZone.GMT));
            Assert.Equal("(UTC-08:00) Pacific", db.TimeZoneDescription(DxfTimeZone.Pacific));
            Assert.Equal("(UTC+05:30) Kolkata", db.TimeZoneDescription(DxfTimeZone.Kolkata));
            Assert.Equal("(UTC-06:00) Central America", db.TimeZoneDescription(DxfTimeZone.CentralAmerica));
            Assert.Equal("(UTC+00:00) UTC", db.TimeZoneDescription(DxfTimeZone.UTC));

            Assert.Equal(DxfTimeZone.Mountain, db.SetTimeZoneAsUtcOffset(-7.0));
            Assert.Equal(DxfTimeZone.Mountain, db.TimeZone);
            Assert.Equal(DxfTimeZone.Kathmandu, db.SetTimeZoneAsUtcOffset(5.75));
            Assert.Equal(DxfTimeZone.Bangkok, db.SetTimeZoneAsUtcOffset(7.0));
            Assert.Equal(DxfTimeZone.UTC, db.SetTimeZoneAsUtcOffset(0.0));
            Assert.Contains("$TIMEZONE\n 70\n0\n", db.ToText(), StringComparison.Ordinal);

            foreach (DxfTimeZone zone in Enum.GetValues(typeof(DxfTimeZone)))
            {
                DxfTimeZone first = db.SetTimeZoneAsUtcOffset(db.TimeZoneOffset(zone));
                Assert.Equal(db.TimeZoneOffset(zone), db.TimeZoneOffset(first));
                Assert.Equal(zone + " 0", zone + " " + ((int)first % 10).ToString(CultureInfo.InvariantCulture));
            }
        }

        [Fact]
        public void TheEdgesOfAutoCADsRanges_AreTaken_AndOnePast_IsRefused()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            double underTwoPi = FromBits(BitConverter.DoubleToInt64Bits(2.0 * Math.PI) - 1);

            Assert.Throws<ArgumentOutOfRangeException>(() => db.LoftAng1 = 2.0 * Math.PI);
            Assert.Throws<ArgumentOutOfRangeException>(() => db.LoftAng2 = 2.0 * Math.PI);
            Assert.Throws<ArgumentOutOfRangeException>(() => db.LoftMag1 = 10.000001);
            Assert.Throws<ArgumentOutOfRangeException>(() => db.LoftMag2 = 11.0);
            Assert.Throws<ArgumentOutOfRangeException>(() => db.HaloGap = 101);
            Assert.Equal(DxfFileEzdxfDrawings.R2018, db.ToText());

            db.Surftab1 = 2;
            db.Surftab2 = 32766;
            db.StepSize = 1E-6;
            db.StepsPerSec = 30.0;
            db.LoftAng1 = underTwoPi;
            db.LoftAng2 = 0.0;
            db.LoftMag1 = 10.0;
            db.HaloGap = 100;
            db.Shadedif = 100;
            db.SortEnts = 0;
            db.ObscuredColor = 257;
            db.Maxactvp = 2;
            db.Latitude = -90.0;
            db.Longitude = 180.0;

            DxfDatabase again = DxfDatabase.Parse(db.ToText());
            Assert.Equal(new[] { 2, 32766, 100, 100, 0, 257, 2 }, new[] { again.Surftab1, again.Surftab2, again.HaloGap, again.Shadedif, again.SortEnts, again.ObscuredColor, again.Maxactvp });
            Assert.Equal(new[] { 1E-6, 30.0, underTwoPi, 0.0, 10.0, -90.0, 180.0 }, new[] { again.StepSize, again.StepsPerSec, again.LoftAng1, again.LoftAng2, again.LoftMag1, again.Latitude, again.Longitude });
        }

        [Fact]
        public void TheTextVariables_AreReadAsWritten_AndAGuidIsWrittenAsAutoCADWritesIt()
        {
            string text = DxfFileEzdxfDrawings.R2018.Replace("$HYPERLINKBASE\n  1\n\n", "$HYPERLINKBASE\n  1\n  ../plans/  \n")
                .Replace("$PROJECTNAME\n  1\n\n", "$PROJECTNAME\n  1\nBridge B-12 \n");
            DxfDatabase db = DxfDatabase.Parse(text);

            Assert.Equal("  ../plans/  ", db.HyperlinkBase);
            Assert.Equal("Bridge B-12 ", db.ProjectName);
            db.HyperlinkBase = db.HyperlinkBase;
            Assert.Equal(text, db.ToText());

            db.StyleSheet = "monochrome.ctb ";
            db.FingerprintGuid = "7021885a-675a-47a4-aa57-30a391b5281b";
            db.VersionGuid = "(11111111-2222-3333-4444-55555555555a)";
            DxfDatabase again = DxfDatabase.Parse(db.ToText());
            Assert.Equal("monochrome.ctb ", again.StyleSheet);
            Assert.Equal("{7021885A-675A-47A4-AA57-30A391B5281B}", again.FingerprintGuid);
            Assert.Equal("{11111111-2222-3333-4444-55555555555A}", again.VersionGuid);

            // The same GUID, written otherwise, is the value the variable has.
            string saved = again.ToText();
            again.FingerprintGuid = "{7021885a-675a-47a4-aa57-30a391b5281b}";
            Assert.Equal(saved, again.ToText());
        }

        [Fact]
        public void ApproxNumObjects_CountsWhatGetObjectFinds()
        {
            foreach (string text in new[] { DxfFileEzdxfDrawings.R2000, Styled() })
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                ulong seed = ulong.Parse(db.Handseed, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                int found = 0;

                for (ulong h = 1; h < seed + 0x200; h++)
                {
                    found += db.TryGetObject(h.ToString("X", CultureInfo.InvariantCulture), out _) ? 1 : 0;
                }

                Assert.Equal(found, db.ApproxNumObjects);
                Assert.InRange(found, 100, int.MaxValue);

                var line = new DxfLine(new GeoPoint3(0.0, 0.0, 0.0), new GeoPoint3(100.0, 0.0, 0.0));
                db.ModelSpace.AppendEntity(line);
                Assert.Equal(found + 1, db.ApproxNumObjects);
                db.NamedObjectsDictionary.SetAt("GH_COUNT", new DxfXrecord());
                Assert.Equal(found + 2, db.ApproxNumObjects);
                line.Erase();
                Assert.Equal(found + 1, db.ApproxNumObjects);
            }
        }

        [Fact]
        public void TheLineweights_AreAutoCADs24()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R12);

            Assert.Equal(DxfLineWeight.LineWeight025, db.GetNearestLineWeight(24));
            Assert.Equal(DxfLineWeight.LineWeight025, db.GetNearestLineWeight(25));
            Assert.Equal(DxfLineWeight.LineWeight009, db.GetNearestLineWeight(7));
            Assert.Equal(DxfLineWeight.LineWeight005, db.GetNearestLineWeight(6));
            Assert.Equal(DxfLineWeight.LineWeight211, db.GetNearestLineWeight(300));
            Assert.Equal(DxfLineWeight.LineWeight211, db.GetNearestLineWeight(int.MaxValue));
            Assert.Equal(DxfLineWeight.LineWeight000, db.GetNearestLineWeight(-5));
            Assert.Equal(DxfLineWeight.LineWeight000, db.GetNearestLineWeight(int.MinValue));

            Assert.True(db.IsValidLineWeight(25));
            Assert.True(db.IsValidLineWeight(211));
            Assert.True(db.IsValidLineWeight(-3));
            Assert.False(db.IsValidLineWeight(24));
            Assert.False(db.IsValidLineWeight(-4));
            Assert.Equal(24, Enumerable.Range(0, 300).Count(db.IsValidLineWeight));
        }

        [Fact]
        public void TheFilename_IsThePathTheDrawingWasOpenedFromOrSavedTo()
        {
            string folder = Path.Combine(Path.GetTempPath(), "gh-header-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);

            try
            {
                string path = Path.Combine(folder, "plan.dxf");
                string copy = Path.Combine(folder, "copy.dxf");
                File.WriteAllText(path, DxfFileEzdxfDrawings.R12);

                Assert.Null(DxfDatabase.Parse(DxfFileEzdxfDrawings.R12).Filename);
                Assert.Null(DxfDatabase.Parse(DxfFileEzdxfDrawings.R12).OriginalFileName);

                DxfDatabase db = DxfDatabase.Open(path);
                Assert.Equal(Path.GetFullPath(path), db.Filename);
                Assert.Equal(Path.GetFullPath(path), db.OriginalFileName);

                db.Save(copy);
                Assert.Equal(Path.GetFullPath(copy), db.Filename);
                Assert.Equal(Path.GetFullPath(path), db.OriginalFileName);

                using (var stream = new MemoryStream())
                {
                    db.Save(stream);
                    Assert.Equal(Path.GetFullPath(copy), db.Filename);
                }
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [Fact]
        public void TheUcs_IsWrittenToItsThreeVariables_AndNamedByTheUcsTable()
        {
            const string Table = "  2\nUCS\n  5\n6\n330\n0\n100\nAcDbSymbolTable\n 70\n0\n";
            const string Record = "  0\nUCS\n  5\nF1\n330\n6\n100\nAcDbSymbolTableRecord\n100\nAcDbUCSTableRecord\n  2\nGH_UCS\n 70\n0\n"
                + " 10\n100.0\n 20\n0.0\n 30\n0.0\n 11\n0.0\n 21\n1.0\n 31\n0.0\n 12\n-1.0\n 22\n0.0\n 32\n0.0\n 79\n0\n146\n0.0\n";
            string text = DxfFileEzdxfDrawings.R2018;
            Assert.Contains(Table, text, StringComparison.Ordinal);
            string named = text.Replace(Table, Table.Replace(" 70\n0\n", " 70\n1\n") + Record);
            DxfDatabase db = DxfDatabase.Parse(named);

            Assert.Equal(new GeoPoint3(0.0, 0.0, 0.0), db.Ucsorg);
            Assert.Equal(new GeoVector3(1.0, 0.0, 0.0), db.Ucsxdir);
            Assert.Equal(new GeoVector3(0.0, 1.0, 0.0), db.Ucsydir);
            Assert.Null(db.Ucsname);
            Assert.Null(db.UcsBase);
            Assert.Equal(DxfOrthographicView.NonOrthoView, db.UcsOrthographic);
            Assert.Equal(DxfOrthographicView.NonOrthoView, db.PucsOrthographic);

            // A quarter turn about Z at (100, 0, 0).
            db.Ucs = new GeoCoordinateSystem3(new GeoPoint3(100.0, 0.0, 0.0), new GeoVector3(0.0, 1.0, 0.0), new GeoVector3(-1.0, 0.0, 0.0));
            db.Ucsname = "gh_ucs";
            db.UcsBase = "GH_UCS";
            db.Pucs = new GeoCoordinateSystem3(new GeoPoint3(5.0, 6.0, 7.0), new GeoVector3(0.0, 0.0, 1.0), new GeoVector3(1.0, 0.0, 0.0));
            db.Pucsname = "GH_UCS";
            db.PucsBase = "GH_UCS";
            db.SetWorldUcsBaseOrigin(new GeoPoint3(0.0, 0.0, 300.0), DxfOrthographicView.TopView);
            db.SetWorldPaperspaceUcsBaseOrigin(new GeoPoint3(0.0, -10.0, 0.0), DxfOrthographicView.FrontView);

            string saved = db.ToText();
            OnlyTheseChanged(named, saved);
            const string Written = "$UCSORG\n 10\n100\n 20\n0\n 30\n0\n  9\n$UCSXDIR\n 10\n0\n 20\n1\n 30\n0\n  9\n$UCSYDIR\n 10\n-1\n 20\n0\n 30\n0\n";
            Assert.Equal(Written, saved.Substring(saved.IndexOf("$UCSORG\n", StringComparison.Ordinal), Written.Length));
            DxfDatabase again = DxfDatabase.Parse(saved);
            Assert.Equal(new GeoPoint3(100.0, 0.0, 0.0), again.Ucsorg);
            Assert.Equal(new GeoVector3(0.0, 1.0, 0.0), again.Ucsxdir);
            Assert.Equal(new GeoVector3(-1.0, 0.0, 0.0), again.Ucsydir);
            Assert.Equal(new GeoPoint3(5.0, 6.0, 7.0), again.Pucsorg);
            Assert.Equal(new GeoVector3(0.0, 0.0, 1.0), again.Pucsxdir);
            Assert.Equal(new GeoVector3(1.0, 0.0, 0.0), again.Pucsydir);
            Assert.Equal("gh_ucs", again.Ucsname);
            Assert.Equal("GH_UCS", again.UcsBase);
            Assert.Equal("GH_UCS", again.Pucsname);
            Assert.Equal("GH_UCS", again.PucsBase);
            Assert.Equal(new GeoPoint3(0.0, 0.0, 300.0), again.WorldUcsBaseOrigin(DxfOrthographicView.TopView));
            Assert.Equal(new GeoPoint3(0.0, 0.0, 0.0), again.WorldUcsBaseOrigin(DxfOrthographicView.BottomView));
            Assert.Equal(new GeoPoint3(0.0, -10.0, 0.0), again.WorldPaperspaceUcsBaseOrigin(DxfOrthographicView.FrontView));

            // Named none again: the group 2 is empty, as ezdxf writes a drawing's.
            again.Ucsname = null;
            Assert.Contains("$UCSNAME\n  2\n\n", again.ToText(), StringComparison.Ordinal);
            Assert.Null(DxfDatabase.Parse(again.ToText()).Ucsname);
        }

        [Fact]
        public void TheVariablesThatNameAnObject_GiveItAndSetItByItsHandle()
        {
            string styled = Styled();
            DxfDatabase db = DxfDatabase.Parse(styled);

            Assert.Equal(db.MaterialDictionary.GetAt("ByLayer").Handle, db.Cmaterial.Handle);
            Assert.Equal(db.MLStyleDictionary.GetAt("Standard").Handle, db.CmlstyleID.Handle);
            Assert.Null(db.dragvs);
            Assert.Null(db.Interfereobjvs);
            Assert.Null(db.Interferevpvs);
            Assert.Equal(db.VisualStyleDictionary.Select(e => e.Key), db.GetVisualStyleList());
            Assert.Contains("Conceptual", db.GetVisualStyleList());
            Assert.Empty(DxfDatabase.Parse(DxfFileEzdxfDrawings.R12).GetVisualStyleList());
            Assert.Null(DxfDatabase.Parse(DxfFileEzdxfDrawings.R12).Cmaterial);

            DxfDBObject global = db.MaterialDictionary.GetAt("Global");
            DxfDBObject conceptual = db.VisualStyleDictionary.GetAt("Conceptual");
            DxfDBObject realistic = db.VisualStyleDictionary.GetAt("Realistic");
            db.Cmaterial = global;
            db.CmlstyleID = db.MLStyleDictionary.GetAt("GH");
            db.dragvs = conceptual;
            db.Interfereobjvs = realistic;
            db.Interferevpvs = conceptual;

            string saved = db.ToText();
            OnlyTheseChanged(styled, saved);
            Assert.Contains("$CMATERIAL\n347\n" + global.Handle + "\n", saved, StringComparison.Ordinal);
            Assert.Contains("$CMLSTYLE\n  2\nGH\n", saved, StringComparison.Ordinal);
            Assert.Contains("  9\n$DRAGVS\n349\n" + conceptual.Handle + "\n", saved, StringComparison.Ordinal);
            DxfDatabase again = DxfDatabase.Parse(saved);
            Assert.Equal(global.Handle, again.Cmaterial.Handle);
            Assert.Equal("GH", again.MLStyleDictionary.NameAt(again.CmlstyleID));
            Assert.Equal(conceptual.Handle, again.dragvs.Handle);
            Assert.Equal(realistic.Handle, again.Interfereobjvs.Handle);
            Assert.Equal(conceptual.Handle, again.Interferevpvs.Handle);

            DxfDatabase r2000 = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            Assert.Throws<InvalidOperationException>(() => r2000.Cmaterial = global);
            Assert.Throws<InvalidOperationException>(() => r2000.dragvs = conceptual);
        }

        /// <summary>
        /// The drawing of R2018 with a second multiline style, GH, and two visual styles, Conceptual and Realistic, which ezdxf
        /// does not write.
        /// </summary>
        private static string Styled()
        {
            const string Lines = "  0\nDICTIONARY\n  5\n10\n330\nA\n100\nAcDbDictionary\n281\n1\n  3\nStandard\n350\n22\n";
            const string Visual = "  0\nDICTIONARY\n  5\n16\n330\nA\n100\nAcDbDictionary\n281\n1\n";
            const string Objects = "  0\nMLINESTYLE\n  5\nF2\n330\n10\n100\nAcDbMlineStyle\n  2\nGH\n 70\n0\n  3\n\n 62\n256\n 51\n90.0\n 52\n90.0\n 71\n1\n 49\n0.0\n 62\n256\n  6\nBYLAYER\n"
                + "  0\nVISUALSTYLE\n  5\nF3\n330\n16\n100\nAcDbVisualStyle\n  2\nConceptual\n 70\n9\n291\n1\n"
                + "  0\nVISUALSTYLE\n  5\nF4\n330\n16\n100\nAcDbVisualStyle\n  2\nRealistic\n 70\n8\n291\n1\n";
            string text = DxfFileEzdxfDrawings.R2018;
            Assert.Contains(Lines, text, StringComparison.Ordinal);
            Assert.Contains(Visual, text, StringComparison.Ordinal);
            int end = text.LastIndexOf("  0\nENDSEC\n", StringComparison.Ordinal);
            return (text.Substring(0, end) + Objects + text.Substring(end)).Replace(Lines, Lines + "  3\nGH\n350\nF2\n")
                .Replace(Visual, Visual + "  3\nConceptual\n350\nF3\n  3\nRealistic\n350\nF4\n");
        }

        /// <summary>The text of a drawing without a variable of its header, where it has it.</summary>
        private static string Without(string text, string variable)
        {
            int at = text.IndexOf("  9\n" + variable + "\n", StringComparison.Ordinal);

            if (at < 0)
            {
                return text;
            }

            int nine = text.IndexOf("\n  9\n", at + 4, StringComparison.Ordinal);
            int zero = text.IndexOf("\n  0\n", at + 4, StringComparison.Ordinal);
            int end = (nine < 0 ? zero : Math.Min(nine, zero)) + 1;
            return text.Remove(at, end - at);
        }

        private static string Show(object value)
        {
            switch (value)
            {
                case null:
                    return "null";
                case double d:
                    return d.ToString("R", CultureInfo.InvariantCulture);
                case GeoPoint2 p:
                    return Show(p.X) + "," + Show(p.Y);
                case GeoPoint3 p:
                    return Show(p.X) + "," + Show(p.Y) + "," + Show(p.Z);
                case DxfColor c:
                    return c.ColorMethod + " " + c.ColorIndex.ToString(CultureInfo.InvariantCulture);
                default:
                    return Convert.ToString(value, CultureInfo.InvariantCulture);
            }
        }

        private static Row Of<T>(string variable, int code, int since, Func<DxfDatabase, T> get, Action<DxfDatabase, T> set, T fallback, T changed, bool written = true)
            => new Row
            {
                Variable = variable,
                Code = code,
                Since = since,
                Get = db => get(db),
                Set = db => set(db, changed),
                Again = db => set(db, get(db)),
                Fallback = fallback,
                Changed = changed,
                Written = written,
            };

        /// <summary>A variable of the header: its name and group, the version it came with, and the values it is read and set to.</summary>
        private sealed class Row
        {
            public string Variable { get; set; }

            public int Code { get; set; }

            public int Since { get; set; }

            public Func<DxfDatabase, object> Get { get; set; }

            public Action<DxfDatabase> Set { get; set; }

            public Action<DxfDatabase> Again { get; set; }

            public object Fallback { get; set; }

            public object Changed { get; set; }

            // Whether ezdxf's drawings hold the value a new drawing of ezdxf is given (not a GUID of their own, or the extents of
            // nothing).
            public bool Written { get; set; }
        }
    }
}
