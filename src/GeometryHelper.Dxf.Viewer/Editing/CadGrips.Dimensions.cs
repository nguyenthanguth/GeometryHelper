using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>The grips of dimensions, as AutoCAD 2021 gives them, and what dragging each writes into the dimension.</summary>
    public static partial class CadGrips
    {
        /// <summary>Adds the grips of a dimension in the order <see cref="Of"/> gives them; none for one of another kind, out of the plan or with no picture.</summary>
        private static void DimensionGrips(DxfDimension dimension, Action<CadGripKind, GeoPoint2> add)
        {
            if (dimension.DimBlock == null || !IsPlan(dimension.Normal) || dimension.Normal.Z < 0.0)
            {
                return;
            }

            switch (dimension)
            {
                case DxfRotatedDimension _:
                case DxfAlignedDimension _:
                    {
                        LinearFrame(dimension, out GeoPoint2 p1, out GeoPoint2 p2, out GeoPoint2 onLine, out GeoVector2 along, out double oblique);
                        add(CadGripKind.DefinitionPoint, p1);
                        add(CadGripKind.DefinitionPoint, p2);
                        add(CadGripKind.DimensionLine, CadCommands.Foot(p1, onLine, along, oblique));
                        add(CadGripKind.DimensionLine, CadCommands.Foot(p2, onLine, along, oblique));
                        add(CadGripKind.Text, WordsAt(dimension));
                        break;
                    }

                case DxfRadialDimension radial:
                    add(CadGripKind.DefinitionPoint, P2(radial.ChordPoint));
                    add(CadGripKind.Text, WordsAt(dimension));
                    break;
                case DxfDiametricDimension diametric:
                    add(CadGripKind.DefinitionPoint, P2(diametric.ChordPoint));
                    add(CadGripKind.DefinitionPoint, P2(diametric.FarChordPoint));
                    add(CadGripKind.Text, WordsAt(dimension));
                    break;
                case DxfRadialDimensionLarge jogged:
                    add(CadGripKind.DefinitionPoint, P2(jogged.ChordPoint));
                    add(CadGripKind.DefinitionPoint, P2(jogged.OverrideCenter));
                    add(CadGripKind.Jog, P2(jogged.JogPoint));
                    add(CadGripKind.Text, WordsAt(dimension));
                    break;
                case DxfPoint3AngularDimension three:
                    add(CadGripKind.DefinitionPoint, P2(three.CenterPoint));
                    add(CadGripKind.DefinitionPoint, P2(three.XLine1Point));
                    add(CadGripKind.DefinitionPoint, P2(three.XLine2Point));
                    add(CadGripKind.DimensionLine, P2(three.ArcPoint));
                    add(CadGripKind.Text, WordsAt(dimension));
                    break;
                case DxfLineAngularDimension2 two:
                    add(CadGripKind.DefinitionPoint, P2(two.XLine1Start));
                    add(CadGripKind.DefinitionPoint, P2(two.XLine1End));
                    add(CadGripKind.DefinitionPoint, P2(two.XLine2Start));
                    add(CadGripKind.DefinitionPoint, P2(two.XLine2End));
                    add(CadGripKind.DimensionLine, P2(two.ArcPoint));
                    add(CadGripKind.Text, WordsAt(dimension));
                    break;
                case DxfOrdinateDimension ordinate:
                    add(CadGripKind.DefinitionPoint, P2(ordinate.DefiningPoint));
                    add(CadGripKind.DefinitionPoint, P2(ordinate.LeaderEndPoint));
                    break;
            }
        }

        /// <summary>
        /// Where a dimension's words stand: where the user put them, else the middle of the words of its picture (its MTEXT,
        /// put where the picture is inserted), else its text point.
        /// </summary>
        private static GeoPoint2 WordsAt(DxfDimension dimension)
        {
            if (!dimension.UsingDefaultTextPosition)
            {
                return P2(dimension.TextPosition);
            }

            DxfMText words = dimension.DimBlock.OfType<DxfMText>().FirstOrDefault(m => !m.IsErased);

            if (words == null)
            {
                return P2(dimension.TextPosition);
            }

            GeoPoint3 inserted = dimension.DimBlockPosition;
            return new GeoPoint2(words.Location.X + inserted.X, words.Location.Y + inserted.Y);
        }

        /// <summary>A linear or aligned dimension's points: its extension line origins, the point of its line, the line's direction and the slant of its extension lines.</summary>
        private static void LinearFrame(DxfDimension dimension, out GeoPoint2 p1, out GeoPoint2 p2, out GeoPoint2 onLine, out GeoVector2 along, out double oblique)
        {
            oblique = CadCommands.ObliqueOf(dimension);

            if (dimension is DxfRotatedDimension rotated)
            {
                p1 = P2(rotated.XLine1Point);
                p2 = P2(rotated.XLine2Point);
                onLine = P2(rotated.DimLinePoint);
                along = new GeoVector2(Math.Cos(rotated.Rotation), Math.Sin(rotated.Rotation));
                return;
            }

            var aligned = (DxfAlignedDimension)dimension;
            p1 = P2(aligned.XLine1Point);
            p2 = P2(aligned.XLine2Point);
            onLine = P2(aligned.DimLinePoint);
            along = Way(p2 - p1, new GeoVector2(1.0, 0.0));
        }

        /// <summary>Drags a grip of a dimension: its points rewritten and its picture drawn anew, one undo step.</summary>
        private static CadEditResult DragDimension(CadDocument document, string handle, int gripIndex, GeoPoint2 from, GeoPoint2 to)
        {
            var dimension = (DxfDimension)document.FindEntity(handle);
            double least = Tolerance.Default.EqualPoint;

            if (!Measures(dimension, gripIndex, to, least))
            {
                return CadEditResult.Refuse("The dimension would measure nothing with its grip there.");
            }

            var record = new CadEditRecord(CadEditRecordKind.Reshaped, handle, "Grip " + gripIndex, CadCommands.Values(from.X, from.Y), CadCommands.Values(to.X, to.Y));
            return document.ApplyEdit("GRIP", new[] { record }, s =>
            {
                if (s.IsHandled(handle))
                {
                    return;
                }

                var changed = (DxfDimension)s.Document.FindEntity(handle);
                CadCommands.OwnPicture(s.Database, changed);
                MoveDimensionPoint(changed, gripIndex, to);

                if (!CadCommands.Redraw(changed))
                {
                    throw new InvalidOperationException("The dimension cannot be drawn with its grip there.");
                }
            });
        }

        /// <summary>Whether a dimension still measures something once a grip is dragged to a point: a length, a radius above nought.</summary>
        private static bool Measures(DxfDimension dimension, int gripIndex, GeoPoint2 to, double least)
        {
            switch (dimension)
            {
                case DxfRotatedDimension _:
                case DxfAlignedDimension _:
                    {
                        if (gripIndex > 1)
                        {
                            return true;
                        }

                        LinearFrame(dimension, out GeoPoint2 p1, out GeoPoint2 p2, out _, out GeoVector2 along, out _);
                        GeoVector2 span = (gripIndex == 1 ? to : p2) - (gripIndex == 0 ? to : p1);
                        return dimension is DxfAlignedDimension ? span.Length > least : Math.Abs(span.X * along.X + span.Y * along.Y) > least;
                    }

                case DxfRadialDimension radial when gripIndex == 0:
                    return (to - P2(radial.Center)).Length > least;
                case DxfDiametricDimension diametric when gripIndex <= 1:
                    return (to - Middle(P2(diametric.ChordPoint), P2(diametric.FarChordPoint))).Length > least;
                case DxfRadialDimensionLarge jogged when gripIndex == 0 || gripIndex == 3:
                    return (to - P2(jogged.Center)).Length > least;
                case DxfOrdinateDimension ordinate:
                    return ((gripIndex == 0 ? P2(ordinate.LeaderEndPoint) : P2(ordinate.DefiningPoint)) - to).Length > least;
                default:
                    return true;
            }
        }

        /// <summary>Writes into a dimension what dragging one of its grips to a point does.</summary>
        private static void MoveDimensionPoint(DxfDimension dimension, int gripIndex, GeoPoint2 to)
        {
            switch (dimension)
            {
                case DxfRotatedDimension _:
                case DxfAlignedDimension _:
                    MoveLinear(dimension, gripIndex, to);
                    break;
                case DxfRadialDimension radial:
                    if (gripIndex == 0)
                    {
                        GeoPoint2 center = P2(radial.Center);
                        GeoPoint2 chord = P2(radial.ChordPoint);
                        GeoPoint2 moved = center + Way(to - center, chord - center) * (chord - center).Length;
                        TurnWords(dimension, center, chord, moved);
                        radial.ChordPoint = At(moved, radial.ChordPoint);
                    }
                    else
                    {
                        CadCommands.DragWords(dimension, to);
                    }

                    break;
                case DxfDiametricDimension diametric:
                    if (gripIndex <= 1)
                    {
                        GeoPoint2 near = P2(diametric.ChordPoint);
                        GeoPoint2 far = P2(diametric.FarChordPoint);
                        GeoPoint2 middle = Middle(near, far);
                        GeoPoint2 dragged = gripIndex == 0 ? near : far;
                        GeoPoint2 moved = middle + Way(to - middle, dragged - middle) * (dragged - middle).Length;
                        GeoPoint2 across = middle - (moved - middle);
                        TurnWords(dimension, middle, dragged, moved);
                        diametric.ChordPoint = At(gripIndex == 0 ? moved : across, diametric.ChordPoint);
                        diametric.FarChordPoint = At(gripIndex == 0 ? across : moved, diametric.FarChordPoint);
                    }
                    else
                    {
                        CadCommands.DragWords(dimension, to);
                    }

                    break;
                case DxfRadialDimensionLarge jogged:
                    CadCommands.MoveJogged(jogged, gripIndex, to);
                    break;
                case DxfPoint3AngularDimension three:
                    switch (gripIndex)
                    {
                        case 0: three.CenterPoint = At(to, three.CenterPoint); break;
                        case 1: three.XLine1Point = At(to, three.XLine1Point); break;
                        case 2: three.XLine2Point = At(to, three.XLine2Point); break;
                        case 3: three.ArcPoint = At(to, three.ArcPoint); break;
                        default: CadCommands.DragWords(dimension, to); break;
                    }

                    break;
                case DxfLineAngularDimension2 two:
                    switch (gripIndex)
                    {
                        case 0: two.XLine1Start = At(to, two.XLine1Start); break;
                        case 1: two.XLine1End = At(to, two.XLine1End); break;
                        case 2: two.XLine2Start = At(to, two.XLine2Start); break;
                        case 3: two.XLine2End = At(to, two.XLine2End); break;
                        case 4: two.ArcPoint = At(to, two.ArcPoint); break;
                        default: CadCommands.DragWords(dimension, to); break;
                    }

                    break;
                case DxfOrdinateDimension ordinate:
                    if (gripIndex == 0)
                    {
                        ordinate.DefiningPoint = At(to, ordinate.DefiningPoint);
                    }
                    else
                    {
                        // Words the user placed go with the leader's end.
                        GeoVector2 shift = to - P2(ordinate.LeaderEndPoint);
                        ordinate.LeaderEndPoint = At(to, ordinate.LeaderEndPoint);
                        Shift(dimension, shift);
                    }

                    break;
            }
        }

        /// <summary>
        /// A linear or aligned dimension's grip dragged: an extension line origin moved, the dimension line kept where it is (an
        /// aligned one's at the distance it stood from its second origin); an end of the line moving the line through the
        /// point, placed words with it; the words moved as DIMTEDIT moves them.
        /// </summary>
        private static void MoveLinear(DxfDimension dimension, int gripIndex, GeoPoint2 to)
        {
            if (gripIndex == 4)
            {
                CadCommands.DragWords(dimension, to);
                return;
            }

            LinearFrame(dimension, out GeoPoint2 p1, out GeoPoint2 p2, out GeoPoint2 onLine, out GeoVector2 along, out double oblique);
            GeoPoint2 line;

            if (gripIndex >= 2)
            {
                line = CadCommands.Foot(p2, to, along, oblique);
                Shift(dimension, line - CadCommands.Foot(p2, onLine, along, oblique));
            }
            else
            {
                GeoPoint2 n1 = gripIndex == 0 ? to : p1;
                GeoPoint2 n2 = gripIndex == 1 ? to : p2;

                if (dimension is DxfAlignedDimension)
                {
                    // The line stays as far from the second origin, square to what it measures, as it stood.
                    var square = new GeoVector2(-along.Y, along.X);
                    double off = (onLine - p2).X * square.X + (onLine - p2).Y * square.Y;
                    GeoVector2 turned = Way(n2 - n1, along);
                    line = CadCommands.Foot(n2, n2 + new GeoVector2(-turned.Y, turned.X) * off, turned, oblique);
                }
                else
                {
                    line = CadCommands.Foot(n2, onLine, along, oblique);
                }

                SetOrigin(dimension, gripIndex, to);
            }

            if (dimension is DxfRotatedDimension rotated)
            {
                rotated.DimLinePoint = At(line, rotated.DimLinePoint);
            }
            else
            {
                var aligned = (DxfAlignedDimension)dimension;
                aligned.DimLinePoint = At(line, aligned.DimLinePoint);
            }
        }

        private static void SetOrigin(DxfDimension dimension, int gripIndex, GeoPoint2 to)
        {
            if (dimension is DxfRotatedDimension rotated)
            {
                if (gripIndex == 0)
                {
                    rotated.XLine1Point = At(to, rotated.XLine1Point);
                }
                else
                {
                    rotated.XLine2Point = At(to, rotated.XLine2Point);
                }

                return;
            }

            var aligned = (DxfAlignedDimension)dimension;

            if (gripIndex == 0)
            {
                aligned.XLine1Point = At(to, aligned.XLine1Point);
            }
            else
            {
                aligned.XLine2Point = At(to, aligned.XLine2Point);
            }
        }

        /// <summary>Moves words the user placed by a shift; words where the style puts them stay so.</summary>
        private static void Shift(DxfDimension dimension, GeoVector2 shift)
        {
            if (!dimension.UsingDefaultTextPosition)
            {
                dimension.TextPosition = At(P2(dimension.TextPosition) + shift, dimension.TextPosition);
            }
        }

        /// <summary>Turns words the user placed about a centre by the turn from one point to another, as a chord point goes round.</summary>
        private static void TurnWords(DxfDimension dimension, GeoPoint2 center, GeoPoint2 from, GeoPoint2 to)
        {
            if (dimension.UsingDefaultTextPosition)
            {
                return;
            }

            double turn = Math.Atan2(to.Y - center.Y, to.X - center.X) - Math.Atan2(from.Y - center.Y, from.X - center.X);
            GeoVector2 off = P2(dimension.TextPosition) - center;
            double cos = Math.Cos(turn);
            double sin = Math.Sin(turn);
            dimension.TextPosition = At(center + new GeoVector2(off.X * cos - off.Y * sin, off.X * sin + off.Y * cos), dimension.TextPosition);
        }

        /// <summary>A vector made a unit long; the fallback where it has no length.</summary>
        private static GeoVector2 Way(GeoVector2 vector, GeoVector2 fallback)
        {
            double length = vector.Length;

            if (length > 1E-12)
            {
                return vector / length;
            }

            double other = fallback.Length;
            return other > 1E-12 ? fallback / other : new GeoVector2(1.0, 0.0);
        }

        private static GeoPoint2 Middle(GeoPoint2 a, GeoPoint2 b) => new GeoPoint2((a.X + b.X) / 2.0, (a.Y + b.Y) / 2.0);

        private static GeoPoint2 P2(GeoPoint3 point) => new GeoPoint2(point.X, point.Y);

        /// <summary>A point of the plan at the height of the point it takes the place of.</summary>
        private static GeoPoint3 At(GeoPoint2 point, GeoPoint3 was) => new GeoPoint3(point.X, point.Y, was.Z);
    }
}
