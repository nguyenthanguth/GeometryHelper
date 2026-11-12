using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The views of model space and of the paper of a layout as undo steps and as ZOOM Previous entries. A viewport's view is
    /// a property of the viewport in the drawing and is undone with the drawing; the view of a space is not in the drawing, so
    /// the step of a zoom or a pan of a space holds the part of the space shown before and after, and undoing it moves the
    /// view and reads nothing again.
    /// </summary>
    public sealed partial class CadDocument
    {
        // The property named by the records of a space's view.
        private const string SpaceViewProperty = "View";

        /// <summary>The views ZOOM Previous goes back to: a list for each space and for each viewport of a layout.</summary>
        internal CadViewHistory ViewHistory { get; } = new CadViewHistory();

        /// <summary>
        /// Records that the view of a space has been zoomed or panned: one undo step, which puts the view back as it was before
        /// (U) or as it is now (REDO) with the drawing left alone, and an entry of the space's ZOOM Previous list, 20 at most.
        /// The wheel and the middle button of the canvas call it once their gesture is done; ZOOM, PAN and PLAN call it too.
        /// A zoom or a pan of the wheel or the middle button made straight after another joins its step, as AutoCAD 2021
        /// combines consecutive real-time zooms and pans; typed ones stay a step each. A step is named ZOOM or PAN.
        /// </summary>
        /// <param name="space">The space whose view moved: model space or a layout, whose paper is meant, not a viewport's view.</param>
        /// <param name="view">The view of the space after the move; kept, so that an undo or a redo moves it.</param>
        /// <param name="before">The part of the space the view showed before the move, <see cref="CadView.VisibleBox"/> then.</param>
        /// <param name="name">The name of the undo step, as ZOOM or PAN; a step joined keeps the name of the first.</param>
        /// <param name="realTime">Whether the move is the wheel's or the middle button's, which joins the real-time step before it
        /// where no other step, a save or an undo came between.</param>
        /// <returns>What came of it: done, with no record where the view shows what it showed.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the space, the view or the name is null.</exception>
        public CadEditResult RecordSpaceView(
            CadSpace space,
            CadView view,
            DisplayBox before,
            string name,
            bool realTime
        ) => RecordSpaceView(space, view, before, name, realTime, true);

        /// <summary>Records the move of a space's view as <see cref="RecordSpaceView(CadSpace, CadView, DisplayBox, string, bool)"/>; ZOOM Previous, which takes its entry off the list, keeps none.</summary>
        internal CadEditResult RecordSpaceView(
            CadSpace space,
            CadView view,
            DisplayBox before,
            string name,
            bool realTime,
            bool keepPrevious
        )
        {
            if (space == null)
            {
                throw new ArgumentNullException(nameof(space));
            }

            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            DisplayBox after = view.VisibleBox;

            if (before.IsEmpty || SameBox(before, after))
            {
                return CadEditResult.Done(Array.Empty<CadEditRecord>());
            }

            space.ShownView = view;

            if (keepPrevious)
            {
                ViewHistory.Add(space, before);
            }

            EnsureStart();
            var record = new CadEditRecord(
                CadEditRecordKind.SettingChanged,
                null,
                SpaceViewProperty,
                BoxText(before),
                BoxText(after)
            )
            {
                Space = space.Name,
            };
            var records = new List<CadEditRecord> { record };

            if (_groupDepth > 0)
            {
                _groupRecords.AddRange(records);
            }
            else
            {
                _realTimeView = realTime;

                try
                {
                    Commit(name, records);
                }
                finally
                {
                    _realTimeView = false;
                }
            }

            Announce(CadChangeKind.Edit, name, records);
            return CadEditResult.Done(records);
        }

        /// <summary>Whether a box shows what another shows, within a hundred-thousandth of the larger side: a zoom or a pan under a pixel is none.</summary>
        private static bool SameBox(DisplayBox before, DisplayBox after)
        {
            double tolerance = Tolerance.Default.EqualVector * Math.Max(before.Width, before.Height);
            return Math.Abs(before.MinX - after.MinX) <= tolerance
                && Math.Abs(before.MinY - after.MinY) <= tolerance
                && Math.Abs(before.MaxX - after.MaxX) <= tolerance
                && Math.Abs(before.MaxY - after.MaxY) <= tolerance;
        }

        /// <summary>Whether a record is of the view of a space.</summary>
        private static bool IsSpaceView(CadEditRecord record) =>
            record.Kind == CadEditRecordKind.SettingChanged
            && record.Handle == null
            && string.Equals(record.Property, SpaceViewProperty, StringComparison.Ordinal);

        /// <summary>The records of a step joined by a move of the view straight after it: the same space's view, moved twice, is one record from the first before to the last after.</summary>
        private static CadEditRecord[] JoinViews(IReadOnlyList<CadEditRecord> first, IReadOnlyList<CadEditRecord> next)
        {
            var joined = new List<CadEditRecord>(first);

            foreach (CadEditRecord record in next)
            {
                CadEditRecord last = joined[joined.Count - 1];

                if (string.Equals(last.Space, record.Space, StringComparison.Ordinal))
                {
                    joined[joined.Count - 1] = new CadEditRecord(
                        record.Kind,
                        null,
                        SpaceViewProperty,
                        last.OldValue,
                        record.NewValue
                    )
                    {
                        Space = record.Space,
                    };
                }
                else
                {
                    joined.Add(record);
                }
            }

            return joined.ToArray();
        }

        /// <summary>Moves the views of the spaces to what a step undone or redone left them: the old boxes from the last record back, or the new ones from the first on.</summary>
        private void MoveViews(IReadOnlyList<CadEditRecord> records, bool undo)
        {
            for (int k = 0; k < records.Count; k++)
            {
                CadEditRecord record = records[undo ? records.Count - 1 - k : k];

                if (!IsSpaceView(record))
                {
                    continue;
                }

                CadSpace space = SpaceNamed(record.Space);

                if (space?.ShownView != null && TryBox(undo ? record.OldValue : record.NewValue, out DisplayBox box))
                {
                    space.ShownView.ZoomTo(box, 0.0);
                }
            }
        }

        private static string BoxText(DisplayBox box) =>
            string.Join(
                ",",
                box.MinX.ToString("R", CultureInfo.InvariantCulture),
                box.MinY.ToString("R", CultureInfo.InvariantCulture),
                box.MaxX.ToString("R", CultureInfo.InvariantCulture),
                box.MaxY.ToString("R", CultureInfo.InvariantCulture)
            );

        private static bool TryBox(string text, out DisplayBox box)
        {
            box = DisplayBox.Empty;
            string[] parts = (text ?? string.Empty).Split(',');

            if (parts.Length != 4)
            {
                return false;
            }

            var values = new double[4];

            for (int i = 0; i < 4; i++)
            {
                if (!double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]))
                {
                    return false;
                }
            }

            box = new DisplayBox(values[0], values[1], values[2], values[3]);
            return true;
        }
    }
}
