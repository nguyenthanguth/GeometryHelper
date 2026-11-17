using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// DIMINSPECT and -DIMINSPECT: inspection dimensions, the label and rate of inspection AutoCAD shows beside a dimension's
    /// words in a frame. -DIMINSPECT asks "Add inspection data or [Remove] &lt;Add&gt;:", "Select dimensions:", "Enter shape
    /// option [Round/Angular/None] &lt;Round&gt;:", "Enter label data or &lt;None&gt;:" and "Enter inspection rate &lt;100%&gt;:"; Remove
    /// takes the inspection out of the dimensions picked and says "1 of inspection removed". DIMINSPECT picks the dimensions
    /// and shows the Inspection Dimension dialog (<see cref="CadDimInspectRequest"/>), with the same choices; with no window
    /// it points to -DIMINSPECT. The data is kept in the dimension as AutoCAD keeps it (<see cref="CadDimInspection"/>) and
    /// the dimension is drawn again with it: the words in a frame of round or pointed ends between the label and the rate
    /// (<see cref="Inspected"/>), the line of the dimension cut where the frame begins.
    /// </summary>
    internal static partial class CadCommands
    {
        private static IEnumerable<CadPrompt> DimInspect(CadCommandContext c)
        {
            foreach (CadPrompt prompt in c.SelectObjectsOf("Select dimensions", e => e is DxfDimension))
            {
                yield return prompt;
            }

            List<DxfDimension> dimensions = InspectedDimensions(c);

            if (dimensions.Count == 0)
            {
                c.Write("Nothing selected.");
                yield break;
            }

            var request = new CadDimInspectRequest(dimensions);

            if (!c.Session.Request("DIMINSPECT", c.Document, request))
            {
                c.Write("DIMINSPECT is shown by the window: -DIMINSPECT asks its questions on the command line.");
                yield break;
            }

            if (request.Action != CadDialogAction.Ok)
            {
                yield break;
            }

            if (request.Remove)
            {
                RemoveInspection(c, dimensions);
                yield break;
            }

            AddInspection(
                c,
                dimensions,
                request.Shape,
                request.ShowLabel ? request.Label : string.Empty,
                string.IsNullOrWhiteSpace(request.Rate) ? "100%" : request.Rate.Trim()
            );
        }

        private static IEnumerable<CadPrompt> DimInspectTyped(CadCommandContext c)
        {
            yield return CadPrompt.ForKeyword("Add inspection data or", new[] { "Remove" }, "Add").Hiding("Add");

            bool remove = c.Input.Is("Remove");

            if (!remove && !c.Input.Is("Add") && !c.Input.IsNone)
            {
                yield break;
            }

            foreach (CadPrompt prompt in c.SelectObjectsOf("Select dimensions", e => e is DxfDimension))
            {
                yield return prompt;
            }

            List<DxfDimension> dimensions = InspectedDimensions(c);

            if (dimensions.Count == 0)
            {
                yield break;
            }

            if (remove)
            {
                RemoveInspection(c, dimensions);
                yield break;
            }

            yield return CadPrompt.ForKeyword("Enter shape option", new[] { "Round", "Angular", "None" }, "Round");
            CadInspectionShape shape =
                c.Input.Is("Angular") ? CadInspectionShape.Angular
                : c.Input.Is("None") ? CadInspectionShape.None
                : CadInspectionShape.Round;

            yield return CadPrompt.ForString("Enter label data or", "None");
            string label = c.Input.Kind == CadInputKind.Text ? c.Input.Text.Trim() : string.Empty;
            bool shown = label.Length > 0;
            yield return CadPrompt.ForString("Enter inspection rate", "100%");
            string rate =
                c.Input.Kind == CadInputKind.Text && c.Input.Text.Trim().Length > 0 ? c.Input.Text.Trim() : "100%";

            // Enter at the label keeps the label a dimension had, with no flag to show it, as AutoCAD 2021 does.
            AddInspection(c, dimensions, shape, label, rate, shown);
        }

        /// <summary>The dimensions among the objects selected, in the order they were chosen.</summary>
        private static List<DxfDimension> InspectedDimensions(CadCommandContext c) =>
            c.Selected.Select(h => c.Document.FindEntity(h)).OfType<DxfDimension>().ToList();

        /// <summary>Writes an inspection into the dimensions and draws each again, as one edit.</summary>
        private static void AddInspection(
            CadCommandContext c,
            List<DxfDimension> dimensions,
            CadInspectionShape shape,
            string label,
            string rate,
            bool? showLabel = null
        )
        {
            ChangeDimensions(
                c,
                dimensions,
                "Inspection",
                ((int)shape).ToString(CultureInfo.InvariantCulture) + ";" + label + ";" + rate,
                dimension =>
                {
                    CadDimInspection data = CadDimInspection.Read(dimension) ?? new CadDimInspection();
                    data.Shape = shape;
                    data.Rate = rate;
                    bool show = showLabel ?? label.Length > 0;

                    // A label typed replaces the label kept; none typed keeps the one kept, shown only where one was typed.
                    if (label.Length > 0)
                    {
                        data.Label = label;
                    }

                    data.ShowLabel = show && label.Length > 0;
                    data.Write(dimension);
                }
            );
        }

        /// <summary>Takes the inspection out of the dimensions, saying how many had one, as AutoCAD 2021 does.</summary>
        private static void RemoveInspection(CadCommandContext c, List<DxfDimension> dimensions)
        {
            int removed = 0;
            ChangeDimensions(c, dimensions, "Inspection", string.Empty, dimension =>
            {
                if (CadDimInspection.Remove(dimension))
                {
                    removed++;
                }
            });
            c.Write(removed.ToString(CultureInfo.InvariantCulture) + " of inspection removed");
        }

        /// <summary>
        /// Draws the inspection of a dimension about its words, as AutoCAD 2021 draws it: the label, the words and the rate side
        /// by side, a gap (DIMGAP) either side of each, the whole centred on the place the words stood and the words moved to
        /// their place in it. A round or an angular frame of the height of the words and two gaps is drawn about them, with a
        /// dividing line after the label (where there is one) and before the rate, its ends round or pointed; the lines of the
        /// dimension that were cut at the words end where the frame ends, as AutoCAD's do. A dimension with no inspection is
        /// left as it is. 100.0000 of 0.18 high with the label A and the rate 50 stands 1.77 wide between the centres of the
        /// round ends, the words moved 0.075 to the left of the middle of the line.
        /// </summary>
        private static void Inspected(
            DxfDimension dimension,
            DxfDimStyleTableRecord style,
            List<DxfEntity> picture,
            DxfMText words
        )
        {
            CadDimInspection inspection = CadDimInspection.Read(dimension);

            if (inspection == null || words == null)
            {
                return;
            }

            double scale = style.Dimscale > 0.0 ? style.Dimscale : 1.0;
            double gap = Math.Abs(style.Dimgap) * scale;
            double height = words.TextHeight;
            string plain = Regex.Replace(words.Contents ?? string.Empty, @"\\[A-Za-z][^;]*;", string.Empty);
            double width = WordsWidth(style, plain, height, out double tall);
            string label = inspection.ShownLabel;
            string rate = inspection.Rate ?? string.Empty;
            double labelWidth = label.Length > 0 ? WordsWidth(style, label, height, out _) : 0.0;
            double rateWidth = rate.Length > 0 ? WordsWidth(style, rate, height, out _) : 0.0;
            bool none = inspection.Shape == CadInspectionShape.None;
            bool round = inspection.Shape == CadInspectionShape.Round;
            double half = (tall + 2.0 * gap) / 2.0;

            // Along the words, from the left end of the whole: its pieces and their widths.
            double leftTip = none ? gap : half;
            double leftPart = leftTip + (label.Length > 0 ? labelWidth + 2.0 * gap : (none ? 0.0 : gap));
            double rightPart = 2.0 * gap + rateWidth + (none ? gap : half);
            double total = leftPart + width + rightPart;
            double start = -total / 2.0;
            double wordsAt = start + leftPart + width / 2.0;
            double rotation = words.Rotation;
            var along = new GeoVector2(Math.Cos(rotation), Math.Sin(rotation));
            var up = new GeoVector2(-along.Y, along.X);
            GeoPoint2 middle = P2(words.Location);
            GeoPoint2 At(double s, double t) => middle + along * s + up * t;
            words.Location = P3(At(wordsAt, 0.0));

            // The dimension line that was cut at the words stops where the frame stops.
            foreach (DxfLine piece in picture.OfType<DxfLine>())
            {
                GeoVector2 a = P2(piece.StartPoint) - middle;
                GeoVector2 b = P2(piece.EndPoint) - middle;
                double sa = a.X * along.X + a.Y * along.Y;
                double sb = b.X * along.X + b.Y * along.Y;
                double ta = a.X * up.X + a.Y * up.Y;
                double tb = b.X * up.X + b.Y * up.Y;

                if (Math.Abs(ta) > 1E-9 || Math.Abs(tb) > 1E-9)
                {
                    continue;
                }

                double near = Math.Max(sa, sb);
                double far = Math.Min(sa, sb);

                if (near <= 1E-9 && near > -total)
                {
                    SetEnd(piece, sa >= sb, P3(At(Math.Min(near, start), 0.0)));
                }
                else if (far >= -1E-9 && far < total)
                {
                    SetEnd(piece, sa <= sb, P3(At(Math.Max(far, total / 2.0), 0.0)));
                }
            }

            double leftCentre = start + half;
            double rightCentre = total / 2.0 - half;
            double dividerLeft = start + leftTip + labelWidth + gap;
            double dividerRight = wordsAt + width / 2.0 + gap;
            var frame = new List<DxfEntity>();

            DxfLine Side(double s1, double t1, double s2, double t2) =>
                Lined(new DxfLine(P3(At(s1, t1)), P3(At(s2, t2))), style.Dimclrd, style.Dimlwd);

            if (!none)
            {
                frame.Add(Side(leftCentre, -half, rightCentre, -half));
                frame.Add(Side(dividerRight, -half, dividerRight, half));
                frame.Add(Side(rightCentre, half, leftCentre, half));

                if (label.Length > 0)
                {
                    frame.Add(Side(dividerLeft, half, dividerLeft, -half));
                }

                if (round)
                {
                    frame.Add(
                        Lined(
                            new DxfArc(
                                P3(At(rightCentre, 0.0)),
                                half,
                                rotation - Math.PI / 2.0,
                                rotation + Math.PI / 2.0
                            ),
                            style.Dimclrd,
                            style.Dimlwd
                        )
                    );
                    frame.Add(
                        Lined(
                            new DxfArc(
                                P3(At(leftCentre, 0.0)),
                                half,
                                rotation + Math.PI / 2.0,
                                rotation + 3.0 * Math.PI / 2.0
                            ),
                            style.Dimclrd,
                            style.Dimlwd
                        )
                    );
                }
                else
                {
                    frame.Add(Side(rightCentre, -half, total / 2.0, 0.0));
                    frame.Add(Side(total / 2.0, 0.0, rightCentre, half));
                    frame.Add(Side(leftCentre, half, start, 0.0));
                    frame.Add(Side(start, 0.0, leftCentre, -half));
                }
            }

            if (label.Length > 0)
            {
                frame.Add(Words(style, At(start + leftTip + labelWidth / 2.0, 0.0), rotation, height, label, scale));
            }

            if (rate.Length > 0)
            {
                frame.Add(
                    Words(
                        style,
                        At(total / 2.0 - (none ? gap : half) - rateWidth / 2.0, 0.0),
                        rotation,
                        height,
                        rate,
                        scale
                    )
                );
            }

            int index = picture.IndexOf(words);
            picture.InsertRange(index + 1, frame);
        }

        /// <summary>Moves the end of a line that points towards the words, the start where it is the start.</summary>
        private static void SetEnd(DxfLine line, bool startIsNear, GeoPoint3 point)
        {
            if (startIsNear)
            {
                line.StartPoint = point;
            }
            else
            {
                line.EndPoint = point;
            }
        }
    }
}
