namespace CryptoTracker.Client.Pages
{
    public class Crypto
    {
        public string Name { get; set; }
    }

    public partial class Overview
    {
        private bool IsLoading { get; set; }
        private IList<Crypto> Cryptos { get; set; } = [];

        private string? ErrorMessage { get; set; }

        protected override async Task OnInitializedAsync()
        {
            await Load();
        }

        private async Task Load()
        {
            IsLoading = true;
            await InvokeAsync(StateHasChanged);

            Cryptos = new List<Crypto>()
            {
                new Crypto { Name = "BTC"}
            };
            IsLoading = false;
            await InvokeAsync(StateHasChanged);
        }
    }
}
