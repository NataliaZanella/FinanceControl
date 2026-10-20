using FinanceControl.Application.Interfaces;
using FinanceControl.Infrastructure.Data;
using FinanceControl.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceControl.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddFinanceControl(this IServiceCollection services, IConfiguration configuration)
    {
        var connection = configuration.GetConnectionString("FinanceDb") ?? "Data Source=financecontrol.db";
        services.AddDbContext<FinanceDbContext>(options => options.UseSqlite(connection));
        services.AddScoped<ILancamentoService, LancamentoService>();
        services.AddScoped<IImportacaoExcelService, ImportacaoExcelService>();
        services.AddScoped<IValoresReceberService, ValoresReceberService>();
        services.AddScoped<ICategoriaService, CategoriaService>();
        return services;
    }
}
