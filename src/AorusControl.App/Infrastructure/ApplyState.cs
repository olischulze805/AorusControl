namespace AorusControl.App.Infrastructure;

/// <summary>
/// Where a setting that applies itself currently stands.
///
/// It lives beside the view models rather than with the mark that draws it: what a pending
/// change is doing is state, and the ring is only one way of showing it.
/// </summary>
public enum ApplyState
{
    /// <summary>Nothing in flight. The indicator draws nothing at all.</summary>
    Idle,
    /// <summary>Changed, and on its way to the device - the wait before the write included.</summary>
    Applying,
    /// <summary>Written and read back. Shown briefly, then it fades out by itself.</summary>
    Confirmed,
    /// <summary>The write or the readback refused. Stays until the next attempt; the reason
    /// belongs in the status line, which is the one place a sentence is still worth having.</summary>
    Failed
}
