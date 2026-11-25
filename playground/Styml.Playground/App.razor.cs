using System.Globalization;
using System.Reflection;
using System.Text.Yaml;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Styml.Playground;

/// <summary>
/// The playground page: an editor for the YAML input, the output in one of the views, and the options. Every change
/// runs again after a short pause, and the state is written in the URL fragment so that a link shows the same result.
/// </summary>
public sealed partial class App : IAsyncDisposable
{
    private const int RunDelayMs = 120;
    private const int HashDelayMs = 400;

    private static readonly string StymlVersion =
        typeof(YamlDocument).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "";

    private static readonly (PlaygroundView View, string Name, string Title)[] Views =
    [
        (PlaygroundView.Yaml, "YAML", "The documents written back with Utf8YamlWriter, in block style"),
        (PlaygroundView.Json, "JSON", "The documents written in the JSON-compatible style"),
        (PlaygroundView.Tree, "Tree", "The nodes of each YamlDocument: kinds, resolved types, styles, anchors and tags"),
        (PlaygroundView.Tokens, "Tokens", "The tokens of Utf8YamlReader"),
    ];

    private string _yaml = "";
    private PlaygroundView _view;
    private PlaygroundOptions _options = new();
    private string _sampleId = "";
    private bool _showOptions;
    private bool _details = true;
    private int _changedOptions;
    private PlaygroundResult? _result;
    private string? _outputHtml;
    private double? _readyMs;
    private string _shareLabel = "Share";
    private string _copyLabel = "Copy";

    private IJSObjectReference? _module;
    private CancellationTokenSource? _pending;
    private CancellationTokenSource? _pendingHash;

    [Inject]
    private IJSRuntime JS { get; set; } = null!;

    [Inject]
    private PlaygroundHighlighter Highlighter { get; set; } = null!;

    private PlaygroundState State => new() { Yaml = _yaml, View = _view, Options = _options };

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        _module = await JS.InvokeAsync<IJSObjectReference>("import", "./js/playground.js");
        var state = PlaygroundState.FromHash(await _module.InvokeAsync<string>("readHash"));
        if (state is null)
        {
            Load(PlaygroundSamples.Default.State);
            _sampleId = PlaygroundSamples.Default.Id;
        }
        else
        {
            Load(state);
            _sampleId = PlaygroundSamples.All.FirstOrDefault(s => s.State.ToHash() == state.ToHash())?.Id ?? "";
        }

        _showOptions = _changedOptions > 0 && await _module.InvokeAsync<bool>("isWide");
        RunNow();
        _readyMs = await _module.InvokeAsync<double>("markReady");
        StateHasChanged();
    }

    private void Load(PlaygroundState state)
    {
        _yaml = state.Yaml;
        _view = state.View;
        _options = state.Options;
        _changedOptions = CountChanged(_options);
    }

    private void RunNow()
    {
        _result = PlaygroundEngine.Run(_yaml, _options, _view);
        _outputHtml = _result.Error is null && _view is PlaygroundView.Yaml or PlaygroundView.Json && _result.Output.Length is > 0 and <= MaxHighlightedLength
            ? Highlighter.ToHtml(_result.Output, _view == PlaygroundView.Json ? PlaygroundLanguage.Json : PlaygroundLanguage.Yaml)
            : null;
    }

    /// <summary>Outputs longer than this are shown without colors, which would take too long.</summary>
    private const int MaxHighlightedLength = 200_000;

    // Runs again after a pause, so that typing doesn't run on every key.
    private async Task ScheduleRunAsync(bool custom = true)
    {
        if (custom)
        {
            _sampleId = "";
        }

        _pending?.Cancel();
        var pending = _pending = new CancellationTokenSource();
        try
        {
            await Task.Delay(RunDelayMs, pending.Token);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        RunNow();
        StateHasChanged();
        await ScheduleHashAsync();
    }

    private async Task ScheduleHashAsync()
    {
        _pendingHash?.Cancel();
        var pending = _pendingHash = new CancellationTokenSource();
        try
        {
            await Task.Delay(HashDelayMs, pending.Token);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        if (_module is not null)
        {
            await _module.InvokeVoidAsync("writeHash", State.ToHash());
        }
    }

    private Task OnYamlChanged(string value)
    {
        _yaml = value;
        return ScheduleRunAsync();
    }

    private async Task OnViewChanged(PlaygroundView view)
    {
        _view = view;
        RunNow();
        await ScheduleHashAsync();
    }

    private async Task OnSampleChanged(ChangeEventArgs e)
    {
        var sample = PlaygroundSamples.All.FirstOrDefault(s => s.Id == e.Value?.ToString());
        if (sample is null)
        {
            return;
        }

        Load(sample.State);
        _sampleId = sample.Id;
        RunNow();
        await ScheduleHashAsync();
    }

    private Task Set(PlaygroundOptions options)
    {
        _options = options;
        _changedOptions = CountChanged(options);
        return ScheduleRunAsync();
    }

    private Task ResetOptions() => Set(new PlaygroundOptions());

    private async Task ShareAsync()
    {
        if (_module is null)
        {
            return;
        }

        await _module.InvokeVoidAsync("writeHash", State.ToHash());
        _shareLabel = await _module.InvokeAsync<bool>("copyText", await _module.InvokeAsync<string>("currentUrl")) ? "Link copied" : "Copy failed";
        StateHasChanged();
        await Task.Delay(1600);
        _shareLabel = "Share";
    }

    private async Task CopyOutputAsync()
    {
        if (_module is null || _result is null)
        {
            return;
        }

        _copyLabel = await _module.InvokeAsync<bool>("copyText", _result.Output) ? "Copied" : "Copy failed";
        StateHasChanged();
        await Task.Delay(1600);
        _copyLabel = "Copy";
    }

    private async Task ToggleThemeAsync()
    {
        if (_module is not null)
        {
            await _module.InvokeAsync<string>("toggleTheme");
        }
    }

    // The options are compared one by one, without reflection, which the trimmer would need to be told about.
    private static int CountChanged(PlaygroundOptions options)
    {
        var d = new PlaygroundOptions();
        bool[] changed =
        [
            options.Schema != d.Schema, options.DuplicateKeyHandling != d.DuplicateKeyHandling, options.ResolveMergeKeys != d.ResolveMergeKeys,
            options.CommentHandling != d.CommentHandling, options.MaxDepth != d.MaxDepth, options.MaxAliasExpansion != d.MaxAliasExpansion,
            options.IndentSize != d.IndentSize, options.IndentSequences != d.IndentSequences, options.ExpandSequenceItems != d.ExpandSequenceItems,
            options.MaxLineWidth != d.MaxLineWidth, options.MultiLineStringStyle != d.MultiLineStringStyle, options.DefaultScalarStyle != d.DefaultScalarStyle,
            options.QuoteForLenientReaders != d.QuoteForLenientReaders, options.IgnoreSourceStyles != d.IgnoreSourceStyles, options.JsonIndented != d.JsonIndented,
        ];
        return changed.Count(c => c);
    }

    private static bool IsChecked(ChangeEventArgs e) => e.Value is true || (e.Value is string s && bool.TryParse(s, out var b) && b);

    private static T Parse<T>(ChangeEventArgs e) where T : struct, Enum =>
        Enum.TryParse<T>(e.Value?.ToString(), out var value) ? value : default;

    private static int ToInt(ChangeEventArgs e, int fallback) =>
        int.TryParse(e.Value?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value >= 0 ? value : fallback;

    private static string Ms(TimeSpan time) => time.TotalMilliseconds.ToString(time.TotalMilliseconds < 10 ? "0.00" : "0", CultureInfo.InvariantCulture) + " ms";

    public async ValueTask DisposeAsync()
    {
        _pending?.Cancel();
        _pendingHash?.Cancel();
        if (_module is not null)
        {
            await _module.DisposeAsync();
        }
    }
}
