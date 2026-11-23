// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Yaml.Serialization;

namespace System.Text.Yaml.SourceGeneration.Tests
{
    // Test for https://github.com/dotnet/runtime/issues/99669
    // Types and first partial declaration for testing partial contexts with
    // [YamlSerializable] attributes on multiple declarations.

    public class TypeFromPartial1
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    // First partial declaration - declares TypeFromPartial1
    [YamlSerializable(typeof(TypeFromPartial1))]
    internal partial class MultiplePartialDeclarationsContext : YamlSerializerContext { }
}
