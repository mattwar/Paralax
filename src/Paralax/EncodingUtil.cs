using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Paralax
{
    internal static class EncodingUtil
    {
        /// <summary>
        /// Gets UTF16 code from byte buffer (little endian order)
        /// </summary>
        public static char ReadUTF16Char(byte[] buffer, int index)
        {
            return (char)(buffer[index] + (buffer[index + 1] << 8));
        }

        /// <summary>
        /// Gets UTF16 code from byte buffer (big endian order)
        /// </summary>
        public static char ReadUTF16Char_BE(byte[] buffer, int index)
        {
            return (char)((buffer[index] << 8) + buffer[index + 1]);
        }

        /// <summary>
        /// Gets the next valid starting position for a UTF16 code (Little Endian encoding; default)
        /// </summary>
        public static int GetNextUTF16CodeStart(byte[] buffer, int offset, int length)
        {
            // assume start of buffer is aligned; so align any partial offset
            offset = (offset & 1) == 0 ? offset : offset + 1;

            // skip low surrogate (as we are in second half of pair)
            if (Char.IsLowSurrogate(ReadUTF16Char(buffer, offset)))
            {
                offset += 2;
            }

            return offset;
        }

        /// <summary>
        /// Gets the next valid starting position for a UTF16 code (Big Endian encoding)
        /// </summary>
        public static int GetNextUTF16CodeStart_BE(byte[] buffer, int offset, int length)
        {
            // assume start of buffer is aligned; so align any partial offset
            offset = (offset & 1) == 0 ? offset : offset + 1;

            // skip low surrogate (as we are in second half of pair)
            var c = ReadUTF16Char_BE(buffer, offset);
            if (char.IsLowSurrogate(c))
            {
                offset += 2;
            }

            return offset;
        }

        /// <summary>
        /// Returns true if the byte is a valid start byte of an encoded UTF8 code.
        /// </summary>
        public static bool IsUTF8StartByte(byte b)
        {
            // any byte with two highest bits as "10" is an extension byte to multi-byte encoding
            // characters cannot start on extension bytes
            return (b & 0b1100_0000) != 0b1000_0000;
        }

        /// <summary>
        /// Reads the UTF8 code unit from the byte buffer.
        /// </summary>
        public static int ReadUTF8Code(byte[] buffer, int index, out int length)
        {
            var b = buffer[index];
            if ((b & 0b1000_0000) == 0b0000_0000)
            {
                length = 1;
                return (ushort)(b & 0b0111_1111);
            }
            else if ((b & 0b1110_0000) == 0b1100_0000)
            {
                length = 2;
                return (ushort)((b & 0b0001_1111)
                    + (buffer[index + 1] & 0b0011_1111) << 5);
            }
            else if ((b & 0b1111_0000) == 0b1110_0000)
            {
                length = 3;
                return (ushort)((b & 0b0000_1111)
                    + (buffer[index + 1] & 0b0011_1111) << 4
                    + (buffer[index + 2] & 0b0011_1111) << 10);
            }
            else if ((b & 0b1111_1000) == 0b1111_0000)
            {
                length = 4;
                return (ushort)((b & 0b0000_0111)
                    + (buffer[index + 1] & 0b0011_1111) << 3
                    + (buffer[index + 2] & 0b0011_1111) << 9
                    + (buffer[index + 3] & 0b0011_1111) << 15);
            }
            else
            {
                // this is not a valid UTF8 encoding byte
                throw new InvalidOperationException();
            }
        }

        /// <summary>
        /// Gets the next valid starting position for a UTF8 code
        /// </summary>
        public static int GetNextUTF8CodeStart(byte[] buffer, int offset, int length)
        {
            // skip forward until we find the start of the next UTF8 character
            while (!IsUTF8StartByte(buffer[offset]))
            {
                offset++;
            }

            return offset;
        }

        /// <summary>
        /// Determines the offset of the first valid character in the buffer.
        /// </summary>
        public static int GetNextCodeStart(byte[] buffer, int offset, int length, Encoding encoding)
        {
            if (encoding == Encoding.UTF8)
            {
                return EncodingUtil.GetNextUTF8CodeStart(buffer, offset, length);
            }
            else if (encoding == Encoding.Unicode)
            {
                return EncodingUtil.GetNextUTF16CodeStart(buffer, offset, length);
            }
            else if (encoding == Encoding.BigEndianUnicode)
            {
                return EncodingUtil.GetNextUTF16CodeStart_BE(buffer, offset, length);
            }
            else
            {
                throw new NotSupportedException();
            }
        }

        /// <summary>
        /// Returns the number of bytes to add to the position to align it on the 
        /// encoding specific boundary.
        public static int GetAlignment(long position, Encoding encoding)
        {
            if (encoding == Encoding.Unicode ||
                encoding == Encoding.BigEndianUnicode)
            {
                // need to be on two-byte boundary
                return (position & 1) == 0 ? 0 : 1;
            }
            else if (encoding == Encoding.UTF8)
            {
                return 0;
            }
            else
            {
                throw new NotSupportedException();
            }
        }
    }
}