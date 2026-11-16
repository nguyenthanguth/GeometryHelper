using System.Runtime.CompilerServices;

namespace GeometryHelper.Dwg
{
    /// <summary>
    /// What the methods that a DWG's objects pass through by the hundred thousand ask of the compiler. Under .NET Core a method
    /// is first compiled with no optimisation, and again with it only after 30 calls and a quiet of 100 ms in which no other
    /// method is compiled for the first time, which a drawing's first reading, 0.6 s of calls to a few dozen methods, is
    /// mostly over before. Asked to be compiled fully at its first call, a method is quick from the first object.
    /// </summary>
    internal static class DwgJit
    {
        /// <summary>
        /// <c>MethodImplOptions.AggressiveOptimization</c>, which .NET Core 3.0 and later know and the .NET Standard 2.0 this is
        /// built for has no name for; .NET Framework ignores the flag, which it does not know.
        /// </summary>
        internal const MethodImplOptions Hot = (MethodImplOptions)512;
    }
}
