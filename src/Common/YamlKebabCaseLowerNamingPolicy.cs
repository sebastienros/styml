// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Text.Yaml
{
    internal sealed class YamlKebabCaseLowerNamingPolicy : YamlSeparatorNamingPolicy
    {
        public YamlKebabCaseLowerNamingPolicy()
            : base(lowercase: true, separator: '-')
        {
        }
    }
}
