using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using FinanceControl.Application.Interfaces;
using FinanceControl.Application.Models;
using FinanceControl.Domain.Entities;
using FinanceControl.Domain.Enums;
using FinanceControl.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FinanceControl.Infrastructure.Services;

public sealed class ImportacaoExcelService(FinanceDbContext db) : IImportacaoExcelService
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
    private static readonly string[] Meses =
    ["JANEIRO", "FEVEREIRO", "MARCO", "ABRIL", "MAIO", "JUNHO",
     "JULHO", "AGOSTO", "SETEMBRO", "OUTUBRO", "NOVEMBRO", "DEZEMBRO"];

    public IReadOnlyList<AbaImportacaoDto> ListarAbas(byte[] dados)
    {
        using var book = Abrir(dados);
        return book.Worksheets.Select(ws => InfoAba(ws.Name)).ToList();
    }

    public async Task<PreviaImportacaoDto> PrepararPreviaAsync(byte[] dados, string nomeArquivo,
        string aba, int? anoManual, CancellationToken ct = default)
    {
        using var book = Abrir(dados);
        var ws = book.Worksheets.FirstOrDefault(x => x.Name == aba)
            ?? throw new ArgumentException("Aba não encontrada na planilha.");
        var info = InfoAba(aba);
        if (!info.Suportada || info.Mes is null)
            throw new ArgumentException("Esta aba não é mensal e precisa de revisão individual.");
        int ano = info.Ano ?? anoManual ?? 0;
        if (ano < 2000 || ano > 2100)
            throw new ArgumentException("Informe o ano correto para esta aba antiga.");
        var competencia = new DateOnly(ano, info.Mes.Value, 1);
        var linhas = new List<LinhaImportacaoDto>();

        int contasCol = 1, trabalhoCol = 7, cabecalho = 1;
        bool achouContas = false, achouTrabalhos = false;
        for (int r = 1; r <= Math.Min(6, ws.LastRowUsed()?.RowNumber() ?? 0); r++)
        for (int c = 1; c <= 12; c++)
        {
            var v = Normalizar(ws.Cell(r, c).GetString());
            if (v == "CONTAS") { contasCol = 1; cabecalho = Math.Max(cabecalho, r); achouContas = true; }
            if (v == "TRABALHOS") { trabalhoCol = c; cabecalho = Math.Max(cabecalho, r); achouTrabalhos = true; }
        }
        if (!achouContas || !achouTrabalhos)
            throw new ArgumentException("Não encontrei as colunas CONTAS e TRABALHOS nesta aba. Ela exige mapeamento manual.");
        int ultima = Math.Min(ws.LastRowUsed()?.RowNumber() ?? 0, 5000);
        var contas = new LayoutColunas(contasCol, contasCol + 1, contasCol + 2);
        var trabalhos = DescobrirLayoutTrabalhos(ws, trabalhoCol, cabecalho + 1, ultima);
        for (int r = cabecalho + 1; r <= ultima; r++)
        {
            AdicionarSeForLancamento(ws, r, contas, TipoLancamento.Despesa, competencia, nomeArquivo, aba, linhas);
            AdicionarSeForLancamento(ws, r, trabalhos, TipoLancamento.Receita, competencia, nomeArquivo, aba, linhas);
        }

        var existentes = await db.Lancamentos.AsNoTracking()
            .Where(x => x.Competencia == competencia).ToListAsync(ct);
        var chaves = linhas.Select(x => x.ChaveOrigem).ToList();
        var importadas = await db.RegistrosImportados.AsNoTracking()
            .Where(x => chaves.Contains(x.ChaveOrigem)).Select(x => x.ChaveOrigem).ToListAsync(ct);
        var importadasSet = importadas.ToHashSet(StringComparer.Ordinal);
        foreach (var linha in linhas)
        {
            if (importadasSet.Contains(linha.ChaveOrigem) ||
                existentes.Any(x => Iguais(x, linha)))
            {
                linha.Duplicada = true;
                linha.Selecionada = false;
                AdicionarAviso(linha, "Já existe um lançamento correspondente no banco.");
            }
        }
        return new PreviaImportacaoDto(aba, competencia, linhas);
    }

    public async Task<ResultadoImportacaoDto> ConfirmarAsync(IReadOnlyCollection<LinhaImportacaoDto> linhas,
        CancellationToken ct = default)
    {
        // Um único clique salva somente as linhas marcadas; falha => rollback de todas elas.
        var marcadas = linhas.Where(x => x.Selecionada).ToList();
        if (marcadas.Count == 0) return new ResultadoImportacaoDto(0, 0, 0);
        if (marcadas.Count > 2000) throw new ArgumentException("Limite de 2.000 registros por importação.");
        using var transaction = await db.Database.BeginTransactionAsync(ct);
        int importadas = 0, duplicadas = 0, ignoradas = 0;
        var chavesNoLote = new HashSet<string>(StringComparer.Ordinal);
        // Snapshot anterior: registros iguais em linhas diferentes do mesmo arquivo podem ser legítimos.
        var meses = marcadas.Select(x => x.Competencia).Distinct().ToList();
        var anteriores = await db.Lancamentos.AsNoTracking()
            .Where(x => meses.Contains(x.Competencia)).ToListAsync(ct);
        var chaves = marcadas.Select(x => x.ChaveOrigem).ToList();
        var importadasAntes = (await db.RegistrosImportados.AsNoTracking()
            .Where(x => chaves.Contains(x.ChaveOrigem)).Select(x => x.ChaveOrigem).ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);
        foreach (var linha in marcadas)
        {
            if (linha.Bloqueada || linha.Valor is null || linha.Valor <= 0 ||
                decimal.Round(linha.Valor.Value, 2) != linha.Valor.Value ||
                string.IsNullOrWhiteSpace(linha.Descricao) || linha.Descricao.Length > 180 ||
                linha.Categoria.Length > 80 || linha.Observacoes?.Length > 1000 ||
                linha.ChaveOrigem.Length != 64 || linha.Aba.Length > 100 ||
                !Enum.IsDefined(linha.Tipo))
            { ignoradas++; continue; }
            if (!chavesNoLote.Add(linha.ChaveOrigem) ||
                importadasAntes.Contains(linha.ChaveOrigem) || anteriores.Any(x => Iguais(x, linha)))
            { duplicadas++; continue; }
            var novo = new Lancamento
            {
                Descricao = linha.Descricao.Trim(),
                Categoria = linha.Categoria,
                Tipo = linha.Tipo,
                ValorPrevisto = linha.Valor.Value,
                Competencia = linha.Competencia,
                DataVencimento = linha.Data.HasValue ? DateOnly.FromDateTime(linha.Data.Value) : null,
                Observacoes = linha.Observacoes?.Trim()
            };
            db.Lancamentos.Add(novo);
            db.RegistrosImportados.Add(new RegistroImportacao
            {
                Lancamento = novo,
                ChaveOrigem = linha.ChaveOrigem,
                Aba = linha.Aba,
                LinhaExcel = linha.LinhaExcel
            });
            importadas++;
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new ResultadoImportacaoDto(importadas, ignoradas, duplicadas);
    }

    private static XLWorkbook Abrir(byte[] dados)
    {
        if (dados.Length == 0 || dados.Length > 12 * 1024 * 1024)
            throw new ArgumentException("Escolha uma planilha XLSX de até 12 MB.");
        try { return new XLWorkbook(new MemoryStream(dados, writable: false)); }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        { throw new ArgumentException("Não foi possível abrir o Excel. Confira se o arquivo é um XLSX válido.", ex); }
    }

    private static AbaImportacaoDto InfoAba(string nome)
    {
        var n = Normalizar(nome);
        int mes = Array.FindIndex(Meses, m => n.StartsWith(m, StringComparison.Ordinal));
        var match = Regex.Match(n, @"(?<!\d)(?:19|20)\d{2}(?!\d)");
        var ano = match.Success ? int.Parse(match.Value, CultureInfo.InvariantCulture) : (int?)null;
        return new AbaImportacaoDto(nome, ano.HasValue, ano, mes >= 0 ? mes + 1 : null, mes >= 0);
    }

    private readonly record struct LayoutColunas(int Data, int Descricao, int Valor);

    private static LayoutColunas DescobrirLayoutTrabalhos(IXLWorksheet ws, int colunaCabecalho,
        int primeiraLinha, int ultimaLinha)
    {
        LayoutColunas melhor = new(colunaCabecalho, colunaCabecalho + 1, colunaCabecalho + 2);
        int pontuacaoMelhor = -1;
        for (int colunaData = colunaCabecalho; colunaData <= colunaCabecalho + 1; colunaData++)
        for (int saltoDescricao = 1; saltoDescricao <= 2; saltoDescricao++)
        for (int saltoValor = saltoDescricao + 1; saltoValor <= 4; saltoValor++)
        {
            var layout = new LayoutColunas(colunaData, colunaData + saltoDescricao, colunaData + saltoValor);
            int pontos = 0;
            for (int r = primeiraLinha; r <= Math.Min(ultimaLinha, primeiraLinha + 30); r++)
            {
                var texto = ws.Cell(r, layout.Descricao).GetString().Trim();
                if (string.IsNullOrWhiteSpace(texto) || decimal.TryParse(texto, out _)) continue;
                if (Regex.IsMatch(Normalizar(texto), @"^(TOTAL\b|SALDO\b|SOMA\b)")) continue;
                pontos += 1;
                if (LerValor(ws.Cell(r, layout.Valor), out var v) && v > 0) pontos += 5;
                if (LerData(ws.Cell(r, layout.Data), out _)) pontos += 2;
            }
            if (pontos > pontuacaoMelhor) { pontuacaoMelhor = pontos; melhor = layout; }
        }
        return melhor;
    }

    private static void AdicionarSeForLancamento(IXLWorksheet ws, int linhaExcel, LayoutColunas layout,
        TipoLancamento tipo, DateOnly competencia, string nomeArquivo, string aba,
        List<LinhaImportacaoDto> linhas)
    {
        var descricao = Regex.Replace(ws.Cell(linhaExcel, layout.Descricao).GetString().Trim(), @"\s+", " ");
        if (string.IsNullOrEmpty(descricao)) return;
        var descNormal = Normalizar(descricao);
        if (Regex.IsMatch(descNormal, @"^(TOTAL(?:\b|$)|SUBTOTAL\b|SOMA\b|SALDO\b|CONTAS\b|TRABALHOS\b|RECEITAS\b|DESPESAS\b|VALOR TOTAL\b)")) return;

        var valorCelula = ws.Cell(linhaExcel, layout.Valor);
        var valorValido = LerValor(valorCelula, out decimal valor);
        var dataCelula = ws.Cell(linhaExcel, layout.Data);
        var dataValida = LerData(dataCelula, out DateTime data);
        var textoData = dataCelula.GetString().Trim();
        var obs = ws.Cell(linhaExcel, layout.Valor + 1).GetString().Trim();
        var linha = new LinhaImportacaoDto
        {
            Aba = aba,
            LinhaExcel = linhaExcel,
            ChaveOrigem = CriarChave(nomeArquivo, aba, linhaExcel, tipo),
            Tipo = tipo,
            Descricao = descricao.Length > 180 ? descricao[..180] : descricao,
            Categoria = tipo == TipoLancamento.Receita ? "Trabalhos e eventos" : CategoriaDespesa(descNormal),
            Valor = valorValido ? valor : null,
            Data = dataValida ? data : null,
            Competencia = competencia,
            Observacoes = string.IsNullOrWhiteSpace(obs) ? null : obs[..Math.Min(obs.Length, 1000)],
            Selecionada = valorValido && valor > 0 && decimal.Round(valor, 2) == valor,
            Bloqueada = !valorValido || valor <= 0 || decimal.Round(valor, 2) != valor
        };
        if (!valorValido) AdicionarAviso(linha, "Valor ausente ou inválido.");
        else if (valor <= 0) AdicionarAviso(linha, "Valor não positivo.");
        else if (decimal.Round(valor, 2) != valor) AdicionarAviso(linha, "Valor com mais de duas casas decimais.");
        if (!string.IsNullOrEmpty(textoData) && !dataValida)
        { AdicionarAviso(linha, "Data inválida."); linha.Selecionada = false; }
        if (dataValida && (data.Year != competencia.Year || data.Month != competencia.Month))
        { AdicionarAviso(linha, "Data fora do mês da aba; confira o vencimento."); linha.Selecionada = false; }
        if (obs.Contains("PAG", StringComparison.OrdinalIgnoreCase) ||
            obs.Contains("RECEB", StringComparison.OrdinalIgnoreCase) ||
            obs.Contains("ATRAS", StringComparison.OrdinalIgnoreCase))
        { AdicionarAviso(linha, "Observação menciona pagamento/atraso; não será lançado como baixa."); linha.Selecionada = false; }
        linhas.Add(linha);
    }

    private static bool LerValor(IXLCell cell, out decimal valor)
    {
        // Texto como "650,00" precisa ser interpretado em pt-BR, e não como 65.000.
        if (cell.DataType == XLDataType.Number && cell.TryGetValue<decimal>(out valor)) return true;
        var texto = cell.GetString().Trim().Replace("R$", "", StringComparison.OrdinalIgnoreCase).Trim();
        if (string.IsNullOrEmpty(texto)) { valor = 0; return false; }
        return decimal.TryParse(texto, NumberStyles.Number, PtBr, out valor) ||
               decimal.TryParse(texto, NumberStyles.Number, CultureInfo.InvariantCulture, out valor);
    }

    private static bool LerData(IXLCell cell, out DateTime data)
    {
        if (cell.TryGetValue<DateTime>(out data)) return data.Year is >= 2000 and <= 2100;
        if (cell.TryGetValue<double>(out var numero) && numero is >= 36526 and <= 73050)
        {
            try { data = DateTime.FromOADate(numero); return true; } catch (ArgumentException) { }
        }
        var texto = cell.GetString().Trim();
        if (DateTime.TryParse(texto, PtBr, DateTimeStyles.None, out data))
            return data.Year is >= 2000 and <= 2100;
        data = default;
        return false;
    }

    private static bool Iguais(Lancamento salvo, LinhaImportacaoDto linha) =>
        linha.Valor.HasValue && salvo.Tipo == linha.Tipo &&
        salvo.Competencia == linha.Competencia && salvo.ValorPrevisto == linha.Valor.Value &&
        Normalizar(salvo.Descricao) == Normalizar(linha.Descricao) &&
        salvo.DataVencimento == (linha.Data.HasValue ? DateOnly.FromDateTime(linha.Data.Value) : null);

    private static string CategoriaDespesa(string d)
    {
        if (d.Contains("ALUGUEL") || d.Contains("LUZ") || d.Contains("CLARO") || d.Contains("INTERNET")) return "Moradia";
        if (d.Contains("FACULDADE") || d.Contains("GRADUACAO") || d.Contains("CURSO")) return "Educação";
        if (d.Contains("ACADEMIA") || d.Contains("TOTALPASS")) return "Saúde";
        if (d.Contains("MEI") || d.Contains("IMPOSTO")) return "Impostos e MEI";
        if (d.Contains("EMPRESTIMO") || d.Contains("CREDITO")) return "Empréstimos";
        return "Outros";
    }

    private static string CriarChave(string arquivo, string aba, int linha, TipoLancamento tipo)
    {
        var nome = Path.GetFileNameWithoutExtension(arquivo);
        var texto = $"{Normalizar(nome)}|{Normalizar(aba)}|{linha}|{(int)tipo}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(texto)));
    }

    private static string Normalizar(string valor)
    {
        var decomposed = valor.Normalize(NormalizationForm.FormD);
        var semAcentos = new string(decomposed.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
        return Regex.Replace(semAcentos.ToUpperInvariant().Trim(), @"\s+", " ");
    }
    private static void AdicionarAviso(LinhaImportacaoDto linha, string aviso) =>
        linha.Aviso = string.IsNullOrEmpty(linha.Aviso) ? aviso : linha.Aviso + " " + aviso;
}
