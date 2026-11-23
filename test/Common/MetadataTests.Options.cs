// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Text.Yaml.Serialization.Metadata;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public abstract partial class MetadataTests
    {
        [Fact]
        public void YamlSerializerContextCtor()
        {
            // Pass no options.
            MyYamlContext context = new();
            YamlSerializerOptions options = context.Options; // New options instance created and bound at this point.
            Assert.NotNull(options);

            // Pass options.
            options = new YamlSerializerOptions();
            context = new MyYamlContext(options); // Provided options are bound at this point.
            Assert.Same(options, context.Options);
        }

        private static void CauseInvalidOperationException(Action action)
        {
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(action);
            string exAsStr = ex.ToString();
            Assert.Contains("YamlSerializerOptions", exAsStr);
            Assert.Contains("YamlSerializerContext", exAsStr);
        }

        [Fact]
        public void AlreadyBindedOptions()
        {
            // Bind the options.
            YamlSerializerOptions options = new();
            new MyYamlContext(options);

            // Attempt to bind the instance again.
            Assert.Throws<InvalidOperationException>(() => new MyYamlContext(options));
        }

        [Fact]
        public void OptionsImmutableAfterBinding()
        {
            // Bind via context ctor
            YamlSerializerOptions options = new YamlSerializerOptions();
            MyYamlContext context = new MyYamlContext(options);
            Assert.Same(options, context.Options);
            CauseInvalidOperationException(() => options.PropertyNamingPolicy = YamlNamingPolicy.CamelCase);
        }

        [Fact]
        public void PassingImmutableOptionsThrowsException()
        {
            YamlSerializerOptions options = Serializer.DefaultOptions;
            Assert.True(options.IsReadOnly);

            Assert.Throws<InvalidOperationException>(() => new MyYamlContext(options));
        }

        [Fact]
        public void PassingWrongOptionsInstanceToResolverThrowsException()
        {
            YamlSerializerOptions contextOptions = new();
            IYamlTypeInfoResolver context = new EmptyContext(contextOptions);

            Assert.IsAssignableFrom<YamlTypeInfo<int>>(context.GetTypeInfo(typeof(int), contextOptions));
            Assert.IsAssignableFrom<YamlTypeInfo<int>>(context.GetTypeInfo(typeof(int), null));
            Assert.Throws<InvalidOperationException>(() => context.GetTypeInfo(typeof(int), Serializer.DefaultOptions));
        }

        private class MyYamlContext : YamlSerializerContext
        {
            public MyYamlContext() : base(null) { }

            public MyYamlContext(YamlSerializerOptions options) : base(options) { }

            public override YamlTypeInfo? GetTypeInfo(Type type) => throw new NotImplementedException();

            protected override YamlSerializerOptions? GeneratedSerializerOptions => null;
        }

        private class MyYamlContextThatSetsOptionsInParameterlessCtor : YamlSerializerContext
        {
            public MyYamlContextThatSetsOptionsInParameterlessCtor() : base(new YamlSerializerOptions()) { }
            public override YamlTypeInfo? GetTypeInfo(Type type) => throw new NotImplementedException();
            protected override YamlSerializerOptions? GeneratedSerializerOptions => null;
        }

        private class EmptyContext : YamlSerializerContext
        {
            public EmptyContext(YamlSerializerOptions options) : base(options) { }
            protected override YamlSerializerOptions? GeneratedSerializerOptions => null;
            public override YamlTypeInfo? GetTypeInfo(Type type)
                => type == typeof(int)
                    ? YamlMetadataServices.CreateValueInfo<int>(Options, YamlMetadataServices.Int32Converter)
                    : null;
        }
    }
}
