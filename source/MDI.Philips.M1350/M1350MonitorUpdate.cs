using MDI.Philips.M1350.Application.CTG;
using MDI.Philips.M1350.Application.EventMessage;
using MDI.Philips.M1350.Application.Failure;
using MDI.Philips.M1350.Application.Identity;
using MDI.Philips.M1350.Application.Nibp;
using MDI.Philips.M1350.Application.Notes;
using MDI.Philips.M1350.Application.SpO2;
using MDI.Philips.M1350.Application.Temperature;

namespace MDI.Philips.M1350;

/// <summary>
/// Represents a time-stamped update emitted by <see cref="M1350Monitor" />.
/// </summary>
public abstract record M1350MonitorUpdate(DateTimeOffset Timestamp);

/// <summary>
/// Represents an identity update.
/// </summary>
public sealed record IdentityUpdated(IdBlock Block, DateTimeOffset Timestamp) : M1350MonitorUpdate(Timestamp);

/// <summary>
/// Represents a negotiated protocol-revision update.
/// </summary>
public sealed record NegotiatedRevisionUpdated(string Revision, DateTimeOffset Timestamp) : M1350MonitorUpdate(Timestamp);

/// <summary>
/// Represents an auto-send activation-state update.
/// </summary>
public sealed record AutoSendStateUpdated(bool IsActive, DateTimeOffset Timestamp) : M1350MonitorUpdate(Timestamp);

/// <summary>
/// Represents a CTG update.
/// </summary>
public sealed record CtgUpdated(CtgBlock Block, DateTimeOffset Timestamp) : M1350MonitorUpdate(Timestamp);

/// <summary>
/// Represents a maternal blood-pressure update.
/// </summary>
public sealed record NibpUpdated(NibpBlock Block, DateTimeOffset Timestamp) : M1350MonitorUpdate(Timestamp);

/// <summary>
/// Represents a maternal oxygen-saturation update.
/// </summary>
public sealed record SpO2Updated(SpO2Block Block, DateTimeOffset Timestamp) : M1350MonitorUpdate(Timestamp);

/// <summary>
/// Represents a maternal temperature update.
/// </summary>
public sealed record TemperatureUpdated(TemperatureBlock Block, DateTimeOffset Timestamp) : M1350MonitorUpdate(Timestamp);

/// <summary>
/// Represents an event-marker update.
/// </summary>
public sealed record EventMarkerUpdated(EventMessageBlock Block, DateTimeOffset Timestamp) : M1350MonitorUpdate(Timestamp);

/// <summary>
/// Represents a note update.
/// </summary>
public sealed record NoteUpdated(NoteBlock Block, DateTimeOffset Timestamp) : M1350MonitorUpdate(Timestamp);

/// <summary>
/// Represents a failure update.
/// </summary>
public sealed record FailureUpdated(FailureBlock Block, DateTimeOffset Timestamp) : M1350MonitorUpdate(Timestamp);
