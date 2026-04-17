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
/// Represents the latest known monitor state retained by <see cref="M1350Monitor" />.
/// </summary>
public sealed record M1350MonitorSnapshot(
    IdBlock? Identity = null,
    CtgBlock? Ctg = null,
    NibpBlock? Nibp = null,
    SpO2Block? SpO2 = null,
    TemperatureBlock? Temperature = null,
    EventMessageBlock? EventMarker = null,
    NoteBlock? Note = null,
    FailureBlock? Failure = null,
    string? NegotiatedRevision = null,
    bool IsAutoSendActive = false,
    DateTimeOffset? MessageReceivedAt = null,
    DateTimeOffset? CtgReceivedAt = null,
    DateTimeOffset? NibpReceivedAt = null,
    DateTimeOffset? SpO2ReceivedAt = null,
    DateTimeOffset? TemperatureReceivedAt = null,
    DateTimeOffset? FailureReceivedAt = null);
