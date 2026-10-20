using FinanceControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinanceControl.Infrastructure.Data;

public sealed class FinanceDbContext(DbContextOptions<FinanceDbContext> options) : DbContext(options)
{
    public DbSet<Lancamento> Lancamentos => Set<Lancamento>();
    public DbSet<Baixa> Baixas => Set<Baixa>();
    public DbSet<RegistroImportacao> RegistrosImportados => Set<RegistroImportacao>();
    public DbSet<GrupoReceber> GruposReceber => Set<GrupoReceber>();
    public DbSet<ItemGrupoReceber> ItensGrupoReceber => Set<ItemGrupoReceber>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GrupoReceber>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Pessoa).IsRequired().HasMaxLength(120);
            entity.Property(x => x.Descricao).IsRequired().HasMaxLength(180);
            entity.HasMany(x => x.Itens).WithOne(x => x.GrupoReceber)
                .HasForeignKey(x => x.GrupoReceberId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<ItemGrupoReceber>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.LancamentoId).IsUnique();
            entity.HasOne(x => x.Lancamento).WithMany().HasForeignKey(x => x.LancamentoId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<Lancamento>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Descricao).IsRequired().HasMaxLength(180);
            entity.Property(x => x.Categoria).IsRequired().HasMaxLength(80);
            entity.Property(x => x.ValorPrevisto).HasPrecision(18, 2);
            entity.Property(x => x.Observacoes).HasMaxLength(1000);
            entity.HasIndex(x => x.Competencia);
            entity.HasMany(x => x.Baixas).WithOne(x => x.Lancamento)
                .HasForeignKey(x => x.LancamentoId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<RegistroImportacao>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ChaveOrigem).IsRequired().HasMaxLength(64);
            entity.Property(x => x.Aba).IsRequired().HasMaxLength(100);
            entity.HasIndex(x => x.ChaveOrigem).IsUnique();
            entity.HasOne(x => x.Lancamento).WithMany().HasForeignKey(x => x.LancamentoId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<Baixa>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Valor).HasPrecision(18, 2);
            entity.Property(x => x.Observacao).HasMaxLength(500);
        });
    }
}
