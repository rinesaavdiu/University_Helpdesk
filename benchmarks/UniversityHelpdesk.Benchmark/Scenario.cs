using System.Diagnostics;

namespace UniversityHelpdesk.Benchmark;

public record ScenarioResult(
    string Name,
    int Concurrency,
    int Requests,
    int Failures,
    double MedianMs,
    double P95Ms,
    double MaxMs,
    double RequestsPerSecond);

public static class Scenario
{
    public static Task<ScenarioResult> RunAsync(
        string name, int workers, int requestsPerWorker, Func<int, int, Task<bool>> request) =>
        RunAsync(name, workers, _ => requestsPerWorker, request);

    /// <summary>
    /// Runs <paramref name="workers"/> loops at once; each sends its share of requests one after
    /// another. A request fails if it returns false or the HTTP call throws.
    /// </summary>
    public static async Task<ScenarioResult> RunAsync(
        string name, int workers, Func<int, int> requestsForWorker, Func<int, int, Task<bool>> request)
    {
        var perWorker = new double[workers][];
        var failures = 0;
        var started = Stopwatch.GetTimestamp();

        await Task.WhenAll(Enumerable.Range(0, workers).Select(worker => Task.Run(async () =>
        {
            var durations = perWorker[worker] = new double[requestsForWorker(worker)];
            for (var i = 0; i < durations.Length; i++)
            {
                var requestStarted = Stopwatch.GetTimestamp();
                bool succeeded;
                try
                {
                    succeeded = await request(worker, i);
                }
                catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
                {
                    succeeded = false;
                }

                durations[i] = Stopwatch.GetElapsedTime(requestStarted).TotalMilliseconds;
                if (!succeeded)
                {
                    Interlocked.Increment(ref failures);
                }
            }
        })));

        var elapsed = Stopwatch.GetElapsedTime(started);
        var all = perWorker.SelectMany(durations => durations).Order().ToArray();
        return new ScenarioResult(
            name,
            workers,
            all.Length,
            failures,
            Percentile(all, 0.50),
            Percentile(all, 0.95),
            all[^1],
            all.Length / elapsed.TotalSeconds);
    }

    /// <summary>Nearest-rank percentile of an already sorted array.</summary>
    public static double Percentile(double[] sorted, double fraction) =>
        sorted[Math.Max(0, (int)Math.Ceiling(fraction * sorted.Length) - 1)];
}
