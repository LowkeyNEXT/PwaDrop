namespace PwaDrop.Core;

public sealed record DragFileDescriptor(
    int Index,
    string DisplayName,
    long? Size,
    DateTimeOffset? LastWriteTime = null);

