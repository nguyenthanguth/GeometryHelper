using System;
using Xunit;

namespace GeometryHelper.Cad.Python.UnitTest
{
    /// <summary>
    /// The one host of the test process (D719, D723): Python starts once per process, so every test class of the collection
    /// shares this fixture. Python is the <c>python.exe</c> named by the environment variable <c>GH_PYTHON</c>, else the one
    /// the automatic order of D713 finds. With none, the fixture throws the environment's problem, so every test fails with
    /// the text that says what to install or set; no test is skipped.
    /// </summary>
    public sealed class PythonHostFixture : IDisposable
    {
        public PythonHostFixture()
        {
            Environment = CadPythonEnvironment.Find(
                System.Environment.GetEnvironmentVariable("GH_PYTHON") ?? string.Empty
            );

            if (!Environment.IsUsable)
            {
                throw new InvalidOperationException(Environment.Problem);
            }

            Owner = new TestPythonOwner();
            Host = new CadPythonHost(Environment, Owner);
        }

        public CadPythonEnvironment Environment { get; }

        internal TestPythonOwner Owner { get; }

        public CadPythonHost Host { get; }

        /// <summary>
        /// The end of the collection is the last test of the process (D723, H14): the host is disposed with no run going on,
        /// and Python must be shut down. A failure here is reported by xUnit as a failure of the collection's cleanup.
        /// </summary>
        public void Dispose()
        {
            Assert.False(Host.IsRunning);
            Host.Dispose();
            Assert.False(global::Python.Runtime.PythonEngine.IsInitialized);
        }
    }

    /// <summary>The collection that holds every test class that needs Python.</summary>
    [CollectionDefinition(Name)]
    public sealed class PythonHostCollection : ICollectionFixture<PythonHostFixture>
    {
        public const string Name = "Python host";
    }
}
