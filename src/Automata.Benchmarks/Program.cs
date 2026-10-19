// Benchmarks BenchmarkDotNet pour l'API publique Automata (EPIC .NET 11, CoursIA#18770).
// Baseline net9.0 : dotnet run -c Release -- --filter *
// Charges deterministes : memes regex et memes sujets a chaque iteration.
using BenchmarkDotNet.Running;

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
