using Xunit;

// The library has one log writer for the whole process, which the tests of the log capture set and read back; the tests are fast enough to run one at a time.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
