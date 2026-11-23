// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text.Yaml.Serialization;
using System.Text.Yaml.Serialization.Metadata;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xunit;

namespace System.Text.Yaml
{
    internal static partial class YamlTestHelper
    {
#if NET
        public const string DoubleFormatString = null;
        public const string SingleFormatString = null;
#else
        public const string DoubleFormatString = "G17";
        public const string SingleFormatString = "G9";
#endif

#if NET
        public static Half NextHalf(Random random)
        {
            double mantissa = (random.NextDouble() * 2.0) - 1.0;
            double exponent = Math.Pow(2.0, random.Next(-15, 16));
            Half value = (Half)(mantissa * exponent);
            return value;
        }
#endif

        public static float NextFloat(Random random)
        {
            double mantissa = (random.NextDouble() * 2.0) - 1.0;
            double exponent = Math.Pow(2.0, random.Next(-126, 128));
            float value = (float)(mantissa * exponent);
            return value;
        }

        public static double NextDouble(Random random, double minValue, double maxValue)
        {
            double value = random.NextDouble() * (maxValue - minValue) + minValue;
            return value;
        }

        public static decimal NextDecimal(Random random, double minValue, double maxValue)
        {
            double value = random.NextDouble() * (maxValue - minValue) + minValue;
            return (decimal)value;
        }

        public static void AssertYamlEqual(string expected, string actual)
        {
            using YamlDocument expectedDom = YamlDocument.Parse(expected);
            using YamlDocument actualDom = YamlDocument.Parse(actual);
            AssertYamlEqual(expectedDom.RootElement, actualDom.RootElement);
        }

        public static void AssertYamlEqual(YamlElement expected, YamlElement actual)
        {
            AssertYamlEqualCore(expected, actual, new());
        }

        private static void AssertYamlEqualCore(YamlElement expected, YamlElement actual, Stack<object> path)
        {
            YamlValueKind valueKind = expected.ValueKind;
            AssertTrue(passCondition: valueKind == actual.ValueKind);

            switch (valueKind)
            {
                case YamlValueKind.Object:
                    var expectedProperties = new List<string>();
                    foreach (YamlProperty property in expected.EnumerateObject())
                    {
                        expectedProperties.Add(property.Name);
                    }

                    var actualProperties = new List<string>();
                    foreach (YamlProperty property in actual.EnumerateObject())
                    {
                        actualProperties.Add(property.Name);
                    }

                    foreach (var property in expectedProperties.Except(actualProperties))
                    {
                        AssertTrue(passCondition: false, $"Property \"{property}\" missing from actual object.");
                    }

                    foreach (var property in actualProperties.Except(expectedProperties))
                    {
                        AssertTrue(passCondition: false, $"Actual object defines additional property \"{property}\".");
                    }

                    foreach (string name in expectedProperties)
                    {
                        path.Push(name);
                        AssertYamlEqualCore(expected.GetProperty(name), actual.GetProperty(name), path);
                        path.Pop();
                    }
                    break;
                case YamlValueKind.Array:
                    YamlElement.ArrayEnumerator expectedEnumerator = expected.EnumerateArray();
                    YamlElement.ArrayEnumerator actualEnumerator = actual.EnumerateArray();

                    int i = 0;
                    while (expectedEnumerator.MoveNext())
                    {
                        AssertTrue(passCondition: actualEnumerator.MoveNext(), "Actual array contains fewer elements.");
                        path.Push(i++);
                        AssertYamlEqualCore(expectedEnumerator.Current, actualEnumerator.Current, path);
                        path.Pop();
                    }

                    AssertTrue(passCondition: !actualEnumerator.MoveNext(), "Actual array contains additional elements.");
                    break;
                case YamlValueKind.String:
                    AssertTrue(passCondition: expected.GetString() == actual.GetString());
                    break;
                case YamlValueKind.Number:
                case YamlValueKind.True:
                case YamlValueKind.False:
                case YamlValueKind.Null:
                    AssertTrue(passCondition: expected.GetRawText() == actual.GetRawText());
                    break;
                default:
                    Debug.Fail($"Unexpected YamlValueKind: YamlValueKind.{valueKind}.");
                    break;
            }

            void AssertTrue(bool passCondition, string? message = null)
            {
                if (!passCondition)
                {
                    message ??= "Expected JSON does not match actual value";
                    Assert.Fail($"{message}\nExpected JSON: {expected}\n  Actual JSON: {actual}\n  in YamlPath: {BuildYamlPath(path)}");
                }

                // TODO replace with YamlPath implementation for YamlElement
                // cf. https://github.com/dotnet/runtime/issues/31068
                static string BuildYamlPath(Stack<object> path)
                {
                    var sb = new StringBuilder("$");
                    foreach (object node in path.Reverse())
                    {
                        string pathNode = node is string propertyName
                            ? "." + propertyName
                            : $"[{(int)node}]";

                        sb.Append(pathNode);
                    }
                    return sb.ToString();
                }
            }
        }

        [RequiresUnreferencedCode("AssertOptionsEqual uses reflection to enumerate YamlSerializerOptions properties.")]
        public static void AssertOptionsEqual(YamlSerializerOptions expected, YamlSerializerOptions actual)
        {
            foreach (PropertyInfo property in typeof(YamlSerializerOptions).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                Type propertyType = property.PropertyType;

                if (property.Name == nameof(YamlSerializerOptions.IsReadOnly))
                {
                    continue; // readonly-ness is not a structural property of YamlSerializerOptions.
                }
                else if (propertyType == typeof(IList<YamlConverter>))
                {
                    var expectedConverters = (IList<YamlConverter>)property.GetValue(expected);
                    var actualConverters = (IList<YamlConverter>)property.GetValue(actual);
                    Assert.Equal(expectedConverters.Count, actualConverters.Count);
                    for (int i = 0; i < actualConverters.Count; i++)
                    {
                        Assert.IsType(expectedConverters[i].GetType(), actualConverters[i]);
                    }
                }
                else if (propertyType == typeof(IList<IYamlTypeInfoResolver>))
                {
                    var list1 = (IList<IYamlTypeInfoResolver>)property.GetValue(expected);
                    var list2 = (IList<IYamlTypeInfoResolver>)property.GetValue(actual);
                    Assert.Equal(list1, list2);
                }
                else
                {
                    Assert.Equal(property.GetValue(expected), property.GetValue(actual));
                }
            }
        }

        /// <summary>
        /// Linq Cartesian product
        /// </summary>
        public static IEnumerable<(TFirst First, TSecond Second)> CrossJoin<TFirst, TSecond>(this IEnumerable<TFirst> first, IEnumerable<TSecond> second)
        {
            TSecond[]? secondCached = null;
            foreach (TFirst f in first)
            {
                secondCached ??= second.ToArray();
                foreach (TSecond s in secondCached)
                {
                    yield return (f, s);
                }
            }
        }

        /// <summary>
        /// Linq Cartesian product
        /// </summary>
        public static IEnumerable<(TFirst First, TSecond Second, TThird Third)> CrossJoin<TFirst, TSecond, TThird>(
            this IEnumerable<TFirst> first,
            IEnumerable<TSecond> second,
            IEnumerable<TThird> third)
        {
            TSecond[]? secondCached = null;
            TThird[]? thirdCached = null;

            foreach (TFirst f in first)
            {
                secondCached ??= second.ToArray();
                foreach (TSecond s in secondCached)
                {
                    thirdCached ??= third.ToArray();
                    foreach (TThird t in thirdCached)
                    {
                        yield return (f, s, t);
                    }
                }
            }
        }

        /// <summary>
        /// Linq Cartesian product
        /// </summary>
        public static IEnumerable<TResult> CrossJoin<TFirst, TSecond, TResult>(this IEnumerable<TFirst> first, IEnumerable<TSecond> second, Func<TFirst, TSecond, TResult> resultSelector)
            => first.CrossJoin(second).Select(tuple => resultSelector(tuple.First, tuple.Second));

        /// <summary>
        /// Linq Cartesian product
        /// </summary>
        public static IEnumerable<TResult> CrossJoin<TFirst, TSecond, TThird, TResult>(
            this IEnumerable<TFirst> first,
            IEnumerable<TSecond> second,
            IEnumerable<TThird> third,
            Func<TFirst, TSecond, TThird, TResult> resultSelector)
            => first.CrossJoin(second, third).Select(tuple => resultSelector(tuple.First, tuple.Second, tuple.Third));

        private static readonly Regex s_stripWhitespace = new Regex(@"\s+", RegexOptions.Compiled);

        public static string StripWhitespace(this string value)
            => s_stripWhitespace.Replace(value, string.Empty);

        public static TheoryData<YamlNumberHandling, YamlNumberHandling?, YamlNumberHandling?, YamlNumberHandling> GetUnionCaseNumberHandlingPrecedenceTestData()
        {
            const YamlNumberHandling Strict = YamlNumberHandling.Strict;
            const YamlNumberHandling ReadFromString = YamlNumberHandling.AllowReadingFromString;
            const YamlNumberHandling WriteAsString = YamlNumberHandling.WriteAsString;

            // Columns: global options, union-type override, case-type override, expected effective case handling.
            // null means no override; an explicit Strict value must still override less restrictive settings.
            return new()
            {
                // With no type overrides, use the global options.
                { Strict, null, null, Strict },
                { ReadFromString, null, null, ReadFromString },
                { WriteAsString, null, null, WriteAsString },

                // The union-type override wins over both the case type and global options; flags are not merged.
                { ReadFromString, Strict, null, Strict },
                { ReadFromString, Strict, ReadFromString, Strict },
                { Strict, ReadFromString, Strict, ReadFromString },
                { ReadFromString, WriteAsString, ReadFromString, WriteAsString },
                { ReadFromString | WriteAsString, Strict, null, Strict },

                // Without a union override, the case-type setting wins over the global options.
                { ReadFromString, null, Strict, Strict },
                { Strict, null, ReadFromString, ReadFromString },
                { Strict, null, WriteAsString, WriteAsString },
                { Strict, null, ReadFromString | WriteAsString, ReadFromString | WriteAsString },
            };
        }
    }

    /// <summary>
    /// Generic visitor pattern used for safely invoking generic methods in AOT.
    /// </summary>
    public abstract class TypeWitness
    {
        public abstract TResult Accept<TState, TResult>(ITypeVisitor<TState, TResult> visitor, TState state);
    }

    /// <summary>
    /// Generic visitor pattern used for safely invoking generic methods in AOT.
    /// </summary>
    public sealed class TypeWitness<T> : TypeWitness
    {
        public override TResult Accept<TState, TResult>(ITypeVisitor<TState, TResult> visitor, TState state) => visitor.Visit<T>(state);
    }

    /// <summary>
    /// Generic visitor pattern used for safely invoking generic methods in AOT.
    /// </summary>
    public interface ITypeVisitor<TState, TResult>
    {
        public TResult Visit<T>(TState state);
    }
}
