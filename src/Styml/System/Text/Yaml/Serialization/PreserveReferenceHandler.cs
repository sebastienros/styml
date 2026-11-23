// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Text.Yaml.Serialization
{
    internal sealed class PreserveReferenceHandler : ReferenceHandler
    {
        /// <param name="anchorMode">
        /// M5b: <see cref="YamlAnchorMode.Repeated"/> for
        /// <see cref="ReferenceHandler.Preserve"/>, <see cref="YamlAnchorMode.All"/> for <see cref="ReferenceHandler.PreserveAll"/>.
        /// </param>
        public PreserveReferenceHandler(YamlAnchorMode anchorMode) => AnchorMode = anchorMode;

        public override ReferenceResolver CreateResolver() => throw new InvalidOperationException();

        internal override ReferenceResolver CreateResolver(bool writing) => new PreserveReferenceResolver(writing);
    }
}
