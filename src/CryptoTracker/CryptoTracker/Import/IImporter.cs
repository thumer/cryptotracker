using CryptoTracker.Import.Objects;

namespace CryptoTracker.Import
{
    public interface IImporter
    {
        void Import(Func<Stream> openStreamFunc);
    }
}
