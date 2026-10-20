namespace AVADoubleDragon.Engine;

/// <summary>
/// The computer brother, for the demonstration and the tests. It fights the way a good player does: it lines up on the
/// nearest of the gang's depth line and stands off at a kick's length, punches close and kicks a little further,
/// elbows anyone behind it, grabs a dazed one by the hair and knees him, and jump-kicks the giants. It steps off the
/// line of a bullet, a charge, a thrown drum or a knife, backs away from lit dynamite, waits out the hideout's spikes
/// (or walks along the front of the floor, clear of them), crosses the gorge by the bridge with a jump over its broken
/// middle, picks up a bat or a whip when one lies near, and climbs the factory's ladder.
/// </summary>
public sealed class Pilot
{
    private bool _punch, _kick, _jump, _elbow;

    public string Doing { get; private set; } = "";

    public void Reset()
    {
        _punch = _kick = _jump = _elbow = false;
        Doing = "";
    }

    public Controls Decide(GameEngine g, int i) => g.Phase is Phase.Play or Phase.Duel ? Decide(g.World, i) : default;

    public Controls Decide(World w, int i)
    {
        var b = w.Brothers[i];
        if (!b.Joined || b.Act == Act.Out || b.Wait > 0 || w.Cleared || w.Ending) return Pulse(default);
        return Pulse(Think(w, b));
    }

    /// <summary>A button wanted twice running is let go in between, so each wanted press is a press.</summary>
    private Controls Pulse(Controls c)
    {
        c = c with { Punch = c.Punch && !_punch, Kick = c.Kick && !_kick, Jump = c.Jump && !_jump, Elbow = c.Elbow && !_elbow };
        _punch = c.Punch;
        _kick = c.Kick;
        _jump = c.Jump;
        _elbow = c.Elbow;
        return c;
    }

    private Controls Think(World w, Fighter b)
    {
        switch (b.Act)
        {
            case Act.Held:
                Doing = "struggling";
                return new Controls(Dx: (int)(w.Clock * 12) % 2 == 0 ? -b.Facing : 0, Punch: true, Kick: true);
            case Act.Hold:
            {
                // Knees, unless there's one of them behind to throw him onto.
                bool behind = w.Everyone.Any(f => w.Hostile(b, f) && f.Open && f.Partner != b.Id && Math.Abs(f.Y - b.Y) < 20 && (f.X - b.X) * b.Facing is < 0 and > -110);
                Doing = behind ? "throwing" : "kneeing";
                return behind ? new Controls(Jump: true) : new Controls(Kick: true);
            }
            case Act.Jump:
                Doing = "jumping";
                return new Controls(Kick: Nearest(w, b, f => f.Open && Math.Abs(f.Y - b.Y) < Sim.DepthReach && (f.X - b.X) * b.Facing is > 0 and < Sim.JumpKickReach + 30) != null);
            case Act.Stand or Act.Walk:
                break;
            default:
                return default;
        }
        var s = w.Stage;
        // Danger first: off the line of anything coming along it.
        if (Incoming(w, b) is { } line)
        {
            Doing = "dodging";
            // Whichever way off the line there's room to go, the nearer first.
            bool up = line - 34 >= Field.Back, down = line + 34 <= Field.Front;
            int away = up && (!down || b.Y <= line) ? -1 : 1;
            return Move(w, b, 0, away);
        }
        foreach (var i in w.Items)
            if (i.Lit && Math.Abs(i.X - b.X) < 150 && Math.Abs(i.Y - b.Y) < 80)
            {
                Doing = "away from the dynamite";
                return Move(w, b, b.X < i.X ? -1 : 1, b.Y < i.Y ? -1 : 1);
            }
        var carry = w.Carried(b);
        // The nearest of them; the whip woman and the dynamite man sooner; one his brother is nearer to, later.
        var bro = w.Brothers[1 - b.Id];
        bool helped = !w.Duel && bro.Joined && bro.Active && bro.Wait <= 0;
        double Gap(Fighter x, Fighter f) => Math.Abs(f.X - x.X) + 2 * Math.Abs(f.Y - x.Y);
        var t = Nearest(w, b, f => f.Active && !f.Ko && f.Act != Act.Fall,
            f => (f.Kind is Kind.Vesper or Kind.Fuse ? 60 : f.Act == Act.Dazed ? 80 : 0) - (helped && Gap(bro, f) < Gap(b, f) ? 110 : 0));
        // A weapon near, and no one on top of him: pick it up.
        if (carry == null && (t == null || Math.Abs(t.X - b.X) > 150) && Sim.Pickable(w, b) is { Kind: not (ItemKind.Drum or ItemKind.Box or ItemKind.Dynamite) })
        {
            Doing = "picking up";
            return new Controls(Punch: true);
        }
        if (carry == null && (t == null || Math.Abs(t.X - b.X) > 150))
            foreach (var i in w.Items)
            {
                if (!i.Loose || i.Kind is ItemKind.Drum or ItemKind.Box or ItemKind.Dynamite || Math.Abs(i.X - b.X) > 260 || i.X < w.Cam + 30 || i.X > w.Cam + Field.Width - 30) continue;
                if (!s.Floor(i.X, i.Y) || s.Bridge is { } gorge && (i.X < gorge.X1) != (b.X < gorge.X1) && (i.X > gorge.X0) != (b.X > gorge.X0)) continue;
                Doing = "fetching a weapon";
                return Toward(w, b, i.X, i.Y, 6);
            }
        if (t == null) return Onward(w, b);
        double dx = t.X - b.X, dy = t.Y - b.Y, ad = Math.Abs(dx);
        int toward = dx < 0 ? -1 : 1;
        bool lined = Math.Abs(dy) < 11;
        // A dazed one: walk into him to grab him by the hair.
        if (t.Act == Act.Dazed && !t.Big && carry == null && Math.Abs(dy) < 12 && ad < 80)
        {
            Doing = "grabbing";
            return Move(w, b, toward, Math.Abs(dy) > 4 ? Math.Sign(dy) : 0);
        }
        // Anyone in reach on his line gets it first: in front a punch, a kick or a swing; behind, the elbow.
        foreach (var f in w.Everyone)
        {
            if (!w.Hostile(b, f) || !f.Open || f.Act == Act.Held || Math.Abs(f.Y - b.Y) > 11) continue;
            double d = (f.X - b.X) * b.Facing;
            if (d < 0 && d > -(Sim.ElbowReach - 10) && carry is not { Heavy: true }) { Doing = "elbowing"; return new Controls(Elbow: true); }
            if (d < 0 || carry is { Swung: false }) continue;
            double reach = carry?.Kind == ItemKind.Whip ? Sim.WhipReach - 14 : carry?.Kind == ItemKind.Bat ? Sim.BatReach - 12 : Sim.KickReach - 8;
            if (carry != null && d < reach) { Doing = "swinging"; return new Controls(Punch: true); }
            if (f.Big && d is > 70 and < 120 && f.Act is not (Act.Punch or Act.Aim or Act.Shoot)) { Doing = "jump kick"; return new Controls(Dx: b.Facing, Jump: true); }
            if (d < 48) { Doing = "punching"; return new Controls(Punch: true); }
            if (d < reach) { Doing = "kicking"; return new Controls(Kick: true); }
        }
        if (t.Open && lined && carry is { Swung: false } && ad is < 420 and > 40)
        {
            Doing = "throwing";
            return b.Facing == toward ? new Controls(Punch: true) : new Controls(Dx: toward);
        }
        // Into place: at a kick's length on the side he's on, on the same line (the boss's line only at the last moment).
        int side = b.X < t.X ? -1 : 1;
        double stand = carry?.Kind switch { ItemKind.Bat => Sim.BatReach - 24, ItemKind.Whip => Sim.WhipReach - 30, _ => 62 };
        // His brother already there: the other side of the man.
        var other = w.Brothers[1 - b.Id];
        if (!w.Duel && other.Joined && other.Active && other.Wait <= 0 && Math.Abs(other.X - (t.X + side * stand)) < 50 && Math.Abs(other.Y - t.Y) < 24
            && t.X - side * stand > w.Cam + 40 && t.X - side * stand < w.Cam + Field.Width - 40 && w.Stage.Floor(t.X - side * stand, t.Y))
            side = -side;
        double gx = t.X + side * stand, gy = t.Y;
        if (!t.Open) gx = t.X + side * 100;
        if (t.Kind is Kind.Magnus or Kind.Goliath && ad > 130 && t.Open) gy = t.Y + (t.Y < Field.Middle ? 46 : -46);
        Doing = "closing in";
        return Toward(w, b, gx, gy, 4, toward);
    }

    /// <summary>Nobody to fight: on along the street (or to the ladder), or wait in the middle for the next of them.</summary>
    private Controls Onward(World w, Fighter b)
    {
        var s = w.Stage;
        if (s.Ladder is { } lx && w.Wave >= s.Waves.Count)
        {
            Doing = "to the ladder";
            if (Math.Abs(b.X - lx) < 20 && b.Y < Field.Back + 24) return new Controls(Dy: -1);
            return Toward(w, b, lx, Field.Back + 6, 3);
        }
        if (w.Locked || s.Statue is { } st && w.Statue < 1 && b.X > st - 300)
        {
            Doing = "waiting";
            return Toward(w, b, w.Cam + Field.Width * 0.45, Field.Middle, 30);
        }
        Doing = "walking on";
        double gy = Field.Middle + (b.Id == 0 ? -20 : 30);
        return Toward(w, b, b.X + 200, gy, 4);
    }

    /// <summary>The nearest of the other side (in view) that passes a test, some counted nearer than they are.</summary>
    private static Fighter? Nearest(World w, Fighter b, Func<Fighter, bool> ok, Func<Fighter, double>? first = null)
    {
        Fighter? best = null;
        double bestD = double.MaxValue;
        foreach (var f in w.Everyone)
        {
            if (!w.Hostile(b, f) || f.X < w.Cam - 10 || f.X > w.Cam + Field.Width + 10 || !ok(f)) continue;
            double d = Math.Abs(f.X - b.X) + 2 * Math.Abs(f.Y - b.Y) - (first?.Invoke(f) ?? 0);
            if (d < bestD) { bestD = d; best = f; }
        }
        return best;
    }

    /// <summary>The depth line of anything coming at him along it: a bullet, a thrown knife, drum or crate, the boss taking aim, a charge or a headbutt. Null if none.</summary>
    private static double? Incoming(World w, Fighter b)
    {
        foreach (var s in w.Shots)
            if (Math.Abs(s.Y - b.Y) < 26 && (b.X - s.X) * Math.Sign(s.Vx) is > -10 and < 460) return s.Y;
        foreach (var i in w.Items)
            if (i.Flying && !i.Lit && w.Get(i.Thrower) is { } th && w.Hostile(th, b) && Math.Abs(i.Y - b.Y) < 30 && (b.X - i.X) * Math.Sign(i.Vx) is > -10 and < 380) return i.Y;
        foreach (var f in w.Everyone)
        {
            if (!w.Hostile(b, f)) continue;
            int at = Math.Sign(b.X - f.X);
            double d = Math.Abs(b.X - f.X);
            if (f.Act is Act.Aim or Act.Shoot && Math.Abs(f.Y - b.Y) < 30 && f.Facing == at) return f.Y;
            if (f.Act is Act.Charge or Act.Headbutt && Math.Abs(f.Y - b.Y) < 32 && Math.Sign(f.Vx) == at && d < 420) return f.Y;
            if (f.Kind == Kind.Magnus && f.Act is Act.Stand or Act.Walk && f.Cool < 0.4 && Math.Abs(f.Y - b.Y) < 12 && d > 150) return f.Y;
            // The whip woman winding up, from beyond a fist's reach.
            if (f.Act == Act.Swing && f.T < 0.12 && w.Carried(f)?.Kind == ItemKind.Whip && Math.Abs(f.Y - b.Y) < 20 && f.Facing == at && d is > 70 and < 140) return f.Y;
        }
        return null;
    }

    private Controls Toward(World w, Fighter b, double gx, double gy, double slack, int face = 0)
    {
        // Not on top of his brother: a line nearer the middle of the street instead.
        var other = w.Brothers[1 - b.Id];
        if (!w.Duel && other.Joined && other.Active && other.Wait <= 0 && Math.Abs(other.X - gx) < 50 && Math.Abs(other.Y - gy) < 30)
            gy = Math.Clamp(gy + (other.Y > Field.Middle ? -50 : 50), Field.Back, Field.Front);
        var br = w.Stage.Bridge;
        if (br is { } bb)
        {
            double mid = (bb.Y0 + bb.Y1) / 2;
            bool across = b.X < bb.X0 + 10 && gx > bb.X0 || b.X > bb.X1 - 10 && gx < bb.X1 || b.X >= bb.X0 && b.X <= bb.X1;
            if (across)
            {
                // Onto the bridge's line, then along it, and over its broken middle with a jump.
                bool on = b.Y > bb.Y0 + 8 && b.Y < bb.Y1 - 8;
                gy = Math.Clamp(gy, bb.Y0 + 12, bb.Y1 - 12);
                if (!on && b.X > bb.X0 - 60 && b.X < bb.X1 + 60) { gy = mid; gx = b.X < bb.X0 ? Math.Min(gx, bb.X0 - 40) : b.X > bb.X1 ? Math.Max(gx, bb.X1 + 40) : b.X; }
                int way = Math.Sign(gx - b.X);
                bool overGap = way > 0 ? gx > bb.Gap0 : gx < bb.Gap1;
                if (on && overGap && (way > 0 && b.X > bb.Gap0 - 44 && b.X < bb.Gap0 || way < 0 && b.X < bb.Gap1 + 44 && b.X > bb.Gap1))
                    return new Controls(Dx: way, Jump: true);
            }
        }
        int dx = Math.Abs(gx - b.X) > slack ? Math.Sign(gx - b.X) : 0, dy = Math.Abs(gy - b.Y) > 3 ? Math.Sign(gy - b.Y) : 0;
        if (dx == 0 && face != 0 && b.Facing != face && dy == 0) dx = face;
        return Move(w, b, dx, dy);
    }

    /// <summary>A step that keeps out of holes and away from the spikes' thrust: the one wanted if it's safe, otherwise the nearest that is.</summary>
    private static Controls Move(World w, Fighter b, int dx, int dy)
    {
        (int, int)[] tries = [(dx, dy), (dx, 0), (0, dy), (0, 1), (0, -1), (-dx, 0), (0, 0), (-1, 0), (1, 0)];
        foreach (var (x, y) in tries)
        {
            double nx = b.X + x * 22, ny = Math.Clamp(b.Y + y * 14, Field.Back, Field.Front);
            if (y != 0 && x == 0 && Math.Abs(ny - b.Y) < 1) continue;
            if (!w.Stage.Floor(nx, ny) || !w.Stage.Floor(b.X + x * 8, b.Y + y * 5)) continue;
            if (Spiked(w, nx, ny) || x == 0 && y == 0 && Spiked(w, b.X, b.Y)) continue;
            return new Controls(Dx: x, Dy: y);
        }
        return new Controls(Dx: dx, Dy: dy);
    }

    /// <summary>Whether a spiked block will be out over a spot in the next moment.</summary>
    private static bool Spiked(World w, double x, double y)
    {
        foreach (var s in w.Stage.Spikes)
        {
            if (Math.Abs(x - s.X) > Spike.Wide / 2 + 20 || y > Field.Back + s.Reach + 18) continue;
            for (double t = 0; t <= 0.8; t += 0.1)
                if (Stage.Thrust(s, w.Clock + t) > 0.3 && y < Field.Back + s.Reach * Stage.Thrust(s, w.Clock + t) + 18) return true;
        }
        return false;
    }
}
