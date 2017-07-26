using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Paralax;
using System.Collections.Generic;

namespace ParalaxTests
{
    [TestClass]
    public class PartitionTests
    {
        [TestMethod]
        public void TestFilePartitions_2_UTF8()
        {
            TestFilePartitions(
                "TestData.txt",
                new[]
                {
                    new[] { "Line 1", "Line 2", "Line 3", "Line 4" },
                    new[] { "Line 5", "Line 6", "Line 7", "Line 8" }
                });
        }

        private void TestFilePartitions(string fileName, string[][] expected, Encoding encoding = null)
        {
            var streams = new Stream[expected.Length];
            for (int i = 0; i < streams.Length; i++)
            {
                streams[i] = System.IO.File.OpenRead(fileName);
            }

            var readers = TextFilePartitioner.PartitionReaders(streams, encoding);
            TestPartitionedReaders(readers, expected);
        }

        [TestMethod]
        public void TestPartitionLines_2_UTF8()
        {
            // two partitions
            TestPartitionLines(
                "Line 1\nLine 2\nLine 3\nLine 4\nLine 5\nLine 6\nLine 7\nLine 8",
                new[]
                {
                    new[] { "Line 1", "Line 2", "Line 3", "Line 4" },
                    new[] { "Line 5", "Line 6", "Line 7", "Line 8" }
                }, 
                Encoding.UTF8);
        }

        [TestMethod]
        public void TestPartitionLines_3_UTF8()
        {
            // three partitions
            TestPartitionLines(
                "Line 1\nLine 2\nLine 3\nLine 4\nLine 5\nLine 6\nLine 7\nLine 8",
                new[]
                {
                    new[] { "Line 1", "Line 2", "Line 3" },
                    new[] { "Line 4", "Line 5", "Line 6" },
                    new[] { "Line 7", "Line 8"}
                },
                Encoding.UTF8);
        }

        [TestMethod]
        public void TestPartitionLines_4_UTF8()
        {
            // three partitions
            TestPartitionLines(
                "Line 1\nLine 2\nLine 3\nLine 4\nLine 5\nLine 6\nLine 7\nLine 8",
                new[]
                {
                    new[] { "Line 1", "Line 2" },
                    new[] { "Line 3", "Line 4" },
                    new[] { "Line 5", "Line 6" },
                    new[] { "Line 7", "Line 8" }
                },
                Encoding.UTF8);
        }

        private void TestPartitionLines(string text, string[][] expected, Encoding encoding)
        {
            var readers = GetPartitionedReaders(text, expected.Length, encoding);
            TestPartitionedReaders(readers, expected);
        }

        private void TestPartitionedReaders(TextReader[] readers, string[][] expected)
        {
            var actual = readers.Select(r => ReadLines(r)).ToArray();

            for (int i = 0; i < expected.Length; i++)
            {
                var expectedLines = expected[i];
                var actualLines = actual[i];
                Assert.AreEqual(expectedLines.Length, actualLines.Length);

                for (int j = 0; j < expectedLines.Length; j++)
                {
                    Assert.AreEqual(expectedLines[j], actualLines[j]);
                }
            }
        }

        private string[] ReadLines(TextReader reader)
        {
            var list = new List<string>();

            while (true)
            {
                var line = reader.ReadLine();
                if (line == null)
                    break;
                list.Add(line);
            }

            return list.ToArray();
        }

        private TextReader[] GetPartitionedReaders(string text, int partitions, Encoding encoding)
        {
            var ms = new MemoryStream();
            var writer = new StreamWriter(ms, encoding, bufferSize: 1024, leaveOpen: true);
            writer.Write(text);
            writer.Close();

            ms.Position = 0;
            var bytes = ms.ToArray();

            var streams = new Stream[partitions];
            for (int i = 0; i < partitions; i++)
            {
                streams[i] = new MemoryStream(bytes);
            }

            return TextFilePartitioner.PartitionReaders(streams, encoding);
        }



        [TestMethod]
        public void TestPartitionStarts()
        {
            TestPartitionStarts("|Line 1\nLine 2\nLine 3\nLine 4\n|Line 5\nLine 6\nLine 7\nLine 8", Encoding.UTF8);
            TestPartitionStarts("|Line 1\nLine 2\nLine 3\nLine 4\nLine 5\n|Line 6\nLine 7\nLine 8", Encoding.Unicode);

            TestPartitionStarts("|Line 1\r\nLine 2\r\nLine 3\r\nLine 4\r\n|Line 5\r\nLine 6\r\nLine 7\r\nLine 8", Encoding.UTF8);
            TestPartitionStarts("|Line 1\r\nLine 2\r\nLine 3\r\nLine 4\r\n|Line 5\r\nLine 6\r\nLine 7\r\nLine 8", Encoding.Unicode);
        }

        private void TestPartitionStarts(string textWithStartPoints, Encoding encoding)
        {
            GetStartPoints(textWithStartPoints, out var textWithoutStartPoints, out var startPoints);
            var expectedStarts = GetByteOffsets(textWithoutStartPoints, encoding, startPoints);

            var stream = EncodeStream(textWithoutStartPoints, encoding);
            var actualStarts = TextFilePartitioner.GetPartitionStarts(stream, expectedStarts.Length, encoding);

            Assert.AreEqual(expectedStarts.Length, actualStarts.Length);
            for (int i = 0; i < expectedStarts.Length; i++)
            {
                Assert.AreEqual((long)expectedStarts[i], actualStarts[i]);
            }
        }

        private void GetStartPoints(string textWithStartPoints, out string textWithoutStartPoints, out int[] startPoints)
        {
            var list = new List<int>();

            int lastPoint = -1;
            while (true)
            {
                int nextPoint = textWithStartPoints.IndexOf('|', lastPoint + 1);
                if (nextPoint > lastPoint)
                {
                    list.Add(nextPoint - list.Count);
                    lastPoint = nextPoint;
                }
                else
                {
                    break;
                }
            }

            textWithoutStartPoints = textWithStartPoints.Replace("|", "");
            startPoints = list.ToArray();
        }

        private int[] GetByteOffsets(string text, Encoding encoding, int[] indexes)
        {
            var chars = text.ToCharArray();
            var encoder = encoding.GetEncoder();

            var byteOffsets = new int[indexes.Length];
            for (int i = 0; i < indexes.Length; i++)
            {
                byteOffsets[i] = encoder.GetByteCount(chars, 0, indexes[i], true);
            }

            return byteOffsets;
        }

        private int GetByteOffset(string text, Encoding encoding, string match)
        {
            var index = text.IndexOf(match);
            var chars = text.Substring(0, index).ToCharArray();
            return encoding.GetEncoder().GetByteCount(chars, 0, chars.Length, true);
        }

        [TestMethod]
        public void TestCharacterStart_UTF8_MultiByte()
        {
            // one byte encoding
            Assert.AreEqual(0, GetCharacterStart("\u007F abcd", Encoding.UTF8, 0));
            Assert.AreEqual(1, GetCharacterStart("\u007F abcd", Encoding.UTF8, 1));

            // two byte encoding
            Assert.AreEqual(0, GetCharacterStart("\u0080 abcd", Encoding.UTF8, 0));
            Assert.AreEqual(2, GetCharacterStart("\u0080 abcd", Encoding.UTF8, 1));
            Assert.AreEqual(2, GetCharacterStart("\u0080 abcd", Encoding.UTF8, 2));
            Assert.AreEqual(3, GetCharacterStart("\u0080 abcd", Encoding.UTF8, 3));

            Assert.AreEqual(0, GetCharacterStart("\u07FF abcd", Encoding.UTF8, 0));
            Assert.AreEqual(2, GetCharacterStart("\u07FF abcd", Encoding.UTF8, 1));
            Assert.AreEqual(2, GetCharacterStart("\u07FF abcd", Encoding.UTF8, 2));
            Assert.AreEqual(3, GetCharacterStart("\u07FF abcd", Encoding.UTF8, 3));

            // three byte encoding
            Assert.AreEqual(0, GetCharacterStart("\u0800 abcd", Encoding.UTF8, 0));
            Assert.AreEqual(3, GetCharacterStart("\u0800 abcd", Encoding.UTF8, 1));
            Assert.AreEqual(3, GetCharacterStart("\u0800 abcd", Encoding.UTF8, 2));
            Assert.AreEqual(3, GetCharacterStart("\u0800 abcd", Encoding.UTF8, 3));

            Assert.AreEqual(0, GetCharacterStart("\uFFFF abcd", Encoding.UTF8, 0));
            Assert.AreEqual(3, GetCharacterStart("\uFFFF abcd", Encoding.UTF8, 1));
            Assert.AreEqual(3, GetCharacterStart("\uFFFF abcd", Encoding.UTF8, 2));
            Assert.AreEqual(3, GetCharacterStart("\uFFFF abcd", Encoding.UTF8, 3));

            // four byte encoding
            // TODO:  need means to encode 4-byte encodings first! :-)
            Assert.AreEqual(0, GetCharacterStart("\U00010000 abcd", Encoding.Unicode, 0));
            Assert.AreEqual(4, GetCharacterStart("\U00010000 abcd", Encoding.Unicode, 1));
            Assert.AreEqual(4, GetCharacterStart("\U00010000 abcd", Encoding.Unicode, 2));
            Assert.AreEqual(4, GetCharacterStart("\U00010000 abcd", Encoding.Unicode, 3));
            Assert.AreEqual(4, GetCharacterStart("\U00010000 abcd", Encoding.Unicode, 4));
        }

        [TestMethod]
        public void TestCharacterStart_UTF16_Surrogates()
        {
            // string starting with surrogate pair
            Assert.AreEqual(0, GetCharacterStart("\U00010000 abcd", Encoding.Unicode, 0));
            Assert.AreEqual(4, GetCharacterStart("\U00010000 abcd", Encoding.Unicode, 1));
            Assert.AreEqual(4, GetCharacterStart("\U00010000 abcd", Encoding.Unicode, 2));
            Assert.AreEqual(4, GetCharacterStart("\U00010000 abcd", Encoding.Unicode, 3));
            Assert.AreEqual(4, GetCharacterStart("\U00010000 abcd", Encoding.Unicode, 4));

            Assert.AreEqual(0, GetCharacterStart("\U0010FFFF abcd", Encoding.Unicode, 0));
            Assert.AreEqual(4, GetCharacterStart("\U0010FFFF abcd", Encoding.Unicode, 1));
            Assert.AreEqual(4, GetCharacterStart("\U0010FFFF abcd", Encoding.Unicode, 2));
            Assert.AreEqual(4, GetCharacterStart("\U0010FFFF abcd", Encoding.Unicode, 3));
            Assert.AreEqual(4, GetCharacterStart("\U0010FFFF abcd", Encoding.Unicode, 4));

        }

        [TestMethod]
        public void TestCharacterStart_UTF16_BE_Surrogates()
        {
            // string starting with surrogate pair
            Assert.AreEqual(0, GetCharacterStart("\U00010000 abcd", Encoding.BigEndianUnicode, 0));
            Assert.AreEqual(4, GetCharacterStart("\U00010000 abcd", Encoding.BigEndianUnicode, 1));
            Assert.AreEqual(4, GetCharacterStart("\U00010000 abcd", Encoding.BigEndianUnicode, 2));
            Assert.AreEqual(4, GetCharacterStart("\U00010000 abcd", Encoding.BigEndianUnicode, 3));
            Assert.AreEqual(4, GetCharacterStart("\U00010000 abcd", Encoding.BigEndianUnicode, 4));

            Assert.AreEqual(0, GetCharacterStart("\U0010FFFF abcd", Encoding.BigEndianUnicode, 0));
            Assert.AreEqual(4, GetCharacterStart("\U0010FFFF abcd", Encoding.BigEndianUnicode, 1));
            Assert.AreEqual(4, GetCharacterStart("\U0010FFFF abcd", Encoding.BigEndianUnicode, 2));
            Assert.AreEqual(4, GetCharacterStart("\U0010FFFF abcd", Encoding.BigEndianUnicode, 3));
            Assert.AreEqual(4, GetCharacterStart("\U0010FFFF abcd", Encoding.BigEndianUnicode, 4));
        }

        private static int GetCharacterStart(string text, Encoding encoding, int byteOffset)
        {
            var bytes = EncodeBytes(text, encoding);
            return EncodingUtil.GetNextCodeStart(bytes, byteOffset, bytes.Length, encoding);
        }

        private static long GetLineStart(string text, Encoding encoding, int byteOffset)
        {
            var encoded = EncodeStream(text, encoding);
            encoded.Position = byteOffset;
            return TextFilePartitioner.FindNextLineStart(encoded, encoding);
        }

        private static byte[] EncodeBytes(string text, Encoding encoding)
        {
            var chars = text.ToCharArray();
            var encoder = encoding.GetEncoder();
            var byteLength = encoder.GetByteCount(chars, 0, chars.Length, false);
            var bytes = new byte[byteLength];
            encoder.GetBytes(chars, 0, chars.Length, bytes, 0, false);
            return bytes;
        }

        private static Stream EncodeStream(string text, Encoding encoding)
        {
            return new MemoryStream(EncodeBytes(text, encoding));
        }
    }
}
