using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace TimePlanning.Pn.Test;

/// <summary>
/// Meta-guard: every test class in this assembly must be named in the
/// <c>FullyQualifiedName=</c> shard filters of BOTH GitHub Actions workflows.
///
/// WHY THIS EXISTS
/// ---------------
/// CI does not run `dotnet test` over the whole assembly. It fans out across a
/// matrix of shards, each one a `--filter` listing the classes it owns. A class
/// that is in no filter is never executed — vstest reports nothing, the shard is
/// green, the gate job is green, and the PR merges. The failure mode is total
/// silence, which is why it has repeatedly gone unnoticed for months at a time
/// (<see cref="CorruptedPauseIdRepairTests"/> sat unsharded long enough to
/// accumulate a test that could never have passed).
///
/// This fixture turns that silence into a red build. It needs no database.
/// </summary>
[TestFixture]
public class ShardCoverageTests
{
    private static readonly string[] WorkflowFileNames =
    [
        "dotnet-core-pr.yml",
        "dotnet-core-master.yml"
    ];

    /// <summary>
    /// The job that owns the shard matrix, and the path to the filter strings
    /// inside it. Quoted in the failure message so the reader has an anchor into
    /// a several-hundred-line YAML file instead of just a path to it.
    /// </summary>
    private const string ShardMatrixLocation = "job `test-dotnet`, `strategy.matrix.shard[].filter`";

    /// <summary>
    /// Identifies a workflow file as belonging to THIS repository. Filename alone
    /// is not enough: the host application this plugin is copied into during dev
    /// mode (eform-angular-frontend) ships workflows with these exact same two
    /// names, and the walk-up below would otherwise stop there.
    /// </summary>
    private const string OurFilterMarker = "FullyQualifiedName=TimePlanning.Pn.Test.";

    /// <summary>
    /// Matches one entry of a shard filter, e.g.
    /// <c>FullyQualifiedName=TimePlanning.Pn.Test.GrpcServices.FooTests</c>.
    /// </summary>
    private static readonly Regex FilterEntry = new(@"FullyQualifiedName=([A-Za-z0-9_.+]+)");

    [Test]
    public void EveryTestClassIsAssignedToAShardInBothWorkflows()
    {
        var (workflowDir, shardedPerFile) = FindWorkflows();
        var discovered = DiscoverTestClasses();

        // Non-vacuous self-check: this very fixture must come back out of
        // discovery. Asserting merely that the list is non-empty would always
        // hold — this class always qualifies — and would not exercise
        // IsTestMethod, the part most likely to be broken by an NUnit upgrade or
        // a careless edit.
        Assert.That(discovered, Does.Contain(typeof(ShardCoverageTests).FullName),
            "Discovery did not find ShardCoverageTests itself, so it is not reliably finding anything. " +
            "Fix the reflection in this file — it is no longer protecting the suite.");

        var missing = discovered
            .Select(fqn => (
                Fqn: fqn,
                AbsentFrom: WorkflowFileNames.Where(f => !shardedPerFile[f].Contains(fqn)).ToArray()))
            .Where(x => x.AbsentFrom.Length > 0)
            .OrderBy(x => x.Fqn, StringComparer.Ordinal)
            .ToList();

        if (missing.Count == 0)
        {
            return;
        }

        var message = new StringBuilder();
        message.AppendLine(
            $"{missing.Count} test class(es) are missing from the CI shard filters " +
            "and so do not run in at least one workflow:");
        message.AppendLine();
        foreach (var (fqn, absentFrom) in missing)
        {
            message.AppendLine($"  {fqn}");
            message.AppendLine($"      absent from: {string.Join(", ", absentFrom)}");
        }

        message.AppendLine();
        message.AppendLine("HOW TO FIX");
        message.AppendLine($"  Add each class above to exactly one shard's `filter:` string ({ShardMatrixLocation})");
        message.AppendLine("  in BOTH files:");
        foreach (var name in WorkflowFileNames)
        {
            message.AppendLine($"    {Path.Combine(workflowDir, name)}");
        }

        message.AppendLine("  The two files' shard filters must list the same classes — a class named in one");
        message.AppendLine("  and not the other runs on master but not on PRs (or the reverse), which is the");
        message.AppendLine("  same bug half-fixed. The files differ in other ways; only the filters must match.");
        message.AppendLine();
        message.AppendLine("  Append to the chosen shard's filter, pipe-separated, e.g.:");
        message.AppendLine($"    |FullyQualifiedName={missing[0].Fqn}");
        message.AppendLine();
        message.AppendLine("  Pick the shard BY COST, not alphabetically. Every fixture deriving from");
        message.AppendLine("  TestBaseSetup starts its own MariaDB Testcontainer, and those containers dominate");
        message.AppendLine("  a shard's wall-clock; a fixture with no database costs almost nothing and can go");
        message.AppendLine("  anywhere. Shard 'c' is already the DB-dense one (11 of its 12 classes derive");
        message.AppendLine("  TestBaseSetup), so keep new DB-backed fixtures out of it. Shard durations shift a");
        message.AppendLine("  lot run to run (runner contention, Testcontainers image pulls), so check the");
        message.AppendLine("  per-shard durations of the last green master run before choosing rather than");
        message.AppendLine("  trusting any figure written down here.");
        message.AppendLine();
        message.AppendLine("  Do NOT silence this guard. If a class genuinely must not be sharded, it should");
        message.AppendLine("  not be carrying [Test] methods in this assembly.");

        throw new AssertionException(message.ToString());
    }

    /// <summary>
    /// Every concrete type in this assembly that owns or inherits at least one
    /// NUnit test method, named the way the vstest <c>FullyQualifiedName=</c>
    /// filter matches on.
    /// </summary>
    private static List<string> DiscoverTestClasses()
    {
        const BindingFlags MethodFlags =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        return Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => t.IsClass)
            // Excluded: abstract fixtures such as TestBaseSetup. NUnit never runs an
            // abstract type as a fixture; any [Test] it declares executes under each
            // concrete subclass's own name, and it is the subclass that needs sharding.
            .Where(t => !t.IsAbstract)
            // Excluded: open generic definitions. NUnit can only run those via
            // [TestFixture(typeof(...))], which produces closed types, and there are
            // none in this assembly today. If one ever appears this exclusion should be
            // revisited rather than widened.
            .Where(t => !t.IsGenericTypeDefinition)
            .Where(t => t.GetMethods(MethodFlags).Any(IsTestMethod))
            .Select(t => t.FullName!)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();
    }

    private static bool IsTestMethod(MethodInfo method) =>
        method.GetCustomAttributes(inherit: true)
            .Any(a => a is TestAttribute or TestCaseAttribute or TestCaseSourceAttribute);

    private static HashSet<string> ParseShardedClasses(string workflowText) =>
        FilterEntry.Matches(workflowText)
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Reads a workflow with its YAML comment lines removed, so that a shard
    /// entry someone commented out does not count as covered — that would be a
    /// false-green, and it is the only way to fool this guard without editing the
    /// C#. A class name can never contain '#', so dropping '#'-leading lines
    /// cannot discard a live entry.
    /// </summary>
    private static string ReadUncommented(string path) =>
        string.Join('\n', File.ReadLines(path).Where(line => !line.TrimStart().StartsWith('#')));

    /// <summary>
    /// Walks up from the test binary's directory looking for THIS repository's
    /// <c>.github/workflows</c> folder — the tests run out of
    /// <c>.../TimePlanning.Pn.Test/bin/&lt;config&gt;/&lt;tfm&gt;/</c>, so the repo root is
    /// several levels above and its depth differs between local and CI layouts.
    ///
    /// A directory qualifies only when both workflow files are present AND both
    /// carry <see cref="OurFilterMarker"/>. Filenames alone do not identify us:
    /// eform-angular-frontend — the host application this plugin is copied into
    /// during Base/Full dev mode — has workflows with the same two names and no
    /// shard filters at all. Matching on content walks past those and keeps
    /// climbing, so a dev-mode run ends in the "not found" failure below, which
    /// lists what it skipped, rather than misdiagnosing the host's workflows as
    /// our own with a broken parser.
    /// </summary>
    private static (string Directory, Dictionary<string, HashSet<string>> ShardedPerFile) FindWorkflows()
    {
        var workflowDirsSeen = new List<string>();

        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, ".github", "workflows");
            if (!Directory.Exists(candidate))
            {
                continue;
            }

            workflowDirsSeen.Add(candidate);

            var paths = WorkflowFileNames.ToDictionary(f => f, f => Path.Combine(candidate, f));
            if (!paths.Values.All(File.Exists))
            {
                continue;
            }

            var texts = paths.ToDictionary(p => p.Key, p => ReadUncommented(p.Value));
            if (!texts.Values.All(t => t.Contains(OurFilterMarker, StringComparison.Ordinal)))
            {
                continue;
            }

            return (candidate, texts.ToDictionary(t => t.Key, t => ParseShardedClasses(t.Value)));
        }

        // Deliberately a failure, not a silent pass: a guard that gives up when it
        // cannot find its input is indistinguishable from no guard at all, and would
        // let exactly the bug it exists to catch back through.
        throw new AssertionException(
            "Could not locate this repository's .github/workflows directory — one holding " +
            $"[{string.Join(", ", WorkflowFileNames)}] with shard filters naming '{OurFilterMarker}...'.\n" +
            $"  Started from: {AppContext.BaseDirectory}\n" +
            "  .github/workflows directories seen while walking up (none qualified): " +
            (workflowDirsSeen.Count == 0 ? "(none)" : string.Join(", ", workflowDirsSeen)) + "\n" +
            "  If one of those belongs to eform-angular-frontend, this is a dev-mode copy of the plugin " +
            "inside the host app and there is nothing here to check — run these tests from a full checkout " +
            "of eform-angular-timeplanning-plugin. Otherwise fix the lookup; do not delete the guard.");
    }
}
