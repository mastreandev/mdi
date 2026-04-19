namespace MDI.Philips.M1350.Application.CTG;

/// <summary>
/// Represents the two-byte big-endian status word from a C-block (Table 3-4).
/// </summary>
/// <param name="RawValue">
/// The raw unsigned 16-bit status word. The high byte occupies bits 15–8 and the
/// low byte occupies bits 7–0, matching the big-endian on-wire order.
/// </param>
public readonly record struct CtgStatusWord(ushort RawValue)
{
    private byte HighByte => (byte)(this.RawValue >> 8);

    private byte LowByte => (byte)(this.RawValue & 0xFF);

    /// <summary>Gets a value indicating whether FMP is enabled.</summary>
    public bool IsFmpEnabled => (this.HighByte & 0x80) != 0;

    /// <summary>
    /// Gets a value indicating whether the HR1 twin offset is active
    /// (adds 20 bpm to the displayed value).
    /// </summary>
    public bool IsHr1TwinOffsetActive => (this.HighByte & 0x40) != 0;

    /// <summary>Gets a value indicating whether DECG logic is on.</summary>
    public bool IsDecgLogicOn => (this.HighByte & 0x04) != 0;

    /// <summary>
    /// Gets a value indicating whether HR cross-channel verification has been detected.
    /// </summary>
    public bool IsHrCrossChannelVerified => (this.LowByte & 0x80) != 0;

    /// <summary>Gets a value indicating whether telemetry is on.</summary>
    public bool IsTelemetryOn => (this.LowByte & 0x40) != 0;

    /// <summary>
    /// Gets a value indicating whether FSpO2 data is available in <see cref="CtgBlock.FSpO2" />.
    /// Requires protocol revision A.02.00 or higher.
    /// </summary>
    public bool IsFSpO2Available => (this.LowByte & 0x10) != 0;

    /// <summary>
    /// Gets a value indicating whether one or more 250 ms CTG data ticks have been deleted.
    /// </summary>
    public bool IsCtgDataDeleted => (this.LowByte & 0x04) != 0;

    /// <summary>
    /// Gets a value indicating whether one or more default 250 ms CTG data ticks have been
    /// inserted.
    /// </summary>
    public bool IsCtgDataInserted => (this.LowByte & 0x02) != 0;

    /// <summary>Gets a value indicating whether the monitor is on.</summary>
    public bool IsMonitorOn => (this.LowByte & 0x01) != 0;
}
