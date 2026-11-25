using Tint;
using Tint.Grammars;
using Tint.Themes;

namespace Styml.Playground;

/// <summary>The languages the playground colors.</summary>
public enum PlaygroundLanguage
{
    /// <summary>YAML, in the editor and the YAML view.</summary>
    Yaml,

    /// <summary>JSON, in the JSON view.</summary>
    Json,
}

/// <summary>
/// Colors the editor and the output with Tint, the VS Code TextMate grammars compiled to .NET, with the Light+ colors
/// inline and the Dark+ ones as CSS variables (<c>--tint-dark</c>) that the stylesheet applies in the dark theme. Not
/// thread-safe, like the Tint highlighters it owns.
/// </summary>
public sealed class PlaygroundHighlighter
{
    private readonly GrammarRegistry _registry = new();
    private readonly Theme _light = Theme.FromSource(TintThemes.LightPlus);
    private readonly Theme _dark = Theme.FromSource(TintThemes.DarkPlus);
    private readonly Dictionary<PlaygroundLanguage, Highlighter> _highlighters = [];

    /// <summary>Creates the highlighter. Grammars are compiled when they are first used.</summary>
    public PlaygroundHighlighter()
    {
        // Only the grammars referenced here are kept by the trimmer. The YAML grammar includes its versioned grammars
        // (the test of this class checks that none is missing).
        _registry.Add(
            TintGrammars.Yaml, TintGrammars.Yaml10, TintGrammars.Yaml11, TintGrammars.Yaml12, TintGrammars.Yaml13,
            TintGrammars.YamlEmbedded, TintGrammars.Json);
    }

    /// <summary>The highlighted HTML of <paramref name="code"/>: a <c>pre</c> with one <c>span.line</c> per line.</summary>
    public string ToHtml(string code, PlaygroundLanguage language)
    {
        if (!_highlighters.TryGetValue(language, out var highlighter))
        {
            _highlighters[language] = highlighter = _registry.CreateHighlighter(Grammar(language).ScopeName, light: _light, dark: _dark);
        }

        return highlighter.ToHtml(code);
    }

    /// <summary>The scopes of the grammars that <paramref name="language"/> includes and that are not registered.</summary>
    public IReadOnlyCollection<string> GetMissingGrammars(PlaygroundLanguage language) => _registry.GetMissingGrammars(Grammar(language).ScopeName);

    private static IGrammarSource Grammar(PlaygroundLanguage language) => language switch
    {
        PlaygroundLanguage.Yaml => TintGrammars.Yaml,
        PlaygroundLanguage.Json => TintGrammars.Json,
        _ => throw new ArgumentOutOfRangeException(nameof(language)),
    };
}
