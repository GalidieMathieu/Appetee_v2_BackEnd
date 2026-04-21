using Xunit;

// The integration suite shares one isolated MySQL database, so tests must not run in parallel.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
