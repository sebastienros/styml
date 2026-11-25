using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Styml.Playground;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.Services.AddSingleton<PlaygroundHighlighter>();

await builder.Build().RunAsync();
