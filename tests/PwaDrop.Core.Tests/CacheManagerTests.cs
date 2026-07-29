using PwaDrop.Core;

namespace PwaDrop.Core.Tests;

public sealed class CacheManagerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "PwaDrop.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void CreateSessionDirectoryCreatesUniqueDirectory()
    {
        var manager = new CacheManager(_root);

        var first = manager.CreateSessionDirectory();
        var second = manager.CreateSessionDirectory();

        Assert.NotEqual(first, second);
        Assert.True(Directory.Exists(first));
        Assert.True(Directory.Exists(second));
    }

    [Fact]
    public void PurgeExpiredRemovesOnlyOldSessions()
    {
        var manager = new CacheManager(_root);
        var oldSession = manager.CreateSessionDirectory();
        var currentSession = manager.CreateSessionDirectory();
        Directory.SetLastWriteTimeUtc(oldSession, DateTime.UtcNow.Subtract(TimeSpan.FromDays(2)));

        manager.PurgeExpired(DateTimeOffset.UtcNow);

        Assert.False(Directory.Exists(oldSession));
        Assert.True(Directory.Exists(currentSession));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}

