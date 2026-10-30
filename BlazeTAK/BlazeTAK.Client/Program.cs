using BlazeTAK.Client.Services;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthenticationStateDeserialization();

//chat over signalr hub (/hubs/chat)
//singleton: the connection (and its events) survives page navigation; the page must NOT dispose it
builder.Services.AddSingleton<ChatService>();

await builder.Build().RunAsync();