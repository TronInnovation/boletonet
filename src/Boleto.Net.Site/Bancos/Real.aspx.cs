using System;
using BoletoNet;

public partial class Bancos_Real : System.Web.UI.Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        DateTime vencimento = DateTime.Now.AddDays(5); 

        Cedente c = new Cedente("00.000.000/0000-00", "Coloque a Razão Social da sua empresa aqui", "1234", "12345");
        c.Codigo = "12345";
        EspecieDocumento especiedocumento = new EspecieDocumento(356, "9");

        Boleto b = new Boleto();
        b.DataVencimento = vencimento;
        b.ValorBoleto = 0.1m;
        b.Carteira = "57";
        b.NossoNumero = "123456";
        b.EspecieDocumento = especiedocumento;
        b.Cedente = c;

        b.NumeroDocumento = "1234567";

        b.Sacado = new Sacado("000.000.000-00", "Nome do seu Cliente ");
        b.Sacado.Endereco.End = "Endereço do seu Cliente ";
        b.Sacado.Endereco.Bairro = "Bairro";
        b.Sacado.Endereco.Cidade = "Cidade";
        b.Sacado.Endereco.CEP = "00000000";
        b.Sacado.Endereco.UF = "UF";

        //b.Instrucoes.Add("Não Receber após o vencimento");
        //b.Instrucoes.Add("Após o Vencimento pague somente no Real");
        //b.Instrucoes.Add("Instrução 2");
        //b.Instrucoes.Add("Instrução 3");
        real.Boleto = b;

        EspeciesDocumento ed = EspecieDocumento_Real.CarregaTodas();

        real.Boleto.Valida();

        real.MostrarComprovanteEntrega = (Request.Url.Query == "?show");
    }
}
