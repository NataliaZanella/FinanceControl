# Cadastro de categorias

Disponível em `/categorias`, com busca e seis nomes por página. Permite criar, renomear e excluir categorias sem uso. O painel mensal passa a oferecer uma seleção com os nomes cadastrados e aqueles encontrados no histórico, mantendo o valor original ao editar um lançamento antigo.

## Regras e compatibilidade

- Nome obrigatório com até 80 caracteres. Espaços nas extremidades são removidos e sequências de espaços são reduzidas a um. Nomes equivalentes sem diferenciar acentos ou maiúsculas não podem ser cadastrados duas vezes; um índice único reforça essa regra.
- Ao renomear, o novo nome é aplicado aos lançamentos de **todos os meses** que usam o nome anterior, inclusive variações de acentos/maiúsculas/espaços. O formulário informa a quantidade de usos antes de salvar. A operação é transacional e não altera valores, tipos, datas, baixas, grupos ou origens de importação.
- Exclusão exige confirmação modal centralizada e é bloqueada se houver qualquer lançamento usando o nome, inclusive em meses antigos. Nenhum lançamento é excluído por esse procedimento.
- Nomes vindos de API ou importação que ainda não tenham cadastro aparecem como “sem cadastro”, com o botão Cadastrar este nome. Consultar essa lista não grava nem corrige dados automaticamente.
- O campo Categoria dos lançamentos continua textual para preservar a compatibilidade com a API, importação e dados existentes; ainda não foi criada uma chave estrangeira. Portanto uma integração antiga ainda pode registrar um nome fora do cadastro. Não há reclassificação automática de nomes sugeridos pelo parser Excel depois de renomear uma categoria.
- A contagem de usos considera o histórico inteiro; os filtros e indicadores do painel continuam funcionando por competência.
- Não existem, nesta etapa, subcategorias, associação exclusiva a receita/despesa, fusão automática de duas categorias existentes ou auditoria imutável de nomes.

## Atualização do banco

Foi adicionada apenas a tabela `Categorias`, com nome e chave normalizada única. Os lançamentos e baixas continuam nas tabelas existentes.

- Banco novo: `EnsureCreated()` cria a tabela e as categorias iniciais.
- Banco anterior sem essa tabela: na próxima inicialização, o sistema gera uma cópia SQLite consistente no mesmo diretório do banco, com sufixo `.antes-categorias-<dataUTC>-<identificador>.db`. O backup usa `BackupDatabase`, incluindo dados do WAL. Se o backup falhar, a criação da tabela não prossegue.
- Em uma transação, cria a tabela e registra os nomes iniciais mais os encontrados no histórico. Essa atualização não reescreve o campo Categoria dos lançamentos antigos. Reiniciar não repete a atualização nem recria categorias já excluídas.
- A rotina de backup foi compartilhada com a atualização de Valores a receber, mantendo seu formato e comportamento anteriores. A adoção completa de migrations ainda está pendente.
- Antes de atualizar com dados reais, pare as instâncias anteriores e mantenha também um backup próprio. Para reverter, pare o aplicativo, preserve o banco atual e planeje a restauração do backup com o código anterior, sem reaproveitar WAL/SHM de outro arquivo. A restauração afeta alterações posteriores ao backup; não existe restauração automática nesta tela.

Nenhum banco pessoal foi acessado durante o desenvolvimento. Os testes de atualização e leitura do backup usam exclusivamente arquivos sintéticos temporários.

## Teste manual isolado

Na raiz: `powershell -File scripts/Testar-ImportacaoLocal.ps1`. Abra `http://127.0.0.1:5189/categorias`.

1. Cadastre uma categoria fictícia. Confira que ela aparece e que a paginação acompanha a posição alfabética do nome salvo.
2. Tente cadastrá-la novamente com maiúsculas e sem acentos: deve haver mensagem de duplicidade.
3. No painel da mesma instância, crie um lançamento fictício com essa categoria, previsto 100, e registre uma baixa de 40.
4. Reabra Categorias: o uso deve ser 1 e Excluir deve ficar desabilitado. Renomeie a categoria, verificando o aviso de que os lançamentos serão atualizados.
5. Confira no painel o novo nome, previsto 100, realizado 40 e o histórico da baixa preservados.
6. Cadastre outra categoria sem uso. Clique em Excluir e cancele: ela deve permanecer. Repita e confirme: somente o cadastro desse nome deve desaparecer.
7. Teste busca, Anterior/Próxima e janela estreita. Encerre com Ctrl+C. A conferência visual dos novos controles ainda depende deste teste no navegador.

## Validação executada

92 testes aprovados na solução, incluindo categorias: duplicidade, limites do nome, preservação financeira na renomeação, bloqueio de exclusão em meses antigos, nomes sem cadastro, rollback, atualização idempotente e backup consistente legível. Build sem erros/avisos. Conferência HTTP com banco temporário: criação 201, consulta/renomeação 200, duplicidade/exclusão em uso 400, exclusão sem uso 204 e repetição 404; tela e seletor do painel renderizados.

## API local

| Método | Rota | Operação |
| --- | --- | --- |
| GET | `/api/categorias` | Lista nomes e quantidade de usos; inclui nomes legados sem cadastro |
| GET | `/api/categorias/{id}` | Consulta uma categoria cadastrada |
| POST | `/api/categorias` | Cadastra `{ "nome": "Categoria fictícia" }`; retorna 201 |
| PUT | `/api/categorias/{id}` | Renomeia e atualiza os usos; retorna 200 |
| DELETE | `/api/categorias/{id}` | Exclui somente se não houver uso; retorna 204 |

Validação retorna 400; registro inexistente retorna 404. Consumidores da API devem apresentar sua própria confirmação de exclusão. A API permanece sem autenticação e somente local.

## Arquivos deste incremento

- Domain: `Entities/Categoria.cs`.
- Application: `Interfaces/ICategoriaService.cs`, `Models/CategoriaModels.cs`.
- Infrastructure: `Data/FinanceDbContext.cs`, `Data/AtualizacaoCategorias.cs`, `Data/BackupBancoSqlite.cs`, `Data/AtualizacaoValoresReceber.cs`, `Services/CategoriaService.cs`, `DependencyInjection.cs`.
- SharedUI: `Components/Pages/Categorias.razor`, `Dashboard.razor`, `Importar.razor`, `ValoresReceber.razor`, `wwwroot/finance.css`.
- Web: `CategoriaEndpoints.cs`, `Program.cs`.
- Testes: `FinanceControl.Tests/CategoriaServiceTests.cs`.
- Documentação: `README.md`, `docs/CONTEXTO_CODEX.md`, `docs/ROADMAP_CODEX.md`, `docs/categorias.md`.
