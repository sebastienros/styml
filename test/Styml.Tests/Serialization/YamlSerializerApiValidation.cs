// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text.Yaml;
using System.Text.Yaml.Serialization;
using System.Text.Yaml.Serialization.Metadata;
using System.Text.Yaml.Serialization.Tests;
using System.Threading.Tasks;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public class YamlSerializerApiValidation_Span : YamlSerializerApiValidation
    {
        public YamlSerializerApiValidation_Span() : base(YamlSerializerWrapper.SpanSerializer) { }
    }

    public class YamlSerializerApiValidation_String : YamlSerializerApiValidation
    {
        public YamlSerializerApiValidation_String() : base(YamlSerializerWrapper.StringSerializer) { }
    }

    public class YamlSerializerApiValidation_AsyncStream : YamlSerializerApiValidation
    {
        public YamlSerializerApiValidation_AsyncStream() : base(YamlSerializerWrapper.AsyncStreamSerializer) { }
    }

    public class YamlSerializerApiValidation_SyncStream : YamlSerializerApiValidation
    {
        public YamlSerializerApiValidation_SyncStream() : base(YamlSerializerWrapper.SyncStreamSerializer) { }
    }

    public class YamlSerializerApiValidation_Writer : YamlSerializerApiValidation
    {
        public YamlSerializerApiValidation_Writer() : base(YamlSerializerWrapper.ReaderWriterSerializer) { }
    }

    public class YamlSerializerApiValidation_Document : YamlSerializerApiValidation
    {
        public YamlSerializerApiValidation_Document() : base(YamlSerializerWrapper.DocumentSerializer) { }
    }

    public class YamlSerializerApiValidation_Element : YamlSerializerApiValidation
    {
        public YamlSerializerApiValidation_Element() : base(YamlSerializerWrapper.ElementSerializer) { }
    }

    public class YamlSerializerApiValidation_Node : YamlSerializerApiValidation
    {
        public YamlSerializerApiValidation_Node() : base(YamlSerializerWrapper.NodeSerializer) { }
    }

    public class YamlSerializerApiValidation_Pipe : YamlSerializerApiValidation
    {
        public YamlSerializerApiValidation_Pipe() : base(YamlSerializerWrapper.AsyncPipeSerializer) { }
    }

    public class YamlSerializerApiValidation_PipeWithSmallBuffer : YamlSerializerApiValidation
    {
        public YamlSerializerApiValidation_PipeWithSmallBuffer() : base(YamlSerializerWrapper.AsyncPipeSerializerWithSmallBuffer) { }
    }
}

/// <summary>
/// Verifies input values for public YamlSerializer methods.
/// </summary>
public abstract class YamlSerializerApiValidation
{
    private class MyPoco { }

    internal partial class MyDummyContext : YamlSerializerContext
    {
        public MyDummyContext() : base(new YamlSerializerOptions()) { }
        public MyDummyContext(YamlSerializerOptions options) : base(options) { }
        public override YamlTypeInfo? GetTypeInfo(Type type) => throw new NotImplementedException();
        protected override YamlSerializerOptions? GeneratedSerializerOptions => null;
    }

    private YamlTypeInfo<MyPoco> myDummyTypeInfo = GetTypeInfo();

    private YamlSerializerWrapper Serializer { get; }

    public YamlSerializerApiValidation(YamlSerializerWrapper serializer)
    {
        Serializer = serializer;
    }

    private static YamlTypeInfo<MyPoco> GetTypeInfo()
    {
        YamlObjectInfoValues<MyPoco> objectInfo = new()
        {
            ObjectCreator = static () => throw new NotImplementedException(),
            SerializeHandler = (Utf8YamlWriter writer, MyPoco value) => throw new NotImplementedException(),
            PropertyMetadataInitializer = (ctx) => new YamlPropertyInfo[0],
        };

        var options = new YamlSerializerOptions();
        new MyDummyContext(options);

        return YamlMetadataServices.CreateObjectInfo<MyPoco>(options, objectInfo);
    }

    [Fact]
    public async Task DeserializeNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await Serializer.DeserializeWrapper<MyPoco>(yaml: "{}", yamlTypeInfo: null));
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await Serializer.DeserializeWrapper(yaml: "{}", type: null));
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await Serializer.DeserializeWrapper(yaml: "{}", type: typeof(MyPoco), context: null));
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await Serializer.DeserializeWrapper(yaml: "{}", type: null, context: new MyDummyContext()));

        if (!Serializer.SupportsNullValueOnDeserialize)
        {
            await Assert.ThrowsAsync<ArgumentNullException>(async () => await Serializer.DeserializeWrapper(yaml: null, type: typeof(MyPoco), context: new MyDummyContext()));
            await Assert.ThrowsAsync<ArgumentNullException>(async () => await Serializer.DeserializeWrapper(yaml: null, type: typeof(MyPoco)));
            await Assert.ThrowsAsync<ArgumentNullException>(async () => await Serializer.DeserializeWrapper<MyPoco>(yaml: null));
            await Assert.ThrowsAsync<ArgumentNullException>(async () => await Serializer.DeserializeWrapper<MyPoco>(yaml: null, yamlTypeInfo: myDummyTypeInfo));
        }
    }

    [Fact]
    public async Task SerializeNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await Serializer.SerializeWrapper<MyPoco>(value: new MyPoco(), yamlTypeInfo: null));
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await Serializer.SerializeWrapper(value: new MyPoco(), inputType: null));
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await Serializer.SerializeWrapper(value: new MyPoco(), inputType: typeof(MyPoco), context: null));
        await Assert.ThrowsAsync<ArgumentNullException>(async () => await Serializer.SerializeWrapper(value: new MyPoco(), inputType: null, context: new MyDummyContext()));
    }
}
