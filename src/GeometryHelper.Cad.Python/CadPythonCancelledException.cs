using System;

namespace GeometryHelper.Cad.Python
{
    /// <summary>Thrown by <see cref="CadPythonScript"/> where Esc ended a question: <c>geometryhelper.cad</c> turns it into <c>Cancelled</c>.</summary>
    public sealed class CadPythonCancelledException : Exception
    {
        /// <summary>Makes the exception.</summary>
        public CadPythonCancelledException()
            : base("Function cancelled") { }
    }
}
