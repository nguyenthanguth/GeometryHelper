using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.CommonGeometry;
using GeometryHelper.CommonGeometry.Enums;

namespace GeometryHelper.TeklaConvert.UnitTest
{
    /// <summary>
    /// Collects what is written to <see cref="GeometryHelperLog"/> until it is disposed, then puts the default writer
    /// back.
    /// </summary>
    internal sealed class LogCapture : IDisposable
    {
        private readonly List<(GeometryHelperLogLevel Level, string Message, Exception Exception)> _entries =
            new List<(GeometryHelperLogLevel Level, string Message, Exception Exception)>();

        internal LogCapture()
        {
            GeometryHelperLog.Enable = true;
            GeometryHelperLog.Writer = (level, message, exception) =>
            {
                lock (_entries)
                {
                    _entries.Add((level, message, exception));
                }
            };
        }

        internal IReadOnlyList<(GeometryHelperLogLevel Level, string Message, Exception Exception)> Entries
        {
            get
            {
                lock (_entries)
                {
                    return _entries.ToList();
                }
            }
        }

        public void Dispose()
        {
            GeometryHelperLog.Writer = null;
        }
    }
}
