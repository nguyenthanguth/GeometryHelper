using GeometryHelper.CommonGeometry;
using Xunit;

// Tolerance.Global is a mutable static that every geometric comparison reads, and CloneTests widens it
// to check that a clone is compared under the caller's tolerance rather than the one in force when it
// was built. xUnit runs test classes in parallel by default, so that test would be changing the ground
// under whatever else happens to be running at that moment, making unrelated tests fail at random.
// Running the suite serially removes that whole class of flakiness; the full run takes under a second
// either way.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
