using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The point modifiers AutoCAD takes at any prompt for a point, typed or chosen from the Shift+right-click menu: FROM, a
    /// base point and an offset from it; M2P (MTP), the point midway between two; TT, a point tracking lines run from; TK
    /// (TRACKING), a point reached from another along the axes; and the point filters .X .Y .Z .XY .XZ .YZ, the coordinates
    /// named taken from one point and the rest from another.
    /// </summary>
    public sealed partial class CadCommandSession
    {
        // The modifiers being answered, the one asking now last: a modifier typed at a modifier's prompt nests in it.
        private readonly List<PointModifier> _modifiers = new List<PointModifier>();

        private readonly List<GeoPoint2> _trackPoints = new List<GeoPoint2>();

        /// <summary>The modifiers, what each asks and gives.</summary>
        private enum ModifierKind
        {
            From,
            Mid,
            Track,
            Tracking,
            Filter,
        }

        /// <summary>
        /// Gets the temporary track points (TT) given for the point asked now, the oldest first: the window acquires each for
        /// object snap tracking, as AutoCAD does, so that lines run from it level and upright, or on the polar angles, while
        /// OTRACK is on. They are let go once a point is given or the command ends.
        /// </summary>
        public IReadOnlyList<GeoPoint2> TemporaryTrackPoints => _trackPoints;

        /// <summary>
        /// Starts a point modifier at a prompt that takes a point, as AutoCAD's: FROM (or FRO) asks "Base point:" then
        /// "&lt;Offset&gt;:" and gives the offset point, an offset typed as @dx,dy or @distance&lt;angle being measured from
        /// the base point; M2P or MTP asks "First point of mid:" and "Second point of mid:" and gives the point midway; TT asks
        /// "Specify temporary OTRACK point:" and asks the first prompt again with tracking lines from the point
        /// (<see cref="TemporaryTrackPoints"/>); TK (TRACK, TRACKING) asks "First tracking point:" then "Next point (Press ENTER
        /// to end tracking):" until Enter, each point taken square from the one before along the axis it lies farther along
        /// (a distance typed going along the axis nearest the cursor), and gives the last; .X, .Y, .Z, .XY, .XZ or .YZ asks
        /// "of" a point whose coordinates named are kept, then "(need YZ):" for the rest. Coordinates are those of the
        /// <see cref="Ucs"/>, and Z, which a plan has none of, is asked as AutoCAD asks it and left out. A modifier typed at a
        /// modifier's prompt nests in it, as FROM with M2P for its base point, and each point may be snapped. A leading
        /// underscore is allowed, as the menu sends "_from". A line of 100,50 FROM with the offset @30,20 starts at (130, 70);
        /// M2P between (0, 0) and (100, 40) gives (50, 20); TK from (100, 0) to (130, 50) gives (100, 50); .X of (30, 999) with
        /// (888, 40) at "(need YZ):" gives (30, 40).
        /// </summary>
        /// <param name="typed">The modifier: FROM, M2P, TT, TK, .XY.</param>
        /// <returns>Whether it was started: false where it is none of these or no prompt waits for a point.</returns>
        public bool StartPointModifier(string typed)
        {
            if (!TakesModifier() || !TryModifier(typed, out ModifierKind kind, out string filter))
            {
                return false;
            }

            Write(Prompt.Text + " " + typed.Trim());
            var modifier = new PointModifier(kind, filter, Prompt);
            _modifiers.Add(modifier);
            Prompt = FirstPrompt(modifier);
            Changed();
            return true;
        }

        /// <summary>Whether the prompt waiting takes a point a modifier may give: a point, or a distance or an angle from a base point.</summary>
        private bool TakesModifier()
        {
            if (Prompt == null)
            {
                return false;
            }

            return Prompt.Kind == CadPromptKind.Point
                || ((Prompt.Kind == CadPromptKind.Distance || Prompt.Kind == CadPromptKind.Angle) && Prompt.BasePoint.HasValue);
        }

        private static bool TryModifier(string typed, out ModifierKind kind, out string filter)
        {
            kind = ModifierKind.From;
            filter = null;
            string word = (typed ?? string.Empty).Trim().ToUpperInvariant();
            word = word.StartsWith("_", StringComparison.Ordinal) ? word.Substring(1) : word;

            switch (word)
            {
                case "FROM":
                case "FRO":
                    kind = ModifierKind.From;
                    return true;
                case "M2P":
                case "MTP":
                    kind = ModifierKind.Mid;
                    return true;
                case "TT":
                    kind = ModifierKind.Track;
                    return true;
                case "TK":
                case "TRACK":
                case "TRACKING":
                    kind = ModifierKind.Tracking;
                    return true;
                case ".X":
                case ".Y":
                case ".Z":
                case ".XY":
                case ".XZ":
                case ".YZ":
                    kind = ModifierKind.Filter;
                    filter = word.Substring(1);
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>The first prompt of a modifier, its rubber band from the base point of the prompt it was typed at.</summary>
        private static CadPrompt FirstPrompt(PointModifier modifier)
        {
            GeoPoint2? basePoint = modifier.Outer.BasePoint;

            switch (modifier.Kind)
            {
                case ModifierKind.From:
                    return CadPrompt.ForPoint("Base point", basePoint);
                case ModifierKind.Mid:
                    return CadPrompt.ForPoint("First point of mid", basePoint);
                case ModifierKind.Track:
                    return CadPrompt.ForPoint("Specify temporary OTRACK point", basePoint);
                case ModifierKind.Tracking:
                    return CadPrompt.ForPoint("First tracking point", basePoint);
                default:
                    return CadPrompt.ForPoint("of", basePoint).WithoutColon();
            }
        }

        /// <summary>
        /// The answer to a modifier's prompt: a point is kept for the second prompt or gives the modifier's point, which goes
        /// to the modifier it nests in or to the prompt it was typed at; anything else is not a point, and is asked again.
        /// </summary>
        private bool TakeModifierInput(CadInput input)
        {
            PointModifier modifier = _modifiers[_modifiers.Count - 1];

            // Enter at "Next point" ends tracking with the point reached.
            bool tracked = modifier.Kind == ModifierKind.Tracking && modifier.First.HasValue && input.IsNone;

            if (!input.IsPoint && !tracked)
            {
                Write("Invalid point.");
                Changed();
                return false;
            }

            GeoPoint2 point = tracked ? modifier.First.Value : input.Point;

            if (modifier.Kind == ModifierKind.Tracking && !tracked)
            {
                modifier.First = modifier.First.HasValue ? Square(modifier.First.Value, point) : point;
                LastPoint = modifier.First.Value;
                Prompt = CadPrompt.ForPoint("Next point (Press ENTER to end tracking)", modifier.First);
                Changed();
                return true;
            }

            if (modifier.Kind == ModifierKind.Track)
            {
                _trackPoints.Add(point);
                _modifiers.RemoveAt(_modifiers.Count - 1);
                Prompt = modifier.Outer;
                Changed();
                return true;
            }

            // The tracking a temporary track point gave was for this point.
            _trackPoints.Clear();

            if (!modifier.First.HasValue)
            {
                modifier.First = point;

                switch (modifier.Kind)
                {
                    case ModifierKind.From:
                        Prompt = CadPrompt.ForPoint("<Offset>", point);
                        break;
                    case ModifierKind.Mid:
                        Prompt = CadPrompt.ForPoint("Second point of mid", point);
                        break;
                    default:
                        Prompt = CadPrompt.ForPoint("(need " + Needed(modifier.Filter) + ")", modifier.Outer.BasePoint);
                        break;
                }

                Changed();
                return true;
            }

            GeoPoint2 given = Combine(modifier, point);
            _modifiers.RemoveAt(_modifiers.Count - 1);
            Prompt = modifier.Outer;

            if (_modifiers.Count > 0)
            {
                LastPoint = given;
                return TakeModifierInput(new CadInput(CadInputKind.Point, given, 0.0, null, null));
            }

            return AnswerPoint(given, null);
        }

        /// <summary>The point a modifier gives from its two: the offset point, the point midway, or the coordinates filtered.</summary>
        private GeoPoint2 Combine(PointModifier modifier, GeoPoint2 second)
        {
            GeoPoint2 first = modifier.First.Value;

            switch (modifier.Kind)
            {
                case ModifierKind.From:
                case ModifierKind.Tracking:
                    return second;
                case ModifierKind.Mid:
                    return new GeoPoint2((first.X + second.X) / 2.0, (first.Y + second.Y) / 2.0);
                default:
                    GeoPoint2 a = Ucs.ToUcs(first);
                    GeoPoint2 b = Ucs.ToUcs(second);
                    bool x = modifier.Filter.IndexOf('X') >= 0;
                    bool y = modifier.Filter.IndexOf('Y') >= 0;
                    return Ucs.ToWorld(new GeoPoint2(x ? a.X : b.X, y ? a.Y : b.Y));
            }
        }

        /// <summary>
        /// The point tracking reaches from one towards another, square along an axis of the <see cref="Ucs"/>: the other's
        /// coordinate along the axis it lies farther along, the one's across it, X where both lie as far. From (100, 0)
        /// towards (130, 50) it is (100, 50).
        /// </summary>
        private GeoPoint2 Square(GeoPoint2 from, GeoPoint2 toward)
        {
            GeoPoint2 a = Ucs.ToUcs(from);
            GeoPoint2 b = Ucs.ToUcs(toward);
            return Ucs.ToWorld(Math.Abs(b.X - a.X) >= Math.Abs(b.Y - a.Y) ? new GeoPoint2(b.X, a.Y) : new GeoPoint2(a.X, b.Y));
        }

        /// <summary>Whether the prompt waiting is TK's "Next point", whose distances typed run along an axis.</summary>
        private bool IsTrackingOn() => _modifiers.Count > 0 && _modifiers[_modifiers.Count - 1].Kind == ModifierKind.Tracking && _modifiers[_modifiers.Count - 1].First.HasValue;

        /// <summary>
        /// The coordinates typed at "(need YZ):" of a point filter, as many numbers as letters asked, in the order X, Y, Z of
        /// the UCS: "40,3" gives Y 40 (Z 3 is left out). Any other answer is a point, whose coordinates asked are taken.
        /// </summary>
        private bool TryNeeded(string typed, out GeoPoint2 point)
        {
            point = default;

            if (_modifiers.Count == 0)
            {
                return false;
            }

            PointModifier modifier = _modifiers[_modifiers.Count - 1];

            if (modifier.Kind != ModifierKind.Filter || !modifier.First.HasValue)
            {
                return false;
            }

            string needed = Needed(modifier.Filter);
            string[] parts = typed.Split(',');

            if (parts.Length != needed.Length)
            {
                return false;
            }

            GeoPoint2 first = Ucs.ToUcs(modifier.First.Value);
            double x = first.X;
            double y = first.Y;

            for (int i = 0; i < parts.Length; i++)
            {
                if (!TryNumber(parts[i], out double value))
                {
                    return false;
                }

                if (needed[i] == 'X')
                {
                    x = value;
                }
                else if (needed[i] == 'Y')
                {
                    y = value;
                }
            }

            point = Ucs.ToWorld(new GeoPoint2(x, y));
            return true;
        }

        /// <summary>The coordinates a point filter still needs: YZ for .X, Z for .XY.</summary>
        private static string Needed(string filter)
        {
            string needed = string.Empty;

            foreach (char axis in "XYZ")
            {
                if (filter.IndexOf(axis) < 0)
                {
                    needed += axis;
                }
            }

            return needed;
        }

        /// <summary>A modifier being answered: what it is, the coordinates a filter keeps, the prompt it was typed at, and its first point once given.</summary>
        private sealed class PointModifier
        {
            public PointModifier(ModifierKind kind, string filter, CadPrompt outer)
            {
                Kind = kind;
                Filter = filter;
                Outer = outer;
            }

            public ModifierKind Kind { get; }

            public string Filter { get; }

            public CadPrompt Outer { get; }

            public GeoPoint2? First { get; set; }
        }
    }
}
