using System.Linq;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// A distance or an angle asked with no base point and given by two points, as AutoCAD 2021 takes them: a point typed or
    /// picked at "Enter new value for POLARDIST &lt;0.0000&gt;:" asks "Specify second point:", a rubber band drawn from the first,
    /// and the distance between the two, or the direction from the first to the second, answers the prompt. 0,0 then 3,4
    /// sets POLARDIST to 5; 0,0 then 1,1 sets SNAPANG to 45.
    /// </summary>
    public sealed partial class CadCommandSession
    {
        // The prompt for a distance or an angle a first point answered, while "Specify second point:" asks the other; null else.
        private CadPrompt _secondFor;

        // The first point, and the last point (LASTPOINT) as it was before it, which the two points leave as they found it.
        private GeoPoint2 _secondFrom;
        private GeoPoint2 _secondLast;

        /// <summary>
        /// A first point given at a prompt for a distance or an angle with no base point: "Specify second point:" asked, its
        /// rubber band from the point. A point typed there as @dx,dy is measured from the first, and the prompt's keywords are
        /// taken there too, as AutoCAD takes T for OFFSET's Through.
        /// </summary>
        private bool StartSecondPoint(GeoPoint2 first, string echo)
        {
            if (echo != null)
            {
                Write(echo);
            }

            CadPrompt outer = Prompt;
            _secondFor = outer;
            _secondFrom = first;
            _secondLast = LastPoint;
            LastPoint = first;
            Prompt = CadPrompt
                .ForPoint("Specify second point", first)
                .Hiding(outer.Keywords.ToArray())
                .Refusing(
                    outer.Kind == CadPromptKind.Angle ? "Requires valid numeric angle or two points." : Refused(outer)
                );
            Changed();
            return true;
        }

        /// <summary>
        /// The answer to "Specify second point:": a point gives the distance to it from the first, or its direction, and the
        /// prompt that asked takes that; a keyword is the prompt's own, and Enter its default, as though answered there. The
        /// last point is put back as it was.
        /// </summary>
        private bool TakeSecondPoint(CadInput input, string echo)
        {
            if (echo != null)
            {
                Write(echo);
            }

            CadPrompt outer = _secondFor;
            GeoPoint2 first = _secondFrom;
            LastPoint = _secondLast;
            _secondFor = null;
            Prompt = outer;
            CadInput answer = input;

            if (input.IsPoint)
            {
                GeoVector2 way = input.Point - first;
                double number = outer.Kind == CadPromptKind.Angle ? PickedAngle(outer, way) : way.Length;
                answer = new CadInput(CadInputKind.Number, input.Point, number, input.Text, null)
                {
                    Control = input.Control,
                };
            }
            else if (input.IsNone)
            {
                string keyword = outer.MatchKeyword(outer.Default);
                answer =
                    keyword != null
                        ? new CadInput(CadInputKind.Keyword, default, 0.0, keyword, null)
                        : CadInput.Nothing;
            }

            _trackPoints.Clear();
            _context.Input = answer;
            Advance();
            return true;
        }

        /// <summary>The second point let go as the command ends, the last point put back as it was.</summary>
        private void DropSecondPoint()
        {
            if (_secondFor != null)
            {
                LastPoint = _secondLast;
                _secondFor = null;
            }
        }

        /// <summary>
        /// AutoCAD 2021's words for text a prompt for a distance cannot take: its own where it has them; with a base point
        /// "Requires numeric distance, second point, or option keyword."; with none "Requires numeric distance or two
        /// points.", or "Requires numeric distance, two points, or option keyword." where it has options.
        /// </summary>
        private static string Refused(CadPrompt prompt) =>
            prompt.Refusal
            ?? (
                prompt.BasePoint != null ? "Requires numeric distance, second point, or option keyword."
                : prompt.Keywords.Count > 0 ? "Requires numeric distance, two points, or option keyword."
                : "Requires numeric distance or two points."
            );
    }
}
