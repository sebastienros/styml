using System.Text.Yaml;

namespace Styml.Playground.Tests;

public class PlaygroundTests
{
    public static TheoryData<string> Samples => [.. PlaygroundSamples.All.Select(s => s.Id)];

    [Theory]
    [MemberData(nameof(Samples))]
    public void EverySampleRunsInEveryView(string id)
    {
        var sample = PlaygroundSamples.All.Single(s => s.Id == id);
        foreach (var view in Enum.GetValues<PlaygroundView>())
        {
            var result = PlaygroundEngine.Run(sample.State.Yaml, sample.State.Options, view);
            // The error sample has a duplicate key, which the tokens don't check; JSON has no collection keys.
            if ((id == "errors" && view != PlaygroundView.Tokens) || (id == "collection-keys" && view == PlaygroundView.Json))
            {
                Assert.NotNull(result.Error);
            }
            else
            {
                Assert.True(result.Error is null, $"{id} in {view}: {result.Error?.Message}");
            }
        }
    }

    [Fact]
    public void SeveralDocumentsAreWrittenWithMarkers()
    {
        var result = PlaygroundEngine.Run("%TAG !e! tag:e.com:\n---\na: !e!x 1\n...\n---\n- [b, c]\n", new(), PlaygroundView.Yaml);
        Assert.Null(result.Error);
        Assert.Equal(2, result.DocumentCount);
        Assert.Equal("a: !<tag:e.com:x> 1\n---\n- [b, c]\n", result.Output);
    }

    [Fact]
    public void JsonViewWritesEveryDocument()
    {
        var result = PlaygroundEngine.Run("a: 1\n---\n[x, 'y']\n", new() { JsonIndented = false }, PlaygroundView.Json);
        Assert.Equal("{\"a\":1}\n[\"x\",\"y\"]\n", result.Output);
    }

    [Theory]
    [InlineData("")]
    [InlineData("# only a comment\n")]
    public void EmptyInputIsOneEmptyDocument(string yaml)
    {
        var result = PlaygroundEngine.Run(yaml, new(), PlaygroundView.Tree);
        Assert.Null(result.Error);
        Assert.Equal(1, result.DocumentCount);
        Assert.Null(Assert.Single(result.Trees));
    }

    [Fact]
    public void OptionsApplyToEveryDocument()
    {
        var yaml = "a: 1\n---\nb: 1\nb: 2\n";
        Assert.Contains("Duplicate", PlaygroundEngine.Run(yaml, new(), PlaygroundView.Yaml).Error!.Message);
        var lastWins = PlaygroundEngine.Run(yaml, new() { DuplicateKeyHandling = YamlDuplicateKeyHandling.LastWins }, PlaygroundView.Json);
        Assert.Null(lastWins.Error);
    }

    [Fact]
    public void ErrorsHaveAPosition()
    {
        var result = PlaygroundEngine.Run("a: [1, 2\nb: 3", new(), PlaygroundView.Yaml);
        Assert.Equal(2, result.Error!.Line);
        Assert.DoesNotContain("LineNumber", result.Error.Message);
    }

    [Fact]
    public void TreeShowsAnchorsAliasesTagsAndStyles()
    {
        var result = PlaygroundEngine.Run("base: &b !p {x: 1}\ncopy: *b\n? [k]\n: 'v'\n", new(), PlaygroundView.Tree);
        var root = Assert.Single(result.Trees)!;
        Assert.Equal("mapping", root.Kind);
        var @base = root.Children[0];
        Assert.Equal(("base", "b", "!p", "Flow", false), (@base.Key, @base.Anchor, @base.Tag, @base.Style, @base.IsAlias));
        Assert.Equal("number", @base.Children[0].Type);
        Assert.True(root.Children[1].IsAlias);
        var complex = root.Children[2];
        Assert.True(complex.ComplexKey);
        Assert.Equal("[k]", complex.Key);
        Assert.Equal("SingleQuoted", complex.Style);
    }

    [Fact]
    public void TokensHavePositions()
    {
        var result = PlaygroundEngine.Run("a: &x 1\nb: *x\n", new(), PlaygroundView.Tokens);
        Assert.Null(result.Error);
        var alias = result.Tokens.Single(t => t.Type == YamlTokenType.Alias);
        Assert.Equal(("x", 2, 4), (alias.Text, alias.Line, alias.Column));
        Assert.Equal("x", result.Tokens.Single(t => t.Type == YamlTokenType.Number).Anchor);
    }

    [Fact]
    public void StateRoundTripsThroughTheHash()
    {
        var state = new PlaygroundState { Yaml = "a: 1\n", View = PlaygroundView.Tokens, Options = new() { Schema = YamlSchemaKind.Json, IndentSize = 4 } };
        Assert.Equal(state.ToHash(), PlaygroundState.FromHash("#" + state.ToHash())!.ToHash());
        var back = PlaygroundState.FromHash(state.ToHash())!;
        Assert.Equal(state.Options, back.Options);
        Assert.Equal(state.Yaml, back.Yaml);
        Assert.Equal(state.View, back.View);
        Assert.False(PlaygroundState.FromHash(new PlaygroundState { Options = new() { JsonIndented = false } }.ToHash())!.Options.JsonIndented);
        Assert.Null(PlaygroundState.FromHash(new PlaygroundState { Yaml = new string('a', PlaygroundState.MaxStateLength) }.ToHash()));
        Assert.Null(PlaygroundState.FromHash("#not-a-state"));
        Assert.Null(PlaygroundState.FromHash(""));
    }

    [Theory]
    [InlineData(PlaygroundLanguage.Yaml)]
    [InlineData(PlaygroundLanguage.Json)]
    public void HighlighterHasEveryGrammar(PlaygroundLanguage language)
    {
        var highlighter = new PlaygroundHighlighter();
        Assert.Empty(highlighter.GetMissingGrammars(language));
        Assert.Contains("<span", highlighter.ToHtml("a: [1, 'b']", language));
    }

    [Fact]
    public void MaxDepthIsBounded()
    {
        var options = new PlaygroundOptions { MaxDepth = 100_000 };
        Assert.Equal(PlaygroundOptions.MaxDepthLimit, options.ToDocumentOptions().MaxDepth);
        Assert.Equal(PlaygroundOptions.MaxDepthLimit, options.ToReaderOptions().MaxDepth);
    }
}
