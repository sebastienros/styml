// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Yaml.Serialization;

namespace System.Text.Yaml.SourceGeneration.Tests
{
    // Second type for testing partial contexts with
    // [YamlSerializable] attributes on multiple declarations.

    public class TypeFromPartial2
    {
        public double Value { get; set; }
        public bool IsActive { get; set; }
    }

    // Second partial declaration - declares TypeFromPartial2
    [YamlSerializable(typeof(TypeFromPartial2))]
    internal partial class MultiplePartialDeclarationsContext { }
}
