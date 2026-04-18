namespace MDI.Philips.M1350.Simulator;

internal readonly record struct SimulatorHostParseResult(
    SimulatorHostOptions? Options,
    bool ShowUsage,
    string? ErrorMessage)
{
    public static SimulatorHostParseResult Error(string message)
    {
        return new(null, true, message);
    }

    public static SimulatorHostParseResult Help()
    {
        return new(null, true, null);
    }

    public static SimulatorHostParseResult Success(SimulatorHostOptions options)
    {
        return new(options, false, null);
    }
}
