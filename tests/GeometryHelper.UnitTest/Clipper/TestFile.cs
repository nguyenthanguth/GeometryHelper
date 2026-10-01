using System;
using System.IO;

namespace GeometryHelper.UnitTest.Clipper
{
    /// <summary>
    /// Where Clipper2's own tests find their cases. Upstream they are read from Tests\ at the root of the Clipper2
    /// repository; here from Data\, copied next to the test assembly.
    /// </summary>
    internal static class TestFile
    {
        public static string Of(string name) => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Clipper", "Data", name);
    }
}
