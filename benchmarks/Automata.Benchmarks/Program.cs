// Benchmarks BenchmarkDotNet pour l'API publique d'Automata (EPIC .NET 11, CoursIA#18770).
// Baseline net8.0 : dotnet run -c Release -- --filter * --job short
// Charges a structure figee (aucun RNG) : memes operandes a chaque iteration, sans I/O ni reseau.
using BenchmarkDotNet.Running;

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
