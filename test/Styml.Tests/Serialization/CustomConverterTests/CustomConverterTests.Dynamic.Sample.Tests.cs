// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Tests;
using System.Text.Yaml.Serialization.Samples;
using Xunit;
using static System.Text.Yaml.Serialization.Samples.YamlSerializerExtensions;

namespace System.Text.Yaml.Serialization.Tests
{
    public static partial class CustomConverterTests
    {
        private const string ExpectedDomYaml = "{\"MyString\":\"Hello!\",\"MyNull\":null,\"MyBoolean\":false,\"MyArray\":[2,3,42]," +
            "\"MyInt\":43,\"MyDateTime\":\"2020-07-08T00:00:00\",\"MyGuid\":\"ed957609-cdfe-412f-88c1-02daca1b4f51\"," +
            "\"MyObject\":{\"MyString\":\"Hello!!\"},\"Child\":{\"ChildProp\":1}}";

        private enum MyCustomEnum
        {
            Default = 0,
            FortyTwo = 42,
            Hello = 77
        }

        [ConditionalFact(typeof(PlatformDetection), nameof(PlatformDetection.IsReflectionEmitSupported))]
        public static void VerifyPrimitives()
        {
            var options = new YamlSerializerOptions();
            options.EnableDynamicTypes();
            options.Converters.Add(new YamlStringEnumConverter());

            dynamic obj = YamlSerializer.Deserialize<dynamic>(DynamicTests.Yaml, options);
            Assert.IsType<YamlDynamicObject>(obj);

            // YamlDynamicString has an implicit cast to string.
            Assert.IsType<YamlDynamicString>(obj.MyString);
            Assert.Equal("Hello", obj.MyString);

            // Verify other string-based types.
            Assert.Equal(MyCustomEnum.Hello, (MyCustomEnum)obj.MyString);
            Assert.Equal(DynamicTests.MyDateTime, (DateTime)obj.MyDateTime);
            Assert.Equal(DynamicTests.MyGuid, (Guid)obj.MyGuid);

            // YamlDynamicBoolean has an implicit cast to bool.
            Assert.IsType<YamlDynamicBoolean>(obj.MyBoolean);
            Assert.True(obj.MyBoolean);

            // Numbers must specify the type through a cast or assignment.
            Assert.IsType<YamlDynamicNumber>(obj.MyInt);
            Assert.ThrowsAny<Exception>(() => obj.MyInt == 42L);
            Assert.Equal(42L, (long)obj.MyInt);
            Assert.Equal((byte)42, (byte)obj.MyInt);

            // Verify int-based Enum support through "unknown number type" fallback.
            Assert.Equal(MyCustomEnum.FortyTwo, (MyCustomEnum)obj.MyInt);

            // Verify floating point.
            obj = YamlSerializer.Deserialize<dynamic>("4.2", options);
            Assert.IsType<YamlDynamicNumber>(obj);

            double dbl = (double)obj;
#if !NET
            string temp = dbl.ToString(System.Globalization.CultureInfo.InvariantCulture);
            // The reader uses "G17" format which causes temp to be 4.2000000000000002 in this case.
            dbl = double.Parse(temp, System.Globalization.CultureInfo.InvariantCulture);
#endif
            Assert.Equal(4.2, dbl);
        }

        [ConditionalFact(typeof(PlatformDetection), nameof(PlatformDetection.IsReflectionEmitSupported))]
        public static void VerifyArray()
        {
            var options = new YamlSerializerOptions();
            options.EnableDynamicTypes();
            options.Converters.Add(new YamlStringEnumConverter());

            dynamic obj = YamlSerializer.Deserialize<dynamic>(DynamicTests.Yaml, options);
            Assert.IsType<YamlDynamicObject>(obj);

            Assert.IsType<YamlDynamicObject>(obj);
            Assert.IsType<YamlDynamicArray>(obj.MyArray);

            Assert.Equal(2, obj.MyArray.Count);
            Assert.Equal(1, (int)obj.MyArray[0]);
            Assert.Equal(2, (int)obj.MyArray[1]);

            // Ensure we can enumerate
            int count = 0;
            foreach (long value in obj.MyArray)
            {
                count++;
            }
            Assert.Equal(2, count);

            // Ensure we can mutate through indexers
            obj.MyArray[0] = 10;
            Assert.Equal(10, (int)obj.MyArray[0]);
        }

        [ConditionalFact(typeof(PlatformDetection), nameof(PlatformDetection.IsReflectionEmitSupported))]
        [Trait("category", "jsonoutput")]
        public static void YamlDynamicTypes_Serialize()
        {
            var options = new YamlSerializerOptions();
            options.EnableDynamicTypes();

            // Guid (string)
            string GuidYaml = $"{DynamicTests.MyGuid:D}";
            string GuidYamlWithQuotes = $"\"{GuidYaml}\"";

            dynamic dynamicString = new YamlDynamicString(GuidYaml, options);
            Assert.Equal(DynamicTests.MyGuid, (Guid)dynamicString);
            string yaml = YamlSerializer.Serialize(dynamicString, options);
            Assert.Equal(GuidYamlWithQuotes, yaml);

            // char (string)
            dynamicString = new YamlDynamicString("a", options);
            Assert.Equal('a', (char)dynamicString);
            yaml = YamlSerializer.Serialize(dynamicString, options);
            Assert.Equal("\"a\"", yaml);

            // Number (YamlElement)
            using (YamlDocument doc = YamlDocument.Parse($"{decimal.MaxValue}"))
            {
                dynamic dynamicNumber = new YamlDynamicNumber(doc.RootElement, options);
                Assert.Equal<decimal>(decimal.MaxValue, (decimal)dynamicNumber);
                yaml = YamlSerializer.Serialize(dynamicNumber, options);
                Assert.Equal(decimal.MaxValue.ToString(), yaml);
            }

            // Boolean
            dynamic dynamicBool = new YamlDynamicBoolean(true, options);
            Assert.True(dynamicBool);
            yaml = YamlSerializer.Serialize(dynamicBool, options);
            Assert.Equal("true", yaml);

            // Array
            dynamic arr = new YamlDynamicArray(options);
            arr.Add(1);
            arr.Add(2);
            yaml = YamlSerializer.Serialize(arr, options);
            Assert.Equal("[1,2]", yaml);

            // Object
            dynamic dynamicObject = new YamlDynamicObject(options);
            dynamicObject["One"] = 1;
            dynamicObject["Two"] = 2;

            yaml = YamlSerializer.Serialize(dynamicObject, options);
            YamlTestHelper.AssertYamlEqual("{\"One\":1,\"Two\":2}", yaml);
        }

        [ConditionalFact(typeof(PlatformDetection), nameof(PlatformDetection.IsReflectionEmitSupported))]
        public static void YamlDynamicTypes_Deserialize()
        {
            var options = new YamlSerializerOptions();
            options.EnableDynamicTypes();

            YamlSerializer.Deserialize<YamlDynamicType>("{}", options);
            YamlSerializer.Deserialize<YamlDynamicArray>("[]", options);
            YamlSerializer.Deserialize<YamlDynamicBoolean>("true", options);
            YamlSerializer.Deserialize<YamlDynamicNumber>("0", options);
            YamlSerializer.Deserialize<YamlDynamicNumber>("1.2", options);
            YamlSerializer.Deserialize<YamlDynamicObject>("{}", options);
            YamlSerializer.Deserialize<YamlDynamicString>("\"str\"", options);
        }

        [ConditionalFact(typeof(PlatformDetection), nameof(PlatformDetection.IsReflectionEmitSupported))]
        public static void YamlDynamicTypes_Deserialize_AsObject()
        {
            var options = new YamlSerializerOptions();
            options.EnableDynamicTypes();

            Assert.IsType<YamlDynamicArray>(YamlSerializer.Deserialize<object>("[]", options));
            Assert.IsType<YamlDynamicBoolean>(YamlSerializer.Deserialize<object>("true", options));
            Assert.IsType<YamlDynamicNumber>(YamlSerializer.Deserialize<object>("0", options));
            Assert.IsType<YamlDynamicNumber>(YamlSerializer.Deserialize<object>("1.2", options));
            Assert.IsType<YamlDynamicObject>(YamlSerializer.Deserialize<object>("{}", options));
            Assert.IsType<YamlDynamicString>(YamlSerializer.Deserialize<object>("\"str\"", options));
        }

        /// <summary>
        /// Use a mutable DOM with the 'dynamic' keyword.
        /// </summary>
        [ConditionalFact(typeof(PlatformDetection), nameof(PlatformDetection.IsReflectionEmitSupported))]
        public static void VerifyMutableDom_UsingDynamicKeyword()
        {
            var options = new YamlSerializerOptions();
            options.EnableDynamicTypes();

            dynamic obj = YamlSerializer.Deserialize<dynamic>(DynamicTests.Yaml, options);
            Assert.IsType<YamlDynamicObject>(obj);

            // Change some primitives.
            obj.MyString = "Hello!";
            obj.MyBoolean = false;
            obj.MyInt = 43;

            // Add nested objects.
            // Use YamlDynamicObject; ExpandoObject should not be used since it doesn't have the same semantics including
            // null handling and case-sensitivity that respects YamlSerializerOptions.PropertyNameCaseInsensitive.
            dynamic myObject = new YamlDynamicObject(options);
            myObject.MyString = "Hello!!";
            obj.MyObject = myObject;

            dynamic child = new YamlDynamicObject(options);
            child.ChildProp = 1;
            obj.Child = child;

            // Modify number elements.
            dynamic arr = obj["MyArray"];
            arr[0] = (int)arr[0] + 1;
            arr[1] = (int)arr[1] + 1;

            // Add an element.
            arr.Add(42);

            string yaml = YamlSerializer.Serialize(obj, options);
            YamlTestHelper.AssertYamlEqual(ExpectedDomYaml, yaml);
        }

        /// <summary>
        /// Use a mutable DOM without the 'dynamic' keyword.
        /// </summary>
        [ConditionalFact(typeof(PlatformDetection), nameof(PlatformDetection.IsReflectionEmitSupported))]
        public static void VerifyMutableDom_WithoutUsingDynamicKeyword()
        {
            var options = new YamlSerializerOptions();
            options.EnableDynamicTypes();

            YamlDynamicObject obj = (YamlDynamicObject)YamlSerializer.Deserialize<object>(DynamicTests.Yaml, options);

            // Change some primitives.
            obj["MyString"] = "Hello!";
            obj["MyBoolean"] = false;
            obj["MyInt"] = 43;

            // Add nested objects.
            obj["MyObject"] = new YamlDynamicObject(options)
            {
                ["MyString"] = "Hello!!"
            };

            obj["Child"] = new YamlDynamicObject(options)
            {
                ["ChildProp"] = 1
            };

            // Modify number elements.
            var arr = (YamlDynamicArray)obj["MyArray"];
            var elem = (YamlDynamicNumber)arr[0];
            elem.SetValue(elem.GetValue<int>() + 1);
            elem = (YamlDynamicNumber)arr[1];
            elem.SetValue(elem.GetValue<int>() + 1);

            // Add an element.
            arr.Add(42);

            string yaml = YamlSerializer.Serialize(obj, options);
            YamlTestHelper.AssertYamlEqual(ExpectedDomYaml, yaml);
        }

        /// <summary>
        /// Use a mutable DOM without the 'dynamic' keyword and use round-trippable values
        /// meaning the 'YamlDynamicType' values are used instead of raw primitives.
        /// </summary>
        [ConditionalFact(typeof(PlatformDetection), nameof(PlatformDetection.IsReflectionEmitSupported))]
        public static void VerifyMutableDom_WithoutUsingDynamicKeyword_YamlDynamicType()
        {
            var options = new YamlSerializerOptions();
            options.EnableDynamicTypes();

            YamlDynamicObject obj = (YamlDynamicObject)YamlSerializer.Deserialize<object>(DynamicTests.Yaml, options);
            Verify();

            // Verify the values are round-trippable.
            ((YamlDynamicArray)obj["MyArray"]).RemoveAt(2);
            Verify();

            void Verify()
            {
                // Change some primitives.
                ((YamlDynamicType)obj["MyString"]).SetValue("Hello!");
                ((YamlDynamicType)obj["MyBoolean"]).SetValue(false);
                ((YamlDynamicType)obj["MyInt"]).SetValue(43);

                // Add nested objects.
                obj["MyObject"] = new YamlDynamicObject(options)
                {
                    ["MyString"] = new YamlDynamicString("Hello!!", options)
                };
                obj["Child"] = new YamlDynamicObject(options)
                {
                    ["ChildProp"] = new YamlDynamicNumber(1, options)
                };

                // Modify number elements.
                var arr = (YamlDynamicArray)obj["MyArray"];
                ((YamlDynamicType)arr[0]).SetValue(2);
                ((YamlDynamicType)arr[1]).SetValue(3);

                // Add an element.
                arr.Add(new YamlDynamicNumber(42, options));

                string yaml = YamlSerializer.Serialize(obj, options);
                YamlTestHelper.AssertYamlEqual(ExpectedDomYaml, yaml);
            }
        }

        [ConditionalFact(typeof(PlatformDetection), nameof(PlatformDetection.IsReflectionEmitSupported))]
        public static void DynamicObject_MissingProperty()
        {
            var options = new YamlSerializerOptions();
            options.EnableDynamicTypes();
            dynamic obj = YamlSerializer.Deserialize<dynamic>("{}", options);

            // We return null here; ExpandoObject throws for missing properties.
            Assert.Null(obj.NonExistingProperty);
        }

        [ConditionalFact(typeof(PlatformDetection), nameof(PlatformDetection.IsReflectionEmitSupported))]
        public static void DynamicObject_CaseSensitivity()
        {
            var options = new YamlSerializerOptions();
            options.EnableDynamicTypes();
            dynamic obj = YamlSerializer.Deserialize<dynamic>("{\"MyProperty\":42}", options);

            Assert.Equal(42, (int)obj.MyProperty);
            Assert.Null(obj.myproperty);
            Assert.Null(obj.MYPROPERTY);

            options = new YamlSerializerOptions();
            options.EnableDynamicTypes();
            options.PropertyNameCaseInsensitive = true;
            obj = YamlSerializer.Deserialize<dynamic>("{\"MyProperty\":42}", options);

            Assert.Equal(42, (int)obj.MyProperty);
            Assert.Equal(42, (int)obj.myproperty);
            Assert.Equal(42, (int)obj.MYPROPERTY);
        }

        [ConditionalFact(typeof(PlatformDetection), nameof(PlatformDetection.IsReflectionEmitSupported))]
        public static void NamingPoliciesAreNotUsed()
        {
            const string Yaml = "{\"myProperty\":42}";

            var options = new YamlSerializerOptions();
            options.EnableDynamicTypes();
            options.PropertyNamingPolicy = YamlNamingPolicy.SnakeCaseLower;

            dynamic obj = YamlSerializer.Deserialize<dynamic>(Yaml, options);

            string yaml = YamlSerializer.Serialize(obj, options);
            YamlTestHelper.AssertYamlEqual(Yaml, yaml);
        }

        [ConditionalFact(typeof(PlatformDetection), nameof(PlatformDetection.IsReflectionEmitSupported))]
        public static void NullHandling()
        {
            var options = new YamlSerializerOptions();
            options.EnableDynamicTypes();

            dynamic obj = YamlSerializer.Deserialize<dynamic>("null", options);
            Assert.Null(obj);
        }

        [ConditionalFact(typeof(PlatformDetection), nameof(PlatformDetection.IsReflectionEmitSupported))]
        public static void QuotedNumbers_Deserialize()
        {
            var options = new YamlSerializerOptions();
            options.EnableDynamicTypes();
            options.NumberHandling = YamlNumberHandling.AllowReadingFromString |
                YamlNumberHandling.AllowNamedFloatingPointLiterals;

            dynamic obj = YamlSerializer.Deserialize<dynamic>("\"42\"", options);
            Assert.IsType<YamlDynamicString>(obj);
            Assert.Equal(42, (int)obj);

            obj = YamlSerializer.Deserialize<dynamic>("\"NaN\"", options);
            Assert.IsType<YamlDynamicString>(obj);
            using (new ThreadCultureChange(CultureInfo.InvariantCulture))
            {
                Assert.Equal(double.NaN, (double)obj);
                Assert.Equal(float.NaN, (float)obj);
            }
        }

        [ConditionalFact(typeof(PlatformDetection), nameof(PlatformDetection.IsReflectionEmitSupported))]
        [Trait("category", "jsonoutput")]
        public static void QuotedNumbers_Serialize()
        {
            var options = new YamlSerializerOptions();
            options.EnableDynamicTypes();
            options.NumberHandling = YamlNumberHandling.WriteAsString;

            dynamic obj = 42L;
            string yaml = YamlSerializer.Serialize<dynamic>(obj, options);
            Assert.Equal("\"42\"", yaml);

            obj = double.NaN;
            yaml = YamlSerializer.Serialize<dynamic>(obj, options);
            Assert.Equal("\"NaN\"", yaml);
        }
    }
}
