using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Broodline.TestHarness
{
    /// Runs the EditMode suites without Unity's TestJobRunner AND without
    /// NUnit's own execution engine. Two prior attempts, in order:
    ///
    /// ROUND 0 (-runTests): deadlocks in
    /// Unity.PerformanceTesting.Editor.TestRunBuilder.Setup(), an
    /// IPrebuildSetup Unity's own task-based launcher (UnityEditor.TestRunner,
    /// specifically PrebuildSetupTask) discovers and calls. IPrebuildSetup is
    /// defined in UnityEngine.TestRunner, not nunit.framework.dll - confirmed
    /// by reflecting the shipped assembly - so this is specific to Unity's
    /// launcher pipeline, not something stock NUnit ever touches.
    ///
    /// ROUND 1 (NUnit's NUnitTestAssemblyRunner.Run()): that method builds
    /// its own SimpleWorkItemDispatcher internally with no public override,
    /// and that dispatcher hands the top-level work item to a thread IT
    /// creates - proven with an isolated, Unity-free repro where even a
    /// trivial synchronous [Test] ran on a different managed thread than the
    /// caller. Fixed by bypassing Run()/RunAsync() and driving a
    /// hand-built WorkItem through a one-line IWorkItemDispatcher whose
    /// Dispatch() just calls work.Execute() on the calling thread instead of
    /// handing off. That got real Unity API calls onto the main thread (a
    /// second isolated repro with multiple fixtures, SetUp/TearDown,
    /// OneTimeSetUp and TestCase confirmed the mechanism works generally) -
    /// but the real run inside this Editor still hung, same Monitor-wait
    /// signature as Round 0. At the time this was blamed on
    /// NUnit.Framework.Internal.Execution.CompositeWorkItem's private
    /// child-completion countdown (_childTestCountdown, OnChildCompleted,
    /// RunChildren are all private - confirmed by reflection) on the theory
    /// that it assumes children signal completion from another thread. That
    /// was never confirmed and, in hindsight, most likely was not it: Round
    /// 3 (below) found the real mechanism, and NUnit's own async-test
    /// handling would hit that exact same mechanism internally for the same
    /// test methods, regardless of which dispatcher drives the surrounding
    /// WorkItem tree. Recorded here anyway, wrong turn included, so nobody
    /// re-spends the two rounds it took to rule it out.
    ///
    /// ROUND 2 (this file): does not touch NUnit's execution engine at all.
    /// Walks each assembly with plain System.Reflection - finds fixtures,
    /// finds [Test]/[TestCase] methods, constructs, calls [SetUp] /
    /// [OneTimeSetUp] / the test / [TearDown] / [OneTimeTearDown] itself,
    /// and records pass/fail from whether the call threw. Nothing here can
    /// hand execution to another thread, because nothing here creates one -
    /// every call happens synchronously, in the order written, on whatever
    /// thread calls Run() (Unity's main thread, under -executeMethod).
    ///
    /// ROUND 3: this STILL hung, 59 tests in, on
    /// Broodline.UI.Tests.LoopGuardTests.LoadAsyncAgainstADeadServer_
    /// RecordsTheFailureOnTheCache - found only by logging each test
    /// immediately before invoking it and reading the last line printed.
    /// That test is a plain [Test] (not async Task - a signature scan for
    /// "async Task" cannot find this shape) whose body does
    /// `roster.LoadAsync(api).GetAwaiter().GetResult()`: classic
    /// sync-over-async. LoadAsync's internal `await` posts its continuation
    /// to Unity's SynchronizationContext, which only runs queued
    /// continuations when the main thread pumps - and the main thread is
    /// the one sitting inside GetResult() waiting for that continuation.
    /// It cannot arrive. The same hazard exists in the 9 async Task
    /// [Test] methods this file already awaits via GetAwaiter().GetResult()
    /// (SessionTests x3, OutboxClientTests x6) - LoopGuardTests just hit it
    /// first, in method-declaration order, because it comes before them.
    ///
    /// THE FIX: suppress the synchronization context for the duration of
    /// each invocation (see RunOne). This is not the same as moving the
    /// test to another thread - the test body still executes on Unity's
    /// main thread throughout, so Resources.Load / AssetDatabase / UI
    /// Toolkit / GameObject calls keep working exactly as Round 2 already
    /// had them working, which is what Round 1's worker-thread approach
    /// broke. All suppressing the context changes is where an `await`'s
    /// continuation resolves: with SynchronizationContext.Current null, it
    /// resolves on the thread pool instead of queueing for a main thread
    /// that is blocked waiting on it. That is the whole reason this
    /// approach can satisfy both classes of test at once.
    ///
    /// RESIDUAL RISK, CHECKED NOT ASSUMED: a test that touches a
    /// main-thread-only Unity API AFTER an await would now throw instead of
    /// hanging, because that continuation runs on a thread-pool thread once
    /// the context is suppressed. Read every one of the 10 affected test
    /// bodies (the 9 async Task methods plus LoopGuardTests's one
    /// sync-over-async [Test]) and their production call chains before
    /// shipping this:
    ///   - SAFE: LoopGuardTests.LoadAsyncAgainstADeadServer... - the only
    ///     state touched after the await is RosterScreen.MarkLoadFailed()
    ///     and ServerError.From(), neither of which touches UnityEngine.
    ///   - SAFE: OutboxClientTests's 6 - OutboxClient/BroodlineApiClient/
    ///     Outbox/OutboxStore never call a UnityEngine API on this path
    ///     (OutboxClient.cs does `using UnityEngine;` for
    ///     Application.internetReachability, but every one of these 6
    ///     tests supplies its own isOffline delegate, so that line never
    ///     runs).
    ///   - SAFE: SessionTests.ColdStart_RendersTheCachedSnapshotBefore
    ///     SyncReturns - Session.ColdStartAsync reads Application.version
    ///     as an argument, but this test's auth store already has a token,
    ///     so the `?? await CreateGuestAsync()` short-circuits and
    ///     Application.version is read before any await ever happens.
    ///   - LOOKED RISKY, RAN SAFE: SessionTests.ColdStart_WithNoAccount_
    ///     CreatesAGuestAndPersistsTheTokens and ColdStart_RetriesOnceAfter
    ///     A401_WithTheRefreshedToken. Both reach `Application.version` in
    ///     Session.ColdStartAsync AFTER a real await
    ///     (CreateGuestAsync, respectively RefreshAsync on the 401 retry
    ///     path) - on paper, exactly the shape that should now throw a
    ///     main-thread exception instead of hanging. They don't; both pass
    ///     (confirmed by running - see task-1a-report.md, fix round 3).
    ///     Why: every mock HttpMessageHandler these two reach
    ///     (RecordingHandler, RefreshFlowHandler) answers with
    ///     Task.FromResult(...), an ALREADY-COMPLETED task. C#'s async
    ///     state machine checks IsCompleted before ever scheduling a
    ///     continuation; on an already-completed awaitable it just keeps
    ///     executing inline on the same thread instead of yielding. No
    ///     continuation is scheduled, so SynchronizationContext.Current
    ///     never comes into it at all - the "await" never actually
    ///     suspends. This is specific to these tests faking the network
    ///     synchronously; do not read it as "post-await Unity calls are
    ///     generally safe here" for a test written differently.
    ///
    /// WHAT THIS DELIBERATELY DOES NOT SUPPORT - said here, not just in the
    /// task report, because a runner that hides its own limits is exactly
    /// the failure mode this phase keeps finding:
    ///   - Assert.Multiple (a failure inside one still aborts the test
    ///     immediately rather than collecting every failure in the block)
    ///   - parameterised sources beyond a literal [TestCase(...)] -
    ///     [TestCaseSource], [ValueSource], [Combinatorial], [Random],
    ///     [TestFixtureSource] are not recognised at all
    ///   - inherited [Test]/[SetUp]/[TearDown]/[OneTimeSetUp]/
    ///     [OneTimeTearDown] - only members DECLARED directly on the
    ///     fixture class are discovered, never ones a base class
    ///     contributes
    ///   - [Explicit], [Repeat], [Timeout], [Parallelizable], [Retry],
    ///     [SetUpFixture], [Category]-based filtering - not recognised
    /// Checked source for all of the above across the six EditMode
    /// assemblies before shipping this: none of them are used anywhere in
    /// this codebase's EditMode suite. Plain [SetUp]/[TearDown] IS used
    /// (WaveRunnerTests, OutboxClientTests, OutboxTests) and IS supported.
    /// An async Task [Test] method is awaited synchronously
    /// (GetAwaiter().GetResult(), which surfaces the real exception rather
    /// than an AggregateException) - 9 such methods exist
    /// (SessionTests x3, OutboxClientTests x6), none are async void.
    ///
    /// PLAYMODE IS NOT COVERED. run-unity-tests.sh PlayMode still uses
    /// -runTests and is expected to deadlock the same way. No task in
    /// Phase 8 needs it; whoever needs it next owns that problem.
    public static class EditModeRunner
    {
        static readonly string[] Assemblies =
        {
            "Broodline.UI.Tests", "Broodline.Game.Tests", "Broodline.View.Tests",
            "Broodline.Net.Tests", "Broodline.Benchmark.Tests", "Broodline.EditorBuild.Tests",
            "Broodline.Creatures.Tests", "Broodline.Frontier.Tests",
        };

        sealed class CaseResult
        {
            public string FullName;
            public bool Passed;
            public bool Skipped;
            public string Message;
        }

        const BindingFlags Declared =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        public static void Run()
        {
            string outPath = ArgAfter("-testResults")
                             ?? Path.Combine(Directory.GetCurrentDirectory(), "test-results-EditMode.xml");

            var results = new List<CaseResult>();
            var problems = new List<string>();

            foreach (var name in Assemblies)
            {
                var asm = AppDomain.CurrentDomain.GetAssemblies()
                                   .FirstOrDefault(a => a.GetName().Name == name);
                if (asm == null)
                {
                    // Loud, not silent: a missing assembly is indistinguishable
                    // from a passing one in the counts otherwise, which is the
                    // failure mode this whole phase keeps finding in records.
                    problems.Add($"assembly not loaded: {name}");
                    continue;
                }

                int before = results.Count;
                RunAssembly(asm, results, problems);
                if (results.Count == before)
                    problems.Add($"assembly loaded but found zero tests: {name}");
            }

            int total = results.Count;
            int passed = results.Count(c => c.Passed);
            int skipped = results.Count(c => c.Skipped);
            int failed = total - passed - skipped;

            WriteXml(outPath, results, total, passed, failed, skipped);

            Debug.Log($"[EditModeRunner] total={total} passed={passed} failed={failed} skipped={skipped}");
            foreach (var c in results.Where(c => !c.Passed && !c.Skipped))
                Debug.Log($"[EditModeRunner] FAILED {c.FullName}: {c.Message}");
            foreach (var p in problems) Debug.Log("[EditModeRunner] " + p);

            // Any problem (missing assembly, zero tests found, a fixture that
            // could not be constructed) fails the gate outright, same reason
            // as Round 1: a runner that can go quietly wrong is worse than
            // one that cannot run at all.
            bool broken = failed > 0 || problems.Count > 0;
            EditorApplication.Exit(broken ? 2 : 0);
        }

        static void RunAssembly(Assembly asm, List<CaseResult> results, List<string> problems)
        {
            Type[] types;
            try { types = asm.GetTypes(); }
            catch (ReflectionTypeLoadException ex)
            {
                // KEEP GOING, BUT SAY SO. Taking the types that did load is
                // right - one broken type should not cost the other fixtures
                // in the assembly. Discarding the loader errors was not: a
                // whole test class could vanish and the run still exited 0,
                // because the only nearby guard fires at assembly granularity
                // ("found zero tests"), which a PARTIAL load never trips.
                //
                // `problems` is what feeds the exit code at the top of Run, so
                // recording them here is what turns this from a silent pass
                // into a red run. This file's own doctrine: a runner that can
                // go quietly wrong is worse than one that cannot run at all.
                types = ex.Types.Where(t => t != null).ToArray();
                foreach (var le in ex.LoaderExceptions.Where(e => e != null).Take(10))
                    problems.Add($"type load failed in {asm.GetName().Name}: {le.Message}");
                problems.Add($"{asm.GetName().Name}: {ex.Types.Count(t => t == null)} type(s) failed to load "
                    + "and were skipped - the tests in them did not run");
            }

            foreach (var type in types)
            {
                if (type.IsAbstract || type.IsInterface || type.IsGenericTypeDefinition) continue;

                var declared = type.GetMethods(Declared);
                var testMethods = declared
                    .Where(m => m.GetCustomAttribute<TestAttribute>() != null || m.GetCustomAttributes<TestCaseAttribute>().Any())
                    .ToArray();
                if (testMethods.Length == 0) continue; // not a fixture we recognise

                object instance;
                try { instance = Activator.CreateInstance(type); }
                catch (Exception ex)
                {
                    problems.Add($"could not construct fixture {type.FullName}: {Unwrap(ex).Message}");
                    continue;
                }

                var oneTimeSetUp = declared.Where(m => m.GetCustomAttribute<OneTimeSetUpAttribute>() != null).ToArray();
                var oneTimeTearDown = declared.Where(m => m.GetCustomAttribute<OneTimeTearDownAttribute>() != null).ToArray();
                var setUp = declared.Where(m => m.GetCustomAttribute<SetUpAttribute>() != null).ToArray();
                var tearDown = declared.Where(m => m.GetCustomAttribute<TearDownAttribute>() != null).ToArray();

                Exception oneTimeSetUpFailure = null;
                foreach (var m in oneTimeSetUp)
                {
                    try { InvokeAndAwait(m, instance, null); }
                    catch (Exception ex) { oneTimeSetUpFailure = Unwrap(ex); break; }
                }

                foreach (var method in testMethods)
                {
                    var cases = method.GetCustomAttributes<TestCaseAttribute>().ToArray();
                    var invocations = cases.Length == 0
                        ? new[] { (label: $"{type.FullName}.{method.Name}", args: (object[])null) }
                        : cases.Select(tc => ($"{type.FullName}.{method.Name}({string.Join(",", tc.Arguments.Select(a => a?.ToString() ?? "null"))})", tc.Arguments)).ToArray();

                    foreach (var (label, args) in invocations)
                    {
                        if (oneTimeSetUpFailure != null)
                        {
                            results.Add(new CaseResult { FullName = label, Passed = false, Message = "[OneTimeSetUp] failed for fixture: " + oneTimeSetUpFailure.Message });
                            continue;
                        }
                        RunOne(instance, method, args, label, setUp, tearDown, results, problems);
                    }
                }

                foreach (var m in oneTimeTearDown)
                {
                    try { InvokeAndAwait(m, instance, null); }
                    catch (Exception ex) { problems.Add($"[OneTimeTearDown] threw in {type.FullName}: {Unwrap(ex).Message}"); }
                }

                (instance as IDisposable)?.Dispose();
            }
        }

        static void RunOne(object instance, MethodInfo method, object[] args, string fullName,
                            MethodInfo[] setUp, MethodInfo[] tearDown, List<CaseResult> results, List<string> problems)
        {
            Exception failure = null;

            // Fix round 3, Step 2, WIDENED: suppress the ambient
            // SynchronizationContext for the whole case - setup, test AND
            // teardown - not just the test call. See the file header ("THE
            // FIX") for why this is not the same as running on another
            // thread: this thread doesn't change, only where an internal
            // await's continuation resolves.
            //
            // WHY IT HAD TO WIDEN. The suppression used to wrap the test
            // invocation alone, while setup ran before it and teardown ran in
            // the finally after the context had been put back. All three go
            // through InvokeAndAwait, which does GetAwaiter().GetResult(); so
            // an `async Task [SetUp]` with any await that does not complete
            // synchronously queued its continuation to the main thread while
            // that thread sat blocked waiting for it - the exact round-3
            // deadlock, in the two places the fix did not cover. The run
            // would hang with no output past the "->" line below, and
            // InvokeAndAwait's own comment advertises async setup/teardown as
            // supported, so nobody had a warning.
            var prevContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
            try
            {
                try
                {
                    foreach (var s in setUp) InvokeAndAwait(s, instance, null);
                    // Fix round 3, Step 1 (coordinator-mandated observability
                    // before any fix): logged immediately before the call that
                    // might hang, so if the process freezes again the last line
                    // of the log names exactly which test it froze on, rather
                    // than leaving that a guess. Kept permanently, not just for
                    // this round - it cost one run to find the first hang and
                    // will cost one run to find the next.
                    Debug.Log($"[EditModeRunner] -> {fullName}");

                    InvokeAndAwait(method, instance, args);
                }
                catch (Exception ex)
                {
                    failure = Unwrap(ex);
                }
                finally
                {
                    foreach (var t in tearDown)
                    {
                        try { InvokeAndAwait(t, instance, null); }
                        catch (Exception ex) { problems.Add($"[TearDown] threw for {fullName}: {Unwrap(ex).Message}"); }
                    }
                }
            }
            finally { SynchronizationContext.SetSynchronizationContext(prevContext); }

            if (failure == null)
                results.Add(new CaseResult { FullName = fullName, Passed = true });
            else if (IsSkip(failure))
                results.Add(new CaseResult { FullName = fullName, Skipped = true, Message = failure.Message });
            else
                results.Add(new CaseResult { FullName = fullName, Passed = false, Message = failure.Message });
        }

        /// Invokes synchronously. If the method returns a Task (an async
        /// Task [Test]/[SetUp]/[TearDown]), waits for it via the awaiter
        /// rather than .Wait()/.Result so a real failure surfaces as itself,
        /// not wrapped in an AggregateException.
        static void InvokeAndAwait(MethodInfo method, object instance, object[] args)
        {
            var ret = method.Invoke(instance, args);
            if (ret is Task task) task.GetAwaiter().GetResult();
        }

        static bool IsSkip(Exception ex)
        {
            var n = ex.GetType().Name;
            return n == "IgnoreException" || n == "InconclusiveException" || n == "SuccessException";
        }

        static Exception Unwrap(Exception ex) =>
            ex is TargetInvocationException tie && tie.InnerException != null ? tie.InnerException : ex;

        static string ArgAfter(string flag)
        {
            var a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == flag) return a[i + 1];
            return null;
        }

        /// Deliberately flat - one <test-case> per result, no <test-suite>
        /// nesting - because run-unity-tests.sh's python parser reads
        /// total/passed/failed/skipped off the root element and finds every
        /// <test-case> with Element.iter, which does not care about depth.
        static void WriteXml(string path, List<CaseResult> results, int total, int passed, int failed, int skipped)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using var w = new StreamWriter(path);
            w.WriteLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            w.WriteLine($"<test-run id=\"1\" testcasecount=\"{total}\" result=\"{(failed > 0 ? "Failed" : "Passed")}\" " +
                        $"total=\"{total}\" passed=\"{passed}\" failed=\"{failed}\" " +
                        $"inconclusive=\"0\" skipped=\"{skipped}\" asserts=\"0\">");
            foreach (var c in results)
            {
                string result = c.Passed ? "Passed" : c.Skipped ? "Skipped" : "Failed";
                if (c.Passed)
                {
                    w.WriteLine($"<test-case fullname=\"{Escape(c.FullName)}\" result=\"{result}\" />");
                }
                else
                {
                    w.WriteLine($"<test-case fullname=\"{Escape(c.FullName)}\" result=\"{result}\">");
                    w.WriteLine($"<failure><message>{Escape(c.Message ?? "")}</message></failure>");
                    w.WriteLine("</test-case>");
                }
            }
            w.WriteLine("</test-run>");
        }

        static string Escape(string s) =>
            (s ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
    }
}
