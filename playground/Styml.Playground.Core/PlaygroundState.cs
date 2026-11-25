using System.IO.Compression;
using System.Text;
using System.Text.Yaml;
using System.Text.Yaml.Serialization;

namespace Styml.Playground;

/// <summary>
/// Everything the playground shows: the input, the view and the options. It is stored in the URL fragment
/// (<see cref="ToHash"/>), so that a link shows the same result. The state is written with Styml itself, by a
/// source-generated context.
/// </summary>
public sealed record PlaygroundState
{
    /// <summary>The YAML input.</summary>
    [YamlPropertyName("y")] public string Yaml { get; init; } = "";

    /// <summary>The view of the output.</summary>
    [YamlPropertyName("v")] public PlaygroundView View { get; init; }

    /// <summary>The options.</summary>
    [YamlPropertyName("o")] public PlaygroundOptions Options { get; init; } = new();

    /// <summary>
    /// The state as a URL fragment, without the <c>#</c>: the state in JSON-compatible YAML, compressed with Deflate and
    /// encoded in base64url. Options that keep their default value are not written.
    /// </summary>
    public string ToHash()
    {
        var yaml = YamlSerializer.SerializeToUtf8Bytes(this, PlaygroundYamlContext.Default.PlaygroundState);
        using var buffer = new MemoryStream();
        using (var deflate = new DeflateStream(buffer, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            deflate.Write(yaml);
        }

        return Convert.ToBase64String(buffer.GetBuffer(), 0, (int)buffer.Length).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    /// <summary>
    /// Reads a state written by <see cref="ToHash"/>, with or without its leading <c>#</c>. Returns <see langword="null"/>
    /// for an empty or invalid fragment.
    /// </summary>
    public static PlaygroundState? FromHash(string? hash)
    {
        if (string.IsNullOrEmpty(hash))
        {
            return null;
        }

        var text = hash[0] == '#' ? hash[1..] : hash;
        if (text.Length == 0)
        {
            return null;
        }

        try
        {
            var base64 = new StringBuilder(text.Length + 3).Append(text).Replace('-', '+').Replace('_', '/');
            base64.Append('=', (4 - base64.Length % 4) % 4);
            using var deflate = new DeflateStream(new MemoryStream(Convert.FromBase64String(base64.ToString())), CompressionMode.Decompress);
            using var yaml = new MemoryStream();
            deflate.CopyTo(yaml);
            return YamlSerializer.Deserialize(yaml.ToArray(), PlaygroundYamlContext.Default.PlaygroundState);
        }
        catch (Exception e) when (e is FormatException or InvalidDataException or YamlException)
        {
            return null;
        }
    }
}

[YamlSourceGenerationOptions(WriteStyle = YamlWriterStyle.JsonCompatible, DefaultIgnoreCondition = YamlIgnoreCondition.WhenWritingDefault)]
[YamlSerializable(typeof(PlaygroundState))]
internal sealed partial class PlaygroundYamlContext : YamlSerializerContext;
