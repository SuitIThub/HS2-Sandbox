using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace HS2SandboxPlugin
{
    /// <summary>
    /// ZIP read/write using only <see cref="System.IO"/> (stored / method 0). Avoids
    /// <c>System.IO.Compression</c>, which Unity Mono often cannot load at runtime.
    /// </summary>
    internal static class MinimalStoredZip
    {
        private const uint LocalFileHeaderSignature = 0x04034b50;
        private const uint CentralDirectoryHeaderSignature = 0x02014b50;
        private const uint EndOfCentralDirectorySignature = 0x06054b50;

        /// <summary>UTF-8 names (ZIP EFS / GP flag bit 11).</summary>
        private const ushort GeneralPurposeUtf8Flag = 1 << 11;

        private readonly struct CentralRecord
        {
            public readonly string Name;
            public readonly uint LocalHeaderOffset;
            public readonly uint Crc32Expected;
            public readonly uint UncompressedSize;

            public CentralRecord(string name, uint localHeaderOffset, uint crc32Expected, uint uncompressedSize)
            {
                Name = name;
                LocalHeaderOffset = localHeaderOffset;
                Crc32Expected = crc32Expected;
                UncompressedSize = uncompressedSize;
            }
        }

        public static void Write(string path, IList<ZipEntryPart> entries)
        {
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
                Write(fs, entries);
        }

        public static void Write(Stream stream, IList<ZipEntryPart> entries)
        {
            var bw = new BinaryWriter(stream, Encoding.UTF8);
            try
            {
            var centralPieces = new List<Action<BinaryWriter>>();

            foreach (ZipEntryPart entry in entries)
            {
                string entryName = entry.Name;
                byte[] data = entry.Data;
                if (string.IsNullOrEmpty(entryName))
                    throw new ArgumentException("Empty entry name.", nameof(entries));
                NormalizeEntryName(entryName, out _, out byte[] nameBytes);
                if (nameBytes.Length > ushort.MaxValue || (ulong)data.LongLength > uint.MaxValue)
                    throw new PackFormatException("Entry too large for ZIP (use smaller pack).");

                uint crc = Crc32(data, 0, data.Length);
                uint uncompressed = (uint)data.Length;
                long localHeaderStart = stream.Position;

                bw.Write(LocalFileHeaderSignature);
                bw.Write((ushort)20);
                bw.Write(GeneralPurposeUtf8Flag);
                bw.Write((ushort)0);
                bw.Write((ushort)0);
                bw.Write((ushort)0);
                bw.Write(crc);
                bw.Write(uncompressed);
                bw.Write(uncompressed);
                bw.Write((ushort)nameBytes.Length);
                bw.Write((ushort)0);
                bw.Write(nameBytes);
                bw.Write(data, 0, data.Length);

                uint localHeaderOffset = (uint)localHeaderStart;
                centralPieces.Add(cw =>
                {
                    cw.Write(CentralDirectoryHeaderSignature);
                    cw.Write((ushort)0x0314);
                    cw.Write((ushort)20);
                    cw.Write(GeneralPurposeUtf8Flag);
                    cw.Write((ushort)0);
                    cw.Write((ushort)0);
                    cw.Write((ushort)0);
                    cw.Write(crc);
                    cw.Write(uncompressed);
                    cw.Write(uncompressed);
                    cw.Write((ushort)nameBytes.Length);
                    cw.Write((ushort)0);
                    cw.Write((ushort)0);
                    cw.Write((ushort)0);
                    cw.Write((ushort)0);
                    cw.Write(0);
                    cw.Write(localHeaderOffset);
                    cw.Write(nameBytes);
                });
            }

            long centralStart = stream.Position;
            foreach (var writeCdh in centralPieces)
                writeCdh(bw);

            long centralSize = stream.Position - centralStart;
            if (centralSize > uint.MaxValue || centralPieces.Count > ushort.MaxValue)
                throw new PackFormatException("Central directory too large.");

            bw.Write(EndOfCentralDirectorySignature);
            bw.Write((ushort)0);
            bw.Write((ushort)0);
            bw.Write((ushort)centralPieces.Count);
            bw.Write((ushort)centralPieces.Count);
            bw.Write((uint)centralSize);
            bw.Write((uint)centralStart);
            bw.Write((ushort)0);
            }
            finally
            {
                bw.Flush();
            }
        }

        /// <summary>Reads all stored (method 0) entries. Deflate is not supported.</summary>
        public static Dictionary<string, byte[]> ReadAll(string path)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                return ReadAll(fs);
        }

        public static Dictionary<string, byte[]> ReadAll(Stream stream)
        {
            long eocdOffset = FindEndOfCentralDirectoryOffset(stream);
            stream.Seek(eocdOffset, SeekOrigin.Begin);
            ushort totalCentralEntries;
            uint centralDirOffset;
            var br = new BinaryReader(stream, Encoding.UTF8);
            uint sig = br.ReadUInt32();
            if (sig != EndOfCentralDirectorySignature)
                throw new PackFormatException("Invalid ZIP (EOCD).");

            br.ReadUInt16();
            br.ReadUInt16();
            ushort cdTotalOnDisk = br.ReadUInt16();
            totalCentralEntries = br.ReadUInt16();
            br.ReadUInt32();
            centralDirOffset = br.ReadUInt32();
            ushort zipCommentLength = br.ReadUInt16();
            if (zipCommentLength > 0)
                br.ReadBytes(zipCommentLength);

            if (cdTotalOnDisk != totalCentralEntries)
                throw new PackFormatException("Multi-disk ZIP not supported.");

            var central = new List<CentralRecord>(totalCentralEntries);
            stream.Seek(centralDirOffset, SeekOrigin.Begin);
            for (int i = 0; i < totalCentralEntries; i++)
            {
                sig = br.ReadUInt32();
                if (sig != CentralDirectoryHeaderSignature)
                    throw new PackFormatException("Invalid ZIP (central directory).");

                br.ReadUInt16();
                br.ReadUInt16();
                ushort gpCd = br.ReadUInt16();
                ushort methodCd = br.ReadUInt16();
                br.ReadUInt16();
                br.ReadUInt16();
                uint crcCd = br.ReadUInt32();
                uint compSizeCd = br.ReadUInt32();
                uint uncompSizeCd = br.ReadUInt32();
                ushort nameLenCd = br.ReadUInt16();
                ushort extraLenCd = br.ReadUInt16();
                ushort commentLenCd = br.ReadUInt16();
                br.ReadUInt16();
                br.ReadUInt16();
                br.ReadUInt32();
                uint localHeaderOffset = br.ReadUInt32();
                byte[] nameBytesCd = br.ReadBytes(nameLenCd);
                if (extraLenCd > 0)
                    br.ReadBytes(extraLenCd);
                if (commentLenCd > 0)
                    br.ReadBytes(commentLenCd);

                string entryName = NormalizeForLookup(DecodeName(nameBytesCd, gpCd));

                if (methodCd != 0)
                    throw new PackFormatException(
                        "This ZIP uses compressed entries; only uncompressed (stored) packs from Pose Browser are supported.");

                if (compSizeCd != uncompSizeCd)
                    throw new PackFormatException("ZIP size mismatch.");

                central.Add(new CentralRecord(entryName, localHeaderOffset, crcCd, uncompSizeCd));
            }

            var dict = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var rec in central)
            {
                stream.Seek(rec.LocalHeaderOffset, SeekOrigin.Begin);
                sig = br.ReadUInt32();
                if (sig != LocalFileHeaderSignature)
                    throw new PackFormatException("Invalid ZIP (local header).");

                br.ReadUInt16();
                ushort gpLocal = br.ReadUInt16();
                ushort methodLocal = br.ReadUInt16();
                br.ReadUInt16();
                br.ReadUInt16();
                uint crcLocal = br.ReadUInt32();
                uint compLocal = br.ReadUInt32();
                uint uncompLocal = br.ReadUInt32();
                ushort nameLenLocal = br.ReadUInt16();
                ushort extraLenLocal = br.ReadUInt16();
                byte[] nameBytesLocal = br.ReadBytes(nameLenLocal);
                if (extraLenLocal > 0)
                    br.ReadBytes(extraLenLocal);

                string entryNameLocal = NormalizeForLookup(DecodeName(nameBytesLocal, gpLocal));
                if (!string.Equals(rec.Name, entryNameLocal, StringComparison.Ordinal))
                    throw new PackFormatException("ZIP central/local name mismatch.");

                if (methodLocal != 0 || compLocal != uncompLocal)
                    throw new PackFormatException("ZIP entry is not stored.");

                if (uncompLocal != rec.UncompressedSize || crcLocal != rec.Crc32Expected)
                    throw new PackFormatException("ZIP local/central metadata mismatch.");

                byte[] payload = br.ReadBytes(checked((int)uncompLocal));
                if (payload.LongLength != uncompLocal)
                    throw new EndOfStreamException("Truncated ZIP entry.");

                if (Crc32(payload, 0, payload.Length) != rec.Crc32Expected)
                    throw new PackFormatException("ZIP CRC mismatch.");

                dict[rec.Name] = payload;
            }

            return dict;
        }

        private static string DecodeName(byte[] raw, ushort gpFlags)
        {
            bool utf8 = (gpFlags & GeneralPurposeUtf8Flag) != 0;
            return utf8 ? Encoding.UTF8.GetString(raw) : Encoding.UTF8.GetString(raw);
        }

        private static string NormalizeForLookup(string name) =>
            name.Replace('\\', '/').TrimStart('/');

        private static void NormalizeEntryName(string entryName, out string normalizedUtf8, out byte[] nameBytes)
        {
            normalizedUtf8 = entryName.Replace('\\', '/').TrimStart('/');
            nameBytes = Encoding.UTF8.GetBytes(normalizedUtf8);
        }

        private static long FindEndOfCentralDirectoryOffset(Stream stream)
        {
            long len = stream.Length;
            if (len < 22)
                throw new PackFormatException("Not a ZIP file (too small).");

            int scan = (int)Math.Min(len, 65536 + 22);
            var buf = new byte[scan];
            stream.Seek(len - scan, SeekOrigin.Begin);
            int read = stream.Read(buf, 0, scan);
            if (read != scan)
                throw new EndOfStreamException();

            for (int i = scan - 22; i >= 0; i--)
            {
                if (buf[i] == 0x50 && i + 3 < scan &&
                    buf[i + 1] == 0x4b && buf[i + 2] == 0x05 && buf[i + 3] == 0x06)
                    return len - scan + i;
            }

            throw new PackFormatException("Not a ZIP file (missing EOCD).");
        }

        private static uint Crc32(byte[] data, int offset, int count)
        {
            uint crc = 0xFFFFFFFFu;
            for (int i = 0; i < count; i++)
            {
                crc ^= data[offset + i];
                for (int j = 0; j < 8; j++)
                {
                    if ((crc & 1) != 0)
                        crc = (crc >> 1) ^ 0xEDB88320u;
                    else
                        crc >>= 1;
                }
            }

            return ~crc;
        }
    }
}
