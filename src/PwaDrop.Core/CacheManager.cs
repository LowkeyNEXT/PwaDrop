namespace PwaDrop.Core;

public sealed class CacheManager
{
    public static readonly TimeSpan SuccessfulDropLifetime = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan AbandonedSessionLifetime = TimeSpan.FromHours(24);

    public CacheManager(string rootPath)
    {
        RootPath = Path.GetFullPath(rootPath ?? throw new ArgumentNullException(nameof(rootPath)));
    }

    public string RootPath { get; }

    public string CreateSessionDirectory()
    {
        Directory.CreateDirectory(RootPath);
        var sessionPath = Path.Combine(RootPath, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(sessionPath);
        return sessionPath;
    }

    public void PurgeExpired(DateTimeOffset now)
    {
        if (!Directory.Exists(RootPath))
        {
            return;
        }

        foreach (var directory in Directory.EnumerateDirectories(RootPath))
        {
            try
            {
                var lastWrite = new DateTimeOffset(Directory.GetLastWriteTimeUtc(directory), TimeSpan.Zero);
                if (now - lastWrite >= AbandonedSessionLifetime)
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
            catch (IOException)
            {
                // A destination may still be reading the file. Cleanup is retried later.
            }
            catch (UnauthorizedAccessException)
            {
                // Preserve user workflow and retry on the next cleanup pass.
            }
        }
    }

    public async Task DeleteSessionAfterDelayAsync(string sessionPath, TimeSpan delay, CancellationToken cancellationToken)
    {
        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        for (var attempt = 0; attempt < 6; attempt++)
        {
            try
            {
                if (Directory.Exists(sessionPath))
                {
                    Directory.Delete(sessionPath, recursive: true);
                }

                return;
            }
            catch (IOException) when (attempt < 5)
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt + 1)), cancellationToken).ConfigureAwait(false);
            }
            catch (UnauthorizedAccessException) when (attempt < 5)
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt + 1)), cancellationToken).ConfigureAwait(false);
            }
        }
    }
}

