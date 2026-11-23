// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if !NET
using System.Runtime.Serialization;
#endif

namespace System.Text.Yaml
{
    // This class exists because the serializer needs to catch reader-originated exceptions in order to throw YamlException which has Path information.
#if !NET
    [Serializable]
#endif
    internal sealed class YamlReaderException : YamlException
    {
        public YamlReaderException(string message, long lineNumber, long bytePositionInLine) : base(message, path: null, lineNumber, bytePositionInLine)
        {
        }

#if !NET
        private YamlReaderException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
#endif
    }
}
