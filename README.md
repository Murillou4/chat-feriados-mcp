# Chat local com consulta de feriados via MCP

Chat em português com um modelo local no Ollama. Quando a conversa pede feriados nacionais de um ano, o sistema consulta a BrasilAPI por meio de uma ferramenta publicada por um servidor MCP em .NET 10.

```text
Navegador → Feriados.Web → Ollama (qwen3:4b-instruct)
                   ↕
              Feriados.Mcp → BrasilAPI
```

O modelo roda no computador. É preciso acesso à internet para instalar as dependências e consultar os feriados na BrasilAPI.

## Requisitos e instalação no Windows

1. Instale o [.NET SDK 10](https://learn.microsoft.com/pt-br/dotnet/core/install/windows). Pelo PowerShell, uma opção é:

   ```powershell
   winget install --id Microsoft.DotNet.SDK.10 --exact
   ```

2. Instale o [Ollama para Windows](https://ollama.com/download/windows) e abra o aplicativo.
3. Feche e abra o PowerShell após as instalações. Confirme que os comandos estão disponíveis e baixe o modelo usado pelo projeto:

   ```powershell
   dotnet --version
   ollama --version
   ollama pull qwen3:4b-instruct
   ollama list
   ```

`dotnet --version` deve mostrar uma versão 10.x, e `ollama list` deve incluir `qwen3:4b-instruct`. O modelo ocupa cerca de 2,5 GB; a primeira resposta pode demorar enquanto ele é carregado na memória. Se você instalou o SDK ou o Ollama em uma pasta personalizada e o terminal não encontra os comandos, use o caminho completo dos respectivos executáveis nos comandos abaixo.

## Rodar o projeto

Abra o PowerShell na raiz do repositório, onde está `Feriados.sln`:

```powershell
dotnet restore Feriados.sln
dotnet build Feriados.sln
dotnet test Feriados.sln
```

Mantenha o Ollama aberto. Em um terminal, inicie o servidor MCP:

```powershell
dotnet run --project src/Feriados.Mcp/Feriados.Mcp.csproj
```

Em outro terminal, inicie o chat:

```powershell
dotnet run --project src/Feriados.Web/Feriados.Web.csproj
```

Acesse **http://127.0.0.1:5200** no navegador. O MCP usa **http://127.0.0.1:5201/mcp** e o Ollama, por padrão, **http://127.0.0.1:11434**. Para parar cada serviço, pressione `Ctrl+C` no respectivo terminal.

Caso o Ollama não esteja atendendo na porta 11434, abra o aplicativo ou execute `ollama serve` em mais um terminal. Não inicie uma segunda instância se o aplicativo já estiver ativo.

## Testar a conversa

1. Envie “Olá”. A resposta não deve mostrar o indicador “Consultado via MCP”.
2. Envie “Quais são os feriados nacionais de 2026?”. O chat deve mostrar o indicador e listar dados que podem ser conferidos em `https://brasilapi.com.br/api/feriados/v1/2026`.
3. Envie “E em 2027?”. O sistema deve consultar o novo ano e mostrar o indicador novamente.

Os testes automatizados cobrem a validação do ano, a leitura dos dados da BrasilAPI e as falhas da consulta externa. Para executá-los novamente, use `dotnet test Feriados.sln`.

## Como funciona

O navegador guarda o histórico somente enquanto a página está aberta e envia as últimas 12 mensagens a `POST /api/chat`. O projeto `Feriados.Web` envia a conversa ao modelo local e lhe apresenta a ferramenta descoberta no servidor MCP. Quando a ferramenta `consultar_feriados_nacionais(ano)` é chamada, `Feriados.Mcp` valida o ano, consulta a BrasilAPI e devolve data, nome e tipo dos feriados.

O servidor Web também verifica se uma pergunta sobre feriados com ano explícito ficou sem consulta por decisão do modelo. Nesse caso, chama a ferramenta MCP e monta a resposta a partir dos dados recebidos. Se a consulta falhar, mostra um erro em vez de apresentar datas não confirmadas.

`POST /api/chat` recebe `{ "messages": [{ "role": "user", "content": "Quais são os feriados de 2026?" }] }` e devolve `{ "answer": "...", "usedMcp": true }`. A interface usa HTML, CSS e JavaScript em arquivos separados. Os dois projetos .NET separam a conversa da ferramenta MCP; não há banco de dados, login ou framework de frontend.

O modelo configurado em `src/Feriados.Web/appsettings.json` é `qwen3:4b-instruct`. Se você mudar o modelo ou os endereços do Ollama e do MCP, ajuste esse arquivo antes de iniciar o Web.

## Problemas comuns

| Sintoma | O que verificar |
| --- | --- |
| `dotnet` não é reconhecido ou mostra versão anterior à 10 | Reabra o PowerShell, confira a instalação do SDK 10 e use o caminho completo do `dotnet.exe` se necessário. |
| `ollama` não é reconhecido | Reabra o PowerShell ou use o caminho completo do `ollama.exe`. |
| O chat não recebe resposta do modelo | Confirme que o Ollama está ativo e que `ollama list` mostra `qwen3:4b-instruct`. |
| A consulta de feriados falha | Confirme que `Feriados.Mcp` está ativo na porta 5201 e que a [BrasilAPI](https://brasilapi.com.br/api/feriados/v1/2026) responde. |
| A porta 5200 ou 5201 já está em uso | Encerre a instância anterior do serviço que usa essa porta. |

Referências: [SDK MCP para C#](https://github.com/modelcontextprotocol/csharp-sdk), [Ollama](https://ollama.com/), [BrasilAPI](https://brasilapi.com.br/).
