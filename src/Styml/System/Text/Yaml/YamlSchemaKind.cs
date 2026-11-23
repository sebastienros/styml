// Copyright (c) Sébastien Ros. Licensed under the MIT license.

namespace System.Text.Yaml
{
    /// <summary>
    /// The schema that gives plain scalars in value position their type. Keys, quoted and block scalars, and tagged nodes are not affected.
    /// </summary>
    public enum YamlSchemaKind
    {
        /// <summary>The YAML 1.2 core schema: <c>true</c>/<c>false</c>, <c>null</c>/<c>~</c>, decimal, <c>0x</c> and
        /// <c>0o</c> integers, floats with <c>.inf</c> and <c>.nan</c>. The default.</summary>
        Core = 0,

        /// <summary>The core schema plus the YAML 1.1 forms that YamlDotNet or SharpYaml accept: <c>yes</c>/<c>no</c>,
        /// <c>on</c>/<c>off</c>, <c>y</c>/<c>n</c> (lower, capitalised or upper case), binary <c>0b</c> and leading-zero
        /// octal (<c>0777</c>) integers, <c>_</c> digit separators and sexagesimal integers (<c>190:20:30</c>). Where
        /// the two schemas disagree (<c>0777</c>), YAML 1.1 wins.</summary>
        Yaml11 = 1,

        /// <summary>The YAML 1.2 JSON schema (section 10.2): only <c>null</c>, <c>true</c>, <c>false</c> and JSON numbers
        /// resolve; any other untagged plain scalar, the empty one included, is an error, as the specification says.
        /// </summary>
        Json = 2,

        /// <summary>The YAML 1.2 failsafe schema (section 10.1): every untagged plain scalar, the empty one included, is
        /// a string.</summary>
        Failsafe = 3,
    }
}
