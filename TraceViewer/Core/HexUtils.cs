namespace TraceViewer.Core;

/// <summary>
/// Shared utility for hex string conversions, avoiding duplicate implementations
/// across TraceParser and WPF_TraceRow.
/// </summary>
internal static class HexUtils
{
    private const string ZeroHexValue = "0";
    private const string ZeroHexValuePadded = "00";

    /// <summary>
    /// Converts a byte array to a big-endian hex string, optionally stripping leading zeros.
    /// </summary>
    public static string ByteArrayToHexString(byte[] bytes, bool stripLeadingZeros = true)
    {
        if (bytes is null || bytes.Length == 0)
            return stripLeadingZeros ? ZeroHexValue : ZeroHexValuePadded;

        // Pre-allocate exact capacity
        Span<char> buffer = stackalloc char[bytes.Length * 2];
        int pos = 0;
        bool leadingZero = stripLeadingZeros;

        for (int i = bytes.Length - 1; i >= 0; i--)
        {
            byte b = bytes[i];
            if (leadingZero && b == 0 && i > 0)
                continue;

            leadingZero = false;
            buffer[pos++] = GetHexChar(b >> 4);
            buffer[pos++] = GetHexChar(b & 0x0F);
        }

        return pos == 0
            ? (stripLeadingZeros ? ZeroHexValue : ZeroHexValuePadded)
            : new string(buffer[..pos]);
    }

    /// <summary>
    /// Converts bytes to hex without the dash separators that BitConverter.ToString produces.
    /// Replacement for BitConverter.ToString(bytes).Replace("-", "").
    /// </summary>
    public static string BytesToHexNoDash(byte[] bytes)
    {
        if (bytes is null || bytes.Length == 0)
            return string.Empty;

        return Convert.ToHexString(bytes);
    }

    private static char GetHexChar(int nibble) => (char)(nibble < 10 ? '0' + nibble : 'A' + nibble - 10);
}
