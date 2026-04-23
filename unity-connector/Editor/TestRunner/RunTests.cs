using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityCliConnector.TestRunner
{
    [UnityCliTool(Description = "Run Unity EditMode or PlayMode tests and return results.")]
    public static class RunTests
    {
        private const int DefaultTimeoutMs = 120000;
        private const int TimeoutPaddingMs = 1000;

        internal static readonly string StatusDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".unity-cli", "status");

        public class Parameters
        {
            [ToolParameter("Test mode: EditMode or PlayMode", Required = true)]
            public string Mode { get; set; }

            [ToolParameter("Filter by namespace, class, or full test name")]
            public string Filter { get; set; }

            [ToolParameter("CLI request timeout in milliseconds")]
            public int TimeoutMs { get; set; }
        }

        public static Task<object> HandleCommand(JObject @params)
        {
            if (@params == null)
                return Task.FromResult<object>(new ErrorResponse("Parameters cannot be null."));

            var p = new ToolParams(@params);

            var modeResult = p.GetRequired("mode");
            if (!modeResult.IsSuccess)
                return Task.FromResult<object>(new ErrorResponse(modeResult.ErrorMessage));

            var modeStr = modeResult.Value.Trim();
            TestMode testMode;
            if (modeStr.Equals("EditMode", StringComparison.OrdinalIgnoreCase))
                testMode = TestMode.EditMode;
            else if (modeStr.Equals("PlayMode", StringComparison.OrdinalIgnoreCase))
                testMode = TestMode.PlayMode;
            else
                return Task.FromResult<object>(new ErrorResponse($"Unknown mode '{modeStr}'. Use EditMode or PlayMode."));

            var filter = p.Get("filter", null);
            var timeoutMs = Math.Max(TimeoutPaddingMs, p.GetInt("timeout_ms", DefaultTimeoutMs) ?? DefaultTimeoutMs);

            StartRun(testMode, filter, timeoutMs);
            return Task.FromResult<object>(new SuccessResponse("running", new { port = HttpServer.Port }));
        }

        private static void StartRun(TestMode mode, string filter, int timeoutMs)
        {
            var port = HttpServer.Port;

            try { var f = ResultsFilePath(port); if (File.Exists(f)) File.Delete(f); } catch { }
            try { var f = ProgressFilePath(port); if (File.Exists(f)) File.Delete(f); } catch { }
            TestRunnerState.MarkPending(port, filter);

            var passed  = new List<string>();
            var failed  = new List<string>();
            var skipped = new List<string>();
            var progress = new TestRunProgress(port, filter);
            string runGuid = null;

            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            var callbacks = new TestCallbacks(
                onRunStarted: tests =>
                {
                    progress.MarkRunStarted(tests);
                    WriteProgressFile(progress);
                },
                onTestStarted: test =>
                {
                    progress.MarkTestStarted(test);
                    WriteProgressFile(progress);
                },
                onResult: r =>
                {
                    CollectResult(r, passed, failed, skipped);
                    progress.MarkTestFinished(r);
                    WriteProgressFile(progress);
                },
                onFinished: _ =>
                {
                    Object.DestroyImmediate(api);
                    TestRunnerState.ClearPending(port);
                    WriteResultsFile(port, passed, failed, skipped, progress);
                }
            );

            api.RegisterCallbacks(callbacks);
            ArmResultsWatchdog(port, api, timeoutMs, filter, progress, () => runGuid);

            RunOnNextUpdate(() =>
                api.RetrieveTestList(mode, root =>
                {
                    progress.Total = CountMatchingTests(root, filter);
                    WriteProgressFile(progress);

                    if (progress.Total == 0)
                    {
                        Object.DestroyImmediate(api);
                        TestRunnerState.ClearPending(port);
                        WriteResultsFile(port, passed, failed, skipped, progress);
                        return;
                    }

                    runGuid = api.Execute(new ExecutionSettings(BuildFilter(mode, filter)));
                    progress.RunGuid = runGuid;
                    WriteProgressFile(progress);
                }));
        }

        private static void RunOnNextUpdate(Action action)
        {
            void Tick()
            {
                EditorApplication.update -= Tick;
                action();
            }

            EditorApplication.update += Tick;
        }

        private static void ArmResultsWatchdog(
            int port,
            Object api,
            int timeoutMs,
            string filter,
            TestRunProgress progress,
            Func<string> runGuidProvider)
        {
            int watchdogMs = Math.Max(TimeoutPaddingMs, timeoutMs - TimeoutPaddingMs);
            double deadline = EditorApplication.timeSinceStartup + watchdogMs / 1000.0;

            void Tick()
            {
                if (File.Exists(ResultsFilePath(port)))
                {
                    EditorApplication.update -= Tick;
                    return;
                }

                if (EditorApplication.timeSinceStartup < deadline)
                    return;

                EditorApplication.update -= Tick;

                if (api != null)
                    Object.DestroyImmediate(api);

                var runGuid = runGuidProvider?.Invoke();
                if (!string.IsNullOrEmpty(runGuid))
                    TestRunnerApi.CancelTestRun(runGuid);

                string filterSuffix = string.IsNullOrEmpty(filter) ? "" : $" for filter '{filter}'";
                TestRunnerState.ClearPending(port);
                WriteResponseFile(
                    port,
                    new ErrorResponse(BuildTimeoutMessage(watchdogMs, filterSuffix, progress)));
            }

            EditorApplication.update += Tick;
        }

        // --- Shared helpers (used by TestRunnerState after domain reload) ---

        internal static void CollectResult(ITestResultAdaptor result,
            List<string> passed, List<string> failed, List<string> skipped)
        {
            if (result.Test.IsSuite) return;
            var name = result.Test.FullName;
            switch (result.TestStatus)
            {
                case TestStatus.Passed:  passed.Add(name); break;
                case TestStatus.Failed:  failed.Add($"{name}: {result.Message}"); break;
                default:                 skipped.Add(name); break;
            }
        }

        internal static void WriteResultsFile(int port, List<string> passed, List<string> failed, List<string> skipped)
        {
            WriteResultsFile(port, passed, failed, skipped, null);
        }

        internal static void WriteResultsFile(
            int port,
            List<string> passed,
            List<string> failed,
            List<string> skipped,
            TestRunProgress progress)
        {
            var data = new
            {
                success = failed.Count == 0,
                message = failed.Count > 0
                    ? $"{failed.Count} test(s) failed."
                    : $"All {passed.Count} test(s) passed.",
                data = new
                {
                    total   = passed.Count + failed.Count + skipped.Count,
                    passed  = passed.Count,
                    failed  = failed.Count,
                    skipped = skipped.Count,
                    failures = failed,
                    passes   = passed,
                    durationSeconds = progress != null ? RoundSeconds(progress.ElapsedSeconds) : 0,
                    slowTests = progress?.SlowTests
                        .OrderByDescending(t => t.durationSeconds)
                        .Take(10)
                        .ToList() ?? new List<TestTiming>(),
                }
            };

            try
            {
                Directory.CreateDirectory(StatusDir);
                File.WriteAllText(ResultsFilePath(port), JsonConvert.SerializeObject(data));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UnityCliConnector] Failed to write test results: {ex.Message}");
            }
        }

        internal static string ResultsFilePath(int port) =>
            Path.Combine(StatusDir, $"test-results-{port}.json");

        internal static string ProgressFilePath(int port) =>
            Path.Combine(StatusDir, $"test-progress-{port}.json");

        internal static void WriteProgressFile(TestRunProgress progress)
        {
            if (progress == null) return;

            var data = new
            {
                port = progress.Port,
                runGuid = progress.RunGuid ?? "",
                filter = progress.Filter ?? "",
                total = progress.Total,
                completed = progress.Completed,
                currentTest = progress.CurrentTest ?? "",
                lastFinishedTest = progress.LastFinishedTest ?? "",
                elapsedSeconds = RoundSeconds(progress.ElapsedSeconds),
                currentTestElapsedSeconds = RoundSeconds(progress.CurrentTestElapsedSeconds),
                slowTests = progress.SlowTests
                    .OrderByDescending(t => t.durationSeconds)
                    .Take(10)
                    .ToList(),
            };

            try
            {
                Directory.CreateDirectory(StatusDir);
                File.WriteAllText(ProgressFilePath(progress.Port), JsonConvert.SerializeObject(data));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UnityCliConnector] Failed to write test progress: {ex.Message}");
            }
        }

        internal static void WriteResponseFile(int port, object response)
        {
            try
            {
                Directory.CreateDirectory(StatusDir);
                File.WriteAllText(ResultsFilePath(port), JsonConvert.SerializeObject(response));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UnityCliConnector] Failed to write test results: {ex.Message}");
            }
        }

        internal static string BuildTimeoutMessage(int watchdogMs, string filterSuffix, TestRunProgress progress)
        {
            var message = $"Test run timed out after {watchdogMs}ms{filterSuffix}.";
            if (progress == null)
                return message;

            var details = new List<string>();
            if (!string.IsNullOrEmpty(progress.CurrentTest))
                details.Add($"current test: {progress.CurrentTest} ({RoundSeconds(progress.CurrentTestElapsedSeconds):0.###}s)");
            if (!string.IsNullOrEmpty(progress.LastFinishedTest))
                details.Add($"last finished: {progress.LastFinishedTest}");
            if (progress.Total > 0 || progress.Completed > 0)
                details.Add($"completed: {progress.Completed}/{progress.Total}");

            return details.Count == 0
                ? message
                : $"{message} {string.Join("; ", details)}";
        }

        internal static object BuildResponse(List<string> passed, List<string> failed, List<string> skipped)
        {
            var summary = new
            {
                total   = passed.Count + failed.Count + skipped.Count,
                passed  = passed.Count,
                failed  = failed.Count,
                skipped = skipped.Count,
                failures = failed,
                passes   = passed,
            };
            return failed.Count > 0
                ? (object)new ErrorResponse($"{failed.Count} test(s) failed.", summary)
                : new SuccessResponse($"All {passed.Count} test(s) passed.", summary);
        }

        internal static Filter BuildFilter(TestMode mode, string filterStr)
        {
            var f = new Filter { testMode = mode };
            if (!string.IsNullOrEmpty(filterStr))
            {
                f.testNames  = new[] { filterStr };
                f.groupNames = new[] { filterStr };
            }
            return f;
        }

        private static int CountMatchingTests(ITestAdaptor root, string filter)
        {
            if (root == null)
                return 0;

            if (string.IsNullOrEmpty(filter))
                return root.TestCaseCount;

            Regex regex = null;
            try { regex = new Regex(filter); } catch { }

            return Flatten(root).Count(test => !test.IsSuite && MatchesFilter(test, filter, regex));
        }

        private static IEnumerable<ITestAdaptor> Flatten(ITestAdaptor root)
        {
            yield return root;

            if (!root.HasChildren)
                yield break;

            foreach (var child in root.Children)
            foreach (var test in Flatten(child))
                yield return test;
        }

        private static bool MatchesFilter(ITestAdaptor test, string filter, Regex regex)
        {
            var fullName = test.FullName ?? "";
            var name = test.Name ?? "";

            return fullName.Equals(filter, StringComparison.Ordinal)
                || name.Equals(filter, StringComparison.Ordinal)
                || (regex != null && (regex.IsMatch(fullName) || regex.IsMatch(name)));
        }

        private static double RoundSeconds(double seconds) =>
            Math.Round(seconds, 3, MidpointRounding.AwayFromZero);

        internal class TestRunProgress
        {
            public readonly int Port;
            public readonly string Filter;
            public string RunGuid;
            public int Total;
            public int Completed;
            public string CurrentTest;
            public string LastFinishedTest;
            public readonly List<TestTiming> SlowTests = new List<TestTiming>();

            private readonly double _runStartedAt;
            private double _currentTestStartedAt;

            public TestRunProgress(int port, string filter)
            {
                Port = port;
                Filter = filter;
                _runStartedAt = EditorApplication.timeSinceStartup;
            }

            public double ElapsedSeconds => Math.Max(0, EditorApplication.timeSinceStartup - _runStartedAt);

            public double CurrentTestElapsedSeconds => string.IsNullOrEmpty(CurrentTest)
                ? 0
                : Math.Max(0, EditorApplication.timeSinceStartup - _currentTestStartedAt);

            public void MarkRunStarted(ITestAdaptor tests)
            {
                if (tests != null)
                    Total = tests.TestCaseCount;
            }

            public void MarkTestStarted(ITestAdaptor test)
            {
                CurrentTest = test?.FullName ?? test?.Name ?? "";
                _currentTestStartedAt = EditorApplication.timeSinceStartup;
            }

            public void MarkTestFinished(ITestResultAdaptor result)
            {
                if (result?.Test?.IsSuite == true)
                    return;

                var name = result?.Test?.FullName ?? result?.Test?.Name ?? CurrentTest ?? "";
                LastFinishedTest = name;
                CurrentTest = "";
                Completed++;

                if (result == null || string.IsNullOrEmpty(name))
                    return;

                SlowTests.Add(new TestTiming
                {
                    name = name,
                    status = result.TestStatus.ToString(),
                    durationSeconds = RoundSeconds(result.Duration),
                });
            }
        }

        internal class TestTiming
        {
            public string name;
            public string status;
            public double durationSeconds;
        }

        internal class TestCallbacks : ICallbacks
        {
            private readonly Action<ITestAdaptor> _onRunStarted;
            private readonly Action<ITestAdaptor> _onTestStarted;
            private readonly Action<ITestResultAdaptor> _onResult;
            private readonly Action<ITestResultAdaptor> _onFinished;

            public TestCallbacks(
                Action<ITestAdaptor> onRunStarted,
                Action<ITestAdaptor> onTestStarted,
                Action<ITestResultAdaptor> onResult,
                Action<ITestResultAdaptor> onFinished)
            {
                _onRunStarted = onRunStarted;
                _onTestStarted = onTestStarted;
                _onResult   = onResult;
                _onFinished = onFinished;
            }

            public void RunStarted(ITestAdaptor testsToRun) => _onRunStarted(testsToRun);
            public void RunFinished(ITestResultAdaptor result) => _onFinished(result);
            public void TestStarted(ITestAdaptor test) => _onTestStarted(test);
            public void TestFinished(ITestResultAdaptor result) => _onResult(result);
        }
    }
}
