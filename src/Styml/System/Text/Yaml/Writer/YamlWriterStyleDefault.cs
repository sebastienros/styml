// Copyright (c) Sébastien Ros. Licensed under the MIT license.

namespace System.Text.Yaml
{
    /// <summary>
    /// What a <see cref="YamlWriterOptions.Style"/> that is not set means:
    /// block style from M4c, unless the internal <see cref="AppContext"/> switch
    /// <c>System.Text.Yaml.Utf8YamlWriter.UseJsonCompatibleStyle</c> restores JSON-compatible output. The switch is the
    /// imported tests' second leg (YJ) and the benchmarks' like-for-like device. (D1's reader switch, its pair, was
    /// deleted with the JSON grammar in M7a.)
    /// It is read once, at first use, and is fixed for the life of the process. An explicit style is never changed.
    /// </summary>
    internal static class YamlWriterStyleDefault
    {
        internal const string UseJsonCompatibleStyleSwitchName = "System.Text.Yaml.Utf8YamlWriter.UseJsonCompatibleStyle";

        /// <summary>True when a style that is not set means block style.</summary>
        internal static readonly bool UnsetStyleIsBlock = !(AppContext.TryGetSwitch(UseJsonCompatibleStyleSwitchName, out bool enabled) && enabled);

        // An explicit static constructor makes the type initialisation precise (not beforefieldinit), so the switch is
        // read at first use rather than whenever the runtime chooses.
        static YamlWriterStyleDefault()
        {
        }
    }
}
