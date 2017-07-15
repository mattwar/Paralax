using System.IO;
using System.Text;

namespace Paralax
{
    /// <summary>
    /// Reads a sequence of decoded characters from a byte stream.
    /// </summary>
    internal class CharReader
    {
        private readonly Stream stream;
        private readonly Encoding encoding;
        private readonly Decoder decoder;
        private readonly byte[] byteBuffer;
        private readonly char[] charBuffer;
        private long startPosition;
        private int bytesRead;
        private int bytesConverted;
        private int charsProduced;
        private int charsConsumed;
        private bool adjustFirstBlock;

        public CharReader(Stream stream, Encoding encoding)
        {
            this.stream = stream;
            this.encoding = encoding;
            this.decoder = encoding.GetDecoder();
            this.byteBuffer = new byte[2048];
            this.charBuffer = new char[1024];
            this.startPosition = stream.Position;
            this.adjustFirstBlock = true;
        }

        /// <summary>
        /// Read the next character in the stream.
        /// </summary>
        /// <remarks>
        /// This is a separate function so hopefully it can be inlined.
        /// </remarks>
        public bool TryReadNextChar(out char ch)
        {
            if (this.charsConsumed < this.charsProduced)
            {
                ch = this.charBuffer[this.charsConsumed];
                this.charsConsumed++;
                return true;
            }
            else
            {
                return TryReadNextCharInternal(out ch);
            }
        }

        /// <summary>
        /// Read the next character in the stream (more expensive)
        /// </summary>
        private bool TryReadNextCharInternal(out char ch)
        {
            while (true)
            {
                if (this.charsConsumed < this.charsProduced)
                {
                    ch = this.charBuffer[this.charsConsumed];
                    this.charsConsumed++;
                    return true;
                }

                // adjust stream position to where it should be for reading..
                // this may matter 
                if (this.stream.Position != this.startPosition + this.bytesConverted)
                {
                    this.stream.Position = this.startPosition + this.bytesConverted;
                }

                this.startPosition = this.stream.Position;
                this.bytesRead = stream.Read(this.byteBuffer, 0, this.byteBuffer.Length);
                this.charsConsumed = 0;
                this.charsProduced = 0;

                if (this.bytesRead == 0)
                {
                    ch = '\0';
                    return false;
                }

                int conversionStart = 0;
                if (this.adjustFirstBlock)
                {
                    conversionStart = EncodingUtil.GetNextCodeStart(this.byteBuffer, 0, this.bytesRead, this.encoding);
                    this.startPosition += conversionStart;
                    this.adjustFirstBlock = false;
                }

                bool completed;
                this.decoder.Convert(this.byteBuffer, conversionStart, this.bytesRead, this.charBuffer, 0, this.charBuffer.Length, true, out this.bytesConverted, out this.charsProduced, out completed);
                this.bytesConverted += conversionStart;
            }
        }

        /// <summary>
        /// Gets the position in the stream of the next character to be read.
        /// </summary>
        public long GetPosition()
        {
            if (this.charsConsumed == 0)
            {
                return this.startPosition;
            }
            else if (this.charsProduced == this.charsConsumed)
            {
                return this.startPosition + this.bytesConverted;
            }
            else
            {
                var encoder = this.encoding.GetEncoder();

                int bytesProduced;
                int charsUsed;
                bool completed;
                encoder.Convert(this.charBuffer, 0, this.charsConsumed, this.byteBuffer, 0, this.byteBuffer.Length, true, out charsUsed, out bytesProduced, out completed);

                return this.startPosition + bytesProduced;
            }
        }
    }
}