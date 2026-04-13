using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Security", "CA5394: Do not use insecure randomness", Justification = "The use of System.Random.Shared in benchmarks is not cryptographic.", Scope = "module")]
