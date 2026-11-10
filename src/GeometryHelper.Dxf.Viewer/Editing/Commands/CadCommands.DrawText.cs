using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// TEXT and MTEXT with the options AutoCAD 2021 offers: TEXT's Justify and Style at its start point, and Enter there going on
    /// below the text written last; MTEXT's Height, Justify, Line spacing, Rotation, Style, Width and Columns at its corner.
    /// </summary>
    internal static partial class CadCommands
    {
        /// <summary>TEXT's justifications as its Justify option lists them, each with the point it asks for and its DXF justification.</summary>
        private static readonly string[] DTextJustifications = { "Left", "Center", "Right", "Align", "Middle", "Fit", "TL", "TC", "TR", "ML", "MC", "MR", "BL", "BC", "BR" };

        private static readonly DxfAttachmentPoint[] TextPoints =
        {
            DxfAttachmentPoint.BaseLeft, DxfAttachmentPoint.BaseCenter, DxfAttachmentPoint.BaseRight, DxfAttachmentPoint.BaseAlign,
            DxfAttachmentPoint.BaseMid, DxfAttachmentPoint.BaseFit, DxfAttachmentPoint.TopLeft, DxfAttachmentPoint.TopCenter,
            DxfAttachmentPoint.TopRight, DxfAttachmentPoint.MiddleLeft, DxfAttachmentPoint.MiddleCenter, DxfAttachmentPoint.MiddleRight,
            DxfAttachmentPoint.BottomLeft, DxfAttachmentPoint.BottomCenter, DxfAttachmentPoint.BottomRight,
        };

        private static readonly string[] TextPointPrompts =
        {
            "Specify start point of text", "Specify center point of text", "Specify right endpoint of text baseline", null,
            "Specify middle point of text", null, "Specify top-left point of text", "Specify top-center point of text",
            "Specify top-right point of text", "Specify middle-left point of text", "Specify middle point of text",
            "Specify middle-right point of text", "Specify bottom-left point of text", "Specify bottom-center point of text",
            "Specify bottom-right point of text",
        };

        private static readonly string[] MTextJustifications = { "TL", "TC", "TR", "ML", "MC", "MR", "BL", "BC", "BR" };

        private static IEnumerable<CadPrompt> Text(CadCommandContext c)
        {
            int justify = 0;
            string style;
            DxfTextStyleTableRecord record;
            double fixedHeight;
            double height;
            GeoPoint2 at;
            GeoPoint2 second = default;
            double angle;
            bool goOn = false;

            while (true)
            {
                style = TextStyle(c);
                record = StyleRecord(c, style);
                fixedHeight = record != null && record.TextSize > 0.0 ? record.TextSize : 0.0;
                height = fixedHeight > 0.0 ? fixedHeight : c.Remembered("text.height", TextSize(c));
                c.Write("Current text style:  \"" + (style ?? "Standard") + "\"  Text height:  " + Length(height) + "  Annotative:  No  Justify:  Left");
                yield return CadPrompt.ForPoint("Specify start point of text", null, new[] { "Justify", "Style" });

                if (c.Input.Is("Style"))
                {
                    foreach (CadPrompt prompt in AskTextStyle(c))
                    {
                        yield return prompt;
                    }

                    continue;
                }

                if (c.Input.IsNone)
                {
                    // Enter goes on below the text written last, of its justification, height and turn, as AutoCAD's.
                    if (c.Remembered("text.next", 0.0) == 0.0)
                    {
                        c.Write("Point or option keyword required.");
                        continue;
                    }

                    goOn = true;
                    break;
                }

                if (c.Input.Is("Justify"))
                {
                    yield return CadPrompt.ForKeyword("Enter an option", DTextJustifications);
                    justify = Array.IndexOf(DTextJustifications, c.Input.Text);

                    if (justify < 0)
                    {
                        yield break;
                    }

                    yield return CadPrompt.ForPoint(TextPointPrompts[justify] ?? "Specify first endpoint of text baseline");
                }

                break;
            }

            if (goOn)
            {
                justify = (int)c.Remembered("text.next.justify", 0.0);
                at = new GeoPoint2(c.Remembered("text.next.x", 0.0), c.Remembered("text.next.y", 0.0));
                second = new GeoPoint2(c.Remembered("text.next.x2", 0.0), c.Remembered("text.next.y2", 0.0));
                height = c.Remembered("text.next.height", height);
                angle = c.Remembered("text.next.angle", 0.0);
            }
            else
            {
                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                at = c.Input.Point;
                bool twoPoints = TextPoints[justify] == DxfAttachmentPoint.BaseAlign || TextPoints[justify] == DxfAttachmentPoint.BaseFit;

                if (twoPoints)
                {
                    yield return CadPrompt.ForPoint("Specify second endpoint of text baseline", at);

                    if (!c.Input.IsPoint || (c.Input.Point - at).Length <= c.Session.Tolerance.EqualPoint)
                    {
                        yield break;
                    }

                    second = c.Input.Point;
                }

                // A style of a fixed height (its height not 0) gives every text that height, and AutoCAD does not ask for one; an
                // aligned text takes the height its two points give it.
                if (fixedHeight <= 0.0 && TextPoints[justify] != DxfAttachmentPoint.BaseAlign)
                {
                    GeoPoint2 start = at;
                    double turned = twoPoints ? Math.Atan2(second.Y - at.Y, second.X - at.X) : c.Session.Ucs.AngleToWorld(c.Remembered("text.rotation", 0.0));
                    yield return CadPrompt.ForDistance("Specify height", at, null, Length(height))
                        .Previewing(CadPreview.MakingOne(c, input => TextBox(c, start, input.Number, turned), true));

                    if (c.Input.Kind == CadInputKind.Number)
                    {
                        if (c.Input.Number <= 0.0)
                        {
                            c.Write("Value must be positive and nonzero.");
                            yield break;
                        }

                        height = c.Input.Number;
                    }
                }

                double rotation = c.Remembered("text.rotation", 0.0);

                if (twoPoints)
                {
                    angle = Math.Atan2(second.Y - at.Y, second.X - at.X);
                }
                else
                {
                    GeoPoint2 start = at;
                    double tall = height;
                    yield return CadPrompt.ForAngle("Specify rotation angle of text", at, null, Counted(c, rotation))
                        .Previewing(CadPreview.MakingOne(c, input => TextBox(c, start, tall, c.Session.Ucs.AngleToWorld(input.Number)), true));

                    if (c.Input.Kind == CadInputKind.Number)
                    {
                        rotation = c.Input.Number;
                    }

                    c.Remember("text.rotation", rotation);

                    // The angle typed is from the X axis of the UCS, as AutoCAD's: the text keeps it from the world's.
                    angle = c.Session.Ucs.AngleToWorld(rotation);
                }

                if (fixedHeight <= 0.0 && TextPoints[justify] != DxfAttachmentPoint.BaseAlign)
                {
                    c.Remember("text.height", height);
                }
            }

            // The width, slant and mirroring of the style are the text's own, as AutoCAD's TEXT copies them; a host's width wins.
            double widthFactor = c.Remembered("text.widthFactor", record?.XScale ?? 1.0);
            double oblique = record?.ObliquingAngle ?? 0.0;
            byte flags = record?.FlagBits ?? 0;
            DxfAttachmentPoint justification = TextPoints[justify];

            // Line after line until an empty one, each a TEXT of its own five thirds of the height below the one before, as DTEXT
            // goes on; the lines of one run are one undo step.
            var down = new GeoVector2(Math.Sin(angle), -Math.Cos(angle)) * (height * 5.0 / 3.0);

            while (true)
            {
                yield return CadPrompt.ForString("Enter text");

                if (c.Input.Kind != CadInputKind.Text || c.Input.Text.Length == 0)
                {
                    yield break;
                }

                string words = c.Input.Text;
                GeoPoint2 line = at;
                GeoPoint2 end = second;
                c.Apply(null, s =>
                {
                    var text = new DxfDBText { Position = P3(line), Height = height, Rotation = angle, TextString = words, WidthFactor = widthFactor };

                    if (style != null)
                    {
                        text.TextStyleName = style;
                    }

                    if (oblique != 0.0)
                    {
                        text.Oblique = oblique;
                    }

                    if ((flags & 2) != 0)
                    {
                        text.IsMirroredInX = true;
                    }

                    if ((flags & 4) != 0)
                    {
                        text.IsMirroredInY = true;
                    }

                    // Justified other than left: held by its second point, the end of the baseline for one aligned or fitted.
                    if (justification != DxfAttachmentPoint.BaseLeft)
                    {
                        text.Justify = justification;
                        text.AlignmentPoint = P3(justification == DxfAttachmentPoint.BaseAlign || justification == DxfAttachmentPoint.BaseFit ? end : line);
                    }

                    s.Append(c.Space, Current(c, text));
                });
                at += down;
                second += down;
                c.Remember("text.next", 1.0);
                c.Remember("text.next.justify", justify);
                c.Remember("text.next.x", at.X);
                c.Remember("text.next.y", at.Y);
                c.Remember("text.next.x2", second.X);
                c.Remember("text.next.y2", second.Y);
                c.Remember("text.next.height", height);
                c.Remember("text.next.angle", angle);
            }
        }

        /// <summary>
        /// The box TEXT previews while its height and its rotation are asked, as AutoCAD 2021 shows where the text will stand: a
        /// square of the height on the baseline from the point given, turned by the angle; a height of 2.5 at (10, 10) level
        /// gives the square from (10, 10) to (12.5, 12.5). Null for a height of nothing.
        /// </summary>
        private static DxfPolyline TextBox(CadCommandContext c, GeoPoint2 at, double height, double angle)
        {
            if (!(height > c.Session.Tolerance.EqualPoint))
            {
                return null;
            }

            var along = new GeoVector2(Math.Cos(angle), Math.Sin(angle)) * height;
            var up = new GeoVector2(-along.Y, along.X);
            var box = new DxfPolyline();
            GeoPoint2[] corners = { at, at + along, at + along + up, at + up };

            for (int i = 0; i < corners.Length; i++)
            {
                box.AddVertexAt(i, corners[i], 0.0, 0.0, 0.0);
            }

            box.Closed = true;
            return Current(c, box);
        }

        /// <summary>
        /// The Style option of TEXT and MTEXT: "Enter style name or [?]", a style of the drawing made current (TEXTSTYLE), ? listing
        /// them; a name the drawing has not said so.
        /// </summary>
        private static IEnumerable<CadPrompt> AskTextStyle(CadCommandContext c)
        {
            string current = TextStyle(c) ?? "Standard";
            yield return CadPrompt.ForKeywordOrText("Enter style name or", new[] { "?" }, current);

            if (c.Input.Is("?"))
            {
                yield return CadPrompt.ForString("Enter text style(s) to list", "*");
                var names = new List<string>();

                foreach (DxfTextStyleTableRecord style in c.Database.TextStyleTable)
                {
                    if (!style.IsShapeFile && !string.IsNullOrEmpty(style.Name))
                    {
                        names.Add(style.Name);
                    }
                }

                names.Sort(StringComparer.OrdinalIgnoreCase);
                c.Write("Text styles:");

                foreach (string name in names)
                {
                    DxfTextStyleTableRecord style = c.Database.TextStyleTable[name];
                    c.Write("Style name: \"" + name + "\"  Font files: " + style.FileName);
                    c.Write("   Height: " + Length(style.TextSize) + "  Width factor: " + Length(style.XScale) + "  Obliquing angle: "
                        + Number(style.ObliquingAngle * 180.0 / Math.PI));
                }

                c.Write("Current text style: \"" + current + "\"");
                yield break;
            }

            string typed = c.Input.Kind == CadInputKind.Text ? c.Input.Text.Trim() : current;

            if (!c.Database.TextStyleTable.Has(typed))
            {
                c.Write("Cannot find text style \"" + typed + "\".");
                yield break;
            }

            string set = c.Database.TextStyleTable[typed].Name;

            if (!string.Equals(set, TextStyle(c), StringComparison.Ordinal))
            {
                string old = c.Database.Textstyle;
                c.Apply(new[] { new CadEditRecord(CadEditRecordKind.SettingChanged, null, "$TEXTSTYLE", old, set) }, s => s.Database.Textstyle = set);
            }
        }

        private static IEnumerable<CadPrompt> MText(CadCommandContext c)
        {
            string style = TextStyle(c);
            DxfTextStyleTableRecord record = StyleRecord(c, style);
            double height = record != null && record.TextSize > 0.0 ? record.TextSize : c.Remembered("text.height", TextSize(c));
            c.Write("Current text style:  \"" + (style ?? "Standard") + "\"  Text height:  " + Length(height) + "  Annotative:  No");
            yield return CadPrompt.ForPoint("Specify first corner");

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            GeoPoint2 a = c.Input.Point;
            CadUcs ucs = c.Session.Ucs;
            string attach = "TL";
            string spacing = "At least";
            double factor = 1.0;
            double rotation = 0.0;
            double? width = null;
            DxfColumnType columns = DxfColumnType.NoColumns;
            double columnWidth = 0.0;
            double gutter = 0.0;
            int count = 0;
            double columnHeight = 0.0;
            string[] options = { "Height", "Justify", "Line spacing", "Rotation", "Style", "Width", "Columns" };

            while (true)
            {
                // The frame of the text the opposite corner at the cursor would give, square to the text's rotation in the UCS,
                // as AutoCAD 2021 draws it while the corner is asked.
                double framed = rotation;
                yield return CadPrompt.ForPoint("Specify opposite corner", a, options).Previewing(CadPreview.MakingOne(c, input =>
                {
                    GeoPoint2 from = ucs.ToUcs(a);
                    GeoVector2 way = ucs.ToUcs(input.Point) - from;
                    var along = new GeoVector2(Math.Cos(framed), Math.Sin(framed));
                    var up = new GeoVector2(-along.Y, along.X);
                    double w = way.X * along.X + way.Y * along.Y;
                    double h = way.X * up.X + way.Y * up.Y;
                    var frame = new DxfPolyline();
                    frame.AddVertexAt(0, ucs.ToWorld(from), 0.0, 0.0, 0.0);
                    frame.AddVertexAt(1, ucs.ToWorld(from + along * w), 0.0, 0.0, 0.0);
                    frame.AddVertexAt(2, ucs.ToWorld(from + along * w + up * h), 0.0, 0.0, 0.0);
                    frame.AddVertexAt(3, ucs.ToWorld(from + up * h), 0.0, 0.0, 0.0);
                    frame.Closed = true;
                    return Current(c, frame);
                }));

                if (c.Input.Is("Height"))
                {
                    yield return CadPrompt.ForDistance("Specify height", a, null, Length(height));

                    if (c.Input.Kind == CadInputKind.Number && c.Input.Number > 0.0)
                    {
                        height = c.Input.Number;
                    }
                }
                else if (c.Input.Is("Justify"))
                {
                    yield return CadPrompt.ForKeyword("Enter justification", MTextJustifications, attach);

                    if (c.Input.Kind == CadInputKind.Keyword)
                    {
                        attach = c.Input.Text;
                    }
                }
                else if (c.Input.Is("Line spacing"))
                {
                    yield return CadPrompt.ForKeyword("Enter line spacing type", new[] { "At least", "Exactly" }, spacing);

                    if (c.Input.Kind == CadInputKind.Keyword)
                    {
                        spacing = c.Input.Text;
                    }

                    // A number with an x after it is a factor of the spacing of the height; one without is the distance between lines.
                    yield return CadPrompt.ForString("Enter line spacing factor or distance", Number(factor) + "x");

                    if (c.Input.Kind == CadInputKind.Text && c.Input.Text.Trim().Length > 0)
                    {
                        string typed = c.Input.Text.Trim();
                        bool times = typed.EndsWith("x", StringComparison.OrdinalIgnoreCase);

                        if (double.TryParse(times ? typed.Substring(0, typed.Length - 1) : typed, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) && value > 0.0)
                        {
                            double asked = times ? value : value / (height * 5.0 / 3.0);

                            if (asked < 0.25 - 1E-9 || asked > 4.0 + 1E-9)
                            {
                                c.Write("Requires a value between 0.25x and 4x.");
                            }
                            else
                            {
                                factor = Math.Max(0.25, Math.Min(4.0, asked));
                            }
                        }
                        else
                        {
                            c.Write("Requires a value between 0.25x and 4x.");
                        }
                    }
                }
                else if (c.Input.Is("Rotation"))
                {
                    yield return CadPrompt.ForAngle("Specify rotation angle", a, null, Counted(c, rotation));

                    if (c.Input.Kind == CadInputKind.Number)
                    {
                        rotation = c.Input.Number;
                    }
                }
                else if (c.Input.Is("Style"))
                {
                    foreach (CadPrompt prompt in AskTextStyle(c))
                    {
                        yield return prompt;
                    }

                    style = TextStyle(c);
                }
                else if (c.Input.Is("Width"))
                {
                    yield return CadPrompt.ForDistance("Specify width", a);

                    if (c.Input.Kind != CadInputKind.Number || c.Input.Number < 0.0)
                    {
                        yield break;
                    }

                    width = c.Input.Number;
                    break;
                }
                else if (c.Input.Is("Columns"))
                {
                    yield return CadPrompt.ForKeyword("Enter column type", new[] { "Dynamic", "Static", "No columns" }, "Dynamic");

                    if (c.Input.Is("No columns"))
                    {
                        columns = DxfColumnType.NoColumns;
                        continue;
                    }

                    bool fixedCount = c.Input.Is("Static");

                    if (!fixedCount && !c.Input.Is("Dynamic"))
                    {
                        yield break;
                    }

                    // AutoCAD offers sizes of the height: 80 times it across all, 30 a column, 5 a gutter and 10 high.
                    double total = 80.0 * height;
                    count = 2;
                    columnWidth = 30.0 * height;
                    gutter = 5.0 * height;
                    columnHeight = 10.0 * height;

                    if (fixedCount)
                    {
                        yield return CadPrompt.ForDistance("Specify total width:", a, null, Length(total));
                        total = c.Input.Kind == CadInputKind.Number && c.Input.Number > 0.0 ? c.Input.Number : total;
                        yield return CadPrompt.ForInteger("Specify number of columns:", null, "2");
                        count = c.Input.Kind == CadInputKind.Number && c.Input.Number >= 1.0 ? (int)c.Input.Number : count;
                    }
                    else
                    {
                        yield return CadPrompt.ForDistance("Specify column width:", a, null, Length(columnWidth));
                        columnWidth = c.Input.Kind == CadInputKind.Number && c.Input.Number > 0.0 ? c.Input.Number : columnWidth;
                    }

                    yield return CadPrompt.ForDistance("Specify gutter width:", a, null, Length(gutter));
                    gutter = c.Input.Kind == CadInputKind.Number && c.Input.Number >= 0.0 ? c.Input.Number : gutter;
                    yield return CadPrompt.ForDistance("Specify column height:", a, null, Length(columnHeight));
                    columnHeight = c.Input.Kind == CadInputKind.Number && c.Input.Number > 0.0 ? c.Input.Number : columnHeight;

                    if (fixedCount)
                    {
                        // The columns share the total width with the gutters between them.
                        columnWidth = (total - gutter * (count - 1)) / count;

                        if (columnWidth <= 0.0)
                        {
                            c.Write("*Invalid* The gutters take the whole width.");
                            yield break;
                        }
                    }

                    columns = fixedCount ? DxfColumnType.StaticColumns : DxfColumnType.DynamicColumns;
                    width = fixedCount ? count * columnWidth + (count - 1) * gutter : columnWidth;
                    break;
                }
                else
                {
                    break;
                }
            }

            if (!width.HasValue && !c.Input.IsPoint)
            {
                yield break;
            }

            // The box lies along the axes of the UCS, the text turned with it, as AutoCAD's; the MTEXT stands at the point of
            // the box its justification names.
            double turn = ucs.AngleToWorld(rotation);
            GeoPoint2 ua = ucs.ToUcs(a);
            GeoPoint2 ub = width.HasValue ? ua : ucs.ToUcs(c.Input.Point);
            double boxWidth = width ?? Math.Abs(ub.X - ua.X);
            double left = width.HasValue ? ua.X : Math.Min(ua.X, ub.X);
            double top = width.HasValue ? ua.Y : Math.Max(ua.Y, ub.Y);
            double bottom = width.HasValue ? ua.Y : Math.Min(ua.Y, ub.Y);
            int place = Array.IndexOf(MTextJustifications, attach);
            double x = width.HasValue ? ua.X : left + boxWidth * (place % 3) / 2.0;
            double y = width.HasValue ? ua.Y : top - (top - bottom) * (place / 3) / 2.0;
            GeoPoint2 corner = ucs.ToWorld(new GeoPoint2(Math.Min(ua.X, ub.X), top));
            GeoPoint2 location = ucs.ToWorld(new GeoPoint2(x, y));
            var attachment = (DxfAttachmentPoint)(place + 1);

            // The window writes the words in the MTEXT editor over the box; the command line takes them typed.
            var edit = new CadInPlaceEdit(CadInPlaceKind.MText, null, string.Empty, corner, height, ucs.IsWorld && rotation == 0.0 ? 0.0 : turn, boxWidth, style, place + 1);
            yield return CadPrompt.ForInPlaceEdit("Enter text", edit);

            if (c.Input.Kind != CadInputKind.Text || c.Input.Text.Length == 0)
            {
                yield break;
            }

            string words = c.Input.Text;
            double widthFactor = c.Remembered("text.widthFactor", 1.0);

            // Letters of another width open the words with the code AutoCAD writes for them, \W0.8000; for 0.8.
            if (Math.Abs(widthFactor - 1.0) > 1E-9)
            {
                words = DxfMText.WidthChange + widthFactor.ToString("0.0000", CultureInfo.InvariantCulture) + ";" + words;
            }

            c.Remember("text.height", height);
            string styleName = style;
            DxfLineSpacingStyle lineSpacing = spacing == "Exactly" ? DxfLineSpacingStyle.Exactly : DxfLineSpacingStyle.AtLeast;
            c.Apply(null, s =>
            {
                var text = new DxfMText { Location = P3(location), TextHeight = height, Width = boxWidth, Attachment = attachment, Contents = words };

                if (!ucs.IsWorld || rotation != 0.0)
                {
                    text.Rotation = turn;
                }

                if (styleName != null)
                {
                    text.TextStyleName = styleName;
                }

                if (lineSpacing != DxfLineSpacingStyle.AtLeast)
                {
                    text.LineSpacingStyle = lineSpacing;
                }

                if (factor != 1.0)
                {
                    text.LineSpacingFactor = factor;
                }

                ApplyBox(text, edit);
                s.Append(c.Space, Current(c, text));

                if (columns == DxfColumnType.StaticColumns)
                {
                    text.SetStaticColumns(columnWidth, gutter, count);
                    text.Height = columnHeight;
                }
                else if (columns == DxfColumnType.DynamicColumns)
                {
                    text.SetDynamicColumns(columnWidth, gutter, true);
                    text.Height = columnHeight;
                }
            });
        }
    }
}
