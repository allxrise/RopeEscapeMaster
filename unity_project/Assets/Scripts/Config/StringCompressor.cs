using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace RopeEscape.Config
{
    /// <summary>
    /// Handles Brotli decompression/compression compatible with the original StringCompressor class,
    /// while also transparently passing through plain UTF-8 JSON strings.
    /// </summary>
    public static class StringCompressor
    {
        public static string BrotliDecompress(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            string trimmed = input.Trim();
            if (trimmed.StartsWith("{") || trimmed.StartsWith("["))
                return trimmed;

            byte[] compressedBytes = Convert.FromBase64String(trimmed);
            using (MemoryStream msInput = new MemoryStream(compressedBytes))
            using (BrotliStream brotli = new BrotliStream(msInput, CompressionMode.Decompress))
            using (MemoryStream msOutput = new MemoryStream())
            {
                byte[] buffer = new byte[4096];
                int read;
                while ((read = brotli.Read(buffer, 0, buffer.Length)) > 0)
                {
                    msOutput.Write(buffer, 0, read);
                }
                return Encoding.UTF8.GetString(msOutput.ToArray());
            }
        }

        public static string BrotliCompress(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return string.Empty;

            byte[] rawBytes = Encoding.UTF8.GetBytes(plainText);
            using (MemoryStream msOutput = new MemoryStream())
            {
                using (BrotliStream brotli = new BrotliStream(msOutput, CompressionLevel.Optimal, true))
                {
                    brotli.Write(rawBytes, 0, rawBytes.Length);
                    brotli.Flush();
                }
                return Convert.ToBase64String(msOutput.ToArray());
            }
        }
    }
}
