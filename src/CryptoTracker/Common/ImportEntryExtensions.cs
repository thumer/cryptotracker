using CryptoTracker.Entities.Import;
using CryptoTracker.Shared;

namespace CryptoTracker.Common;

public static class ImportEntryExtensions
{

    public static BinanceDepositDTO ToDto(this BinanceDepositEntity e)
        => new()
        {
            WalletId = e.WalletId,
            Wallet = e.Wallet.Name,
            Date = e.Date,
            Coin = e.Coin,
            Network = e.Network,
            Amount = e.Amount,
            TransactionFee = e.TransactionFee,
            Address = e.Address,
            TXID = e.TXID,
            Comment = e.Comment
        };

    public static BinanceWithdrawalDTO ToDto(this BinanceWithdrawalEntity e)
        => new()
        {
            WalletId = e.WalletId,
            Wallet = e.Wallet.Name,
            Date = e.Date,
            Coin = e.Coin,
            Network = e.Network,
            Amount = e.Amount,
            TransactionFee = e.TransactionFee,
            Address = e.Address,
            TXID = e.TXID,
            Comment = e.Comment
        };

    public static BinanceTradeDTO ToDto(this BinanceTradeEntity e)
        => new()
        {
            Id = e.Id,
            WalletId = e.WalletId,
            Wallet = e.Wallet.Name,
            Date = e.Date,
            Pair = e.Pair,
            Side = e.Side,
            Price = e.Price,
            Executed = e.Executed,
            Amount = e.Amount,
            Fee = e.Fee
        };

    public static BitcoinDeTransactionDTO ToDto(this BitcoinDeTransactionEntity e)
        => new()
        {
            WalletId = e.WalletId,
            Wallet = e.Wallet.Name,
            Datum = e.Datum,
            Typ = e.Typ,
            Waehrung = e.Waehrung,
            Referenz = e.Referenz,
            Adresse = e.Adresse,
            Kurs = e.Kurs,
            EinheitKurs = e.EinheitKurs,
            CryptoVorGebuehr = e.CryptoVorGebuehr,
            MengeVorGebuehr = e.MengeVorGebuehr,
            EinheitMengeVorGebuehr = e.EinheitMengeVorGebuehr,
            CryptoNachGebuehr = e.CryptoNachGebuehr,
            MengeNachGebuehr = e.MengeNachGebuehr,
            EinheitMengeNachGebuehr = e.EinheitMengeNachGebuehr,
            ZuAbgang = e.ZuAbgang,
            Kontostand = e.Kontostand,
            Kommentar = e.Kommentar
        };

    public static BitpandaTransactionDTO ToDto(this BitpandaTransactionEntity e)
        => new()
        {
            WalletId = e.WalletId,
            Wallet = e.Wallet.Name,
            TransactionId = e.TransactionId,
            Timestamp = e.Timestamp,
            TransactionType = e.TransactionType,
            InOut = e.InOut,
            AmountFiat = e.AmountFiat,
            Fiat = e.Fiat,
            AmountAsset = e.AmountAsset,
            Asset = e.Asset,
            AssetMarketPrice = e.AssetMarketPrice,
            AssetMarketPriceCurrency = e.AssetMarketPriceCurrency,
            AssetClass = e.AssetClass,
            ProductID = e.ProductID,
            Fee = e.Fee,
            FeeAsset = e.FeeAsset,
            Spread = e.Spread,
            SpreadCurrency = e.SpreadCurrency,
            TaxFiat = e.TaxFiat,
            Address = e.Address,
            Comment = e.Comment
        };

    public static MetamaskTradeDTO ToDto(this MetamaskTradeEntity e)
        => new()
        {
            WalletId = e.WalletId,
            Wallet = e.Wallet.Name,
            Date = e.Date,
            Pair = e.Pair,
            Side = e.Side,
            Price = e.Price,
            Executed = e.Executed,
            Amount = e.Amount,
            Fee = e.Fee,
            Tradingplatform = e.Tradingplatform
        };

    public static MetamaskTransactionDTO ToDto(this MetamaskTransactionEntity e)
        => new()
        {
            WalletId = e.WalletId,
            Wallet = e.Wallet.Name,
            Datum = e.Datum,
            Typ = e.Typ,
            Coin = e.Coin,
            Network = e.Network,
            Amount = e.Amount,
            TransactionFee = e.TransactionFee,
            Kommentar = e.Kommentar
        };

    public static OkxDepositDTO ToDto(this OkxDepositEntity e)
        => new()
        {
            WalletId = e.WalletId,
            Wallet = e.Wallet.Name,
            Date = e.Date,
            Coin = e.Coin,
            Network = e.Network,
            Amount = e.Amount,
            TransactionFee = e.TransactionFee,
            Address = e.Address,
            TXID = e.TXID,
            Comment = e.Comment
        };

    public static OkxTradeDTO ToDto(this OkxTradeEntity e)
        => new()
        {
            WalletId = e.WalletId,
            Wallet = e.Wallet.Name,
            Date = e.Date,
            Pair = e.Pair,
            Side = e.Side,
            Price = e.Price,
            Executed = e.Executed
        };
}
