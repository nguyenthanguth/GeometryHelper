using GeometryHelper;
using Xunit;

// Tolerance.Global is process-wide state, and a handful of tests set it to show what a looser or tighter
// setting does. They restore it in a finally, but two tests running at once would still see each other's
// setting, so the whole assembly runs one test at a time.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
