using Xunit;

public sealed class ConsultaFeriadosHelperTests
{
    [Fact]
    public void InterpretaFeriadosDessaSemanaSemPedirAno()
    {
        var mensagens = new List<ChatInput>
        {
            new("user", "Olá, poderia me falar os feriados dessa semana ?")
        };

        var consulta = ConsultaFeriadosHelper.Resolver(mensagens, new DateOnly(2026, 9, 24));

        Assert.NotNull(consulta);
        Assert.Equal(2026, consulta.Ano);
        Assert.Equal(new DateOnly(2026, 9, 21), consulta.InicioSemana);
        Assert.Equal(new DateOnly(2026, 9, 27), consulta.FimSemana);
    }

    [Fact]
    public void InterpretaFeriadosDesseAnoSemPedirAno()
    {
        var mensagens = new List<ChatInput>
        {
            new("user", "Me fala os feriados desse ano")
        };

        var consulta = ConsultaFeriadosHelper.Resolver(mensagens, new DateOnly(2026, 9, 24));

        Assert.NotNull(consulta);
        Assert.Equal(2026, consulta.Ano);
        Assert.Null(consulta.InicioSemana);
        Assert.Null(consulta.FimSemana);
    }

    [Fact]
    public void UsaAnoAtualMesmoQuandoAFonteDaConversaEraOutroAno()
    {
        var mensagens = new List<ChatInput>
        {
            new("user", "Quais são os feriados de 2025?"),
            new("assistant", "Feriados nacionais de 2025..."),
            new("user", "E os feriados desse ano?")
        };

        var consulta = ConsultaFeriadosHelper.Resolver(mensagens, new DateOnly(2026, 9, 24));

        Assert.NotNull(consulta);
        Assert.Equal(2026, consulta.Ano);
    }

    [Fact]
    public void MantemSemanaInteiraQuandoCruzaAViradaDoAno()
    {
        var mensagens = new List<ChatInput>
        {
            new("user", "Tem feriado nesta semana?")
        };

        var consulta = ConsultaFeriadosHelper.Resolver(mensagens, new DateOnly(2026, 12, 31));

        Assert.NotNull(consulta);
        Assert.Equal(2026, consulta.Ano);
        Assert.Equal(new DateOnly(2026, 12, 28), consulta.InicioSemana);
        Assert.Equal(new DateOnly(2027, 1, 3), consulta.FimSemana);
    }

    [Fact]
    public void AnoEscritoNaPerguntaTemPrioridadeSobreOAnoAtual()
    {
        var mensagens = new List<ChatInput>
        {
            new("user", "Quais são os feriados de 2027?")
        };

        var consulta = ConsultaFeriadosHelper.Resolver(mensagens, new DateOnly(2026, 9, 24));

        Assert.NotNull(consulta);
        Assert.Equal(2027, consulta.Ano);
        Assert.Null(consulta.InicioSemana);
    }

    [Fact]
    public void SaudacaoNaoViraConsultaDeFeriados()
    {
        var mensagens = new List<ChatInput>
        {
            new("user", "Olá, tudo bem?")
        };

        Assert.Null(ConsultaFeriadosHelper.Resolver(mensagens, new DateOnly(2026, 9, 24)));
    }

    [Fact]
    public void RespostaSemanalIncluiSoDatasDentroDoIntervalo()
    {
        var feriados = new[]
        {
            new FeriadoDaResposta("2026-09-20", "Antes da semana"),
            new FeriadoDaResposta("2026-09-21", "No começo"),
            new FeriadoDaResposta("2026-09-27", "No fim"),
            new FeriadoDaResposta("2026-09-28", "Depois da semana")
        };

        var resposta = ConsultaFeriadosHelper.MontarRespostaSemanal(
            feriados, new DateOnly(2026, 9, 21), new DateOnly(2026, 9, 27));

        Assert.Contains("No começo", resposta);
        Assert.Contains("No fim", resposta);
        Assert.DoesNotContain("Antes da semana", resposta);
        Assert.DoesNotContain("Depois da semana", resposta);
    }

    [Fact]
    public void RespostaSemanalConsideraFeriadoDoAnoSeguinte()
    {
        var feriados = new[]
        {
            new FeriadoDaResposta("2026-12-25", "Natal"),
            new FeriadoDaResposta("2027-01-01", "Confraternização Universal")
        };

        var resposta = ConsultaFeriadosHelper.MontarRespostaSemanal(
            feriados, new DateOnly(2026, 12, 28), new DateOnly(2027, 1, 3));

        Assert.Contains("01/01/2027", resposta);
        Assert.Contains("Confraternização Universal", resposta);
        Assert.DoesNotContain("Natal", resposta);
    }

    [Fact]
    public void RespostaSemFeriadosInformaPeriodoEOrigemDaConsulta()
    {
        var resposta = ConsultaFeriadosHelper.MontarRespostaSemanal(
            Array.Empty<FeriadoDaResposta>(), new DateOnly(2026, 9, 21), new DateOnly(2026, 9, 27));

        Assert.Contains("21/09/2026", resposta);
        Assert.Contains("27/09/2026", resposta);
        Assert.Contains("BrasilAPI", resposta);
        Assert.Matches("(?i)(não há|nenhum|não foram encontrados)", resposta);
    }
}
