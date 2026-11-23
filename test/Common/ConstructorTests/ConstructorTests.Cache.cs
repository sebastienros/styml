// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public abstract partial class ConstructorTests
    {
        [Fact]
        [OuterLoop]
        [ActiveIssue("https://github.com/dotnet/runtime/issues/80020", TestRuntimes.Mono)]
        public async Task MultipleThreadsLooping()
        {
            const int Iterations = 100;

            for (int i = 0; i < Iterations; i++)
            {
                await MultipleThreads();
            }
        }

        [Fact]
        public async Task MultipleThreads()
        {
            // Verify the test class has >32 properties since that is a threshold for using the fallback dictionary.
            Assert.True(typeof(ObjWCtorMixedParams).GetProperties(BindingFlags.Instance | BindingFlags.Public).Length > 32);

            async Task DeserializeObjectAsync(string yaml, Type type, YamlSerializerOptions options)
            {
                var obj = await Serializer.DeserializeWrapper(yaml, type, options);
                ((ITestClassWithParameterizedCtor)obj).Verify();
            }

            async Task DeserializeObjectMinimalAsync([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type type, YamlSerializerOptions options)
            {
                string yaml = (string)type.GetProperty("s_yaml_minimal").GetValue(null);
                var obj = await Serializer.DeserializeWrapper(yaml, type, options);
                ((ITestClassWithParameterizedCtor)obj).VerifyMinimal();
            };

            async Task DeserializeObjectFlippedAsync([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type type, YamlSerializerOptions options)
            {
                string yaml = (string)type.GetProperty("s_yaml_flipped").GetValue(null);
                await DeserializeObjectAsync(yaml, type, options);
            };

            async Task DeserializeObjectNormalAsync([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type type, YamlSerializerOptions options)
            {
                string yaml = (string)type.GetProperty("s_yaml").GetValue(null);
                await DeserializeObjectAsync(yaml, type, options);
            };

            async Task SerializeObject(Type type, YamlSerializerOptions options)
            {
                var obj = ObjWCtorMixedParams.GetInstance();
                await Serializer.SerializeWrapper(obj, options);
            };

            async Task RunTestAsync([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type type)
            {
                // Use local options to avoid obtaining already cached metadata from the default options.
                YamlSerializerOptions options = Serializer.CreateOptions();

                const int ThreadCount = 8;
                const int ConcurrentTestsCount = 4;
                Task[] tasks = new Task[ThreadCount * ConcurrentTestsCount];

                for (int i = 0; i < tasks.Length; i += ConcurrentTestsCount)
                {
                    // Create race condition to populate the sorted property cache with different json ordering.
                    tasks[i + 0] = Task.Run(() => DeserializeObjectMinimalAsync(type, options));
                    tasks[i + 1] = Task.Run(() => DeserializeObjectFlippedAsync(type, options));
                    tasks[i + 2] = Task.Run(() => DeserializeObjectNormalAsync(type, options));

                    // Ensure no exceptions on serialization
                    tasks[i + 3] = Task.Run(() => SerializeObject(type, options));
                };

                await Task.WhenAll(tasks);
            }

            await RunTestAsync(typeof(ObjWCtorMixedParams));
            await RunTestAsync(typeof(Person_Class));
            await RunTestAsync(typeof(Parameterized_Class_With_ComplexTuple));
        }

        [Fact]
        public async Task PropertyCacheWithMinInputsFirst()
        {
            // Use local options to avoid obtaining already cached metadata from the default options.
            YamlSerializerOptions options = Serializer.CreateOptions();

            string yaml = "{}";
            await Serializer.DeserializeWrapper<ObjWCtorMixedParams>(yaml, options);

            ObjWCtorMixedParams testObj = ObjWCtorMixedParams.GetInstance();
            testObj.Verify();

            yaml = await Serializer.SerializeWrapper(testObj, options);
            testObj = await Serializer.DeserializeWrapper<ObjWCtorMixedParams>(yaml, options);
            testObj.Verify();
        }

        [Fact]
        public async Task PropertyCacheWithMinInputsLast()
        {
            // Use local options to avoid obtaining already cached metadata from the default options.
            YamlSerializerOptions options = Serializer.CreateOptions();

            ObjWCtorMixedParams testObj = ObjWCtorMixedParams.GetInstance();
            testObj.Verify();

            string yaml = await Serializer.SerializeWrapper(testObj, options);
            testObj = await Serializer.DeserializeWrapper<ObjWCtorMixedParams>(yaml, options);
            testObj.Verify();

            yaml = "{}";
            await Serializer.DeserializeWrapper<ObjWCtorMixedParams>(yaml, options);
        }

        // Use a common options instance to encourage additional metadata collisions across types. Also since
        // this options is not the default options instance the tests will not use previously cached metadata.
        private YamlSerializerOptions s_options = new YamlSerializerOptions();

        [Fact]
        [SkipOnCoreClr("https://github.com/dotnet/runtime/issues/45464", ~RuntimeConfiguration.Release)]
        public async Task MultipleTypes()
        {
            async Task Serialize<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(object[] args)
            {
                Type type = typeof(T);

                T localTestObj = (T)Activator.CreateInstance(type, args);
                ((ITestClass)localTestObj).Initialize();
                ((ITestClass)localTestObj).Verify();
                string yaml = await Serializer.SerializeWrapper(localTestObj, s_options);
            };

            async Task DeserializeAsync<T>(string yaml)
            {
                ITestClass obj = (ITestClass)await Serializer.DeserializeWrapper<T>(yaml, s_options);
                obj.Verify();
            };

            async Task RunTestAsync<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(T testObj, object[] args)
            {
                // Get the test json with the default options to avoid cache pollution of DeserializeAsync() below.
                ((ITestClass)testObj).Initialize();
                ((ITestClass)testObj).Verify();
                string yaml = await Serializer.SerializeWrapper(testObj);

                const int ThreadCount = 12;
                const int ConcurrentTestsCount = 2;
                Task[] tasks = new Task[ThreadCount * ConcurrentTestsCount];

                for (int i = 0; i < tasks.Length; i += ConcurrentTestsCount)
                {
                    tasks[i + 0] = Task.Run(() => DeserializeAsync<T>(yaml));
                    tasks[i + 1] = Task.Run(() => Serialize<T>(args));
                };

                await Task.WhenAll(tasks);
            }

            await RunTestAsync<Point_2D>(new Point_2D(1, 2), new object[] { 1, 2 });
            await RunTestAsync(new Point_3D(1, 2, 3), new object[] { 1, 2, 3 });
            await RunTestAsync(new Point_2D_With_ExtData(1, 2), new object[] { 1, 2 });

            Guid id = Guid.Parse("270bb22b-4816-4bd9-9acd-8ec5b1a896d3");
            await RunTestAsync(new Parameterized_Person_Simple(id), new object[] { id });
            await RunTestAsync(new Point_MembersHave_YamlPropertyName(1, 2), new object[] { 1, 2 });
        }
    }
}
