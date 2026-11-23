// Copyright (c) Sébastien Ros. Licensed under the MIT license.

#if NETFRAMEWORK
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Xunit;

namespace System.Text.Yaml.Tests
{
    /// <summary>
    /// M7c: on .NET Framework and .NET Standard, where they are not obsolete, a
    /// <see cref="YamlException"/> keeps its formatter-based serialization members, so that it can cross an AppDomain as
    /// upstream's does; on .NET they are removed (SYSLIB0051). A round trip through <see cref="BinaryFormatter"/> keeps
    /// the message, the position and the path.
    /// </summary>
    public class ExceptionSerializationTests
    {
        private static T RoundTrip<T>(T exception) where T : Exception
        {
            var formatter = new BinaryFormatter();
            using var stream = new MemoryStream();
            formatter.Serialize(stream, exception);
            stream.Position = 0;
            return (T)formatter.Deserialize(stream);
        }

        [Fact]
        public void YamlException_RoundTrips_ThroughTheBinaryFormatter()
        {
            // A serializer error: the path, the line and the position.
            YamlException thrown = Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<Holder>("A: [1, x]\n"));
            Assert.NotNull(thrown.Path);
            YamlException back = RoundTrip(thrown);
            Assert.Equal(thrown.GetType(), back.GetType());
            Assert.Equal(thrown.Message, back.Message);
            Assert.Equal(thrown.Path, back.Path);
            Assert.Equal(thrown.LineNumber, back.LineNumber);
            Assert.Equal(thrown.BytePositionInLine, back.BytePositionInLine);

            // A reader error, thrown as the reader's own exception type.
            YamlException reader = Assert.ThrowsAny<YamlException>(() =>
            {
                var r = new Utf8YamlReader(Encoding.UTF8.GetBytes("a: [1, 2\n"));
                while (r.Read())
                {
                }
            });
            YamlException readerBack = RoundTrip(reader);
            Assert.Equal(reader.GetType(), readerBack.GetType());
            Assert.Equal(reader.Message, readerBack.Message);
            Assert.Equal(reader.LineNumber, readerBack.LineNumber);
            Assert.Equal(reader.BytePositionInLine, readerBack.BytePositionInLine);

            // One made by the caller.
            var created = new YamlException("m", "$.p", 3, 4);
            YamlException createdBack = RoundTrip(created);
            Assert.Equal("m", createdBack.Message);
            Assert.Equal("$.p", createdBack.Path);
            Assert.Equal(3, createdBack.LineNumber);
            Assert.Equal(4, createdBack.BytePositionInLine);
        }

        public sealed class Holder
        {
            public int[]? A { get; set; }
        }
    }
}
#endif
