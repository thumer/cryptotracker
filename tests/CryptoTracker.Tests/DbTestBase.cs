using System;
using CryptoTracker;
using Microsoft.EntityFrameworkCore;

namespace CryptoTracker.Tests;

public abstract class DbTestBase : IDisposable
{
    protected DbTestBase()
    {
        var options = new DbContextOptionsBuilder<CryptoTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        DbContext = new CryptoTrackerDbContext(options);
    }

    protected CryptoTrackerDbContext DbContext { get; }

    public void Dispose() => DbContext.Dispose();
}
