using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The rows of polylines, ellipses, hatches, dimensions, MTEXTs, block references, viewports and multileaders, set as
    /// AutoCAD 2021's palette sets them.
    /// </summary>
    public static partial class CadPropertyEditor
    {
        /// <summary>
        /// Steps the vertex the rows of a polyline show (Current vertex), which is no edit of the drawing and no step of undo:
        /// 3 shows the third vertex, a number past the last the last.
        /// </summary>
        private static CadEditResult Browse(CadDocument document, List<DxfEntity> polylines, string text)
        {
            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int vertex) || vertex < 1)
            {
                return CadEditResult.Refuse("Current vertex is a whole number from 1.");
            }

            int most = 1;

            foreach (DxfEntity entity in polylines)
            {
                most = Math.Max(most, ((DxfPolyline)entity).NumberOfVertices);
            }

            document.Selection.CurrentVertex = Math.Min(vertex, most);
            return CadEditResult.Done(Array.Empty<CadEditRecord>());
        }

        /// <summary>Works out how a row of the kinds of this file is written: true where the kind has the row, with the reason it cannot be or null and the writer.</summary>
        private static bool More(DxfDatabase db, string label, string text, int vertex, DxfEntity entity, out Action<DxfEntity> write, out string error)
        {
            write = null;
            bool number = Number(text, out double v);
            string notNumber = label + " is a number.";
            string notPositive = label + " is a number above nought.";
            string notNegative = label + " is a number, nought or more.";

            switch (entity)
            {
                case DxfPolyline _:
                    {
                        int At(DxfPolyline p) => Math.Max(0, Math.Min(vertex, p.NumberOfVertices) - 1);

                        switch (label)
                        {
                            case "Vertex X": write = e => { var p = (DxfPolyline)e; p.SetPointAt(At(p), new GeoPoint2(v, p.GetPoint2dAt(At(p)).Y)); }; error = number ? null : notNumber; return true;
                            case "Vertex Y": write = e => { var p = (DxfPolyline)e; p.SetPointAt(At(p), new GeoPoint2(p.GetPoint2dAt(At(p)).X, v)); }; error = number ? null : notNumber; return true;
                            case "Start segment width": write = e => { var p = (DxfPolyline)e; p.SetStartWidthAt(At(p), v); }; error = number && v >= 0.0 ? null : notNegative; return true;
                            case "End segment width": write = e => { var p = (DxfPolyline)e; p.SetEndWidthAt(At(p), v); }; error = number && v >= 0.0 ? null : notNegative; return true;
                            case "Global width": write = e => ((DxfPolyline)e).ConstantWidth = v; error = number && v >= 0.0 ? null : notNegative; return true;
                            case "Elevation": write = e => ((DxfPolyline)e).Elevation = v; error = number ? null : notNumber; return true;
                            case "Closed": return Flag(text, CadProperties.YesNoChoices, label, (e, on) => ((DxfPolyline)e).Closed = on, out write, out error);
                            case "Linetype generation": return Flag(text, CadProperties.EnabledChoices, label, (e, on) => ((DxfPolyline)e).Plinegen = on, out write, out error);
                        }

                        break;
                    }

                case DxfEllipse _:
                    switch (label)
                    {
                        case "Center X": write = e => { var l = (DxfEllipse)e; l.Center = new GeoPoint3(v, l.Center.Y, l.Center.Z); }; error = number ? null : notNumber; return true;
                        case "Center Y": write = e => { var l = (DxfEllipse)e; l.Center = new GeoPoint3(l.Center.X, v, l.Center.Z); }; error = number ? null : notNumber; return true;
                        case "Center Z": write = e => { var l = (DxfEllipse)e; l.Center = new GeoPoint3(l.Center.X, l.Center.Y, v); }; error = number ? null : notNumber; return true;
                        case "Major radius":
                            // The whole ellipse scaled about its centre: the ratio and the end angles kept.
                            write = e =>
                            {
                                var l = (DxfEllipse)e;
                                l.Set(l.Center, l.Normal, l.MajorAxis * (v / l.MajorRadius), l.RadiusRatio, l.StartAngle, l.EndAngle);
                            };
                            error = number && v > 0.0 ? null : notPositive;
                            return true;
                        case "Minor radius":
                            write = e => { var l = (DxfEllipse)e; l.RadiusRatio = v / l.MajorRadius; };
                            error = number && v > 0.0 && v <= ((DxfEllipse)entity).MajorRadius * (1.0 + 1E-12) ? null : "Minor radius is a number above nought and no more than the major radius.";
                            return true;
                        case "Radius ratio": write = e => ((DxfEllipse)e).RadiusRatio = v; error = number && v > 0.0 && v <= 1.0 ? null : "Radius ratio is a number above nought and up to 1."; return true;
                        case "Start angle": write = e => ((DxfEllipse)e).StartAngle = Turn(v); error = number ? null : notNumber; return true;
                        case "End angle": write = e => ((DxfEllipse)e).EndAngle = Turn(v); error = number ? null : notNumber; return true;
                    }

                    break;
                case DxfHatch hatch:
                    return Hatch(db, label, text, hatch, number, v, out write, out error);
                case DxfDimension dimension:
                    return Dimension(db, label, text, dimension, out write, out error);
                case DxfMText mtext:
                    return MText(db, label, text, mtext, number, v, out write, out error);
                case DxfMLeader _:
                    return MLeader(db, label, text, number, v, out write, out error);
                case DxfBlockReference insert:
                    return Insert(label, text, insert, number, v, out write, out error);
                case DxfViewport _:
                    return Viewport(label, text, number, v, out write, out error);
            }

            error = null;
            return false;
        }

        private static bool Hatch(DxfDatabase db, string label, string text, DxfHatch hatch, bool number, double v, out Action<DxfEntity> write, out string error)
        {
            write = null;
            bool gradient = hatch.IsGradient;
            bool solid = gradient || hatch.IsSolidFill;
            bool user = !solid && hatch.PatternType == DxfHatchPatternType.UserDefined;
            string notPositive = label + " is a number above nought.";

            switch (label)
            {
                case "Type":
                    {
                        int index = Array.FindIndex(CadProperties.HatchTypeChoices, c => string.Equals(c, text, StringComparison.OrdinalIgnoreCase));
                        error = gradient ? "A gradient has no pattern type." : index < 0 ? "Type is User defined, Predefined or Custom." : null;
                        var type = (DxfHatchPatternType)Math.Max(0, index);

                        // A pattern made by the user is named _USER; one of a file keeps the name it has, ANSI31 where it had none.
                        write = e =>
                        {
                            var h = (DxfHatch)e;

                            if (h.PatternType != type || h.IsSolidFill)
                            {
                                string name = type == DxfHatchPatternType.UserDefined ? "_USER" : h.PatternType == DxfHatchPatternType.UserDefined ? "ANSI31" : h.PatternName;
                                h.SetHatchPattern(type, name);
                            }
                        };
                        return true;
                    }

                case "Pattern name":
                    error = gradient ? "The pattern of a gradient is set in the Hatch Editor." : text.Length == 0 || text.IndexOf('\n') >= 0 ? "A pattern has a name, on one line." : null;
                    write = e =>
                    {
                        var h = (DxfHatch)e;

                        if (!string.Equals(h.PatternName, text, StringComparison.OrdinalIgnoreCase))
                        {
                            h.SetHatchPattern(h.PatternType == DxfHatchPatternType.UserDefined && !string.Equals(text, "_USER", StringComparison.OrdinalIgnoreCase) ? DxfHatchPatternType.PreDefined : h.PatternType, text);
                        }
                    };
                    return true;
                case "Angle":
                    write = e => ((DxfHatch)e).PatternAngle = Turn(v);
                    error = solid ? "A solid fill has no angle." : number ? null : label + " is a number.";
                    return true;
                case "Scale":
                    write = e => ((DxfHatch)e).PatternScale = v;
                    error = solid || user ? "A solid fill and a pattern made by the user have no scale." : number && v > 0.0 ? null : notPositive;
                    return true;
                case "Spacing":
                    write = e => ((DxfHatch)e).PatternSpace = v;
                    error = !user ? "Only a pattern made by the user has a spacing." : number && v > 0.0 ? null : notPositive;
                    return true;
                case "Double":
                    {
                        Flag(text, CadProperties.YesNoChoices, label, (e, on) => ((DxfHatch)e).PatternDouble = on, out write, out string flag);
                        error = user ? flag : "Only a pattern made by the user is doubled.";
                        return true;
                    }
                case "Origin X":
                case "Origin Y":
                    {
                        bool x = label == "Origin X";
                        error = solid ? "A solid fill has no origin." : user ? "The lines of a pattern made by the user follow its spacing: its origin is not moved here."
                            : number ? null : label + " is a number.";

                        // The pattern's lines moved, every base point by as much as the first's moves.
                        write = e =>
                        {
                            var h = (DxfHatch)e;
                            int count = h.NumberOfPatternDefinitions;

                            if (count == 0)
                            {
                                return;
                            }

                            DxfPatternDefinition first = h.GetPatternDefinitionAt(0);
                            double dx = x ? v - first.BaseX : 0.0;
                            double dy = x ? 0.0 : v - first.BaseY;
                            var moved = new List<DxfPatternDefinition>();

                            for (int i = 0; i < count; i++)
                            {
                                DxfPatternDefinition line = h.GetPatternDefinitionAt(i);
                                moved.Add(new DxfPatternDefinition(line.Angle, line.BaseX + dx, line.BaseY + dy, line.OffsetX, line.OffsetY, line.GetDashes()));
                            }

                            h.SetPatternDefinitions(moved);
                        };
                        return true;
                    }

                case "Elevation":
                    write = e => ((DxfHatch)e).Elevation = v;
                    error = number ? null : label + " is a number.";
                    return true;
                case "Island detection":
                    {
                        int index = Array.FindIndex(CadProperties.IslandChoices, c => string.Equals(c, text, StringComparison.OrdinalIgnoreCase));
                        write = e => ((DxfHatch)e).HatchStyle = (DxfHatchStyle)index;
                        error = index < 0 ? "Island detection is Normal, Outer or Ignore." : null;
                        return true;
                    }

                case "Background color":
                    {
                        DxfColor color = DxfColor.FromColorIndex(DxfColorMethod.None, 0);
                        error = string.Equals(text, "None", StringComparison.OrdinalIgnoreCase) ? null : ParseColor(text, out color);
                        write = e => ((DxfHatch)e).BackgroundColor = color;
                        return true;
                    }
            }

            error = null;
            return false;
        }

        /// <summary>A row of a dimension, written as an override of its variable and the dimension drawn again (see <see cref="Override"/> for a picture it shares).</summary>
        private static bool Dimension(DxfDatabase db, string label, string text, DxfDimension dimension, out Action<DxfEntity> write, out string error)
        {
            write = null;

            foreach (CadProperties.DimensionRow row in CadProperties.DimensionRows)
            {
                if (row.Label != label || row.Parse == null)
                {
                    continue;
                }

                error = row.Parse(db, text, out Action<DxfDimension> set);
                write = e =>
                {
                    var d = (DxfDimension)e;

                    // The value it shows already is not written, so that no override is added for nothing.
                    if (row.Label != "Text rotation" && string.Equals(row.Read(d, a => null), text, StringComparison.Ordinal))
                    {
                        return;
                    }

                    set(d);

                    if (d.DimBlock != null && !SharesPicture(db, d, d.DimBlock))
                    {
                        CadCommands.Redraw(d);
                    }
                };
                return true;
            }

            error = null;
            return false;
        }

        private static bool MText(DxfDatabase db, string label, string text, DxfMText mtext, bool number, double v, out Action<DxfEntity> write, out string error)
        {
            write = null;

            switch (label)
            {
                case "Style":
                    {
                        bool has = text.Length > 0 && db.TextStyleTable.Has(text);
                        string name = has ? db.TextStyleTable[text].Name : null;
                        write = e => ((DxfMText)e).TextStyleName = name;
                        error = has ? null : "The drawing has no text style \"" + text + "\".";
                        return true;
                    }

                case "Justify":
                    {
                        int index = Array.FindIndex(CadProperties.JustifyChoices, c => string.Equals(c, text, StringComparison.OrdinalIgnoreCase));
                        write = e => ((DxfMText)e).Attachment = (DxfAttachmentPoint)(index + 1);
                        error = index < 0 ? "Justify is one of " + string.Join(", ", CadProperties.JustifyChoices) + "." : null;
                        return true;
                    }

                case "Direction":
                    {
                        DxfFlowDirection direction = string.Equals(text, "Top to bottom", StringComparison.OrdinalIgnoreCase) ? DxfFlowDirection.TopToBottom
                            : string.Equals(text, "By style", StringComparison.OrdinalIgnoreCase) ? DxfFlowDirection.ByStyle
                            : string.Equals(text, "Left to right", StringComparison.OrdinalIgnoreCase) ? DxfFlowDirection.LeftToRight : DxfFlowDirection.NotSet;
                        write = e => ((DxfMText)e).FlowDirection = direction;
                        error = direction == DxfFlowDirection.NotSet ? "Direction is Left to right, Top to bottom or By style." : null;
                        return true;
                    }

                case "Line space factor":
                    write = e => ((DxfMText)e).LineSpacingFactor = v;
                    error = number && v >= 0.25 && v <= 4.0 ? null : "Line space factor is a number from 0.25 to 4.";
                    return true;
                case "Line space distance":
                    {
                        double factor = v / (mtext.TextHeight * 5.0 / 3.0);
                        write = e => ((DxfMText)e).LineSpaceDistance = v;
                        error = number && factor >= 0.25 - 1E-12 && factor <= 4.0 + 1E-12 ? null : "Line space distance is from a quarter to four times single spacing, which is 5/3 of the text height.";
                        return true;
                    }

                case "Line space style":
                    {
                        int index = Array.FindIndex(CadProperties.LineSpaceChoices, c => string.Equals(c, text, StringComparison.OrdinalIgnoreCase));
                        write = e => ((DxfMText)e).LineSpacingStyle = (DxfLineSpacingStyle)(index + 1);
                        error = index < 0 ? "Line space style is At least or Exactly." : null;
                        return true;
                    }

                case "Defined width":
                    write = e => ((DxfMText)e).Width = v;
                    error = number && v >= 0.0 ? null : label + " is a number, nought or more.";
                    return true;
                case "Columns":
                    {
                        int index = Array.FindIndex(CadProperties.ColumnChoices, c => string.Equals(c, text, StringComparison.OrdinalIgnoreCase));
                        error = index < 0 ? "Columns is None, Static or Dynamic." : null;
                        write = e => Columns((DxfMText)e, (DxfColumnType)index);
                        return true;
                    }
            }

            error = null;
            return false;
        }

        /// <summary>
        /// The columns of an MTEXT, as the palette's Columns sets them: each as wide as the MTEXT was where it had none, a
        /// gutter of five times the text height, as AutoCAD's MTEXT editor gives, and static columns two or as many as it had.
        /// </summary>
        private static void Columns(DxfMText mtext, DxfColumnType type)
        {
            if (mtext.ColumnType == type)
            {
                return;
            }

            if (type == DxfColumnType.NoColumns)
            {
                mtext.ColumnType = type;
                return;
            }

            double width = mtext.ColumnType == DxfColumnType.NoColumns || !(mtext.ColumnWidth > 0.0) ? (mtext.Width > 0.0 ? mtext.Width : mtext.ActualWidth) : mtext.ColumnWidth;
            width = width > 0.0 ? width : mtext.TextHeight * 20.0;
            double gutter = mtext.ColumnType == DxfColumnType.NoColumns ? mtext.TextHeight * 5.0 : mtext.ColumnGutterWidth;

            if (type == DxfColumnType.StaticColumns)
            {
                mtext.SetStaticColumns(width, gutter, Math.Max(2, mtext.ColumnCount));
            }
            else
            {
                mtext.SetDynamicColumns(width, gutter, true);
            }
        }

        private static bool MLeader(DxfDatabase db, string label, string text, bool number, double v, out Action<DxfEntity> write, out string error)
        {
            write = null;

            switch (label)
            {
                case "Multileader style":
                    {
                        string name = null;

                        foreach (DxfDBDictionaryEntry entry in (IEnumerable<DxfDBDictionaryEntry>)db.MLeaderStyleDictionary ?? Array.Empty<DxfDBDictionaryEntry>())
                        {
                            if (string.Equals(entry.Key, text, StringComparison.OrdinalIgnoreCase))
                            {
                                name = entry.Key;
                            }
                        }

                        write = e => ((DxfMLeader)e).MLeaderStyle = name;
                        error = name == null ? "The drawing has no multileader style \"" + text + "\"." : null;
                        return true;
                    }

                case "Leader type":
                    {
                        int index = Array.FindIndex(CadProperties.LeaderTypeChoices, c => string.Equals(c, text, StringComparison.OrdinalIgnoreCase));
                        write = e => ((DxfMLeader)e).LeaderLineType = (DxfLeaderType)index;
                        error = index < 0 ? "Leader type is None, Straight or Spline." : null;
                        return true;
                    }

                case "Horizontal landing":
                    return Flag(text, CadProperties.YesNoChoices, label, (e, on) => { var m = (DxfMLeader)e; m.EnableLanding = on; m.EnableDogleg = on; }, out write, out error);
                case "Landing distance":
                    write = e => ((DxfMLeader)e).DoglegLength = v;
                    error = number && v >= 0.0 ? null : label + " is a number, nought or more.";
                    return true;
                case "Contents":
                    write = e => { var m = (DxfMLeader)e; DxfMText words = m.MText; words.Contents = text; m.MText = words; };
                    error = text.Length > 0 ? null : "A multileader's text has contents.";
                    return true;
                case "Text style":
                    {
                        bool has = text.Length > 0 && db.TextStyleTable.Has(text);
                        string name = has ? db.TextStyleTable[text].Name : null;
                        write = e => { var m = (DxfMLeader)e; DxfMText words = m.MText; words.TextStyleName = name; m.MText = words; };
                        error = has ? null : "The drawing has no text style \"" + text + "\".";
                        return true;
                    }

                case "Text height":
                    write = e => ((DxfMLeader)e).TextHeight = v;
                    error = number && v > 0.0 ? null : label + " is a number above nought.";
                    return true;
            }

            error = null;
            return false;
        }

        /// <summary>The rows of a block reference: its place, scales and rotation, and the value of an attribute by its tag.</summary>
        private static bool Insert(string label, string text, DxfBlockReference insert, bool number, double v, out Action<DxfEntity> write, out string error)
        {
            write = null;
            string notNumber = label + " is a number.";

            switch (label)
            {
                case "Position X": write = e => Shift(e, v - ((DxfBlockReference)e).Position.X, 0.0, 0.0); error = number ? null : notNumber; return true;
                case "Position Y": write = e => Shift(e, 0.0, v - ((DxfBlockReference)e).Position.Y, 0.0); error = number ? null : notNumber; return true;
                case "Position Z": write = e => Shift(e, 0.0, 0.0, v - ((DxfBlockReference)e).Position.Z); error = number ? null : notNumber; return true;
                case "Scale X":
                case "Scale Y":
                case "Scale Z":
                    {
                        char axis = label[label.Length - 1];
                        write = e =>
                        {
                            var b = (DxfBlockReference)e;
                            GeoVector3 s = b.ScaleFactors;
                            b.ScaleFactors = new GeoVector3(axis == 'X' ? v : s.X, axis == 'Y' ? v : s.Y, axis == 'Z' ? v : s.Z);
                        };
                        error = number && v != 0.0 ? null : label + " is a number other than nought.";
                        return true;
                    }

                case "Rotation":
                    write = e => ((DxfBlockReference)e).Rotation = Turn(v);
                    error = number ? null : notNumber;
                    return true;
            }

            foreach (DxfAttributeReference attribute in insert.AttributeCollection)
            {
                if (!attribute.IsErased && attribute.Tag == label)
                {
                    write = e =>
                    {
                        foreach (DxfAttributeReference each in ((DxfBlockReference)e).AttributeCollection)
                        {
                            if (!each.IsErased && each.Tag == label)
                            {
                                each.TextString = text;
                                return;
                            }
                        }
                    };
                    error = attribute.IsConstant ? "The attribute " + label + " is constant." : text.IndexOf('\n') >= 0 ? "An attribute's value is on one line." : null;
                    return true;
                }
            }

            error = null;
            return false;
        }

        private static bool Viewport(string label, string text, bool number, double v, out Action<DxfEntity> write, out string error)
        {
            write = null;
            string notNumber = label + " is a number.";
            string notPositive = label + " is a number above nought.";

            switch (label)
            {
                case "Center X": write = e => { var p = (DxfViewport)e; p.CenterPoint = new GeoPoint3(v, p.CenterPoint.Y, p.CenterPoint.Z); }; error = number ? null : notNumber; return true;
                case "Center Y": write = e => { var p = (DxfViewport)e; p.CenterPoint = new GeoPoint3(p.CenterPoint.X, v, p.CenterPoint.Z); }; error = number ? null : notNumber; return true;
                case "Center Z": write = e => { var p = (DxfViewport)e; p.CenterPoint = new GeoPoint3(p.CenterPoint.X, p.CenterPoint.Y, v); }; error = number ? null : notNumber; return true;
                case "Height":
                case "Width":
                    {
                        bool height = label == "Height";

                        // The scale kept, as AutoCAD keeps it: the view grows with the viewport.
                        write = e =>
                        {
                            var p = (DxfViewport)e;
                            double scale = p.Height > 0.0 ? p.CustomScale : 0.0;

                            if (height)
                            {
                                p.Height = v;
                            }
                            else
                            {
                                p.Width = v;
                            }

                            if (scale > 0.0)
                            {
                                p.CustomScale = scale;
                            }
                        };
                        error = number && v > 0.0 ? null : notPositive;
                        return true;
                    }

                case "On":
                    return Flag(text, CadProperties.YesNoChoices, label, (e, on) => ((DxfViewport)e).On = on, out write, out error);
                case "Display locked":
                    return Flag(text, CadProperties.YesNoChoices, label, (e, on) => ((DxfViewport)e).Locked = on, out write, out error);
                case "Standard scale":
                    {
                        bool known = CadProperties.TryParseScale(text, out DxfStandardScaleType scale);
                        write = e => ((DxfViewport)e).StandardScale = scale;
                        error = known ? null : "Standard scale is one of AutoCAD's, as 1:50; type another into Custom scale.";
                        return true;
                    }

                case "Custom scale":
                    write = e => ((DxfViewport)e).CustomScale = v;
                    error = number && v > 0.0 ? null : notPositive;
                    return true;
            }

            error = null;
            return false;
        }

        /// <summary>A row of two words, as Yes and No, the first setting the flag.</summary>
        private static bool Flag(string text, IReadOnlyList<string> words, string label, Action<DxfEntity, bool> set, out Action<DxfEntity> write, out string error)
        {
            bool on = string.Equals(text, words[0], StringComparison.OrdinalIgnoreCase);
            write = e => set(e, on);
            error = on || string.Equals(text, words[1], StringComparison.OrdinalIgnoreCase) ? null : label + " is " + words[0] + " or " + words[1] + ".";
            return true;
        }
    }
}
