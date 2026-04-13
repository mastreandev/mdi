using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Security", "CA5394: Do not use insecure randomness", Justification = "The use of System.Random.Shared in benchmarks is not cryptographic.", Scope = "module")]
[assembly: SuppressMessage("Design", "CA1001: Types that own disposable fields should be disposable", Justification = "BenchmarkDotNet doesn't support IDisposable implementations.", Scope = "module")]
