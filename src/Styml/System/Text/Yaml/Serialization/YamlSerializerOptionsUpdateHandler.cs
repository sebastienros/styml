// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Text.Yaml;
using System.Text.Yaml.Serialization.Metadata;

[assembly: MetadataUpdateHandler(typeof(YamlSerializerOptionsUpdateHandler))]

#pragma warning disable IDE0060

namespace System.Text.Yaml
{
    /// <summary>Handler used to clear YamlSerializerOptions reflection cache upon a metadata update.</summary>
    internal static class YamlSerializerOptionsUpdateHandler
    {
        public static void ClearCache(Type[]? types)
        {
            // Ignore the types, and just clear out all reflection caches from serializer options.
            foreach (KeyValuePair<YamlSerializerOptions, object?> options in YamlSerializerOptions.TrackedOptionsInstances.All)
            {
                options.Key.ClearCaches();
            }

            MemberAccessor.ClearCache();
        }
    }
}
