# Edição e exclusão de lançamentos

Implementadas na branch `developer`, sem alterações de esquema. UI em `SharedUI`, contratos em `Application`, regras no serviço compartilhado em `Infrastructure` e acesso HTTP em `Web`. O futuro cliente Windows poderá consumir as mesmas operações pela API.

## Regras

- **Editar** carrega o lançamento no formulário lateral. Permite descrição, categoria, valor previsto, vencimento e observações. Sem baixas, também permite tipo e competência. Ao salvar em outra competência, o painel acompanha o mês de destino.
- A edição mantém IDs, datas, valores e observações das baixas, bem como a origem da importação. O valor previsto deve ser positivo, ter até duas casas decimais e ser maior ou igual ao realizado. Um previsto igual ao realizado deixa o lançamento quitado; aumentá-lo pode voltar a deixá-lo parcial.
- Com qualquer baixa existente, mudar tipo ou competência é bloqueado, para evitar reclassificar pagamentos/recebimentos no histórico.
- **Cancelar edição** descarta somente o formulário. Trocar de mês ou abrir outra operação também encerra a edição sem salvar.
- **Excluir** abre uma janela modal centralizada na tela, com descrição, valor e competência, fundo escurecido e foco restrito à confirmação. Cancelar ou Escape fecha sem gravar; durante a exclusão, o fechamento fica bloqueado. O primeiro clique não grava nada. O botão de confirmação informa que a exclusão é permanente.
- A tabela mostra seis lançamentos por página, com Anterior/Próxima e indicação da página e intervalo. Os indicadores continuam representando todo o mês. Trocar o mês volta à primeira página; excluir o último item da última página ajusta a navegação para a página anterior disponível.
- Lançamentos com baixas não podem ser excluídos, inclusive via API. Não há estorno implementado nesta etapa.
- A exclusão de um lançamento sem baixas também remove sua origem de importação. A linha original pode ser importada novamente se não houver outro lançamento correspondente.
- Edição, registro de baixa e exclusão fazem leitura e escrita dentro de transações SQLite e consultam dados atuais sem reutilizar entidades rastreadas para validar saldo. Ainda não há aviso de conflito entre duas edições dos mesmos campos: a última edição válida prevalece.

## API local

`PUT /api/lancamentos/{id}` recebe os mesmos campos do cadastro: `descricao`, `categoria`, `tipo`, `valorPrevisto`, `competencia`, `dataVencimento`, `observacoes`. É uma atualização completa, não um PATCH.

Retornos: 200 com lançamento atualizado; 400 para validação; 404 para lançamento inexistente. `DELETE` retorna 204 quando exclui, 404 quando não existe e 400 quando há baixas. A confirmação é uma etapa da interface; consumidores da API devem apresentar sua própria confirmação antes do DELETE.

## Conferência manual em ambiente isolado

Execute `powershell -File scripts/Testar-ImportacaoLocal.ps1` na raiz. Use `http://127.0.0.1:5189/` e somente dados fictícios. O script cria um banco temporário e compila em uma pasta separada.

1. Cadastre uma despesa fictícia de 100. Clique em Editar, altere descrição e valor para 120 e salve. Confira tabela e indicadores.
2. Abra Editar, mude campos e cancele. Confira que os dados persistidos continuam iguais.
3. Sem baixas, altere a competência para outro mês. O painel deve acompanhar esse mês; ao voltar ao mês original, o lançamento não deve aparecer.
4. Registre uma baixa de 40. Edite a categoria ou descrição: o histórico deve permanecer igual. Tipo e competência ficam desabilitados.
5. Tente salvar previsto 30: deve haver erro e nenhuma alteração. Salve previsto 40: deve ficar Quitado. Aumente para 80: deve ficar Parcial, com realizado 40 e pendente 40.
6. Confira que Excluir fica desabilitado para esse lançamento com baixa.
7. Cadastre outro lançamento sem baixas. Clique em Excluir e depois Cancelar: ele deve continuar na tabela. Repita e confirme: somente esse lançamento deve desaparecer, e os indicadores devem atualizar.
8. Edite um lançamento importado no ambiente sintético e envie novamente a mesma planilha: sua origem deve continuar bloqueada como duplicada.
9. Cadastre pelo menos sete lançamentos fictícios no mesmo mês. Confira que só seis aparecem na primeira página e que Próxima mostra os restantes. Os indicadores não devem mudar ao navegar. Na última página, exclua seus itens sem baixas até esvaziá-la: a tabela deve voltar para uma página válida.
10. Abra Excluir com a página rolada: a confirmação deve aparecer no centro da área visível. Confira Cancelar, Escape, navegação por Tab e foco retornando ao botão de origem quando ele ainda existir. Faça também a conferência em uma janela estreita.

Encerre com Ctrl+C. Não há desfazer para exclusão; em uso real, restauração depende de backup consistente feito com a aplicação parada. Nenhuma exclusão em dados pessoais foi executada durante o desenvolvimento.

## Validação de desenvolvimento

- Testes automatizados com SQLite em memória cobrem alterações válidas e inválidas, histórico, origem de importação, mudança de competência, exclusão sem baixas, bloqueio com baixas e releitura após outra sessão.
- Conferência HTTP com banco sintético: PUT 200/400/404, DELETE 204/400/404, histórico preservado e dashboard 200 com as ações Editar/Excluir no HTML.
- Os cliques, cancelamento e apresentação visual dos novos controles ainda exigem a conferência manual acima; não foram automatizados em navegador nesta sessão.
