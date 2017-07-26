using System.IO;
using System.Linq;
using System.Text;

namespace Paralax
{
    /// <summary>
    /// A utility for partitioning a text file into multiple separate, contiguous input streams.
    /// </summary>
    public class TextFilePartitioner
    {
        /// <summary>
        /// Opens a file for reading as multiple parititioned <see cref="TextReader"/>'s.
        /// Each reader reads a separate, contiguous, similarly-sized block of lines.
        /// </summary>
        /// <param name="filename">The file to be read.</param>
        /// <param name="partitions">The number of partitions.</param>
        /// <param name="encoding">An optional encoding for the file. If unspecified it will be inferred from the file itself.</param>
        /// <returns></returns>
        public static TextReader[] OpenPartitions(string filename, int partitions, Encoding encoding = null)
        {
            var streams = new Stream[partitions];
            for (int i = 0; i < partitions; i++)
            {
                streams[i] = File.OpenRead(filename);
            }

            return PartitionReaders(streams, encoding);
        }

        /// <summary>
        /// Creates an array of <see cref="TextReader"/>'s, one for each specified <see cref="Stream"/>.
        /// Each reader reads a separate, contiguous, similarly-sized block of lines.
        /// </summary>
        /// <param name="streams">Multiple overlapping streams of the same data source.</param>
        /// <param name="encoding">An optional encoding for the file. If unspecified it will be inferred from the stream itself.</param>
        /// <returns></returns>
        public static TextReader[] PartitionReaders(Stream[] streams, Encoding encoding = null)
        {
            encoding = encoding ?? GetEncoding(streams[0]);
            return PartitionStreams(streams, encoding).Select(s => new StreamReader(s, encoding)).ToArray();
        }

        /// <summary>
        /// Creates an array of partitioned text <see cref="Stream"/>'s from an array of overlapping text <see cref="Stream"/>'s.
        /// Each partitioned stream covers a separate, contiguous, similarly-size block of text lines.
        /// </summary>
        /// <param name="streams">Multiple overlapping streams of the same data source.</param>
        /// <param name="encoding">An optional encoding for the file. If unspecified it will be inferred from the stream itself.</param>
        /// <returns></returns>
        private static Stream[] PartitionStreams(Stream[] streams, Encoding encoding = null)
        {
            var length = streams[0].Length;
            var starts = GetPartitionStarts(streams[0], streams.Length, encoding);

            var subStreams = new Stream[streams.Length];
            for (int i = 0; i < streams.Length; i++)
            {
                streams[i].Position = starts[i];

                // wrap in sub stream to constrain consumer to just the designated range of text
                var subStreamLength = (i < streams.Length - 1)
                    ? starts[i + 1] - starts[i]       // length is difference between this stream's start and next stream's start
                    : length - starts[i];  // length is difference between total stream length and this stream's start

                subStreams[i] = new SubStream(streams[i], starts[i], subStreamLength);
            }

            return subStreams;
        }

        /// <summary>
        /// Divides the stream into separate partitions. 
        /// </summary>
        /// <param name="stream">A stream over the entire file.</param>
        /// <param name="partitions">The number of similar size partitions requested.</param>
        /// <param name="encoding">An optional encoding for the file. If unspecified it will be inferred from the stream itself.</param>
        /// <returns>Returns an array of starting positions, one for each partition.</returns>
        public static long[] GetPartitionStarts(Stream stream, int partitions, Encoding encoding = null)
        {
            encoding = encoding ?? GetEncoding(stream);

            // give each stream initial starting point.
            var size = stream.Length / partitions;

            // position each stream at initial computed start position
            long[] starts = new long[partitions];

            long start = 0;
            for (int i = 0; i < partitions; i++, start += size)
            {
                starts[i] = start + EncodingUtil.GetAlignment(start, encoding);
            }

            // adjust starting points by finding next actual line start
            for (int i = 1; i < partitions; i++)
            {
                // move ahead if the prior stream is already overlapping initial starting point
                if (starts[i - 1] > starts[i])
                {
                    starts[i] = starts[i - 1];
                }

                stream.Position = starts[i];
                var nextLineStart = FindNextLineStart(stream, encoding);
                if (nextLineStart > 0)
                {
                    starts[i] = nextLineStart;
                }
                else
                {
                    starts[i] = stream.Length;
                }
            }

            stream.Position = 0;
            return starts;
        }

        /// <summary>
        /// Gets the encoding from the stream BOM (unicode only?)
        /// </summary>
        private static Encoding GetEncoding(Stream stream)
        {
            stream.Position = 0;
            var encoding = new StreamReader(stream).CurrentEncoding;
            stream.Position = 0;
            return encoding;
        }

        /// <summary>
        /// Determine the start of the next text line.
        /// </summary>
        internal static long FindNextLineStart(Stream stream, Encoding encoding)
        {
            // use CharReader so I can determine the byte offset (position) in the
            // stream that corresponds to the start of the character after the line break.
            // If I could do this with StreamReader I would.
            var reader = new CharReader(stream, encoding);

            char ch;
            while (reader.TryReadNextChar(out ch))
            {
                if (ch == '\n')
                {
                    return reader.GetPosition();
                }
            }

            return -1;
        }
    }
}
