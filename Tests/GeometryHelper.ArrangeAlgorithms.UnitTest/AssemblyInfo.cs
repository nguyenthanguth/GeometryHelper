using GeometryHelper.CommonGeometry;
using Xunit;

// Tolerance.Global is a mutable static that every geometric comparison reads. xUnit runs test classes in
// parallel by default, so a test that widens the global tolerance to verify behaviour around it would be
// changing the ground under whatever else happens to be running at that moment, making unrelated tests
// fail at random. Running the suite serially removes that whole class of flakiness; the full run takes
// about a second either way.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
