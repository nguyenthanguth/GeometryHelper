using System;

namespace GeometryHelper
{
    /// <summary>
    /// How far a long reading or writing has got, told to an <see cref="IProgress{T}"/> as a share from 0 to 1: the work cut
    /// into stages, each a span of that share, and the share told only when it has grown by a thousandth, so that a loop over
    /// a million records tells it a thousand times at most and the share told never goes back.
    /// </summary>
    internal sealed class ProgressSteps
    {
        private readonly IProgress<double> _progress;
        private double _outerFrom;
        private double _outerTo = 1.0;
        private double _from;
        private double _to = 1.0;
        private int _told = -1;

        private ProgressSteps(IProgress<double> progress) => _progress = progress;

        /// <summary>The steps of a progress; null for none, so that a caller with none pays only a test for null.</summary>
        internal static ProgressSteps Of(IProgress<double> progress) =>
            progress == null ? null : new ProgressSteps(progress);

        /// <summary>
        /// Puts the stages that follow, each still from 0 to 1, within the share from <paramref name="from"/> to
        /// <paramref name="to"/> of the whole: a DWG's own stages within the first 0.9 of its opening, the reading of the DXF
        /// it is made into the rest.
        /// </summary>
        internal void Within(double from, double to)
        {
            _outerFrom = from;
            _outerTo = to;
            _from = 0.0;
            _to = 1.0;
        }

        /// <summary>Starts the stage that takes the share from <paramref name="from"/> to <paramref name="to"/>, telling its start.</summary>
        internal void Stage(double from, double to)
        {
            _from = from;
            _to = to;
            Report(0.0);
        }

        /// <summary>Tells that <paramref name="done"/> of the <paramref name="total"/> steps of the stage are done.</summary>
        internal void Report(long done, long total) => Report(total > 0 ? (double)done / total : 1.0);

        /// <summary>Tells the share of the stage done, 0 to 1.</summary>
        internal void Report(double share)
        {
            double clamped =
                share > 1.0 ? 1.0
                : share > 0.0 ? share
                : 0.0;
            double value = _outerFrom + (_outerTo - _outerFrom) * (_from + (_to - _from) * clamped);
            int thousandths = (int)(value * 1000.0);

            if (thousandths <= _told)
            {
                return;
            }

            _told = thousandths;
            _progress.Report(thousandths / 1000.0);
        }

        /// <summary>Tells that the whole work is done: a share of 1.</summary>
        internal void Done()
        {
            _outerFrom = 1.0;
            _outerTo = 1.0;
            _from = 1.0;
            _to = 1.0;
            Report(1.0);
        }
    }
}
