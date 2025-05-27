using CryptoTracker.Shared;
using Microsoft.AspNetCore.Components;

namespace CryptoTracker.Client.Pages;

public partial class AssetFlow
{
    private bool IsLoading { get; set; } = true;
    private string? ErrorMessage { get; set; }
    private IList<PlatformBalanceDTO> Balances { get; set; } = new List<PlatformBalanceDTO>();
    private IList<string> WalletNames { get; set; } = new List<string>();
    private IList<AssetOption> WalletAssetOptions { get; set; } = new List<AssetOption>();
    private string? SelectedWallet { get; set; }
    private string? SelectedAsset { get; set; }
    private decimal SelectedAmount { get; set; }
    private IList<AssetFlowLineDTO>? FlowLines { get; set; }
    private decimal CurrentValue { get; set; }
    private decimal TotalCost { get; set; }

    private record AssetOption(string Symbol, string Display, decimal Amount);

    private const int WalletSpacing = 150;
    private const int RowSpacing = 60;

    private Dictionary<string, int> WalletPositions
        => FlowLines?
            .SelectMany(l => new[] { l.SourceWallet, l.TargetWallet })
            .Where(w => !string.IsNullOrEmpty(w))
            .Distinct()
            .Select((w, i) => new { w, i })
            .ToDictionary(x => x.w!, x => (x.i + 1) * WalletSpacing)
            ?? new Dictionary<string, int>();

    private int SvgWidth => WalletPositions.Count * WalletSpacing + WalletSpacing;
    private int SvgHeight => (FlowLines?.Count ?? 0) * RowSpacing + 60;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        await LoadBalances();
        IsLoading = false;
    }

    private async Task LoadBalances()
    {
        Balances = await BalanceApi.GetBalancesAsync();
        WalletNames = Balances.Select(b => b.Platform).ToList();
    }

    private async Task LoadFlows()
    {
        if (string.IsNullOrEmpty(SelectedAsset) || string.IsNullOrEmpty(SelectedWallet))
            return;

        try
        {
            FlowLines = await AssetFlowApi.GetAssetFlowsAsync(SelectedWallet, SelectedAsset);
            await LoadCurrentValue();
            CalculateCost();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    private void CalculateCost()
    {
        if (FlowLines == null)
            return;

        decimal cost = 0;
        var fifo = new Queue<(decimal qty, decimal price)>();
        foreach (var line in FlowLines.OrderBy(l => l.DateTime))
        {
            if (line.TradeId.HasValue && line.OppositeSymbol == "EUR" && line.Amount > 0)
            {
                fifo.Enqueue((line.Amount, line.Price ?? 0));
                cost += line.Amount * (line.Price ?? 0);
            }
            else if (line.TradeId.HasValue && line.OppositeSymbol == "EUR" && line.Amount < 0)
            {
                var remaining = Math.Abs(line.Amount);
                while (remaining > 0 && fifo.Count > 0)
                {
                    var item = fifo.Peek();
                    var take = Math.Min(remaining, item.qty);
                    cost -= take * item.price;
                    item.qty -= take;
                    remaining -= take;
                    if (item.qty <= 0)
                        fifo.Dequeue();
                    else
                    {
                        fifo.Dequeue();
                        fifo.Enqueue(item);
                    }
                }
            }
        }

        TotalCost = cost;
    }

    private void OnWalletChanged(string wallet)
    {
        SelectedWallet = wallet;
        var assets = Balances.FirstOrDefault(b => b.Platform == wallet)?.Assets ?? new List<AssetBalanceDTO>();
        WalletAssetOptions = assets.Where(a => a.Amount > 0)
            .Select(a => new AssetOption(a.Symbol, $"{a.Symbol} ({a.Amount})", a.Amount))
            .ToList();
        SelectedAsset = null;
        SelectedAmount = 0;
    }

    private void OnAssetChanged(string symbol)
    {
        SelectedAsset = symbol;
        var opt = WalletAssetOptions.FirstOrDefault(o => o.Symbol == symbol);
        SelectedAmount = opt?.Amount ?? 0;
    }

    private void SetMax()
    {
        var opt = WalletAssetOptions.FirstOrDefault(o => o.Symbol == SelectedAsset);
        if (opt != null)
            SelectedAmount = opt.Amount;
    }

    private async Task LoadCurrentValue()
    {
        var balances = await BalanceApi.GetBalancesAsync();
        var asset = balances.FirstOrDefault(b => b.Platform == SelectedWallet)?.Assets.FirstOrDefault(a => a.Symbol == SelectedAsset);
        if (asset != null && asset.Amount > 0)
        {
            var rate = asset.EuroValue / asset.Amount;
            CurrentValue = SelectedAmount * rate;
        }
    }
}
