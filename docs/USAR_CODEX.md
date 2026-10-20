# Como usar o Codex no FinanceControl (Windows 11 + VS Code)

## Preparação
1. No VS Code, abra a pasta **D:\Projetos\FinanceControl** (Arquivo > Abrir Pasta).
2. Instale a extensão oficial **Codex – OpenAI's coding agent**, da OpenAI. Página oficial: https://marketplace.visualstudio.com/items?itemName=OpenAI.chatgpt
3. Abra o painel Codex no VS Code e entre com sua conta ChatGPT seguindo o assistente.
4. Coloque `AGENTS.md` na raiz do FinanceControl e os documentos deste pacote dentro de `docs/`. O Codex reconhece `AGENTS.md` e consegue ler os documentos indicados nele.
5. Confira os arquivos e faça um backup do banco financeiro antes de mudanças relevantes. A simples instalação deste pacote **não muda código, banco ou planilhas**.

## Prompt para iniciar uma conversa nova com Codex

> Estou continuando meu sistema pessoal FinanceControl em C#/.NET 10, localizado neste workspace. Leia `AGENTS.md`, `README.md`, `docs/CONTEXTO_CODEX.md`, `docs/ROADMAP_CODEX.md`, `docs/importacao-excel.md` e analise a estrutura real do código. Primeiro, faça apenas uma auditoria de leitura, execute `dotnet build .\FinanceControl.slnx` se possível e identifique se a tela `/importar` e a proteção contra duplicidade estão realmente instaladas. Não altere arquivos, não leia dados reais do Excel e não apague nem modifique `financecontrol.db`. Ao final, resuma o estado atual, inconsistências e proponha a próxima tarefa pequena para minha aprovação.

## Comandos no terminal integrado (PowerShell)

```powershell
cd D:\Projetos\FinanceControl
dotnet --version
dotnet restore .\FinanceControl.slnx
dotnet build .\FinanceControl.slnx
dotnet run --project .\FinanceControl.Web --launch-profile http
```

Abra `http://127.0.0.1:5178`. A tela `/importar` está disponível se o importador tiver sido aplicado e compilado corretamente.

## Observações
- Codex usa instruções do `AGENTS.md`, mas **não tem automaticamente todo o histórico desta conversa**; o arquivo `docs/CONTEXTO_CODEX.md` registra as decisões importantes.
- Não precisa instalar SDK novo: a usuária já confirmou a instalação do .NET 10.0.402.
- Evite compartilhar o Excel original e o SQLite real com projetos públicos/Git ou execuções remotas. Use dados fictícios ao pedir testes.
- A versão Windows Desktop é objetivo futuro, não projeto atual.
- Se o Codex oferecer edições, revise o diff antes de aceitar; peça que explique quais arquivos serão alterados e por quê.

## Links de referência
- Extensão para VS Code: https://marketplace.visualstudio.com/items?itemName=OpenAI.chatgpt
- Guia oficial da extensão: https://developers.openai.com/codex/ide
- Instruções AGENTS.md: https://developers.openai.com/codex/guides/agents-md
