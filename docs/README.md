# Styml documentation site

The documentation published at https://sebastienros.github.io/styml/, built by
[Douze](https://sebastienros.github.io/douze/) from this folder. The playground, at `/styml/playground/`, is the
`playground/Styml.Playground` project. The other Markdown files of this folder (`options.md`, `release.md`,
`benchmarks/`...) are the repository's own documents; the site copies some of them (see below).

## Build and preview

Install Douze (a preview, published on every push to its `main` branch):

```shell
dotnet tool install -g douze --prerelease --add-source https://f.feedz.io/sebastienros/douze/nuget/index.json
```

From the root of the repository:

```shell
dotnet run --project eng/docs/ApiDocs -c Release -- docs/_content/styml/api   # the API reference (git-ignored)
douze serve -s docs                             # http://localhost:8080/styml/, rebuilt on every change
douze -s docs                                   # writes docs/_site; the site is docs/_site/styml
dotnet run --project playground/Styml.Playground  # the playground, at the root of its own server
```

The deployment (`.github/workflows/docs.yml`) runs the same steps, publishes the playground into
`docs/_site/styml/playground/`, and uploads `docs/_site/styml` to GitHub Pages. Pages must use the "GitHub Actions"
source in the repository settings.

## Layout

| Path | Content |
| --- | --- |
| `douze.yml` | The build workflow: front matter, Markdown, headings, sample checks, highlighting, layouts, link checks and search |
| `_content/styml/` | The pages, served under `/styml/` like GitHub Pages does |
| `_content/styml/guide/` | The guide, one Markdown file per page |
| `_content/styml/api/` | The API reference, written by `eng/docs/ApiDocs` from the library's XML documentation (git-ignored) |
| `_content/styml/assets/` | Styles, scripts and the logo (a copy of `assets/logo.svg`) |
| `_content/styml/playground/` | A placeholder replaced by the published playground |
| `_data/sidebar.yml` | The navigation of the guide; the API pages have theirs in the generated `api/api.yml` |
| `_includes/` | The layouts |

A page is a Markdown file with a `title` and a `description` in its front matter. Its content is not Liquid, so that
samples are written as they are. Link to other pages with relative links to their `.md` files: Douze turns them into
URLs and fails the build when a link or an anchor is broken. YAML and JSON fences are checked by Douze's `check-code`.

## Samples

The C# samples are not written in the pages. Each one is a region of a test in `test/Styml.Yaml.Tests/Docs/*.cs`,
between `// docs: <name> [language]` and `// docs: end`, and its output, when the marker names a language, is
`test/Styml.Yaml.Tests/Docs/expected/<name>.txt`, which the test checks. A page shows a sample with an empty block:

```markdown
<!-- sample:first-serialize -->
<!-- /sample:first-serialize -->
```

and `eng/docs/site.py` fills it with the code and the output. To add or change a sample:

```shell
STYML_DOCS_RECORD=$PWD/test/Styml.Yaml.Tests/Docs dotnet test test/Styml.Yaml.Tests -f net11.0 --filter "FullyQualifiedName~Tests.Docs"
python3 eng/docs/site.py
```

The first command records the outputs (review them: they are what the page shows), the second copies the samples into
the pages. `DocsSiteTests` and `python3 eng/docs/site.py --check` fail when a page and its samples drift. The same
script copies the options reference of `docs/options.md` and the benchmark panels of the README into the pages
(`<!-- include:options -->`, `<!-- include:benchmarks -->`).

The theme is derived from the documentation sites of Fluid and Douze (MIT).
