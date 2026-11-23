// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public static partial class CustomConverterTests
    {
        private class FailConverter<TException> : YamlConverter<int> where TException: Exception, new()
        {
            public override int Read(ref Utf8YamlReader reader, Type typeToConvert, YamlSerializerOptions options)
            {
                throw new TException();
            }

            public override void Write(Utf8YamlWriter writer, int value, YamlSerializerOptions options)
            {
                throw new TException();
            }
        }

        private static void ConverterFailNoRethrow<TException>() where TException : Exception, new()
        {
            var options = new YamlSerializerOptions();
            YamlConverter converter = new FailConverter<TException>();
            options.Converters.Add(converter);

            Assert.Throws<TException>(() => YamlSerializer.Deserialize<int>("0", options));
            Assert.Throws<TException>(() => YamlSerializer.Deserialize<int[]>("[0]", options));
            Assert.Throws<TException>(() => YamlSerializer.Serialize(0, options));
            Assert.Throws<TException>(() => YamlSerializer.Serialize(new int[] { 0 }, options));

            var obj = new Dictionary<string, int>();
            obj["key"] = 0;

            Assert.Throws<TException>(() => YamlSerializer.Serialize(obj, options));
        }

        [Fact]
        public static void ConverterExceptionsNotRethrownFail()
        {
            // We should not catch these unless thrown from the reader\document.
            ConverterFailNoRethrow<FormatException>();
            ConverterFailNoRethrow<ArgumentException>();

            // Other misc exception we should not catch:
            ConverterFailNoRethrow<Exception>();
            ConverterFailNoRethrow<InvalidOperationException>();
            ConverterFailNoRethrow<IndexOutOfRangeException>();
            ConverterFailNoRethrow<NotSupportedException>();
        }
    }
}
