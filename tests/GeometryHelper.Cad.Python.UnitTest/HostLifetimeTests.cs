using System;
using System.IO;
using Xunit;

namespace GeometryHelper.Cad.Python.UnitTest
{
    /// <summary>
    /// The host as a whole (D713, D719): started once per process, with no usable Python it says why and the rest of the
    /// app works. That Dispose shuts Python down is checked where the collection ends, in
    /// <see cref="PythonHostFixture.Dispose"/>.
    /// </summary>
    public sealed class HostLifetimeTests : PythonHostTestBase
    {
        public HostLifetimeTests(PythonHostFixture fixture)
            : base(fixture) { }

        [Fact]
        public void TheHost_IsStartedAfterItsFirstRun_WithNoProblem()
        {
            CadCommandSession session = NewSession();

            RunLines(session, new[] { "pass" });

            Assert.True(Host.IsStarted);
            Assert.Null(Host.Problem);
            Assert.True(Host.Environment.IsUsable);
            Assert.False(Host.IsRunning);
        }

        [Fact]
        public void ASecondHostThatWouldStartPython_ThrowsBecausePythonIsStartedOncePerProcess()
        {
            // The first run makes sure Python is started; a second host that is asked to run starts none.
            CadCommandSession session = NewSession();
            RunLines(session, new[] { "pass" });

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            {
                using (var second = new CadPythonHost(Host.Environment, new TestPythonOwner()))
                {
                    second.Enter(session, "1 + 1");
                }
            });

            Assert.Equal("Python is started once per process.", error.Message);
        }

        [Fact]
        public void WithNoUsablePython_PyRunSaysWhy_AndTheAppGoesOn()
        {
            // A host made on a Python that is not there never touches pythonnet (a failed start cannot be tried again): a
            // run writes the environment's problem and nothing else happens. The session works as before.
            string missing = Path.Combine(Folder, "nowhere", "python.exe");
            CadPythonEnvironment environment = CadPythonEnvironment.Find(missing);
            var owner = new TestPythonOwner();
            CadCommandSession session = NewSession();
            owner.Session = session;

            using (var host = new CadPythonHost(environment, owner))
            {
                host.RunFile(session, Script("never.py", "print('ran')"));
                owner.Drain();

                Assert.False(host.IsStarted);
                Assert.Equal(environment.Problem, host.Problem);
                Assert.Equal("The Python set in Global settings (" + missing + ") was not found.", host.Problem);
                Assert.Contains(host.Problem, session.History);
            }

            Assert.True(session.Feed("CIRCLE"));
            Assert.True(session.Feed("0,0"));
            Assert.True(session.Feed("5"));
            Assert.Equal(1, session.Document.EntityCount);
        }
    }
}
