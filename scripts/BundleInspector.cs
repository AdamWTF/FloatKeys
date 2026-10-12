using System;
using System.IO;

// Build-only helper. Marker format is defined by dotnet/runtime's bundle_marker.h.
// Uses only the PowerShell host's BCL, so CI does not need .NET 10 inside PowerShell.
public static class FloatKeysBundleInspector
{
    public static long HeaderOffset(string path)
    {
        byte[] marker = Convert.FromHexString("8b1202b96a612038727b930214d7a03213f5b9e6efae3318ee3b2dce24b36aae");
        byte[] buffer = new byte[65536 + 40];
        using (var stream = File.OpenRead(path))
        {
            int carry = 0;
            int read;
            while ((read = stream.Read(buffer, carry, buffer.Length - carry)) != 0)
            {
                int total = carry + read;
                for (int index = 8; index <= total - marker.Length; index++)
                {
                    if (buffer[index] != marker[0]) continue;
                    int matched = 1;
                    while (matched < marker.Length && buffer[index + matched] == marker[matched]) matched++;
                    if (matched != marker.Length) continue;
                    long offset = BitConverter.ToInt64(buffer, index - 8);
                    if (offset > 0 && offset < stream.Length) return offset;
                }
                carry = Math.Min(40, total);
                Buffer.BlockCopy(buffer, total - carry, buffer, 0, carry);
            }
        }
        throw new InvalidDataException("Executable has no valid .NET bundle marker.");
    }
}
