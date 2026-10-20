namespace FinanceControl.Application.Models;

public sealed record SalvarCategoriaRequest(string Nome);
public sealed record CategoriaDto(int Id, string Nome, int QuantidadeLancamentos, bool Cadastrada);
