(() => {
  "use strict";

  const form = document.querySelector("#chat-form");
  const input = document.querySelector("#message-input");
  const sendButton = document.querySelector("#send-button");
  const sendLabel = document.querySelector("#send-label");
  const clearButton = document.querySelector("#clear-chat");
  const messages = document.querySelector("#messages");
  const emptyState = document.querySelector("#empty-state");
  const errorBanner = document.querySelector("#error-banner");
  const errorText = document.querySelector("#error-text");

  // O histórico vive somente nesta aba; o servidor recebe apenas as 12 mensagens mais recentes.
  const history = [];
  let waiting = false;

  function resizeInput() {
    input.style.height = "auto";
    input.style.height = `${Math.min(input.scrollHeight, 160)}px`;
  }

  function updateControls() {
    sendButton.disabled = waiting || !input.value.trim();
    clearButton.disabled = waiting || history.length === 0;
    input.disabled = waiting;
    sendLabel.textContent = waiting ? "Aguarde" : "Enviar";
    form.setAttribute("aria-busy", String(waiting));
  }

  function scrollToLatest() {
    requestAnimationFrame(() => { messages.scrollTop = messages.scrollHeight; });
  }

  function appendMessage(role, content, usedMcp = false) {
    emptyState.hidden = true;

    const item = document.createElement("article");
    item.className = `message message--${role}`;

    const meta = document.createElement("div");
    meta.className = "message__meta";
    meta.textContent = role === "user" ? "Você" : "Assistente";

    const text = document.createElement("p");
    text.className = "message__text";
    text.textContent = content;
    item.append(meta, text);
    if (usedMcp) {
      const source = document.createElement("span");
      source.className = "message__source";
      source.textContent = "Consultado via MCP";
      item.append(source);
    }

    messages.append(item);
    scrollToLatest();
    return item;
  }

  function appendPending() {
    const item = appendMessage("assistant", "Preparando resposta...");
    item.classList.add("message--pending");
    return item;
  }

  function showError(message) {
    errorText.textContent = message;
    errorBanner.hidden = false;
  }

  function readServerError(payload, status) {
    const detail = payload?.error ?? payload?.message ?? payload?.detail;
    if (typeof detail === "string" && detail.trim()) return detail.trim();
    if (status === 503) return "O serviço de IA está indisponível. Confira se o Ollama e o servidor MCP estão em execução.";
    if (status === 502 || status === 504) return "A consulta não foi concluída. Confira o servidor MCP e a conexão com a BrasilAPI.";
    return "Não foi possível concluir a pergunta. Tente novamente.";
  }

  async function send(question) {
    if (waiting || !question.trim()) return;

    const content = question.trim();
    errorBanner.hidden = true;
    input.value = "";
    resizeInput();
    waiting = true;
    updateControls();

    const userMessage = appendMessage("user", content);
    const pendingMessage = appendPending();

    try {
      const outgoing = [...history, { role: "user", content }].slice(-12);
      let response;
      try {
        response = await fetch("/api/chat", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ messages: outgoing })
        });
      } catch {
        throw new Error("Não foi possível conectar ao aplicativo. Confira se o servidor web está em execução.");
      }

      const payload = await response.json().catch(() => null);
      if (!response.ok) throw new Error(readServerError(payload, response.status));

      const answer = typeof payload?.answer === "string" ? payload.answer.trim() : "";
      if (!answer) throw new Error("O assistente respondeu sem texto. Tente novamente.");

      pendingMessage.remove();
      appendMessage("assistant", answer, payload.usedMcp === true);
      history.push({ role: "user", content }, { role: "assistant", content: answer });
    } catch (error) {
      // Devolve a pergunta ao campo para que a pessoa possa tentar novamente sem reescrever.
      pendingMessage.remove();
      userMessage.remove();
      if (history.length === 0) emptyState.hidden = false;
      input.value = content;
      resizeInput();
      showError(error instanceof Error ? error.message : "Não foi possível concluir a pergunta.");
    } finally {
      waiting = false;
      updateControls();
      input.focus();
      scrollToLatest();
    }
  }

  form.addEventListener("submit", event => {
    event.preventDefault();
    void send(input.value);
  });

  input.addEventListener("input", () => {
    resizeInput();
    updateControls();
    errorBanner.hidden = true;
  });

  input.addEventListener("keydown", event => {
    if (event.key === "Enter" && !event.shiftKey && !event.isComposing) {
      event.preventDefault();
      form.requestSubmit();
    }
  });

  clearButton.addEventListener("click", () => {
    history.length = 0;
    messages.querySelectorAll(".message").forEach(message => message.remove());
    emptyState.hidden = false;
    errorBanner.hidden = true;
    input.value = "";
    resizeInput();
    updateControls();
    input.focus();
  });

  updateControls();
})();
