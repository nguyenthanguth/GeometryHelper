using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// The system variables an AutoCAD user types most that SETVAR lacked, each with the range, the refusal and the start
    /// AutoCAD 2021 gives it (checked in its console). The units of the drawing, LUNITS, LUPREC, AUNITS, AUPREC, ANGBASE,
    /// ANGDIR and INSUNITS, are the header's own and what UNITS sets; PLINEWID is the width PLINE starts at and PLINEGEN is
    /// given to the polylines PLINE, RECTANG and POLYGON make; FILLMODE 0 draws solids, wide polylines and solid or gradient
    /// hatches by their outlines; HPNAME, HPSCALE, HPANG, HPLAYER, HPASSOC, HPDOUBLE, HPGAPTOL and HPORIGINMODE are the
    /// settings of the next hatch (<see cref="CadDocument.HatchSettings"/>); DIMLAYER is the layer new dimensions go on;
    /// CIRCLERAD, POLYSIDES and TRIMEXTENDMODE are what CIRCLE, POLYGON, TRIM and EXTEND start with. Those the presenter has no
    /// behaviour for, CMDECHO, EXPERT, HIGHLIGHT, PICKAUTO, PICKDRAG, OSNAPZ, ARRAYASSOCIATIVITY, ELEVATION, THICKNESS, MEASUREMENT,
    /// UNITMODE, LIMCHECK, SPLFRAME, SPLINETYPE, SPLINESEGS, QTEXTMODE, ATTMODE and USERI1 to USERI5 and USERR1 to USERR5, keep
    /// their value where AutoCAD does, in the drawing's header or for the session. DISTANCE, AREA and PERIMETER give the last
    /// measure DIST and AREA made, and LASTPOINT the last point given, in the user coordinate system. What AutoLISP routines
    /// ask of the program: PRODUCT, which is "GeometryHelper.Cad" where AutoCAD gives "AutoCAD", ACADVER "24.0", the version of
    /// AutoCAD whose behaviour the presenter follows, CMDACTIVE and CMDNAMES for what runs, LISPINIT, BLIPMODE and MENUECHO, and
    /// the axes of the user coordinate system, UCSXDIR and UCSYDIR.
    /// </summary>
    internal static partial class CadCommands
    {
        private static void AddMoreVariablesTo(CadCommandRegistry registry)
        {
            // The units of the drawing: how lengths and angles are shown, the direction of angle nought and the unit it is in.
            registry.AddVariable(HeaderWhole("LUNITS", 1, 5, d => d.Lunits, (d, v) => d.Lunits = v));
            registry.AddVariable(HeaderWhole("LUPREC", 0, 8, d => d.Luprec, (d, v) => d.Luprec = v));
            registry.AddVariable(HeaderWhole("AUNITS", 0, 4, d => d.Aunits, (d, v) => d.Aunits = v));
            registry.AddVariable(HeaderWhole("AUPREC", 0, 8, d => d.Auprec, (d, v) => d.Auprec = v));
            registry.AddVariable(
                CadSystemVariable.Angle(
                    "ANGBASE",
                    true,
                    s => s.Document.Database.Angbase,
                    (c, v) => c.Database.Angbase = v,
                    "$ANGBASE",
                    true
                )
            );
            registry.AddVariable(
                Flag("ANGDIR", s => s.Document.Database.Angdir, (c, v) => c.Database.Angdir = v, "$ANGDIR")
            );
            registry.AddVariable(
                HeaderWhole(
                    "INSUNITS",
                    0,
                    24,
                    d => (int)d.Insunits,
                    (d, v) => d.Insunits = (DxfUnits)v,
                    (c, v) => SinceR2000(c, "INSUNITS")
                )
            );
            registry.AddVariable(
                HeaderWhole(
                    "MEASUREMENT",
                    0,
                    1,
                    d => (int)d.Measurement,
                    (d, v) => d.Measurement = (DxfMeasurementValue)v,
                    (c, v) => SinceR2000(c, "MEASUREMENT")
                )
            );
            registry.AddVariable(HeaderWhole("UNITMODE", 0, 1, d => d.Unitmode, (d, v) => d.Unitmode = v));

            // The width and the linetype generation of the polylines PLINE makes, and what the header keeps for new objects.
            registry.AddVariable(
                CadSystemVariable.Number(
                    "PLINEWID",
                    true,
                    CadSystemVariable.RealBound.Finite,
                    false,
                    s => s.Document.Database.Plinewid,
                    (c, v) => c.Database.Plinewid = v,
                    check: v => v < 0.0 ? "Cannot set PLINEWID to that value." : null
                )
            );
            registry.AddVariable(
                Flag("PLINEGEN", s => s.Document.Database.Plinegen, (c, v) => c.Database.Plinegen = v, "$PLINEGEN")
            );
            registry.AddVariable(
                Flag("FILLMODE", s => s.Document.Database.Fillmode, (c, v) => c.Database.Fillmode = v, "$FILLMODE")
            );
            registry.AddVariable(
                CadSystemVariable.Number(
                    "ELEVATION",
                    true,
                    CadSystemVariable.RealBound.Finite,
                    false,
                    s => s.Document.Database.Elevation,
                    (c, v) => c.Database.Elevation = v
                )
            );
            registry.AddVariable(
                CadSystemVariable.Number(
                    "THICKNESS",
                    true,
                    CadSystemVariable.RealBound.Finite,
                    false,
                    s => s.Document.Database.Thickness,
                    (c, v) => c.Database.Thickness = v
                )
            );
            registry.AddVariable(
                Flag("LIMCHECK", s => s.Document.Database.Limcheck, (c, v) => c.Database.Limcheck = v, "$LIMCHECK")
            );
            registry.AddVariable(
                Flag("SPLFRAME", s => s.Document.Database.Splframe, (c, v) => c.Database.Splframe = v, "$SPLFRAME")
            );
            registry.AddVariable(
                Flag("QTEXTMODE", s => s.Document.Database.Qtextmode, (c, v) => c.Database.Qtextmode = v, "$QTEXTMODE")
            );
            registry.AddVariable(HeaderWhole("ATTMODE", 0, 2, d => d.Attmode, (d, v) => d.Attmode = v));

            // AutoCAD takes any whole number of 0 to 32767 for the spline of a fitted polyline and any of -32768 to 32767 but
            // nought for its segments; the header holds 5 or 6, and -32768 to 32767 but nought.
            registry.AddVariable(
                HeaderWhole(
                    "SPLINETYPE",
                    0,
                    short.MaxValue,
                    d => d.Splinetype,
                    (d, v) => d.Splinetype = v,
                    (c, v) => v == 5 || v == 6 ? null : "Cannot set SPLINETYPE to that value."
                )
            );
            registry.AddVariable(
                HeaderWhole(
                    "SPLINESEGS",
                    short.MinValue,
                    short.MaxValue,
                    d => d.Splinesegs,
                    (d, v) => d.Splinesegs = v,
                    (c, v) => v == 0 ? "Cannot set SPLINESEGS to that value." : null
                )
            );

            // The five whole numbers and the five reals the drawing keeps for programs.
            AddUserVariablesTo(registry);

            // What CIRCLE, POLYGON, TRIM and EXTEND start with, kept by the session as the commands keep it.
            registry.AddVariable(
                CadSystemVariable.Number(
                    "CIRCLERAD",
                    false,
                    CadSystemVariable.RealBound.Finite,
                    false,
                    s => Kept(s, "circle", 0.0),
                    (c, v) => c.Remember("circle", v),
                    check: v => v < 0.0 ? "Cannot set CIRCLERAD to that value." : null
                )
            );
            registry.AddVariable(
                CadSystemVariable.Whole(
                    "POLYSIDES",
                    false,
                    3,
                    1024,
                    s => (int)Kept(s, "polygon.sides", 4.0),
                    (c, v) => c.Remember("polygon.sides", v)
                )
            );
            registry.AddVariable(
                CadSystemVariable.Whole(
                    "TRIMEXTENDMODE",
                    false,
                    0,
                    1,
                    s => (int)Kept(s, ModeKey, 1.0),
                    (c, v) => c.Remember(ModeKey, v)
                )
            );

            // Kept by the session with no behaviour behind them in the presenter, as AutoCAD starts them.
            registry.AddVariable(KeptWhole("CMDECHO", 0, 1, "cmdecho", 1));
            registry.AddVariable(KeptWhole("EXPERT", 0, 5, "expert", 0));
            registry.AddVariable(KeptWhole("HIGHLIGHT", 0, 1, "highlight", 1));
            registry.AddVariable(KeptWhole("PICKAUTO", -7, 7, "pickauto", 5));
            registry.AddVariable(KeptWhole("PICKDRAG", 0, 2, "pickdrag", 2));
            registry.AddVariable(KeptWhole("OSNAPZ", 0, 1, "osnapz", 0));
            registry.AddVariable(KeptWhole("ARRAYASSOCIATIVITY", 0, 1, "arrayassociativity", 1));

            // How HATCH shows its settings: the command line alone (0), the Hatch dialog (1) or the Hatch Creation tab (2).
            registry.AddVariable(KeptWhole("HPDLGMODE", 0, 2, HatchDialogModeKey, 2));

            // Whether HATCH keeps a polyline of each boundary it found, which -HATCH's Retain boundary sets too.
            registry.AddVariable(KeptWhole("HPBOUNDRETAIN", 0, 1, HatchRetainKey, 0));

            // The settings of the next hatch, which the hatch dialog and -HATCH hold too.
            registry.AddVariable(
                CadSystemVariable.Text("HPNAME", false, s => HatchNameOf(s.Document.HatchSettings), HatchNameTyped)
            );
            registry.AddVariable(
                CadSystemVariable.Number(
                    "HPSCALE",
                    false,
                    CadSystemVariable.RealBound.Finite,
                    false,
                    s => s.Document.HatchSettings.Scale,
                    (c, v) => c.Document.HatchSettings.Scale = v,
                    check: v => v > 0.0 ? null : "Cannot set HPSCALE to that value."
                )
            );
            registry.AddVariable(
                CadSystemVariable.Angle(
                    "HPANG",
                    false,
                    s => s.Document.HatchSettings.Angle * Math.PI / 180.0,
                    (c, v) => c.Document.HatchSettings.Angle = Math.Round(v * 180.0 / Math.PI, 9),
                    null,
                    true
                )
            );
            registry.AddVariable(
                CadSystemVariable.Optional(
                    "HPLAYER",
                    "use current",
                    s => s.Document.HatchSettings.Layer ?? "use current",
                    (c, typed) =>
                    {
                        c.Document.HatchSettings.Layer = typed.Length == 0 ? null : typed;
                        return null;
                    }
                )
            );
            registry.AddVariable(
                CadSystemVariable.Whole(
                    "HPASSOC",
                    false,
                    0,
                    1,
                    s => s.Document.HatchSettings.Associative ? 1 : 0,
                    (c, v) => c.Document.HatchSettings.Associative = v != 0
                )
            );
            registry.AddVariable(
                CadSystemVariable.Whole(
                    "HPDOUBLE",
                    false,
                    0,
                    1,
                    s => s.Document.HatchSettings.Double ? 1 : 0,
                    (c, v) => c.Document.HatchSettings.Double = v != 0
                )
            );
            registry.AddVariable(
                CadSystemVariable.Number(
                    "HPGAPTOL",
                    false,
                    CadSystemVariable.RealBound.Finite,
                    false,
                    s => s.Document.HatchSettings.GapTolerance,
                    (c, v) => c.Document.HatchSettings.GapTolerance = v,
                    check: v => v < 0.0 || v > 5000.0 ? "Cannot set HPGAPTOL to that value." : null
                )
            );
            registry.AddVariable(
                CadSystemVariable.Whole(
                    "HPORIGINMODE",
                    false,
                    0,
                    5,
                    s => (int)s.Document.HatchSettings.OriginMode,
                    (c, v) => c.Document.HatchSettings.OriginMode = (CadHatchOriginMode)v
                )
            );

            // The layer new dimensions go on, which DIM's LAyer sets too; "." for the current layer.
            registry.AddVariable(
                CadSystemVariable.Optional(
                    "DIMLAYER",
                    "use current",
                    s => s.DimLayer ?? "use current",
                    (c, typed) =>
                    {
                        c.Session.DimLayer = typed.Length == 0 ? null : typed;
                        return null;
                    }
                )
            );

            // What the commands measured and the last point, for the user to read.
            registry.AddVariable(MeasuredVariable("DISTANCE", DistanceKey));
            registry.AddVariable(MeasuredVariable("AREA", AreaKey));
            registry.AddVariable(MeasuredVariable("PERIMETER", PerimeterKey));
            registry.AddVariable(
                CadSystemVariable.Point(
                    "LASTPOINT",
                    true,
                    s => P3(s.Ucs.ToUcs(s.LastPoint)),
                    (c, v) => c.Session.LastPoint = c.Session.Ucs.ToWorld(v),
                    null
                )
            );

            // What AutoLISP routines ask of the program: which one it is, and what is running.
            registry.AddVariable(
                CadSystemVariable.ReadOnly("PRODUCT", CadVariableKind.Text, s => "\"GeometryHelper.Cad\"")
            );
            registry.AddVariable(CadSystemVariable.ReadOnly("ACADVER", CadVariableKind.Text, s => "\"24.0\""));

            // PROGRAM and PLATFORM read as AutoCAD 2021's console gives them (probe q6), so that a routine that tests them takes
            // AutoCAD's path; PRODUCT is the one that tells the programs apart. ERRNO is the number AutoLISP's failing functions
            // leave in AutoCAD, 0 at the start, here kept as set: AutoCAD takes a whole number of a short's range (probe p5: -5 and
            // 7 taken, 40000, 2.5 and "x" refused); the bounds are ours.
            registry.AddVariable(CadSystemVariable.ReadOnly("PROGRAM", CadVariableKind.Text, s => "\"acad\""));
            registry.AddVariable(
                CadSystemVariable.ReadOnly("PLATFORM", CadVariableKind.Text, s =>
                    "\"Microsoft Windows NT Version 6.2 (x64)\""
                )
            );
            registry.AddVariable(KeptWhole("ERRNO", short.MinValue, short.MaxValue, "errno", 0));

            // The centre of the view in the user coordinate system (the origin where the session has no canvas, which is ours: the
            // console's own 3.8274,0,0 is its default window), and the state of UNDO as the bits AutoCAD adds (probe p5): 1 on, 2
            // One, 4 Auto, 8 a group open, 16 Combine, 32 Layer; None is 0.
            registry.AddVariable(
                CadSystemVariable.ReadOnly(
                    "VIEWCTR",
                    CadVariableKind.Point,
                    s => CadSystemVariable.Show(ViewCentre(s), true),
                    s => new[] { ViewCentre(s).X, ViewCentre(s).Y, ViewCentre(s).Z }
                )
            );
            registry.AddVariable(
                CadSystemVariable.ReadOnly(
                    "UNDOCTL",
                    CadVariableKind.Integer,
                    s => UndoBits(s.Document).ToString(CultureInfo.InvariantCulture),
                    s => new double[] { UndoBits(s.Document) }
                )
            );
            registry.AddVariable(
                CadSystemVariable.ReadOnly("LISPINIT", CadVariableKind.Integer, s => "1", s => new[] { 1.0 })
            );
            registry.AddVariable(
                CadSystemVariable.ReadOnly(
                    "CMDACTIVE",
                    CadVariableKind.Integer,
                    s => CommandsActive(s).ToString(CultureInfo.InvariantCulture),
                    s => new double[] { CommandsActive(s) }
                )
            );
            registry.AddVariable(
                CadSystemVariable.ReadOnly("CMDNAMES", CadVariableKind.Text, s =>
                    "\"" + (s.Current?.Name ?? string.Empty) + "\""
                )
            );
            registry.AddVariable(KeptWhole("BLIPMODE", 0, 1, "blipmode", 0));
            registry.AddVariable(KeptWhole("MENUECHO", 0, 31, "menuecho", 0));

            // The axes of the user coordinate system, in the world's.
            registry.AddVariable(
                CadSystemVariable.ReadOnly(
                    "UCSXDIR",
                    CadVariableKind.Point,
                    s => CadSystemVariable.Show(P3(s.Ucs.XAxis), true),
                    s => CoordinatesOf(P3(s.Ucs.XAxis))
                )
            );
            registry.AddVariable(
                CadSystemVariable.ReadOnly(
                    "UCSYDIR",
                    CadVariableKind.Point,
                    s => CadSystemVariable.Show(P3(s.Ucs.YAxis), true),
                    s => CoordinatesOf(P3(s.Ucs.YAxis))
                )
            );

            // The dimension variables, which the header holds as those new dimensions take.
            AddDimVariablesTo(registry);

            // The gate in front of the files of code: SECURELOAD, TRUSTEDPATHS and LEGACYCODESEARCH.
            AddLoadSecurityVariablesTo(registry);
        }

        /// <summary>
        /// CMDACTIVE: 1 while a command runs and 2 while a transparent one does too, as AutoCAD sums them. The bits of a script
        /// (4) and of AutoLISP (32) wait for the session to say when one runs.
        /// </summary>
        private static int CommandsActive(CadCommandSession session) =>
            (session.IsRunning ? 1 : 0) | (session.IsRunning && session.IsTransparent ? 2 : 0);

        /// <summary>VIEWCTR: the point of the drawing at the centre of the view worked in, in the user coordinate system.</summary>
        private static GeoPoint3 ViewCentre(CadCommandSession session)
        {
            CadView view = session.WorkView;
            return view == null
                ? new GeoPoint3(0.0, 0.0, 0.0)
                : session.Ucs.ToUcs(new GeoPoint3(view.CenterX, view.CenterY, 0.0));
        }

        /// <summary>UNDOCTL: the bits of UNDO's state, 0 where Control is None.</summary>
        private static int UndoBits(CadDocument document) =>
            document.UndoControl == CadUndoControl.None
                ? 0
                : 1
                    | (document.UndoControl == CadUndoControl.One ? 2 : 0)
                    | (document.UndoAuto ? 4 : 0)
                    | (document.IsUndoGroupOpen ? 8 : 0)
                    | (document.UndoCombine ? 16 : 0)
                    | (document.UndoLayer ? 32 : 0);

        private static GeoPoint3 P3(GeoVector3 vector) => new GeoPoint3(vector.X, vector.Y, vector.Z);

        /// <summary>The key of the distance DIST measured last in <see cref="CadCommandSession.Remembered"/>.</summary>
        private const string DistanceKey = "measured.distance";

        /// <summary>The key of the area AREA measured last.</summary>
        private const string AreaKey = "measured.area";

        /// <summary>The key of the perimeter, or length, AREA measured last.</summary>
        private const string PerimeterKey = "measured.perimeter";

        /// <summary>Keeps the area and the perimeter, or length, AREA found, for AREA and PERIMETER to give.</summary>
        private static void KeepArea(CadCommandContext c, double area, double perimeter)
        {
            c.Remember(AreaKey, area);
            c.Remember(PerimeterKey, perimeter);
        }

        /// <summary>A measure kept by the session, to be read: shown to four places, as AutoCAD shows DISTANCE.</summary>
        private static CadSystemVariable MeasuredVariable(string name, string key) =>
            CadSystemVariable.ReadOnly(
                name,
                CadVariableKind.Real,
                s => CadSystemVariable.Show(Kept(s, key, 0.0), false),
                s => new[] { Kept(s, key, 0.0) }
            );

        /// <summary>A whole number of the drawing's header: the change is one undo step, written as the header writes it.</summary>
        private static CadSystemVariable HeaderWhole(
            string name,
            int least,
            int most,
            Func<DxfDatabase, int> get,
            Action<DxfDatabase, int> set,
            Func<CadCommandContext, int, string> check = null
        ) =>
            CadSystemVariable.Whole(
                name,
                true,
                least,
                most,
                s => get(s.Document.Database),
                (c, v) => set(c.Database, v),
                null,
                check
            );

        /// <summary>A whole number the session keeps under a key, from AutoCAD's start.</summary>
        private static CadSystemVariable KeptWhole(string name, int least, int most, string key, int start) =>
            CadSystemVariable.Whole(
                name,
                false,
                least,
                most,
                s => (int)Kept(s, key, start),
                (c, v) => c.Remember(key, v)
            );

        /// <summary>A variable of R2000 and later: a drawing of R12 has none, and says so as XCLIPFRAME does.</summary>
        private static string SinceR2000(CadCommandContext c, string name) =>
            string.CompareOrdinal(c.Database.Version ?? "AC1032", "AC1015") < 0
                ? name + " is kept in a drawing of R2000 and later; this one is older."
                : null;

        /// <summary>USERI1 to USERI5, whole numbers of -32768 to 32767, and USERR1 to USERR5, reals, as the header keeps them.</summary>
        private static void AddUserVariablesTo(CadCommandRegistry registry)
        {
            Func<DxfDatabase, int>[] whole =
            {
                d => d.Useri1,
                d => d.Useri2,
                d => d.Useri3,
                d => d.Useri4,
                d => d.Useri5,
            };
            Action<DxfDatabase, int>[] setWhole =
            {
                (d, v) => d.Useri1 = v,
                (d, v) => d.Useri2 = v,
                (d, v) => d.Useri3 = v,
                (d, v) => d.Useri4 = v,
                (d, v) => d.Useri5 = v,
            };
            Func<DxfDatabase, double>[] real =
            {
                d => d.Userr1,
                d => d.Userr2,
                d => d.Userr3,
                d => d.Userr4,
                d => d.Userr5,
            };
            Action<DxfDatabase, double>[] setReal =
            {
                (d, v) => d.Userr1 = v,
                (d, v) => d.Userr2 = v,
                (d, v) => d.Userr3 = v,
                (d, v) => d.Userr4 = v,
                (d, v) => d.Userr5 = v,
            };

            for (int i = 0; i < 5; i++)
            {
                Func<DxfDatabase, int> getWhole = whole[i];
                Action<DxfDatabase, int> putWhole = setWhole[i];
                Func<DxfDatabase, double> getReal = real[i];
                Action<DxfDatabase, double> putReal = setReal[i];
                string number = (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
                registry.AddVariable(HeaderWhole("USERI" + number, short.MinValue, short.MaxValue, getWhole, putWhole));
                registry.AddVariable(
                    CadSystemVariable.Number(
                        "USERR" + number,
                        true,
                        CadSystemVariable.RealBound.Finite,
                        false,
                        s => getReal(s.Document.Database),
                        (c, v) => putReal(c.Database, v)
                    )
                );
            }
        }

        /// <summary>HPNAME as the next hatch holds it: SOLID, a gradient's name, _USER for lines made by the user, else the pattern's.</summary>
        private static string HatchNameOf(CadHatchSettings settings) =>
            settings.Type == CadHatchType.Solid ? "SOLID"
            : settings.Type == CadHatchType.Gradient ? settings.GradientName
            : settings.Type == CadHatchType.UserDefined ? "_USER"
            : settings.PatternName;

        /// <summary>
        /// HPNAME set: SOLID, one of the nine gradients, U or _USER for lines made by the user, or a pattern of the presenter's
        /// set, in letters of either case; a period keeps the name; any other is refused as AutoCAD refuses "xyz".
        /// </summary>
        private static string HatchNameTyped(CadCommandContext c, string typed)
        {
            CadHatchSettings settings = c.Document.HatchSettings;
            string name = typed.Trim();
            string upper = name.ToUpperInvariant();

            if (name == ".")
            {
                return null;
            }

            if (upper == "SOLID")
            {
                settings.Type = CadHatchType.Solid;
                return null;
            }

            if (upper == "U" || upper == "_USER")
            {
                settings.Type = CadHatchType.UserDefined;
                return null;
            }

            for (int i = 0; i < CadHatchSettings.GradientNames.Count; i++)
            {
                if (string.Equals(CadHatchSettings.GradientNames[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    settings.Type = CadHatchType.Gradient;
                    settings.GradientName = CadHatchSettings.GradientNames[i];
                    return null;
                }
            }

            if (name.Length > 0 && CadPatternFile.FindBuiltIn(upper) != null)
            {
                settings.Type = CadHatchType.Pattern;
                settings.PatternName = upper;
                settings.PatternFile = null;
                return null;
            }

            return "Cannot set HPNAME to that value.";
        }
    }
}
