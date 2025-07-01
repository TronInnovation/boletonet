using BoletoNet.EDI.Banco;
using BoletoNet.Excecoes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.UI;

[assembly: WebResource("BoletoNet.Imagens.748.jpg", "image/jpg")]
namespace BoletoNet
{
    /// <Author>
    /// Samuel Schmidt - Sicredi Nordeste RS / Felipe Eduardo - RS
    /// </Author>
    internal class Banco_Sicredi : AbstractBanco, IBanco
    {
        private static Dictionary<int, string> carteirasDisponiveis = new Dictionary<int, string>() {
            { 1, "Com Registro" },
            { 3, "Sem Registro" }
        };

        #region Variáveis

        string _byteGeracao = "2";

        #endregion


        #region Properties

        private int QtdRegistrosGeral { get; set; }
        private int QtdRegistrosLote { get; set; }
        private int QtdLotesGeral { get; set; }
        private int QtdTitulosLote { get; set; }
        private decimal ValorTotalTitulosLote { get; set; }

        #endregion


        private HeaderRetorno header;

        /// <author>
        /// Classe responsavel em criar os campos do Banco Sicredi.
        /// </author>
        internal Banco_Sicredi()
        {
            try
            {
                this.Codigo = 748;
                this.Digito = "0";
                this.Nome = "Sicredi";
                this.QtdRegistrosGeral = 0;
                this.QtdRegistrosLote = 0;
                this.QtdLotesGeral = 0;
                this.QtdTitulosLote = 0;
                this.ValorTotalTitulosLote = 0;
            }
            catch (Exception ex)
            {
                throw new Exception("Erro ao instanciar objeto.", ex);
            }
        }

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
            else boleto.LocalPagamento = "PAGÁVEL PREFERENCIALMENTE NAS COOPERATIVAS DE CRÉDITO DO SICREDI";

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
           // Removido em:18/03/2025 Flavio Ribeiro, estava alterando o codigo do cedente gerando problema na formatacao do codigo de barras e linha digitavel 
           // if (boleto.Cedente.ContaBancaria != null &&
           //     (!codigoCedente.StartsWith(boleto.Cedente.ContaBancaria.Agencia) ||
           //      !(codigoCedente.EndsWith(conta) || codigoCedente.EndsWith(conta.Substring(0, conta.Length - 1)))))
           //     //throw new BoletoNetException("Código do cedente deve estar no " + infoFormatoCodigoCedente);
           //     boleto.Cedente.Codigo = string.Format("{0}{1}{2}", boleto.Cedente.ContaBancaria.Agencia, boleto.Cedente.ContaBancaria.OperacaConta, boleto.Cedente.Codigo);

            if (string.IsNullOrEmpty(boleto.Carteira))
                throw new BoletoNetException("Tipo de carteira é obrigatório. " + ObterInformacoesCarteirasDisponiveis());

            if (!CarteiraValida(boleto.Carteira))
                throw new BoletoNetException("Carteira informada é inválida. Informe " + ObterInformacoesCarteirasDisponiveis());

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
                    
                    // Estava Alterando o nosso numero gerando inconsistência, o nosso número deve ter no máximo 9 digitos
                    if(boleto.Banco.Codigo == 748)
                    {
                        boleto.DigitoNossoNumero = DigNossoNumeroSicredi(boleto, true);
                        boleto.NossoNumero += boleto.DigitoNossoNumero;
                    }
                    else
                    {
                        boleto.DigitoNossoNumero = DigNossoNumeroSicredi(boleto);
                    }
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

        private string ObterInformacoesCarteirasDisponiveis()
        {
            return string.Join(", ", carteirasDisponiveis.Select(o => string.Format("“{0}” – {1}", o.Key, o.Value)));
        }

        private bool CarteiraValida(string carteira)
        {
            int tipoCarteira;
            if (int.TryParse(carteira, out tipoCarteira))
            {
                return carteirasDisponiveis.ContainsKey(tipoCarteira);
            }
            return false;
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
                Utils.FormatCode(boleto.Cedente.ContaBancaria.Agencia, 4) +
                Utils.FormatCode(boleto.Cedente.ContaBancaria.OperacaConta, 2) +
                Utils.FormatCode(boleto.Cedente.Codigo.Substring(0,5), 5) + "10";

            string dv_cmpLivre = digSicredi(cmp_livre).ToString();

            var codigoTemp = GerarCodigoDeBarras(boleto, valorBoleto, cmp_livre, dv_cmpLivre);

            boleto.CodigoBarra.CampoLivre = cmp_livre;
            boleto.CodigoBarra.FatorVencimento = FatorVencimento(boleto);
            boleto.CodigoBarra.Moeda = 9;
            boleto.CodigoBarra.ValorDocumento = valorBoleto;

            int _dacBoleto = digSicredi(codigoTemp);

            if (_dacBoleto == 0 || _dacBoleto > 9)
                _dacBoleto = 1;

            // Identificado no layout do SICREDI que não tem o DAC do boleto
            boleto.CodigoBarra.Codigo = GerarCodigoDeBarras(boleto, valorBoleto, cmp_livre, dv_cmpLivre, _dacBoleto);
            //boleto.CodigoBarra.Codigo = GerarCodigoDeBarras(boleto, valorBoleto, cmp_livre, dv_cmpLivre);
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

        //public bool RegistroByCarteira(Boleto boleto)
        //{
        //    bool valida = false;
        //    if (boleto.Carteira == "112"
        //        || boleto.Carteira == "115"
        //        || boleto.Carteira == "104"
        //        || boleto.Carteira == "147"
        //        || boleto.Carteira == "188"
        //        || boleto.Carteira == "108"
        //        || boleto.Carteira == "109"
        //        || boleto.Carteira == "150"
        //        || boleto.Carteira == "121")
        //        valida = true;
        //    return valida;
        //}

        #region Métodos de Geração do Arquivo de Remessa
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
                    case TipoArquivo.CNAB400:
                        _detalhe = GerarDetalheRemessaCNAB400(boleto, numeroRegistro, tipoArquivo);
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

                detalhe += "0";
                detalhe += "00000000";
                valorBoleto = "0000000000000";

                // Removendo o valor do desconto nao se aplica para esse banco - 01/07/2025
                //                detalhe += "1";
                //                detalhe += boleto.DataDesconto.ToString("ddMMyyyy");
                //                valorBoleto = boleto.ValorDesconto.ToString("f").Replace(",", "").Replace(".", "");
                //                valorBoleto = Utils.FormatCode(valorBoleto, 13);
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
                        _header = GerarHeaderRemessaCNAB240(cedente,numeroArquivoRemessa);
                        break;
                    case TipoArquivo.CNAB400:
                        _header = GerarHeaderRemessaCNAB400(0, cedente, numeroArquivoRemessa);
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

        #region Formatações Remessa

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
                                           _byteGeracao,
                                           Utils.RightStr(Utils.FormatCode(boleto.NossoNumeroSemFormatacao, 5), 5));

                string dv_NossoNumero = DigNossoNumeroSicredi(seq);

                return string.Concat(anoReferencia, _byteGeracao, Utils.FormatCode(boleto.NossoNumero, 5), dv_NossoNumero);
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

        #endregion

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
                
                valorJuros = (decimal)(boleto.JurosMora * 30);

                detalhe.Append(Utils.FitStringLength(valorJuros.ToString("0.00").Replace(",", "").Replace(".", ""), 15, 15, '0', 0, true, true, true));//Posição 127 a 141
                detalhe.Append(Utils.FitStringLength(boleto.DataDesconto >= DateTime.Now ? "1" : "3", 1, 1, '0', 0, true, true, true));//Posição 142  
                detalhe.Append(Utils.FitStringLength(boleto.DataDesconto != null && boleto.DataDesconto >= Convert.ToDateTime("01/01/1990") ? boleto.DataDesconto.ToString("ddMMyyyy") : "0", 8, 8, '0', 0, true, true, true));//Posição 143 a 150   
                detalhe.Append(Utils.FitStringLength(boleto.ValorDesconto.ToString("0.00").Replace(",", ""), 15, 15, '0', 0, true, true, true));//Posição 151 a 165 
                detalhe.Append(Utils.FitStringLength(boleto.IOF.ToString("0.00").Replace(",", ""), 15, 15, '0', 0, true, true, true));//Posição 166 a 180 
                detalhe.Append(Utils.FitStringLength("0", 15, 15, '0', 0, true, true, true));//Posição 181 a 195 
                detalhe.Append(Utils.FitStringLength(boleto.NossoNumero, 25, 25, '0', 0, true, true, true));//Posição 196 a 220
                detalhe.Append(boleto.ProtestaTitulos == true ? "1" : "3");//Protesto
                detalhe.Append(boleto.ProtestaTitulos == true ? boleto.NumeroDiasProtesto.ToString() : "00");//dias protesto
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
                detalhe.Append(Utils.FitStringLength(boleto.Sacado.CPFCNPJ.Length < 14? "1" : "2", 1, 1, '0', 0, true, true, true));//Posição  018
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

        // <summary>
        // 
        // </summary>
        // <param name = "boleto" ></ param >
        // < param name="numeroRegistro"></param>
        // <param name = "tipoArquivo" ></ param >
        // < returns ></ returns >
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
                    case TipoArquivo.CNAB400:
                        _trailer = GerarTrailerRemessa400(numeroRegistro, cedente);
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

        #endregion

        #region Métodos de Leitura do Arquivo de Retorno
        /*
         * Substituído Método de Leitura do Retorno pelo Interpretador de EDI;
        public override DetalheRetorno LerDetalheRetornoCNAB400(string registro)
        {
            try
            {
                DetalheRetorno detalhe = new DetalheRetorno(registro);

                int idRegistro = Utils.ToInt32(registro.Substring(0, 1));
                detalhe.IdentificacaoDoRegistro = idRegistro;

                detalhe.NossoNumero = registro.Substring(47, 15);

                int codigoOcorrencia = Utils.ToInt32(registro.Substring(108, 2));
                detalhe.CodigoOcorrencia = codigoOcorrencia;

                //Data Ocorrência no Banco
                int dataOcorrencia = Utils.ToInt32(registro.Substring(110, 6));
                detalhe.DataOcorrencia = Utils.ToDateTime(dataOcorrencia.ToString("##-##-##"));

                detalhe.SeuNumero = registro.Substring(116, 10);

                int dataVencimento = Utils.ToInt32(registro.Substring(146, 6));
                detalhe.DataVencimento = Utils.ToDateTime(dataVencimento.ToString("##-##-##"));

                decimal valorTitulo = Convert.ToUInt64(registro.Substring(152, 13));
                detalhe.ValorTitulo = valorTitulo / 100;

                detalhe.EspecieTitulo = registro.Substring(174, 1);

                decimal despeasaDeCobranca = Convert.ToUInt64(registro.Substring(175, 13));
                detalhe.DespeasaDeCobranca = despeasaDeCobranca / 100;

                decimal outrasDespesas = Convert.ToUInt64(registro.Substring(188, 13));
                detalhe.OutrasDespesas = outrasDespesas / 100;

                decimal abatimentoConcedido = Convert.ToUInt64(registro.Substring(227, 13));
                detalhe.Abatimentos = abatimentoConcedido / 100;

                decimal descontoConcedido = Convert.ToUInt64(registro.Substring(240, 13));
                detalhe.Descontos = descontoConcedido / 100;

                decimal valorPago = Convert.ToUInt64(registro.Substring(253, 13));
                detalhe.ValorPago = valorPago / 100;

                decimal jurosMora = Convert.ToUInt64(registro.Substring(266, 13));
                detalhe.JurosMora = jurosMora / 100;

                int dataCredito = Utils.ToInt32(registro.Substring(328, 8));
                detalhe.DataCredito = Utils.ToDateTime(dataCredito.ToString("####-##-##"));

                detalhe.MotivosRejeicao = registro.Substring(318, 10);

                detalhe.NomeSacado = registro.Substring(19, 5);
                return detalhe;
            }
            catch (Exception ex)
            {
                throw new Exception("Erro ao ler detalhe do arquivo de RETORNO / CNAB 400.", ex);
            }
        }
        */
        #endregion Métodos de Leitura do Arquivo de Retorno

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


        /// <summary>
        /// Efetua as Validações dentro da classe Boleto, para garantir a geração da remessa
        /// </summary>
        public override bool ValidarRemessa(TipoArquivo tipoArquivo, string numeroConvenio, IBanco banco, Cedente cedente, Boletos boletos, int numeroArquivoRemessa, out string mensagem)
        {
            bool vRetorno = true;
            string vMsg = string.Empty;
            //            
            switch (tipoArquivo)
            {
                case TipoArquivo.CNAB240:
                    //vRetorno = ValidarRemessaCNAB240(numeroConvenio, banco, cedente, boletos, numeroArquivoRemessa, out vMsg);
                    break;
                case TipoArquivo.CNAB400:
                    vRetorno = ValidarRemessaCNAB400(numeroConvenio, banco, cedente, boletos, numeroArquivoRemessa, out vMsg);
                    break;
                case TipoArquivo.Outro:
                    throw new Exception("Tipo de arquivo inexistente.");
            }
            //
            mensagem = vMsg;
            return vRetorno;
        }


        #region CNAB 400 - sidneiklein
        public bool ValidarRemessaCNAB400(string numeroConvenio, IBanco banco, Cedente cedente, Boletos boletos, int numeroArquivoRemessa, out string mensagem)
        {
            bool vRetorno = true;
            string vMsg = string.Empty;
            //
            #region Pré Validações
            if (banco == null)
            {
                vMsg += String.Concat("Remessa: O Banco é Obrigatório!", Environment.NewLine);
                vRetorno = false;
            }
            if (cedente == null)
            {
                vMsg += String.Concat("Remessa: O Cedente/Beneficiário é Obrigatório!", Environment.NewLine);
                vRetorno = false;
            }
            if (boletos == null || boletos.Count.Equals(0))
            {
                vMsg += String.Concat("Remessa: Deverá existir ao menos 1 boleto para geração da remessa!", Environment.NewLine);
                vRetorno = false;
            }
            #endregion
            //
            foreach (Boleto boleto in boletos)
            {
                #region Validação de cada boleto
                if (boleto.Remessa == null)
                {
                    vMsg += String.Concat("Boleto: ", boleto.NumeroDocumento, "; Remessa: Informe as diretrizes de remessa!", Environment.NewLine);
                    vRetorno = false;
                }
                else
                {
                    #region Validações da Remessa que deverão estar preenchidas quando SICREDI
                    //Comentado porque ainda está fixado em 01
                    //if (String.IsNullOrEmpty(boleto.Remessa.CodigoOcorrencia))
                    //{
                    //    vMsg += String.Concat("Boleto: ", boleto.NumeroDocumento, "; Remessa: Informe o Código de Ocorrência!", Environment.NewLine);
                    //    vRetorno = false;
                    //}
                    if (String.IsNullOrEmpty(boleto.NumeroDocumento))
                    {
                        vMsg += String.Concat("Boleto: ", boleto.NumeroDocumento, "; Remessa: Informe um Número de Documento!", Environment.NewLine);
                        vRetorno = false;
                    }
                    else if (String.IsNullOrEmpty(boleto.Remessa.TipoDocumento))
                    {
                        // Para o Sicredi, defini o Tipo de Documento sendo: 
                        //       A = 'A' - SICREDI com Registro
                        //      C1 = 'C' - SICREDI sem Registro Impressão Completa pelo Sicredi
                        //      C2 = 'C' - SICREDI sem Registro Pedido de bloquetos pré-impressos
                        // ** Isso porque são tratados 3 leiautes de escrita diferentes para o Detail da remessa;

                        vMsg += String.Concat("Boleto: ", boleto.NumeroDocumento, "; Remessa: Informe o Tipo Documento!", Environment.NewLine);
                        vRetorno = false;
                    }
                    else if (!boleto.Remessa.TipoDocumento.Equals("A") && !boleto.Remessa.TipoDocumento.Equals("C1") && !boleto.Remessa.TipoDocumento.Equals("C2"))
                    {
                        vMsg += String.Concat("Boleto: ", boleto.NumeroDocumento, "; Remessa: Tipo de Documento Inválido! Deverão ser: A = SICREDI com Registro; C1 = SICREDI sem Registro Impressão Completa pelo Sicredi;  C2 = SICREDI sem Registro Pedido de bloquetos pré-impressos", Environment.NewLine);
                        vRetorno = false;
                    }
                    //else if (boleto.Remessa.TipoDocumento.Equals("06") && !String.IsNullOrEmpty(boleto.NossoNumero))
                    //{
                    //    //Para o "Remessa.TipoDocumento = "06", não poderá ter NossoNumero Gerado!
                    //    vMsg += String.Concat("Boleto: ", boleto.NumeroDocumento, "; Não pode existir NossoNumero para o Tipo Documento '06 - cobrança escritural'!", Environment.NewLine);
                    //    vRetorno = false;
                    //}
                    else if (!boleto.EspecieDocumento.Codigo.Equals("A") && //A - Duplicata Mercantil por Indicação
                             !boleto.EspecieDocumento.Codigo.Equals("B") && //B - Duplicata Rural;
                             !boleto.EspecieDocumento.Codigo.Equals("C") && //C - Nota Promissória;
                             !boleto.EspecieDocumento.Codigo.Equals("D") && //D - Nota Promissória Rural;
                             !boleto.EspecieDocumento.Codigo.Equals("E") && //E - Nota de Seguros;
                             !boleto.EspecieDocumento.Codigo.Equals("F") && //G – Recibo;

                             !boleto.EspecieDocumento.Codigo.Equals("H") && //H - Letra de Câmbio;
                             !boleto.EspecieDocumento.Codigo.Equals("I") && //I - Nota de Débito;
                             !boleto.EspecieDocumento.Codigo.Equals("J") && //J - Duplicata de Serviço por Indicação;
                             !boleto.EspecieDocumento.Codigo.Equals("O") && //O – Boleto Proposta
                             !boleto.EspecieDocumento.Codigo.Equals("K") //K – Outros.
                            )
                    {
                        vMsg += String.Concat("Boleto: ", boleto.NumeroDocumento, "; Remessa: Informe o Código da EspécieDocumento! São Aceitas:{A,B,C,D,E,F,H,I,J,O,K}", Environment.NewLine);
                        vRetorno = false;
                    }
                    else if (!boleto.Sacado.CPFCNPJ.Length.Equals(11) && !boleto.Sacado.CPFCNPJ.Length.Equals(14))
                    {
                        vMsg += String.Concat("Boleto: ", boleto.NumeroDocumento, "; Remessa: Cpf/Cnpj diferente de 11/14 caracteres!", Environment.NewLine);
                        vRetorno = false;
                    }
                    else if (!boleto.NossoNumero.Length.Equals(8))
                    {
                        //sidnei.klein: Segundo definição recebida pelo Sicredi-RS, o Nosso Número sempre terá somente 8 caracteres sem o DV que está no boleto.DigitoNossoNumero
                        vMsg += String.Concat("Boleto: ", boleto.NumeroDocumento, "; Remessa: O Nosso Número diferente de 8 caracteres!", Environment.NewLine);
                        vRetorno = false;
                    }
                    else if (!boleto.TipoImpressao.Equals("A") && !boleto.TipoImpressao.Equals("B"))
                    {
                        vMsg += String.Concat("Boleto: ", boleto.NumeroDocumento, "; Tipo de Impressão deve conter A - Normal ou B - Carnê", Environment.NewLine);
                        vRetorno = false;
                    }
                    #endregion
                }
                #endregion
            }
            //
            mensagem = vMsg;
            return vRetorno;
        }
        public string GerarHeaderRemessaCNAB400(int numeroConvenio, Cedente cedente, int numeroArquivoRemessa)
        {
            try
            {
                TRegistroEDI reg = new TRegistroEDI();
                reg.CamposEDI.Add(new TCampoRegistroEDI(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0001, 001, 0, "0", ' '));                             //001-001
                reg.CamposEDI.Add(new TCampoRegistroEDI(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0002, 001, 0, "1", ' '));                             //002-002
                reg.CamposEDI.Add(new TCampoRegistroEDI(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0003, 007, 0, "REMESSA", ' '));                       //003-009
                reg.CamposEDI.Add(new TCampoRegistroEDI(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0010, 002, 0, "01", ' '));                            //010-011
                reg.CamposEDI.Add(new TCampoRegistroEDI(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0012, 015, 0, "COBRANCA", ' '));                      //012-026
                reg.CamposEDI.Add(new TCampoRegistroEDI(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0027, 005, 0, cedente.ContaBancaria.Conta, ' '));     //027-031
                reg.CamposEDI.Add(new TCampoRegistroEDI(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0032, 014, 0, cedente.CPFCNPJ, ' '));                 //032-045
                reg.CamposEDI.Add(new TCampoRegistroEDI(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0046, 031, 0, "", ' '));                              //046-076
                reg.CamposEDI.Add(new TCampoRegistroEDI(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0077, 003, 0, "748", ' '));                           //077-079
                reg.CamposEDI.Add(new TCampoRegistroEDI(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0080, 015, 0, "SICREDI", ' '));                       //080-094
                reg.CamposEDI.Add(new TCampoRegistroEDI(TTiposDadoEDI.ediDataAAAAMMDD_________, 0095, 008, 0, DateTime.Now, ' '));                    //095-102
                reg.CamposEDI.Add(new TCampoRegistroEDI(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0103, 008, 0, "", ' '));                              //103-110
                reg.CamposEDI.Add(new TCampoRegistroEDI(TTiposDadoEDI.ediNumericoSemSeparador_, 0111, 007, 0, numeroArquivoRemessa.ToString(), '0')); //111-117
                reg.CamposEDI.Add(new TCampoRegistroEDI(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0118, 273, 0, "", ' '));                              //118-390
                reg.CamposEDI.Add(new TCampoRegistroEDI(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0391, 004, 0, "2.00", ' '));                          //391-394
                reg.CamposEDI.Add(new TCampoRegistroEDI(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0395, 006, 0, "000001", ' '));                        //395-400
                //
                reg.CodificarLinha();
                //
                string vLinha = reg.LinhaRegistro;
                string _header = Utils.SubstituiCaracteresEspeciais(vLinha);
                //
                return _header;
            }
            catch (Exception ex)
            {
                throw new Exception("Erro ao gerar HEADER do arquivo de remessa do CNAB400.", ex);
            }
        }

        public string GerarDetalheRemessaCNAB400(Boleto boleto, int numeroRegistro, TipoArquivo tipoArquivo)
        {
            base.GerarDetalheRemessa(boleto, numeroRegistro, tipoArquivo);
            return GerarDetalheRemessaCNAB400_A(boleto, numeroRegistro, tipoArquivo);
        }
        public string GerarDetalheRemessaCNAB400_A(Boleto boleto, int numeroRegistro, TipoArquivo tipoArquivo)
        {
            try
            {
                TRegistroEDI reg = new TRegistroEDI();
                reg.CamposEDI.Add(new TCampoRegistroEDI(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0001, 001, 0, "1", ' '));                                       //001-001
                reg.CamposEDI.Add(new TCampoRegistroEDI(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0002, 001, 0, "A", ' '));                                       //002-002  'A' - SICREDI com Registro
                reg.CamposEDI.Add(new TCampoRegistroEDI(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0003, 001, 0, "A", ' '));                                       //003-003  'A' - Simples
                reg.CamposEDI.Add(new TCampoRegistroEDI(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0004, 001, 0, boleto.TipoImpressao, ' '));                                       //004-004  'A' – Normal
                reg.CamposEDI.Add(new TCampoRegistroEDI(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0005, 012, 0, string.Empty, ' '));                              //005-016
                reg.CamposEDI.Add(new TCampoRegistroEDI(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0017, 001, 0, "A", ' '));                                       //017-017  Tipo de moeda: 'A' - REAL
                reg.CamposEDI.Add(new TCampoRegistroEDI(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0018, 001, 0, "A", ' '));                                       //018-018  Tipo de desconto: 'A' - VALOR
                #region Código de Juros
                string CodJuros = "A";
                decimal ValorOuPercJuros = 0;
                if (boleto.JurosMora > 0)
                {
                    CodJuros = "A";
                    ValorOuPercJuros = boleto.JurosMora;
                }
                else if (boleto.PercJurosMora > 0)
                {
                    CodJuros = "B";
                    ValorOuPercJuros = boleto.PercJurosMora;
                }
                #endregion
                reg.CamposEDI.Add(new TCampoRegistroEDI(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0019, 001, 0, CodJuros, ' '));                                  //019-019  Tipo de juros: 'A' - VALOR / 'B' PERCENTUAL
                reg.CamposEDI.Add(new TCampoRegistroEDI(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0020, 028, 0, string.Empty, ' '));                              //020-047
                #region Nosso Número + DV
                string NossoNumero = boleto.NossoNumero.Replace("/", "").Replace("-", ""); // AA/BXXXXX-D
                string vAuxNossoNumeroComDV = NossoNumero;
                if (string.IsNullOrEmpty(boleto.DigitoNossoNumero) || NossoNumero.Length < 9)
                {
                    boleto.DigitoNossoNumero = DigNossoNumeroSicredi(boleto, true);
                    vAuxNossoNumeroComDV = string.Format("{0}{1}{2}",
                                                          DateTime.Now.ToString("yy"),
                                                          2,
                                                          int.Parse(string.Concat(NossoNumero, boleto.DigitoNossoNumero)).ToString().PadLeft(6, '0'));
                }
                reg.CamposEDI.Add(new TCampoRegistroEDI(TTiposDadoEDI.ediNumericoSemSeparador_, 0048, 009, 0, vAuxNossoNumeroComDV, '0'));                      //048-056
                #endregion
                reg.CamposEDI.Add(new TCampoRegistroEDI(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0057, 006, 0, string.Empty, ' '));                              //057-062
                reg.CamposEDI.Add(new TCampoRegistroEDI(TTiposDadoEDI.ediDataAAAAMMDD_________, 0063, 008, 0, boleto.DataProcessamento, ' '));                  //063-070
                reg.CamposEDI.Add(new TCampoRegistroEDI(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0071, 001, 0, string.Empty, ' '));                              //071-071
                reg.CamposEDI.Add(new TCampoRegistroEDI(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0072, 001, 0, "N", ' '));                                       //072-072 'N' - Não Postar e remeter para o beneficiário
                reg.CamposEDI.Add(new TCampoRegistroEDI(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0073, 001, 0, string.Empty, ' '));                              //073-073
                reg.CamposEDI.Add(new TCampoRegistroEDI(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0074, 001, 0, "B", ' '));                                       //074-074 'B' – Impressão é feita pelo Beneficiário
                if (boleto.TipoImpressao.Equals("A"))
                {
                    reg.CamposEDI.Add(new TCampoRegistroEDI(TTiposDadoEDI.ediNumericoSemSeparador_, 0075, 002, 0, 0, '0'));                                      //075-076
                    reg.CamposEDI.Add(new TCampoRegistroEDI(TTiposDadoEDI.ediNumericoSemSeparador_, 0077, 002, 0, 0, '0'));                                      //077-078
                }
                else if (boleto.TipoImpressao.Equals("B"))
                {
                    reg.CamposEDI.Add(new TCampoRegistroEDI(TTiposDadoEDI.ediNumericoSemSeparador_, 0075, 002, 0, boleto.NumeroParcela, '0'));                   //075-076
                    reg.CamposEDI.Add(new TCampoRegistroEDI(TTiposDadoEDI.ediNumericoSemSeparador_, 0077, 002, 0, boleto.TotalParcela, '0'));                    //077-078
                }
                reg.CamposEDI.Add(new TCampoRegistroEDI(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0079, 004, 0, string.Empty, ' '));                              //079-082
                reg.CamposEDI.Add(new TCampoRegistroEDI(TTiposDadoEDI.ediNumericoSemSeparador_, 0083, 010, 2, boleto.ValorDescontoAntecipacao, '0'));           //083-092
                reg.CamposEDI.Add(new TCampoRegistroEDI(TTiposDadoEDI.ediNumericoSemSeparador_, 0093, 004, 2, boleto.PercMulta, '0'));                          //093-096
                reg.CamposEDI.Add(new TCampoRegistroEDI(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0097, 012, 0, string.Empty, ' '));                              //097-108
                reg.CamposEDI.Add(new TCampoRegistroEDI(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0109, 002, 0, ObterCodigoDaOcorrencia(boleto), ' '));           //109-110 01 - Cadastro de título;
                reg.CamposEDI.Add(new TCampoRegistroEDI(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0111, 010, 0, boleto.NumeroDocumento, ' '));                    //111-120
                reg.CamposEDI.Add(new TCampoRegistroEDI(TTiposDadoEDI.ediDataDDMMAA___________, 0121, 006, 0, boleto.DataVencimento, ' '));                     //121-126
                reg.CamposEDI.Add(new TCampoRegistroEDI(TTiposDadoEDI.ediNumericoSemSeparador_, 0127, 013, 2, boleto.ValorBoleto, '0'));                        //127-139
                reg.CamposEDI.Add(new TCampoRegistroEDI(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0140, 009, 0, string.Empty, ' '));                              //140-148
                #region Espécie de documento
                //Adota Duplicata Mercantil p/ Indicação como padrão.
                var especieDoc = boleto.EspecieDocumento ?? new EspecieDocumento_Sicredi("A");
                #endregion
                reg.CamposEDI.Add(new TCampoRegistroEDI(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0149, 001, 0, especieDoc.Codigo, ' '));                         //149-149
                reg.CamposEDI.Add(new TCampoRegistroEDI(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0150, 001, 0, boleto.Aceite, ' '));                             //150-150
                reg.CamposEDI.Add(new TCampoRegistroEDI(TTiposDadoEDI.ediDataDDMMAA___________, 0151, 006, 0, boleto.DataProcessamento, ' '));                  //151-156
                #region Instruções
                string vInstrucao1 = boleto.ProtestaTitulos == true ? "06" : "00"; //1ª instrução (2, N) Caso Queira colocar um cod de uma instrução. ver no Manual caso nao coloca 00
                string vInstrucao2 = boleto.ProtestaTitulos == true ? Utils.FormatCode(boleto.NumeroDiasProtesto.ToString(), "0", 2, true) : "00"; //2ª instrução (2, N) Caso Queira colocar um cod de uma instrução. ver no Manual caso nao coloca 00
                
                        
                foreach (IInstrucao instrucao in boleto.Instrucoes)
                {
                    switch ((EnumInstrucoes_Sicredi)instrucao.Codigo)
                    {
                        case EnumInstrucoes_Sicredi.AlteracaoOutrosDados_CancelamentoProtestoAutomatico:
                            vInstrucao1 = "00";
                            vInstrucao2 = "00";
                            break;
                        case EnumInstrucoes_Sicredi.PedidoProtesto:
                            vInstrucao1 = "06"; //Indicar o código “06” - (Protesto)
                            vInstrucao2 = Utils.FitStringLength(instrucao.QuantidadeDias.ToString(), 2, 2, '0', 0, true, true, true);
                            break;
                    }
                }
                reg.CamposEDI.Add(new TCampoRegistroEDI(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0157, 002, 0, vInstrucao1, '0'));                               //157-158
                reg.CamposEDI.Add(new TCampoRegistroEDI(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0159, 002, 0, vInstrucao2, '0'));                               //159-160
                #endregion               
                reg.CamposEDI.Add(new TCampoRegistroEDI(TTiposDadoEDI.ediNumericoSemSeparador_, 0161, 013, 2, ValorOuPercJuros, '0'));                          //161-173 Valor/% de juros por dia de atraso
                #region DataDesconto
                string vDataDesconto = "000000";
                if (!boleto.DataDesconto.Equals(DateTime.MinValue))
                    vDataDesconto = boleto.DataDesconto.ToString("ddMMyy");
                #endregion
                reg.CamposEDI.Add(new TCampoRegistroEDI(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0174, 006, 0, vDataDesconto, '0'));                             //174-179
                reg.CamposEDI.Add(new TCampoRegistroEDI(TTiposDadoEDI.ediNumericoSemSeparador_, 0180, 013, 2, boleto.ValorDesconto, '0'));                      //180-192
                reg.CamposEDI.Add(new TCampoRegistroEDI(TTiposDadoEDI.ediNumericoSemSeparador_, 0193, 013, 0, 0, '0'));                                         //193-205
                reg.CamposEDI.Add(new TCampoRegistroEDI(TTiposDadoEDI.ediNumericoSemSeparador_, 0206, 013, 2, boleto.Abatimento, '0'));                         //206-218
                #region Regra Tipo de Inscrição Sacado
                string vCpfCnpjSac = "0";
                if (boleto.Sacado.CPFCNPJ.Length.Equals(11)) vCpfCnpjSac = "1"; //Cpf é sempre 11;
                else if (boleto.Sacado.CPFCNPJ.Length.Equals(14)) vCpfCnpjSac = "2"; //Cnpj é sempre 14;
                #endregion
                reg.CamposEDI.Add(new TCampoRegistroEDI(TTiposDadoEDI.ediNumericoSemSeparador_, 0219, 001, 0, vCpfCnpjSac, '0'));                               //219-219
                reg.CamposEDI.Add(new TCampoRegistroEDI(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0220, 001, 0, "0", '0'));                                       //220-220
                reg.CamposEDI.Add(new TCampoRegistroEDI(TTiposDadoEDI.ediNumericoSemSeparador_, 0221, 014, 0, boleto.Sacado.CPFCNPJ, '0'));                     //221-234
                reg.CamposEDI.Add(new TCampoRegistroEDI(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0235, 040, 0, boleto.Sacado.Nome.ToUpper(), ' '));              //235-274
                reg.CamposEDI.Add(new TCampoRegistroEDI(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0275, 040, 0, boleto.Sacado.Endereco.EndComNumeroEComplemento.ToUpper(), ' '));      //275-314
                reg.CamposEDI.Add(new TCampoRegistroEDI(TTiposDadoEDI.ediNumericoSemSeparador_, 0315, 005, 0, 0, '0'));                                         //315-319
                reg.CamposEDI.Add(new TCampoRegistroEDI(TTiposDadoEDI.ediNumericoSemSeparador_, 0320, 006, 0, 0, '0'));                                         //320-325
                reg.CamposEDI.Add(new TCampoRegistroEDI(TTiposDadoEDI.ediNumericoSemSeparador_, 0326, 001, 0, string.Empty, ' '));                              //326-326
                reg.CamposEDI.Add(new TCampoRegistroEDI(TTiposDadoEDI.ediNumericoSemSeparador_, 0327, 008, 0, boleto.Sacado.Endereco.CEP, '0'));                //327-334
                reg.CamposEDI.Add(new TCampoRegistroEDI(TTiposDadoEDI.ediNumericoSemSeparador_, 0335, 005, 1, 0, '0'));                                         //335-339
                reg.CamposEDI.Add(new TCampoRegistroEDI(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0340, 014, 0, string.Empty, ' '));                              //340-353
                reg.CamposEDI.Add(new TCampoRegistroEDI(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0354, 041, 0, string.Empty, ' '));                              //354-394
                reg.CamposEDI.Add(new TCampoRegistroEDI(TTiposDadoEDI.ediNumericoSemSeparador_, 0395, 006, 0, numeroRegistro, '0'));                            //395-400
                //
                reg.CodificarLinha();
                //
                string _detalhe = Utils.SubstituiCaracteresEspeciais(reg.LinhaRegistro);
                //
                return _detalhe;
            }
            catch (Exception ex)
            {
                throw new Exception("Erro ao gerar DETALHE do arquivo CNAB400.", ex);
            }
        }

        public string GerarTrailerRemessa400(int numeroRegistro, Cedente cedente)
        {
            try
            {
                TRegistroEDI reg = new TRegistroEDI();
                reg.CamposEDI.Add(new TCampoRegistroEDI(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0001, 001, 0, "9", ' '));                         //001-001
                reg.CamposEDI.Add(new TCampoRegistroEDI(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0002, 001, 0, "1", ' '));                         //002-002
                reg.CamposEDI.Add(new TCampoRegistroEDI(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0003, 003, 0, "748", ' '));                       //003-006
                reg.CamposEDI.Add(new TCampoRegistroEDI(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0006, 005, 0, cedente.ContaBancaria.Conta, ' ')); //006-010
                reg.CamposEDI.Add(new TCampoRegistroEDI(TTiposDadoEDI.ediAlphaAliEsquerda_____, 0011, 384, 0, string.Empty, ' '));                //011-394
                reg.CamposEDI.Add(new TCampoRegistroEDI(TTiposDadoEDI.ediNumericoSemSeparador_, 0395, 006, 0, numeroRegistro, '0'));              //395-400
                //
                reg.CodificarLinha();
                //
                string vLinha = reg.LinhaRegistro;
                string _trailer = Utils.SubstituiCaracteresEspeciais(vLinha);
                //
                return _trailer;
            }
            catch (Exception ex)
            {
                throw new Exception("Erro durante a geração do registro TRAILER do arquivo de REMESSA.", ex);
            }
        }

        private string LerMotivoRejeicao(string codigorejeicao)
        {
            var rejeicao = String.Empty;

            if (codigorejeicao.Length >= 2)
            {
                #region LISTA DE MOTIVOS
                List<String> ocorrencias = new List<string>();

                ocorrencias.Add("01-Código do banco inválido");
                ocorrencias.Add("02-Código do registro detalhe inválido");
                ocorrencias.Add("03-Código da ocorrência inválido");
                ocorrencias.Add("04-Código de ocorrência não permitida para a carteira");
                ocorrencias.Add("05-Código de ocorrência não numérico");
                ocorrencias.Add("07-Cooperativa/agência/conta/dígito inválidos");
                ocorrencias.Add("08-Nosso número inválido");
                ocorrencias.Add("09-Nosso número duplicado");
                ocorrencias.Add("10-Carteira inválida");
                ocorrencias.Add("14-Título protestado");
                ocorrencias.Add("15-Cooperativa/carteira/agência/conta/nosso número inválidos");
                ocorrencias.Add("16-Data de vencimento inválida");
                ocorrencias.Add("17-Data de vencimento anterior à data de emissão");
                ocorrencias.Add("18-Vencimento fora do prazo de operação");
                ocorrencias.Add("20-Valor do título inválido");
                ocorrencias.Add("21-Espécie do título inválida");
                ocorrencias.Add("22-Espécie não permitida para a carteira");
                ocorrencias.Add("24-Data de emissão inválida");
                ocorrencias.Add("29-Valor do desconto maior/igual ao valor do título");
                ocorrencias.Add("31-Concessão de desconto - existe desconto anterior");
                ocorrencias.Add("33-Valor do abatimento inválido");
                ocorrencias.Add("34-Valor do abatimento maior/igual ao valor do título");
                ocorrencias.Add("36-Concessão de abatimento - existe abatimento anterior");
                ocorrencias.Add("38-Prazo para protesto inválido");
                ocorrencias.Add("39-Pedido para protesto não permitido para o título");
                ocorrencias.Add("40-Título com ordem de protesto emitida");
                ocorrencias.Add("41-Pedido cancelamento/sustação sem instrução de protesto");
                ocorrencias.Add("44-Cooperativa de crédito/agência beneficiária não prevista");
                ocorrencias.Add("45-Nome do pagador inválido");
                ocorrencias.Add("46-Tipo/número de inscrição do pagador inválidos");
                ocorrencias.Add("47-Endereço do pagador não informado");
                ocorrencias.Add("48-CEP irregular");
                ocorrencias.Add("49-Número de Inscrição do pagador/avalista inválido");
                ocorrencias.Add("50-Pagador/avalista não informado");
                ocorrencias.Add("60-Movimento para título não cadastrado");
                ocorrencias.Add("63-Entrada para título já cadastrado");
                ocorrencias.Add("A -Aceito");
                ocorrencias.Add("A1-Praça do pagador não cadastrada.");
                ocorrencias.Add("A2-Tipo de cobrança do título divergente com a praça do pagador.");
                ocorrencias.Add("A3-Cooperativa/agência depositária divergente: atualiza o cadastro de praças da Coop./agência beneficiária");
                ocorrencias.Add("A4-Beneficiário não cadastrado ou possui CGC/CIC inválido");
                ocorrencias.Add("A5-Pagador não cadastrado");
                ocorrencias.Add("A6-Data da instrução/ocorrência inválida");
                ocorrencias.Add("A7-Ocorrência não pode ser comandada");
                ocorrencias.Add("A8-Recebimento da liquidação fora da rede Sicredi - via compensação eletrônica");
                ocorrencias.Add("B4-Tipo de moeda inválido");
                ocorrencias.Add("B5-Tipo de desconto/juros inválido");
                ocorrencias.Add("B6-Mensagem padrão não cadastrada");
                ocorrencias.Add("B7-Seu número inválido");
                ocorrencias.Add("B8-Percentual de multa inválido");
                ocorrencias.Add("B9-Valor ou percentual de juros inválido");
                ocorrencias.Add("C1-Data limite para concessão de desconto inválida");
                ocorrencias.Add("C2-Aceite do título inválido");
                ocorrencias.Add("C3-Campo alterado na instrução “31 – alteração de outros dados” inválido");
                ocorrencias.Add("C4-Título ainda não foi confirmado pela centralizadora");
                ocorrencias.Add("C5-Título rejeitado pela centralizadora");
                ocorrencias.Add("C6-Título já liquidado");
                ocorrencias.Add("C7-Título já baixado");
                ocorrencias.Add("C8-Existe mesma instrução pendente de confirmação para este título");
                ocorrencias.Add("C9-Instrução prévia de concessão de abatimento não existe ou não confirmada");
                ocorrencias.Add("D -Desprezado");
                ocorrencias.Add("D1-Título dentro do prazo de vencimento (em dia);");
                ocorrencias.Add("D2-Espécie de documento não permite protesto de título");
                ocorrencias.Add("D3-Título possui instrução de baixa pendente de confirmação");
                ocorrencias.Add("D4-Quantidade de mensagens padrão excede o limite permitido");
                ocorrencias.Add("D5-Quantidade inválida no pedido de boletos pré-impressos da cobrança sem registro");
                ocorrencias.Add("D6-Tipo de impressão inválida para cobrança sem registro");
                ocorrencias.Add("D7-Cidade ou Estado do pagador não informado");
                ocorrencias.Add("D8-Seqüência para composição do nosso número do ano atual esgotada");
                ocorrencias.Add("D9-Registro mensagem para título não cadastrado");
                ocorrencias.Add("E2-Registro complementar ao cadastro do título da cobrança com e sem registro não cadastrado");
                ocorrencias.Add("E3-Tipo de postagem inválido, diferente de S, N e branco");
                ocorrencias.Add("E4-Pedido de boletos pré-impressos");
                ocorrencias.Add("E5-Confirmação/rejeição para pedidos de boletos não cadastrado");
                ocorrencias.Add("E6-Pagador/avalista não cadastrado");
                ocorrencias.Add("E7-Informação para atualização do valor do título para protesto inválido");
                ocorrencias.Add("E8-Tipo de impressão inválido, diferente de A, B e branco");
                ocorrencias.Add("E9-Código do pagador do título divergente com o código da cooperativa de crédito");
                ocorrencias.Add("F1-Liquidado no sistema do cliente");
                ocorrencias.Add("F2-Baixado no sistema do cliente");
                ocorrencias.Add("F3-Instrução inválida, este título está caucionado/descontado");
                ocorrencias.Add("F4-Instrução fixa com caracteres inválidos");
                ocorrencias.Add("F6-Nosso número / número da parcela fora de seqüência – total de parcelas inválido");
                ocorrencias.Add("F7-Falta de comprovante de prestação de serviço");
                ocorrencias.Add("F8-Nome do beneficiário incompleto / incorreto.");
                ocorrencias.Add("F9-CNPJ / CPF incompatível com o nome do pagador / Sacador Avalista");
                ocorrencias.Add("G1-CNPJ / CPF do pagador Incompatível com a espécie");
                ocorrencias.Add("G2-Título aceito: sem a assinatura do pagador");
                ocorrencias.Add("G3-Título aceito: rasurado ou rasgado");
                ocorrencias.Add("G4-Título aceito: falta título (cooperativa/ag. beneficiária deverá enviá-lo);");
                ocorrencias.Add("G5-Praça de pagamento incompatível com o endereço");
                ocorrencias.Add("G6-Título aceito: sem endosso ou beneficiário irregular");
                ocorrencias.Add("G7-Título aceito: valor por extenso diferente do valor numérico");
                ocorrencias.Add("G8-Saldo maior que o valor do título");
                ocorrencias.Add("G9-Tipo de endosso inválido");
                ocorrencias.Add("H1-Nome do pagador incompleto / Incorreto");
                ocorrencias.Add("H2-Sustação judicial");
                ocorrencias.Add("H3-Pagador não encontrado");
                ocorrencias.Add("H4-Alteração de carteira");
                ocorrencias.Add("H5-Recebimento de liquidação fora da rede Sicredi – VLB Inferior – Via Compensação");
                ocorrencias.Add("H6-Recebimento de liquidação fora da rede Sicredi – VLB Superior – Via Compensação");
                ocorrencias.Add("H7-Espécie de documento necessita beneficiário ou avalista PJ");
                ocorrencias.Add("H8-Recebimento de liquidação fora da rede Sicredi – Contingência Via Compe");
                ocorrencias.Add("H9-Dados do título não conferem com disquete");
                ocorrencias.Add("I1-Pagador e Sacador Avalista são a mesma pessoa");
                ocorrencias.Add("I2-Aguardar um dia útil após o vencimento para protestar");
                ocorrencias.Add("I3-Data do vencimento rasurada");
                ocorrencias.Add("I4-Vencimento – extenso não confere com número");
                ocorrencias.Add("I5-Falta data de vencimento no título");
                ocorrencias.Add("I6-DM/DMI sem comprovante autenticado ou declaração");
                ocorrencias.Add("I7-Comprovante ilegível para conferência e microfilmagem");
                ocorrencias.Add("I8-Nome solicitado não confere com emitente ou pagador");
                ocorrencias.Add("I9-Confirmar se são 2 emitentes. Se sim, indicar os dados dos 2");
                ocorrencias.Add("J1-Endereço do pagador igual ao do pagador ou do portador");
                ocorrencias.Add("J2-Endereço do apresentante incompleto ou não informado");
                ocorrencias.Add("J3-Rua/número inexistente no endereço");
                ocorrencias.Add("J4-Falta endosso do favorecido para o apresentante");
                ocorrencias.Add("J5-Data da emissão rasurada");
                ocorrencias.Add("J6-Falta assinatura do pagador no título");
                ocorrencias.Add("J7-Nome do apresentante não informado/incompleto/incorreto");
                ocorrencias.Add("J8-Erro de preenchimento do titulo");
                ocorrencias.Add("J9-Titulo com direito de regresso vencido");
                ocorrencias.Add("K1-Titulo apresentado em duplicidade");
                ocorrencias.Add("K2-Titulo já protestado");
                ocorrencias.Add("K3-Letra de cambio vencida – falta aceite do pagador");
                ocorrencias.Add("K4-Falta declaração de saldo assinada no título");
                ocorrencias.Add("K5-Contrato de cambio – Falta conta gráfica");
                ocorrencias.Add("K6-Ausência do documento físico");
                ocorrencias.Add("K7-Pagador falecido");
                ocorrencias.Add("K8-Pagador apresentou quitação do título");
                ocorrencias.Add("K9-Título de outra jurisdição territorial");
                ocorrencias.Add("L1-Título com emissão anterior a concordata do pagador");
                ocorrencias.Add("L2-Pagador consta na lista de falência");
                ocorrencias.Add("L3-Apresentante não aceita publicação de edital");
                ocorrencias.Add("L4-Dados do Pagador em Branco ou inválido");
                ocorrencias.Add("L5-Código do Pagador na agência beneficiária está duplicado");
                ocorrencias.Add("M1-Reconhecimento da dívida pelo pagador");
                ocorrencias.Add("M2-Não reconhecimento da dívida pelo pagador");
                ocorrencias.Add("M3-Inclusão de desconto 2 e desconto 3 inválida");
                ocorrencias.Add("X0-Pago com cheque");
                ocorrencias.Add("X1-Regularização centralizadora – Rede Sicredi");
                ocorrencias.Add("X2-Regularização centralizadora – Compensação");
                ocorrencias.Add("X3-Regularização centralizadora – Banco correspondente");
                ocorrencias.Add("X4-Regularização centralizadora - VLB Inferior - via compensação");
                ocorrencias.Add("X5-Regularização centralizadora - VLB Superior - via compensação");
                ocorrencias.Add("X6-Pago com cheque – bloqueado 24 horas");
                ocorrencias.Add("X7-Pago com cheque – bloqueado 48 horas");
                ocorrencias.Add("X8-Pago com cheque – bloqueado 72 horas");
                ocorrencias.Add("X9-Pago com cheque – bloqueado 96 horas");
                ocorrencias.Add("XA-Pago com cheque – bloqueado 120 horas");
                ocorrencias.Add("XB-Pago com cheque – bloqueado 144 horas");
                #endregion

                var ocorrencia = (from s in ocorrencias where s.Substring(0, 2) == codigorejeicao.Substring(0, 2) select s).FirstOrDefault();

                if (ocorrencia != null)
                    rejeicao = ocorrencia;
            }

            return rejeicao;
        }

        public override DetalheRetorno LerDetalheRetornoCNAB400(string registro)
        {
            try
            {
                TRegistroEDI_Sicredi_Retorno reg = new TRegistroEDI_Sicredi_Retorno();
                //
                reg.LinhaRegistro = registro;
                reg.DecodificarLinha();

                //Passa para o detalhe as propriedades de reg;
                DetalheRetorno detalhe = new DetalheRetorno(registro);
                //
                detalhe.IdentificacaoDoRegistro = Utils.ToInt32(reg.IdentificacaoRegDetalhe);
                //Filler1
                //TipoCobranca
                //CodigoPagadorAgenciaBeneficiario
                detalhe.NomeSacado = reg.CodigoPagadorJuntoAssociado;
                //BoletoDDA
                //Filler2
                #region NossoNumeroSicredi
                detalhe.NossoNumeroComDV = reg.NossoNumeroSicredi;
                detalhe.NossoNumero = reg.NossoNumeroSicredi.Substring(0, reg.NossoNumeroSicredi.Length - 1); //Nosso Número sem o DV!
                detalhe.DACNossoNumero = reg.NossoNumeroSicredi.Substring(reg.NossoNumeroSicredi.Length - 1); //DV do Nosso Numero
                #endregion
                //Filler3
                detalhe.CodigoOcorrencia = Utils.ToInt32(reg.Ocorrencia);
                int dataOcorrencia = Utils.ToInt32(reg.DataOcorrencia);
                detalhe.DataOcorrencia = Utils.ToDateTime(dataOcorrencia.ToString("##-##-##"));

                //Descrição da ocorrência
                detalhe.DescricaoOcorrencia = new CodigoMovimento(748, detalhe.CodigoOcorrencia).Descricao;

                detalhe.NumeroDocumento = reg.SeuNumero;
                //Filler4
                if (!String.IsNullOrEmpty(reg.DataVencimento))
                {
                    int dataVencimento = Utils.ToInt32(reg.DataVencimento);
                    detalhe.DataVencimento = Utils.ToDateTime(dataVencimento.ToString("##-##-##"));
                }
                decimal valorTitulo = Convert.ToInt64(reg.ValorTitulo);
                detalhe.ValorTitulo = valorTitulo / 100;
                //Filler5
                //Despesas de cobrança para os Códigos de Ocorrência (Valor Despesa)
                if (!String.IsNullOrEmpty(reg.DespesasCobranca))
                {
                    decimal valorDespesa = Convert.ToUInt64(reg.DespesasCobranca);
                    detalhe.ValorDespesa = valorDespesa / 100;
                }
                //Outras despesas Custas de Protesto (Valor Outras Despesas)
                if (!String.IsNullOrEmpty(reg.DespesasCustasProtesto))
                {
                    decimal valorOutrasDespesas = Convert.ToUInt64(reg.DespesasCustasProtesto);
                    detalhe.ValorOutrasDespesas = valorOutrasDespesas / 100;
                }
                //Filler6
                //Abatimento Concedido sobre o Título (Valor Abatimento Concedido)
                decimal valorAbatimento = Convert.ToUInt64(reg.AbatimentoConcedido);
                detalhe.ValorAbatimento = valorAbatimento / 100;
                //Desconto Concedido (Valor Desconto Concedido)
                decimal valorDesconto = Convert.ToUInt64(reg.DescontoConcedido);
                detalhe.Descontos = valorDesconto / 100;
                //Valor Pago
                decimal valorPago = Convert.ToUInt64(reg.ValorEfetivamentePago);
                detalhe.ValorPago = valorPago / 100;
                //Juros Mora
                decimal jurosMora = Convert.ToUInt64(reg.JurosMora);
                detalhe.JurosMora = jurosMora / 100;
                //Filler7
                //SomenteOcorrencia19
                //Filler8
                detalhe.MotivoCodigoOcorrencia = reg.MotivoOcorrencia;
                int dataCredito = Utils.ToInt32(reg.DataPrevistaLancamentoContaCorrente);
                detalhe.DataCredito = Utils.ToDateTime(dataCredito.ToString("####-##-##"));
                //Filler9
                detalhe.NumeroSequencial = Utils.ToInt32(reg.NumeroSequencialRegistro);
                //
                #region NAO RETORNADOS PELO SICREDI
                //detalhe.Especie = reg.TipoDocumento; //Verificar Espécie de Documentos...
                detalhe.OutrosCreditos = 0;
                detalhe.OrigemPagamento = String.Empty;
                detalhe.MotivoCodigoOcorrencia = reg.MotivoOcorrencia;
                //
                detalhe.IOF = 0;
                //Motivos das Rejeições para os Códigos de Ocorrência
                detalhe.MotivosRejeicao = LerMotivoRejeicao(detalhe.MotivoCodigoOcorrencia);
                //Número do Cartório
                detalhe.NumeroCartorio = 0;
                //Número do Protocolo
                detalhe.NumeroProtocolo = string.Empty;

                detalhe.CodigoInscricao = 0;
                detalhe.NumeroInscricao = string.Empty;
                detalhe.Agencia = 0;
                detalhe.Conta = header.Conta;
                detalhe.DACConta = header.DACConta;

                detalhe.NumeroControle = string.Empty;
                detalhe.IdentificacaoTitulo = string.Empty;
                //Banco Cobrador
                detalhe.CodigoBanco = 0;
                //Agência Cobradora
                detalhe.AgenciaCobradora = 0;
                #endregion
                //
                return detalhe;
            }
            catch (Exception ex)
            {
                throw new Exception("Erro ao ler detalhe do arquivo de RETORNO / CNAB 400.", ex);
            }
        }

        public override HeaderRetorno LerHeaderRetornoCNAB400(string registro)
        {
            try
            {
                header = new HeaderRetorno(400, registro);
                header.TipoRegistro = Utils.ToInt32(registro.Substring(000, 1));
                header.CodigoRetorno = Utils.ToInt32(registro.Substring(001, 1));
                header.LiteralRetorno = registro.Substring(002, 7);
                header.CodigoServico = Utils.ToInt32(registro.Substring(009, 2));
                header.LiteralServico = registro.Substring(011, 15);
                string _conta = registro.Substring(026, 5);
                header.Conta = Utils.ToInt32(_conta.Substring(0, _conta.Length - 1));
                header.DACConta = Utils.ToInt32(_conta.Substring(_conta.Length - 1));
                header.ComplementoRegistro2 = registro.Substring(031, 14);
                header.CodigoBanco = Utils.ToInt32(registro.Substring(076, 3));
                header.NomeBanco = registro.Substring(079, 15);
                header.DataGeracao = Utils.ToDateTime(Utils.ToInt32(registro.Substring(094, 8)).ToString("##-##-##"));
                header.NumeroSequencialArquivoRetorno = Utils.ToInt32(registro.Substring(110, 7));
                header.Versao = registro.Substring(390, 5);
                header.NumeroSequencial = Utils.ToInt32(registro.Substring(394, 6));



                return header;
            }
            catch (Exception ex)
            {
                throw new Exception("Erro ao ler header do arquivo de RETORNO / CNAB 400.", ex);
            }
        }

        #endregion

        public override long ObterNossoNumeroSemConvenioOuDigitoVerificador(long convenio, string nossoNumero)
        {
            long num;
            if (nossoNumero.Length >= 8 && long.TryParse(nossoNumero.Substring(0, 8), out num))
            {
                return num;
            }
            throw new BoletoNetException("Nosso número é inválido!");
        }
    }
}
