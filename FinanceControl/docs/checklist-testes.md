# Roteiro de testes manuais (MVP)

- [ ] `dotnet restore` e `dotnet build` concluem sem erros.
- [ ] A página abre em http://127.0.0.1:5178.
- [ ] Criar despesa de R$ 1.000,00: prevista 1.000, paga 0, pendente 1.000.
- [ ] Registrar baixa de R$ 400,00: situação `Parcial`, pendente 600.
- [ ] Registrar baixa de R$ 600,00: situação `Quitado`, pendente 0.
- [ ] Verificar no histórico os dois registros, com datas e observações.
- [ ] Tentar registrar baixa acima do pendente: deve ser rejeitada.
- [ ] Criar receita prevista de R$ 500,00: recebida ainda é 0.
- [ ] Registrar recebimento parcial: o valor recebido aumenta, não o valor total previsto.
- [ ] Alterar competência para outro mês: lançamentos anteriores não aparecem.
- [ ] Parar e reiniciar o aplicativo: dados continuam no SQLite.
- [ ] Consultar `GET /api/lancamentos?mes=2026-10` e validar JSON.

Nota: testes automáticos e compilação em ambiente .NET serão realizados localmente; este pacote foi preparado sem SDK .NET no ambiente de geração.
