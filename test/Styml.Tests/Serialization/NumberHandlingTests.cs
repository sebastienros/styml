// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public sealed partial class NumberHandlingTestsDynamic : NumberHandlingTests
    {
        public NumberHandlingTestsDynamic() : base(YamlSerializerWrapper.StringSerializer) { }
    }

    public class NumberHandlingTests_AsyncStreamOverload : NumberHandlingTests_OverloadSpecific
    {
        public NumberHandlingTests_AsyncStreamOverload() : base(YamlSerializerWrapper.AsyncStreamSerializer) { }
    }

    public class NumberHandlingTests_SyncStreamOverload : NumberHandlingTests_OverloadSpecific
    {
        public NumberHandlingTests_SyncStreamOverload() : base(YamlSerializerWrapper.SyncStreamSerializer) { }
    }

    public class NumberHandlingTests_SyncOverload : NumberHandlingTests_OverloadSpecific
    {
        public NumberHandlingTests_SyncOverload() : base(YamlSerializerWrapper.StringSerializer) { }
    }

    public class NumberHandlingTests_Document : NumberHandlingTests_OverloadSpecific
    {
        public NumberHandlingTests_Document() : base(YamlSerializerWrapper.DocumentSerializer) { }
    }

    public class NumberHandlingTests_Element : NumberHandlingTests_OverloadSpecific
    {
        public NumberHandlingTests_Element() : base(YamlSerializerWrapper.ElementSerializer) { }
    }

    public class NumberHandlingTests_Node : NumberHandlingTests_OverloadSpecific
    {
        public NumberHandlingTests_Node() : base(YamlSerializerWrapper.NodeSerializer) { }
    }

    public class NumberHandlingTests_Pipe : NumberHandlingTests_OverloadSpecific
    {
        public NumberHandlingTests_Pipe() : base(YamlSerializerWrapper.AsyncPipeSerializer) { }
    }

    public class NumberHandlingTests_PipeWithSmallBuffer : NumberHandlingTests_OverloadSpecific
    {
        public NumberHandlingTests_PipeWithSmallBuffer() : base(YamlSerializerWrapper.AsyncPipeSerializerWithSmallBuffer) { }
    }

    public abstract class NumberHandlingTests_OverloadSpecific
    {
        private YamlSerializerWrapper Serializer { get; }

        public NumberHandlingTests_OverloadSpecific(YamlSerializerWrapper deserializer)
        {
            Serializer = deserializer;
        }

        [Theory]
        [MemberData(nameof(NumberHandling_ForPropsReadAfter_DeserializingCtorParams_TestData))]
        public async Task NumberHandling_ForPropsReadAfter_DeserializingCtorParams(string yaml)
        {
            YamlSerializerOptions options = new YamlSerializerOptions
            {
                NumberHandling = YamlNumberHandling.AllowReadingFromString | YamlNumberHandling.WriteAsString,
                PropertyNamingPolicy = YamlNamingPolicy.CamelCase,
            };

            Result result = await Serializer.DeserializeWrapper<Result>(yaml, options);
            YamlTestHelper.AssertYamlEqual(yaml, await Serializer.SerializeWrapper(result, options));
        }

        public static IEnumerable<object[]> NumberHandling_ForPropsReadAfter_DeserializingCtorParams_TestData()
        {
            yield return new object[]
            {
                """
                        {
                                            "album": {
                                                "userPlayCount": "123",
                                                "name": "the name of the album",
                                                "artist": "the name of the artist",
                                                "wiki": {
                                                    "summary": "a summary of the album"
                                                }
                                            }
                                        }
                    """
            };

            yield return new object[]
            {
                """
                        {
                                            "album": {
                                                "name": "the name of the album",
                                                "userPlayCount": "123",
                                                "artist": "the name of the artist",
                                                "wiki": {
                                                    "summary": "a summary of the album"
                                                }
                                            }
                                        }
                    """
            };

            yield return new object[]
            {
                """
                        {
                                            "album": {
                                                "name": "the name of the album",
                                                "artist": "the name of the artist",
                                                "userPlayCount": "123",
                                                "wiki": {
                                                    "summary": "a summary of the album"
                                                }
                                            }
                                        }
                    """
            };

            yield return new object[]
            {
                """
                        {
                                            "album": {
                                                "name": "the name of the album",
                                                "artist": "the name of the artist",
                                                "wiki": {
                                                    "summary": "a summary of the album"
                                                },
                                                "userPlayCount": "123"
                                            }
                                        }
                    """
            };
        }

        public class Result
        {
            public Album Album { get; init; }
        }

        public class Album
        {
            public Album(string name, string artist)
            {
                Name = name;
                Artist = artist;
            }

            public string Name { get; init; }
            public string Artist { get; init; }

            public long? userPlayCount { get; init; }

            [YamlExtensionData]
            public Dictionary<string, YamlElement> ExtensionData { get; set; }
        }
    }
}
