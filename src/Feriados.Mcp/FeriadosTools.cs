using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Feriados.Mcp;

[McpServerToolType]
public sealed class FeriadosTools
{
    [McpServerTool(Name = "consultar_feriados_nacionais", ReadOnly = true)]
    [Description("Consulta os feriados nacionais do Brasil em um ano usando a BrasilAPI. Use quando a pergunta pedir feriados ou suas datas.")]
    public static async Task<string> ConsultarFeriadosNacionais(
        [Description("Ano entre 1900 e 2199.")] int ano,
        FeriadosApiClient api,
        CancellationToken cancellationToken)
    {
        if (ano is < 1900 or > 2199)
        {
            throw new McpException("O ano deve estar entre 1900 e 2199.");
        }

        try
        {
            var feriados = await api.ConsultarAsync(ano, cancellationToken);
            var resultado = new
            {
                ano,
                fonte = $"https://brasilapi.com.br/api/feriados/v1/{ano}",
                feriados
            };

            // O texto JSON mantém os dados da API separados da explicação gerada pelo modelo.
            return JsonSerializer.Serialize(resultado, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }
        catch (FeriadosConsultaException ex)
        {
            throw new McpException($"Não foi possível consultar os feriados de {ano}: {ex.Message}", ex);
        }
    }
}
