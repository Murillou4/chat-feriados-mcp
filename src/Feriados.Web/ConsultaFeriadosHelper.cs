using System.Globalization;
using System.Text.RegularExpressions;

internal static class ConsultaFeriadosHelper
{
    internal static ConsultaFeriados? Resolver(IReadOnlyList<ChatInput> messages, DateOnly hoje)
    {
        var pergunta = messages[^1].Content;
        var assuntoEhFeriados = messages.TakeLast(3).Any(message =>
            message.Role == "user" && message.Content.Contains("feriad", StringComparison.OrdinalIgnoreCase));
        if (!assuntoEhFeriados)
            return null;

        var anoExplicito = Regex.Match(pergunta, @"\b\d{4}\b");
        if (anoExplicito.Success)
            return new ConsultaFeriados(int.Parse(anoExplicito.Value, CultureInfo.InvariantCulture), null, null);

        if (Regex.IsMatch(pergunta,
                @"\b(?:dessa|desta|nessa|nesta|essa|esta)\s+semana\b|\bsemana\s+atual\b",
                RegexOptions.IgnoreCase))
        {
            var diasDesdeSegunda = ((int)hoje.DayOfWeek + 6) % 7;
            var inicio = hoje.AddDays(-diasDesdeSegunda);
            return new ConsultaFeriados(inicio.Year, inicio, inicio.AddDays(6));
        }

        if (Regex.IsMatch(pergunta,
                @"\b(?:desse|deste|nesse|neste|esse|este)\s+ano\b|\bano\s+atual\b",
                RegexOptions.IgnoreCase))
            return new ConsultaFeriados(hoje.Year, null, null);

        return null;
    }

    internal static string MontarRespostaSemanal(IEnumerable<FeriadoDaResposta> feriados,
        DateOnly inicio, DateOnly fim)
    {
        var noPeriodo = feriados
            .Select(feriado => new
            {
                Data = DateOnly.ParseExact(feriado.Data, "yyyy-MM-dd", CultureInfo.InvariantCulture),
                feriado.Nome
            })
            .Where(feriado => feriado.Data >= inicio && feriado.Data <= fim)
            .OrderBy(feriado => feriado.Data)
            .ToList();

        var periodo = $"{inicio:dd/MM/yyyy} a {fim:dd/MM/yyyy}";
        if (noPeriodo.Count == 0)
            return $"Não há feriados nacionais nesta semana ({periodo}), segundo a BrasilAPI.";

        var linhas = noPeriodo.Select(feriado => $"{feriado.Data:dd/MM/yyyy} - {feriado.Nome}");
        return $"Feriados nacionais desta semana ({periodo}), segundo a BrasilAPI:\n{string.Join("\n", linhas)}";
    }
}

internal sealed record ConsultaFeriados(int Ano, DateOnly? InicioSemana, DateOnly? FimSemana);
