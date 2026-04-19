namespace MDI.Philips.M1350;

/// <summary>
/// Controls whether the monitor should enable CTG auto-send mode during connection startup.
/// </summary>
public enum AutoSendBehavior
{
    /// <summary>
    /// Leaves automatic CTG transmission disabled.
    /// </summary>
    Disabled,

    /// <summary>
    /// Enables automatic CTG transmission.
    /// </summary>
    Enabled,
}
