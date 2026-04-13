namespace MDI.IO.Hashing;

public sealed partial class Crc16
{
    private const int Polynomial = 0x1021;
    private static readonly ushort[] Lookup = GenerateTable(Polynomial);

    private static ushort[] GenerateTable(ushort polynomial)
    {
        ushort[] table = new ushort[256];

        ushort accum;
        ushort data;

        for (int i = 0; i < 256; ++i)
        {
            accum = 0;
            data = (ushort)(i << 8);

            for (int j = 8; j > 0; j--)
            {
                if (((accum ^ data) & 0x8000) != 0)
                {
                    accum = (ushort)((accum << 1) ^ polynomial);
                }
                else
                {
                    accum <<= 1;
                }

                data <<= 1;
            }

            table[i] = accum;
        }

        return table;
    }
}
