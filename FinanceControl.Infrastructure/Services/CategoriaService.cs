using FinanceControl.Application.Interfaces;
using FinanceControl.Application.Models;
using FinanceControl.Domain.Entities;
using FinanceControl.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FinanceControl.Infrastructure.Services;

public sealed class CategoriaService(FinanceDbContext db) : ICategoriaService
{
    public async Task<IReadOnlyList<CategoriaDto>> ListarAsync(CancellationToken ct = default)
    {
        var cadastradas = await db.Categorias.AsNoTracking().ToListAsync(ct);
        var nomes = await db.Lancamentos.AsNoTracking().Select(x => x.Categoria).ToListAsync(ct);
        var usos = nomes.GroupBy(Categoria.NormalizarNome).ToDictionary(x => x.Key, x => x.ToList());
        var resultado = cadastradas.Select(x => new CategoriaDto(x.Id, x.Nome,
            usos.TryGetValue(x.NomeNormalizado, out var itens) ? itens.Count : 0, true)).ToList();
        var chaves = cadastradas.Select(x => x.NomeNormalizado).ToHashSet();
        resultado.AddRange(usos.Where(x => !chaves.Contains(x.Key))
            .Select(x => new CategoriaDto(0, Categoria.LimparNome(x.Value[0]), x.Value.Count, false)));
        return resultado.OrderBy(x => x.Nome, StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("pt-BR"), true)).ToList();
    }

    public async Task<CategoriaDto> CriarAsync(SalvarCategoriaRequest request, CancellationToken ct = default)
    {
        var nome = ValidarNome(request.Nome);
        var chave = Categoria.NormalizarNome(nome);
        if (await db.Categorias.AnyAsync(x => x.NomeNormalizado == chave, ct))
            throw new ArgumentException("Já existe uma categoria com esse nome, mesmo com diferenças de acentos ou maiúsculas.");
        var categoria = new Categoria { Nome = nome, NomeNormalizado = chave };
        db.Categorias.Add(categoria);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteExtendedErrorCode: 2067 })
        {
            db.Entry(categoria).State = EntityState.Detached;
            throw new ArgumentException("Já existe uma categoria com esse nome.");
        }
        catch
        {
            db.Entry(categoria).State = EntityState.Detached;
            throw;
        }
        return (await ListarAsync(ct)).Single(x => x.Id == categoria.Id);
    }

    public async Task<CategoriaDto> RenomearAsync(int id, SalvarCategoriaRequest request, CancellationToken ct = default)
    {
        var nome = ValidarNome(request.Nome);
        var chave = Categoria.NormalizarNome(nome);
        using var transacao = await db.Database.BeginTransactionAsync(ct);
        var categoria = await db.Categorias.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException("Categoria não encontrada.");
        if (await db.Categorias.AnyAsync(x => x.Id != id && x.NomeNormalizado == chave, ct))
            throw new ArgumentException("Já existe outra categoria com esse nome.");
        var ids = await IdentificarUsosAsync(categoria.NomeNormalizado, ct);
        await db.Lancamentos.Where(x => ids.Contains(x.Id))
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.Categoria, nome), ct);
        await db.Categorias.Where(x => x.Id == id).ExecuteUpdateAsync(setters => setters
            .SetProperty(x => x.Nome, nome).SetProperty(x => x.NomeNormalizado, chave), ct);
        await transacao.CommitAsync(ct);
        return new(id, nome, ids.Count, true);
    }

    public async Task<bool> ExcluirAsync(int id, CancellationToken ct = default)
    {
        using var transacao = await db.Database.BeginTransactionAsync(ct);
        var categoria = await db.Categorias.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (categoria is null) return false;
        if ((await IdentificarUsosAsync(categoria.NomeNormalizado, ct)).Count > 0)
            throw new ArgumentException("Esta categoria está em uso em lançamentos e não pode ser excluída. Reclassifique os lançamentos primeiro.");
        await db.Categorias.Where(x => x.Id == id).ExecuteDeleteAsync(ct);
        await transacao.CommitAsync(ct);
        return true;
    }

    private async Task<List<int>> IdentificarUsosAsync(string chave, CancellationToken ct)
    {
        var registros = await db.Lancamentos.AsNoTracking().Select(x => new { x.Id, x.Categoria }).ToListAsync(ct);
        return registros.Where(x => Categoria.NormalizarNome(x.Categoria) == chave).Select(x => x.Id).ToList();
    }

    private static string ValidarNome(string? valor)
    {
        var nome = Categoria.LimparNome(valor ?? "");
        if (nome.Length is < 1 or > 80) throw new ArgumentException("Informe uma categoria com até 80 caracteres.");
        return nome;
    }
}
