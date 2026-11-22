using System;

namespace GeometryHelper.Cad.Lisp
{
    /// <summary>
    /// <c>getenv</c>, <c>setenv</c>, <c>getcfg</c> and <c>setcfg</c>: the text a routine keeps between drawings and sessions, as
    /// AutoCAD 2021 keeps it in the registry (AutoCAD 2021, probes g1, g2, g14 of the C4a measurements). The store is
    /// <see cref="EnvironmentStore"/>.
    /// </summary>
    public sealed partial class LispInterpreter
    {
        private LispEnvironment _environment = new LispEnvironment();

        /// <summary>
        /// Gets or sets where <c>setenv</c> and <c>setcfg</c> keep their values. An interpreter has a store of its own in memory
        /// until a session gives it the one the window shares, which the Global settings file keeps. Null is taken as a new,
        /// empty store.
        /// </summary>
        internal LispEnvironment EnvironmentStore
        {
            get => _environment;
            set => _environment = value ?? new LispEnvironment();
        }

        private void RegisterEnvironment()
        {
            Define("GETENV", 1, 1, a => GetEnvironment(Str(a[0])));
            Define("SETENV", 2, 2, SetEnvironment);
            Define("GETCFG", 1, 1, a => EnvironmentStore.GetConfig(Str(a[0])));
            Define("SETCFG", 2, 2, SetConfig);
        }

        /// <summary>
        /// <c>(getenv name)</c>: the value <c>setenv</c> gave the name, else the Windows environment variable of that name, as
        /// text; nil for neither or an empty one. The name's case does not matter.
        /// </summary>
        private object GetEnvironment(string name)
        {
            string set = EnvironmentStore.GetEnvironment(name);

            if (set != null)
            {
                return set;
            }

            try
            {
                string variable = name.Length == 0 ? null : System.Environment.GetEnvironmentVariable(name);
                return string.IsNullOrEmpty(variable) ? null : variable;
            }
            catch (System.Security.SecurityException)
            {
                return null;
            }
        }

        /// <summary>
        /// <c>(setenv name value)</c>: keeps the value for the name and gives it back; the empty text takes the name away, so that
        /// <c>getenv</c> goes back to the Windows environment.
        /// </summary>
        private object SetEnvironment(object[] a)
        {
            string name = Str(a[0]);
            string value = Str(a[1]);
            OnOwner(() =>
            {
                EnvironmentStore.SetEnvironment(name, value);
                return null;
            });
            return value;
        }

        /// <summary>
        /// <c>(setcfg path value)</c>: keeps the value for a path such as <c>AppData/Tools/Last</c> and gives it back, the empty
        /// text too; nil, and nothing kept, for a path with no folder in it.
        /// </summary>
        private object SetConfig(object[] a)
        {
            string path = Str(a[0]);
            string value = Str(a[1]);
            bool kept = (bool)OnOwner(() => EnvironmentStore.SetConfig(path, value));
            return kept ? value : null;
        }
    }
}
