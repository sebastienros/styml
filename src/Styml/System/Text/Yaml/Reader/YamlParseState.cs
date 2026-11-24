// Copyright (c) Sébastien Ros. Licensed under the MIT license.

using System.Diagnostics;

namespace System.Text.Yaml
{
    /// <summary>
    /// The part of the <see cref="Utf8YamlReader"/> state that only the YAML grammar uses. It is carried in
    /// <see cref="YamlReaderState"/> so that a reader can be resumed over a new buffer.
    /// </summary>
    /// <remarks>
    /// One level is kept per open collection (the collection kind itself lives in the reader's BitStack).
    /// Levels 1..16 are packed one byte each into <see cref="Levels0"/> / <see cref="Levels1"/>:
    /// <c>0xFF</c> flow collection, <c>0xFE</c> implicit single-pair mapping inside a flow sequence, otherwise a
    /// block collection whose value is its indentation delta from the enclosing block level (0 means an indentless
    /// sequence). Deeper nesting, or a delta above <see cref="MaxInlineDelta"/>, spills all levels into
    /// <see cref="LevelsSpill"/> as ints: -1 flow, -2 flow pair, otherwise <c>(indent &lt;&lt; 1) | indentless</c>.
    /// </remarks>
    internal struct YamlParseState
    {
        internal const int InlineLevels = 16;
        internal const byte FlowCode = 0xFF;
        internal const byte FlowPairCode = 0xFE;
        internal const int MaxInlineDelta = 0xFD;

        internal ulong Levels0;
        internal ulong Levels1;
        internal int[]? LevelsSpill;

        // Indentation of the innermost block collection, or of the context when there is none, stored + 1 so that
        // the default value means -1 (top level).
        internal int BlockIndentPlusOne;

        // Indentation of the context the reader starts in: -1 for a whole stream; the parent's indentation for a
        // reader scoped to one value (stored + 1).
        internal int BaseIndentPlusOne;

        internal YamlParseFlags Flags;

        // The line (+1) of the last collection the key probe found too long to be an implicit key, so that a ':' after
        // it reports that limit rather than a key spanning lines.
        internal long TooLongKeyLinePlusOne;

        // The %TAG handles of the current document, or null when it declares none.
        internal YamlTagDirectives? TagDirectives;

        // The serializer operation's anchor table and this reader's base position (M5b), or null before the first anchor.
        internal YamlAnchorContext? AnchorContext;

        internal readonly int BlockIndent => BlockIndentPlusOne - 1;

        /// <summary>
        /// The state of a reader over one value sliced out of a larger document: it starts inside the parent's
        /// indentation, and its first content may start a block collection even though it is not at column 0.
        /// </summary>
        internal static YamlParseState ForScopedValue(int parentIndent, YamlTagDirectives? tagDirectives, YamlAnchorContext? anchorContext = null) => new()
        {
            BaseIndentPlusOne = parentIndent + 1,
            BlockIndentPlusOne = parentIndent + 1,
            Flags = YamlParseFlags.ContentAtLineStart | YamlParseFlags.ScopedValue,
            TagDirectives = tagDirectives,
            AnchorContext = anchorContext,
        };

        internal readonly int BaseIndent => BaseIndentPlusOne - 1;

        internal readonly bool Has(YamlParseFlags flag) => (Flags & flag) != 0;

        internal void Set(YamlParseFlags flag, bool value)
        {
            if (value)
            {
                Flags |= flag;
            }
            else
            {
                Flags &= ~flag;
            }
        }

        /// <summary>Returns the level code of the collection at <paramref name="depth"/> (1-based).</summary>
        /// <returns>-1 flow, -2 flow pair mapping, otherwise <c>(indent &lt;&lt; 1) | indentless</c>.</returns>
        internal readonly int GetLevel(int depth)
        {
            Debug.Assert(depth >= 1);
            if (LevelsSpill is not null)
            {
                return LevelsSpill[depth - 1];
            }

            Debug.Assert(depth <= InlineLevels);
            byte code = GetInlineByte(depth - 1);
            if (code == FlowCode)
            {
                return -1;
            }
            if (code == FlowPairCode)
            {
                return -2;
            }

            // Only the innermost block level's absolute indentation is tracked; callers only ask for it.
            return (BlockIndent << 1) | (code == 0 ? 1 : 0);
        }

        internal void Push(int depth, int code)
        {
            Debug.Assert(depth >= 1);
            int parentIndent = BlockIndent;

            if (LevelsSpill is null)
            {
                byte inline;
                if (code == -1)
                {
                    inline = FlowCode;
                }
                else if (code == -2)
                {
                    inline = FlowPairCode;
                }
                else
                {
                    int delta = (code >> 1) - parentIndent;
                    Debug.Assert(delta >= 0);
                    Debug.Assert((code & 1) == 0 || delta == 0);
                    inline = delta <= MaxInlineDelta ? (byte)delta : FlowCode;
                    if (delta > MaxInlineDelta)
                    {
                        Spill(depth);
                    }
                }

                if (LevelsSpill is null)
                {
                    if (depth <= InlineLevels)
                    {
                        SetInlineByte(depth - 1, inline);
                    }
                    else
                    {
                        Spill(depth);
                    }
                }
            }

            if (LevelsSpill is not null)
            {
                if (LevelsSpill.Length < depth)
                {
                    Array.Resize(ref LevelsSpill, Math.Max(depth, LevelsSpill.Length * 2));
                }
                LevelsSpill[depth - 1] = code;
            }

            if (code >= 0)
            {
                BlockIndentPlusOne = (code >> 1) + 1;
            }
        }

        /// <summary>Removes the level at <paramref name="depth"/> and restores the enclosing block indentation.</summary>
        internal void Pop(int depth)
        {
            int code = GetLevel(depth);
            if (code < 0)
            {
                return;
            }

            if (LevelsSpill is null)
            {
                int delta = GetInlineByte(depth - 1);
                BlockIndentPlusOne -= delta;
                return;
            }

            int indent = BaseIndent;
            for (int d = depth - 1; d >= 1; d--)
            {
                int c = LevelsSpill[d - 1];
                if (c >= 0)
                {
                    indent = c >> 1;
                    break;
                }
            }
            BlockIndentPlusOne = indent + 1;
        }

        private void Spill(int depth)
        {
            // Rebuild absolute codes for the existing inline levels, innermost block indentation first.
            int existing = depth - 1;
            int[] spill = new int[Math.Max(InlineLevels * 2, depth * 2)];
            int indent = BlockIndent;
            for (int d = existing; d >= 1; d--)
            {
                byte code = GetInlineByte(d - 1);
                if (code == FlowCode)
                {
                    spill[d - 1] = -1;
                }
                else if (code == FlowPairCode)
                {
                    spill[d - 1] = -2;
                }
                else
                {
                    spill[d - 1] = (indent << 1) | (code == 0 ? 1 : 0);
                    indent -= code;
                }
            }
            LevelsSpill = spill;
        }

        private readonly byte GetInlineByte(int index)
        {
            ulong word = index < 8 ? Levels0 : Levels1;
            return (byte)(word >> ((index & 7) * 8));
        }

        private void SetInlineByte(int index, byte value)
        {
            int shift = (index & 7) * 8;
            ulong mask = ~(0xFFUL << shift);
            if (index < 8)
            {
                Levels0 = (Levels0 & mask) | ((ulong)value << shift);
            }
            else
            {
                Levels1 = (Levels1 & mask) | ((ulong)value << shift);
            }
        }
    }

    [Flags]
    internal enum YamlParseFlags : ushort
    {
        None = 0,

        /// <summary>'---' was read and the document's root node has not started.</summary>
        DocumentOpen = 1 << 0,

        /// <summary>The current document's root node is complete; only an end marker, a new document or the end may follow.</summary>
        RootDone = 1 << 1,

        /// <summary>At least one document has started (a second one needs AllowMultipleValues).</summary>
        AnyDocument = 1 << 2,

        /// <summary>Directives were read since the last document; '---' must follow.</summary>
        DirectivesSeen = 1 << 3,

        /// <summary>A %YAML directive was read for the coming document.</summary>
        YamlDirectiveSeen = 1 << 4,

        /// <summary>A '? key' was read; the ':' value indicator (or nothing) follows.</summary>
        ExplicitKeyPending = 1 << 5,

        /// <summary>The consumed position is the first content byte of its line (after a zero-width token).</summary>
        ContentAtLineStart = 1 << 6,

        /// <summary>The previous token was the key of a flow mapping read without ':' (its value is implicitly null).</summary>
        FlowKeyWithoutValue = 1 << 7,

        /// <summary>
        /// The ',' after a flow entry was consumed, and comment tokens followed it (CommentHandling.Allow): the next
        /// content starts the next entry, with no ',' to read first.
        /// </summary>
        FlowSeparatorConsumed = 1 << 8,

        /// <summary>
        /// The previous PropertyName was a flow key whose ':' follows after comment tokens (CommentHandling.Allow): the
        /// next content is that ':', which the value's Read consumes.
        /// </summary>
        FlowColonPending = 1 << 9,

        /// <summary>
        /// The reader reads one value sliced out of a larger document (ForScopedValue): its depth 0 is not the
        /// document level, so JSON comments are not looked for around its root node.
        /// </summary>
        ScopedValue = 1 << 10,
    }
}
