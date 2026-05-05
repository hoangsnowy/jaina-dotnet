using BenchmarkDotNet.Running;

// Run all benchmarks in this assembly. Filter from CLI:
//   dotnet run -c Release -- --filter "*CacheBench*"
//   dotnet run -c Release -- --list flat
BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
