#if DEBUG
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, new DebugInProcessConfig());
#else
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, IsCiEnvironment() ? ContinuousIntegrationConfig.Instance : DefaultConfig.Instance);

static bool IsCiEnvironment() =>
	string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase);

file sealed class ContinuousIntegrationConfig : ManualConfig
{
	public static readonly IConfig Instance = CreateMinimumViable().AddLogger(ContinuousIntegrationLogger.Instance);
}

file sealed class ContinuousIntegrationLogger : ILogger
{
	private readonly ILogger _innerLogger = ConsoleLogger.Default;
	private bool _hasVisibleContentOnCurrentLine;

	public static readonly ContinuousIntegrationLogger Instance = new();

	public string Id => _innerLogger.Id;

	public int Priority => _innerLogger.Priority + 1;

	public void Flush()
	{
		_innerLogger.Flush();
	}

	public void Write(LogKind logKind, string text)
	{
		if (!ShouldWrite(logKind))
		{
			return;
		}

		_innerLogger.Write(logKind, text);
		_hasVisibleContentOnCurrentLine = true;
	}

	public void WriteLine()
	{
		if (!_hasVisibleContentOnCurrentLine)
		{
			return;
		}

		_innerLogger.WriteLine();
		_hasVisibleContentOnCurrentLine = false;
	}

	public void WriteLine(LogKind logKind, string text)
	{
		if (!ShouldWrite(logKind))
		{
			return;
		}

		_innerLogger.WriteLine(logKind, text);
		_hasVisibleContentOnCurrentLine = false;
	}

	private static bool ShouldWrite(LogKind logKind) =>
		logKind is LogKind.Error or LogKind.Warning or LogKind.Result;
}
#endif
