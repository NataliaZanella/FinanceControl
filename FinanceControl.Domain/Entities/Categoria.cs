using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace FinanceControl.Domain.Entities;

public sealed class Categoria
{
    public int Id { get; set; }
    public string Nome { get; set; } = "";
    public string NomeNormalizado { get; set; } = "";

    public static IReadOnlyList<string> Padroes { get; } = Array.AsReadOnly(new[] {
        "Moradia", "Educação", "Transporte", "Saúde", "Alimentação", "Cartão de crédito",
        "Empréstimos", "Trabalhos e eventos", "Salário", "Impostos e MEI", "Outros", "Valores a receber"
    });

    public static string LimparNome(string nome) => Regex.Replace(nome.Trim(), @"\s+", " ");

    public static string NormalizarNome(string nome)
    {
        var texto = LimparNome(nome).Normalize(NormalizationForm.FormD);
        return new string(texto.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .ToArray()).ToUpperInvariant();
    }
}
