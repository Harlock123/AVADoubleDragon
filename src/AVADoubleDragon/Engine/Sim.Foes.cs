namespace AVADoubleDragon.Engine;

/// <summary>The Black Talons: how each of them goes about a fight.</summary>
public static partial class Sim
{
    public const double ChargeSpeed = 290, HeadbuttSpeed = 330, BulletSpeed = 560, AimTime = 0.7, ShotEvery = 0.14;
    public const int HeadbuttHarm = 10, ChargeHarm = 12;

    /// <summary>How fast each walks, along the street and across it.</summary>
    public static (double X, double Y) Pace(Kind k) => k switch
    {
        Kind.Dutch => (95, 60), Kind.Rooker => (88, 56), Kind.Vesper => (115, 70), Kind.Slade => (100, 64), Kind.Fuse => (110, 70),
        Kind.Goliath => (72, 46), Kind.Magnus => (80, 54), _ => (WalkX, WalkY),
    };

    /// <summary>How far from his man he stands to strike: further with a bat, further still with the whip.</summary>
    public static double Reach(World w, Fighter f) => w.Carried(f)?.Kind switch
    {
        ItemKind.Whip => 112,
        ItemKind.Bat => 84,
        _ => f.Kind switch { Kind.Goliath => 66, Kind.Magnus => 64, Kind.Vesper => 66, _ => 56 },
    };

    /// <summary>The gang grow keener mission by mission: the pause between their blows.</summary>
    private static double Temper(World w) => 1.12 - 0.06 * w.Mission;

    private static void Foe(World w, Fighter f, double dt)
    {
        f.Cool -= dt;
        f.Think -= dt;
        if (f.Act == Act.Hold)
        {
            // A brother held by the hair: two knees to the face, then thrown.
            if (f.Cool > 0) return;
            if (f.Knees < 2) { Begin(w, f, Act.Knee); f.Cool = 0.6; }
            else Begin(w, f, Act.Throw);
            return;
        }
        if (f.Act is not (Act.Stand or Act.Walk)) return;
        var t = Mark(w, f);
        if (t == null) { f.Act = Act.Stand; return; }
        var carry = w.Carried(f);
        double dx = t.X - f.X, dy = t.Y - f.Y, ad = Math.Abs(dx);
        int toward = dx < 0 ? -1 : 1;
        bool lined = Math.Abs(dy) < 10, seen = f.X > w.Cam + 16 && f.X < w.Cam + Field.Width - 16, ready = f.Cool <= 0 && seen && t.Open;
        double reach = Reach(w, f);

        // A weapon lying near, and nobody close enough to fight first: go and get it.
        if (carry == null && ad > 130 && Wanted(w, f) is { } item)
        {
            if (Math.Abs(item.X - f.X) < 22 && Math.Abs(item.Y - f.Y) < 12) { PickUp(w, f, item); return; }
            Steer(w, f, item.X, item.Y, dt);
            return;
        }
        // A dazed brother within reach: by the hair.
        if (f.Kind is Kind.Dutch or Kind.Rooker or Kind.Slade && t.Act == Act.Dazed && t.Partner < 0 && lined && ad < GrabReach + 4)
        {
            f.Facing = toward;
            Grab(w, f, t);
            return;
        }
        switch (f.Kind)
        {
            case Kind.Fuse:
                // Keeps his distance and lobs dynamite; swings a feeble fist if cornered.
                f.Facing = toward;
                if (ready && Math.Abs(dy) < 40 && ad is > 140 and < 470)
                {
                    w.Items.Add(new Item { Id = w.NextId++, Kind = ItemKind.Dynamite, Holder = f.Id, X = f.X, Y = f.Y, Z = 60 });
                    f.Range = ad;
                    Begin(w, f, Act.Toss);
                    f.Cool = w.Dice.Range(2.3, 3.2) * Temper(w);
                    return;
                }
                if (ready && lined && ad < 64) { Attack(w, f, Act.Punch); return; }
                double fx = Math.Clamp(t.X + f.Side * 320, w.Cam + 40, w.Cam + Field.Width - 40);
                // Backed into a corner, too close to throw: he has to come in with his fists.
                if (Math.Abs(fx - t.X) < 150) { f.Side = -f.Side; fx = Math.Clamp(t.X + f.Side * 320, w.Cam + 40, w.Cam + Field.Width - 40); }
                if (Math.Abs(fx - t.X) < 150) fx = t.X - toward * 52;
                Steer(w, f, fx, Math.Clamp(t.Y + (f.Id % 2 == 0 ? 14 : -14), Field.Back, Field.Front), dt, toward);
                return;
            case Kind.Magnus:
                // Keeps his distance, lines up with a brother, levels the gun and fires a burst along the line. Close in, the rifle butt.
                f.Facing = toward;
                if (ready && lined && ad < 92) { Attack(w, f, Act.Punch); return; }
                if (ready && Math.Abs(dy) < 6 && ad is > 170 and < 640)
                {
                    Begin(w, f, Act.Aim);
                    w.Heard.Add(Sfx.Cock);
                    f.Cool = w.Dice.Range(1.9, 2.7);
                    return;
                }
                Steer(w, f, t.X + f.Side * 330, t.Y, dt, toward);
                return;
            case Kind.Goliath when ready && lined && ad is > 170 and < 460 && carry == null && w.Dice.Next() < 0.03:
                f.Facing = toward;
                Begin(w, f, Act.Charge);
                f.Vx = toward * ChargeSpeed;
                w.Heard.Add(Sfx.Charge);
                f.Cool = w.Dice.Range(1.6, 2.4) * Temper(w);
                return;
            case Kind.Slade when ready && lined && ad is > 90 and < 200 && w.Dice.Next() < 0.04:
                f.Facing = toward;
                Begin(w, f, Act.Headbutt);
                f.Vx = toward * HeadbuttSpeed;
                f.Cool = w.Dice.Range(1.2, 2.0) * Temper(w);
                return;
        }
        // Thrown weapons: a knife from a distance, a drum or a crate down the line.
        if (carry is { Swung: false } && ready && lined && (carry.Kind == ItemKind.Knife ? ad is > 120 and < 470 : ad < 420))
        {
            f.Facing = toward;
            Begin(w, f, Act.Toss);
            f.Cool = w.Dice.Range(1.0, 1.8) * Temper(w);
            return;
        }
        if (ready && lined && ad > reach - 30 && ad < reach + 10 && carry is not { Swung: false })
        {
            f.Facing = toward;
            if (carry is { Swung: true }) Begin(w, f, Act.Swing);
            else Attack(w, f, w.Dice.Next() < 0.6 ? Act.Punch : Act.Kick);
            f.Cool = w.Dice.Range(0.8, 1.6) * Temper(w);
            return;
        }
        // Into place beside his man, on his line.
        double gx = t.X + f.Side * (carry is { Swung: false } ? 260 : reach - 6);
        if (gx < w.Cam + 40 && f.X > w.Cam || gx > w.Cam + Field.Width - 40 && f.X < w.Cam + Field.Width) { f.Side = -f.Side; gx = t.X + f.Side * (reach - 6); }
        Steer(w, f, gx, t.Y, dt, toward);
    }

    private static void Attack(World w, Fighter f, Act act)
    {
        Begin(w, f, act);
        f.Cool = w.Dice.Range(0.8, 1.6) * Temper(w);
    }

    /// <summary>The brother he's after: the nearest one in the fight, looked at again now and then. He keeps to the side he's on, or to the other if it's crowded.</summary>
    private static Fighter? Mark(World w, Fighter f)
    {
        var t = w.Get(f.Target);
        bool fit = t is { Joined: true, Wait: <= 0 } && t.Active && t.Act is not (Act.Fall or Act.Climb);
        if (fit && f.Think > 0) return t;
        f.Think = w.Dice.Range(0.8, 1.4);
        Fighter? best = null;
        double bestD = double.MaxValue;
        foreach (var b in w.Playing)
        {
            if (b.Wait > 0 || b.Act is Act.Fall or Act.Climb || b.Ko) continue;
            double d = Math.Abs(b.X - f.X) + 2 * Math.Abs(b.Y - f.Y);
            if (d < bestD) { bestD = d; best = b; }
        }
        if (best == null) return null;
        f.Target = best.Id;
        f.Side = f.X < best.X ? -1 : 1;
        if (f.Kind == Kind.Magnus) f.Side = best.X < w.Cam + Field.Width / 2 ? 1 : -1;
        int crowd = w.Foes.Count(o => o != f && o.Active && o.Target == best.Id && Math.Sign(o.X - best.X) == f.Side && Math.Abs(o.X - best.X) < Math.Abs(f.X - best.X));
        if (crowd > 0 && w.Dice.Next() < 0.6) f.Side = -f.Side;
        return best;
    }

    /// <summary>A weapon worth fetching, close by: a bat or knife for a punk, anything for the dock hand, a drum or crate for the giant, her whip for the whip woman.</summary>
    private static Item? Wanted(World w, Fighter f)
    {
        foreach (var i in w.Items)
        {
            if (!i.Loose || Math.Abs(i.X - f.X) > 240 || Math.Abs(i.Y - f.Y) > 120 || i.X < w.Cam + 20 || i.X > w.Cam + Field.Width - 20) continue;
            bool want = f.Kind switch
            {
                Kind.Dutch => i.Kind is ItemKind.Bat or ItemKind.Knife,
                Kind.Rooker => i.Kind is not (ItemKind.Whip or ItemKind.Dynamite),
                Kind.Goliath => i.Heavy,
                Kind.Vesper => i.Kind == ItemKind.Whip,
                _ => false,
            };
            if (want && w.Brothers.All(b => !b.Joined || Math.Abs(b.X - i.X) > 60 || Math.Abs(b.Y - i.Y) > 30)) return i;
        }
        return null;
    }

    /// <summary>
    /// A step towards a place, along and across the street, never over an edge: around the gorge by its bridge, and
    /// over the bridge's broken middle with a jump.
    /// </summary>
    private static void Steer(World w, Fighter f, double gx, double gy, double dt, int face = 0)
    {
        var s = w.Stage;
        var (px, py) = Pace(f.Kind);
        if (w.Carried(f) is { Heavy: true }) px *= HeavyPace;
        if (s.Bridge is { } br)
        {
            double mid = (br.Y0 + br.Y1) / 2;
            bool across = f.X < br.X0 + 10 && gx > br.X0 || f.X > br.X1 - 10 && gx < br.X1 || f.X >= br.X0 && f.X <= br.X1;
            if (across)
            {
                gy = mid;
                if (Math.Abs(f.Y - mid) > 8 && (f.X > br.X0 - 40 && f.X < br.X1 + 40)) gx = f.X < br.X0 ? Math.Min(gx, br.X0 - 30) : f.X > br.X1 ? Math.Max(gx, br.X1 + 30) : f.X;
            }
        }
        double mx = Math.Clamp(gx - f.X, -px * dt, px * dt), my = Math.Clamp(gy - f.Y, -py * dt, py * dt);
        f.Facing = face != 0 ? face : mx < 0 ? -1 : mx > 0 ? 1 : f.Facing;
        if (Math.Abs(mx) < 0.05 && Math.Abs(my) < 0.05) { f.Act = Act.Stand; return; }
        bool Ok(double x, double y) => s.Floor(x, y) && s.Floor(x + Math.Sign(x - f.X) * 16, y + Math.Sign(y - f.Y) * 6);
        if (Ok(f.X + mx, f.Y + my)) { f.X += mx; f.Y += my; }
        else if (s.Bridge is { } b2 && Math.Abs(f.Y - (b2.Y0 + b2.Y1) / 2) < 14 && f.X + Math.Sign(mx) * 40 is var ahead && ahead > b2.Gap0 && ahead < b2.Gap1)
        {
            // The broken middle of the bridge: over it with a jump.
            f.Act = Act.Jump;
            f.T = 0;
            f.Vx = Math.Sign(mx) * JumpX;
            f.Vy = 0;
            f.Vz = JumpSpeed;
            f.Z = 0.01;
            return;
        }
        else if (my != 0 && Ok(f.X, f.Y + my)) f.Y += my;
        else if (mx != 0 && Ok(f.X + mx, f.Y)) f.X += mx;
        f.Act = Act.Walk;
        f.Walk += dt;
        Keep(w, f);
    }

    /// <summary>The rushes: the bald man's headbutt, the giant's charge, the boss's aim and burst.</summary>
    private static void Rush(World w, Fighter f, double dt)
    {
        switch (f.Act)
        {
            case Act.Headbutt or Act.Charge:
            {
                bool butt = f.Act == Act.Headbutt;
                f.X += f.Vx * dt;
                Keep(w, f);
                foreach (var g in w.Everyone.ToList())
                {
                    if (!w.Hostile(f, g) || !g.Open || g.Act == Act.Held || Math.Abs(g.Y - f.Y) > DepthReach + 2 || Math.Abs(g.X - f.X) > 42 || g.Z > 50) continue;
                    Hurt(w, f, g, butt ? HeadbuttHarm : ChargeHarm, true, Math.Sign(f.Vx), butt ? Sfx.Headbutt : Sfx.Slap);
                    f.Act = Act.Stand;
                    f.T = 0;
                    break;
                }
                bool edge = f.X < w.Cam + 24 || f.X > w.Cam + Field.Width - 24;
                if (f.Act != Act.Stand && (f.T > (butt ? 0.45 : 1.2) || edge && f.T > 0.2)) { f.Act = Act.Stand; f.T = 0; }
                f.Vx = f.Act == Act.Stand ? 0 : f.Vx;
                return;
            }
            case Act.Aim:
                if (f.T < AimTime) return;
                f.Act = Act.Shoot;
                f.T = ShotEvery;
                f.Shots = 3;
                return;
            case Act.Shoot:
                if (f.T < ShotEvery) return;
                f.T = 0;
                w.Shots.Add(new Shot { X = f.X + f.Facing * 58, Y = f.Y, Z = 76, Vx = f.Facing * BulletSpeed, Owner = f.Id });
                w.Heard.Add(Sfx.Gun);
                if (--f.Shots <= 0) { f.Act = Act.Stand; f.T = 0; }
                return;
        }
    }
}
