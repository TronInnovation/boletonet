using BoletoNet.EDI.Banco;
using BoletoNet.Excecoes;
using BoletoNet.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;

namespace BoletoNet
{
    internal class Banco_Btg_Pactual : AbstractBanco, IBanco
    {
        #region CONSTRUTOR
        private int QtdRegistrosGeral { get; set; }
        private int QtdRegistrosLote { get; set; }
        private int QtdLotesGeral { get; set; }
        private int QtdTitulosLote { get; set; }
        private decimal ValorTotalTitulosLote { get; set; }


        private HeaderRetorno header;

        internal Banco_Btg_Pactual()
        {
            this.Nome = "BTG Pactual";
            this.Codigo = 208;
            this.Digito = "1";
            this.QtdRegistrosGeral = 0;
            this.QtdRegistrosLote = 0;
            this.QtdLotesGeral = 0;
            this.QtdTitulosLote = 0;
            this.ValorTotalTitulosLote = 0;
        }
        #endregion CONSTRUTOR

        public override void ValidaBoleto(Boleto boleto)
        {
            //Formata o tamanho do número da agência
            if (boleto.Cedente.ContaBancaria.Agencia.Length < 4)
                boleto.Cedente.ContaBancaria.Agencia = Utils.FormatCode(boleto.Cedente.ContaBancaria.Agencia, 4);

            //Formata o tamanho do número da conta corrente
            if (boleto.Cedente.ContaBancaria.Conta.Length < 5)
                boleto.Cedente.ContaBancaria.Conta = Utils.FormatCode(boleto.Cedente.ContaBancaria.Conta, 5);

            //Atribui o nome do banco ao local de pagamento
            if (boleto.LocalPagamento == "Até o vencimento, preferencialmente no ")
                boleto.LocalPagamento += Nome;
            else boleto.LocalPagamento = "PAGÁVEL PREFERENCIALMENTE NAS COOPERATIVAS DE CRÉDITO DO BTF PACTUAL";

            //Verifica se data do processamento é valida
            if (boleto.DataProcessamento == DateTime.MinValue) // diegomodolo (diego.ribeiro@nectarnet.com.br)
                boleto.DataProcessamento = DateTime.Now;

            //Verifica se data do documento é valida
            if (boleto.DataDocumento == DateTime.MinValue) // diegomodolo (diego.ribeiro@nectarnet.com.br)
                boleto.DataDocumento = DateTime.Now;

            string infoFormatoCodigoCedente = "formato AAAAPPCCCCC, onde: AAAA = Número da agência, PP = Posto do beneficiário, CCCCC = Código do beneficiário";

            var codigoCedente = Utils.FormatCode(boleto.Cedente.Codigo, 11);

            if (string.IsNullOrEmpty(codigoCedente))
                throw new BoletoNetException("Código do cedente deve ser informado, " + infoFormatoCodigoCedente);

            var conta = boleto.Cedente.ContaBancaria.Conta;
            if (boleto.Cedente.ContaBancaria != null &&
                (!codigoCedente.StartsWith(boleto.Cedente.ContaBancaria.Agencia) ||
                 !(codigoCedente.EndsWith(conta) || codigoCedente.EndsWith(conta.Substring(0, conta.Length - 1)))))
                //throw new BoletoNetException("Código do cedente deve estar no " + infoFormatoCodigoCedente);
                boleto.Cedente.Codigo = string.Format("{0}{1}{2}", boleto.Cedente.ContaBancaria.Agencia, boleto.Cedente.ContaBancaria.OperacaConta, boleto.Cedente.Codigo);

            //Verifica se o nosso número é válido
            var Length_NN = boleto.NossoNumero.Length;
            switch (Length_NN)
            {
                case 9:
                    boleto.NossoNumero = boleto.NossoNumero.Substring(0, Length_NN - 1);
                    boleto.DigitoNossoNumero = DigNossoNumeroSicredi(boleto);
                    boleto.NossoNumero += boleto.DigitoNossoNumero;
                    break;
                case 8:
                    boleto.DigitoNossoNumero = DigNossoNumeroSicredi(boleto);
                    boleto.NossoNumero += boleto.DigitoNossoNumero;
                    break;
                case 6:
                    var iNossoNumero = int.Parse(boleto.NossoNumero);
                    boleto.NossoNumero = DateTime.Now.ToString("yy") + "2" + iNossoNumero.ToString().PadLeft(5, '0');
                    boleto.DigitoNossoNumero = DigNossoNumeroSicredi(boleto);
                    boleto.NossoNumero += boleto.DigitoNossoNumero;
                    break;
                case 3:
                    boleto.NossoNumero = DateTime.Now.ToString("yy") + "2" + boleto.NossoNumero.ToString().PadLeft(5, '0');
                    boleto.DigitoNossoNumero = DigNossoNumeroSicredi(boleto);
                    boleto.NossoNumero += boleto.DigitoNossoNumero;
                    break;
                default:
                    throw new NotImplementedException("Nosso número inválido");
            }

            boleto.NossoNumeroSemFormatacao = boleto.NossoNumero;

            FormataCodigoBarra(boleto);
            if (boleto.CodigoBarra.Codigo.Length != 44)
                throw new BoletoNetException("Código de barras é inválido");

            FormataLinhaDigitavel(boleto);
            FormataNossoNumero(boleto);
        }


        public override void FormataNossoNumero(Boleto boleto)
        {
            string nossoNumero = boleto.NossoNumero;

            if (nossoNumero == null || nossoNumero.Length != 9)
            {
                throw new Exception("Erro ao tentar formatar nosso número, verifique o tamanho do campo");
            }

            try
            {
                boleto.NossoNumero = string.Format("{0}/{1}-{2}", nossoNumero.Substring(0, 2), nossoNumero.Substring(2, 6), nossoNumero.Substring(8));
            }
            catch (Exception ex)
            {
                throw new Exception("Erro ao formatar nosso número", ex);
            }
        }

        public override void FormataNumeroDocumento(Boleto boleto)
        {
            throw new NotImplementedException("Função do fomata número do documento não implementada.");
        }
        public override void FormataLinhaDigitavel(Boleto boleto)
        {
            //041M2.1AAAd1  CCCCC.CCNNNd2  NNNNN.041XXd3  V FFFF9999999999

            string campo1 = "7489" + boleto.CodigoBarra.Codigo.Substring(19, 5);
            int d1 = Mod10Sicredi(campo1);
            campo1 = FormataCampoLD(campo1) + d1.ToString();

            string campo2 = boleto.CodigoBarra.Codigo.Substring(24, 10);
            int d2 = Mod10Sicredi(campo2);
            campo2 = FormataCampoLD(campo2) + d2.ToString();

            string campo3 = boleto.CodigoBarra.Codigo.Substring(34, 10);
            int d3 = Mod10Sicredi(campo3);
            campo3 = FormataCampoLD(campo3) + d3.ToString();

            string campo4 = boleto.CodigoBarra.Codigo.Substring(4, 1);

            string campo5 = boleto.CodigoBarra.Codigo.Substring(5, 14);

            boleto.CodigoBarra.LinhaDigitavel = campo1 + "  " + campo2 + "  " + campo3 + "  " + campo4 + "  " + campo5;
        }
        private string FormataCampoLD(string campo)
        {
            return string.Format("{0}.{1}", campo.Substring(0, 5), campo.Substring(5));
        }

        public override void FormataCodigoBarra(Boleto boleto)
        {
            string valorBoleto = boleto.ValorBoleto.ToString("f").Replace(",", "").Replace(".", "");
            valorBoleto = Utils.FormatCode(valorBoleto, 10);

            var codigoCobranca = 1; //Código de cobrança com registro
            string cmp_livre =
                codigoCobranca +
                boleto.Carteira +
                Utils.FormatCode(boleto.NossoNumero, 9) +
                Utils.FormatCode(boleto.Cedente.Codigo, 11) + "10";

            string dv_cmpLivre = digSicredi(cmp_livre).ToString();

            var codigoTemp = GerarCodigoDeBarras(boleto, valorBoleto, cmp_livre, dv_cmpLivre);

            boleto.CodigoBarra.CampoLivre = cmp_livre;
            boleto.CodigoBarra.FatorVencimento = FatorVencimento(boleto);
            boleto.CodigoBarra.Moeda = 9;
            boleto.CodigoBarra.ValorDocumento = valorBoleto;

            int _dacBoleto = digSicredi(codigoTemp);

            if (_dacBoleto == 0 || _dacBoleto > 9)
                _dacBoleto = 1;

            boleto.CodigoBarra.Codigo = GerarCodigoDeBarras(boleto, valorBoleto, cmp_livre, dv_cmpLivre, _dacBoleto);
        }

        private string GerarCodigoDeBarras(Boleto boleto, string valorBoleto, string cmp_livre, string dv_cmpLivre, int? dv_geral = null)
        {
            return string.Format("{0}{1}{2}{3}{4}{5}{6}",
                Utils.FormatCode(Codigo.ToString(), 3),
                boleto.Moeda,
                dv_geral.HasValue ? dv_geral.Value.ToString() : string.Empty,
                FatorVencimento(boleto),
                valorBoleto,
                cmp_livre,
                dv_cmpLivre);
        }

        public override string GerarDetalheRemessa(Boleto boleto, int numeroRegistro, TipoArquivo tipoArquivo)
        {
            try
            {
                string _detalhe = " ";

                //base.GerarDetalheRemessa(boleto, numeroRegistro, tipoArquivo);

                switch (tipoArquivo)
                {
                    case TipoArquivo.CNAB240:
                        _detalhe = GerarDetalheRemessaCNAB240(boleto, numeroRegistro, tipoArquivo);
                        break;
                    case TipoArquivo.Outro:
                        throw new Exception("Tipo de arquivo inexistente.");
                }

                return _detalhe;

            }
            catch (Exception ex)
            {
                throw new Exception("Erro durante a geração do DETALHE arquivo de REMESSA.", ex);
            }
        }
        public override string GerarHeaderRemessa(string numeroConvenio, Cedente cedente, TipoArquivo tipoArquivo, int numeroArquivoRemessa, Boleto boletos)
        {
            throw new NotImplementedException("Função não implementada.");
        }
        public string GerarDetalheRemessaCNAB240(Boleto boleto, int numeroRegistro, TipoArquivo tipoArquivo)
        {
            try
            {
                string detalhe = Utils.FormatCode(Codigo.ToString(), "0", 3, true);
                detalhe += Utils.FormatCode("", "0", 4, true);
                detalhe += "3";
                detalhe += Utils.FormatCode(numeroRegistro.ToString(), 5);
                detalhe += "P 01";
                detalhe += Utils.FormatCode(boleto.Cedente.ContaBancaria.Agencia, 5);
                detalhe += "0";
                detalhe += Utils.FormatCode(boleto.Cedente.ContaBancaria.Conta, 12);
                detalhe += boleto.Cedente.ContaBancaria.DigitoConta;
                detalhe += " ";
                detalhe += Utils.FormatCode(boleto.NossoNumero.Replace("/", "").Replace("-", ""), 20);
                detalhe += "1";
                detalhe += (Convert.ToInt16(boleto.Carteira) == 1 ? "1" : "2");
                detalhe += "122";
                detalhe += Utils.FormatCode(boleto.NumeroDocumento, 15);
                detalhe += boleto.DataVencimento.ToString("ddMMyyyy");
                string valorBoleto = boleto.ValorBoleto.ToString("f").Replace(",", "").Replace(".", "");
                valorBoleto = Utils.FormatCode(valorBoleto, 13);
                detalhe += valorBoleto;
                detalhe += "00000 99A";
                detalhe += boleto.DataDocumento.ToString("ddMMyyyy");
                detalhe += "200000000";
                valorBoleto = boleto.JurosMora.ToString("f").Replace(",", "").Replace(".", "");
                valorBoleto = Utils.FormatCode(valorBoleto, 13);
                detalhe += valorBoleto;
                detalhe += "1";
                detalhe += boleto.DataDesconto.ToString("ddMMyyyy");
                valorBoleto = boleto.ValorDesconto.ToString("f").Replace(",", "").Replace(".", "");
                valorBoleto = Utils.FormatCode(valorBoleto, 13);
                detalhe += valorBoleto;
                detalhe += Utils.FormatCode("", 26);
                detalhe += Utils.FormatCode("", " ", 25);
                detalhe += "0001060090000000000 ";

                detalhe = Utils.SubstituiCaracteresEspeciais(detalhe);
                return detalhe;
            }
            catch (Exception e)
            {
                throw new Exception("Erro ao gerar DETALHE do arquivo CNAB240.", e);
            }
        }

        public override string GerarHeaderRemessa(Cedente cedente, TipoArquivo tipoArquivo, int numeroArquivoRemessa)
        {
            return GerarHeaderRemessa("0", cedente, tipoArquivo, numeroArquivoRemessa);
        }

        public override string GerarHeaderRemessa(string numeroConvenio, Cedente cedente, TipoArquivo tipoArquivo, int numeroArquivoRemessa)
        {
            try
            {
                string _header = " ";

                base.GerarHeaderRemessa("0", cedente, tipoArquivo, numeroArquivoRemessa);

                switch (tipoArquivo)
                {

                    case TipoArquivo.CNAB240:
                        _header = GerarHeaderRemessaCNAB240(cedente, numeroArquivoRemessa);
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

        private string GerarHeaderLoteRemessaCNAB240(Cedente cedente, int numeroArquivoRemessa)
        {
            QtdLotesGeral++;
            QtdRegistrosGeral++;
            QtdRegistrosLote = 1;
            QtdTitulosLote = 0;
            ValorTotalTitulosLote = 0;

            var headerLoteRemessa = new StringBuilder();

            try
            {
                headerLoteRemessa.Append("748");//Posição 001 a 003
                headerLoteRemessa.Append(Utils.FitStringLength(Convert.ToString(QtdLotesGeral), 4, 4, '0', 0, true, true, true));//Posição 004 a 007
                headerLoteRemessa.Append("1");//Posição 008
                headerLoteRemessa.Append("R");//Posição 009 
                headerLoteRemessa.Append("01");//Posição 010 a 011
                headerLoteRemessa.Append(new string(' ', 2));//Posição 012 a 013
                headerLoteRemessa.Append("040");//Posição 014 a 016
                headerLoteRemessa.Append(new string(' ', 1));//Posição 017
                headerLoteRemessa.Append(Utils.FitStringLength(cedente.CPFCNPJ.Length == 11 ? "1" : "2", 1, 1, '0', 0, true, true, true));//Posição 018
                headerLoteRemessa.Append(Utils.FitStringLength(Utils.OnlyNumbers(cedente.CPFCNPJ), 15, 15, '0', 0, true, true, true));//Posição 019 a 033
                headerLoteRemessa.Append(Utils.FitStringLength(" ", 20, 20, ' ', 0, true, true, false));//Posição 034 a 053
                headerLoteRemessa.Append(Utils.FitStringLength(Utils.OnlyNumbers(cedente.ContaBancaria.Agencia), 5, 5, '0', 0, true, true, true));//Posição 054 a 058
                headerLoteRemessa.Append(Utils.FitStringLength(" ", 1, 1, '0', 0, true, true, true));//Posição 059
                headerLoteRemessa.Append(Utils.FitStringLength(Utils.OnlyNumbers(cedente.ContaBancaria.Conta), 12, 12, '0', 0, true, true, true));//Posição 060 a 071
                headerLoteRemessa.Append(Utils.FitStringLength(Utils.OnlyNumbers(cedente.ContaBancaria.DigitoConta), 1, 1, '0', 0, true, true, true));//Posição 072
                headerLoteRemessa.Append(new string(' ', 1));//Posição 073
                headerLoteRemessa.Append(Utils.FitStringLength(cedente.Nome, 30, 30, ' ', 0, true, true, false));//Posição 074 a 103
                headerLoteRemessa.Append(Utils.FitStringLength(" ", 40, 40, ' ', 0, true, true, false));//Posição 104 a 143  ----Verificar campo mensagem
                headerLoteRemessa.Append(Utils.FitStringLength(" ", 40, 40, ' ', 0, true, true, false));//Posição 144 a 183  ----Verificar campo mensagem
                headerLoteRemessa.Append(Utils.FitStringLength(numeroArquivoRemessa.ToString(), 8, 8, '0', 0, true, true, true));//Posição 184 a 191
                headerLoteRemessa.Append(DateTime.Today.ToString("ddMMyyyy"));//Posição 192 a 199
                headerLoteRemessa.Append("00000000");//Posição 200 a 207
                headerLoteRemessa.Append(new string(' ', 33));//Posição 208 a 240

                return Utils.SubstituiCaracteresEspeciais(headerLoteRemessa.ToString());

            }
            catch (Exception ex)
            {
                throw new Exception("Erro ao gerar HEADER DO LOTE DE REMESSA do arquivo de remessa do CNAB240.", ex);
            }
        }

        public string DigNossoNumeroSicredi(string seq)
        {
            //string codigoCedente = boleto.Cedente.Codigo;           //código do beneficiário aaaappccccc
            //string nossoNumero = boleto.NossoNumero;                //ano atual (yy), indicador de geração do nosso número (b) e o número seqüencial do beneficiário (nnnnn);

            //string seq = boleto.NossoNumero; //string.Concat(codigoCedente, nossoNumero); // = aaaappcccccyybnnnnn
            /* Variáveis
             * -------------
             * d - Dígito
             * s - Soma
             * p - Peso
             * b - Base
             * r - Resto
             */

            int d, s = 0, p = 2, b = 9;
            //Atribui os pesos de {2..9}
            for (int i = seq.Length - 1; i >= 0; i--)
            {
                s = s + (Convert.ToInt32(seq.Substring(i, 1)) * p);
                if (p < b)
                    p = p + 1;
                else
                    p = 2;
            }
            d = 11 - (s % 11);//Calcula o Módulo 11;
            if (d > 9)
                d = 0;
            return d.ToString();
        }

        public String PreparaNossoNumero(Boleto boleto)
        {
            try
            {
                var anoReferencia = Utils.RightStr(boleto.DataProcessamento.Year.ToString(), 2);
                // Calcula o Digito Verificador do Nosso Número
                string seq = string.Format("{0}{1}{2}{3}{4}{5}",
                                           Utils.RightStr(Utils.FormatCode(boleto.Cedente.ContaBancaria.Agencia, 4), 4),
                                           Utils.RightStr(Utils.FormatCode(boleto.Cedente.CodigoTransmissao, 2), 2),
                                           Utils.RightStr(Utils.FormatCode(boleto.Cedente.Codigo, 5), 5),
                                           anoReferencia,
                                           Utils.RightStr(Utils.FormatCode(boleto.NossoNumeroSemFormatacao, 5), 5));

                string dv_NossoNumero = DigNossoNumeroSicredi(seq);

                return string.Concat(anoReferencia, Utils.FormatCode(boleto.NossoNumero, 5), dv_NossoNumero);
            }
            catch (Exception ex)
            {
                throw new Exception("Erro ao formatar nosso número", ex);
            }
        }

        public String DigitoVerificador(Boleto boleto)
        {
            var dvString = Utils.FitStringLength(boleto.Cedente.ContaBancaria.Agencia, 4, 4, '0', 0, true, true, true);
            dvString += Utils.FitStringLength(String.Concat(Utils.RightStr(boleto.Cedente.Codigo, 6), boleto.Cedente.DigitoCedente), 10, 10, '0', 0, true, true, true);
            dvString += Utils.FitStringLength(boleto.NossoNumeroSemFormatacao, 7, 7, '0', 0, true, true, true);
            var soma = 0;

            return Mod11(dvString, 7913).ToString();
        }

        public override string GerarDetalheSegmentoPRemessa(Boleto boleto, int numeroRegistro, string numeroConvenio)
        {
            QtdRegistrosGeral++;
            QtdRegistrosLote++;
            QtdTitulosLote++;
            ValorTotalTitulosLote = ValorTotalTitulosLote + boleto.ValorBoleto;

            var detalhe = new StringBuilder();

            try
            {
                var valorJuros = 0.00m;

                //Montagem do Detalhe
                detalhe.Append("748"); //Posição 001 a 003
                detalhe.Append(Utils.FitStringLength(Convert.ToString(QtdLotesGeral), 4, 4, '0', 0, true, true, true));//Posição 004 a 007
                detalhe.Append("3");//Posição 008
                detalhe.Append(Utils.FitStringLength(Convert.ToString(numeroRegistro), 5, 5, '0', 0, true, true, true));//Posição 009 a 013
                detalhe.Append("P");//Posição 014
                detalhe.Append(" ");//Posição 015
                detalhe.Append("01"); //Posição 016 a 017
                detalhe.Append(Utils.FitStringLength(Utils.OnlyNumbers(boleto.Cedente.ContaBancaria.Agencia), 5, 5, '0', 0, true, true, true)); //Posição 018 a 022
                detalhe.Append(Utils.FitStringLength(" ", 1, 1, ' ', 0, true, true, true)); //Posição 023
                detalhe.Append(Utils.FitStringLength(Utils.OnlyNumbers(boleto.Cedente.ContaBancaria.Conta), 12, 12, '0', 0, true, true, true)); //Posição 024 a 035
                detalhe.Append(Utils.FitStringLength(Utils.OnlyNumbers(boleto.Cedente.ContaBancaria.DigitoConta), 1, 1, '0', 0, true, true, true)); //Posição 036
                detalhe.Append(" ");//Posição 037
                detalhe.Append(Utils.FitStringLength(Utils.OnlyNumbers(boleto.NossoNumeroSemFormatacao), 20, 20, '0', 0, true, true, false));//Posição 038 a 057
                                                                                                                                             //                detalhe.Append(Utils.FitStringLength(Utils.OnlyNumbers(PreparaNossoNumero(boleto)), 20, 20, ' ', 0, true, true, false));//Posição 038 a 057
                detalhe.Append(Utils.FitStringLength(Utils.OnlyNumbers(boleto.Carteira), 1, 1, '0', 0, true, true, true));//Posição 058
                detalhe.Append("1");//Posição 059
                detalhe.Append("1");//Posição 060
                detalhe.Append(Utils.FitStringLength("2", 1, 1, '0', 0, true, true, true));//Posição 061  
                detalhe.Append(Utils.FitStringLength("2", 1, 1, '0', 0, true, true, true));//Posição 062  
                detalhe.Append(Utils.FitStringLength(Utils.OnlyNumbers(boleto.NumeroDocumento), 15, 15, ' ', 0, true, true, false));//Posição 063 a 077
                detalhe.Append(Utils.FitStringLength(boleto.DataVencimento.ToString("ddMMyyyy"), 8, 8, '0', 0, true, true, true)); //Posição 078 a 085
                detalhe.Append(Utils.FitStringLength(boleto.ValorBoleto.ToString("0.00").Replace(",", ""), 15, 15, '0', 0, true, true, true));//Posição 086 a 100
                detalhe.Append("00000");//Posição 101 a 105
                detalhe.Append(" ");//Posição 106
                detalhe.Append("03");//Posição 107 a 108
                detalhe.Append(Utils.FitStringLength(boleto.Aceite, 1, 1, 'A', 0, true, true, false));//Posição 109
                detalhe.Append(Utils.FitStringLength(DateTime.Today.ToString("ddMMyyyy"), 8, 8, '0', 0, true, true, true));//Posição 110 a 117
                detalhe.Append(Utils.FitStringLength((boleto.CodJurosMora != null) && (boleto.CodJurosMora != "") && (boleto.CodJurosMora != "0") ? boleto.CodJurosMora.ToString() : "2", 1, 1, '1', 0, true, true, true));//Posição 118
                detalhe.Append(Utils.FitStringLength(boleto.DataVencimento.AddDays(1).ToString("ddMMyyyy"), 8, 8, '0', 0, true, true, true));//Posição 119 a 126

                if (boleto.CodJurosMora == "1" || boleto.CodJurosMora == null || boleto.CodJurosMora != "0")//Atribuindo a porcentagem de juros diários
                    valorJuros = (decimal)(((boleto.ValorBoleto * boleto.PercJurosMora) / 100));
                else                                                      //Calculando valor do juros com base na porcentagem informada  
                    valorJuros = (decimal)(((boleto.ValorBoleto * boleto.PercJurosMora) / 100) / 30);

                detalhe.Append(Utils.FitStringLength(valorJuros.ToString("0.00").Replace(",", "").Replace(".", ""), 15, 15, '0', 0, true, true, true));//Posição 127 a 141
                detalhe.Append(Utils.FitStringLength(boleto.DataDesconto >= DateTime.Now ? "1" : "3", 1, 1, '0', 0, true, true, true));//Posição 142  
                detalhe.Append(Utils.FitStringLength(boleto.DataDesconto != null && boleto.DataDesconto >= Convert.ToDateTime("01/01/1990") ? boleto.DataDesconto.ToString("ddMMyyyy") : "0", 8, 8, '0', 0, true, true, true));//Posição 143 a 150   
                detalhe.Append(Utils.FitStringLength(boleto.ValorDesconto.ToString("0.00").Replace(",", ""), 15, 15, '0', 0, true, true, true));//Posição 151 a 165 
                detalhe.Append(Utils.FitStringLength(boleto.IOF.ToString("0.00").Replace(",", ""), 15, 15, '0', 0, true, true, true));//Posição 166 a 180 
                detalhe.Append(Utils.FitStringLength("0", 15, 15, '0', 0, true, true, true));//Posição 181 a 195 
                detalhe.Append(Utils.FitStringLength(boleto.NossoNumero, 25, 25, '0', 0, true, true, true));//Posição 196 a 220
                detalhe.Append(boleto.ProtestaTitulos == true ? "1" : "3");//Protesto
                detalhe.Append(boleto.ProtestaTitulos == true ? boleto.NumeroDiasProtesto.ToString() : "30");//dias protesto
                detalhe.Append("1");//Posição 224
                detalhe.Append("060");//Posição 225 a 227
                detalhe.Append("09");//Posição 228 a 229
                detalhe.Append("0000000000");//Posição 230 a 239
                detalhe.Append(" ");//Posição 240

                //Retorno
                return Utils.SubstituiCaracteresEspeciais(detalhe.ToString());

            }
            catch (Exception ex)
            {
                throw new Exception("Erro ao gerar DETALHE SEGMENTO P do arquivo de remessa do CNAB240.", ex);
            }

        }

        public override string GerarDetalheSegmentoQRemessa(Boleto boleto, int numeroRegistro, TipoArquivo tipoArquivo)
        {
            QtdRegistrosGeral++;
            QtdRegistrosLote++;

            var detalhe = new StringBuilder();

            try
            {
                detalhe.Append("748");//Posição 001 a 003
                detalhe.Append(Utils.FitStringLength(Convert.ToString(QtdLotesGeral), 4, 4, '0', 0, true, true, true));//Posição 004 a 007
                detalhe.Append("3");//Posição 008
                detalhe.Append(Utils.FitStringLength(Convert.ToString(numeroRegistro), 5, 5, '0', 0, true, true, true));//Posição 009 a 013
                detalhe.Append("Q");//Posição 014 
                detalhe.Append(" ");//Posição 015
                detalhe.Append("01");//Posição 016 a 017
                detalhe.Append(Utils.FitStringLength(boleto.Sacado.CPFCNPJ.Length < 14 ? "1" : "2", 1, 1, '0', 0, true, true, true));//Posição  018
                detalhe.Append(Utils.FitStringLength(Utils.OnlyNumbers(boleto.Sacado.CPFCNPJ), 15, 15, '0', 0, true, true, true));//Posição 019 a 033
                detalhe.Append(Utils.FitStringLength(boleto.Sacado.Nome, 40, 40, ' ', 0, true, true, false));//Posição 034 073
                detalhe.Append(Utils.FitStringLength(boleto.Sacado.Endereco.End != null ? boleto.Sacado.Endereco.End : "", 40, 40, ' ', 0, true, true, false));//Posição 074 a 113
                detalhe.Append(Utils.FitStringLength(boleto.Sacado.Endereco.Bairro != null ? boleto.Sacado.Endereco.Bairro : "", 15, 15, ' ', 0, true, true, false));//Posição 114 a 128
                detalhe.Append(Utils.FitStringLength(boleto.Sacado.Endereco.CEP != null ? boleto.Sacado.Endereco.CEP.Replace("-", "").Replace(".", "").Replace("/", "") : "", 8, 8, '0', 0, true, true, true)); //Posição 129 a 136
                detalhe.Append(Utils.FitStringLength(boleto.Sacado.Endereco.Cidade != null ? boleto.Sacado.Endereco.Cidade : "", 15, 15, ' ', 0, true, true, false));//Posição 137 a 151
                detalhe.Append(Utils.FitStringLength(boleto.Sacado.Endereco.UF != null ? boleto.Sacado.Endereco.UF : "", 2, 2, ' ', 0, true, true, false));//Posição 152 a 153
                detalhe.Append(Utils.FitStringLength(boleto.Sacado.CPFCNPJ.Length < 14 ? "1" : "2", 1, 1, '0', 0, true, true, false));//Posição 154
                detalhe.Append(Utils.FitStringLength(Utils.OnlyNumbers(boleto.Sacado.CPFCNPJ), 15, 15, '0', 0, true, true, true));//Posição 155 a 169
                detalhe.Append(Utils.FitStringLength(boleto.Sacado.Nome, 40, 40, ' ', 0, true, true, false));//Posição 170 a 209
                detalhe.Append("000");//Posição 210 a 212
                detalhe.Append(Utils.FitStringLength(" ", 20, 20, ' ', 0, true, true, true));//Posição 213 a 232
                detalhe.Append(new string(' ', 8));//Posição 233 a 240

                return Utils.SubstituiCaracteresEspeciais(detalhe.ToString());
            }
            catch (Exception ex)
            {
                throw new Exception("Erro ao gerar DETALHE SEGMENTO P do arquivo de remessa do CNAB240.", ex);
            }

        }
        public override string GerarDetalheSegmentoRRemessa(Boleto boleto, int numeroRegistro, TipoArquivo tipoArquivo)
        {
            QtdRegistrosGeral++;
            QtdRegistrosLote++;

            var detalhe = new StringBuilder();

            try
            {
                detalhe.Append("748");//Posição 001 a 003
                detalhe.Append(Utils.FitStringLength(Convert.ToString(QtdLotesGeral), 4, 4, '0', 0, true, true, true));//Posição 004 a 007
                detalhe.Append("3");//Posição 008
                detalhe.Append(Utils.FitStringLength(Convert.ToString(numeroRegistro), 5, 5, '0', 0, true, true, true));//Posição 009 a 013
                detalhe.Append("R");//Posição 014
                detalhe.Append(" ");//Posição 015
                detalhe.Append("01");//Posição 016 a 017
                detalhe.Append(Utils.FitStringLength("0", 1, 1, '0', 0, true, true, true));//Posição 018
                detalhe.Append(Utils.FitStringLength("0", 8, 8, '0', 0, true, true, true));//Posição 019 a 026
                detalhe.Append(Utils.FitStringLength("0", 15, 15, '0', 0, true, true, true));//Posição 027 a 041
                detalhe.Append("0");//Posição 042
                detalhe.Append(Utils.FitStringLength("0", 8, 8, '0', 0, true, true, true));//Posição 043 a 050
                detalhe.Append(Utils.FitStringLength("0", 15, 15, '0', 0, true, true, true));//Posição 051 a 065
                detalhe.Append(Utils.FitStringLength("2", 1, 1, '1', 0, true, true, true));//Posição 066
                detalhe.Append(Utils.FitStringLength(boleto.DataVencimento.ToString("ddMMyyyy"), 8, 8, '0', 0, true, true, true)); //Posição 067 a 074

                decimal percentualMulta = 0.0m;

                if (boleto.CodJurosMora == "2")
                {
                    percentualMulta = (decimal)boleto.PercMulta;
                }
                else
                {
                    percentualMulta = (decimal)boleto.ValorMulta * 100 / boleto.ValorBoleto;
                }

                detalhe.Append(Utils.FitStringLength(string.Format("{0:F2}", percentualMulta).Replace(",", "").Replace(".", ""), 15, 15, '0', 0, true, true, true));//Posição 075 a 089
                detalhe.Append(new string(' ', 10));//Posição 090 a 099
                detalhe.Append(Utils.FitStringLength(" ", 40, 40, ' ', 0, true, true, false));//Posição 100 a 139
                detalhe.Append(Utils.FitStringLength(" ", 40, 40, ' ', 0, true, true, false));//Posição 140 a 179
                detalhe.Append(new string(' ', 20));//Posição 180 a 199
                detalhe.Append(Utils.FitStringLength("0", 8, 8, '0', 0, true, true, true));//Posição 200 a 207
                detalhe.Append(Utils.FitStringLength("0", 3, 3, '0', 0, true, true, true));//Posição 208 a 210
                detalhe.Append(Utils.FitStringLength("0", 5, 5, '0', 0, true, true, true));//Posição 211 a 215
                detalhe.Append(" ");//Posição 216
                detalhe.Append(Utils.FitStringLength("0", 12, 12, '0', 0, true, true, true));//Posição 217 a 228
                detalhe.Append(" ");//Posição 229 
                detalhe.Append(" ");//Posição 230
                detalhe.Append("0");//Posição 231
                detalhe.Append(new string(' ', 9));//Posição 232 a 240

                return Utils.SubstituiCaracteresEspeciais(detalhe.ToString());
            }
            catch (Exception ex)
            {
                throw new Exception("Erro ao gerar DETALHE SEGMENTO R do arquivo de remessa do CNAB240.", ex);
            }

        }

        public override string GerarDetalheSegmentoSRemessa(Boleto boleto, int numeroRegistro, TipoArquivo tipoArquivo)
        {
            QtdRegistrosGeral++;
            QtdRegistrosLote++;

            var detalhe = new StringBuilder();

            try
            {
                detalhe.Append("748");//Posição 001 a 003
                detalhe.Append(Utils.FitStringLength(Convert.ToString(QtdLotesGeral), 4, 4, '0', 0, true, true, true));//Posição 004 a 007
                detalhe.Append("3");//Posição 008
                detalhe.Append(Utils.FitStringLength(Convert.ToString(numeroRegistro), 5, 5, '0', 0, true, true, true));//Posição 009 a 013
                detalhe.Append("S");//Posição 014
                detalhe.Append(" ");//Posição 015
                detalhe.Append("01");//Posição 016 a 017
                detalhe.Append("3");//Posição 018
                detalhe.Append(Utils.FitStringLength(boleto.Cedente.Instrucoes.Count > 0 ? boleto.Cedente.Instrucoes[0].Descricao : "", 40, 40, ' ', 0, true, true, false));//Posição 019 a 058
                detalhe.Append(Utils.FitStringLength(boleto.Cedente.Instrucoes.Count > 1 ? boleto.Cedente.Instrucoes[1].Descricao : "", 40, 40, ' ', 0, true, true, false));//Posição 059 a 098
                detalhe.Append(Utils.FitStringLength(boleto.Cedente.Instrucoes.Count > 2 ? boleto.Cedente.Instrucoes[2].Descricao : "", 40, 40, ' ', 0, true, true, false));//Posição 099 a 138
                detalhe.Append(Utils.FitStringLength(boleto.Cedente.Instrucoes.Count > 3 ? boleto.Cedente.Instrucoes[3].Descricao : "", 40, 40, ' ', 0, true, true, false));//Posição 139 a 178
                detalhe.Append(Utils.FitStringLength(boleto.Cedente.Instrucoes.Count > 4 ? boleto.Cedente.Instrucoes[4].Descricao : "", 40, 40, ' ', 0, true, true, false));//Posição 179 a 218
                detalhe.Append(new string(' ', 22));//Posição 139 a 240

                return Utils.SubstituiCaracteresEspeciais(detalhe.ToString());
            }
            catch (Exception ex)
            {
                throw new Exception("Erro ao gerar DETALHE SEGMENTO S do arquivo de remessa do CNAB240.", ex);
            }

        }


        public override string GerarHeaderLoteRemessa(string numeroConvenio, Cedente cedente, int numeroArquivoRemessa, TipoArquivo tipoArquivo)
        {
            try
            {
                string header = " ";

                switch (tipoArquivo)
                {

                    case TipoArquivo.CNAB240:
                        header = GerarHeaderLoteRemessaCNAB240(cedente, numeroArquivoRemessa);
                        break;
                    case TipoArquivo.CNAB400:
                        // não tem no CNAB 400 header = GerarHeaderLoteRemessaCNAB400(0, cedente, numeroArquivoRemessa);
                        break;
                    case TipoArquivo.Outro:
                        throw new Exception("Tipo de arquivo inexistente.");
                }

                return header;

            }
            catch (Exception ex)
            {
                throw new Exception("Erro durante a geração do HEADER DO LOTE do arquivo de REMESSA.", ex);
            }
        }

        public string GerarHeaderRemessaCNAB240(Cedente cedente, int numeroArquivoRemessa)
        {
            QtdRegistrosGeral = 1;
            QtdLotesGeral = 0;

            var header = new StringBuilder();

            try
            {
                header.Append("748");//Posição 001 a 003
                header.Append("0000");//Posição 004 a 007
                header.Append("0");//Posição 008

                header.Append(Utils.FitStringLength(" ", 9, 9, ' ', 0, true, true, false));//Posição 009 a 017
                header.Append(Utils.FitStringLength(cedente.CPFCNPJ.Length == 11 ? "1" : "2", 1, 1, '0', 0, true, true, true));//Posição 018
                header.Append(Utils.FitStringLength(Utils.OnlyNumbers(cedente.CPFCNPJ), 14, 14, '0', 0, true, true, true));//Posição 019 a 032
                header.Append(new string(' ', 20));//Posição 033 a 52
                header.Append(Utils.FitStringLength(Utils.OnlyNumbers(cedente.ContaBancaria.Agencia), 5, 5, '0', 0, true, true, true));//Posição 053 a 057
                header.Append(Utils.FitStringLength(" ", 1, 1, ' ', 0, true, true, true));//Posição 058
                header.Append(Utils.FitStringLength(Utils.OnlyNumbers(cedente.ContaBancaria.Conta), 12, 12, '0', 0, true, true, true));//Posição 059 a 070
                header.Append(Utils.FitStringLength(Utils.OnlyNumbers(cedente.ContaBancaria.DigitoConta), 1, 1, '0', 0, true, true, true));//Posição 071
                header.Append(" ");//Posição 072
                header.Append(Utils.FitStringLength(cedente.Nome, 30, 30, ' ', 0, true, true, false));//Posição 073 a 102
                header.Append(Utils.FitStringLength("SICREDI", 30, 30, ' ', 0, true, true, false));//Posição 103 a 132
                header.Append(new string(' ', 10));//Posição 133 a 142
                header.Append("1");//Posição 143 ------------------------
                header.Append(DateTime.Today.ToString("ddMMyyyy"));//Posição 144 a 151
                header.Append(DateTime.Now.ToString("HHmmss"));//Posição 152 a 157
                header.Append(Utils.FitStringLength(numeroArquivoRemessa.ToString(), 6, 6, '0', 0, true, true, true));//Posição 158 a 163
                header.Append("081");//Posição 164 a 166
                header.Append("01600");//Posição 167 a 171
                header.Append(new string(' ', 20));//Posição 172 a 191
                header.Append(new string(' ', 20));//Posição 192 a 211
                header.Append(new string(' ', 29));//Posição 212 a 240

                return Utils.SubstituiCaracteresEspeciais(header.ToString());

            }
            catch (Exception ex)
            {
                throw new Exception("Erro ao gerar HEADER do arquivo de remessa do CNAB240.", ex);
            }
        }

        public override string GerarTrailerRemessa(int numeroRegistro, TipoArquivo tipoArquivo, Cedente cedente, decimal vltitulostotal)
        {
            try
            {
                string _trailer = " ";

                switch (tipoArquivo)
                {
                    case TipoArquivo.CNAB240:
                        _trailer = GerarTrailerRemessa240(numeroRegistro);
                        break;
                    case TipoArquivo.Outro:
                        throw new Exception("Tipo de arquivo inexistente.");
                }

                return _trailer;

            }
            catch (Exception ex)
            {
                throw new Exception("", ex);
            }
        }

        public override string GerarTrailerLoteRemessa(int numeroRegistro)
        {
            QtdRegistrosGeral++;
            QtdRegistrosLote++;

            var trailler = new StringBuilder();

            try
            {
                trailler.Append("748");//Posição 001 a 003
                trailler.Append(Utils.FitStringLength(Convert.ToString(QtdLotesGeral), 4, 4, '0', 0, true, true, true));//Posição 004 a 007
                trailler.Append("5");//Posição 008
                trailler.Append(new string(' ', 9));//Posição 009 a 017
                trailler.Append(Utils.FitStringLength(Convert.ToString(QtdRegistrosLote), 6, 6, '0', 0, true, true, true));//Posição 018 a 023
                trailler.Append(Utils.FitStringLength("0", 6, 6, '0', 0, true, true, true));//Posição 024 a 029
                trailler.Append(Utils.FitStringLength("0", 17, 17, '0', 0, true, true, true));//Posição 030 a 046
                trailler.Append(Utils.FitStringLength("0", 6, 6, '0', 0, true, true, true));//Posição 047 a 052
                trailler.Append(Utils.FitStringLength("0", 17, 17, '0', 0, true, true, true));//Posição 053 a 069
                trailler.Append(Utils.FitStringLength("0", 6, 6, '0', 0, true, true, true));//Posição 070 a 075
                trailler.Append(Utils.FitStringLength("0", 17, 17, '0', 0, true, true, true));//Posição 076 a 092
                trailler.Append(Utils.FitStringLength("0", 6, 6, '0', 0, true, true, true));//Posição 093 a 098
                trailler.Append(Utils.FitStringLength("0", 17, 17, '0', 0, true, true, true));//Posição 099 a 115
                trailler.Append(new string(' ', 8));//Posição 116 a 123
                trailler.Append(new string(' ', 117));//Posição 124 a 240

                return Utils.SubstituiCaracteresEspeciais(trailler.ToString());
            }
            catch (Exception ex)
            {
                throw new Exception("Erro ao gerar TRAILLER DO LOTE do arquivo de remessa do CNAB240.", ex);
            }

        }

        public override string GerarTrailerArquivoRemessa(int numeroRegistro)
        {
            QtdRegistrosGeral++;

            var trailler = new StringBuilder();

            try
            {
                trailler.Append("748");//Posição 001 a 003
                trailler.Append("9999");//Posição 004 a 007
                trailler.Append("9");//Posição 008
                trailler.Append(new string(' ', 9));//Posição 009 a 017
                trailler.Append(Utils.FitStringLength(Convert.ToString(QtdLotesGeral), 6, 6, '0', 0, true, true, true));//Posição 018 a 023
                trailler.Append(Utils.FitStringLength(Convert.ToString(QtdRegistrosGeral), 6, 6, '0', 0, true, true, true));//Posição 024 a 029
                trailler.Append("000000");//Posição 030 a 035
                trailler.Append(new string(' ', 205));//Posição 036 a 240

                return Utils.SubstituiCaracteresEspeciais(trailler.ToString());
            }
            catch (Exception ex)
            {
                throw new Exception("Erro durante a geração do registro TRAILER do arquivo de REMESSA.", ex);
            }
        }

        public string GerarTrailerRemessa240(int numeroRegistro)
        {
            QtdRegistrosGeral++;

            var trailler = new StringBuilder();

            try
            {
                trailler.Append("748");//Posição 001 a 003
                trailler.Append("9999");//Posição 004 a 007
                trailler.Append("9");//Posição 008
                trailler.Append(new string(' ', 9));//Posição 009 a 017
                trailler.Append(Utils.FitStringLength(Convert.ToString(QtdLotesGeral), 6, 6, '0', 0, true, true, true));//Posição 018 a 023
                trailler.Append(Utils.FitStringLength(Convert.ToString(QtdRegistrosGeral), 6, 6, '0', 0, true, true, true));//Posição 024 a 029
                trailler.Append("000000");//Posição 030 a 035
                trailler.Append(new string(' ', 205));//Posição 036 a 240

                return Utils.SubstituiCaracteresEspeciais(trailler.ToString());
            }
            catch (Exception ex)
            {
                throw new Exception("Erro durante a geração do registro TRAILER do arquivo de REMESSA.", ex);
            }
        }

        public int Mod10Sicredi(string seq)
        {
            /* Variáveis
             * -------------
             * d - Dígito
             * s - Soma
             * p - Peso
             * b - Base
             * r - Resto
             */

            int d, s = 0, p = 2, b = 2, r;

            for (int i = seq.Length - 1; i >= 0; i--)
            {

                r = (Convert.ToInt32(seq.Substring(i, 1)) * p);
                if (r > 9)
                    r = SomaDezena(r);
                s = s + r;
                if (p < b)
                    p++;
                else
                    p--;
            }

            d = Multiplo10(s);
            return d;
        }

        public int SomaDezena(int dezena)
        {
            string d = dezena.ToString();
            int d1 = Convert.ToInt32(d.Substring(0, 1));
            int d2 = Convert.ToInt32(d.Substring(1));
            return d1 + d2;
        }

        public int digSicredi(string seq)
        {
            /* Variáveis
             * -------------
             * d - Dígito
             * s - Soma
             * p - Peso
             * b - Base
             * r - Resto
             */

            int d, s = 0, p = 2, b = 9;

            for (int i = seq.Length - 1; i >= 0; i--)
            {
                s = s + (Convert.ToInt32(seq.Substring(i, 1)) * p);
                if (p < b)
                    p = p + 1;
                else
                    p = 2;
            }

            d = 11 - (s % 11);
            if (d > 9)
                d = 0;
            return d;
        }

        public string DigNossoNumeroSicredi(Boleto boleto, bool arquivoRemessa = false)
        {
            if (!string.IsNullOrEmpty(boleto.DigitoNossoNumero))
                return boleto.DigitoNossoNumero;

            //Adicionado por diego.dariolli pois ao gerar remessa o dígito saía errado pois faltava agência e posto no código do cedente
            string codigoCedente = ""; //código do beneficiário aaaappccccc
            if (arquivoRemessa)
            {
                if (string.IsNullOrEmpty(boleto.Cedente.ContaBancaria.OperacaConta))
                    throw new Exception("O código do posto beneficiário não foi informado.");

                codigoCedente = string.Concat(boleto.Cedente.ContaBancaria.Agencia, boleto.Cedente.ContaBancaria.OperacaConta, boleto.Cedente.Codigo);
            }
            else
                codigoCedente = boleto.Cedente.Codigo;

            string nossoNumero = boleto.NossoNumero; //ano atual (yy), indicador de geração do nosso número (b) e o número seqüencial do beneficiário (nnnnn);

            string seq = string.Concat(codigoCedente, nossoNumero); // = aaaappcccccyybnnnnn
            /* Variáveis
             * -------------
             * d - Dígito
             * s - Soma
             * p - Peso
             * b - Base
             * r - Resto
             */

            int d, s = 0, p = 2, b = 9;
            //Atribui os pesos de {2..9}
            for (int i = seq.Length - 1; i >= 0; i--)
            {
                s = s + (Convert.ToInt32(seq.Substring(i, 1)) * p);
                if (p < b)
                    p = p + 1;
                else
                    p = 2;
            }
            d = 11 - (s % 11);//Calcula o Módulo 11;
            if (d > 9)
                d = 0;
            return d.ToString();
        }

        public bool ValidarRemessaCNAB240(string numeroConvenio, IBanco banco, Cedente cedente, Boletos boletos, int numeroArquivoRemessa, out string mensagem)
        {
            string vMsg = string.Empty;
            mensagem = vMsg;
            return true;
            //throw new NotImplementedException("Função não implementada.");
        }
        public override bool ValidarRemessa(TipoArquivo tipoArquivo, string numeroConvenio, IBanco banco, Cedente cedente, Boletos boletos, int numeroArquivoRemessa, out string mensagem)
        {
            bool vRetorno = true;
            string vMsg = string.Empty;
            //            
            switch (tipoArquivo)
            {
                case TipoArquivo.CNAB240:
                    vRetorno = ValidarRemessaCNAB240(numeroConvenio, banco, cedente, boletos, numeroArquivoRemessa, out vMsg);
                    break;
                case TipoArquivo.Outro:
                    throw new Exception("Tipo de arquivo inexistente.");
            }
            //
            mensagem = vMsg;
            return vRetorno;
        }
    }
}
