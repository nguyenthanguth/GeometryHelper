using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// CHSPACE, as AutoCAD 2021 asks it (the words are those of its own string table, its answers those its console gave): on
    /// the Model tab "** Command not allowed in Model Tab **"; with a viewport worked through, the objects of model space are
    /// selected and moved to the paper, "Set the SOURCE viewport active and press ENTER to continue.", each scaled and placed
    /// as the viewport shows it; on the paper, the objects of the paper are selected, the viewports among them "Ignoring 3
    /// selected viewport(s)", "Set the TARGET viewport active and press ENTER to continue.", and the objects are moved into the
    /// model space the viewport shows, scaled the other way. Enter takes the viewport worked through (or the one last worked
    /// through, or the first of the layout); a point picked inside another viewport takes that one. The words end "2 object(s)
    /// changed from PAPER space to MODEL space." and, where the scale is not 1, "Objects were scaled by a factor of 0.68181818181818
    /// to maintain visual appearance." (a viewport at 1.4667 paper units to the model unit). A viewport twisted turns the
    /// objects with its view. The command ends in the space the objects went to: on the paper after one to the paper, in the
    /// viewport after one to the model.
    /// </summary>
    internal static partial class CadCommands
    {
        private static IEnumerable<CadPrompt> ChangeSpace(CadCommandContext c)
        {
            CadDocument document = c.Document;
            CadSpace shown = document.CurrentSpace;

            if (shown == null || shown.IsModel)
            {
                c.Write("** Command not allowed in Model Tab **");
                yield break;
            }

            bool fromModel = document.ActiveViewport != null;

            if (!fromModel && document.EnterableViewports.Count == 0)
            {
                c.Write("There are no active Model space viewports");
                yield break;
            }

            foreach (CadPrompt prompt in c.SelectObjects())
            {
                yield return prompt;
            }

            var chosen = new List<string>();
            int viewports = 0;

            foreach (string handle in c.Selected)
            {
                if (document.FindEntity(handle) is DxfViewport)
                {
                    viewports++;
                }
                else
                {
                    chosen.Add(handle);
                }
            }

            if (viewports > 0 && !fromModel)
            {
                c.Write("Ignoring " + viewports.ToString(CultureInfo.InvariantCulture) + " selected viewport(s)");
            }

            if (chosen.Count == 0)
            {
                yield break;
            }

            yield return CadPrompt.ForPoint(
                fromModel
                    ? "Set the SOURCE viewport active and press ENTER to continue."
                    : "Set the TARGET viewport active and press ENTER to continue."
            );

            DxfViewport viewport;

            if (c.Input.IsPoint)
            {
                // A point of the viewport worked through is of model space, a point on the paper is the paper's.
                GeoPoint2 onPaper = fromModel
                    ? CadViewports.ToPaper(document.ActiveViewport, c.Input.Point)
                    : c.Input.Point;
                viewport = ViewportAt(document, onPaper) ?? (fromModel ? document.ActiveViewport : null);
            }
            else if (c.Input.IsNone)
            {
                viewport = fromModel ? document.ActiveViewport : document.ViewportToEnter();
            }
            else
            {
                yield break;
            }

            if (viewport == null)
            {
                c.Write("There are no active Model space viewports");
                yield break;
            }

            if (!CadViewports.CanWorkThrough(viewport))
            {
                c.Write("Cannot use viewports that are turned off or have perspective view on.");
                yield break;
            }

            // The paper point each model point is shown at: the scale, the view's turn and the move, as one transformation.
            GeoPoint2 origin = CadViewports.ToPaper(viewport, new GeoPoint2(0.0, 0.0));
            GeoPoint2 along = CadViewports.ToPaper(viewport, new GeoPoint2(1.0, 0.0));
            GeoPoint2 up = CadViewports.ToPaper(viewport, new GeoPoint2(0.0, 1.0));
            double factor = viewport.Height / viewport.ViewHeight;
            GeoTransform3 toPaper = new GeoTransform3(
                new[,]
                {
                    { along.X - origin.X, up.X - origin.X, 0.0, origin.X },
                    { along.Y - origin.Y, up.Y - origin.Y, 0.0, origin.Y },
                    { 0.0, 0.0, factor, 0.0 },
                    { 0.0, 0.0, 0.0, 1.0 },
                }
            );
            GeoTransform3 transform = fromModel ? toPaper : toPaper.Inverse();
            CadSpace target = fromModel ? shown : document.ModelSpace;
            var planned = new List<CadEditRecord>();

            foreach (string handle in chosen)
            {
                planned.Add(new CadEditRecord(CadEditRecordKind.Erased, handle));
            }

            int moved = 0;
            CadEditResult result = c.Apply(planned, s =>
            {
                moved = 0;

                foreach (string handle in chosen)
                {
                    if (s.IsHandled(handle))
                    {
                        continue;
                    }

                    DxfEntity entity = s.Document.FindEntity(handle);
                    DxfEntity copy;

                    try
                    {
                        copy = entity.GetTransformedCopy(transform);
                    }
                    catch (ArgumentException)
                    {
                        continue;
                    }
                    catch (InvalidOperationException)
                    {
                        continue;
                    }
                    catch (NotSupportedException)
                    {
                        continue;
                    }

                    s.Append(target, copy);
                    entity.Erase();
                    moved++;
                }
            });

            if (!result.Succeeded || moved == 0)
            {
                yield break;
            }

            c.Write(
                moved.ToString(CultureInfo.InvariantCulture)
                    + " object(s) changed from "
                    + (fromModel ? "MODEL space to PAPER space." : "PAPER space to MODEL space.")
            );
            double scaled = fromModel ? factor : 1.0 / factor;

            if (Math.Abs(scaled - 1.0) > 1E-9)
            {
                c.Write(
                    "Objects were scaled by a factor of "
                        + scaled.ToString("0.##############", CultureInfo.InvariantCulture)
                        + " to maintain visual"
                );
                c.Write("appearance.");
            }

            if (fromModel)
            {
                document.LeaveViewport();
            }
            else
            {
                document.EnterViewport(viewport.Handle);
            }
        }

        /// <summary>The viewport of the layout shown that a point of the paper is inside, the one drawn last (on top) where several are; null for none.</summary>
        private static DxfViewport ViewportAt(CadDocument document, GeoPoint2 paper)
        {
            DxfViewport found = null;

            foreach (DxfViewport viewport in document.EnterableViewports)
            {
                GeoPoint3 middle = viewport.CenterPoint;

                if (
                    Math.Abs(paper.X - middle.X) <= viewport.Width / 2.0
                    && Math.Abs(paper.Y - middle.Y) <= viewport.Height / 2.0
                )
                {
                    found = viewport;
                }
            }

            return found;
        }
    }
}
