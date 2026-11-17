using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// The items of a dimension grip's hover menu: Continue Dimension and Baseline Dimension on a linear dimension's extension
    /// line origins and arrowheads, Flip Arrow on its arrowheads, and on the grip of the words Move Text Only, Move with
    /// Leader, Move with Dim Line, Above Dim Line, Center Vertically and Reset Text Position, with the header and prompt of
    /// AutoCAD 2021's grip table (read from the string table of <c>acdb24res.dll</c>).
    /// </summary>
    internal static partial class CadCommands
    {
        /// <summary>Runs one of the dimension items of a grip's menu; all the edits it makes are one undo step.</summary>
        private static IEnumerable<CadPrompt> GripDimension(
            CadCommandContext c,
            string handle,
            int gripIndex,
            CadGripAction action
        )
        {
            var dimension = (DxfDimension)c.Document.FindEntity(handle);
            IReadOnlyList<CadGrip> grips = CadGrips.Of(dimension);

            switch (action)
            {
                case CadGripAction.ContinueDimension:
                case CadGripAction.BaselineDimension:
                {
                    // The row goes on from the extension line origin on the side of the grip: the first for grips 0 and 2, the
                    // second for 1 and 3.
                    GeoPoint2 origin = grips[gripIndex == 1 || gripIndex == 3 ? 1 : 0].Point;

                    foreach (
                        CadPrompt prompt in DimChain(
                            c,
                            action == CadGripAction.BaselineDimension,
                            false,
                            dimension,
                            origin
                        )
                    )
                    {
                        yield return prompt;
                    }

                    yield break;
                }

                case CadGripAction.FlipArrow:
                {
                    c.Write("** FLIP ARROW**");
                    yield return CadPrompt.ForPoint("Pick to flip arrowhead");

                    if (!c.Input.IsPoint)
                    {
                        yield break;
                    }

                    bool first = gripIndex == 2;
                    ChangeDimensions(
                        c,
                        Changeable(c, new[] { handle }, "Flip arrow", d => true),
                        "Flip arrow",
                        first ? "First" : "Second",
                        d =>
                        {
                            if (first)
                            {
                                d.FlipFirstArrow = !d.FlipFirstArrow;
                            }
                            else
                            {
                                d.FlipSecondArrow = !d.FlipSecondArrow;
                            }
                        }
                    );
                    yield break;
                }

                case CadGripAction.MoveTextOnly:
                case CadGripAction.MoveWithLeader:
                case CadGripAction.MoveWithDimLine:
                {
                    int moves =
                        action == CadGripAction.MoveTextOnly ? 2
                        : action == CadGripAction.MoveWithLeader ? 1
                        : 0;
                    c.Write(
                        action == CadGripAction.MoveTextOnly ? "** MOVE TEXT ONLY **"
                        : action == CadGripAction.MoveWithLeader ? "** MOVE WITH LEADER **"
                        : "** MOVE WITH DIM LINE **"
                    );
                    yield return CadPrompt.ForPoint("Specify destination point", grips[gripIndex].Point);

                    if (!c.Input.IsPoint)
                    {
                        yield break;
                    }

                    GeoPoint2 at = c.Input.Point;

                    // The words go to the point as DIMTEDIT puts them there for the DIMTMOVE the item stands for, kept on the
                    // dimension as an override where the style's is another, so that it is drawn so again.
                    ChangeDimensions(
                        c,
                        Changeable(c, new[] { handle }, "Text position", d => true),
                        "Text position",
                        Values(at.X, at.Y),
                        d =>
                        {
                            if (d.Dimtmove != moves)
                            {
                                Overridden(d, DxfDimVariable.Named("Dimtmove"), moves);
                            }

                            MoveWords(d, at);
                        }
                    );
                    yield break;
                }

                case CadGripAction.AboveDimLine:
                case CadGripAction.CenterVertically:
                {
                    bool above = action == CadGripAction.AboveDimLine;
                    c.Write(above ? "** ABOVE DIMENSION LINE **" : "** CENTER VERTICALLY **");
                    yield return CadPrompt.ForPoint(
                        above
                            ? "Pick to place text above dimension line"
                            : "Pick to center text vertically with dimension line"
                    );

                    if (!c.Input.IsPoint)
                    {
                        yield break;
                    }

                    // DIMTAD 1 puts the words above the line, 0 on it; words put by hand along a linear dimension's line are
                    // brought back onto it, where DIMTAD stands them.
                    ChangeDimensions(
                        c,
                        Changeable(c, new[] { handle }, "Text position", d => true),
                        "Text position",
                        above ? "Above" : "Center",
                        d =>
                        {
                            int wanted = above ? 1 : 0;

                            if (d.Dimtad != wanted)
                            {
                                Overridden(d, DxfDimVariable.Named("Dimtad"), wanted);
                            }

                            if (!d.UsingDefaultTextPosition && d is DxfRotatedDimension rotated)
                            {
                                rotated.TextPosition = P3(
                                    OnTheLine(P2(rotated.DimLinePoint), rotated.Rotation, P2(rotated.TextPosition))
                                );
                            }
                            else if (!d.UsingDefaultTextPosition && d is DxfAlignedDimension aligned)
                            {
                                aligned.TextPosition = P3(
                                    OnTheLine(P2(aligned.DimLinePoint), LineAngleOf(aligned), P2(aligned.TextPosition))
                                );
                            }
                        }
                    );
                    yield break;
                }

                default:
                {
                    c.Write("** RESET TEXT POSITION **");
                    yield return CadPrompt.ForPoint("Pick to reset text to default location");

                    if (!c.Input.IsPoint)
                    {
                        yield break;
                    }

                    ChangeDimensions(
                        c,
                        Changeable(c, new[] { handle }, "Text position", d => true),
                        "Text position",
                        "Home",
                        d =>
                        {
                            d.UsingDefaultTextPosition = true;

                            // A radius or a diameter is back at its default place with a leader of nought, as DIMTEDIT Home leaves it.
                            if (d is DxfRadialDimension radial)
                            {
                                radial.LeaderLength = 0.0;
                            }
                            else if (d is DxfDiametricDimension diametric)
                            {
                                diametric.LeaderLength = 0.0;
                            }
                        }
                    );
                    yield break;
                }
            }
        }

        /// <summary>Where a point falls on the line through a point at an angle, square to it.</summary>
        private static GeoPoint2 OnTheLine(GeoPoint2 onLine, double angle, GeoPoint2 point)
        {
            var along = new GeoVector2(Math.Cos(angle), Math.Sin(angle));
            return onLine + along * ((point - onLine).X * along.X + (point - onLine).Y * along.Y);
        }
    }
}
