namespace Sandy.Core.Input;

public readonly record struct WindowsKeyDecision(bool Suppress, bool InvokeHome);

public sealed class WindowsKeySequence
{
    private readonly HashSet<int> _windowsKeysDown = [];
    private readonly HashSet<int> _swallowedChordKeys = [];
    private bool _chord;

    public WindowsKeyDecision Handle(int key, bool down, bool windowsKey, bool modifiersDown)
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
            if (_windowsKeysDown.Count != 0)
                return new(true, false);
            var invokeHome = !_chord;
            _chord = false;
            return new(true, invokeHome);
        }

        if (_windowsKeysDown.Count > 0)
        {
            _chord = true;
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
