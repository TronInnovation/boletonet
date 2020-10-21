using BoletoNet.Util;
using System;
using System.Web.UI;

[assembly: WebResource("BoletoNet.Imagens.070.jpg", "image/jpg")]
namespace BoletoNet
{
    /// <author>  
    /// Eduardo Frare
    /// Stiven 
    /// Diogo
    /// Miamoto
    /// </author>    


    /// <summary>
    /// Classe referente ao banco Banco_BRB
    /// </summary>
    internal class Banco_BRB : AbstractBanco, IBanco
    {
        private int _dacBoleto = 0;

        internal Banco_BRB()
        {
            this.Codigo = 70;
            this.Digito = "1";
            this.Nome = "Banco_BRB";
        }

        #region IBanco Members

        public override void FormataCodigoBarra(Boleto boleto)
        {
            // Código de Barras
            //banco & moeda & fator & valor & carteira & nossonumero & dac_nossonumero & agencia & conta & dac_conta & "000"

            string banco = Utils.FormatCode(Codigo.ToString(), 3);
            int moeda = boleto.Moeda;
            //string digito = "";
            string valorBoleto = boleto.ValorBoleto.ToString("f").Replace(",", "").Replace(".", "");
            valorBoleto = Utils.FormatCode(valorBoleto, 10);

            string fatorVencimento = FatorVencimento(boleto).ToString();
            string chave = boleto.CodigoBarra.Chave;


            boleto.CodigoBarra.Codigo =
                string.Format("{0}{1}{2}{3}{4}", banco, moeda, fatorVencimento,
                              valorBoleto, boleto.CodigoBarra.Chave);


            _dacBoleto = Banco_BRB.Mod11_CodigoBarra(boleto.CodigoBarra.Codigo, 9);

            boleto.CodigoBarra.Codigo = Strings.Left(boleto.CodigoBarra.Codigo, 4) + _dacBoleto + Strings.Right(boleto.CodigoBarra.Codigo, 39);
        }

        public override void FormataLinhaDigitavel(Boleto boleto)
        {
            string BBB = Utils.FormatCode(Codigo.ToString(), 3);
            int M = boleto.Moeda;
            string CCCCC1 = boleto.CodigoBarra.Chave.Substring(0, 5);
            int D1 = 0;

            string CCCCCCCCCC2 = boleto.CodigoBarra.Chave.Substring(5, 10);
            int D2 = 0;

            string CCCCCCCCCC3 = boleto.CodigoBarra.Chave.Substring(15, 10);
            int D3 = 0;

            D1 = Mod10(BBB + M + CCCCC1);
            string Grupo1 = string.Format("{0}.{1}{2} ", BBB + M + CCCCC1.Substring(0, 1), CCCCC1.Substring(1, 4), D1);

            D2 = Mod10(CCCCCCCCCC2);
            string Grupo2 = string.Format("{0}.{1}{2} ", CCCCCCCCCC2.Substring(0, 5), CCCCCCCCCC2.Substring(5, 5), D2);

            D3 = Mod10(CCCCCCCCCC3);
            string Grupo3 = string.Format("{0}.{1}{2} ", CCCCCCCCCC3.Substring(0, 5), CCCCCCCCCC3.Substring(5, 5), D3);

            string Grupo4 = string.Format("{0} {1}{2}", _dacBoleto, FatorVencimento(boleto).ToString(), Utils.FormatCode(boleto.ValorBoleto.ToString("f").Replace(",", "").Replace(".", ""), 10));

            boleto.CodigoBarra.LinhaDigitavel = Grupo1 + Grupo2 + Grupo3 + Grupo4;
        }

        public override void FormataNossoNumero(Boleto boleto)
        {
            boleto.NossoNumero = string.Format("{0}{1}{2}", boleto.Categoria, boleto.NossoNumeroSemFormatacao, Utils.FormatCode(Codigo.ToString(), 3) + boleto.CodigoBarra.Chave.Substring(23, 2));
        }

        public override void FormataNumeroDocumento(Boleto boleto)
        {
            boleto.NumeroDocumento = string.Format("{0}", boleto.NumeroDocumento);
        }

        public override void ValidaBoleto(Boleto boleto)
        {
            //Verifica se o nosso número é válido
            if (Utils.ToInt64(boleto.NossoNumero) == 0)
                throw new NotImplementedException("Nosso número inválido");

            //Verifica se o tamanho para o NossoNumero são 12 dígitos
            if (Convert.ToInt32(boleto.NossoNumero).ToString().Length > 6)
                throw new NotImplementedException("A quantidade de dígitos do nosso número para a carteira " + boleto.Carteira + ", são 6 números.");
            else if (Convert.ToInt32(boleto.NossoNumero).ToString().Length < 6)
                boleto.NossoNumero = Utils.FormatCode(boleto.NossoNumero, 6);

            boleto.NossoNumeroSemFormatacao = boleto.NossoNumero;

            if (boleto.Carteira != "COB")
                throw new NotImplementedException("Carteira não implementada. Utilize a carteira COB.");

            //Atribui o nome do banco ao local de pagamento
            boleto.LocalPagamento += Nome + "";

            //Verifica se data do processamento é valida
            //if (boleto.DataProcessamento.ToString("dd/MM/yyyy") == "01/01/0001")
            if (boleto.DataProcessamento == DateTime.MinValue) // diegomodolo (diego.ribeiro@nectarnet.com.br)
                boleto.DataProcessamento = DateTime.Now;

            //Verifica se data do documento é valida
            //if (boleto.DataDocumento.ToString("dd/MM/yyyy") == "01/01/0001")
            if (boleto.DataDocumento == DateTime.MinValue) // diegomodolo (diego.ribeiro@nectarnet.com.br)
                boleto.DataDocumento = DateTime.Now;

            FormataChave(boleto);
            FormataCodigoBarra(boleto);
            FormataLinhaDigitavel(boleto);
            FormataNossoNumero(boleto);
            FormataNumeroDocumento(boleto);
        }

        public override string GerarHeaderRemessa(string numeroConvenio, Cedente cedente, TipoArquivo tipoArquivo, int numeroArquivoRemessa, Boleto boletos)
        {
            throw new NotImplementedException("Função não implementada.");
        }
        #endregion

        public void FormataChave(Boleto boleto)
        {
            string zeros = "000";
            string agencia = boleto.Cedente.ContaBancaria.Agencia;
            string conta = boleto.Cedente.ContaBancaria.Conta + boleto.Cedente.ContaBancaria.DigitoConta;
            int categoria = 1;
            boleto.Categoria = categoria;
            string nossonumero = boleto.NossoNumeroSemFormatacao;
            string banco = Utils.FormatCode(Codigo.ToString(), 3);

            //Mod10 dentro da classe Banco_BRB pelas particularidades que ela tem.
            int d1 = Banco_BRB.Mod10(zeros + agencia + conta + categoria + nossonumero + banco);
            int d2 = Banco_BRB.Mod11_NossoNumero(zeros + agencia + conta + categoria + nossonumero + banco + d1, 7);

            if (d2 > 10)
            {
                d1 += 1;
                d2 -= 20;
            }

            boleto.CodigoBarra.Chave = zeros + agencia + conta + categoria + nossonumero + banco + d1 + d2;
        }

        internal static int Mod11_CodigoBarra(string value, int Base)
        {
            int Digito, Soma = 0, Peso = 2;

            for (int i = value.Length; i > 0; i--)
            {
                Soma = Soma + (Convert.ToInt32(value.Mid(i, 1)) * Peso);
                if (Peso == Base)
                    Peso = 2;
                else
                    Peso = Peso + 1;
            }

            if (((Soma % 11) == 0) || ((Soma % 11) == 10) || ((Soma % 11) == 1))
            {
                Digito = 1;
            }
            else
            {
                Digito = 11 - (Soma % 11);
            }

            return Digito;
        }

        internal static int Mod11_NossoNumero(string value, int Base)
        {

            int Digito, Soma = 0, Peso = 2;

            for (int i = value.Length; i > 0; i--)
            {
                Soma = Soma + (Convert.ToInt32(value.Mid(i, 1)) * Peso);
                if (Peso == Base)
                    Peso = 2;
                else
                    Peso = Peso + 1;
            }

            if ((Soma % 11) > 1)
            {
                Digito = 11 - (Soma % 11);
            }
            else if ((Soma % 11) == 1)
            {
                int d1 = Utils.ToInt32(Strings.Mid(value, value.Length, value.Length - 1));

                d1 += 1;

                if (d1 == 10)
                    d1 = 0;

                Digito = Banco_BRB.Mod11_NossoNumero(Strings.Mid(value, 1, value.Length - 1) + d1, 7);
                Digito += 20;
            }
            else
            {
                Digito = (Soma % 11);
            }

            return Digito;

        }


        internal new static int Mod10(string seq)
        {

            int Digito, Soma = 0, Peso = 2, res;

            for (int i = seq.Length; i > 0; i--)
            {
                res = (Convert.ToInt32(Strings.Mid(seq, i, 1)) * Peso);

                if (res > 9)
                    res = (res - 9);

                Soma += res;

                if (Peso == 2)
                    Peso = 1;
                else
                    Peso = Peso + 1;
            }

            Digito = ((10 - (Soma % 10)) % 10);

            return Digito;
        }

        public override string GerarHeaderRemessa(string numeroConvenio, Cedente cedente, TipoArquivo tipoArquivo, int numeroArquivoRemessa)
        {
            try
            {
                string header = Utils.FormatCode("DCB", 3);
                header += "001";//versão
                header += "075";// arquivo
                header += cedente.ContaBancaria.Agencia.ToString();// numero agencia
                header += cedente.ContaBancaria.Conta.ToString() + cedente.ContaBancaria.DigitoConta.ToString();  // numero conta corrente
                header += DateTime.Now.ToString("yyyyMMdd"); // data de formatação
                header += DateTime.Now.ToString("hhMMss"); // hora de formatação
                header += numeroConvenio.ToString().PadLeft(6, '0'); // header+registros

                return header;
            }
            catch (Exception e)
            {
                throw new Exception("Erro ao gerar HEADER DO LOTE do arquivo de remessa.", e);

            }
        }

        public string GerarDetalheRemessa(Boleto boleto, int numeroRegistro, TipoArquivo tipoArquivo)
        {
            try
            {
                ValidaBoleto(boleto);
                
                string _detalhe;
                _detalhe = "01";// identificacao registro 1 a 2
                _detalhe += Utils.FitStringLength(boleto.ContaBancaria.Agencia.ToString(), 3, 3, '0', 0, true, true, false); //agencia 3 a 5
                _detalhe += Utils.FitStringLength(boleto.ContaBancaria.Conta.ToString() + boleto.ContaBancaria.DigitoConta.ToString(), 7, 7, '0', 0, true, true, false);//conta + digito conta 6  a 12
                _detalhe += Utils.FitStringLength(boleto.Sacado.CPFCNPJ.Replace(".", "").Replace("-", "").Replace("/", ""), 14, 14, ' ', 0, true, true, false);//cpf ou cnpj 12 a 26
                _detalhe += Utils.FitStringLength(boleto.Sacado.Nome, 35, 35, ' ', 0, true, true, false); //nome pagador 27 a 61
                _detalhe += Utils.FitStringLength(boleto.Sacado.Endereco.End, 35, 35, ' ', 0, true, true, false); //endereco pagador 62 a 96
                _detalhe += Utils.FitStringLength(boleto.Sacado.Endereco.Cidade, 15, 15, ' ', 0, true, true, false); //cidade pagador 97 a 111
                _detalhe += Utils.FitStringLength(boleto.Sacado.Endereco.UF, 2, 2, ' ', 0, true, true, false); //uf pagador 112 a 113
                _detalhe += Utils.FitStringLength(boleto.Sacado.Endereco.CEP.Replace(".", "").Replace("-", ""), 8, 8, ' ', 0, true, true, false); //uf pagador 114 a 121
                _detalhe += Utils.FitStringLength(boleto.Sacado.CPFCNPJ.Length > 11 ? "2" : "1", 1, 1, ' ', 0, true, true, false); //uf pagador 122 a 122
                _detalhe += Utils.FitStringLength(boleto.NumeroDocumento, 13, 13, ' ', 0, true, true, false); //seu numero 123 a 135
                _detalhe += Utils.FitStringLength("1", 1, 1, ' ', 0, true, true, false); //cod categoria cobranca 136 a 136
                _detalhe += Utils.FitStringLength(DateTime.Now.ToString("ddMMyyyy"), 8, 8, ' ', 0, true, true, false); //data emissao titulo 137 a 144
                _detalhe += Utils.FitStringLength("21", 2, 2, ' ', 0, true, true, false); //cod  tipo documento 145 a 146
                _detalhe += Utils.FitStringLength("0", 1, 1, ' ', 0, true, true, false); //cod  natureza 0 -simples 147 a 147 
                _detalhe += Utils.FitStringLength("0", 1, 1, ' ', 0, true, true, false); //cod  cond pagamento 148 a 148
                _detalhe += Utils.FitStringLength("02", 2, 2, ' ', 0, true, true, false); //cod da moeda 149 a 150
                _detalhe += Utils.FitStringLength("070", 3, 3, ' ', 0, true, true, false); //cod numero banco 151 a 153
                _detalhe += Utils.FitStringLength(boleto.Cedente.ContaBancaria.Agencia, 4, 4, '0', '0', true, true, false); //numero agencia cobradora 154 a 157
                _detalhe += Utils.FitStringLength("Brasilia", 30, 30, ' ', 0, true, true, false); //praca cobranca 158 a 187
                _detalhe += Utils.FitStringLength(boleto.DataVencimento.ToString("ddMMyyyy"), 8, 8, ' ', 0, true, true, false); //data vencimento titulo 188 a  195
                _detalhe += Utils.FormatCode(boleto.ValorBoleto.ToString("f").Replace(",", "").Replace(".", ""), 14); // 196 a 209 - valor do titulo
                _detalhe += Utils.FitStringLength(boleto.NossoNumero, 12, 12, '0', 0, true, true, false); // 210 a 221 nossoNumero
                _detalhe += Utils.FitStringLength(boleto.CodJurosMora == "2"? "51":"50", 2, 2, '0', 0, true, true, false); // 222 a 223 codigo tipo juros

                //FormataNossoNumero

                decimal juro = 0;
                if(boleto.CodJurosMora == "2")
                {
                    juro = boleto.PercJurosMora * 30;
                }
                else
                {
                    juro = boleto.JurosMora;
                }

                _detalhe += Utils.FormatCode(juro.ToString("f").Replace(",", "").Replace(".", ""), 14); // 224 a 237 valor do juros
                _detalhe += Utils.FormatCode(boleto.ValorAbatimento.ToString("f").Replace(",", "").Replace(".", ""), 14); // 238 a 251 valor abatimento
                _detalhe += Utils.FitStringLength(boleto.ValorDesconto == 0 ? "00": boleto.CodigoDesconto.ToString(), 2, 2, '0', 0, true, true, false); ; // 252 a 253 codigo desconto
                _detalhe += Utils.FitStringLength(boleto.DataDesconto.ToString("ddMMyyyy"), 8, 8, '0', 0, true, true, false); // 254 a 261 data limite desconto
                _detalhe += Utils.FormatCode(boleto.ValorDesconto.ToString("f").Replace(",", "").Replace(".", ""), 14); // 262 a 275 valor do desconto
                _detalhe += Utils.FitStringLength("03", 2, 2, '0', 0, true, true, false); // 276 a 277  codigo da 1 instrucao
                _detalhe += Utils.FitStringLength("00", 2, 2, '0', 0, true, true, false); // 278 a 279  prazo da 1 instrucao
                _detalhe += Utils.FitStringLength("94", 2, 2, '0', 0, true, true, false); // 280 a 281  codigo da 2 instrucao
                _detalhe += Utils.FitStringLength(boleto.NumeroDiasBaixa > 99?"99":boleto.NumeroDiasBaixa.ToString(), 2, 2, ' ', 0, true, true, false); // 282 a 283 prazo da 2 instrucao
                _detalhe += Utils.FormatCode(String.Format("{0:0.00}", boleto.PercMulta).Replace(",", "").Replace(".", ""), 5); // 284 a 288 taxa ref
                _detalhe += Utils.FitStringLength(boleto.Cedente.Nome, 40, 40, ' ', ' ', true, true, false); //289 a 328 emitente do titulo
                _detalhe += Utils.FitStringLength("", 40, 40, ' ', ' ', true, true, false); // 329 a 369 observacoes
                _detalhe += Utils.FitStringLength("", 32, 32, ' ', ' ', true, true, false); //369 a 400 branco


                _detalhe = Utils.SubstituiCaracteresEspeciais(_detalhe);

                return _detalhe;
            }
            catch (Exception ex)
            {
                throw new Exception("Erro durante a geração do SEGMENTO S DO DETALHE do arquivo de Remessa", ex);
            }
        }

        public override HeaderRetorno LerHeaderRetornoCNAB400(string registro)
        {
            try
            {
                HeaderRetorno header = new HeaderRetorno(400, registro);

                header.ComplementoRegistro1 = Utils.ToInt32(registro.Substring(000, 1));
                header.CodigoRetorno = Utils.ToInt32(registro.Substring(001, 1));
                header.LiteralRetorno = registro.Substring(002, 7);
                header.CodigoServico = Utils.ToInt32(registro.Substring(009, 2));
                header.LiteralServico = registro.Substring(01, 15);
                header.Conta = Utils.ToInt32(registro.Substring(0, 20));
                header.NomeEmpresa = registro.Substring(046, 20);
                header.CodigoBanco = Utils.ToInt32(registro.Substring(076, 3));
                header.NomeBanco = registro.Substring(079, 15);
                header.DataCredito = Utils.ToDateTime(Utils.ToInt32(registro.Substring(094, 285)).ToString("ddMMyyyy"));
                header.DataGeracao = Utils.ToDateTime(Utils.ToInt32(registro.Substring(094, 15)).ToString("ddMMyyyy"));
                header.NumeroSequencial = Utils.ToInt32(registro.Substring(394, 6));
                return header;
            }
            catch (Exception ex)
            {
                throw new Exception("Erro ao ler header do arquivo de RETORNO / CNAB 400.", ex);
            }
        }

        public override DetalheRetorno LerDetalheRetornoCNAB400(string registro)
        {
            try
            {
                DetalheRetorno detalhe = new DetalheRetorno(registro);

                //Tipo de Identificação do registro
                detalhe.IdentificacaoDoRegistro = Utils.ToInt32(registro.Substring(0, 1));

                //Tipo de inscrição
                detalhe.TipoInscricao = Utils.ToInt32(registro.Substring(1, 2));

                //CGC ou CPF
                detalhe.CgcCpf = registro.Substring(3, 14);

                //Conta Corrente
                detalhe.ContaCorrente = Utils.ToInt32(registro.Substring(20, 17));

                //Nosso Número
                detalhe.NossoNumeroComDV = registro.Substring(70, 12);
                detalhe.NossoNumero = registro.Substring(70, 11); //Sem o DV
                detalhe.DACNossoNumero = registro.Substring(82, 1); //DV 
                //Seu Número
                detalhe.SeuNumero = registro.Substring(92, 13);

                //Instrução
                detalhe.CodigoOcorrencia= Utils.ToInt32(registro.Substring(108, 2));

                //Número do documento
                detalhe.NumeroDocumento = registro.Substring(128, 12);

                //Código do Raterio
                detalhe.CodigoRateio = Utils.ToInt32(registro.Substring(140, 4));

                //Data Ocorrência no Banco
                int dataOcorrencia = Utils.ToInt32(registro.Substring(110, 8));
                detalhe.DataOcorrencia = Utils.ToDateTime(dataOcorrencia.ToString("##-##-####"));

                //Data Vencimento do Título
                int dataVencimento = Utils.ToInt32(registro.Substring(148, 8));
                detalhe.DataVencimento = Utils.ToDateTime(dataVencimento.ToString("##-##-####"));

                //Valor do Título
                decimal valorTitulo = Convert.ToInt64(registro.Substring(156, 13));
                detalhe.ValorTitulo = valorTitulo / 100;

                //Banco Cobrador
                detalhe.BancoCobrador = Utils.ToInt32(registro.Substring(163, 3));

                //Agência Cobradora
                detalhe.AgenciaCobradora = Utils.ToInt32(registro.Substring(172, 5));

                //Espécie Título
                detalhe.EspecieTitulo = registro.Substring(177, 2);

                //Despesas de cobrança para os Códigos de Ocorrência (Valor Despesa)
                decimal despeasaDeCobranca = Convert.ToUInt64(registro.Substring(179, 13));
                detalhe.DespeasaDeCobranca = despeasaDeCobranca / 100;

                //Outras despesas Custas de Protesto (Valor Outras Despesas)
                decimal outrasDespesas = Convert.ToUInt64(registro.Substring(192, 13));
                detalhe.OutrasDespesas = outrasDespesas / 100;

                //Juros Mora
                decimal juros = Convert.ToUInt64(registro.Substring(205, 13));
                detalhe.Juros = juros / 100;

                // IOF
                decimal iof = Convert.ToUInt64(registro.Substring(218, 13));
                detalhe.IOF = iof / 100;

                //Abatimento Concedido sobre o Título (Valor Abatimento Concedido)
                decimal abatimento = Convert.ToUInt64(registro.Substring(231, 13));
                detalhe.Abatimentos = abatimento / 100;

                //Desconto Concedido (Valor Desconto Concedido)
                decimal desconto = Convert.ToUInt64(registro.Substring(244, 13));
                detalhe.Descontos = desconto / 100;

                //Valor Pago
                decimal valorPago = Convert.ToUInt64(registro.Substring(257, 13));
                detalhe.ValorPago = valorPago / 100;

                //Outros Débitos
                decimal outrosDebitos = Convert.ToUInt64(registro.Substring(270, 13));
                detalhe.OutrosDebitos = outrosDebitos / 100;

                //Outros Créditos
                decimal outrosCreditos = Convert.ToUInt64(registro.Substring(283, 13));
                detalhe.OutrosCreditos = outrosCreditos / 100;

                // Data de Liquidação
                int dataLiquidacao = Utils.ToInt32(registro.Substring(299, 8));
                detalhe.DataLiquidacao = Utils.ToDateTime(dataLiquidacao.ToString("##-##-####"));

                //Motivo de Rejeição
                detalhe.MotivosRejeicao = registro.Substring(364, 30);

                //Motivo de Rejeição
                detalhe.Sequencial = Utils.ToInt32(registro.Substring(394, 6));

                return detalhe;
            }
            catch (Exception ex)
            {
                throw new Exception("Erro ao ler detalhe do arquivo de RETORNO / CNAB 400.", ex);
            }
        }

       

        /// <summary>
        /// Efetua as Validações dentro da classe Boleto, para garantir a geração da remessa
        /// </summary>
        public override bool ValidarRemessa(TipoArquivo tipoArquivo, string numeroConvenio, IBanco banco, Cedente cedente, Boletos boletos, int numeroArquivoRemessa, out string mensagem)
        {
            bool vRetorno = true;
            string vMsg = string.Empty;
            ////IMPLEMENTACAO PENDENTE...
            mensagem = vMsg;
            return vRetorno;
        }

    }
}
