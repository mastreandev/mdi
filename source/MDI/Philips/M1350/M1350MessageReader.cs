using System.Buffers;

using MDI.Philips.M1350.Application.CTG;
using MDI.Philips.M1350.Application.EventMessage;
using MDI.Philips.M1350.Application.Failure;
using MDI.Philips.M1350.Application.Identity;
using MDI.Philips.M1350.Application.Nibp;
using MDI.Philips.M1350.Application.Notes;
using MDI.Philips.M1350.Application.SpO2;
using MDI.Philips.M1350.Application.Temperature;
using MDI.Philips.M1350.DataLink;

namespace MDI.Philips.M1350;

/// <summary>
/// Reads framed Philips M1350 data and routes supported blocks to typed messages.
/// </summary>
public static class M1350MessageReader
{
    /// <summary>
    /// Attempts to read the next supported message from <paramref name="buffer" />.
    /// Unknown or currently unsupported blocks are consumed and ignored.
    /// </summary>
    /// <param name="buffer">The framed transport buffer to read from.</param>
    /// <param name="message">
    /// When this method returns <see langword="true" />, contains the next supported
    /// parsed message; otherwise the default value.
    /// </param>
    /// <returns>
    /// <see langword="true" /> if a complete framed block was available and parsed as a
    /// supported message; otherwise <see langword="false" />.
    /// </returns>
    public static bool TryRead(ref ReadOnlySequence<byte> buffer, out M1350Message message)
    {
        while (DataBlockReader.TryRead(ref buffer, out ReadOnlySequence<byte> block))
        {
            SequenceReader<byte> reader = new(block);

            if (!reader.TryRead(out byte typeByte))
            {
                continue;
            }

            switch (typeByte)
            {
                case CtgBlockParser.TypeByte when CtgBlockParser.TryParse(block, out CtgBlock ctgBlock):
                    message = new CtgMessage(ctgBlock);
                    return true;

                case IdBlockParser.TypeByte when IdBlockParser.TryParse(block, out IdBlock idBlock):
                    message = new IdMessage(idBlock);
                    return true;

                case EventMessageBlockParser.TypeByte when EventMessageBlockParser.TryParse(block, out EventMessageBlock eventMessageBlock):
                    message = new EventMarkerMessage(eventMessageBlock);
                    return true;

                case NoteBlockParser.TypeByte when NoteBlockParser.TryParse(block, out NoteBlock noteBlock):
                    message = new NoteMessage(noteBlock);
                    return true;

                case FailureBlockParser.TypeByte when FailureBlockParser.TryParse(block, out FailureBlock failureBlock):
                    message = new FailureMessage(failureBlock);
                    return true;

                case NibpBlockParser.TypeByte when NibpBlockParser.TryParse(block, out NibpBlock nibpBlock):
                    message = new NibpMessage(nibpBlock);
                    return true;

                case SpO2BlockParser.TypeByte when SpO2BlockParser.TryParse(block, out SpO2Block spo2Block):
                    message = new SpO2Message(spo2Block);
                    return true;

                case TemperatureBlockParser.TypeByte when TemperatureBlockParser.TryParse(block, out TemperatureBlock temperatureBlock):
                    message = new TemperatureMessage(temperatureBlock);
                    return true;

                default:
                    continue;
            }
        }

        message = default!;
        return false;
    }
}
