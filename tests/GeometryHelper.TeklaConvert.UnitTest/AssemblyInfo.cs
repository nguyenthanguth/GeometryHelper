using Xunit;

// GeometryHelperLog.Writer is process-wide state. The tests that capture what is logged would otherwise pick up
// messages written by tests running at the same moment in other classes. The whole suite takes about a second
// either way.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
