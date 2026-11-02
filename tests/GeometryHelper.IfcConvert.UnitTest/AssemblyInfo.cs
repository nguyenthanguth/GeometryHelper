using Xunit;

// xBIM sets up its services, once a process, the first time a store asks for them, and takes no lock doing it.
// IfcStoreCache waits for that, but a test that creates an IfcStore itself (IfcPropertyModelTests and
// GeometryCacheTests do) does not, and threw "Collection was modified" when a test in another class set xBIM up at
// the same moment. Nearly every class is in the "IfcEngine" collection anyway, so the whole assembly runs one test
// at a time.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
