# Conferência da importação pela interface

Execute na raiz: `powershell -File scripts/Testar-ImportacaoLocal.ps1`.
O script cria uma pasta temporária única com XLSX fictício e usa somente um banco novo nessa pasta. Não reutiliza a conexão pessoal. A porta de teste é 5189; se estiver ocupada, encerre o teste e libere a porta antes de tentar novamente. Não acesse outra instância do FinanceControl durante esta conferência.

1. Abra `http://127.0.0.1:5189/importar` e envie o arquivo indicado pelo script.
2. Selecione somente OUTUBRO 2026 e clique em **Gerar prévia das abas selecionadas**: quatro linhas encontradas; duas selecionadas (despesa 100 e receita 250). A linha sem valor fica bloqueada e oculta pelo filtro inicial. Desative o filtro para vê-la. A linha com PAGUEI fica desmarcada. TOTAL não aparece.
3. Confirme somente as duas linhas inicialmente selecionadas. Esperado: dois registros cadastrados e nenhum duplicado.
4. Confira a prévia recarregada: essas duas linhas ficam duplicadas e desmarcadas. Envie o mesmo arquivo novamente: o resultado deve permanecer igual.
5. Abra o painel e selecione outubro/2026. Esperado: despesas previstas 100, receitas previstas 250, valores pagos/recebidos e saldo realizado zero; ambos os lançamentos pendentes.
6. Em um novo ambiente vazio (reinicie o script), selecione OUTUBRO 2026 e NOVEMBRO 2026, gere a prévia e confira oito linhas no conjunto, quatro selecionadas. Alterne em **Revisar aba**, desmarque uma linha e ajuste uma data: ao voltar à aba, as alterações devem permanecer. Restaure a seleção e a data para confirmar quatro registros; cada mês terá despesa 100 e receita 250, sem baixas.
7. Selecione JANEIRO: informe um ano antes de gerar a prévia e confira a competência. A aba VALOR TOTAL DE DÍVIDAS deve ficar desabilitada. Alterar ano ou seleção de abas deve remover a prévia anterior até gerar outra.
8. Confira a navegação em uma aba com mais de seis linhas válidas: somente seis por página, com anterior/próxima preservando seleções. A fixture padrão contém quatro linhas por aba; use uma cópia sintética ampliada para esta etapa.
9. Encerre com Ctrl+C. Os arquivos fictícios permanecem na pasta temporária indicada para inspeção. Uma nova execução cria outro ambiente vazio.

Este roteiro não substitui os testes automatizados (`dotnet test FinanceControl.slnx`). O fluxo de upload, cliques, atualização visual e responsividade precisa ser conferido em navegador.
