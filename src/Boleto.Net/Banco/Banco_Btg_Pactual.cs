using BoletoNet.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoletoNet
{
    internal class Banco_Btg_Pactual : AbstractBanco, IBanco
    {
        #region CONSTRUTOR
        internal Banco_Btg_Pactual()
        {
            this.Nome = "BTG Pactual";
            this.Codigo = 208;
            this.Digito = "1";
        }
        #endregion CONSTRUTOR

        #region FORMATAÇÕES

        public override void FormataNossoNumero(Boleto boleto)
        {
            if (string.IsNullOrWhiteSpace(boleto.NossoNumero))
                throw new Exception("Nosso Número não informado.");

            const int tamanhoMaximoNossoNumero = 11;
            if (boleto.NossoNumero.Length > tamanhoMaximoNossoNumero)
                throw new Exception($"Nosso Número ({boleto.NossoNumero}) deve conter {tamanhoMaximoNossoNumero} dígitos.");

            var contaBancaria = boleto.Cedente.ContaBancaria;

            if (contaBancaria.DigitoConta.Length != 1)
                throw new NotImplementedException($"Não foi possível formatar o campo livre: Digito da conta ({contaBancaria.DigitoConta}) não possui 1 dígito.");

            if (contaBancaria.Agencia.Length != 4)
                throw new NotImplementedException($"Não foi possível formatar o campo livre: Numero de Agencia ({contaBancaria.Agencia}) não possui 4 dígitos.");

            boleto.NossoNumero = boleto.NossoNumero.PadLeft(tamanhoMaximoNossoNumero, '0');
            boleto.DigitoNossoNumero = CalcularDVBancoBTGPactual(boleto.NossoNumero);
            boleto.NossoNumero = $"{boleto.Carteira.PadLeft(3, '0')}/{boleto.NossoNumero.PadLeft(10, '0')}-{boleto.DigitoNossoNumero}";
        }

        public override void FormataLinhaDigitavel(Boleto boleto)
        {
            if (string.IsNullOrWhiteSpace(boleto.CodigoBarra.Codigo))
            {
                boleto.CodigoBarra.LinhaDigitavel = "";
                return;
            }

            var codigoDeBarras = boleto.CodigoBarra.Codigo;

            codigoDeBarras = codigoDeBarras.Replace("-", "");

            #region Campo 1
            if (codigoDeBarras.Length < 24) return;
            var bbb = codigoDeBarras.Substring(0, 3);
            var m = codigoDeBarras.Substring(3, 1);
            var ccccc = codigoDeBarras.Substring(19, 5);
            var d1 = CalcularDvModulo10(bbb + m + ccccc);
            var grupo1 = $"{bbb}{m}{ccccc.Substring(0, 1)}{ccccc.Substring(1, 4)}{d1}";  

            #endregion Campo 1

            #region Campo 2
            var d2A = codigoDeBarras.Substring(24, 10);
            var d2B = CalcularDvModulo10(d2A).ToString();
            var grupo2 = $"{d2A.Substring(0, 5)}{d2A.Substring(5, 5)}{d2B}"; 

            #endregion Campo 2

            #region Campo 3
            var d3A = codigoDeBarras.Substring(34, 10);
            var d3B = CalcularDvModulo10(d3A).ToString();
            var grupo3 = $"{d3A.Substring(0, 5)}{d3A.Substring(5, 5)}{d3B}"; 

            #endregion Campo 3

            #region Campo 4
            var grupo4 = $"{boleto.CodigoBarra.DigitoVerificador}"; 
            #endregion Campo 4

            #region Campo 5
            var d5A = codigoDeBarras.Substring(5, 4);
            var d5B = codigoDeBarras.Substring(9, 10);
            var grupo5 = $"{d5A}{d5B}"; 
            #endregion Campo 5

            boleto.CodigoBarra.LinhaDigitavel = $"{grupo1}{grupo2}{grupo3}{grupo4}{grupo5}";
        }


        private static int CalcularDvModulo10(string texto)
        {
            int soma = 0, peso = 2;
            for (var i = texto.Length; i > 0; i--)
            {
                var resto = Convert.ToInt32(texto.Substring(i - 1, 1)) * peso;
                if (resto > 9)
                    resto = resto / 10 + resto % 10;    
                soma += resto;
                if (peso == 2)
                    peso = 1;
                else
                    peso = peso + 1;
            }
            var digito = (10 - soma % 10) % 10;
            return digito;
        }


        public string CalcularDVBancoBTGPactual(string nossoNumero)
        {
            var soma = 0;
            var multiplicador = 2;

            for (int i = nossoNumero.Length - 1; i >= 0; i--)
            {
                int numero = int.Parse(nossoNumero[i].ToString());
                soma += numero * multiplicador;

                multiplicador++;
                if (multiplicador > 9)
                {
                    multiplicador = 2;
                }
            }

            int resto = soma % 11;
            int digitoVerificador;

            if (resto == 0 || resto == 1)
            {
                digitoVerificador = 1;  
            }
            else if (resto == 10)
            {
                digitoVerificador = 1; 
            }
            else
            {
                digitoVerificador = 11 - resto;
            }

            return digitoVerificador.ToString();
        }

        public String FormataNumeroTitulo(Boleto boleto)
        {
            var novoTitulo = new StringBuilder();
            novoTitulo.Append(boleto.NossoNumero.Replace("-", "").PadLeft(8, '0'));
            return novoTitulo.ToString();
        }

        public String FormataNumeroParcela(Boleto boleto)
        {
            if (boleto.NumeroParcela <= 0)
                boleto.NumeroParcela = 1;

            //Variaveis
            StringBuilder novoNumero = new StringBuilder();

            //Formatando
            for (int i = 0; i < (3 - boleto.NumeroParcela.ToString().Length); i++)
            {
                novoNumero.Append("0");
            }
            novoNumero.Append(boleto.NumeroParcela.ToString());
            return novoNumero.ToString();
        }

        public string FormataCodigoBarraCampoLivre(Boleto boleto)
        {
            string contaFinal = string.Empty;

            if (boleto.Cedente.ContaBancaria?.Conta != null)
            {
                contaFinal = boleto.Cedente.ContaBancaria.Conta.Length >= 7
                    ? boleto.Cedente.ContaBancaria.Conta.Substring(boleto.Cedente.ContaBancaria.Conta.Length - 7)
                    : boleto.Cedente.ContaBancaria.Conta;  
            }

            string campoLivre =
                $"{boleto.Cedente.ContaBancaria.Agencia}" +                          // Agência
                $"{Convert.ToInt32(boleto.Carteira).ToString().PadLeft(2, '0')}" +    // Carteira com 2 dígitos
                $"{boleto.NossoNumero}" +                                            // Nosso Número
                $"{contaFinal}" +                                                     // Conta Final (últimos 7 dígitos da conta)
                $"0";                                                                  // Valor fixo para completar

            if (campoLivre.Length > 25)
            {
                campoLivre = campoLivre.Substring(0, 25);  // Cortar o valor se for maior que 25
            }
            else if (campoLivre.Length < 25)
            {
                campoLivre = campoLivre.PadLeft(25, '0');  // Preencher com zeros à esquerda se for menor que 25
            }


            campoLivre = campoLivre.Replace("/", "").Replace("-", "");

            return campoLivre;
        }


        public override void FormataCodigoBarra(Boleto boleto)
        {
            var codigoBarra = boleto.CodigoBarra;
            codigoBarra.CampoLivre = FormataCodigoBarraCampoLivre(boleto);

            if (codigoBarra.CampoLivre.Length != 25)
                throw new Exception($"Campo Livre ({codigoBarra.CampoLivre}) deve conter 25 dígitos.");

            // Formata Código de Barras do Boleto
            codigoBarra.CodigoBanco = "208";
            codigoBarra.Moeda = boleto.Moeda;
            codigoBarra.ValorDocumento = boleto.ValorCobrado.ToString("N2").Replace(",", "").Replace(".", "").PadLeft(10, '0');
        }
        #endregion FORMATAÇÕES

        #region VALIDAÇÕES

        public override void ValidaBoleto(Boleto boleto)
        {
            boleto.LocalPagamento += Nome + "";
            if (boleto.DataProcessamento == DateTime.MinValue)
                boleto.DataProcessamento = DateTime.Now;
            if (boleto.DataDocumento == DateTime.MinValue)
                boleto.DataDocumento = DateTime.Now;

            boleto.QuantidadeMoeda = 0;
            boleto.LocalPagamento = "PAGÁVEL EM QUALQUER CORRESPONDENTE BANCÁRIO PERTO DE VOCÊ!";

            this.FormataNossoNumero(boleto);
            this.FormataCodigoBarra(boleto);
            this.FormataLinhaDigitavel(boleto);
        }

        #endregion VALIDAÇÕES

        #region ARQUIVO DE REMESSA

        private string GerarHeaderRemessaCNAB240(int numeroConvenio, Cedente cedente, int numeroArquivoRemessa)
        {
            //Variaveis
            try
            {
                //Montagem do header
                string header = "756"; //Posição 001 a 003   Código do Sicoob na Compensação: "756"
                header += "0000"; //Posição 004 a 007  Lote de Serviço: "0000"
                header += "0"; //Posição 008           Tipo de Registro: "0"
                header += new string(' ', 9); //); //Posição 09 a 017     Uso Exclusivo FEBRABAN / CNAB: Brancos
                header += cedente.CPFCNPJ.Length == 11 ? "1" : "2"; //Posição 018  1=CPF    2=CGC/CNPJ
                header += Utils.FormatCode(cedente.CPFCNPJ, "0", 14, true); //Posição 019 a 032   Número de Inscrição da Empresa
                header += Utils.FormatCode((cedente.Convenio > 0 ? cedente.Convenio.ToString() : ""), " ", 20, true); //Posição 033 a 052     Código do Convênio no Sicoob: Brancos
                header += Utils.FormatCode(cedente.ContaBancaria.Agencia, 5);//Posição 053 a 057     Prefixo da Cooperativa: vide planilha "Capa" deste arquivo
                header += Utils.FormatCode(cedente.ContaBancaria.DigitoAgencia, "0", 1);  //Posição 058 a 058 Digito Agência
                header += Utils.FormatCode(cedente.ContaBancaria.Conta, "0", 12, true);   //Posição 059 a 070
                header += cedente.ContaBancaria.DigitoConta;  //Posição 071 a 71
                header += new string('0', 1); //Posição 072 a 72     Dígito Verificador da Ag/Conta: Preencher com zeros
                header += Utils.FormatCode(cedente.Nome, " ", 30);  //Posição 073 a 102      Nome do Banco: SICOOB
                header += Utils.FormatCode("SICOOB", " ", 30);     //Posição 103 a 132       Nome da Empresa
                header += Utils.FormatCode("", " ", 10);     //Posição 133 a 142  Uso Exclusivo FEBRABAN / CNAB: Brancos
                header += "1";        //Posição 103 a 142   Código Remessa / Retorno: "1"
                header += DateTime.Now.ToString("ddMMyyyy");       //Posição 144 a 151       Data de Geração do Arquivo
                header += Utils.FormatCode("", "0", 6);            //Posição 152 a 157       Hora de Geração do Arquivo
                header += "000001";         //Posição 158 a 163     Seqüência
                header += "081";            //Posição 164 a 166    No da Versão do Layout do Arquivo: "081"
                header += "00000";          //Posição 167 a 171    Densidade de Gravação do Arquivo: "00000"
                header += Utils.FormatCode("", " ", 69);
                header = Utils.SubstituiCaracteresEspeciais(header);
                //Retorno
                return header;
            }
            catch (Exception ex)
            {
                throw new Exception("Erro ao gerar HEADER do arquivo de remessa do CNAB400.", ex);
            }
        }


        public override string GerarHeaderRemessa(string numeroConvenio, Cedente cedente, TipoArquivo tipoArquivo, int numeroArquivoRemessa)
        {
            try
            {
                string _header = " ";

                base.GerarHeaderRemessa(numeroConvenio, cedente, tipoArquivo, numeroArquivoRemessa);

                switch (tipoArquivo)
                {

                    case TipoArquivo.CNAB240:
                        _header = GerarHeaderRemessaCNAB240(int.Parse(numeroConvenio), cedente, numeroArquivoRemessa);
                        break;
                    case TipoArquivo.Outro:
                        throw new Exception("Tipo de arquivo inexistente.");
                }

                return _header;

            }
            catch (Exception ex)
            {
                throw new Exception("Erro durante a geração do HEADER do arquivo de REMESSA.", ex);
            }
        }

        public override string GerarHeaderLoteRemessa(string numeroConvenio, Cedente cedente, int numeroArquivoRemessa, TipoArquivo tipoArquivo)
        {
            string header = "208";
            header += "0000";
            header += "0";
            header += new string(' ', 9);
            header += cedente.CPFCNPJ.Length == 11 ? "1" : "2";
            header += Utils.FormatCode(cedente.CPFCNPJ, "0", 14, true);
            header += Utils.FormatCode((cedente.Convenio > 0 ? cedente.Convenio.ToString() : ""), " ", 20, true);
            header += Utils.FormatCode(cedente.ContaBancaria.Agencia, 5);
            header += Utils.FormatCode(cedente.ContaBancaria.DigitoAgencia, "0", 1);
            header += Utils.FormatCode(cedente.ContaBancaria.Conta, "0", 12, true);
            header += cedente.ContaBancaria.DigitoConta;
            header += new string('0', 1);
            header += Utils.FormatCode(cedente.Nome, " ", 30);
            header += Utils.FormatCode("BTG Pactual", " ", 30);
            header += Utils.FormatCode("", " ", 10);
            header += "1";
            header += DateTime.Now.ToString("ddMMyyyy");
            header += Utils.FormatCode("", "0", 6);
            header += "000001";
            header += "081";
            header += "00000";
            header += Utils.FormatCode("", " ", 69);
            header = Utils.SubstituiCaracteresEspeciais(header);

            return header;
        }

        public override string GerarTrailerLoteRemessa(int numeroRegistro)
        {
            string trailer = Utils.FormatCode(Codigo.ToString(), "0", 3, true);
            trailer += "0001";
            trailer += "5";
            trailer += Utils.FormatCode("", " ", 9);
            trailer += Utils.FormatCode(numeroRegistro.ToString(), "0", 6, true);
            trailer += Utils.FormatCode("", "0", 6, true);
            trailer += Utils.FormatCode("", "0", 17, true);
            trailer += Utils.FormatCode("", "0", 6, true);
            trailer += Utils.FormatCode("", "0", 17, true);
            trailer += Utils.FormatCode("", "0", 6, true);
            trailer += Utils.FormatCode("", "0", 17, true);
            trailer += Utils.FormatCode("", "0", 6, true);
            trailer += Utils.FormatCode("", "0", 17, true);
            trailer += Utils.FormatCode("", " ", 8, true);
            trailer += Utils.FormatCode("", " ", 117);
            trailer = Utils.SubstituiCaracteresEspeciais(trailer);

            return trailer;
        }

        #endregion ARQUIVO DE REMESSA
    }
}
