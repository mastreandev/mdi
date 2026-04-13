namespace MDI.Tests.IO;

public static class Constants
{
    public const string KnownMessage = "Check this message!";
    public const ushort KnownMessageCrc = 0x9e8f;

    public static readonly byte[] KnownMessageBytes = System.Text.Encoding.ASCII.GetBytes(KnownMessage);
}
