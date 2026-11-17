using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The limits of a space and the check LIMCHECK makes against them: with $LIMCHECK on, a point given to a command that draws
    /// or places objects outside the limits of the space worked in is refused with <see cref="Outside"/> and the prompt is asked
    /// again, as AutoCAD 2021 refuses it. Model space has the limits of the header (<c>$LIMMIN</c> and <c>$LIMMAX</c>); a layout
    /// has those of its sheet.
    /// </summary>
    internal static class CadLimits
    {
        /// <summary>What AutoCAD writes when a point is outside the limits.</summary>
        internal const string Outside = "**Outside limits";

        // The commands whose points are checked: those that draw, insert, move or copy. A point that only measures, picks or
        // views (DIST, ZOOM, a window of a selection) is never refused.
        private static readonly HashSet<string> Checked = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "LINE",
            "PLINE",
            "RECTANG",
            "CIRCLE",
            "ARC",
            "ELLIPSE",
            "POINT",
            "POLYGON",
            "DONUT",
            "SPLINE",
            "XLINE",
            "RAY",
            "TEXT",
            "MTEXT",
            "TRACE",
            "MLINE",
            "REVCLOUD",
            "INSERT",
            "-INSERT",
            "MINSERT",
            "MOVE",
            "COPY",
        };

        /// <summary>
        /// Gets the limits of the space a command works in: those of the header in model space, those of the sheet in a layout.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="min">The lower left corner.</param>
        /// <param name="max">The upper right corner.</param>
        /// <returns>False where the space has none to give (a layout of no sheet).</returns>
        internal static bool Of(CadDocument document, out GeoPoint2 min, out GeoPoint2 max)
        {
            CadSpace space = document.WorkingSpace;

            if (space.IsModel || space.Layout == null)
            {
                min = document.Database.Limmin;
                max = document.Database.Limmax;
                return true;
            }

            DxfExtents2d limits = space.Layout.Limits;
            min = limits.MinPoint;
            max = limits.MaxPoint;
            return true;
        }

        /// <summary>Whether a point is outside the limits and LIMCHECK is on for a command that checks its points.</summary>
        /// <param name="session">The command line the point was given to.</param>
        /// <param name="point">The point, in the world.</param>
        /// <returns>True where it is to be refused.</returns>
        internal static bool Refuses(CadCommandSession session, GeoPoint2 point)
        {
            if (
                session.Current == null
                || !Checked.Contains(session.Current.Name)
                || !session.Document.Database.Limcheck
                || !Of(session.Document, out GeoPoint2 min, out GeoPoint2 max)
            )
            {
                return false;
            }

            double slack = session.Tolerance.EqualPoint;
            return point.X < min.X - slack
                || point.X > max.X + slack
                || point.Y < min.Y - slack
                || point.Y > max.Y + slack;
        }
    }
}
