using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The commands of C2, each a body of a few lines that asks AutoCAD's prompts in AutoCAD's words: the view commands here,
    /// the edit commands in CadCommands.Edit.cs, the draw commands in CadCommands.Draw.cs and the dimensions in
    /// CadCommands.Dimension.cs.
    /// </summary>
    internal static partial class CadCommands
    {
        internal static void AddTo(CadCommandRegistry registry)
        {
            registry.Add(new CadCommand("ZOOM", Zoom));
            registry.Add(new CadCommand("PAN", Pan));
            registry.Add(new CadCommand("REGEN", Regen));
            registry.Add(new CadCommand("PROPERTIES", c => Ask(c, "PROPERTIES")));
            registry.Add(new CadCommand("OPTIONS", c => Ask(c, "OPTIONS")));
            registry.Add(new CadCommand("QSAVE", QuickSave));

            registry.Add(new CadCommand("MOVE", Move, true));
            registry.Add(new CadCommand("COPY", Copy, true));
            registry.Add(new CadCommand("ERASE", Erase, true));
            registry.Add(new CadCommand("ROTATE", Rotate, true));
            registry.Add(new CadCommand("SCALE", Scale, true));
            registry.Add(new CadCommand("MIRROR", Mirror, true));
            registry.Add(new CadCommand("EXPLODE", Explode, true));
            registry.Add(new CadCommand("MATCHPROP", MatchProperties));
            registry.Add(new CadCommand("LAYER", Layer));
            registry.Add(new CadCommand("UNDO", Undo));
            registry.Add(new CadCommand("U", UndoOne));
            registry.Add(new CadCommand("REDO", Redo));

            registry.Add(new CadCommand("LINE", Line));
            registry.Add(new CadCommand("PLINE", Polyline));
            registry.Add(new CadCommand("CIRCLE", Circle));
            registry.Add(new CadCommand("ARC", Arc));
            registry.Add(new CadCommand("RECTANG", Rectangle));
            registry.Add(new CadCommand("TEXT", Text));
            registry.Add(new CadCommand("MTEXT", MText));
            registry.Add(new CadCommand("MLEADER", MLeader));
            registry.Add(new CadCommand("MVIEW", MView));

            registry.Add(new CadCommand("DIMLINEAR", DimLinear));
            registry.Add(new CadCommand("DIMALIGNED", DimAligned));
            registry.Add(new CadCommand("DIMRADIUS", c => DimRadial(c, false)));
            registry.Add(new CadCommand("DIMDIAMETER", c => DimRadial(c, true)));
            registry.Add(new CadCommand("DIMANGULAR", DimAngular));
        }

        /// <summary>A point of the plan in space, at nought height.</summary>
        internal static GeoPoint3 P3(GeoPoint2 point) => new GeoPoint3(point.X, point.Y, 0.0);

        internal static string Length(double value) => CadCommandContext.Length(value);

        /// <summary>Numbers as the owner contract writes them (<see cref="CadEditKind"/>): invariant, to the bit, separated by commas.</summary>
        internal static string Values(params double[] numbers)
        {
            var texts = new string[numbers.Length];

            for (int i = 0; i < numbers.Length; i++)
            {
                texts[i] = numbers[i].ToString("R", CultureInfo.InvariantCulture);
            }

            return string.Join(",", texts);
        }

        /// <summary>The handle of the first entity an edit added, or null.</summary>
        internal static string AddedHandle(CadEditResult result)
        {
            foreach (CadEditRecord record in result.Records)
            {
                if (record.Kind == CadEditRecordKind.Added)
                {
                    return record.Handle;
                }
            }

            return null;
        }

        /// <summary>Gives a new entity the current layer, colour, linetype and lineweight of the drawing, as AutoCAD draws one.</summary>
        internal static T Current<T>(CadCommandContext c, T entity)
            where T : DxfEntity
        {
            entity.SetDatabaseDefaults(c.Database);
            return entity;
        }

        private static IEnumerable<CadPrompt> Ask(CadCommandContext c, string name)
        {
            if (!c.Session.Request(name))
            {
                c.Write(name + " is shown by the window.");
            }

            yield break;
        }

        private static IEnumerable<CadPrompt> QuickSave(CadCommandContext c)
        {
            if (c.Document.HasFile)
            {
                c.Document.Save();
                c.Write("Saved " + c.Document.FileName + ".");
            }
            else if (!c.Session.Request("QSAVE"))
            {
                c.Write("The drawing has no file yet: save it with Save As.");
            }

            yield break;
        }

        private static IEnumerable<CadPrompt> Regen(CadCommandContext c)
        {
            c.Write("Regenerating model.");
            c.Document.Regen();
            yield break;
        }

        private static IEnumerable<CadPrompt> Zoom(CadCommandContext c)
        {
            CadView view = c.Session.View;

            if (view == null)
            {
                c.Write("There is no view to zoom.");
                yield break;
            }

            yield return CadPrompt.ForPoint("Specify corner of window, enter a scale factor (nX or nXP)", null, new[] { "All", "Extents", "Previous", "Window" }, "real time", true);
            DisplayBox before = view.VisibleBox;

            // What the space draws and, in a layout, its sheet, as a double click of the middle button shows them.
            DisplayBox extents = c.Document.GetDisplay(c.Space).Bounds.Union(c.Space.PaperBounds);

            if (c.Input.IsNone)
            {
                c.Write("Real-time zoom is the mouse wheel's: the view is as it was.");
                yield break;
            }

            if (c.Input.Is("All") || c.Input.Is("Extents"))
            {
                view.ZoomTo(extents);
            }
            else if (c.Input.Kind == CadInputKind.Text)
            {
                string typed = c.Input.Text.Trim();
                bool relative = typed.EndsWith("X", StringComparison.OrdinalIgnoreCase) && !typed.EndsWith("XP", StringComparison.OrdinalIgnoreCase);
                string number = relative ? typed.Substring(0, typed.Length - 1) : typed;

                if (typed.EndsWith("XP", StringComparison.OrdinalIgnoreCase))
                {
                    c.Write("nXP scales the view of a viewport to paper space, which needs a viewport's model space (MSPACE): the view is as it was.");
                    yield break;
                }

                if (!double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out double factor) || !(factor > 0.0) || double.IsInfinity(factor))
                {
                    c.Write("Requires a point, a scale factor above nought (nX or nXP), or an option keyword.");
                    yield break;
                }

                // nX scales the view as it is about its centre; n alone scales the view of the extents, as AutoCAD's n scales that of the limits.
                if (!relative)
                {
                    view.ZoomTo(extents);
                }

                view.ZoomAt(view.Width / 2.0, view.Height / 2.0, factor);
            }
            else if (c.Input.Is("Previous"))
            {
                if (c.Session.Views.Count == 0)
                {
                    c.Write("No previous view saved.");
                    yield break;
                }

                DisplayBox previous = c.Session.Views[c.Session.Views.Count - 1];
                c.Session.Views.RemoveAt(c.Session.Views.Count - 1);
                view.ZoomTo(previous, 0.0);
                yield break;
            }
            else if (c.Input.IsPoint || c.Input.Is("Window"))
            {
                GeoPoint2 first;

                if (c.Input.IsPoint)
                {
                    first = c.Input.Point;
                }
                else
                {
                    yield return CadPrompt.ForPoint("Specify first corner");

                    if (!c.Input.IsPoint)
                    {
                        yield break;
                    }

                    first = c.Input.Point;
                }

                yield return CadPrompt.ForPoint("Specify opposite corner", first);

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                GeoPoint2 second = c.Input.Point;
                var box = new DisplayBox(Math.Min(first.X, second.X), Math.Min(first.Y, second.Y), Math.Max(first.X, second.X), Math.Max(first.Y, second.Y));

                if (box.Width <= 0.0 || box.Height <= 0.0)
                {
                    c.Write("*Invalid window*");
                    yield break;
                }

                view.ZoomTo(box, 0.0);
            }
            else
            {
                yield break;
            }

            c.Session.Views.Add(before);
        }

        private static IEnumerable<CadPrompt> Pan(CadCommandContext c)
        {
            CadView view = c.Session.View;

            if (view == null)
            {
                c.Write("There is no view to pan.");
                yield break;
            }

            yield return CadPrompt.ForPoint("Specify base point or displacement");

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            GeoPoint2 from = c.Input.Point;
            yield return CadPrompt.ForPoint("Specify second point", from);
            GeoVector2 move = c.Input.IsPoint ? c.Input.Point - from : new GeoVector2(from.X, from.Y);
            c.Session.Views.Add(view.VisibleBox);
            view.Pan(move.X * view.PixelsPerUnit, -move.Y * view.PixelsPerUnit);
        }

        private static string Number(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);
    }
}
