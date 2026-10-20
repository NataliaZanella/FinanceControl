# Valores a receber por pessoa

Primeiro incremento de agrupamento de dívidas a receber, disponível em `/valores-a-receber`. Uma pessoa pode ter um grupo com vários itens, cada um com descrição, valor, competência e vencimento opcional. Nenhum dado pessoal foi cadastrado ou convertido durante o desenvolvimento.

## Funcionamento e limites

- **Novo grupo**: informe a pessoa, um título e o mês de referência; adicione cada valor separadamente, confira o total e confirme. O cadastro inicial aceita de 1 a 100 itens e é transacional: uma linha inválida ou falha no banco reverte o grupo inteiro.
- **Adicionar item** permite acrescentar novos valores ao grupo existente, com competência própria.
- O grupo mostra total devido, total recebido e saldo a receber de todas as competências. As listas de grupos e itens exibem seis por página.
- Cada item é um único lançamento do tipo Receita, categoria inicial Valores a receber. Valores e baixas permanecem nas tabelas existentes; as novas tabelas armazenam somente pessoa, título e vínculos. Os itens aparecem no painel mensal por competência e não são somados duas vezes.
- **Receber** registra uma baixa com data efetiva, valor e observação opcional. Aceita recebimentos parciais e bloqueia ultrapassar o saldo. O histórico é o mesmo disponível no painel. Receber pelo painel também atualiza o grupo.
- **Editar** na linha permite corrigir descrição, valor, competência, vencimento e observações na própria tela. Os recebimentos são preservados; o valor não pode ficar abaixo do recebido e a competência não muda quando já há recebimentos. Um item vinculado permanece como Receita e conserva a categoria. O botão Editar do painel também continua disponível.
- **Editar grupo** corrige a pessoa e o título sem recriar itens ou alterar recebimentos.
- **Excluir** um item sem recebimentos remove também o lançamento no painel e atualiza os totais. Os demais itens são preservados e o grupo pode ficar vazio.
- **Excluir grupo** remove permanentemente o grupo e todos os lançamentos vinculados, em uma transação. Qualquer recebimento em qualquer item bloqueia a exclusão inteira; grupos vazios podem ser excluídos.
- Toda exclusão pela interface exige confirmação em janela centralizada, com a descrição do que será removido. Cancelar ou Escape fecha sem gravar. As mesmas proteções financeiras valem na API, inclusive quando a tela estiver desatualizada.
- Não há juros, multas, estornos, parcelamento automático, distribuição automática de um recebimento entre vários itens ou importação da aba de dívidas. Não se deduz que um valor já foi recebido por estar em texto livre.
- O painel atual trata os recebimentos como entradas (Receitas), inclusive devolução de empréstimo; não apura lucro nem distingue principal de juros. O agrupamento não registra automaticamente a saída original do empréstimo.

## Lançamento antigo com valores somados na descrição

O cadastro pessoal antigo foi mantido intacto durante o desenvolvimento. **Não use Novo grupo para recadastrar valores já contabilizados**: use **Desmembrar lançamento** para revisar uma conversão.

1. Selecione o mês e uma receita de origem. Somente receitas sem recebimentos e ainda fora de grupos são elegíveis. Despesas não são reclassificadas automaticamente; caso a dívida esteja cadastrada como despesa, o significado desse registro precisa ser revisado antes de qualquer conversão.
2. Informe pessoa, título e de 2 a 100 itens manualmente. O sistema não extrai valores da descrição nem inventa datas. Todos os itens mantêm a competência original.
3. Confira original, total dos itens e diferença. A diferença deve ser zero. Se o valor cadastrado não corresponde à soma que você considera correta, pare e reconcilie o registro original; a conversão não ajusta o total automaticamente.
4. Confirme o desmembramento revisado. A gravação é atômica: o primeiro item reutiliza ID, data de criação e origem de importação do registro original; somente as demais partes geram novos lançamentos. O total previsto do mês permanece igual e nenhuma baixa é criada.
5. A categoria original é mantida em todos os itens. O primeiro preserva o vencimento antigo quando não é informado outro e mantém observações anteriores mais uma nota com descrição e total originais. Se a nota ultrapassar o limite de 1.000 caracteres, a conversão é bloqueada sem truncar ou perder conteúdo. Essa nota é informativa e editável; auditoria imutável ainda não existe.

A confirmação relê os dados no banco: origem já agrupada, recebimentos registrados depois da prévia, competência diferente ou soma divergente bloqueiam a operação. Repetir a confirmação não duplica o grupo. Falhas de validação ou armazenamento revertem todo o lote, inclusive a alteração do primeiro item.

A chave da importação fica preservada: reimportar o mesmo arquivo/aba/linha continua bloqueado. Se o item que guarda essa origem for excluído depois, a origem passa a outro item restante do grupo na mesma transação, protegendo a reimportação enquanto houver partes remanescentes. Excluir o grupo completo ou seu último item remove também a origem. A limitação anterior para arquivo renomeado permanece: a chave depende do nome do arquivo e a similaridade com a antiga soma pode não corresponder aos itens separados.

Não há botão para desfazer automaticamente a conversão. Antes de confirmar com dados reais, faça backup consistente do banco e revise cada valor. O backup automático da atualização de esquema não é gerado a cada desmembramento. Para voltar a uma situação anterior, pare a aplicação, preserve a versão atual do banco e planeje a restauração do backup; isso também afeta alterações posteriores ao backup.

## Atualização segura do SQLite

Foram adicionadas somente `GruposReceber` e `ItensGrupoReceber`, sem alterar colunas, valores, baixas ou origens das tabelas existentes. O procedimento está em `AtualizacaoValoresReceber.AplicarAsync`; ainda não representa a adoção completa de migrations do EF Core.

- Banco novo: `EnsureCreated()` cria o modelo completo.
- Banco anterior: na próxima inicialização, o sistema verifica as tabelas e gera, antes da adição, uma cópia SQLite consistente com `BackupDatabase` no mesmo diretório do banco. O nome termina em `.antes-valores-receber-<dataUTC>-<identificador>.db`.
- A cópia inclui dados ainda no WAL; não é uma cópia simples do arquivo em uso. Se o backup falhar, a atualização não continua.
- As duas tabelas e seus índices são adicionados em uma transação. Reiniciar após uma atualização concluída não cria novo backup nem apaga dados. Bancos SQLite em memória são exclusivos de teste e não precisam de arquivo de backup.
- Antes de usar com dados reais, pare as instâncias antigas e mantenha também um backup próprio. Não há API ou botão para restaurar nesta etapa. Para reverter a versão, pare a aplicação, preserve o banco atualizado e restaure uma cópia consistente do backup com o código anterior; não reutilize WAL/SHM de outro banco. Uma restauração perde alterações posteriores ao backup, portanto precisa de revisão humana.

## Testar sem dados pessoais

Na raiz, execute `powershell -File scripts/Testar-ImportacaoLocal.ps1`. Acesse `http://127.0.0.1:5189/valores-a-receber` na instância temporária. Cada execução cria um banco novo e compila em uma pasta separada.

1. Crie um grupo para Pessoa fictícia com dois itens de 100 e 200. Confira total 300, recebido zero e saldo 300.
2. Registre recebimento de 40 no primeiro item. Confira recebido 40, saldo do grupo 260 e situação Parcial nesse item.
3. Tente receber 61 no mesmo item: deve ser bloqueado, pois restam 60. Confira o histórico e receba somente os 60 restantes; o item deve ficar Quitado.
4. Adicione um item de 50. Confira os totais e o painel no mês escolhido. O painel deve ter exatamente três receitas, sem cópias do grupo.
5. Crie itens em competências diferentes e confira que o grupo soma todos, enquanto o painel filtra por competência. Crie mais de seis para conferir Anterior/Próxima.
6. Encerre com Ctrl+C. Não use os dados privados descritos na conversa como exemplos automatizados ou fixtures.

Para conferir edição e exclusão, também em banco temporário:

1. Clique em Editar grupo, corrija pessoa/título e salve; os itens, totais e recebimentos devem continuar iguais. Confira também Cancelar.
2. Edite um item sem recebimentos, incluindo observações e competência, e confira painel e totais. Edite um item com recebimentos: a competência deve ficar desabilitada, e salvar valor abaixo do recebido deve ser bloqueado. O histórico deve permanecer igual.
3. Exclua um item sem recebimentos: Cancelar deve preservá-lo; confirmar deve removê-lo do grupo e do painel, ajustando a página se necessário. Um item com recebimentos tem Excluir desabilitado.
4. Crie outro grupo fictício sem recebimentos. Confira o aviso centralizado em Excluir grupo, cancele e depois confirme. Somente esse grupo e seus lançamentos devem desaparecer. Um grupo com recebimentos tem Excluir grupo desabilitado.

Para conferir o desmembramento, sempre na instância temporária:

1. No painel, cadastre uma receita fictícia de 300 em outubro/2026, sem registrar recebimento.
2. Em Valores a receber, clique em Desmembrar lançamento, escolha outubro/2026 e esse registro.
3. Informe pessoa/título fictícios e dois itens de 100 e 199: a diferença deve mostrar 1 e o botão de confirmação deve ficar desabilitado.
4. Corrija o segundo para 200 e confirme. O grupo deve mostrar previsto 300, recebido zero, pendente 300. No painel de outubro devem existir duas receitas que somam 300, sem a linha consolidada adicional.
5. Abra o primeiro item para conferir as observações de origem. Ao abrir outro desmembramento, a origem convertida já não deve aparecer. Uma receita com recebimentos também não deve aparecer.

Testes automatizados: `dotnet test FinanceControl.slnx`. Incluem revalidação do saldo, pertencimento do item ao grupo, rollback, preservação do esquema antigo, idempotência e leitura do backup gerado com dados fictícios. Os endpoints e o HTML foram verificados com banco temporário. A conferência visual dos cliques ainda depende do navegador.

## API e arquitetura

Contratos e DTOs ficam em `Application`; entidades em `Domain`; agrupamento, atualização aditiva e persistência em `Infrastructure`; página e CSS em `SharedUI`; endpoints no host `Web`. O serviço delega criação e recebimentos ao `ILancamentoService`, preservando as mesmas regras para o futuro Windows.

| Método | Rota | Uso |
| --- | --- | --- |
| GET | `/api/valores-a-receber` | Lista grupos com itens e totais |
| GET | `/api/valores-a-receber/{id}` | Consulta grupo |
| GET | `/api/valores-a-receber/origens?mes=2026-10` | Lista receitas elegíveis para desmembramento |
| POST | `/api/valores-a-receber/desmembrar` | Converte origem em grupo revisado; retorna 201 |
| POST | `/api/valores-a-receber` | Cadastra grupo e itens; retorna 201 |
| POST | `/api/valores-a-receber/{id}/itens` | Acrescenta item |
| PUT | `/api/valores-a-receber/{id}` | Atualiza pessoa e título |
| PUT | `/api/valores-a-receber/{id}/itens/{lancamentoId}` | Atualiza campos do item preservando recebimentos |
| DELETE | `/api/valores-a-receber/{id}` | Exclui grupo e lançamentos sem recebimentos |
| DELETE | `/api/valores-a-receber/{id}/itens/{lancamentoId}` | Exclui somente item sem recebimentos |
| POST | `/api/valores-a-receber/{id}/itens/{lancamentoId}/recebimentos` | Registra recebimento no item |

Grupo: `pessoa`, `descricao`, `itens`. Item: `descricao`, `valor`, `competencia`, `vencimento`, `observacoes`. Recebimento: `valor`, `data`, `observacao`. As validações retornam 400; grupo ou vínculo inexistente retorna 404. API segue somente local, sem autenticação.

O PUT de grupo recebe somente `pessoa` e `descricao`; o PUT de item recebe todos os campos do item (atualização completa). DELETE retorna 204 ao excluir, 404 para grupo/vínculo inexistente e 400 se houver recebimentos. Consumidores da API devem apresentar sua própria confirmação antes do DELETE. A inclusão destas operações não altera o esquema do banco.

Desmembramento recebe `lancamentoId`, `pessoa`, `descricao`, `itens` (mesmos campos dos itens de cadastro). Retorna 201, 400 para impedimentos e 404 para origem inexistente. Esta etapa não acrescenta tabelas/colunas; reutiliza o esquema de agrupamento.
