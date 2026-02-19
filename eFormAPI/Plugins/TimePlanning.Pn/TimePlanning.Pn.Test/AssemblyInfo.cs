using NUnit.Framework;

// Enable parallel test execution at the fixture level
// This allows different test classes to run in parallel while tests within each class run sequentially
// Tests within the same class share a Testcontainer instance, so they cannot run in parallel
[assembly: Parallelizable(ParallelScope.Fixtures)]

// Optionally set the level of parallelism (defaults to number of processors)
// Uncomment and adjust if needed to limit parallelism:
// [assembly: LevelOfParallelism(4)]
