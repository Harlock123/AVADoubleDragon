using Avalonia.Input;

namespace AVADoubleDragon.Engine;

/// <summary>
/// Keyboard and gamepads boiled down to each brother's controls. Both share the keyboard, on keys that never
/// overlap: Jake walks with W A S D, punches with F, kicks with G, elbows with R and jumps with Space (or Left
/// Shift); Cole walks with the arrow keys (or the number pad's 8 4 6 2), punches with J, kicks with K, elbows with I
/// and jumps with L (or Right Shift, or the pad's 0). Kicking with the way he walks held away from where he faces is
/// the back elbow too. Each gamepad is a brother's (the first Jake's, the second Cole's): the left stick or D-pad
/// walks, X punches, B kicks, A jumps and Y elbows. Up is -1, the way the engine counts Y down the screen.
/// </summary>
public sealed class InputHandler : IDisposable
{
    private readonly List<Key> _held = [];
    private readonly Dictionary<PhysicalKey, Key> _pressedAs = [];
    private readonly ControllerManager[] _pads = [new(0), new(1)];

    public void OnKeyDown(Key key, PhysicalKey physical = PhysicalKey.None)
    {
        if (physical != PhysicalKey.None)
        {
            if (_pressedAs.TryGetValue(physical, out var was) && was != key) _held.Remove(was);
            _pressedAs[physical] = key;
        }
        _held.Remove(key);
        _held.Add(key);
    }

    /// <summary>A key comes up. The key it went down as is let go too: with the keyboard option
    /// shift:both_capslock_cancel, letting go of a Shift arrives as CapsLock, and the Shift would stay held.</summary>
    public void OnKeyUp(Key key, PhysicalKey physical = PhysicalKey.None)
    {
        _held.Remove(key);
        if (physical != PhysicalKey.None && _pressedAs.Remove(physical, out var was)) _held.Remove(was);
    }

    public bool Has(Key key) => _held.Contains(key);

    public void Update()
    {
        foreach (var pad in _pads) pad.Update();
    }

    /// <summary>One brother's keys.</summary>
    private sealed record Keys(Key[] Left, Key[] Right, Key[] Up, Key[] Down, Key[] Punch, Key[] Kick, Key[] Jump, Key[] Elbow);

    private static readonly Keys[] Layout =
    [
        new([Key.A], [Key.D], [Key.W], [Key.S], [Key.F], [Key.G], [Key.Space, Key.LeftShift], [Key.R]),
        new([Key.Left, Key.NumPad4], [Key.Right, Key.NumPad6], [Key.Up, Key.NumPad8], [Key.Down, Key.NumPad2],
            [Key.J], [Key.K], [Key.L, Key.RightShift, Key.NumPad0], [Key.I]),
    ];

    /// <summary>The last-pressed of the keys held, as -1 or +1 (0 for neither).</summary>
    private int Axis(Key[] minus, Key[] plus)
    {
        for (int i = _held.Count - 1; i >= 0; i--)
        {
            if (minus.Contains(_held[i])) return -1;
            if (plus.Contains(_held[i])) return 1;
        }
        return 0;
    }

    private bool Any(Key[] keys) => keys.Any(_held.Contains);

    /// <summary>Brother 0's or 1's controls, from his keys and his gamepad.</summary>
    public Controls For(int player)
    {
        var k = Layout[player];
        var pad = _pads[player];
        int dx = Axis(k.Left, k.Right), dy = Axis(k.Up, k.Down);
        if (dx == 0) dx = pad.DPadLeft || pad.LeftStickX < -0.4f ? -1 : pad.DPadRight || pad.LeftStickX > 0.4f ? 1 : 0;
        if (dy == 0) dy = pad.DPadUp || pad.LeftStickY < -0.4f ? -1 : pad.DPadDown || pad.LeftStickY > 0.4f ? 1 : 0;
        return new Controls(dx, dy, Any(k.Punch) || pad.ButtonX, Any(k.Kick) || pad.ButtonB, Any(k.Jump) || pad.ButtonA, Any(k.Elbow) || pad.ButtonY);
    }

    public Controls Player1 => For(0);
    public Controls Player2 => For(1);

    /// <summary>Jake's start: Enter or 1, or the first pad's Start.</summary>
    public bool Start1 => Has(Key.Enter) || Has(Key.D1) || Has(Key.NumPad1) || _pads[0].ButtonStart;
    /// <summary>Cole's start (or join, or back in): 2, or the second pad's Start.</summary>
    public bool Start2 => Has(Key.D2) || _pads[1].ButtonStart;

    private ControllerManager Pad => _pads[0];
    public bool PadUp => Pad.DPadUp || Pad.LeftStickY < -0.5f;
    public bool PadDown => Pad.DPadDown || Pad.LeftStickY > 0.5f;
    public bool PadLeft => Pad.DPadLeft || Pad.LeftStickX < -0.5f;
    public bool PadRight => Pad.DPadRight || Pad.LeftStickX > 0.5f;
    public bool PadAccept => Pad.ButtonA || Pad.ButtonX || Pad.ButtonB;
    public bool PadDone => Pad.ButtonStart;
    /// <summary>A pad's Start (to pause, when its brother is in the game).</summary>
    public bool PadPause(int player) => _pads[player].ButtonStart;
    public bool PadBack => _pads[0].ButtonBack || _pads[1].ButtonBack;

    public void Dispose()
    {
        foreach (var pad in _pads) pad.Dispose();
    }
}
