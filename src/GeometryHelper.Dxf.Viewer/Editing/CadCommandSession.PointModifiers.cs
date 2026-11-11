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

        // Text a modifier hands on to the prompt it was typed at, its echo written already.
        private bool _refeeding;

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
        /// (<see cref="TemporaryTrackPoints"/>); TK (TRACK, TRACKING) asks "First tracking point:" then "Next point  (Press
        /// ENTER to end tracking):" until Enter, each point taken square from the one before along the axis it lies farther
        /// along, upright where it lies as far along both (a distance typed going along the axis nearest the cursor), and gives
        /// the last; .X, .Y, .Z, .XY, .XZ or .YZ asks "of" a point whose coordinates named are kept, then "(need YZ):" for the
        /// rest, a point whose coordinates asked are taken, or where one is asked a number alone. Coordinates are those of the
        /// <see cref="Ucs"/>, and Z, which a plan has none of, is asked as AutoCAD asks it and left out. A modifier typed at a
        /// modifier's prompt nests in it, as FROM with M2P for its base point, and each point may be snapped; as AutoCAD 2021
        /// takes them, TT and TK only at the command's own prompt, and FROM not at TK's. What is no point AutoCAD's way too:
        /// Enter or text at TT's or a filter's prompt, and Enter at FROM's, is answered by the prompt they were typed at; FROM
        /// and M2P ask again for text, M2P for Enter too; TK says "Invalid 2D point.", at its first prompt going back to the
        /// prompt it was typed at. A leading underscore is allowed, as the menu sends "_from". A line of 100,50 FROM with the
        /// offset @30,20 starts at (130, 70); M2P between (0, 0) and (100, 40) gives (50, 20); TK from (100, 0) to (130, 50)
        /// gives (100, 50); .X of (30, 999) with (888, 40) at "(need YZ):" gives (30, 40), and with 40,3 gives (30, 3).
        /// </summary>
        /// <param name="typed">The modifier: FROM, M2P, TT, TK, .XY.</param>
        /// <returns>Whether it was started: false where it is none of these, no prompt waits for a point, or it does not nest in the modifier asking.</returns>
        public bool StartPointModifier(string typed)
        {
            if (!TakesModifier() || !TryModifier(typed, out ModifierKind kind, out string filter) || !Nests(kind))
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

        /// <summary>
        /// Whether the prompt waiting takes a point a modifier may give: a point, or a distance or an angle, from a base point
        /// or, with none, as the first of two points, as AutoCAD 2021 takes FROM at "Enter new value for POLARDIST".
        /// </summary>
        private bool TakesModifier()
        {
            if (Prompt == null)
            {
                return false;
            }

            return Prompt.Kind == CadPromptKind.Point
                || Prompt.Kind == CadPromptKind.Distance
                || Prompt.Kind == CadPromptKind.Angle;
        }

        /// <summary>
        /// Whether a modifier is taken at the prompt waiting, as AutoCAD 2021 takes them: any at a command's own prompt; at a
        /// modifier's, FROM, M2P and the filters, but not TT or TK, and not FROM at TK's.
        /// </summary>
        private bool Nests(ModifierKind kind)
        {
            if (_modifiers.Count == 0)
            {
                return true;
            }

            return kind != ModifierKind.Track
                && kind != ModifierKind.Tracking
                && !(kind == ModifierKind.From && _modifiers[_modifiers.Count - 1].Kind == ModifierKind.Tracking);
        }

        /// <summary>
        /// An answer to a modifier's prompt that is no point, text or Enter (<paramref name="typed"/> null), as AutoCAD 2021
        /// takes it: at TT's and a filter's prompts, and Enter at FROM's, the modifier ends and the prompt it was typed at takes
        /// the answer; FROM asks again for text, and M2P for both, saying "Point or option keyword required." where that prompt
        /// has options and "Invalid point." where it has none; TK says "Invalid 2D point.", asking its next point again, or at
        /// its first ending and asking the prompt it was typed at again.
        /// </summary>
        /// <param name="typed">The text typed; null for Enter.</param>
        /// <param name="echo">The prompt and the text, to be written; null where it has been.</param>
        private bool MissModifier(string typed, string echo)
        {
            PointModifier modifier = _modifiers[_modifiers.Count - 1];

            if (modifier.Kind == ModifierKind.Mid || (modifier.Kind == ModifierKind.From && typed != null))
            {
                bool options = _modifiers[0].Outer.Keywords.Count > 0;
                return Invalid(options ? "Point or option keyword required." : "Invalid point.", echo);
            }

            if (modifier.Kind == ModifierKind.Tracking && modifier.First.HasValue)
            {
                return Invalid("Invalid 2D point.", echo);
            }

            if (echo != null)
            {
                Write(echo);
            }

            _modifiers.RemoveAt(_modifiers.Count - 1);
            Prompt = modifier.Outer;

            if (modifier.Kind == ModifierKind.Tracking)
            {
                Write("Invalid 2D point.");
                Changed();
                return false;
            }

            if (typed != null)
            {
                return Refeed(typed);
            }

            string keyword = Prompt.MatchKeyword(Prompt.Default);
            return Answer(
                keyword != null ? new CadInput(CadInputKind.Keyword, default, 0.0, keyword, null) : CadInput.Nothing,
                null,
                null
            );
        }

        /// <summary>Text a modifier hands on, answered by the prompt now waiting as though typed there, its echo not written again.</summary>
        private bool Refeed(string typed)
        {
            _refeeding = true;

            try
            {
                return Feed(typed);
            }
            finally
            {
                _refeeding = false;
            }
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
                return MissModifier(null, null);
            }

            GeoPoint2 point = tracked ? modifier.First.Value : input.Point;

            if (modifier.Kind == ModifierKind.Tracking && !tracked)
            {
                modifier.First = modifier.First.HasValue ? Square(modifier.First.Value, point) : point;
                LastPoint = modifier.First.Value;
                Prompt = CadPrompt.ForPoint("Next point  (Press ENTER to end tracking)", modifier.First);
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
        /// coordinate along the axis it lies farther along, the one's across it, Y where both lie as far, as AutoCAD 2021
        /// tracks from (100, 0) towards (130, 30) to (100, 30). From (100, 0) towards (130, 50) it is (100, 50).
        /// </summary>
        private GeoPoint2 Square(GeoPoint2 from, GeoPoint2 toward)
        {
            GeoPoint2 a = Ucs.ToUcs(from);
            GeoPoint2 b = Ucs.ToUcs(toward);
            return Ucs.ToWorld(
                Math.Abs(b.X - a.X) > Math.Abs(b.Y - a.Y) ? new GeoPoint2(b.X, a.Y) : new GeoPoint2(a.X, b.Y)
            );
        }

        /// <summary>Whether the prompt waiting is TK's "Next point", whose distances typed run along an axis.</summary>
        private bool IsTrackingOn() =>
            _modifiers.Count > 0
            && _modifiers[_modifiers.Count - 1].Kind == ModifierKind.Tracking
            && _modifiers[_modifiers.Count - 1].First.HasValue;

        /// <summary>
        /// The coordinate typed alone at "(need X):", "(need Y):" or "(need Z):" of a point filter, a number in the UCS: "-20"
        /// gives X -20 (a Z is left out). Any other answer is a point, whose coordinates asked are taken, as AutoCAD 2021 takes
        /// Y 3 from "40,3" at "(need YZ):".
        /// </summary>
        private bool TryNeeded(string typed, out GeoPoint2 point)
        {
            point = default;

            if (_modifiers.Count == 0)
            {
                return false;
            }

            PointModifier modifier = _modifiers[_modifiers.Count - 1];

            if (
                modifier.Kind != ModifierKind.Filter
                || !modifier.First.HasValue
                || Needed(modifier.Filter).Length != 1
                || !TryNumber(typed, out double value)
            )
            {
                return false;
            }

            GeoPoint2 first = Ucs.ToUcs(modifier.First.Value);
            string needed = Needed(modifier.Filter);
            point = Ucs.ToWorld(new GeoPoint2(needed == "X" ? value : first.X, needed == "Y" ? value : first.Y));
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
