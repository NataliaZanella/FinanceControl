# Conferência da importação pela interface

Execute na raiz: `powershell -File scripts/Testar-ImportacaoLocal.ps1`.
O script cria uma pasta temporária única com XLSX fictício e usa somente um banco novo nessa pasta. Não reutiliza a conexão pessoal. A porta de teste é 5189; se estiver ocupada, encerre o teste e libere a porta antes de tentar novamente. Não acesse outra instância do FinanceControl durante esta conferência.

1. Abra `http://127.0.0.1:5189/importar` e envie o arquivo indicado pelo script.
2. Confira a aba OUTUBRO 2026: quatro linhas encontradas; duas selecionadas (despesa 100 e receita 250). A linha sem valor fica bloqueada e oculta pelo filtro inicial. Desative o filtro para vê-la. A linha com PAGUEI fica desmarcada. TOTAL não aparece.
3. Confirme somente as duas linhas inicialmente selecionadas. Esperado: dois registros cadastrados e nenhum duplicado.
4. Confira a prévia recarregada: essas duas linhas ficam duplicadas e desmarcadas. Envie o mesmo arquivo novamente: o resultado deve permanecer igual.
5. Abra o painel e selecione outubro/2026. Esperado: despesas previstas 100, receitas previstas 250, valores pagos/recebidos e saldo realizado zero; ambos os lançamentos pendentes.
6. Encerre com Ctrl+C. Os arquivos fictícios permanecem na pasta temporária indicada para inspeção. Uma nova execução cria outro ambiente vazio.

Este roteiro não substitui os testes automatizados (`dotnet test FinanceControl.slnx`). O fluxo de upload, cliques, atualização visual e responsividade precisa ser conferido em navegador.
