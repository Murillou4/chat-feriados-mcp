using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using OllamaSharp;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://127.0.0.1:5200");

var ollamaEndpoint = builder.Configuration["Ollama:Endpoint"]
    ?? throw new InvalidOperationException("Configure Ollama:Endpoint.");
var model = builder.Configuration["Ollama:Model"]
    ?? throw new InvalidOperationException("Configure Ollama:Model.");
var mcpEndpoint = builder.Configuration["Mcp:Endpoint"]
    ?? throw new InvalidOperationException("Configure Mcp:Endpoint.");
var fusoBrasil = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

builder.Services.AddSingleton<IChatClient>(_ =>
    new ChatClientBuilder(new OllamaApiClient(new Uri(ollamaEndpoint), model))
        .UseFunctionInvocation()
        .Build());

var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapPost("/api/chat", async (ChatRequest request, IChatClient chatClient,
    ILogger<Program> logger, CancellationToken cancellationToken) =>
{
    if (request.Messages is null || request.Messages.Count == 0)
        return Results.BadRequest(new { error = "Envie pelo menos uma mensagem." });

    // O navegador guarda a conversa; o servidor recebe apenas o trecho recente.
    var recentMessages = request.Messages.TakeLast(12).ToList();
    if (recentMessages.Any(message =>
            message is null ||
            (message.Role != "user" && message.Role != "assistant") ||
            string.IsNullOrWhiteSpace(message.Content) ||
            message.Content.Length > (message.Role == "user" ? 2000 : 12000)) ||
        recentMessages[^1].Role != "user")
    {
        return Results.BadRequest(new { error = "Histórico inválido. Cada pergunta pode ter até 2000 caracteres; termine com uma pergunta." });
    }

    McpClient mcpClient;
    try
    {
        var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri(mcpEndpoint),
            TransportMode = HttpTransportMode.StreamableHttp,
            ConnectionTimeout = TimeSpan.FromSeconds(10)
        });
        mcpClient = await McpClient.CreateAsync(transport, cancellationToken: cancellationToken);
    }
    catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
    {
        logger.LogWarning(exception, "Não foi possível conectar ao servidor MCP.");
        return Results.Json(new { error = "O servidor MCP está indisponível. Inicie Feriados.Mcp e tente novamente." }, statusCode: 503);
    }

    await using (mcpClient)
    {
        try
        {
            var tools = await mcpClient.ListToolsAsync(cancellationToken: cancellationToken);
            var hoje = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, fusoBrasil).DateTime);
            var consulta = ConsultaFeriadosHelper.Resolver(recentMessages, hoje);
            var history = new List<ChatMessage>
            {
                new(ChatRole.System, $"""
                    Responda em português, de modo claro e breve, em texto simples sem Markdown. Quando a pergunta pedir feriados nacionais
                    brasileiros de um ano ou desta semana, inclusive em perguntas de acompanhamento, chame a ferramenta
                    consultar_feriados_nacionais antes de responder. Se a pergunta atual trouxer um novo ano,
                    consulte esse ano de novo; nunca reutilize datas de um ano anterior da conversa. Use somente
                    os dados retornados pela ferramenta nesta pergunta para datas e nomes. Se a consulta falhar, explique que não conseguiu confirmar
                    os dados. Para outros assuntos, responda sem chamar a ferramenta. Ao usar a ferramenta,
                    mencione que a fonte dos dados é a BrasilAPI. Escreva datas no formato dd/MM/aaaa.
                    Hoje no Brasil é {hoje:dd/MM/yyyy}. "Desse ano" significa {hoje.Year}; "dessa semana" significa
                    a semana de segunda a domingo que contém a data de hoje. Não peça o ano ao usuário para essas expressões.
                    """)
            };

            foreach (var message in recentMessages)
                history.Add(new ChatMessage(
                    message.Role == "user" ? ChatRole.User : ChatRole.Assistant,
                    message.Content));

            // O SDK MCP entrega as ferramentas ao cliente de IA como funções invocáveis.
            var response = await chatClient.GetResponseAsync(history,
                new ChatOptions { Tools = [.. tools] }, cancellationToken);
            var functionResults = response.Messages
                .SelectMany(message => message.Contents)
                .OfType<FunctionResultContent>()
                .ToList();
            var usedMcp = functionResults.Count > 0;

            var consultaRelativa = consulta is not null &&
                !Regex.IsMatch(recentMessages[^1].Content, @"\b\d{4}\b");
            if (consulta is not null && (consultaRelativa || !usedMcp))
            {
                // Expressões relativas precisam do ano atual e, no caso da semana, do recorte exato dos dados da API.
                var anos = new List<int> { consulta.Ano };
                if (consulta.FimSemana is DateOnly fim && fim.Year != consulta.Ano)
                    anos.Add(fim.Year);

                var feriados = new List<FeriadoDaResposta>();
                foreach (var ano in anos)
                {
                    var result = await mcpClient.CallToolAsync("consultar_feriados_nacionais",
                        new Dictionary<string, object?> { ["ano"] = ano }, cancellationToken: cancellationToken);
                    if (result.IsError == true)
                        return Results.Json(new { error = "Não foi possível confirmar os feriados. Verifique o ano e a conexão com a BrasilAPI." }, statusCode: 502);

                    var json = result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text;
                    var dados = JsonSerializer.Deserialize<ResultadoFeriados>(json ?? "",
                        new JsonSerializerOptions(JsonSerializerDefaults.Web));
                    if (dados is null || dados.Ano != ano || dados.Feriados is null)
                        return Results.Json(new { error = "O servidor MCP devolveu uma resposta inválida." }, statusCode: 502);

                    feriados.AddRange(dados.Feriados);
                }

                var answer = consulta.InicioSemana is DateOnly inicioSemana && consulta.FimSemana is DateOnly fimSemana
                    ? ConsultaFeriadosHelper.MontarRespostaSemanal(feriados, inicioSemana, fimSemana)
                    : MontarRespostaFeriados(new ResultadoFeriados(consulta.Ano, feriados), recentMessages[^1].Content);
                return Results.Ok(new ChatReply(answer, true));
            }

            if (functionResults.Any(result => result.Result is JsonElement json &&
                    json.TryGetProperty("isError", out var error) && error.ValueKind == JsonValueKind.True))
            {
                return Results.Json(new { error = "Não foi possível confirmar os feriados. Verifique o ano e a conexão com a BrasilAPI." }, statusCode: 502);
            }

            if (string.IsNullOrWhiteSpace(response.Text))
                return Results.Json(new { error = "O modelo não gerou uma resposta. Tente reformular a pergunta." }, statusCode: 502);

            return Results.Ok(new ChatReply(response.Text, usedMcp));
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Falha ao processar o chat.");
            return Results.Json(new { error = "Não foi possível obter a resposta. Verifique se o Ollama e o servidor MCP estão ativos." }, statusCode: 503);
        }
    }
});

app.Run();

static string MontarRespostaFeriados(ResultadoFeriados dados, string pergunta)
{
    if (dados.Feriados.Count == 0)
        return $"A BrasilAPI não retornou feriados para {dados.Ano}.";

    var primeirasTres = Regex.IsMatch(pergunta, @"\b(?:três|3)\s+primeir", RegexOptions.IgnoreCase);
    var feriados = primeirasTres ? dados.Feriados.Take(3) : dados.Feriados;
    var linhas = feriados.Select(feriado =>
        $"{DateOnly.ParseExact(feriado.Data, "yyyy-MM-dd", CultureInfo.InvariantCulture):dd/MM/yyyy} - {feriado.Nome}");
    return $"Feriados nacionais de {dados.Ano} (BrasilAPI):\n{string.Join("\n", linhas)}";
}

internal sealed record ChatRequest(List<ChatInput>? Messages);
internal sealed record ChatInput(string Role, string Content);
internal sealed record ChatReply(string Answer, bool UsedMcp);
internal sealed record ResultadoFeriados(int Ano, List<FeriadoDaResposta> Feriados);
internal sealed record FeriadoDaResposta(string Data, string Nome);
