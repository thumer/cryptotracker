using CryptoTracker;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace CryptoTracker.Tests;

public static class TestHelper
{
    public static CryptoTrackerDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CryptoTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new CryptoTrackerDbContext(options);
    }

    public static Stream CreateStream(string data)
        => new MemoryStream(Encoding.UTF8.GetBytes(data));
}
