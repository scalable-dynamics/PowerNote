using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;
using PowerNote;
using PowerNote.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddSingleton(services => (IJSInProcessRuntime)services.GetRequiredService<IJSRuntime>());
builder.Services.AddSingleton<FormulaJsHost>();
builder.Services.AddSingleton<PowerFxHost>();
builder.Services.AddSingleton<FileManager>();
builder.Services.AddSingleton<AppPreviewer>();
builder.Services.AddSingleton<AppReader>();
builder.Services.AddSingleton<ExcelReader>();
builder.Services.AddSingleton<YamlReader>();
builder.Services.AddSingleton<UrlManager>();

await builder.Build().RunAsync();
