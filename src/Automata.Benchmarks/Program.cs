// Benchmarks BenchmarkDotNet pour l'API publique Automata (EPIC .NET 11, CoursIA#18770).
// Baseline net8.0 : dotnet run -c Release -f net8.0 -- --filter '*'
// Baseline net9.0 : dotnet run -c Release -f net9.0 -- --filter '*'
// Delta multi-runtime (net9/10/11, CoursIA#18771), job process enfant :
//   DOTNET_MULTI_RUNTIME=1 dotnet run -c Release -f net8.0 -- --runtimes net9.0 net10.0 net11.0
// Charges deterministes : memes regex et memes sujets a chaque iteration.
using BenchmarkDotNet.Running;

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
