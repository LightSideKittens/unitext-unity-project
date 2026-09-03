using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace LightSide.Hub
{
    /// <summary>
    /// Unpacks the gzipped tar an npm registry serves a package as. Only what a package tarball
    /// actually contains is handled — regular files, directories, and the pax headers npm emits for
    /// paths longer than the tar name field.
    /// </summary>
    internal static class TarGz
    {
        private const int Block = 512;
        private const int NameOffset = 0;
        private const int NameLength = 100;
        private const int SizeOffset = 124;
        private const int SizeLength = 12;
        private const int TypeOffset = 156;
        private const int PrefixOffset = 345;
        private const int PrefixLength = 155;

        /// <summary>
        /// Replaces <paramref name="destination"/> with the tarball's contents, dropping the leading
        /// <c>package/</c> component every npm tarball wraps its files in.
        /// </summary>
        /// <returns>How many files were written.</returns>
        /// <exception cref="InvalidDataException">The archive held no files, so the download was not a package.</exception>
        public static int ExtractPackage(byte[] tarball, string destination)
        {
            if (Directory.Exists(destination)) Directory.Delete(destination, true);
            Directory.CreateDirectory(destination);

            var tar = Inflate(tarball);
            var position = 0;
            string pendingName = null;
            var written = 0;

            while (position + Block <= tar.Length)
            {
                if (IsZeroBlock(tar, position)) break;

                var name = ReadString(tar, position + NameOffset, NameLength);
                var prefix = ReadString(tar, position + PrefixOffset, PrefixLength);
                var size = (int)ParseOctal(ReadString(tar, position + SizeOffset, SizeLength));
                var type = (char)tar[position + TypeOffset];
                position += Block;

                var fullName = string.IsNullOrEmpty(prefix) ? name : prefix + "/" + name;
                if (pendingName != null)
                {
                    fullName = pendingName;
                    pendingName = null;
                }

                if (type == 'x' || type == 'g')
                {
                    var path = ParsePaxPath(Encoding.UTF8.GetString(tar, position, size));
                    if (type == 'x' && path != null) pendingName = path;
                    position += RoundUp(size);
                    continue;
                }

                var relative = StripFirstComponent(fullName);
                if (relative.Length > 0)
                {
                    if (type == '5' || fullName.EndsWith("/"))
                    {
                        Directory.CreateDirectory(Path.Combine(destination, relative));
                    }
                    else if (type == '0' || type == '\0')
                    {
                        var target = Path.Combine(destination,
                            relative.Replace('/', Path.DirectorySeparatorChar));
                        Directory.CreateDirectory(Path.GetDirectoryName(target) ?? destination);
                        var bytes = new byte[size];
                        Array.Copy(tar, position, bytes, 0, size);
                        File.WriteAllBytes(target, bytes);
                        written++;
                    }
                }

                position += RoundUp(size);
            }

            if (written == 0)
                throw new InvalidDataException("The archive contained no package files.");
            return written;
        }

        private static byte[] Inflate(byte[] gzip)
        {
            using var input = new MemoryStream(gzip);
            using var stream = new GZipStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            stream.CopyTo(output);
            return output.ToArray();
        }

        private static bool IsZeroBlock(byte[] bytes, int offset)
        {
            for (var i = 0; i < Block; i++)
                if (bytes[offset + i] != 0) return false;
            return true;
        }

        private static string ReadString(byte[] bytes, int offset, int length)
        {
            var end = offset;
            var limit = Math.Min(offset + length, bytes.Length);
            while (end < limit && bytes[end] != 0) end++;
            return Encoding.UTF8.GetString(bytes, offset, end - offset);
        }

        private static long ParseOctal(string value)
        {
            value = value.Trim();
            long result = 0;
            foreach (var c in value)
            {
                if (c < '0' || c > '7') break;
                result = result * 8 + (c - '0');
            }
            return result;
        }

        private static int RoundUp(int size) => (size + Block - 1) / Block * Block;

        private static string StripFirstComponent(string path)
        {
            path = path.Replace('\\', '/').TrimStart('/');
            var slash = path.IndexOf('/');
            return slash < 0 ? "" : path.Substring(slash + 1);
        }

        private static string ParsePaxPath(string header)
        {
            foreach (var line in header.Split('\n'))
            {
                var space = line.IndexOf(' ');
                if (space < 0) continue;
                var pair = line.Substring(space + 1);
                var equals = pair.IndexOf('=');
                if (equals > 0 && pair.Substring(0, equals) == "path")
                    return pair.Substring(equals + 1).TrimEnd('\r', '\n');
            }
            return null;
        }
    }
}
