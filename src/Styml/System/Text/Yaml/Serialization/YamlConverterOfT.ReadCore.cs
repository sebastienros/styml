// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace System.Text.Yaml.Serialization
{
    public partial class YamlConverter<T>
    {
        internal bool ReadCore(
            ref Utf8YamlReader reader,
            out T? value,
            YamlSerializerOptions options,
            ref ReadStack state)
        {
            try
            {
                if (!state.IsContinuation && !IsRootLevelMultiContentStreamingConverter)
                {
                    // This is first call to the converter -- advance the reader
                    // to the first JSON token and perform a read-ahead if necessary.
                    if (!reader.TryAdvanceWithOptionalReadAhead(RequiresReadAhead))
                    {
                        if (state.SupportContinuation && state.Current.ReturnValue is object result)
                        {
                            // This branch is hit when deserialization has completed in an earlier call
                            // but we're still processing trailing whitespace. Return the result stored in the state machine.
                            Debug.Assert(!reader.AllowMultipleValues, "should not be entered by converters allowing multiple values.");
                            value = (T)result;
                        }
                        else
                        {
                            value = default;
                        }

                        if (reader.IsFinalBlock && reader.TokenType == YamlTokenType.None && !reader.AllowMultipleValues)
                        {
                            // Decision 2 (M5c): the YAML grammar reads empty input as zero tokens, and the serializer
                            // reads zero tokens as a null, under every schema.
                            Utf8YamlReader nullReader = YamlSerializer.EmptyInputReader(reader.CurrentState.Options);
                            nullReader.Read();
                            return TryRead(ref nullReader, state.Current.YamlTypeInfo.Type, options, ref state, out value, out _);
                        }

                        return reader.IsFinalBlock;
                    }
                }

                bool success = TryRead(ref reader, state.Current.YamlTypeInfo.Type, options, ref state, out value, out _);
                if (success)
                {
                    if (!reader.AllowMultipleValues)
                    {
                        // Read any trailing whitespace. This will throw if YamlCommentHandling=Disallow.
                        // Avoiding setting ReturnValue for the final block; reader.Read() returns 'false' even when this is the final block.
                        if (!reader.Read() && !reader.IsFinalBlock)
                        {
                            // This method will re-enter if so set `ReturnValue` which will be returned during re-entry.
                            state.Current.ReturnValue = value;
                            success = false;
                        }
                    }
                }

                return success;
            }
            catch (Exception ex)
            {
                switch (ex)
                {
                    case YamlReaderException yamlReaderEx:
                        ThrowHelper.ReThrowWithPath(ref state, yamlReaderEx);
                        break;

                    case FormatException when ex.Source == ThrowHelper.ExceptionSourceValueToRethrowAsYamlException:
                        ThrowHelper.ReThrowWithPath(ref state, reader, ex);
                        break;

                    case InvalidOperationException when ex.Source == ThrowHelper.ExceptionSourceValueToRethrowAsYamlException:
                        ThrowHelper.ReThrowWithPath(ref state, reader, ex);
                        break;

                    case YamlException yamlEx when yamlEx.Path is null:
                        // YamlExceptions where the Path property is already set
                        // typically originate from nested calls to YamlSerializer;
                        // treat these cases as any other exception type and do not
                        // overwrite any exception information.
                        ThrowHelper.AddYamlExceptionInformation(ref state, reader, yamlEx);
                        break;

                    case NotSupportedException when !ex.Message.Contains(" Path: "):
                        // If the message already contains Path, just re-throw. This could occur in serializer re-entry cases.
                        // To get proper Path semantics in re-entry cases, APIs that take 'state' need to be used.
                        ThrowHelper.ThrowNotSupportedException(ref state, reader, ex);
                        break;
                }

                throw;
            }
        }
    }
}
