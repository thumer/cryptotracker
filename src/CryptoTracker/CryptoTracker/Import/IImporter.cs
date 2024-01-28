using CryptoTracker.Import.Objects;

namespace CryptoTracker.Import
{
    public interface IImporter
    {
        Task Import(ImportArgs args, Func<Stream> openStreamFunc);
    }
}
