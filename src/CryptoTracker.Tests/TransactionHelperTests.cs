using CryptoTracker.Shared;
using FluentAssertions;

namespace CryptoTracker.Tests;

public class TransactionHelperTests
{
    #region IsSellTrade Tests

    [Fact]
    public void IsSellTrade_WithTrade_ReturnsTrue()
    {
        var row = CreateRow(FlowType.Trade, FlowDirection.Outflow);

        var result = IsSellTrade(row);

        result.Should().BeTrue();
    }

    [Fact]
    public void IsSellTrade_WithTradeInflow_ReturnsTrue()
    {
        // Even inflow trades (buys) return true because they're still trades
        var row = CreateRow(FlowType.Trade, FlowDirection.Inflow);

        var result = IsSellTrade(row);

        result.Should().BeTrue();
    }

    [Fact]
    public void IsSellTrade_WithTransaction_ReturnsFalse()
    {
        var row = CreateRow(FlowType.Transaction, FlowDirection.Outflow);

        var result = IsSellTrade(row);

        result.Should().BeFalse();
    }

    [Fact]
    public void IsSellTrade_WithTransactionInflow_ReturnsFalse()
    {
        var row = CreateRow(FlowType.Transaction, FlowDirection.Inflow);

        var result = IsSellTrade(row);

        result.Should().BeFalse();
    }

    #endregion

    #region RequiresLotAssignment Tests

    [Fact]
    public void RequiresLotAssignment_TradeOutflow_ReturnsTrue()
    {
        var row = CreateRow(FlowType.Trade, FlowDirection.Outflow);

        var result = RequiresLotAssignment(row);

        result.Should().BeTrue();
    }

    [Fact]
    public void RequiresLotAssignment_TradeInflow_ReturnsFalse()
    {
        // Buy trades (inflows) create new lots, they don't consume existing ones
        var row = CreateRow(FlowType.Trade, FlowDirection.Inflow);

        var result = RequiresLotAssignment(row);

        result.Should().BeFalse();
    }

    [Fact]
    public void RequiresLotAssignment_TransactionOutflow_ReturnsTrue()
    {
        // Send transactions need lot assignment to track which lots are moved
        var row = CreateRow(FlowType.Transaction, FlowDirection.Outflow);

        var result = RequiresLotAssignment(row);

        result.Should().BeTrue();
    }

    [Fact]
    public void RequiresLotAssignment_TransactionInflow_ReturnsFalse()
    {
        // Receive transactions don't need lot assignment, they receive lots from the send side
        var row = CreateRow(FlowType.Transaction, FlowDirection.Inflow);

        var result = RequiresLotAssignment(row);

        result.Should().BeFalse();
    }

    #endregion

    #region Helper Methods (copied from Transaktionen.razor.cs)

    /// <summary>
    /// Determines if the row represents a sell trade (crypto sold for fiat/other crypto)
    /// vs. a transfer (crypto moved between own wallets)
    /// </summary>
    private static bool IsSellTrade(TransactionRowDTO row)
    {
        // A sell trade is a Trade flow type where crypto is sold
        // A transfer is a Transaction flow type (Send/Receive between wallets)
        return row.FlowType == FlowType.Trade;
    }

    /// <summary>
    /// Determines if a transaction requires lot assignment
    /// </summary>
    private static bool RequiresLotAssignment(TransactionRowDTO row)
    {
        // Sell trades need lot assignment to calculate gains
        if (row.FlowType == FlowType.Trade)
        {
            // Only outgoing trades (sells) need lot assignment
            return row.FlowDirection == FlowDirection.Outflow;
        }

        // Transfers (Send transactions) need lot assignment to track which lots are moved
        if (row.FlowType == FlowType.Transaction)
        {
            return row.FlowDirection == FlowDirection.Outflow;
        }

        return false;
    }

    private static TransactionRowDTO CreateRow(FlowType flowType, FlowDirection flowDirection)
    {
        return new TransactionRowDTO(
            FlowType: flowType,
            FlowDirection: flowDirection,
            DateTime: DateTimeOffset.UtcNow,
            FlowId: 1,
            Symbol: "BTC",
            Amount: 1.0m,
            EuroValue: 50000m,
            RateEur: 50000m,
            Fee: 0m,
            SourceWallet: "Wallet1",
            TargetWallet: "Wallet2",
            Slug: "bitcoin",
            Comment: null,
            HasOpposite: false,
            TargetSymbol: null,
            TargetAmount: null,
            TargetSlug: null,
            RowKey: Guid.NewGuid(),
            IsHidden: false
        );
    }

    #endregion
}
