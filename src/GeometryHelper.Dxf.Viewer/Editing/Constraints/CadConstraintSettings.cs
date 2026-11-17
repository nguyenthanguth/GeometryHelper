using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>The constraint types of AUTOCONSTRAIN's tab in AutoCAD 2021's Constraint Settings, in the order the bits of CONSTRAINTBARMODE count them.</summary>
    public enum CadConstraintType
    {
        /// <summary>Coincident (bit 1).</summary>
        Coincident,

        /// <summary>Collinear (bit 2).</summary>
        Collinear,

        /// <summary>Concentric (bit 4).</summary>
        Concentric,

        /// <summary>Fix (bit 8).</summary>
        Fix,

        /// <summary>Parallel (bit 16).</summary>
        Parallel,

        /// <summary>Perpendicular (bit 32).</summary>
        Perpendicular,

        /// <summary>Horizontal (bit 64).</summary>
        Horizontal,

        /// <summary>Vertical (bit 128).</summary>
        Vertical,

        /// <summary>Tangent (bit 256).</summary>
        Tangent,

        /// <summary>Smooth (bit 512).</summary>
        Smooth,

        /// <summary>Symmetric (bit 1024).</summary>
        Symmetric,

        /// <summary>Equal (bit 2048).</summary>
        Equal,
    }

    /// <summary>One line of the AutoConstrain list of the Constraint Settings dialog: a constraint type and whether AUTOCONSTRAIN applies it; the list's order is the order they are tried in.</summary>
    public sealed class CadAutoConstrainRule
    {
        /// <summary>Makes a rule.</summary>
        /// <param name="type">The constraint type.</param>
        /// <param name="enabled">Whether AUTOCONSTRAIN applies it.</param>
        public CadAutoConstrainRule(CadConstraintType type, bool enabled)
        {
            Type = type;
            Enabled = enabled;
        }

        /// <summary>Gets the constraint type.</summary>
        public CadConstraintType Type { get; }

        /// <summary>Gets or sets whether AUTOCONSTRAIN applies the type.</summary>
        public bool Enabled { get; set; }
    }

    /// <summary>
    /// The settings of AutoCAD 2021's Constraint Settings dialog and of the variables behind them (CONSTRAINTBARMODE,
    /// CONSTRAINTBARDISPLAY, CONSTRAINTINFER, CONSTRAINTNAMEFORMAT, CCONSTRAINTFORM, DYNCONSTRAINTMODE, CONSTRAINTSOLVEMODE,
    /// PARAMETERCOPYMODE), and the tolerances and the order AUTOCONSTRAIN works by. They stay with the session.
    /// </summary>
    public sealed class CadConstraintSettings
    {
        /// <summary>The constraint types a bar shows, as the bits of CONSTRAINTBARMODE (4095 for all twelve).</summary>
        public int BarMode { get; set; } = 4095;

        /// <summary>
        /// CONSTRAINTBARDISPLAY: 0 shows no constraint bar, 1 shows the bars of the objects that are selected, 2 those of the
        /// objects a constraint was just applied to, 3 both. AutoCAD 2021 starts at 3.
        /// </summary>
        public int BarDisplay { get; set; } = 3;

        /// <summary>CONSTRAINTINFER: whether a coincident, horizontal or vertical constraint is inferred where a line or arc is drawn.</summary>
        public bool Infer { get; set; }

        /// <summary>CONSTRAINTNAMEFORMAT: the words a dimensional constraint shows: 0 its name, 1 its value, 2 its name and expression.</summary>
        public int NameFormat { get; set; } = 2;

        /// <summary>CCONSTRAINTFORM: the form a new dimensional constraint is made in: 0 dynamic, 1 annotational.</summary>
        public int Form { get; set; }

        /// <summary>DYNCONSTRAINTMODE: whether dynamic dimensional constraints are shown (1) or hidden (0).</summary>
        public int DynamicMode { get; set; } = 1;

        /// <summary>CONSTRAINTSOLVEMODE: how an edit is solved; kept as AutoCAD keeps it, the presenter always solves.</summary>
        public int SolveMode { get; set; } = 1;

        /// <summary>PARAMETERCOPYMODE: what a copy of constrained objects keeps; kept as AutoCAD keeps it.</summary>
        public int CopyMode { get; set; } = 1;

        /// <summary>The bar transparency of the Geometric tab, in percent.</summary>
        public int Transparency { get; set; } = 50;

        /// <summary>The distance within which AUTOCONSTRAIN takes two points for one, in drawing units.</summary>
        public double AutoDistance { get; set; } = 0.01;

        /// <summary>The angle within which AUTOCONSTRAIN takes two directions for one, in degrees.</summary>
        public double AutoAngle { get; set; } = 1.0;

        /// <summary>Whether a tangent is applied only where the objects touch at an end of one of them (the AutoConstrain tab's box).</summary>
        public bool TangentAtIntersection { get; set; }

        /// <summary>Whether a perpendicular is applied only where the objects meet (the AutoConstrain tab's box).</summary>
        public bool PerpendicularAtIntersection { get; set; }

        /// <summary>The types AUTOCONSTRAIN tries and the order it tries them in.</summary>
        public List<CadAutoConstrainRule> AutoRules { get; set; } = DefaultRules();

        /// <summary>The types that AUTOCONSTRAIN's Coincident option alone applies.</summary>
        internal static readonly CadConstraintType[] OnlyCoincident = { CadConstraintType.Coincident };

        private static List<CadAutoConstrainRule> DefaultRules() =>
            new List<CadAutoConstrainRule>
            {
                new CadAutoConstrainRule(CadConstraintType.Horizontal, true),
                new CadAutoConstrainRule(CadConstraintType.Vertical, true),
                new CadAutoConstrainRule(CadConstraintType.Perpendicular, true),
                new CadAutoConstrainRule(CadConstraintType.Parallel, true),
                new CadAutoConstrainRule(CadConstraintType.Tangent, true),
                new CadAutoConstrainRule(CadConstraintType.Coincident, true),
                new CadAutoConstrainRule(CadConstraintType.Concentric, true),
                new CadAutoConstrainRule(CadConstraintType.Collinear, true),
                new CadAutoConstrainRule(CadConstraintType.Equal, true),
                new CadAutoConstrainRule(CadConstraintType.Fix, false),
            };

        /// <summary>Whether a bar shows the constraints of a kind.</summary>
        /// <param name="type">The type.</param>
        /// <returns>True where its bit of <see cref="BarMode"/> is set.</returns>
        public bool ShowsBarOf(CadConstraintType type) => (BarMode & (1 << (int)type)) != 0;

        /// <summary>A copy the Constraint Settings dialog changes.</summary>
        /// <returns>The copy.</returns>
        public CadConstraintSettings Clone()
        {
            var copy = (CadConstraintSettings)MemberwiseClone();
            copy.AutoRules = new List<CadAutoConstrainRule>();

            foreach (CadAutoConstrainRule rule in AutoRules)
            {
                copy.AutoRules.Add(new CadAutoConstrainRule(rule.Type, rule.Enabled));
            }

            return copy;
        }

        /// <summary>The type of a geometric constraint kind.</summary>
        internal static CadConstraintType TypeOf(CadGeoKind kind)
        {
            switch (kind)
            {
                case CadGeoKind.Coincident:
                case CadGeoKind.PointOnCurve:
                    return CadConstraintType.Coincident;
                case CadGeoKind.Collinear:
                    return CadConstraintType.Collinear;
                case CadGeoKind.Concentric:
                    return CadConstraintType.Concentric;
                case CadGeoKind.Fix:
                    return CadConstraintType.Fix;
                case CadGeoKind.Parallel:
                    return CadConstraintType.Parallel;
                case CadGeoKind.Perpendicular:
                    return CadConstraintType.Perpendicular;
                case CadGeoKind.Horizontal:
                    return CadConstraintType.Horizontal;
                case CadGeoKind.Vertical:
                    return CadConstraintType.Vertical;
                case CadGeoKind.Tangent:
                    return CadConstraintType.Tangent;
                case CadGeoKind.Symmetric:
                    return CadConstraintType.Symmetric;
                default:
                    return CadConstraintType.Equal;
            }
        }
    }

    /// <summary>What the command hands the window's Constraint Settings dialog: the settings to show (a copy), the tab to open on, and what the dialog did.</summary>
    public sealed class CadConstraintSettingsRequest
    {
        /// <summary>Makes the request.</summary>
        /// <param name="settings">The settings the dialog shows and changes.</param>
        /// <param name="tab">The tab to open on: 0 Geometric, 1 Dimensional, 2 AutoConstrain.</param>
        /// <exception cref="ArgumentNullException">Thrown when the settings are null.</exception>
        public CadConstraintSettingsRequest(CadConstraintSettings settings, int tab)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Tab = tab;
        }

        /// <summary>Gets the settings the dialog shows and changes; OK makes them the session's.</summary>
        public CadConstraintSettings Settings { get; }

        /// <summary>Gets the tab to open on: 0 Geometric, 1 Dimensional, 2 AutoConstrain.</summary>
        public int Tab { get; }

        /// <summary>Gets or sets what the dialog did: OK or Cancel.</summary>
        public CadDialogAction Action { get; set; }
    }

    /// <summary>One parameter as the Parameters Manager lists it.</summary>
    public sealed class CadParameterRow
    {
        /// <summary>Makes a row.</summary>
        /// <param name="name">The name.</param>
        /// <param name="expression">The expression.</param>
        /// <param name="value">The value, a length or an angle in degrees.</param>
        /// <param name="isUser">Whether it is a user parameter.</param>
        /// <param name="isAngle">Whether the value is an angle.</param>
        public CadParameterRow(string name, string expression, double value, bool isUser, bool isAngle)
        {
            Name = name;
            Expression = expression;
            Value = value;
            IsUser = isUser;
            IsAngle = isAngle;
        }

        /// <summary>Gets the name.</summary>
        public string Name { get; }

        /// <summary>Gets the expression.</summary>
        public string Expression { get; }

        /// <summary>Gets the value.</summary>
        public double Value { get; }

        /// <summary>Gets whether it is a user parameter.</summary>
        public bool IsUser { get; }

        /// <summary>Gets whether the value is an angle in degrees.</summary>
        public bool IsAngle { get; }
    }
}
