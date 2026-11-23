//Licensed to the .NET Foundation under one or more agreements.
//The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Text.Yaml.Nodes;
using System.Text.Yaml.Serialization;
using System.Text.Yaml.Serialization.Tests;

namespace System.Text.Yaml.SourceGeneration.Tests
{
    public sealed partial class ExtensionDataTests_Metadata : ExtensionDataTests
    {
        public ExtensionDataTests_Metadata()
            : base(new StringSerializerWrapper(ExtensionDataTestsContext_Metadata.Default))
        {
        }

        [YamlSourceGenerationOptions(GenerationMode = YamlSourceGenerationMode.Metadata)]
        [YamlSerializable(typeof(ClassWithEmptyPropertyNameAndExtensionProperty))]
        [YamlSerializable(typeof(EmptyClassWithExtensionProperty))]
        [YamlSerializable(typeof(ClassWithExtensionProperty))]
        [YamlSerializable(typeof(ClassWithExtensionField))]
        [YamlSerializable(typeof(ClassWithExtensionPropertyAsObject))]
        [YamlSerializable(typeof(ClassWithIgnoredData))]
        [YamlSerializable(typeof(Dictionary<string, object>))]
        [YamlSerializable(typeof(Dictionary<string, YamlElement>))]
        [YamlSerializable(typeof(CustomOverflowDictionary<object>))]
        [YamlSerializable(typeof(CustomOverflowDictionary<YamlElement>))]
        [YamlSerializable(typeof(ExtensionDataTests))]
        [YamlSerializable(typeof(DictionaryOverflowConverter))]
        [YamlSerializable(typeof(YamlElementOverflowConverter))]
        [YamlSerializable(typeof(CustomObjectDictionaryOverflowConverter))]
        [YamlSerializable(typeof(CustomYamlElementDictionaryOverflowConverter))]
        [YamlSerializable(typeof(ClassWithExtensionDataWithAttributedConverter))]
        [YamlSerializable(typeof(ClassWithYamlElementExtensionDataWithAttributedConverter))]
        [YamlSerializable(typeof(ClassWithCustomElementExtensionDataWithAttributedConverter))]
        [YamlSerializable(typeof(ClassWithCustomYamlElementExtensionDataWithAttributedConverter))]
        [YamlSerializable(typeof(Dictionary<string, object>))]
        [YamlSerializable(typeof(Dictionary<string, YamlElement>))]
        [YamlSerializable(typeof(CustomOverflowDictionary<object>))]
        [YamlSerializable(typeof(CustomOverflowDictionary<YamlElement>))]
        [YamlSerializable(typeof(ClassWithExtensionDataWithAttributedConverter))]
        [YamlSerializable(typeof(ClassWithYamlElementExtensionDataWithAttributedConverter))]
        [YamlSerializable(typeof(ClassWithCustomElementExtensionDataWithAttributedConverter))]
        [YamlSerializable(typeof(ClassWithCustomYamlElementExtensionDataWithAttributedConverter))]
        [YamlSerializable(typeof(ClassWithExtensionPropertyAlreadyInstantiated))]
        [YamlSerializable(typeof(ClassWithExtensionPropertyAsObjectAndNameProperty))]
        [YamlSerializable(typeof(ClassWithExtensionPropertyAsYamlObject))]
        [YamlSerializable(typeof(ClassWithExtensionPropertyAsYamlElement))]
        [YamlSerializable(typeof(ClassWithReference))]
        [YamlSerializable(typeof(ParentClassWithObject))]
        [YamlSerializable(typeof(ParentClassWithYamlElement))]
        [YamlSerializable(typeof(ClassWithMultipleDictionaries))]
        [YamlSerializable(typeof(ClassWithEscapedProperty))]
        [YamlSerializable(typeof(ClassWithExtensionPropertyAsImmutable))]
        [YamlSerializable(typeof(ClassWithExtensionPropertyAsImmutableYamlElement))]
        [YamlSerializable(typeof(ClassWithExtensionPropertyPrivateConstructor))]
        [YamlSerializable(typeof(ClassWithExtensionPropertyPrivateConstructorYamlElement))]
        [YamlSerializable(typeof(ClassWithExtensionPropertyNoGenericParameters))]
        [YamlSerializable(typeof(ClassWithExtensionPropertyOneGenericParameter))]
        [YamlSerializable(typeof(ClassWithExtensionPropertyThreeGenericParameters))]
        [YamlSerializable(typeof(YamlElement))]
        [YamlSerializable(typeof(ClassWithExtensionData<YamlObject>))]
        [YamlSerializable(typeof(ClassWithExtensionData<Dictionary<string, YamlElement>>))]
        [YamlSerializable(typeof(ClassWithExtensionData<Dictionary<string, object>>))]
        [YamlSerializable(typeof(ClassWithExtensionData<CustomOverflowDictionary<object>>))]
        [YamlSerializable(typeof(ClassWithExtensionData<CustomOverflowDictionary<YamlElement>>))]
        [YamlSerializable(typeof(int))]
        [YamlSerializable(typeof(DummyObj))]
        [YamlSerializable(typeof(DummyStruct))]
        [YamlSerializable(typeof(ClassWithIReadOnlyDictionaryExtensionPropertyAsObject))]
        [YamlSerializable(typeof(ClassWithIReadOnlyDictionaryExtensionPropertyAsYamlElement))]
        [YamlSerializable(typeof(ClassWithIReadOnlyDictionaryExtensionPropertyAsObjectWithProperty))]
        [YamlSerializable(typeof(ClassWithIReadOnlyDictionaryExtensionPropertyAsYamlElementWithProperty))]
        [YamlSerializable(typeof(ClassWithIReadOnlyDictionaryAlreadyInstantiated))]
        [YamlSerializable(typeof(ClassWithIReadOnlyDictionaryYamlElementAlreadyInstantiated))]
        [YamlSerializable(typeof(ClassWithYamlObjectExtensionDataAndProperty))]
        internal sealed partial class ExtensionDataTestsContext_Metadata : YamlSerializerContext
        {
        }
    }

    public sealed partial class ExtensionDataTests_Default : ExtensionDataTests
    {
        public ExtensionDataTests_Default()
            : base(new StringSerializerWrapper(ExtensionDataTestsContext_Default.Default))
        {
        }

        [YamlSerializable(typeof(ClassWithEmptyPropertyNameAndExtensionProperty))]
        [YamlSerializable(typeof(EmptyClassWithExtensionProperty))]
        [YamlSerializable(typeof(ClassWithExtensionProperty))]
        [YamlSerializable(typeof(ClassWithExtensionField))]
        [YamlSerializable(typeof(ClassWithExtensionPropertyAsObject))]
        [YamlSerializable(typeof(ClassWithIgnoredData))]
        [YamlSerializable(typeof(Dictionary<string, object>))]
        [YamlSerializable(typeof(Dictionary<string, YamlElement>))]
        [YamlSerializable(typeof(CustomOverflowDictionary<object>))]
        [YamlSerializable(typeof(CustomOverflowDictionary<YamlElement>))]
        [YamlSerializable(typeof(ExtensionDataTests))]
        [YamlSerializable(typeof(DictionaryOverflowConverter))]
        [YamlSerializable(typeof(YamlElementOverflowConverter))]
        [YamlSerializable(typeof(CustomObjectDictionaryOverflowConverter))]
        [YamlSerializable(typeof(CustomYamlElementDictionaryOverflowConverter))]
        [YamlSerializable(typeof(ClassWithExtensionDataWithAttributedConverter))]
        [YamlSerializable(typeof(ClassWithYamlElementExtensionDataWithAttributedConverter))]
        [YamlSerializable(typeof(ClassWithCustomElementExtensionDataWithAttributedConverter))]
        [YamlSerializable(typeof(ClassWithCustomYamlElementExtensionDataWithAttributedConverter))]
        [YamlSerializable(typeof(Dictionary<string, object>))]
        [YamlSerializable(typeof(Dictionary<string, YamlElement>))]
        [YamlSerializable(typeof(CustomOverflowDictionary<object>))]
        [YamlSerializable(typeof(CustomOverflowDictionary<YamlElement>))]
        [YamlSerializable(typeof(ClassWithExtensionDataWithAttributedConverter))]
        [YamlSerializable(typeof(ClassWithYamlElementExtensionDataWithAttributedConverter))]
        [YamlSerializable(typeof(ClassWithCustomElementExtensionDataWithAttributedConverter))]
        [YamlSerializable(typeof(ClassWithCustomYamlElementExtensionDataWithAttributedConverter))]
        [YamlSerializable(typeof(ClassWithExtensionPropertyAlreadyInstantiated))]
        [YamlSerializable(typeof(ClassWithExtensionPropertyAsObjectAndNameProperty))]
        [YamlSerializable(typeof(ClassWithExtensionPropertyAsYamlObject))]
        [YamlSerializable(typeof(ClassWithExtensionPropertyAsYamlElement))]
        [YamlSerializable(typeof(ClassWithReference))]
        [YamlSerializable(typeof(ParentClassWithObject))]
        [YamlSerializable(typeof(ParentClassWithYamlElement))]
        [YamlSerializable(typeof(ClassWithMultipleDictionaries))]
        [YamlSerializable(typeof(ClassWithEscapedProperty))]
        [YamlSerializable(typeof(ClassWithExtensionPropertyAsImmutable))]
        [YamlSerializable(typeof(ClassWithExtensionPropertyAsImmutableYamlElement))]
        [YamlSerializable(typeof(ClassWithExtensionPropertyPrivateConstructor))]
        [YamlSerializable(typeof(ClassWithExtensionPropertyPrivateConstructorYamlElement))]
        [YamlSerializable(typeof(ClassWithExtensionPropertyNoGenericParameters))]
        [YamlSerializable(typeof(ClassWithExtensionPropertyOneGenericParameter))]
        [YamlSerializable(typeof(ClassWithExtensionPropertyThreeGenericParameters))]
        [YamlSerializable(typeof(YamlElement))]
        [YamlSerializable(typeof(ClassWithExtensionData<YamlObject>))]
        [YamlSerializable(typeof(ClassWithExtensionData<Dictionary<string, YamlElement>>))]
        [YamlSerializable(typeof(ClassWithExtensionData<Dictionary<string, object>>))]
        [YamlSerializable(typeof(ClassWithExtensionData<CustomOverflowDictionary<object>>))]
        [YamlSerializable(typeof(ClassWithExtensionData<CustomOverflowDictionary<YamlElement>>))]
        [YamlSerializable(typeof(int))]
        [YamlSerializable(typeof(DummyObj))]
        [YamlSerializable(typeof(DummyStruct))]
        [YamlSerializable(typeof(ClassWithIReadOnlyDictionaryExtensionPropertyAsObject))]
        [YamlSerializable(typeof(ClassWithIReadOnlyDictionaryExtensionPropertyAsYamlElement))]
        [YamlSerializable(typeof(ClassWithIReadOnlyDictionaryExtensionPropertyAsObjectWithProperty))]
        [YamlSerializable(typeof(ClassWithIReadOnlyDictionaryExtensionPropertyAsYamlElementWithProperty))]
        [YamlSerializable(typeof(ClassWithIReadOnlyDictionaryAlreadyInstantiated))]
        [YamlSerializable(typeof(ClassWithIReadOnlyDictionaryYamlElementAlreadyInstantiated))]
        [YamlSerializable(typeof(ClassWithYamlObjectExtensionDataAndProperty))]
        internal sealed partial class ExtensionDataTestsContext_Default : YamlSerializerContext
        {
        }
    }
}
