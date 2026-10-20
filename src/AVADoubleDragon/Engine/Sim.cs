namespace AVADoubleDragon.Engine;

/// <summary>The rules, one 60th of a second at a time.</summary>
public static partial class Sim
{
    public const double WalkX = 150, WalkY = 95, JumpSpeed = 520, Gravity = 1500, JumpX = 170, JumpY = 60, HeavyPace = 0.7;
    public const int BrotherHp = 60, Lives0 = 3, ExtraLifeEvery = 30000;
    /// <summary>How far apart in depth two fighters can be and still reach each other: about one line of the street.</summary>
    public const double DepthReach = 16;
    public const double PunchReach = 64, KickReach = 80, ElbowReach = 64, JumpKickReach = 78, BatReach = 98, WhipReach = 126, GrabReach = 48, HeldGap = 98;
    public const int PunchHarm = 4, KickHarm = 6, ElbowHarm = 7, JumpKickHarm = 10, KneeHarm = 7, SlamHarm = 12, ThrowHarm = 14, ThrownHarm = 8;
    public const int BatHarm = 12, WhipHarm = 8, KnifeHarm = 14, DrumHarm = 16, BoxHarm = 12, BlastHarm = 20, SpikeHarm = 18, BulletHarm = 12;
    public const double DazeTime = 1.4, HoldTime = 2.2, HurtTime = 0.3, DownTime = 1.0, RiseTime = 0.4, SafeAfterRise = 0.9, SafeAfterSpawn = 2.5;
    public const double StunMemory = 1.2, WaitTime = 1.2, FallTime = 0.9, BlastReach = 100, BlastDepth = 56;
    public const double WarnAt = 20;
    /// <summary>The brothers are tougher than the gang: what the gang (or the street) does to them is scaled by this.</summary>
    public const double Toughened = 0.6;
    public const int TimePoints = 50, RubyPoints = 20000, PointsPerHarm = 10;

    public static string BrotherName(int i) => i == 0 ? "JAKE" : "COLE";

    /// <summary>What each of the gang is worth, knocked out.</summary>
    public static int Points(Kind k) => k switch
    {
        Kind.Dutch => 500, Kind.Rooker => 600, Kind.Vesper => 700, Kind.Fuse => 800, Kind.Slade => 1000, Kind.Goliath => 2000, Kind.Magnus => 10000, _ => 0,
    };

    public static int Toughness(Kind k) => k switch
    {
        Kind.Brother => BrotherHp, Kind.Dutch => 30, Kind.Rooker => 36, Kind.Vesper => 26, Kind.Fuse => 24, Kind.Slade => 44, Kind.Goliath => 90, _ => 130,
    };

    public static Fighter NewBrother(int i) => new() { Id = i, Kind = Kind.Brother, Hp = BrotherHp, MaxHp = BrotherHp, NextLife = ExtraLifeEvery, X = 160 + i * 120, Y = Field.Middle + (i == 0 ? -40 : 40) };

    /// <summary>A new game: the first mission, with one brother in it, or both.</summary>
    public static void Start(World w, int brothers = 1)
    {
        w.Brothers = [NewBrother(0), NewBrother(1)];
        StartMission(w, 1);
        for (int i = 0; i < brothers; i++) Join(w, i);
    }

    /// <summary>Into a mission: the street from the start, the brothers in their places with their lives and scores as they were.</summary>
    public static void StartMission(World w, int mission)
    {
        w.Mission = mission;
        w.Stage = Stage.Of(mission);
        w.Foes.Clear();
        w.Items.Clear();
        w.Shots.Clear();
        w.Bursts.Clear();
        w.NextId = 2;
        foreach (var p in w.Stage.Items) w.Items.Add(new Item { Id = w.NextId++, Kind = p.Kind, X = p.X, Y = p.Y });
        w.Wave = w.Spawned = 0;
        w.Locked = false;
        w.WaveT = 0;
        w.Cam = 0;
        w.Clock = 0;
        w.TimeLeft = w.Stage.Time;
        w.Warned = w.Cleared = w.Duel = w.Ending = false;
        w.Garage = w.Statue = 0;
        w.Winner = -1;
        w.EndT = 0;
        w.RubyX = w.Stage.Ruby ?? 0;
        w.RubyY = Field.Back + 26;
        w.Dice = new Dice(1987 + mission * 31);
        foreach (var b in w.Brothers)
        {
            if (!b.Joined) continue;
            if (b.Act == Act.Out) continue;
            Place(w, b, 160 + b.Id * 120, Field.Middle + (b.Id == 0 ? -40 : 40));
        }
    }

    /// <summary>A brother set down on his feet, fresh.</summary>
    private static void Place(World w, Fighter b, double x, double y)
    {
        b.X = x;
        b.Y = y;
        b.Z = b.Vx = b.Vy = b.Vz = 0;
        b.Act = Act.Stand;
        b.T = 0;
        b.Hp = b.MaxHp;
        b.Facing = 1;
        b.Partner = -1;
        b.Ko = b.Missile = false;
        b.Wait = 0;
        b.Stun = 0;
        b.Safe = 0;
        foreach (var i in w.Items) if (i.Holder == b.Id) i.Gone = true;
    }

    /// <summary>
    /// A brother steps in: new to the game with three lives, or back after running out of them with three more and
    /// his score kept. He drops in from above, safe for a moment. False if he's already in.
    /// </summary>
    public static bool Join(World w, int i)
    {
        var b = w.Brothers[i];
        if (b.Joined && b.Act != Act.Out) return false;
        if (b.Joined) b.Continues++;
        else
        {
            b.Score = 0;
            b.NextLife = ExtraLifeEvery;
        }
        b.Joined = true;
        b.Lives = Lives0;
        Respawn(w, b);
        return true;
    }

    /// <summary>Back into the fight from above, near the left of the screen, on solid ground.</summary>
    private static void Respawn(World w, Fighter b)
    {
        double x = Math.Min(w.Cam + 180 + b.Id * 110, w.Stage.Length - 200), y = Field.Middle + (b.Id == 0 ? -30 : 30);
        while (!w.Stage.Floor(x, y) || !w.Stage.Floor(x + 40, y) || !w.Stage.Floor(x - 40, y)) x -= 40;
        if (w.Stage.Statue is { } s && w.Statue < 1) x = Math.Min(x, s - 120);
        Place(w, b, x, y);
        b.Z = 380;
        b.Act = Act.Jump;
        b.Safe = SafeAfterSpawn;
    }

    public static void Step(World w, Controls p1, Controls p2 = default)
    {
        double dt = Field.Step;
        w.Clock += dt;
        for (int i = w.Bursts.Count - 1; i >= 0; i--)
        {
            var b = w.Bursts[i];
            if (b.T + dt > 0.6) w.Bursts.RemoveAt(i);
            else w.Bursts[i] = b with { T = b.T + dt };
        }
        if (w.Ending) { Ending(w, dt); return; }
        if (w.Cleared) return;
        if (!w.Duel) Clock(w, dt);
        Brother(w, w.Brothers[0], p1, dt);
        Brother(w, w.Brothers[1], p2, dt);
        foreach (var f in w.Foes) Foe(w, f, dt);
        foreach (var f in w.Everyone.ToList()) { Acts(w, f, dt); Body(w, f, dt); }
        Items(w, dt);
        Shots(w, dt);
        Spikes(w, dt);
        Belt(w, dt);
        Scroll(w, dt);
        Waves(w, dt);
        Lives(w);
        w.Foes.RemoveAll(f => f.Gone);
        w.Items.RemoveAll(i => i.Gone);
        w.Shots.RemoveAll(s => s.Gone);
        Finish(w);
    }

    // ───────────────────────────── the clock ─────────────────────────────

    private static void Clock(World w, double dt)
    {
        if (!w.Playing.Any(b => b.Wait <= 0)) return;
        w.TimeLeft -= dt;
        if (w.TimeLeft <= WarnAt && !w.Warned) { w.Warned = true; w.Heard.Add(Sfx.TimeWarning); }
        if (w.TimeLeft > 0) return;
        // Time up: every brother in the fight loses a life, and the clock starts again.
        foreach (var b in w.Playing.Where(b => b.Wait <= 0 && !b.Ko).ToList())
        {
            Release(w, b);
            Drop(w, b);
            b.Hp = 0;
            b.Ko = true;
            b.Act = Act.Down;
            b.T = 0;
            b.Z = 0;
            w.Heard.Add(Sfx.Death);
        }
        w.TimeLeft = w.Stage.Time;
        w.Warned = false;
    }

    public static int TimeBonus(World w) => (int)Math.Ceiling(Math.Max(0, w.TimeLeft)) * TimePoints;

    // ───────────────────────────── the brothers ─────────────────────────────

    private static void Brother(World w, Fighter b, Controls c, double dt)
    {
        if (!b.Joined || b.Act == Act.Out) return;
        bool punch = c.Punch && !b.PunchPrev, kick = c.Kick && !b.KickPrev, jump = c.Jump && !b.JumpPrev, elbow = c.Elbow && !b.ElbowPrev;
        b.PunchPrev = c.Punch;
        b.KickPrev = c.Kick;
        b.JumpPrev = c.Jump;
        b.ElbowPrev = c.Elbow;
        if (b.Wait > 0)
        {
            if ((b.Wait -= dt) <= 0) Respawn(w, b);
            return;
        }
        switch (b.Act)
        {
            case Act.Stand or Act.Walk:
                Free(w, b, c, punch, kick, jump, elbow, dt);
                break;
            case Act.Jump:
                if ((punch || kick) && b.T > 0.05) { b.Act = Act.JumpKick; b.Struck = false; w.Heard.Add(Sfx.Swish); }
                break;
            case Act.Hold:
                Holding(w, b, c, punch, kick, jump || elbow);
                break;
            case Act.Held:
                // Struggling free: every press loosens his grip.
                if (punch || kick || jump || elbow || c.Dx != 0 && c.Dx != b.Facing) b.T += 0.12;
                break;
        }
    }

    /// <summary>On his feet with his hands free (or carrying something): walk, punch, kick, elbow, jump, pick up, grab.</summary>
    private static void Free(World w, Fighter b, Controls c, bool punch, bool kick, bool jump, bool elbow, double dt)
    {
        var carry = w.Carried(b);
        if (elbow || kick && c.Dx == -b.Facing && carry is not { Heavy: true }) { Begin(w, b, Act.Elbow); return; }
        if (punch || kick && carry is { Heavy: true })
        {
            if (carry == null && Pickable(w, b) is { } item) { PickUp(w, b, item); return; }
            if (carry is { Swung: true }) { Begin(w, b, Act.Swing); return; }
            if (carry != null) { Begin(w, b, Act.Toss); return; }
            Begin(w, b, Act.Punch);
            return;
        }
        if (kick) { Begin(w, b, Act.Kick); return; }
        if (jump && carry is not { Heavy: true })
        {
            b.Act = Act.Jump;
            b.T = 0;
            b.Vx = c.Dx * JumpX;
            b.Vy = c.Dy * JumpY;
            b.Vz = JumpSpeed;
            b.Z = 0.01;
            if (c.Dx != 0) b.Facing = c.Dx;
            w.Heard.Add(Sfx.Jump);
            return;
        }
        // Up the factory's ladder, once the floor's clear.
        if (w.Stage.Ladder is { } lx && c.Dy < 0 && w.Wave >= w.Stage.Waves.Count && Math.Abs(b.X - lx) < 30 && b.Y < Field.Back + 34)
        {
            Drop(w, b);
            b.Act = Act.Climb;
            b.T = 0;
            b.X = lx;
            b.Y = Field.Back + 2;
            w.Heard.Add(Sfx.Climb);
            return;
        }
        if (c.Dx == 0 && c.Dy == 0) { b.Act = Act.Stand; return; }
        double pace = carry is { Heavy: true } ? HeavyPace : 1;
        if (c.Dx != 0) b.Facing = c.Dx;
        b.X += c.Dx * WalkX * pace * dt;
        b.Y += c.Dy * WalkY * pace * dt;
        b.Walk += dt;
        b.Act = Act.Walk;
        Keep(w, b);
        // Walking into a dazed one of them grabs him by the hair.
        if (c.Dx != 0 && carry == null)
            foreach (var f in w.Everyone)
            {
                if (!w.Hostile(b, f) || f.Act != Act.Dazed || f.Big || f.Partner >= 0 || Math.Abs(f.Y - b.Y) > DepthReach - 2) continue;
                double d = (f.X - b.X) * b.Facing;
                if (d > 0 && d < GrabReach) { Grab(w, b, f); return; }
            }
    }

    /// <summary>Holding one by the hair: kick for a knee to the face, punch for the hair-pull slam, jump (or attack away from him) to throw him over the shoulder.</summary>
    private static void Holding(World w, Fighter b, Controls c, bool punch, bool kick, bool jump)
    {
        if (jump || (punch || kick) && c.Dx == -b.Facing) Begin(w, b, Act.Throw);
        else if (kick) Begin(w, b, Act.Knee);
        else if (punch) Begin(w, b, Act.Slam);
    }

    /// <summary>A brother or one of the gang starts a move.</summary>
    public static void Begin(World w, Fighter f, Act act)
    {
        f.Act = act;
        f.T = 0;
        f.Struck = false;
        if (act is Act.Punch or Act.Kick or Act.Elbow or Act.Swing or Act.Headbutt) w.Heard.Add(Sfx.Swish);
    }

    public static Item? Pickable(World w, Fighter f)
    {
        foreach (var i in w.Items)
            if (i.Loose && Math.Abs(i.X - f.X) < 42 && Math.Abs(i.Y - f.Y) < 20) return i;
        return null;
    }

    public static void PickUp(World w, Fighter f, Item item)
    {
        item.Holder = f.Id;
        item.Flying = false;
        item.Vx = item.Vz = 0;
        w.Heard.Add(Sfx.Pickup);
        if (item.Heavy) Begin(w, f, Act.Lift);
    }

    /// <summary>Lets go of whatever he carries, at his feet.</summary>
    public static void Drop(World w, Fighter f)
    {
        if (w.Carried(f) is not { } i) return;
        i.Holder = -1;
        i.X = f.X + f.Facing * 18;
        i.Y = f.Y + 2;
        i.Z = 0;
        if (!w.Stage.Floor(i.X, i.Y)) i.Gone = true;
    }

    public static void Grab(World w, Fighter holder, Fighter held)
    {
        Drop(w, holder);
        Drop(w, held);
        holder.Act = Act.Hold;
        holder.T = 0;
        holder.Partner = held.Id;
        holder.Knees = 0;
        holder.Cool = 0.35;
        held.Act = Act.Held;
        held.T = 0;
        held.Partner = holder.Id;
        held.Z = 0;
        Hang(holder, held);
        w.Heard.Add(Sfx.Grab);
    }

    /// <summary>The held one bent double in front of his holder.</summary>
    private static void Hang(Fighter holder, Fighter held)
    {
        held.Facing = -holder.Facing;
        held.X = holder.X + holder.Facing * HeldGap;
        held.Y = holder.Y + 0.5;
    }

    /// <summary>Breaks any hold between him and someone: both let go.</summary>
    public static void Release(World w, Fighter f)
    {
        if (f.Partner < 0) return;
        var o = w.Get(f.Partner);
        f.Partner = -1;
        if (o == null || o.Partner != f.Id) return;
        o.Partner = -1;
        if (o.Act is Act.Held or Act.Hold or Act.Knee or Act.Slam or Act.Throw) { o.Act = Act.Stand; o.T = 0; }
    }

    /// <summary>Keeps a fighter on the floor band, and a brother on the screen (and the boss's statue in the way while it stands).</summary>
    private static void Keep(World w, Fighter f)
    {
        f.Y = Math.Clamp(f.Y, Field.Back, Field.Front);
        if (f.IsBrother) f.X = Math.Clamp(f.X, Math.Max(Field.Margin, w.Cam + Field.Margin), Math.Min(w.Stage.Length - Field.Margin, w.Cam + Field.Width - Field.Margin));
        else
        {
            // One of the gang walks in from off the screen; once in, he stays in.
            f.In |= f.X > w.Cam + 24 && f.X < w.Cam + Field.Width - 24;
            f.X = f.In ? Math.Clamp(f.X, w.Cam + 20, w.Cam + Field.Width - 20) : Math.Clamp(f.X, w.Cam - 140, w.Cam + Field.Width + 140);
        }
        if (w.Stage.Statue is { } s && w.Statue < 1) f.X = Math.Min(f.X, s - 70);
    }

    // ───────────────────────────── moves under way ─────────────────────────────

    /// <summary>A move's timing: how long it lasts, and the moment it lands.</summary>
    private static (double Length, double At) Timing(World w, Fighter f) => f.Act switch
    {
        Act.Punch => f.Big ? (0.46, 0.2) : (0.24, 0.08),
        Act.Kick => (0.34, 0.12),
        Act.Elbow => (0.34, 0.1),
        Act.Swing => w.Carried(f)?.Kind == ItemKind.Whip ? (0.5, 0.22) : (0.42, 0.16),
        Act.Toss => (0.32, 0.12),
        Act.Lift => (0.26, 1),
        Act.Knee => (0.3, 0.12),
        Act.Slam => (0.5, 0.26),
        Act.Throw => (0.56, 0.26),
        Act.Hurt => (f.Big ? 0.16 : HurtTime, 1),
        Act.Dazed => (DazeTime, 9),
        _ => (9e9, 9e9),
    };

    private static void Acts(World w, Fighter f, double dt)
    {
        f.Safe -= dt;
        if ((f.StunT -= dt) <= 0) f.Stun = 0;
        if (f.Wait > 0) return;
        double was = f.T;
        f.T += dt;
        var (length, at) = Timing(w, f);
        bool now = was < at && f.T >= at;
        switch (f.Act)
        {
            case Act.Punch when now:
                Strike(w, f, f.Big ? PunchReach + 8 : PunchReach, Harm(f), f.Big, back: false, f.Big ? Sfx.Slap : Sfx.Punch);
                break;
            case Act.Kick when now:
                Strike(w, f, KickReach, f.IsBrother ? KickHarm : Harm(f) + 1, false, back: false, Sfx.Kick);
                break;
            case Act.Elbow when now:
                Strike(w, f, ElbowReach, ElbowHarm, false, back: true, Sfx.Punch);
                break;
            case Act.Swing when now:
                if (w.Carried(f)?.Kind == ItemKind.Whip) Strike(w, f, WhipReach, WhipHarm, false, back: false, Sfx.Whip, all: true);
                else Strike(w, f, BatReach, BatHarm, true, back: false, Sfx.Bat, all: true);
                break;
            case Act.Toss when now:
                Throw(w, f);
                break;
            case Act.Knee or Act.Slam or Act.Throw:
                Grip(w, f, now);
                break;
            case Act.Hold:
                if (w.Get(f.Partner) is not { Act: Act.Held } h || h.Partner != f.Id) { f.Partner = -1; f.Act = Act.Stand; }
                else Hang(f, h);
                break;
            case Act.Held:
                // Long enough held and he wrenches free.
                if (f.T > HoldTime || w.Get(f.Partner) is not { } holder || holder.Partner != f.Id)
                {
                    Release(w, f);
                    f.Act = Act.Stand;
                    f.Safe = 0.4;
                }
                return;
            case Act.Down:
                Lying(w, f);
                return;
            case Act.Rise:
                if (f.T > RiseTime) { f.Act = Act.Stand; f.T = 0; f.Safe = SafeAfterRise; }
                return;
            case Act.Fall:
                if (f.T > FallTime) Fell(w, f);
                return;
            case Act.Climb:
                f.Z += 95 * dt;
                return;
            case Act.Headbutt or Act.Charge or Act.Aim or Act.Shoot:
                Rush(w, f, dt);
                return;
        }
        if (f.T >= length && f.Act is not (Act.Knee or Act.Slam or Act.Throw))
        {
            if (f.Act == Act.Dazed) f.Stun = 0;
            f.Act = Act.Stand;
            f.T = 0;
        }
    }

    /// <summary>The harm of a fist, by who throws it.</summary>
    private static int Harm(Fighter f) => f.Kind switch
    {
        Kind.Brother => PunchHarm, Kind.Rooker or Kind.Slade => 6, Kind.Goliath => 10, Kind.Magnus => 10, Kind.Fuse => 4, _ => 5,
    };

    /// <summary>The moves on a held man: a knee, the hair-pull slam, the over-the-shoulder throw.</summary>
    private static void Grip(World w, Fighter f, bool now)
    {
        var h = w.Get(f.Partner);
        var (length, _) = Timing(w, f);
        if (h == null || h.Act != Act.Held || h.Partner != f.Id)
        {
            // Let go (thrown, slammed, or wrenched free): the move plays out, then he's on his feet.
            f.Partner = -1;
            if (f.T >= length || f.Act == Act.Knee && f.T > 0.3) { f.Act = Act.Stand; f.T = 0; }
            return;
        }
        Hang(f, h);
        if (now)
        {
            switch (f.Act)
            {
                case Act.Knee:
                    f.Knees++;
                    h.T = Math.Max(0, h.T - 0.5);
                    if (f.Knees >= 3) { Release(w, f); Hurt(w, f, h, KneeHarm, true, f.Facing, Sfx.Knee); }
                    else Hurt(w, f, h, KneeHarm, false, f.Facing, Sfx.Knee, held: true);
                    break;
                case Act.Slam:
                    // The hair-pull: yanked down by the hair and slammed face first into the street in front.
                    Release(w, f);
                    Hurt(w, f, h, SlamHarm, false, f.Facing, Sfx.Slam, held: true);
                    if (h.Hp > 0) Knock(w, h, f.Facing, false, vx: 90, vz: 160);
                    break;
                case Act.Throw:
                    // Over the shoulder: he sails over and lands hard behind, flattening anyone there.
                    Release(w, f);
                    h.X = f.X - f.Facing * 10;
                    Hurt(w, f, h, ThrowHarm, false, -f.Facing, Sfx.Throw, held: true);
                    if (h.Hp > 0) Knock(w, h, -f.Facing, false, missile: true, vx: 300, vz: 430);
                    else h.Missile = true;
                    h.LastBy = f.Id;
                    break;
            }
        }
        if (f.T >= length && f.Act == Act.Knee) { f.Act = Act.Hold; f.T = 0; }
    }

    /// <summary>On the ground: a moment, then up again with a moment's grace; or, knocked out, gone (a brother loses a life).</summary>
    private static void Lying(World w, Fighter f)
    {
        if (!f.Ko)
        {
            if (f.T > DownTime) { f.Act = Act.Rise; f.T = 0; }
            return;
        }
        if (f.IsBrother)
        {
            if (w.Duel || f.T < 1.4) return;
            LoseLife(w, f);
        }
        else if (f.T > 1.6) f.Gone = true;
    }

    /// <summary>Over the edge and gone: a brother loses a life, one of the gang is out (his points to whoever put him there).</summary>
    private static void Fell(World w, Fighter f)
    {
        if (f.IsBrother) { f.Hp = 0; LoseLife(w, f); return; }
        if (!f.Ko) Award(w, f);
        f.Gone = true;
    }

    private static void LoseLife(World w, Fighter b)
    {
        b.Lives--;
        Release(w, b);
        Drop(w, b);
        if (b.Lives > 0)
        {
            b.Wait = WaitTime;
            b.Act = Act.Stand;
            b.Ko = false;
            b.Z = 0;
            return;
        }
        b.Lives = 0;
        b.Act = Act.Out;
        b.Ko = false;
    }

    /// <summary>Airborne bodies, falls, and holes underfoot.</summary>
    private static void Body(World w, Fighter f, double dt)
    {
        if (f.Gone || f.Wait > 0 || f.Act == Act.Out) return;
        if (f.Act == Act.Fall)
        {
            f.Vz -= Gravity * dt;
            f.Z += f.Vz * dt;
            f.X += f.Vx * dt * 0.3;
            return;
        }
        if (f.Airborne)
        {
            f.X += f.Vx * dt;
            f.Y += f.Vy * dt;
            f.Vz -= Gravity * dt;
            f.Z += f.Vz * dt;
            Keep(w, f);
            if (f.Act == Act.JumpKick && !f.Struck) Strike(w, f, JumpKickReach, JumpKickHarm, true, back: false, Sfx.Kick);
            if (f.Missile) Flatten(w, f);
            if (f.Z > 0) return;
            f.Z = 0;
            if (!w.Stage.Floor(f.X, f.Y)) { StartFall(w, f); return; }
            if (f.Act == Act.Fly)
            {
                f.Act = Act.Down;
                f.T = 0;
                f.Missile = false;
                w.Heard.Add(Sfx.BodySlam);
                w.Bursts.Add(new Burst(f.X, f.Y, 0, 2));
            }
            else { f.Act = Act.Stand; f.T = 0; }
            f.Vx = f.Vy = f.Vz = 0;
            return;
        }
        if (f.Act is Act.Held or Act.Climb) return;
        if (f.Z <= 0 && !w.Stage.Floor(f.X, f.Y)) StartFall(w, f);
    }

    /// <summary>A thrown body bowling over anyone it comes down on.</summary>
    private static void Flatten(World w, Fighter f)
    {
        var by = w.Get(f.LastBy);
        foreach (var g in w.Everyone)
        {
            if (g == f || !g.Open || g.Z > 40 || f.Z > 90 || Math.Abs(g.Y - f.Y) > DepthReach + 4 || Math.Abs(g.X - f.X) > 36) continue;
            if (by != null ? !w.Hostile(by, g) : g.IsBrother == f.IsBrother) continue;
            Hurt(w, by, g, ThrownHarm, true, Math.Sign(f.Vx), Sfx.BodySlam);
        }
    }

    public static void StartFall(World w, Fighter f)
    {
        Release(w, f);
        if (w.Carried(f) is { } i) i.Gone = true;
        f.Act = Act.Fall;
        f.T = 0;
        f.Vz = Math.Min(0, f.Vz);
        f.Missile = false;
        w.Heard.Add(Sfx.Fall);
    }

    // ───────────────────────────── blows ─────────────────────────────

    /// <summary>
    /// A blow reaching out in front (or behind): it lands on the nearest of the other side who's on this line of the
    /// street, within reach, and at the height it's thrown; a swung weapon lands on everyone it sweeps.
    /// </summary>
    public static bool Strike(World w, Fighter f, double reach, int harm, bool knock, bool back, Sfx sound, bool all = false)
    {
        int dir = back ? -f.Facing : f.Facing;
        Fighter? best = null;
        double bestD = double.MaxValue;
        bool any = false;
        foreach (var g in w.Everyone.ToList())
        {
            if (!w.Hostile(f, g) || !g.Open || g.Act == Act.Held && !all) continue;
            if (!InLine(f, g, reach, dir)) continue;
            if (all) { any |= Hurt(w, f, g, harm, knock, dir, sound); continue; }
            double d = Math.Abs(g.X - f.X);
            if (d < bestD) { bestD = d; best = g; }
        }
        if (best != null) any = Hurt(w, f, best, harm, knock, dir, sound);
        if (any) f.Struck = true;
        return any;
    }

    /// <summary>The depth-line rule: on (about) the same line, in front within reach, and at a height the blow can meet.</summary>
    public static bool InLine(Fighter f, Fighter g, double reach, int dir)
    {
        if (Math.Abs(g.Y - f.Y) > DepthReach) return false;
        double d = (g.X - f.X) * dir;
        if (d < -12 || d > reach) return false;
        return f.Z > 20 ? g.Z < f.Z + 70 && f.Z < g.Z + 110 : g.Z < 56;
    }

    /// <summary>
    /// A blow lands: the harm, the points, and how he takes it: rocked; dazed by a third quick blow (then any blow knocks
    /// him down); or knocked flying. Out of strength, he's knocked out. The giants are never dazed; it takes six
    /// quick blows to floor one without a heavy hit. False if he couldn't be hit.
    /// </summary>
    public static bool Hurt(World w, Fighter? by, Fighter g, int harm, bool knock, int dir, Sfx sound, bool held = false)
    {
        if (!g.Open && !(held && g.Act == Act.Held)) return false;
        if (dir == 0) dir = by != null && by.X > g.X ? -1 : 1;
        if (!held) Release(w, g);
        if (g.IsBrother && !w.Duel) harm = Math.Max(1, (int)Math.Round(harm * Toughened));
        g.Hp -= harm;
        if (by != null) g.LastBy = by.Id;
        if (by is { IsBrother: true } && !g.IsBrother) by.Score += harm * PointsPerHarm;
        w.Heard.Add(sound);
        w.Heard.Add(g.IsBrother ? Sfx.Hurt : Sfx.Grunt);
        w.Bursts.Add(new Burst(g.X - dir * 6, g.Y - g.Z - (g.Act == Act.Held ? 60 : 92), 0, 0));
        g.StunT = StunMemory;
        g.Struck = true;
        if (g.Hp <= 0)
        {
            g.Hp = 0;
            Knock(w, g, dir, true);
            if (g.IsBrother) w.Heard.Add(Sfx.Death);
            else Award(w, g);
            return true;
        }
        if (knock) { Knock(w, g, dir, false); return true; }
        if (held) return true;
        if (g.Act == Act.Dazed) { Knock(w, g, dir, false); return true; }
        g.Stun++;
        if (g.Big)
        {
            if (g.Stun >= 6) Knock(w, g, dir, false);
            else if (g.Act is not (Act.Charge or Act.Aim or Act.Shoot)) { g.Act = Act.Hurt; g.T = 0; }
            return true;
        }
        if (g.Stun >= 3)
        {
            Drop(w, g);
            g.Act = Act.Dazed;
            g.Stun = 0;
        }
        else g.Act = Act.Hurt;
        g.T = 0;
        g.X += dir * 6;
        g.Vz = 0;
        return true;
    }

    /// <summary>The knockout points, to the brother who did it (none in the duel).</summary>
    private static void Award(World w, Fighter g)
    {
        if (g.IsBrother) return;
        w.Heard.Add(Sfx.Yell);
        if (w.Get(g.LastBy) is { IsBrother: true } b) b.Score += Points(g.Kind);
    }

    /// <summary>Knocked off his feet: flying back, to land flat. Whatever he held falls from his hands.</summary>
    public static void Knock(World w, Fighter g, int dir, bool ko, bool missile = false, double vx = 230, double vz = 330)
    {
        Release(w, g);
        Drop(w, g);
        g.Act = Act.Fly;
        g.T = 0;
        g.Vx = dir * vx;
        g.Vy = 0;
        g.Vz = vz;
        g.Z = Math.Max(g.Z, 1);
        g.Ko |= ko;
        g.Missile = missile;
        g.Stun = 0;
        g.Facing = -dir;
    }

    // ───────────────────────────── weapons ─────────────────────────────

    /// <summary>What he carries, thrown: a knife flat and fast, a drum or a crate in a low arc, dynamite lobbed and lit.</summary>
    private static void Throw(World w, Fighter f)
    {
        if (w.Carried(f) is not { } i) return;
        i.Holder = -1;
        i.Thrower = f.Id;
        i.Flying = true;
        i.Spin = 0;
        i.X = f.X + f.Facing * 30;
        i.Y = f.Y;
        switch (i.Kind)
        {
            case ItemKind.Knife:
                i.Z = 76;
                i.Vx = f.Facing * 620;
                i.Vz = 0;
                w.Heard.Add(Sfx.KnifeThrow);
                break;
            case ItemKind.Dynamite:
                i.Z = 84;
                i.Vz = 330;
                i.Vx = f.Facing * Math.Clamp((f.Range > 0 ? f.Range : DynamiteRange) / 0.62, 120, 540);
                i.Lit = true;
                i.Fuse = 1.3;
                w.Heard.Add(Sfx.Toss);
                break;
            default:
                i.Z = 104;
                i.Vx = f.Facing * 430;
                i.Vz = 140;
                w.Heard.Add(Sfx.Toss);
                break;
        }
    }

    /// <summary>For a brother, how far a stick of dynamite is lobbed.</summary>
    public const double DynamiteRange = 190;

    private static void Items(World w, double dt)
    {
        foreach (var i in w.Items)
        {
            if (i.Gone) continue;
            if (i.Holder >= 0)
            {
                var h = w.Get(i.Holder);
                if (h == null || h.Gone || !h.Active)
                {
                    i.Holder = -1;
                    i.Z = 0;
                    if (!w.Stage.Floor(i.X, i.Y)) i.Gone = true;
                    continue;
                }
                i.X = h.X + h.Facing * (i.Heavy ? 0 : 16);
                i.Y = h.Y + 1;
                i.Z = h.Z + (i.Heavy ? 128 : 60);
                continue;
            }
            if (i.Blast > 0)
            {
                if ((i.Blast += dt) > 0.5) i.Gone = true;
                continue;
            }
            if (i.Lit && (i.Fuse -= dt) <= 0) { Explode(w, i); continue; }
            if (!i.Flying)
            {
                if (i.Vx != 0)
                {
                    // A drum rolling to a stop.
                    i.X += i.Vx * dt;
                    i.Vx *= Math.Pow(0.05, dt);
                    if (Math.Abs(i.Vx) < 20) i.Vx = 0;
                    if (!w.Stage.Floor(i.X, i.Y)) i.Gone = true;
                }
                continue;
            }
            i.Spin += dt;
            i.X += i.Vx * dt;
            // A knife flies flat a while before it drops.
            if (i.Kind != ItemKind.Knife || i.Spin > 0.8)
            {
                i.Vz -= Gravity * dt;
                i.Z += i.Vz * dt;
            }
            if (i.Kind != ItemKind.Dynamite) Smash(w, i);
            if (i.X < w.Cam - 200 || i.X > w.Cam + Field.Width + 200) { i.Gone = true; continue; }
            if (i.Z > 0 || i.Gone) continue;
            i.Z = 0;
            if (!w.Stage.Floor(i.X, i.Y)) { i.Gone = true; continue; }
            switch (i.Kind)
            {
                case ItemKind.Box:
                    Break(w, i);
                    break;
                case ItemKind.Drum when Math.Abs(i.Vz) > 200:
                    i.Vz = -i.Vz * 0.3;
                    i.Z = 0.1;
                    w.Heard.Add(Sfx.Crash);
                    break;
                case ItemKind.Drum:
                    i.Flying = false;
                    i.Vx *= 0.5;
                    i.Vz = 0;
                    break;
                default:
                    i.Flying = false;
                    i.Vx = i.Vz = 0;
                    break;
            }
        }
    }

    /// <summary>A flying knife, drum or crate meeting someone on its line: it knocks him down (a knife drops there; a crate breaks).</summary>
    private static void Smash(World w, Item i)
    {
        var by = w.Get(i.Thrower);
        foreach (var g in w.Everyone.ToList())
        {
            if (!g.Open || g.Id == i.Thrower || Math.Abs(g.Y - i.Y) > DepthReach + 2 || Math.Abs(g.X - i.X) > 30 || i.Z > g.Z + 140 || i.Z < g.Z - 10) continue;
            if (by == null || !w.Hostile(by, g)) continue;
            int harm = i.Kind switch { ItemKind.Knife => KnifeHarm, ItemKind.Drum => DrumHarm, _ => BoxHarm };
            Hurt(w, by, g, harm, true, Math.Sign(i.Vx), i.Kind == ItemKind.Knife ? Sfx.Stab : Sfx.Crash);
            if (i.Kind == ItemKind.Knife) { i.Flying = false; i.Vx = 0; i.Z = 0; return; }
            if (i.Kind == ItemKind.Box) { Break(w, i); return; }
        }
    }

    private static void Break(World w, Item i)
    {
        i.Gone = true;
        w.Heard.Add(Sfx.Crash);
        w.Bursts.Add(new Burst(i.X, i.Y - i.Z - 20, 0, 3));
    }

    /// <summary>Dynamite goes off: everyone near, on any side, is blown off his feet.</summary>
    public static void Explode(World w, Item i)
    {
        i.Lit = false;
        i.Blast = 0.001;
        i.Flying = false;
        w.Heard.Add(Sfx.Dynamite);
        w.Bursts.Add(new Burst(i.X, i.Y - 30, 0, 1));
        var by = w.Get(i.Thrower);
        foreach (var g in w.Everyone.ToList())
            if (g.Open && Math.Abs(g.X - i.X) < BlastReach && Math.Abs(g.Y - i.Y) < BlastDepth && g.Z < 120)
                Hurt(w, by != null && w.Hostile(by, g) ? by : null, g, BlastHarm, true, g.X < i.X ? -1 : 1, Sfx.Grunt);
    }

    private static void Shots(World w, double dt)
    {
        foreach (var s in w.Shots)
        {
            s.X += s.Vx * dt;
            s.Age += dt;
            if (s.Age > 2.5) s.Gone = true;
            var by = w.Get(s.Owner);
            foreach (var g in w.Everyone)
            {
                if (!g.Open || by == null || !w.Hostile(by, g) || Math.Abs(g.Y - s.Y) > 14 || Math.Abs(g.X - s.X) > 22 || g.Z > 100) continue;
                Hurt(w, by, g, BulletHarm, true, Math.Sign(s.Vx), Sfx.Hurt);
                s.Gone = true;
                break;
            }
        }
    }

    // ───────────────────────────── the street ─────────────────────────────

    /// <summary>The hideout's spiked blocks: a thrust skewers anyone in front of one, as far out as it reaches, and flings him clear.</summary>
    private static void Spikes(World w, double dt)
    {
        foreach (var s in w.Stage.Spikes)
        {
            if (s.X < w.Cam - 200 || s.X > w.Cam + Field.Width + 200) continue;
            double e = Stage.Thrust(s, w.Clock), before = Stage.Thrust(s, w.Clock - dt);
            if (before < 0.3 && e >= 0.3 && s.X > w.Cam - 40 && s.X < w.Cam + Field.Width + 40) w.Heard.Add(Sfx.SpikeWall);
            if (e < 0.5) continue;
            double edge = Field.Back + s.Reach * e;
            foreach (var f in w.Everyone.ToList())
            {
                if (!f.Active || f.Act is Act.Fall or Act.Climb || Math.Abs(f.X - s.X) > Spike.Wide / 2 || f.Y > edge || f.Z > 60) continue;
                if (f.Open)
                {
                    Hurt(w, null, f, SpikeHarm, true, f.X < s.X ? -1 : 1, Sfx.Stab);
                    f.Vy = 280;
                }
                else if (f.Act is Act.Down or Act.Rise) f.Y = Math.Min(Field.Front, edge + 8);
            }
        }
    }

    /// <summary>The factory's conveyor carries anyone standing on it (and anything lying on it) along towards the pit at its end.</summary>
    private static void Belt(World w, double dt)
    {
        if (w.Stage.Belt is not { } b) return;
        bool On(double x, double y) => x >= b.X0 && x <= b.X1 && y >= b.Y0 && y <= b.Y1;
        foreach (var f in w.Everyone)
            if (f.Active && f.Z <= 0 && f.Act is not (Act.Fall or Act.Held or Act.Climb) && On(f.X, f.Y))
            {
                f.X += b.Speed * dt;
                if (w.Get(f.Partner) is { Act: Act.Held } h && h.Partner == f.Id) Hang(f, h);
            }
        foreach (var i in w.Items)
            if (i.Loose && i.Z <= 0 && On(i.X, i.Y))
            {
                i.X += b.Speed * dt;
                if (!w.Stage.Floor(i.X, i.Y)) i.Gone = true;
            }
        if (b.X0 < w.Cam + Field.Width && b.X1 > w.Cam && (int)(w.Clock / 1.4) != (int)((w.Clock - dt) / 1.4)) w.Heard.Add(Sfx.Conveyor);
    }

    /// <summary>
    /// The street scrolls on as the leading brother walks on, but never back, and never so far the trailing one is left
    /// behind, and not past the next fight until it's won (nor past the statue while it stands).
    /// </summary>
    private static void Scroll(World w, double dt)
    {
        var s = w.Stage;
        // The gang before the statue beaten, it sinks into the floor.
        if (s.Statue != null && w.Wave >= s.Waves.Count - 1 && w.Statue < 1)
        {
            if (w.Statue == 0) w.Heard.Add(Sfx.Statue);
            w.Statue = Math.Min(1, w.Statue + dt / 2.2);
        }
        var on = w.Playing.Where(b => b.Wait <= 0 && b.Act is not (Act.Fall or Act.Down) && !b.Ko).ToList();
        if (on.Count == 0) return;
        double lead = on.Max(b => b.X), trail = on.Min(b => b.X);
        double limit = s.Length - Field.Width;
        if (w.Wave < s.Waves.Count) limit = Math.Min(limit, s.Waves[w.Wave].Lock);
        if (s.Statue is { } st && w.Statue < 1) limit = Math.Min(limit, st + 160 - Field.Width);
        double want = Math.Min(lead - Field.Lead, trail - Field.Margin - 30);
        w.Cam = Math.Max(w.Cam, Math.Min(want, limit));
    }

    /// <summary>The gang coming in, a wave at each stop along the street; when every one of a wave is down the street goes on, with the clock set again.</summary>
    private static void Waves(World w, double dt)
    {
        var s = w.Stage;
        if (w.Wave >= s.Waves.Count) return;
        var wave = s.Waves[w.Wave];
        if (!w.Locked)
        {
            if (w.Cam < wave.Lock - 0.5) return;
            if (s.Statue != null && w.Wave == s.Waves.Count - 1 && w.Statue < 1) return;
            w.Locked = true;
            w.WaveT = 0;
            w.Spawned = 0;
            if (wave.Spawns.Any(p => p.Side == 0)) w.Heard.Add(Sfx.Garage);
        }
        w.WaveT += dt;
        if (wave.Spawns.Any(p => p.Side == 0)) w.Garage = Math.Min(1, w.Garage + dt / 1.2);
        int alive = w.Foes.Count(f => !f.Gone && !f.Ko);
        if (w.Spawned < wave.Spawns.Length)
        {
            var next = wave.Spawns[w.Spawned];
            if (w.WaveT >= next.Delay || alive == 0 && w.WaveT > 0.6) { Bring(w, next); w.Spawned++; }
            return;
        }
        if (alive > 0) return;
        w.Wave++;
        w.Locked = false;
        w.TimeLeft = s.Time;
        w.Warned = false;
        if (w.Wave < s.Waves.Count || !s.Final) w.Heard.Add(Sfx.Go);
    }

    /// <summary>One of the gang walks in from the edge of the screen, or out of the garage.</summary>
    public static Fighter Bring(World w, Spawn p)
    {
        var s = w.Stage;
        var f = new Fighter { Id = w.NextId++, Kind = p.Kind, Hp = Toughness(p.Kind), MaxHp = Toughness(p.Kind), Act = Act.Walk };
        if (p.Side == 0 && s.Garage is { } g)
        {
            f.X = g + w.Dice.Range(-40, 40);
            f.Y = Field.Back + 2;
        }
        else
        {
            f.X = p.Side < 0 ? w.Cam - 60 : w.Cam + Field.Width + 60;
            if (s.Statue is { } st && w.Statue < 1) f.X = Math.Min(f.X, st - 90);
            f.Y = w.Dice.Range(Field.Back + 30, Field.Front - 30);
            for (int k = 0; k < 20 && !s.Floor(f.X, f.Y); k++) f.Y = w.Dice.Range(Field.Back + 10, Field.Front - 10);
        }
        f.Facing = p.Side > 0 ? -1 : 1;
        f.Side = p.Side == 0 ? 1 : p.Side;
        f.Cool = w.Dice.Range(0.6, 1.4);
        var carry = p.Carry ?? (p.Kind == Kind.Vesper ? ItemKind.Whip : null);
        if (carry is { } k2) w.Items.Add(new Item { Id = w.NextId++, Kind = k2, Holder = f.Id, X = f.X, Y = f.Y });
        w.Foes.Add(f);
        if (p.Kind == Kind.Magnus) w.Heard.Add(Sfx.Laugh);
        return f;
    }

    /// <summary>Extra lives, and the duel's end.</summary>
    private static void Lives(World w)
    {
        foreach (var b in w.Brothers)
        {
            if (!b.Joined || b.Score < b.NextLife) continue;
            b.NextLife += ExtraLifeEvery;
            if (b.Act == Act.Out) continue;
            b.Lives++;
            w.Heard.Add(Sfx.OneUp);
        }
    }

    /// <summary>The way out: off the right edge, up the ladder, or past the boss to her.</summary>
    private static void Finish(World w)
    {
        var s = w.Stage;
        if (w.Duel)
        {
            if (w.Brothers.FirstOrDefault(b => b.Joined && b.Ko && b.Act == Act.Down && b.T > 1.2) is { } loser)
                Ended(w, 1 - loser.Id);
            return;
        }
        if (w.Wave < s.Waves.Count) return;
        if (s.Final)
        {
            var standing = w.Playing.ToList();
            if (standing.Count == 0) return;
            if (standing.Count == 2) { StartDuel(w); return; }
            if (standing[0].Wait > 0 || standing[0].Airborne) return;
            Ended(w, standing[0].Id);
            return;
        }
        bool over = s.Ladder != null ? w.Brothers.Any(b => b.Act == Act.Climb && b.Z > 230) : w.Playing.Any(b => b.Wait <= 0 && b.X >= s.Exit);
        if (!over) return;
        w.Cleared = true;
        foreach (var b in w.Playing) b.Score += TimeBonus(w);
        w.Heard.Add(Sfx.MissionClear);
    }

    /// <summary>The boss down and both brothers standing: only one of them can have her. They square up; now their blows land on each other.</summary>
    public static void StartDuel(World w)
    {
        w.Duel = true;
        w.Shots.Clear();
        foreach (var i in w.Items) if (i.Flying || i.Lit) i.Gone = true;
        double mid = w.Cam + Field.Width / 2 - 60;
        foreach (var b in w.Playing)
        {
            Release(w, b);
            Drop(w, b);
            Place(w, b, mid + (b.Id == 0 ? -170 : 170), Field.Middle + 10);
            b.Facing = b.Id == 0 ? 1 : -1;
            b.Safe = 1.6;
        }
        w.Heard.Add(Sfx.Duel);
    }

    private static void Ended(World w, int winner)
    {
        w.Ending = true;
        w.Winner = winner;
        w.EndT = 0;
        w.Brothers[winner].Score += RubyPoints;
        w.Heard.Add(Sfx.Victory);
    }

    /// <summary>She runs to the winner.</summary>
    private static void Ending(World w, double dt)
    {
        w.EndT += dt;
        var b = w.Brothers[w.Winner];
        foreach (var f in w.Everyone)
            if (f.Airborne) Body(w, f, dt);
        if (!b.Airborne && b.Act != Act.Down)
        {
            b.Act = Act.Stand;
            b.Facing = w.RubyX > b.X ? 1 : -1;
        }
        if (w.EndT < 1) return;
        double tx = b.X + (w.RubyX > b.X ? 56 : -56), ty = b.Y;
        double dx = tx - w.RubyX, dy = ty - w.RubyY, d = Math.Sqrt(dx * dx + dy * dy);
        if (d > 2) { w.RubyX += dx / d * Math.Min(d, 170 * dt); w.RubyY += dy / d * Math.Min(d, 170 * dt); }
    }

    /// <summary>Whether the girl has reached her man.</summary>
    public static bool Together(World w) => w.Ending && w.Winner >= 0 && Math.Abs(w.RubyX - w.Brothers[w.Winner].X) < 60 && Math.Abs(w.RubyY - w.Brothers[w.Winner].Y) < 6;
}
