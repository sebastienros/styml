// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Text.Yaml
{
    internal static class YamlTestSerializerOptions
    {
        internal static YamlSerializerOptions DisallowDuplicateProperties =>
            field ??= new() { AllowDuplicateProperties = false };

        internal static YamlSerializerOptions DisallowDuplicatePropertiesIgnoringCase =>
            field ??= new() { AllowDuplicateProperties = false, PropertyNameCaseInsensitive = true };

        // M7a: the duplicate-key methods that relied on System.Text.Json's
        // last-wins default, retargeted to the YAML grammar (whose default is Error) with the option set. Only those
        // methods use these.
        internal static YamlSerializerOptions LastWins =>
            field ??= new() { DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins };

        internal static YamlDocumentOptions LastWinsDocument { get; } = new() { DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins };
    }
}
