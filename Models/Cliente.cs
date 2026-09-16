namespace GBS_Web.Models;

public class Cliente
{
    public int IdCliente { get; set; }
    public string NomeRazao { get; set; } = "";
    public string? NomeFantasia { get; set; }
    public string? Documento { get; set; }
    public string? Email { get; set; }
    public string? Telefone { get; set; }
    public string? Endereco1 { get; set; }
    public string? Endereco2 { get; set; }
    public string? Cidade { get; set; }
    public string? Estado { get; set; }
    public string? ZipCode { get; set; }
    public string Pais { get; set; } = "USA";
    public string Ativo { get; set; } = "Y";
    public string? Observacoes { get; set; }
    public DateTime? DataCadastro { get; set; }
    public DateTime? DataAlteracao { get; set; }

    public string NomeExibicao => string.IsNullOrWhiteSpace(NomeFantasia) ? NomeRazao : $"{NomeRazao} ({NomeFantasia})";
}
