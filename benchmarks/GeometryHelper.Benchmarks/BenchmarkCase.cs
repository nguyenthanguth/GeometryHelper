using System;

namespace GeometryHelper.Benchmarks
{
    /// <summary>
    /// One benchmark: its name, how big its input is, and how to build the fixture it times.
    /// </summary>
    /// <remarks>
    /// The fixture is built only when the case is run, so that a run filtered down to a few cases does not build the
    /// others, and building it is never timed.
    /// </remarks>
    internal sealed class BenchmarkCase
    {
        private readonly Func<Fixture> _prepare;

        /// <summary>
        /// Initializes a benchmark.
        /// </summary>
        /// <param name="name">The name, dotted from the general to the particular: <c>Clash.AxisTrap.OneSet.X</c>.</param>
        /// <param name="size">How big the input is, in <paramref name="unit"/>.</param>
        /// <param name="unit">What the size counts: parts, edges, faces, labels.</param>
        /// <param name="heavy">Whether a sample takes long enough to be taken fewer times; see <see cref="Profile"/>.</param>
        /// <param name="prepare">Builds the fixture: the inputs, and the call timed on them.</param>
        internal BenchmarkCase(string name, long size, string unit, bool heavy, Func<Fixture> prepare)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Size = size;
            Unit = unit ?? throw new ArgumentNullException(nameof(unit));
            Heavy = heavy;
            _prepare = prepare ?? throw new ArgumentNullException(nameof(prepare));
        }

        /// <summary>Gets the name, unique among all the cases.</summary>
        internal string Name { get; }

        /// <summary>Gets how big the input is, in <see cref="Unit"/>.</summary>
        internal long Size { get; }

        /// <summary>Gets what <see cref="Size"/> counts.</summary>
        internal string Unit { get; }

        /// <summary>Gets whether a sample takes long enough for the case to be sampled fewer times.</summary>
        internal bool Heavy { get; }

        /// <summary>
        /// Builds the fixture.
        /// </summary>
        internal Fixture Prepare() => _prepare();
    }

    /// <summary>
    /// The inputs of a benchmark, built: the call that is timed, how its result is signed, and what is worth knowing of
    /// the inputs.
    /// </summary>
    internal sealed class Fixture
    {
        private readonly Func<object> _run;
        private readonly Action<ResultSignature, object> _sign;
        private readonly Func<object, string> _describe;

        private Fixture(Func<object> run, Action<ResultSignature, object> sign, Func<object, string> describe, string note)
        {
            _run = run;
            _sign = sign;
            _describe = describe;
            Note = note ?? string.Empty;
        }

        /// <summary>
        /// Gets what is worth knowing of the inputs, such as how many pairs a sweep along each axis has to look at.
        /// </summary>
        internal string Note { get; }

        /// <summary>
        /// Makes a fixture.
        /// </summary>
        /// <typeparam name="T">The type of the result.</typeparam>
        /// <param name="run">The call that is timed, every input already built.</param>
        /// <param name="sign">Writes every observable value of a result into a signature, in order.</param>
        /// <param name="describe">Says in a few words what a result holds, for the report: 49 hard, 49 touching.</param>
        /// <param name="note">What is worth knowing of the inputs; null for nothing.</param>
        internal static Fixture Of<T>(Func<T> run, Action<ResultSignature, T> sign, Func<T, string> describe, string note)
        {
            if (run == null)
            {
                throw new ArgumentNullException(nameof(run));
            }

            if (sign == null)
            {
                throw new ArgumentNullException(nameof(sign));
            }

            if (describe == null)
            {
                throw new ArgumentNullException(nameof(describe));
            }

            return new Fixture(() => run(), (signature, result) => sign(signature, (T)result), result => describe((T)result), note);
        }

        /// <summary>
        /// Runs the timed call once.
        /// </summary>
        internal object Run() => _run();

        /// <summary>
        /// Signs a result of <see cref="Run"/>.
        /// </summary>
        /// <returns>The signature, 64 hexadecimal digits.</returns>
        internal string Sign(object result)
        {
            using (var signature = new ResultSignature())
            {
                _sign(signature, result);
                return signature.Finish();
            }
        }

        /// <summary>
        /// Says in a few words what a result of <see cref="Run"/> holds.
        /// </summary>
        internal string Describe(object result) => _describe(result);
    }
}
