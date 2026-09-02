namespace TiezhuToolbox.Modules.Capture;

/// <summary>串行化 Capture，避免实时预览和 OCR/自动强化同时截图冲突。</summary>
internal sealed class LockedGameSession : IGameSession
{
    private readonly IGameSession _inner;
    private readonly object _gate;

    public LockedGameSession(IGameSession inner, object gate)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
    }

    public string DisplayName => _inner.DisplayName;

    public Bitmap Capture()
    {
        lock (_gate)
            return _inner.Capture();
    }

    public void Tap(int x, int y) => _inner.Tap(x, y);

    public void PressBack() => _inner.PressBack();
}
