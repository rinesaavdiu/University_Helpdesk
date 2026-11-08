using UniversityHelpdesk.Benchmark;

var options = BenchmarkOptions.Parse(args);
if (options is null)
{
    Console.Error.WriteLine(BenchmarkOptions.Usage);
    return 2;
}

return await BenchmarkRunner.RunAsync(options);
