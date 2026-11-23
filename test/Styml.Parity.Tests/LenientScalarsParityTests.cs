// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Xunit;
using SY = SharpYaml;
using YD = YamlDotNet.Serialization;

namespace System.Text.Yaml.Parity.Tests
{
    /// <summary>
    /// AcceptLenientScalars, cell by cell against YamlDotNet 18.1.0 and SharpYaml 3.17.0
    /// with default options. By the parity rule a behaviour exists if either library has it and is refused only if neither
    /// does; a behaviour no standard has is opt-in. Each row is one scalar into one
    /// target, written as a value (<c>A: v</c>), a sequence item (<c>- v</c>) or a key (<c>v: 1</c>), and gives what each
    /// reader yields: YamlDotNet, SharpYaml, Styml by default, Styml with the option (core schema) and Styml with the option
    /// and the YAML 1.1 schema. "throws" is any exception.
    /// </summary>
    public class LenientScalarsParityTests
    {
        private static readonly YD.IDeserializer s_yamlDotNet = new YD.DeserializerBuilder().Build();
        private static readonly SY.YamlSerializerOptions s_sharpYaml = new();
        private static readonly YamlSerializerOptions s_lenient = new() { AcceptLenientScalars = true };

        public interface IHolder
        {
            object? Value { get; }
        }

        public class H<T> : IHolder
        {
            public T A { get; set; } = default!;

            object? IHolder.Value => A;
        }

        private static readonly Dictionary<string, (Type Type, string Position)> s_targets = new()
        {
            ["string"] = (typeof(H<string?>), "value"),
            ["int?"] = (typeof(H<int?>), "value"),
            ["int"] = (typeof(H<int>), "value"),
            ["object"] = (typeof(H<object?>), "value"),
            ["Uri"] = (typeof(H<Uri?>), "value"),
            ["List<string>"] = (typeof(List<string?>), "item"),
            ["List<int?>"] = (typeof(List<int?>), "item"),
            ["Dictionary<string, string>"] = (typeof(Dictionary<string, string?>), "value"),
            ["Dictionary<string, int?>"] = (typeof(Dictionary<string, int?>), "value"),
            ["bool"] = (typeof(H<bool>), "value"),
            ["bool?"] = (typeof(H<bool?>), "value"),
            ["Dictionary<bool, int> key"] = (typeof(Dictionary<bool, int>), "key"),
            ["double"] = (typeof(H<double>), "value"),
            ["float"] = (typeof(H<float>), "value"),
            ["decimal"] = (typeof(H<decimal>), "value"),
            ["long"] = (typeof(H<long>), "value"),
            ["Half"] = (typeof(H<Half>), "value"),
            ["Int128"] = (typeof(H<Int128>), "value"),
            ["enum"] = (typeof(H<Color>), "value"),
            ["DateTime"] = (typeof(H<DateTime>), "value"),
            ["Guid"] = (typeof(H<Guid>), "value"),
            ["char"] = (typeof(H<char>), "value"),
            ["TimeSpan"] = (typeof(H<TimeSpan>), "value"),
            ["struct"] = (typeof(H<Pt>), "value"),
            ["List<int>"] = (typeof(List<int>), "item"),
            ["List<bool>"] = (typeof(List<bool>), "item"),
            ["List<enum>"] = (typeof(List<Color>), "item"),
            ["List<DateTime>"] = (typeof(List<DateTime>), "item"),
            ["Dictionary<string, int>"] = (typeof(Dictionary<string, int>), "value"),
            ["Dictionary<string, bool>"] = (typeof(Dictionary<string, bool>), "value"),
            ["root int"] = (typeof(int), "root"),
            ["root bool"] = (typeof(bool), "root"),
            ["constructor (int, bool)"] = (typeof(Rec), "raw"),
            ["DayOfWeek"] = (typeof(H<DayOfWeek>), "value"),
            ["DayOfWeek?"] = (typeof(H<DayOfWeek?>), "value"),
            ["FileAttributes"] = (typeof(H<System.IO.FileAttributes>), "value"),
            ["DateTimeOffset"] = (typeof(H<DateTimeOffset>), "value"),
            ["DateOnly"] = (typeof(H<DateOnly>), "value"),
            ["TimeOnly"] = (typeof(H<TimeOnly>), "value"),
            ["Version"] = (typeof(H<Version?>), "value"),
            ["List<DayOfWeek>"] = (typeof(List<DayOfWeek>), "item"),
        };

        public enum Color { Red = 1, Green }

        public record struct Pt(int X);

        public record Rec(int I, bool B);

        [Theory]
        //          scalar         target                        YamlDotNet    SharpYaml   Styml       Styml+option  Styml+option+1.1
        // NuLL: SharpYaml only, as a null token (so its object reads differ: Styml keeps the token a String and takes it
        // as null only where a typed target takes null). Into a non-nullable value type, null and NuLL are its default
        // with the option.
        [InlineData("NuLL", "string", "\"NuLL\"", "null", "\"NuLL\"", "null", "null")]
        [InlineData("nULL", "string", "\"nULL\"", "null", "\"nULL\"", "null", "null")]
        [InlineData("NuLL", "int?", "throws", "null", "throws", "null", "null")]
        [InlineData("NuLL", "int", "throws", "0", "throws", "0", "0")]
        [InlineData("null", "int", "0", "0", "throws", "0", "0")]
        [InlineData("NuLL", "object", "\"NuLL\"", "null", "NuLL", "NuLL", "NuLL")]
        [InlineData("NuLL", "Uri", "uri:NuLL", "null", "uri:NuLL", "null", "null")]
        [InlineData("NuLL", "List<string>", "[\"NuLL\"]", "[null]", "[\"NuLL\"]", "[null]", "[null]")]
        [InlineData("NuLL", "List<int?>", "throws", "[null]", "throws", "[null]", "[null]")]
        [InlineData("NuLL", "Dictionary<string, string>", "{\"A\":\"NuLL\"}", "{\"A\":null}", "{\"A\":\"NuLL\"}", "{\"A\":null}", "{\"A\":null}")]
        [InlineData("NuLL", "Dictionary<string, int?>", "throws", "{\"A\":null}", "throws", "{\"A\":null}", "{\"A\":null}")]
        [InlineData("'NuLL'", "string", "\"NuLL\"", "\"NuLL\"", "\"NuLL\"", "\"NuLL\"", "\"NuLL\"")]
        [InlineData("!!str NuLL", "int?", "throws", "throws", "throws", "throws", "throws")]
        [InlineData("!!null NuLL", "int?", "throws", "null", "throws", "null", "null")]
        [InlineData("|-\n  NuLL", "int?", "throws", "throws", "throws", "throws", "throws")]
        // tRue: both libraries; behind the option since M4b, as a behaviour no standard has.
        [InlineData("tRuE", "bool", "true", "true", "throws", "true", "true")]
        [InlineData("FaLsE", "bool?", "false", "false", "throws", "false", "false")]
        [InlineData("tRuE", "object", "\"tRuE\"", "true", "tRuE", "tRuE", "tRuE")]
        // Both libraries read quoted, !!str and block scalars into a bool; Styml with the option.
        [InlineData("'tRuE'", "bool", "true", "true", "throws", "true", "true")]
        [InlineData("'true'", "bool", "true", "true", "throws", "true", "true")]
        [InlineData("!!str tRuE", "bool", "true", "true", "throws", "true", "true")]
        [InlineData("|-\n  tRuE", "bool", "true", "true", "throws", "true", "true")]
        [InlineData("\"yEs\"", "bool", "true", "throws", "throws", "throws", "true")]
        // yEs: YamlDotNet only (its default schema is YAML 1.1's words in any case); Styml needs the option and the 1.1 schema.
        [InlineData("yEs", "bool", "true", "throws", "throws", "throws", "true")]
        [InlineData("oN", "bool", "true", "throws", "throws", "throws", "true")]
        [InlineData("nO", "bool?", "false", "throws", "throws", "throws", "false")]
        [InlineData("oFF", "bool", "false", "throws", "throws", "throws", "false")]
        [InlineData("Y", "bool", "true", "throws", "throws", "throws", "true")]
        [InlineData("yes", "bool", "true", "throws", "throws", "throws", "true")]
        // Keys: a quoted key is text, true/false in any case, as System.Text.Json reads a
        // property name; a plain key reads as a value would, so tRuE needs the option and yEs the option and Yaml11.
        [InlineData("tRuE", "Dictionary<bool, int> key", "{true:1}", "{true:1}", "throws", "{true:1}", "{true:1}")]
        [InlineData("'fAlse'", "Dictionary<bool, int> key", "{false:1}", "{false:1}", "{false:1}", "{false:1}", "{false:1}")]
        [InlineData("yEs", "Dictionary<bool, int> key", "{true:1}", "throws", "throws", "throws", "{true:1}")]
        [InlineData("yes", "Dictionary<bool, int> key", "{true:1}", "throws", "throws", "throws", "{true:1}")]
        // .iNf, .nAn: both libraries; an addition beyond the three behaviours above.
        [InlineData(".iNf", "double", "Infinity", "Infinity", "throws", "Infinity", "Infinity")]
        [InlineData("-.iNF", "double", "-Infinity", "-Infinity", "throws", "-Infinity", "-Infinity")]
        [InlineData("+.inF", "float", "Infinity", "Infinity", "throws", "Infinity", "Infinity")]
        [InlineData(".nAn", "double", "NaN", "NaN", "throws", "NaN", "NaN")]
        [InlineData(".NAn", "float", "NaN", "NaN", "throws", "NaN", "NaN")]
        // One library is enough (the parity rule): a signed .nan (YamlDotNet), .NET's own names (SharpYaml), with the option.
        [InlineData("-.nan", "double", "NaN", "throws", "throws", "NaN", "NaN")]
        [InlineData("+.nAn", "double", "NaN", "throws", "throws", "NaN", "NaN")]
        [InlineData("NaN", "double", "throws", "NaN", "throws", "NaN", "NaN")]
        [InlineData("Infinity", "double", "throws", "Infinity", "throws", "Infinity", "Infinity")]
        [InlineData("nan", "double", "throws", "NaN", "throws", "NaN", "NaN")]
        // A quoted, block or tagged scalar whose text, trimmed, is the plain scalar of a number,
        // a boolean or a float name, with the option.
        [InlineData("'1'", "int", "1", "1", "throws", "1", "1")]
        [InlineData("'1'", "long", "1", "1", "throws", "1", "1")]
        [InlineData("\"12\"", "double", "12", "12", "throws", "12", "12")]
        [InlineData("!!str 12", "int", "12", "12", "throws", "12", "12")]
        [InlineData("|\n  12", "int", "12", "12", "throws", "12", "12")]
        [InlineData(">-\n  12", "int", "12", "12", "throws", "12", "12")]
        [InlineData("' 12'", "int", "12", "12", "throws", "12", "12")]
        [InlineData("'\t12\t'", "int", "12", "12", "throws", "12", "12")]
        [InlineData("\"1\\x32\"", "int", "12", "12", "throws", "12", "12")]
        [InlineData("'0x1F'", "int", "31", "31", "throws", "31", "31")]
        [InlineData("'0o17'", "int", "throws", "15", "throws", "15", "15")]
        [InlineData("'1_000'", "int", "1000", "1000", "throws", "throws", "1000")]
        [InlineData("'1.5'", "decimal", "1.5m", "1.5m", "throws", "1.5m", "1.5m")]
        [InlineData("'1.5'", "int", "throws", "throws", "throws", "throws", "throws")]
        [InlineData("'1e3'", "double", "1000", "1000", "throws", "1000", "1000")]
        [InlineData("'1'", "Half", "1", "1", "throws", "1", "1")]
        [InlineData("'12'", "Int128", "12", "12", "throws", "12", "12")]
        [InlineData("'.inf'", "double", "Infinity", "Infinity", "throws", "Infinity", "Infinity")]
        [InlineData("'.iNf'", "float", "Infinity", "Infinity", "throws", "Infinity", "Infinity")]
        [InlineData("'NaN'", "double", "throws", "NaN", "throws", "NaN", "NaN")]
        [InlineData("'1'", "int?", "1", "1", "throws", "1", "1")]
        [InlineData("'yes'", "bool", "true", "throws", "throws", "throws", "true")]
        [InlineData("'true'", "object", "\"true\"", "\"true\"", "true", "true", "true")]
        // A quoted null is a string: refused into every typed target, in both libraries too.
        [InlineData("''", "int", "throws", "throws", "throws", "throws", "throws")]
        [InlineData("' '", "int", "throws", "throws", "throws", "throws", "throws")]
        [InlineData("'null'", "int", "throws", "throws", "throws", "throws", "throws")]
        [InlineData("'~'", "int", "throws", "throws", "throws", "throws", "throws")]
        [InlineData("'NuLL'", "int", "throws", "throws", "throws", "throws", "throws")]
        [InlineData("'null'", "int?", "throws", "throws", "throws", "throws", "throws")]
        [InlineData("''", "bool", "throws", "throws", "throws", "throws", "throws")]
        // The text must be one plain scalar: a comment, an anchor, a collection or an error is not (YamlDotNet's
        // '- 1' as -1 is its number parser's).
        [InlineData("'1 # c'", "int", "throws", "throws", "throws", "throws", "throws")]
        [InlineData("'&a 1'", "int", "throws", "throws", "throws", "throws", "throws")]
        [InlineData("'[1]'", "int", "throws", "throws", "throws", "throws", "throws")]
        [InlineData("'%1'", "int", "throws", "throws", "throws", "throws", "throws")]
        [InlineData("'- 1'", "int", "-1", "throws", "throws", "throws", "throws")]
        // Tags: a matching core tag takes the lenient forms with the option; a tag that does not match stays an error.
        [InlineData("!!bool tRuE", "bool", "true", "true", "throws", "true", "true")]
        [InlineData("!!bool yEs", "bool", "true", "throws", "throws", "throws", "true")]
        [InlineData("!!float .iNf", "double", "Infinity", "Infinity", "throws", "Infinity", "Infinity")]
        [InlineData("!!float NaN", "double", "throws", "NaN", "throws", "NaN", "NaN")]
        [InlineData("!!int '1'", "int", "1", "1", "1", "1", "1")]
        [InlineData("!!bool 'true'", "bool", "true", "true", "true", "true", "true")]
        [InlineData("!!int true", "bool", "throws", "true", "throws", "throws", "throws")]
        [InlineData("!!int true", "int", "throws", "throws", "throws", "throws", "throws")]
        [InlineData("!!bool 1", "int", "throws", "1", "throws", "throws", "throws")]
        [InlineData("!custom 12", "int", "throws", "12", "12", "12", "12")]
        // A null into a non-nullable value type is its default with the option, in every position but a
        // constructor parameter.
        [InlineData("null", "bool", "false", "false", "throws", "false", "false")]
        [InlineData("~", "double", "0", "0", "throws", "0", "0")]
        [InlineData("", "decimal", "0m", "0m", "throws", "0m", "0m")]
        [InlineData("null", "enum", "0", "0", "throws", "0", "0")]
        [InlineData("null", "DateTime", "0001-01-01T00:00:00.0000000", "0001-01-01T00:00:00.0000000", "throws", "0001-01-01T00:00:00.0000000", "0001-01-01T00:00:00.0000000")]
        [InlineData("null", "Guid", "throws", "00000000-0000-0000-0000-000000000000", "throws", "00000000-0000-0000-0000-000000000000", "00000000-0000-0000-0000-000000000000")]
        [InlineData("", "Guid", "00000000-0000-0000-0000-000000000000", "00000000-0000-0000-0000-000000000000", "throws", "00000000-0000-0000-0000-000000000000", "00000000-0000-0000-0000-000000000000")]
        [InlineData("null", "char", "char:0", "char:0", "throws", "char:0", "char:0")]
        [InlineData("null", "TimeSpan", "throws", "00:00:00", "throws", "00:00:00", "00:00:00")]
        [InlineData("null", "struct", "Pt { X = 0 }", "Pt { X = 0 }", "throws", "Pt { X = 0 }", "Pt { X = 0 }")]
        [InlineData("NuLL", "bool", "throws", "false", "throws", "false", "false")]
        [InlineData("null", "List<int>", "[0]", "throws", "throws", "[0]", "[0]")]
        [InlineData("~", "List<bool>", "[false]", "throws", "throws", "[false]", "[false]")]
        [InlineData("null", "List<enum>", "[0]", "throws", "throws", "[0]", "[0]")]
        [InlineData("null", "List<DateTime>", "[0001-01-01T00:00:00.0000000]", "throws", "throws", "[0001-01-01T00:00:00.0000000]", "[0001-01-01T00:00:00.0000000]")]
        [InlineData("null", "Dictionary<string, int>", "{\"A\":0}", "throws", "throws", "{\"A\":0}", "{\"A\":0}")]
        [InlineData("", "Dictionary<string, bool>", "{\"A\":false}", "throws", "throws", "{\"A\":false}", "{\"A\":false}")]
        [InlineData("null", "root int", "0", "throws", "throws", "0", "0")]
        [InlineData("~", "root bool", "false", "throws", "throws", "false", "false")]
        [InlineData("I: null\nB: true\n", "constructor (int, bool)", "throws", "throws", "throws", "throws", "throws")]
        [InlineData("I: 1\nB: true\n", "constructor (int, bool)", "throws", "Rec { I = 1, B = True }", "Rec { I = 1, B = True }", "Rec { I = 1, B = True }", "Rec { I = 1, B = True }")]
        [InlineData("''", "int", "throws", "throws", "throws", "throws", "throws")]
        // D: Half takes .NET's names (both libraries), not the dotted ones (neither); YamlDotNet's ∞ is refused.
        [InlineData("nan", "Half", "NaN", "NaN", "throws", "NaN", "NaN")]
        [InlineData("Infinity", "Half", "throws", "Infinity", "throws", "Infinity", "Infinity")]
        [InlineData("-infinity", "float", "throws", "-Infinity", "throws", "-Infinity", "-Infinity")]
        [InlineData(".inf", "Half", "throws", "throws", "throws", "throws", "throws")]
        [InlineData(".nan", "Half", "throws", "throws", "throws", "throws", "throws")]
        [InlineData("\u221E", "Half", "Infinity", "throws", "throws", "throws", "throws")]
        [InlineData("inf", "double", "throws", "throws", "throws", "throws", "throws")]
        [InlineData("nan", "decimal", "throws", "throws", "throws", "throws", "throws")]
        // Enums: a quoted, block or !!str number reads as the plain number does, with the option.
        // Names (Monday) read in both libraries by default; Styml's default enum converter reads numbers only, as
        // System.Text.Json's does (YamlStringEnumConverter reads names), so they are refused here.
        [InlineData("1", "DayOfWeek", "Monday", "Monday", "Monday", "Monday", "Monday")]
        [InlineData("'1'", "DayOfWeek", "Monday", "Monday", "throws", "Monday", "Monday")]
        [InlineData("'1'", "DayOfWeek?", "Monday", "Monday", "throws", "Monday", "Monday")]
        [InlineData("\"12\"", "DayOfWeek", "12", "12", "throws", "12", "12")]
        [InlineData("'-3'", "DayOfWeek", "-3", "-3", "throws", "-3", "-3")]
        [InlineData("!!str 1", "DayOfWeek", "Monday", "Monday", "throws", "Monday", "Monday")]
        [InlineData("|\n  1", "DayOfWeek", "Monday", "Monday", "throws", "Monday", "Monday")]
        [InlineData("' 1 '", "DayOfWeek", "Monday", "Monday", "throws", "Monday", "Monday")]
        [InlineData("'1'", "FileAttributes", "ReadOnly", "ReadOnly", "throws", "ReadOnly", "ReadOnly")]
        [InlineData("'1'", "List<DayOfWeek>", "[Monday]", "[Monday]", "throws", "[Monday]", "[Monday]")]
        [InlineData("'0x1'", "DayOfWeek", "throws", "Monday", "throws", "Monday", "Monday")]
        [InlineData("'1.0'", "DayOfWeek", "throws", "throws", "throws", "throws", "throws")]
        [InlineData("Monday", "DayOfWeek", "Monday", "Monday", "throws", "throws", "throws")]
        [InlineData("'Monday'", "DayOfWeek", "Monday", "Monday", "throws", "throws", "throws")]
        // Padded text into the types that parse a string: both libraries trim white space and line breaks for
        // the date and time types and Guid, YamlDotNet for Version and a trailing pad on char.
        [InlineData("' 2001-01-01 '", "DateTime", "2001-01-01T00:00:00.0000000", "2001-01-01T00:00:00.0000000", "throws", "2001-01-01T00:00:00.0000000", "2001-01-01T00:00:00.0000000")]
        [InlineData("\"2001-01-01\\n\"", "DateTime", "2001-01-01T00:00:00.0000000", "2001-01-01T00:00:00.0000000", "throws", "2001-01-01T00:00:00.0000000", "2001-01-01T00:00:00.0000000")]
        [InlineData("|\n  2001-01-01", "DateTime", "2001-01-01T00:00:00.0000000", "2001-01-01T00:00:00.0000000", "throws", "2001-01-01T00:00:00.0000000", "2001-01-01T00:00:00.0000000")]
        [InlineData("'\t2001-01-01'", "DateTime", "2001-01-01T00:00:00.0000000", "2001-01-01T00:00:00.0000000", "throws", "2001-01-01T00:00:00.0000000", "2001-01-01T00:00:00.0000000")]
        [InlineData("' 2001-01-01T10:00:00+01:00 '", "DateTimeOffset", "2001-01-01T10:00:00.0000000+01:00", "2001-01-01T10:00:00.0000000+01:00", "throws", "2001-01-01T10:00:00.0000000+01:00", "2001-01-01T10:00:00.0000000+01:00")]
        [InlineData("' 2001-01-01 '", "DateOnly", "2001-01-01", "2001-01-01", "throws", "2001-01-01", "2001-01-01")]
        [InlineData("' 10:00:00 '", "TimeOnly", "10:00:00.0000000", "10:00:00.0000000", "throws", "10:00:00.0000000", "10:00:00.0000000")]
        [InlineData("|\n  01:02:03", "TimeSpan", "01:02:03", "01:02:03", "throws", "01:02:03", "01:02:03")]
        [InlineData("' 6f9619ff-8b86-d011-b42d-00c04fc964ff '", "Guid", "6f9619ff-8b86-d011-b42d-00c04fc964ff", "6f9619ff-8b86-d011-b42d-00c04fc964ff", "throws", "6f9619ff-8b86-d011-b42d-00c04fc964ff", "6f9619ff-8b86-d011-b42d-00c04fc964ff")]
        [InlineData("' 1.2.3 '", "Version", "1.2.3", "throws", "throws", "1.2.3", "1.2.3")]
        [InlineData("\"x\\n\"", "char", "char:120", "throws", "throws", "char:120", "char:120")]
        [InlineData("'x '", "char", "char:120", "throws", "throws", "char:120", "char:120")]
        [InlineData("' x '", "char", "char:32", "throws", "throws", "throws", "throws")]
        [InlineData("' 2001-13-01 '", "DateTime", "throws", "throws", "throws", "throws", "throws")]
        [InlineData("'2001-01-01 # c'", "DateTime", "throws", "throws", "throws", "throws", "throws")]
        // The core schema's forms, unchanged.
        [InlineData(".Inf", "double", "Infinity", "Infinity", "Infinity", "Infinity", "Infinity")]
        [InlineData("Null", "string", "null", "null", "null", "null", "null")]
        [InlineData("TRUE", "bool", "true", "true", "true", "true", "true")]
        public void Matrix(string scalar, string target, string yamlDotNet, string sharpYaml, string styml, string stymlLenient, string stymlLenientYaml11)
        {
            (Type type, string position) = s_targets[target];
            string yaml = position switch
            {
                "item" => "- " + scalar + "\n",
                "key" => scalar + ": 1\n",
                "root" => scalar,
                "raw" => scalar,
                _ => "A: " + scalar + "\n",
            };

            Assert.Equal(yamlDotNet, Describe(() => s_yamlDotNet.Deserialize(new StringReader(yaml), type)));
            Assert.Equal(sharpYaml, Describe(() => SY.YamlSerializer.Deserialize(yaml, type, s_sharpYaml)));
            Assert.Equal(styml, Describe(() => YamlSerializer.Deserialize(yaml, type)));
            Assert.Equal(stymlLenient, Describe(() => YamlSerializer.Deserialize(yaml, type, s_lenient)));
            Assert.Equal(stymlLenientYaml11, Describe(() =>
            {
                var reader = new Utf8YamlReader(Encoding.UTF8.GetBytes(yaml), new YamlReaderOptions { Schema = YamlSchemaKind.Yaml11, AcceptLenientScalars = true });
                return YamlSerializer.Deserialize(ref reader, type);
            }));
        }

        /// <summary>
        /// A gap of the YAML 1.1 schema in key position, fixed without the option. A plain key reads as a
        /// value would: YAML 1.1's words in its casings with Yaml11, other cases with the option too; quoted keys keep
        /// System.Text.Json's true/false rule.
        /// </summary>
        [Theory]
        //          key        YamlDotNet  SharpYaml   Styml core  Styml Yaml11  Styml Yaml11 + option
        [InlineData("yes", "{true:1}", "throws", "throws", "{true:1}", "{true:1}")]
        [InlineData("On", "{true:1}", "throws", "throws", "{true:1}", "{true:1}")]
        [InlineData("NO", "{false:1}", "throws", "throws", "{false:1}", "{false:1}")]
        [InlineData("y", "{true:1}", "throws", "throws", "{true:1}", "{true:1}")]
        [InlineData("yEs", "{true:1}", "throws", "throws", "throws", "{true:1}")]
        [InlineData("'yes'", "{true:1}", "throws", "throws", "throws", "throws")]
        [InlineData("TRUE", "{true:1}", "{true:1}", "{true:1}", "{true:1}", "{true:1}")]
        [InlineData("tRuE", "{true:1}", "{true:1}", "throws", "throws", "{true:1}")]
        [InlineData("'tRuE'", "{true:1}", "{true:1}", "{true:1}", "{true:1}", "{true:1}")]
        public void Yaml11Keys(string key, string yamlDotNet, string sharpYaml, string styml, string stymlYaml11, string stymlYaml11Lenient)
        {
            string yaml = key + ": 1\n";
            Assert.Equal(yamlDotNet, Describe(() => s_yamlDotNet.Deserialize<Dictionary<bool, int>>(yaml)));
            Assert.Equal(sharpYaml, Describe(() => SY.YamlSerializer.Deserialize<Dictionary<bool, int>>(yaml, s_sharpYaml)));
            Assert.Equal(styml, Describe(() => YamlSerializer.Deserialize<Dictionary<bool, int>>(yaml)));
            foreach ((bool lenient, string expected) in new[] { (false, stymlYaml11), (true, stymlYaml11Lenient) })
            {
                Assert.Equal(expected, Describe(() =>
                {
                    var reader = new Utf8YamlReader(Encoding.UTF8.GetBytes(yaml), new YamlReaderOptions { Schema = YamlSchemaKind.Yaml11, AcceptLenientScalars = lenient });
                    return YamlSerializer.Deserialize<Dictionary<bool, int>>(ref reader);
                }));
            }
        }

        private static string Describe(Func<object?> read)
        {
            object? value;
            try
            {
                value = read();
            }
            catch (Exception)
            {
                return "throws";
            }
            return Describe(value is IHolder h ? h.Value : value);
        }

        private static string Describe(object? value) => value switch
        {
            null => "null",
            string s => "\"" + s + "\"",
            bool b => b ? "true" : "false",
            double d => d.ToString(CultureInfo.InvariantCulture),
            Half h => ((double)h).ToString(CultureInfo.InvariantCulture),
            decimal m => m.ToString(CultureInfo.InvariantCulture) + "m",
            DateTime t => t.ToString("o", CultureInfo.InvariantCulture),
            DateTimeOffset t => t.ToString("o", CultureInfo.InvariantCulture),
            DateOnly t => t.ToString("o", CultureInfo.InvariantCulture),
            TimeOnly t => t.ToString("o", CultureInfo.InvariantCulture),
            TimeSpan t => t.ToString("c", CultureInfo.InvariantCulture),
            char c => "char:" + (int)c,
            float f => f.ToString(CultureInfo.InvariantCulture),
            int i => i.ToString(CultureInfo.InvariantCulture),
            Uri u => "uri:" + u.OriginalString,
            IDictionary d => "{" + string.Join(",", d.Keys.Cast<object>().Select(k => Describe(k) + ":" + Describe(d[k]))) + "}",
            IEnumerable e => "[" + string.Join(",", e.Cast<object?>().Select(Describe)) + "]",
            _ => Convert.ToString(value, CultureInfo.InvariantCulture)!,
        };
    }
}
