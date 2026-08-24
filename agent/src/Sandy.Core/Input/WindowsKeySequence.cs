namespace Sandy.Core.Input;

public readonly record struct WindowsKeyDecision(bool Suppress, bool InvokeHome, int ReplayWindowsKey = 0);

public sealed class WindowsKeySequence
{
    private readonly HashSet<int> _windowsKeysDown = [];
    private readonly HashSet<int> _swallowedChordKeys = [];
    private readonly HashSet<int> _passedChordKeys = [];
    private bool _chord;
    private int _replayedWindowsKey;

    public WindowsKeyDecision Handle(
        int key, bool down, bool windowsKey, bool modifiersDown, bool allowWindowsSpace = false)
    {
        if (windowsKey)
        {
            if (down)
            {
                if (_windowsKeysDown.Count == 0)
                    _chord = modifiersDown;
                _windowsKeysDown.Add(key);
                return new(true, false);
            }

            _windowsKeysDown.Remove(key);
            if (key == _replayedWindowsKey)
            {
                _replayedWindowsKey = 0;
                if (_windowsKeysDown.Count == 0)
                    _chord = false;
                return new(false, false);
            }
            if (_windowsKeysDown.Count != 0)
                return new(true, false);
            var invokeHome = !_chord;
            _chord = false;
            return new(true, invokeHome);
        }

        if (!down && _passedChordKeys.Remove(key))
            return new(false, false);

        if (_windowsKeysDown.Count > 0)
        {
            _chord = true;
            if (allowWindowsSpace)
            {
                if (!down)
                    return new(false, false);
                _passedChordKeys.Add(key);
                if (_replayedWindowsKey != 0)
                    return new(false, false);
                _replayedWindowsKey = _windowsKeysDown.First();
                return new(true, false, _replayedWindowsKey);
            }
            if (down)
                _swallowedChordKeys.Add(key);
            else
                _swallowedChordKeys.Remove(key);
            return new(true, false);
        }

        if (!down && _swallowedChordKeys.Remove(key))
            return new(true, false);
        return new(false, false);
    }
}
