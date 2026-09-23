using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Feriados.Mcp;

public sealed record Feriado(string Data, string Nome, string Tipo);

public sealed class FeriadosApiClient(HttpClient http)
{
    public async Task<IReadOnlyList<Feriado>> ConsultarAsync(int ano, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await http.GetAsync($"api/feriados/v1/{ano}", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new FeriadosConsultaException($"A BrasilAPI respondeu com HTTP {(int)response.StatusCode}.");
            }

            var dados = await response.Content.ReadFromJsonAsync<List<FeriadoDaApi>>(cancellationToken);
            if (dados is null || dados.Any(item => !EhValido(item, ano)))
            {
                throw new FeriadosConsultaException("A BrasilAPI devolveu dados incompletos ou inválidos.");
            }

            return dados.Select(item => new Feriado(item.Data!, item.Nome!, item.Tipo!)).ToArray();
        }
        catch (HttpRequestException ex)
        {
            throw new FeriadosConsultaException("Não foi possível acessar a BrasilAPI.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new FeriadosConsultaException("A consulta à BrasilAPI excedeu o tempo limite.", ex);
        }
        catch (JsonException ex)
        {
            throw new FeriadosConsultaException("A BrasilAPI devolveu JSON inválido.", ex);
        }
    }

    private static bool EhValido(FeriadoDaApi item, int ano) =>
        item is not null &&
        DateOnly.TryParseExact(item.Data, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var data) &&
        data.Year == ano &&
        !string.IsNullOrWhiteSpace(item.Nome) &&
        !string.IsNullOrWhiteSpace(item.Tipo);

    private sealed class FeriadoDaApi
    {
        [JsonPropertyName("date")]
        public string? Data { get; init; }

        [JsonPropertyName("name")]
        public string? Nome { get; init; }

        [JsonPropertyName("type")]
        public string? Tipo { get; init; }
    }
}

public sealed class FeriadosConsultaException : Exception
{
    public FeriadosConsultaException(string message) : base(message) { }
    public FeriadosConsultaException(string message, Exception innerException) : base(message, innerException) { }
}
