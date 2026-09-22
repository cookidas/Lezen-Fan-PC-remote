namespace LezenTray;

public enum FanCommand : byte
{
    PowerOn = 0xA0, PowerOff = 0xA1,
    SpeedUp = 0xA2, SpeedDown = 0xA3,
    TimerUp = 0xA4, TimerDown = 0xA5,
    SwingOn = 0xA7, SwingOff = 0xA9,
    Sleep = 0xB0, Natural = 0xB1, Normal = 0xB2, Temperature = 0xB3,
    Bind = 0xB4
}

public static class FanProtocol
{
    public const ushort CompanyId = 0xFFF0;
    public static readonly TimeSpan Duration = TimeSpan.FromMilliseconds(200);

    public static bool IsValidId(string? id) => id is { Length: 4 } && id.All(char.IsAsciiHexDigit);

    // This Windows publisher omits Android's three-byte Flags AD structure.
    // Restore the RF sequence's position and the three bytes immediately before
    // it. Verified on the user's LZEF-DC02; changing whitening alone did not work.
    public static byte[] EncodeForWindows(FanCommand command, string id) =>
        [0xFF, 0xF0, 0xFF, .. Encode(command, id)];

    // Port of the original app's AA66 command frame and RF297L radio encoding.
    // ID characters are individual nibbles in four bytes, not a two-byte integer.
    public static byte[] Encode(FanCommand command, string id, int whiteningOffset = 15)
    {
        if (!IsValidId(id)) throw new ArgumentException(Strings.T("err.bad_id_arg"), nameof(id));
        if (!Enum.IsDefined(command)) throw new ArgumentOutOfRangeException(nameof(command));
        if (whiteningOffset is not (12 or 15)) throw new ArgumentOutOfRangeException(nameof(whiteningOffset));
        byte[] data = new byte[8];
        data[0] = 0xAA;
        data[1] = 0x66;
        data[2] = (byte)command;
        for (int i = 0; i < 4; i++) data[3 + i] = Convert.ToByte(id[i].ToString(), 16);
        data[7] = unchecked((byte)data.Sum(b => (int)b));

        byte[] frame = new byte[33];
        frame[15] = 0x71;
        frame[16] = 0x0F;
        frame[17] = 0x55;
        frame.AsSpan(18, 5).Fill(0xCC);
        data.CopyTo(frame, 23);
        for (int i = 15; i < 23; i++) frame[i] = ReverseBits(frame[i]);

        ushort crc = 0xFFFF;
        for (int i = 0; i < 5; i++) crc = CrcByte(crc, 0xCC);
        foreach (byte b in data) crc = CrcByte(crc, ReverseBits(b));
        ushort reversed = (ushort)((ReverseBits((byte)crc) << 8) | ReverseBits((byte)(crc >> 8)));
        crc = (ushort)(reversed ^ 0xFFFF);
        frame[31] = (byte)crc;
        frame[32] = (byte)(crc >> 8);

        Whiten(frame.AsSpan(18), 63);
        // BLE whitening starts at the PDU header. Android inserts a three-byte
        // Flags AD structure; a broadcaster without Flags has a shorter prefix.
        byte[] advertisement = new byte[whiteningOffset + 18];
        frame.AsSpan(15).CopyTo(advertisement.AsSpan(whiteningOffset));
        Whiten(advertisement, 37);
        return advertisement[whiteningOffset..];
    }

    private static ushort CrcByte(ushort crc, byte value)
    {
        crc ^= (ushort)(value << 8);
        for (int bit = 0; bit < 8; bit++)
            crc = unchecked((ushort)((crc << 1) ^ ((crc & 0x8000) != 0 ? 0x1021 : 0)));
        return crc;
    }

    private static byte ReverseBits(byte value)
    {
        int result = 0;
        for (int bit = 0; bit < 8; bit++) result = (result << 1) | ((value >> bit) & 1);
        return (byte)result;
    }

    private static void Whiten(Span<byte> bytes, int seed)
    {
        int[] r = [1, (seed >> 5) & 1, (seed >> 4) & 1, (seed >> 3) & 1, (seed >> 2) & 1, (seed >> 1) & 1, seed & 1];
        for (int i = 0; i < bytes.Length; i++)
        {
            int result = 0;
            for (int bit = 0; bit < 8; bit++)
            {
                int feedback = r[6];
                result |= (((bytes[i] >> bit) & 1) ^ feedback) << bit;
                int tap = r[3] ^ feedback;
                r[6] = r[5]; r[5] = r[4]; r[4] = tap; r[3] = r[2];
                r[2] = r[1]; r[1] = r[0]; r[0] = feedback;
            }
            bytes[i] = (byte)result;
        }
    }
}
