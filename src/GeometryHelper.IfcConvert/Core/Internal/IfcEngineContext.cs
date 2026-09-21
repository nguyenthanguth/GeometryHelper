using System;
using Xbim.Common.Geometry;
using Xbim.Geometry.Engine.Interop;
using Xbim.Ifc4.Interfaces;

namespace GeometryHelper.IfcConvert.Core.Internal
{
    /// <summary>
    /// Manages the lifecycle of xBIM geometry engines.
    /// Uses thread-local instances to ensure thread-safety when converting geometry concurrently.
    /// </summary>
    internal static class IfcEngineContext
    {
        [ThreadStatic]
        private static IXbimGeometryEngine _threadEngine;

        /// <summary>
        /// Gets an <see cref="IXbimGeometryEngine"/> instance dedicated to the calling thread.
        /// </summary>
        internal static IXbimGeometryEngine CurrentEngine
        {
            get
            {
                if (_threadEngine == null)
                {
                    _threadEngine = new XbimGeometryEngine();
                }

                return _threadEngine;
            }
        }
    }
}
