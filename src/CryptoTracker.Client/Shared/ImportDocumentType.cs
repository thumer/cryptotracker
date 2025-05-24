using CryptoTracker.Common;

namespace CryptoTracker.Shared
{
    public enum ImportDocumentType
    {
        [DisplayName("Binance Einzahlungen")]
        BinanceDepositHistory,

        [DisplayName("Binance Auszahlungen")]
        BinanceWithdrawalHistory,

        [DisplayName("Binance Trading History")]
        BinanceTradingHistory,

        [DisplayName("Bitcoin.de Transaktionen")]
        BitcoinDeTransactions,

        [DisplayName("Bitpanda Transaktionen")]
        BitpandaTransaction,

        [DisplayName("Metamask Transaktionen")]
        MetamaskTransactions,

        [DisplayName("Metamask Trading")]
        MetamaskTradingHistory,

        [DisplayName("Okx Einzahlungen")]
        OkxDepositHistory,

        [DisplayName("Okx Trading History")]
        OkxTradingHistory,
    }
}
