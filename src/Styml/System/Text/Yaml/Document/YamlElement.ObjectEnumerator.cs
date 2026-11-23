// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace System.Text.Yaml
{
    public partial struct YamlElement
    {
        /// <summary>
        ///   An enumerable and enumerator for the properties of a YAML object.
        /// </summary>
        [DebuggerDisplay("{Current,nq}")]
        public struct ObjectEnumerator : IEnumerable<YamlProperty>, IEnumerator<YamlProperty>
        {
            private readonly YamlElement _target;
            private int _curIdx;
            private readonly int _endIdxOrVersion;

            internal ObjectEnumerator(YamlElement target, int currentIndex = -1)
            {
                _target = target;
                _curIdx = currentIndex;

                Debug.Assert(target.TokenType == YamlTokenType.StartObject);
                _endIdxOrVersion = target._parent.GetEndIndex(_target._idx, includeEndElement: false);
            }

            /// <inheritdoc />
            public YamlProperty Current
            {
                get
                {
                    if (_curIdx < 0)
                    {
                        return default;
                    }

                    // A default enumerator has no document; its Current is a property over nothing, as before M5a.
                    return new YamlProperty(_target._parent is null ? new YamlElement(null!, _curIdx) : _target._parent.CreateElement(_curIdx));
                }
            }

            /// <summary>
            ///   Returns an enumerator that iterates the properties of an object.
            /// </summary>
            /// <returns>
            ///   An <see cref="ObjectEnumerator"/> value that can be used to iterate
            ///   through the object.
            /// </returns>
            /// <remarks>
            ///   The enumerator will enumerate the properties in the order they are
            ///   declared, and when an object has multiple definitions of a single
            ///   property they will all individually be returned (each in the order
            ///   they appear in the content).
            /// </remarks>
            public ObjectEnumerator GetEnumerator()
            {
                ObjectEnumerator ator = this;
                ator._curIdx = -1;
                return ator;
            }

            /// <inheritdoc />
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            /// <inheritdoc />
            IEnumerator<YamlProperty> IEnumerable<YamlProperty>.GetEnumerator() => GetEnumerator();

            /// <inheritdoc />
            public void Dispose()
            {
                _curIdx = _endIdxOrVersion;
            }

            /// <inheritdoc />
            public void Reset()
            {
                _curIdx = -1;
            }

            /// <inheritdoc />
            object IEnumerator.Current => Current;

            /// <inheritdoc />
            public bool MoveNext()
            {
                if (_curIdx >= _endIdxOrVersion)
                {
                    return false;
                }

                int nameRow = _curIdx < 0
                    ? _target._idx + YamlDocument.DbRow.Size
                    : _target._parent.GetEndIndex(_curIdx, includeEndElement: true);

                // nameRow is now pointing at a property name: its value follows (M5e: after a collection key's rows)
                _curIdx = _target._parent.ValueRowOf(nameRow);

                return _curIdx < _endIdxOrVersion;
            }
        }
    }
}
