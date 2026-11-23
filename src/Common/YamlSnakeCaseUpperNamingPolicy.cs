// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Text.Yaml
{
    internal sealed class YamlSnakeCaseUpperNamingPolicy : YamlSeparatorNamingPolicy
    {
        public YamlSnakeCaseUpperNamingPolicy()
            : base(lowercase: false, separator: '_')
        {
        }
    }
}
