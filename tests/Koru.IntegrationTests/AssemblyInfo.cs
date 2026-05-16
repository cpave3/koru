using Xunit;

// Integration tests mutate process-global state (env vars, CWD).
// Run them serially so test classes don't clobber each other.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
