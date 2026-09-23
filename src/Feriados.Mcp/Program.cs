using Feriados.Mcp;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Server;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://127.0.0.1:5201");

builder.Services.AddHttpClient<FeriadosApiClient>(http =>
{
    http.BaseAddress = new Uri("https://brasilapi.com.br/");
    http.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddMcpServer()
    .WithHttpTransport(options => options.SessionMode = HttpServerSessionMode.Stateless)
    .WithTools<FeriadosTools>();

var app = builder.Build();
app.MapMcp("/mcp");
app.Run();
