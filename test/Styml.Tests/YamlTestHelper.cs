// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Yaml.Tests;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Sdk;

namespace System.Text.Yaml
{
    internal static partial class YamlTestHelper
    {
        public static string NewtonsoftReturnStringHelper(TextReader reader)
        {
            var sb = new StringBuilder();
            var yaml = new JsonTextReader(reader) { MaxDepth = null };
            while (yaml.Read())
            {
                if (yaml.Value != null)
                {
                    // Use InvariantCulture to make sure numbers retain the decimal point '.'
                    sb.AppendFormat(CultureInfo.InvariantCulture, "{0}, ", yaml.Value);
                }
            }
            return sb.ToString();
        }

        public static string WriteDepthObject(int depth, bool writeComment = false)
        {
            var sb = new StringBuilder();
            var textWriter = new StringWriter();
            var yaml = new JsonTextWriter(textWriter);
            yaml.WriteStartObject();
            for (int i = 0; i < depth; i++)
            {
                yaml.WritePropertyName("message" + i);
                yaml.WriteStartObject();
            }
            if (writeComment)
                yaml.WriteComment("Random comment string");
            yaml.WritePropertyName("message" + depth);
            yaml.WriteValue("Hello, World!");
            for (int i = 0; i < depth; i++)
            {
                yaml.WriteEndObject();
            }
            yaml.WriteEndObject();
            yaml.Flush();

            return textWriter.ToString();
        }

        public static string WriteDepthArray(int depth, bool writeComment = false)
        {
            var sb = new StringBuilder();
            var textWriter = new StringWriter();
            var yaml = new JsonTextWriter(textWriter);
            yaml.WriteStartArray();
            for (int i = 0; i < depth; i++)
            {
                yaml.WriteStartArray();
            }
            if (writeComment)
                yaml.WriteComment("Random comment string");
            yaml.WriteValue("Hello, World!");
            for (int i = 0; i < depth; i++)
            {
                yaml.WriteEndArray();
            }
            yaml.WriteEndArray();
            yaml.Flush();

            return textWriter.ToString();
        }

        public static string WriteDepthObjectWithArray(int depth, bool writeComment = false)
        {
            var sb = new StringBuilder();
            var textWriter = new StringWriter();
            var yaml = new JsonTextWriter(textWriter);
            yaml.WriteStartObject();
            for (int i = 0; i < depth; i++)
            {
                yaml.WritePropertyName("message" + i);
                yaml.WriteStartObject();
            }
            if (writeComment)
                yaml.WriteComment("Random comment string");
            yaml.WritePropertyName("message" + depth);
            yaml.WriteStartArray();
            yaml.WriteValue("string1");
            yaml.WriteValue("string2");
            yaml.WriteEndArray();
            yaml.WritePropertyName("address");
            yaml.WriteStartObject();
            yaml.WritePropertyName("street");
            yaml.WriteValue("1 Microsoft Way");
            yaml.WritePropertyName("city");
            yaml.WriteValue("Redmond");
            yaml.WritePropertyName("zip");
            yaml.WriteValue(98052);
            yaml.WriteEndObject();
            for (int i = 0; i < depth; i++)
            {
                yaml.WriteEndObject();
            }
            yaml.WriteEndObject();
            yaml.Flush();

            return textWriter.ToString();
        }

        public static byte[] ReturnBytesHelper(byte[] data, out int length, YamlCommentHandling commentHandling = YamlCommentHandling.Disallow, int maxDepth = 64)
        {
            var state = new YamlReaderState(new YamlReaderOptions { CommentHandling = commentHandling, MaxDepth = maxDepth });
            var reader = new Utf8YamlReader(data, true, state);
            return ReaderLoop(data.Length, out length, ref reader);
        }

        public static byte[] SequenceReturnBytesHelper(byte[] data, out int length, YamlCommentHandling commentHandling = YamlCommentHandling.Disallow, int maxDepth = 64)
        {
            ReadOnlySequence<byte> sequence = CreateSegments(data);
            var state = new YamlReaderState(new YamlReaderOptions { CommentHandling = commentHandling, MaxDepth = maxDepth });
            var reader = new Utf8YamlReader(sequence, true, state);
            return ReaderLoop(data.Length, out length, ref reader);
        }

        public delegate void Utf8YamlReaderAction(ref Utf8YamlReader reader);

        public static void AssertWithSingleAndMultiSegmentReader(string yaml, Utf8YamlReaderAction action, YamlReaderOptions options = default)
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(yaml);

            var singleSegmentReader = new Utf8YamlReader(utf8, options);
            action(ref singleSegmentReader);

            ReadOnlySequence<byte> sequence = GetSequence(utf8, segmentSize: 1);
            var multiSegmentReader = new Utf8YamlReader(sequence, options);
            action(ref multiSegmentReader);
        }

        public static ReadOnlySequence<byte> CreateSegments(byte[] data)
        {
            ReadOnlyMemory<byte> dataMemory = data;

            var firstSegment = new BufferSegment<byte>(dataMemory.Slice(0, data.Length / 2));
            ReadOnlyMemory<byte> secondMem = dataMemory.Slice(data.Length / 2);
            BufferSegment<byte> secondSegment = firstSegment.Append(secondMem);

            return new ReadOnlySequence<byte>(firstSegment, 0, secondSegment, secondMem.Length);
        }

        public static ReadOnlySequence<byte> CreateSegments(byte[] data, int splitLocation)
        {
            Debug.Assert(splitLocation <= data.Length);

            ReadOnlyMemory<byte> dataMemory = data;

            var firstSegment = new BufferSegment<byte>(dataMemory.Slice(0, splitLocation));
            ReadOnlyMemory<byte> secondMem = dataMemory.Slice(splitLocation);
            BufferSegment<byte> secondSegment = firstSegment.Append(secondMem);

            return new ReadOnlySequence<byte>(firstSegment, 0, secondSegment, secondMem.Length);
        }

        public static ReadOnlySequence<byte> CreateSegments(byte[] data, int firstSplit, int secondSplit)
        {
            Debug.Assert(firstSplit <= data.Length && secondSplit <= data.Length && firstSplit <= secondSplit);

            ReadOnlyMemory<byte> dataMemory = data;

            var firstSegment = new BufferSegment<byte>(dataMemory.Slice(0, firstSplit));
            ReadOnlyMemory<byte> secondMem = dataMemory.Slice(firstSplit, secondSplit - firstSplit);
            BufferSegment<byte> secondSegment = firstSegment.Append(secondMem);

            ReadOnlyMemory<byte> thirdMem = dataMemory.Slice(secondSplit);
            BufferSegment<byte> thirdSegment = secondSegment.Append(thirdMem);

            return new ReadOnlySequence<byte>(firstSegment, 0, thirdSegment, thirdMem.Length);
        }

        public static ReadOnlySequence<byte> GetSequence(string yaml, int segmentSize)
        {
            byte[] encoding = Encoding.UTF8.GetBytes(yaml);
            return GetSequence(encoding, segmentSize);
        }

        public static ReadOnlySequence<byte> GetSequence(byte[] dataUtf8, int segmentSize)
        {
            int numberOfSegments = dataUtf8.Length / segmentSize + 1;
            byte[][] buffers = new byte[numberOfSegments][];

            for (int j = 0; j < numberOfSegments - 1; j++)
            {
                buffers[j] = new byte[segmentSize];
                Array.Copy(dataUtf8, j * segmentSize, buffers[j], 0, segmentSize);
            }

            int remaining = dataUtf8.Length % segmentSize;
            buffers[numberOfSegments - 1] = new byte[remaining];
            Array.Copy(dataUtf8, dataUtf8.Length - remaining, buffers[numberOfSegments - 1], 0, remaining);

            return BufferFactory.Create(buffers);
        }

        public static List<ReadOnlySequence<byte>> GetSequences(ReadOnlyMemory<byte> dataMemory)
        {
            var sequences = new List<ReadOnlySequence<byte>>
            {
                new ReadOnlySequence<byte>(dataMemory)
            };

            for (int i = 0; i < dataMemory.Length; i++)
            {
                var firstSegment = new BufferSegment<byte>(dataMemory.Slice(0, i));
                ReadOnlyMemory<byte> secondMem = dataMemory.Slice(i);
                BufferSegment<byte> secondSegment = firstSegment.Append(secondMem);
                var sequence = new ReadOnlySequence<byte>(firstSegment, 0, secondSegment, secondMem.Length);
                sequences.Add(sequence);
            }
            return sequences;
        }

        public static ReadOnlySequence<byte> GetSequence(IEnumerable<byte[]> yaml)
        {
            return BufferFactory.Create(yaml.ToArray());
        }

        internal static ReadOnlySequence<byte> SegmentInto(ReadOnlyMemory<byte> data, int segmentCount)
        {
            if (segmentCount < 2)
                throw new ArgumentOutOfRangeException(nameof(segmentCount));

            int perSegment = data.Length / segmentCount;
            BufferSegment<byte> first;

            if (perSegment == 0 && data.Length > 0)
            {
                first = new BufferSegment<byte>(data.Slice(0, 1));
                data = data.Slice(1);
            }
            else
            {
                first = new BufferSegment<byte>(data.Slice(0, perSegment));
                data = data.Slice(perSegment);
            }

            BufferSegment<byte> last = first;
            segmentCount--;

            while (segmentCount > 1)
            {
                perSegment = data.Length / segmentCount;
                last = last.Append(data.Slice(0, perSegment));
                data = data.Slice(perSegment);
                segmentCount--;
            }

            last = last.Append(data);
            return new ReadOnlySequence<byte>(first, 0, last, data.Length);
        }

        public static object ReturnObjectHelper(byte[] data, YamlCommentHandling commentHandling = YamlCommentHandling.Disallow)
        {
            var state = new YamlReaderState(options: new YamlReaderOptions { CommentHandling = commentHandling });
            var reader = new Utf8YamlReader(data, true, state);
            return ReaderLoop(ref reader);
        }

        public static string ObjectToString(object yamlValues)
        {
            string s = "";
            if (yamlValues is List<object> yamlList)
                s = ListToString(yamlList);
            else if (yamlValues is Dictionary<string, object> yamlDictionary)
                s = DictionaryToString(yamlDictionary);
            return s;
        }

        public static string DictionaryToString(Dictionary<string, object> dictionary)
        {
            var builder = new StringBuilder();
            foreach (KeyValuePair<string, object> entry in dictionary)
            {
                if (entry.Value is Dictionary<string, object> nestedDictionary)
                    builder.AppendFormat(CultureInfo.InvariantCulture, "{0}, ", entry.Key).Append(DictionaryToString(nestedDictionary));
                else if (entry.Value is List<object> nestedList)
                    builder.AppendFormat(CultureInfo.InvariantCulture, "{0}, ", entry.Key).Append(ListToString(nestedList));
                else
                    builder.AppendFormat(CultureInfo.InvariantCulture, "{0}, {1}, ", entry.Key, entry.Value);
            }
            return builder.ToString();
        }

        public static string ListToString(List<object> list)
        {
            var builder = new StringBuilder();
            foreach (object entry in list)
            {
                if (entry is Dictionary<string, object> nestedDictionary)
                    builder.Append(DictionaryToString(nestedDictionary));
                else if (entry is List<object> nestedList)
                    builder.Append(ListToString(nestedList));
                else
                    builder.AppendFormat(CultureInfo.InvariantCulture, "{0}, ", entry);
            }
            return builder.ToString();
        }

        private static YamlTokenType MapTokenType(JsonToken token)
        {
            switch (token)
            {
                case JsonToken.None:
                    return YamlTokenType.None;
                case JsonToken.StartObject:
                    return YamlTokenType.StartObject;
                case JsonToken.StartArray:
                    return YamlTokenType.StartArray;
                case JsonToken.PropertyName:
                    return YamlTokenType.PropertyName;
                case JsonToken.Comment:
                    return YamlTokenType.Comment;
                case JsonToken.Integer:
                case JsonToken.Float:
                    return YamlTokenType.Number;
                case JsonToken.String:
                    return YamlTokenType.String;
                case JsonToken.Boolean:
                    return YamlTokenType.True;
                case JsonToken.Null:
                    return YamlTokenType.Null;
                case JsonToken.EndObject:
                    return YamlTokenType.EndObject;
                case JsonToken.EndArray:
                    return YamlTokenType.EndArray;
                case JsonToken.StartConstructor:
                case JsonToken.EndConstructor:
                case JsonToken.Date:
                case JsonToken.Bytes:
                case JsonToken.Undefined:
                case JsonToken.Raw:
                default:
                    throw new InvalidOperationException();
            }
        }

        public static string InsertCommentsEverywhere(string yamlString)
        {
            StringBuilder sb = new StringBuilder();
            StringWriter sw = new StringWriter(sb);

            using (JsonWriter writer = new JsonTextWriter(sw))
            {
                writer.Formatting = Formatting.Indented;

                var newtonsoft = new JsonTextReader(new StringReader(yamlString)) { MaxDepth = null };
                writer.WriteComment("comment");
                while (newtonsoft.Read())
                {
                    writer.WriteToken(newtonsoft, writeChildren: false);
                    writer.WriteComment("comment");
                }
                writer.WriteComment("comment");
            }
            return sb.ToString();
        }

        public static List<YamlTokenType> GetTokenTypes(string yamlString)
        {
            var newtonsoft = new JsonTextReader(new StringReader(yamlString)) { MaxDepth = null };
            int totalReads = 0;
            while (newtonsoft.Read())
            {
                totalReads++;
            }

            var expectedTokenTypes = new List<YamlTokenType>();

            for (int i = 0; i < totalReads; i++)
            {
                newtonsoft = new JsonTextReader(new StringReader(yamlString)) { MaxDepth = null };
                for (int j = 0; j < i; j++)
                {
                    Assert.True(newtonsoft.Read());
                }
                newtonsoft.Skip();
                expectedTokenTypes.Add(MapTokenType(newtonsoft.TokenType));
            }

            return expectedTokenTypes;
        }

        public static byte[] ReaderLoop(int inpuDataLength, out int length, ref Utf8YamlReader yaml)
        {
            byte[] outputArray = new byte[inpuDataLength];
            Span<byte> destination = outputArray;

            while (yaml.Read())
            {
                YamlTokenType tokenType = yaml.TokenType;
                ReadOnlySpan<byte> valueSpan = yaml.HasValueSequence ? yaml.ValueSequence.ToArray() : yaml.ValueSpan;
                if (yaml.HasValueSequence)
                {
                    Assert.True(yaml.ValueSpan == default);
                    if ((tokenType != YamlTokenType.String && tokenType != YamlTokenType.PropertyName) || yaml.GetString().Length != 0)
                    {
                        // Empty strings could still make this true, i.e. ""
                        Assert.False(yaml.ValueSequence.IsEmpty);
                    }
                }
                else
                {
                    Assert.True(yaml.ValueSequence.IsEmpty);
                    if ((tokenType != YamlTokenType.String && tokenType != YamlTokenType.PropertyName) || yaml.GetString().Length != 0)
                    {
                        // Empty strings could still make this true, i.e. ""
                        Assert.False(yaml.ValueSpan == default);
                    }
                }
                switch (tokenType)
                {
                    case YamlTokenType.PropertyName:
                        valueSpan.CopyTo(destination);
                        destination[valueSpan.Length] = (byte)',';
                        destination[valueSpan.Length + 1] = (byte)' ';
                        destination = destination.Slice(valueSpan.Length + 2);
                        break;
                    case YamlTokenType.Number:
                    case YamlTokenType.String:
                    case YamlTokenType.Comment:
                        valueSpan.CopyTo(destination);
                        destination[valueSpan.Length] = (byte)',';
                        destination[valueSpan.Length + 1] = (byte)' ';
                        destination = destination.Slice(valueSpan.Length + 2);
                        break;
                    case YamlTokenType.True:
                        // Special casing True/False so that the casing matches with Yaml.NET
                        destination[0] = (byte)'T';
                        destination[1] = (byte)'r';
                        destination[2] = (byte)'u';
                        destination[3] = (byte)'e';
                        destination[valueSpan.Length] = (byte)',';
                        destination[valueSpan.Length + 1] = (byte)' ';
                        destination = destination.Slice(valueSpan.Length + 2);
                        break;
                    case YamlTokenType.False:
                        destination[0] = (byte)'F';
                        destination[1] = (byte)'a';
                        destination[2] = (byte)'l';
                        destination[3] = (byte)'s';
                        destination[4] = (byte)'e';
                        destination[valueSpan.Length] = (byte)',';
                        destination[valueSpan.Length + 1] = (byte)' ';
                        destination = destination.Slice(valueSpan.Length + 2);
                        break;
                    case YamlTokenType.Null:
                        // Special casing Null so that it matches what JSON.NET does
                        break;
                    case YamlTokenType.StartObject:
                        Assert.True(yaml.ValueSpan.SequenceEqual(new byte[] { (byte)'{' }));
                        Assert.True(yaml.ValueSequence.IsEmpty);
                        break;
                    case YamlTokenType.EndObject:
                        Assert.True(yaml.ValueSpan.SequenceEqual(new byte[] { (byte)'}' }));
                        Assert.True(yaml.ValueSequence.IsEmpty);
                        break;
                    case YamlTokenType.StartArray:
                        Assert.True(yaml.ValueSpan.SequenceEqual(new byte[] { (byte)'[' }));
                        Assert.True(yaml.ValueSequence.IsEmpty);
                        break;
                    case YamlTokenType.EndArray:
                        Assert.True(yaml.ValueSpan.SequenceEqual(new byte[] { (byte)']' }));
                        Assert.True(yaml.ValueSequence.IsEmpty);
                        break;
                    default:
                        break;
                }
            }
            length = outputArray.Length - destination.Length;
            return outputArray;
        }

        public static object ReaderLoop(ref Utf8YamlReader yaml)
        {
            object root = null;

            while (yaml.Read())
            {
                YamlTokenType tokenType = yaml.TokenType;
                ReadOnlySpan<byte> valueSpan = yaml.ValueSpan;
                switch (tokenType)
                {
                    case YamlTokenType.True:
                    case YamlTokenType.False:
                        root = valueSpan[0] == 't';
                        break;
                    case YamlTokenType.Number:
                        yaml.TryGetDouble(out double valueDouble);
                        root = valueDouble;
                        break;
                    case YamlTokenType.String:
                        string valueString = yaml.GetString();
                        root = valueString;
                        break;
                    case YamlTokenType.Null:
                        break;
                    case YamlTokenType.StartObject:
                        Assert.True(valueSpan.SequenceEqual(new byte[] { (byte)'{' }));
                        root = ReaderDictionaryLoop(ref yaml);
                        break;
                    case YamlTokenType.StartArray:
                        Assert.True(valueSpan.SequenceEqual(new byte[] { (byte)'[' }));
                        root = ReaderListLoop(ref yaml);
                        break;
                    case YamlTokenType.EndObject:
                        Assert.True(valueSpan.SequenceEqual(new byte[] { (byte)'}' }));
                        break;
                    case YamlTokenType.EndArray:
                        Assert.True(valueSpan.SequenceEqual(new byte[] { (byte)']' }));
                        break;
                    case YamlTokenType.None:
                    case YamlTokenType.Comment:
                    default:
                        break;
                }
            }
            return root;
        }

        public static Dictionary<string, object> ReaderDictionaryLoop(ref Utf8YamlReader yaml)
        {
            Dictionary<string, object> dictionary = new Dictionary<string, object>();

            string key = "";
            object value = null;

            while (yaml.Read())
            {
                YamlTokenType tokenType = yaml.TokenType;
                ReadOnlySpan<byte> valueSpan = yaml.ValueSpan;
                switch (tokenType)
                {
                    case YamlTokenType.PropertyName:
                        key = yaml.GetString();
                        dictionary.Add(key, null);
                        break;
                    case YamlTokenType.True:
                    case YamlTokenType.False:
                        value = valueSpan[0] == 't';
                        if (dictionary.TryGetValue(key, out _))
                        {
                            dictionary[key] = value;
                        }
                        else
                        {
                            dictionary.Add(key, value);
                        }
                        break;
                    case YamlTokenType.Number:
                        yaml.TryGetDouble(out double valueDouble);
                        if (dictionary.TryGetValue(key, out _))
                        {
                            dictionary[key] = valueDouble;
                        }
                        else
                        {
                            dictionary.Add(key, valueDouble);
                        }
                        break;
                    case YamlTokenType.String:
                        string valueString = yaml.GetString();
                        if (dictionary.TryGetValue(key, out _))
                        {
                            dictionary[key] = valueString;
                        }
                        else
                        {
                            dictionary.Add(key, valueString);
                        }
                        break;
                    case YamlTokenType.Null:
                        value = null;
                        if (dictionary.TryGetValue(key, out _))
                        {
                            dictionary[key] = value;
                        }
                        else
                        {
                            dictionary.Add(key, value);
                        }
                        break;
                    case YamlTokenType.StartObject:
                        Assert.True(valueSpan.SequenceEqual(new byte[] { (byte)'{' }));
                        value = ReaderDictionaryLoop(ref yaml);
                        if (dictionary.TryGetValue(key, out _))
                        {
                            dictionary[key] = value;
                        }
                        else
                        {
                            dictionary.Add(key, value);
                        }
                        break;
                    case YamlTokenType.StartArray:
                        Assert.True(valueSpan.SequenceEqual(new byte[] { (byte)'[' }));
                        value = ReaderListLoop(ref yaml);
                        if (dictionary.TryGetValue(key, out _))
                        {
                            dictionary[key] = value;
                        }
                        else
                        {
                            dictionary.Add(key, value);
                        }
                        break;
                    case YamlTokenType.EndObject:
                        Assert.True(valueSpan.SequenceEqual(new byte[] { (byte)'}' }));
                        return dictionary;
                    case YamlTokenType.None:
                    case YamlTokenType.Comment:
                    default:
                        break;
                }
            }
            return dictionary;
        }

        public static List<object> ReaderListLoop(ref Utf8YamlReader yaml)
        {
            List<object> arrayList = new List<object>();

            object value = null;

            while (yaml.Read())
            {
                YamlTokenType tokenType = yaml.TokenType;
                ReadOnlySpan<byte> valueSpan = yaml.ValueSpan;
                switch (tokenType)
                {
                    case YamlTokenType.True:
                    case YamlTokenType.False:
                        value = valueSpan[0] == 't';
                        arrayList.Add(value);
                        break;
                    case YamlTokenType.Number:
                        yaml.TryGetDouble(out double doubleValue);
                        arrayList.Add(doubleValue);
                        break;
                    case YamlTokenType.String:
                        string valueString = yaml.GetString();
                        arrayList.Add(valueString);
                        break;
                    case YamlTokenType.Null:
                        value = null;
                        arrayList.Add(value);
                        break;
                    case YamlTokenType.StartObject:
                        Assert.True(valueSpan.SequenceEqual(new byte[] { (byte)'{' }));
                        value = ReaderDictionaryLoop(ref yaml);
                        arrayList.Add(value);
                        break;
                    case YamlTokenType.StartArray:
                        Assert.True(valueSpan.SequenceEqual(new byte[] { (byte)'[' }));
                        value = ReaderListLoop(ref yaml);
                        arrayList.Add(value);
                        break;
                    case YamlTokenType.EndArray:
                        Assert.True(valueSpan.SequenceEqual(new byte[] { (byte)']' }));
                        return arrayList;
                    case YamlTokenType.None:
                    case YamlTokenType.Comment:
                    default:
                        break;
                }
            }
            return arrayList;
        }

        public static string GetCompactString(string yamlString)
        {
            using (var yamlReader = new JsonTextReader(new StringReader(yamlString)) { MaxDepth = null })
            {
                yamlReader.FloatParseHandling = FloatParseHandling.Decimal;
                JToken jtoken = JToken.ReadFrom(yamlReader);
                var stringWriter = new StringWriter();
                using (JsonTextWriter yamlWriter = new JsonTextWriter(stringWriter))
                {
                    jtoken.WriteTo(yamlWriter);
                    return stringWriter.ToString();
                }
            }
        }

        public static void AssertContents(
#if NET
            [StringSyntax(StringSyntaxAttribute.Json)]
#endif
            string expectedValue,
            ArrayBufferWriter<byte> buffer,
            bool skipSpecialRules = false)
        {
            string value = Encoding.UTF8.GetString(
                    buffer.WrittenSpan
#if NETFRAMEWORK
                        .ToArray()
#endif
                    );

            AssertContentsAgainstYamlNet(expectedValue, value, skipSpecialRules);
        }

        public static void AssertContents(
#if NET
            [StringSyntax(StringSyntaxAttribute.Json)]
#endif
            string expectedValue,
            MemoryStream stream,
            bool skipSpecialRules = false)
        {
            string value = Encoding.UTF8.GetString(stream.ToArray());

            AssertContentsAgainstYamlNet(expectedValue, value, skipSpecialRules);
        }

        public static void AssertContentsNotEqual(
#if NET
            [StringSyntax(StringSyntaxAttribute.Json)]
#endif
            string expectedValue,
            ArrayBufferWriter<byte> buffer,
            bool skipSpecialRules = false)
        {
            string value = Encoding.UTF8.GetString(
                    buffer.WrittenSpan
#if NETFRAMEWORK
                        .ToArray()
#endif
                    );

            AssertContentsNotEqualAgainstYamlNet(expectedValue, value, skipSpecialRules);
        }

        public static void AssertContentsAgainstYamlNet(
#if NET
            [StringSyntax(StringSyntaxAttribute.Json)]
#endif
            string expectedValue,
            string value,
            bool skipSpecialRules)
        {
            Assert.Equal(expectedValue.NormalizeToYamlNetFormat(skipSpecialRules), value.NormalizeToYamlNetFormat(skipSpecialRules), ignoreLineEndingDifferences: true);
        }

        public static void AssertContentsNotEqualAgainstYamlNet(
#if NET
            [StringSyntax(StringSyntaxAttribute.Json)]
#endif
            string expectedValue,
            string value,
            bool skipSpecialRules)
        {
            Assert.NotEqual(expectedValue.NormalizeToYamlNetFormat(skipSpecialRules), value.NormalizeToYamlNetFormat(skipSpecialRules));
        }

        public delegate void AssertThrowsActionUtf8YamlReader(ref Utf8YamlReader yaml);

        // Cannot use standard Assert.Throws() when testing Utf8YamlReader - ref structs and closures don't get along.
        public static TException AssertThrows<TException>(ref Utf8YamlReader yaml, AssertThrowsActionUtf8YamlReader action) where TException : Exception
        {
            Exception ex;

            try
            {
                action(ref yaml);
                ex = null;
            }
            catch (Exception e)
            {
                ex = e;
            }

            if (ex is TException matchingEx)
            {
                return matchingEx;
            }

            throw ex is null ? ThrowsException.ForNoException(typeof(TException)) : ThrowsException.ForIncorrectExceptionType(typeof(TException), ex);
        }

#if NET
        // This is needed due to the fact that git might normalize line endings when checking-out files
        public static string NormalizeLineEndings(this string value) => value.ReplaceLineEndings();
#else
        private const string CompiledNewline = """


            """;

        private static readonly bool s_replaceNewlines =
            !StringComparer.Ordinal.Equals(CompiledNewline, Environment.NewLine);

        // Should be called only on compile-time strings
        // This is needed due to the fact that git might normalize line endings when checking-out files
        public static string NormalizeLineEndings(this string value)
            => s_replaceNewlines ?
            value.Replace(CompiledNewline, Environment.NewLine) :
            value;
#endif
    }
}
