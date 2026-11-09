using System.Runtime.InteropServices;

namespace MajsoulBroadcastScorer.V27.Services;

public sealed record ClipboardRetryResult(bool Success, Exception? Error);

public static class ClipboardRetry
{
    public static async Task<ClipboardRetryResult> TrySetAsync(
        Action setClipboard,
        int attempts = 25,
        int delayMilliseconds = 120)
    {
        ArgumentNullException.ThrowIfNull(setClipboard);
        if (attempts < 1)
            throw new ArgumentOutOfRangeException(nameof(attempts));

        Exception? lastError = null;
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            try
            {
                setClipboard();
                return new ClipboardRetryResult(true, null);
            }
            catch (ExternalException exception)
            {
                lastError = exception;
                if (attempt + 1 < attempts)
                    await Task.Delay(delayMilliseconds);
            }
        }

        return new ClipboardRetryResult(false, lastError);
    }
}
