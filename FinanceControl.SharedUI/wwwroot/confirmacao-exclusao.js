const janelas = new WeakMap();

export function abrir(janela, componente) {
    const anterior = document.activeElement;
    const cancelar = event => {
        event.preventDefault();
        componente.invokeMethodAsync("SolicitarCancelamento");
    };
    janelas.set(janela, { anterior, cancelar });
    janela.addEventListener("cancel", cancelar);
    // O diálogo nativo mantém o foco dentro da janela e bloqueia o fundo.
    janela.showModal();
}

export function fechar(janela) {
    const estado = janelas.get(janela);
    if (!estado) return;
    janela.removeEventListener("cancel", estado.cancelar);
    if (janela.open) janela.close();
    if (estado.anterior?.isConnected) estado.anterior.focus({ preventScroll: true });
    janelas.delete(janela);
}
