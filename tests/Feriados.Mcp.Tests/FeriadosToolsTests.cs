using System.Net;
using System.Text;
using System.Text.Json;
using Feriados.Mcp;
using ModelContextProtocol;
using Xunit;

namespace Feriados.Mcp.Tests;

public sealed class FeriadosToolsTests
{
    [Theory]
    [InlineData(1899)]
    [InlineData(2200)]
    public async Task RejeitaAnoForaDoIntervaloSemChamarApi(int ano)
    {
        var handler = new RespostaFalsa(HttpStatusCode.OK, "[]");
        var erro = await Assert.ThrowsAsync<McpException>(() =>
            FeriadosTools.ConsultarFeriadosNacionais(ano, CriarApi(handler), CancellationToken.None));

        Assert.Contains("1900 e 2199", erro.Message);
        Assert.Equal(0, handler.Chamadas);
    }

    [Fact]
    public async Task MapeiaDataNomeETipoDaBrasilApi()
    {
        var handler = new RespostaFalsa(HttpStatusCode.OK,
            """[{"date":"2026-01-01","name":"Confraternização Universal","type":"national"}]""");

        var resposta = await FeriadosTools.ConsultarFeriadosNacionais(
            2026, CriarApi(handler), CancellationToken.None);
        using var json = JsonDocument.Parse(resposta);

        Assert.Equal(2026, json.RootElement.GetProperty("ano").GetInt32());
        Assert.Equal("https://brasilapi.com.br/api/feriados/v1/2026",
            json.RootElement.GetProperty("fonte").GetString());
        var feriado = json.RootElement.GetProperty("feriados")[0];
        Assert.Equal("2026-01-01", feriado.GetProperty("data").GetString());
        Assert.Equal("Confraternização Universal", feriado.GetProperty("nome").GetString());
        Assert.Equal("national", feriado.GetProperty("tipo").GetString());
        Assert.Equal("https://brasilapi.com.br/api/feriados/v1/2026", handler.UltimaUrl);
    }

    [Fact]
    public async Task InformaFalhaHttpSemCriarFeriados()
    {
        var handler = new RespostaFalsa(HttpStatusCode.ServiceUnavailable, "{}");

        var erro = await Assert.ThrowsAsync<McpException>(() =>
            FeriadosTools.ConsultarFeriadosNacionais(2026, CriarApi(handler), CancellationToken.None));

        Assert.Contains("HTTP 503", erro.Message);
    }

    [Fact]
    public async Task RejeitaRespostaComCampoObrigatorioAusente()
    {
        var handler = new RespostaFalsa(HttpStatusCode.OK,
            """[{"date":"2026-01-01","name":"Confraternização Universal"}]""");

        var erro = await Assert.ThrowsAsync<McpException>(() =>
            FeriadosTools.ConsultarFeriadosNacionais(2026, CriarApi(handler), CancellationToken.None));

        Assert.Contains("dados incompletos ou inválidos", erro.Message);
    }

    private static FeriadosApiClient CriarApi(HttpMessageHandler handler) => new(new HttpClient(handler)
    {
        BaseAddress = new Uri("https://brasilapi.com.br/")
    });

    private sealed class RespostaFalsa(HttpStatusCode status, string corpo) : HttpMessageHandler
    {
        public int Chamadas { get; private set; }
        public string? UltimaUrl { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Chamadas++;
            UltimaUrl = request.RequestUri?.ToString();
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(corpo, Encoding.UTF8, "application/json")
            });
        }
    }
}
