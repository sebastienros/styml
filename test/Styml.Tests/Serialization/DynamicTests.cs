// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public static class DynamicTests
    {
        public const string Yaml =
            "{\"MyString\":\"Hello\",\"MyNull\":null,\"MyBoolean\":true,\"MyArray\":[1,2],\"MyInt\":42,\"MyDateTime\":\"2020-07-08T00:00:00\",\"MyGuid\":\"ed957609-cdfe-412f-88c1-02daca1b4f51\",\"MyObject\":{\"MyString\":\"World\"}}";
        public static DateTime MyDateTime => new DateTime(2020, 7, 8);
        public static Guid MyGuid => new Guid("ed957609-cdfe-412f-88c1-02daca1b4f51");

        internal static ExpandoObject GetExpandoObject()
        {
            dynamic myDynamicChild = new ExpandoObject();
            myDynamicChild.MyString = "World";

            dynamic myDynamic = new ExpandoObject();
            myDynamic.MyString = "Hello";
            myDynamic.MyNull = null;
            myDynamic.MyBoolean = true;
            myDynamic.MyArray = new List<int>() { 1, 2 };
            myDynamic.MyInt = 42;
            myDynamic.MyDateTime = MyDateTime;
            myDynamic.MyGuid = MyGuid;
            myDynamic.MyObject = myDynamicChild;

            // Verify basic dynamic support.
            int c = myDynamic.MyInt;
            Assert.Equal(42, c);

            return myDynamic;
        }

        [ConditionalFact(typeof(PlatformDetection), nameof(PlatformDetection.IsReflectionEmitSupported))]
        public static void DynamicKeyword()
        {
            dynamic myDynamic = GetExpandoObject();

            // STJ serializes ExpandoObject as IDictionary<string, object>;
            // there is no custom converter for ExpandoObject.
            string yaml = YamlSerializer.Serialize<dynamic>(myDynamic);
            YamlTestHelper.AssertYamlEqual(Yaml, yaml);

            dynamic d = YamlSerializer.Deserialize<dynamic>(yaml);

            try
            {
                // We will get an exception here if we try to access a dynamic property since 'object' is deserialized
                // as a YamlElement and not an ExpandoObject.
                int c = d.MyInt;
                Assert.Fail("Should have thrown Exception!");
            }
            catch (Microsoft.CSharp.RuntimeBinder.RuntimeBinderException) { }

            Assert.IsType<YamlElement>(d);
            YamlElement elem = (YamlElement)d;

            VerifyPrimitives();
            VerifyObject();
            VerifyArray();

            // Re-serialize
            yaml = YamlSerializer.Serialize<object>(elem);
            YamlTestHelper.AssertYamlEqual(Yaml, yaml);

            yaml = YamlSerializer.Serialize<dynamic>(elem);
            YamlTestHelper.AssertYamlEqual(Yaml, yaml);

            yaml = YamlSerializer.Serialize(elem);
            YamlTestHelper.AssertYamlEqual(Yaml, yaml);

            void VerifyPrimitives()
            {
                Assert.Equal("Hello", elem.GetProperty("MyString").GetString());
                Assert.True(elem.GetProperty("MyBoolean").GetBoolean());
                Assert.Equal(42, elem.GetProperty("MyInt").GetInt32());
                Assert.Equal(MyDateTime, elem.GetProperty("MyDateTime").GetDateTime());
                Assert.Equal(MyGuid, elem.GetProperty("MyGuid").GetGuid());
            }

            void VerifyObject()
            {
                Assert.Equal("World", elem.GetProperty("MyObject").GetProperty("MyString").GetString());
            }

            void VerifyArray()
            {
                YamlElement.ArrayEnumerator enumerator = elem.GetProperty("MyArray").EnumerateArray();
                Assert.Equal(2, enumerator.Count());
                enumerator.MoveNext();
                Assert.Equal(1, enumerator.Current.GetInt32());
                enumerator.MoveNext();
                Assert.Equal(2, enumerator.Current.GetInt32());
            }
        }

        [ConditionalFact(typeof(PlatformDetection), nameof(PlatformDetection.IsReflectionEmitSupported))]
        public static void ExpandoObject()
        {
            ExpandoObject expando = YamlSerializer.Deserialize<ExpandoObject>(Yaml);
            Assert.Equal(8, ((IDictionary<string, object>)expando).Keys.Count);

            dynamic obj = expando;

            VerifyPrimitives();
            VerifyObject();
            VerifyArray();

            // Re-serialize
            string yaml = YamlSerializer.Serialize<ExpandoObject>(obj);
            YamlTestHelper.AssertYamlEqual(Yaml, yaml);

            yaml = YamlSerializer.Serialize<dynamic>(obj);
            YamlTestHelper.AssertYamlEqual(Yaml, yaml);

            yaml = YamlSerializer.Serialize(obj);
            YamlTestHelper.AssertYamlEqual(Yaml, yaml);

            void VerifyPrimitives()
            {
                YamlElement yamlElement = obj.MyString;
                Assert.Equal("Hello", yamlElement.GetString());

                yamlElement = obj.MyBoolean;
                Assert.True(yamlElement.GetBoolean());

                yamlElement = obj.MyInt;
                Assert.Equal(42, yamlElement.GetInt32());

                yamlElement = obj.MyDateTime;
                Assert.Equal(MyDateTime, yamlElement.GetDateTime());

                yamlElement = obj.MyGuid;
                Assert.Equal(MyGuid, yamlElement.GetGuid());
            }

            void VerifyObject()
            {
                YamlElement yamlElement = obj.MyObject;
                // Here we access a property on a nested object and must use YamlElement (not a dynamic property).
                Assert.Equal("World", yamlElement.GetProperty("MyString").GetString());
            }

            void VerifyArray()
            {
                YamlElement yamlElement = obj.MyArray;
                Assert.Equal(2, yamlElement.EnumerateArray().Count());
            }
        }
    }
}
