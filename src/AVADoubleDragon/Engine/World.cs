namespace AVADoubleDragon.Engine;

/// <summary>
/// The screen and the street. Everyone stands on a floor band seen from the side and a little above: X runs along
/// the street, Y is depth (down the screen is nearer), and Z is height off the ground, drawn as a lift up the screen.
/// Up the screen (a smaller Y) is up, -1, in the controls too.
/// </summary>
public static class Field
{
    public const double Step = 1.0 / 60;
    public const double Width = 1024, Height = 768;
    /// <summary>The floor band: the back of it (against the wall) and the front of it (nearest the player).</summary>
    public const double Back = 452, Front = 700;
    public const double Middle = (Back + Front) / 2;
    /// <summary>How far ahead of the screen's left edge the leading brother is let go before the street scrolls on.</summary>
    public const double Lead = 560;
    /// <summary>How close to the screen's edges the brothers can go.</summary>
    public const double Margin = 26;
}

/// <summary>A small random number generator whose state can be copied.</summary>
public struct Dice
{
    private ulong _s;
    public Dice(int seed) => _s = (ulong)seed * 0x9E3779B97F4A7C15UL + 0x632BE59BD9B4E019UL;

    public double Next()
    {
        _s ^= _s << 13;
        _s ^= _s >> 7;
        _s ^= _s << 17;
        return (_s >> 11) * (1.0 / (1UL << 53));
    }

    public int Next(int n) => (int)(Next() * n);
    public double Range(double a, double b) => a + Next() * (b - a);
}

/// <summary>
/// One brother's controls: along the street (Dx) and up or down it in depth (Dy, up -1); punch, kick and jump; and
/// the back elbow (a gamepad's own button; on the keyboard, kick with the stick held away from where he faces).
/// </summary>
public readonly record struct Controls(int Dx = 0, int Dy = 0, bool Punch = false, bool Kick = false, bool Jump = false, bool Elbow = false);

/// <summary>The brothers, and the Black Talons gang.</summary>
public enum Kind
{
    Brother,
    /// <summary>The street punk: punches and kicks, grabs a dazed brother by the hair, picks up bats and knives.</summary>
    Dutch,
    /// <summary>The dock hand: tougher, lifts oil drums and crates and hurls them.</summary>
    Rooker,
    /// <summary>The whip woman: lashes from beyond a fist's reach.</summary>
    Vesper,
    /// <summary>The bald brawler: dives in with his head down, and grabs.</summary>
    Slade,
    /// <summary>The demolition man: keeps his distance and lobs dynamite.</summary>
    Fuse,
    /// <summary>The giant: slaps, lifts drums, charges along the line, and is far too big to grab.</summary>
    Goliath,
    /// <summary>The Black Talons' boss, with a machine gun.</summary>
    Magnus,
}

/// <summary>What a fighter is doing.</summary>
public enum Act
{
    Stand, Walk, Punch, Kick, Elbow, Jump, JumpKick,
    /// <summary>Rocked by a blow.</summary>
    Hurt,
    /// <summary>Bent over, reeling: ready to be grabbed by the hair.</summary>
    Dazed,
    /// <summary>Knocked off his feet (or thrown), in the air.</summary>
    Fly,
    /// <summary>Flat on the ground.</summary>
    Down,
    /// <summary>Getting up.</summary>
    Rise,
    /// <summary>Holding someone by the hair.</summary>
    Hold,
    /// <summary>Held by the hair.</summary>
    Held,
    /// <summary>A knee to the face of whoever he holds.</summary>
    Knee,
    /// <summary>The hair-pull: yanking him down by the hair and slamming him face first into the street.</summary>
    Slam,
    /// <summary>The over-the-shoulder throw.</summary>
    Throw,
    /// <summary>A swing of a bat or a crack of a whip.</summary>
    Swing,
    /// <summary>Throwing what he carries: a knife, a drum, a crate, a stick of dynamite.</summary>
    Toss,
    /// <summary>Bending to lift something heavy.</summary>
    Lift,
    Headbutt,
    Charge,
    /// <summary>The boss levelling his gun.</summary>
    Aim,
    Shoot,
    /// <summary>Over the edge: falling into the gorge or a pit.</summary>
    Fall,
    /// <summary>Up the ladder, out of the factory.</summary>
    Climb,
    /// <summary>A brother out of lives, waiting for someone to put him back in.</summary>
    Out,
}

/// <summary>A brother or one of the gang: everything about a body on the street, and the brothers' lives and score.</summary>
public sealed class Fighter
{
    public int Id;
    public Kind Kind;
    public double X, Y = Field.Middle, Z, Vx, Vy, Vz, T;
    public int Facing = 1, Hp, MaxHp;
    public Act Act;
    /// <summary>A moment no blow can land: after getting up, or coming back in.</summary>
    public double Safe;
    /// <summary>Blows taken in quick succession (three and he's dazed), and how long since the last.</summary>
    public int Stun;
    public double StunT;
    /// <summary>Whom he holds by the hair, or who holds him (-1 for no one); and the knees given so far.</summary>
    public int Partner = -1, Knees;
    /// <summary>The blow under way has landed (a swing hits once).</summary>
    public bool Struck;
    /// <summary>A flying body that knocks down whoever it lands on: the thrown.</summary>
    public bool Missile;
    /// <summary>Knocked out: this time he stays down.</summary>
    public bool Ko;
    public bool Gone;
    /// <summary>The last to land a blow on him, for the points.</summary>
    public int LastBy = -1;
    public double Walk, Cool, Think;

    // ── a brother's ──
    public bool Joined;
    public int Lives, Score, NextLife, Continues;
    public bool PunchPrev, KickPrev, JumpPrev, ElbowPrev;
    /// <summary>Down and out of this life: the moment before he comes back in.</summary>
    public double Wait;

    // ── one of the gang's ──
    /// <summary>The brother he's after.</summary>
    public int Target = -1;
    /// <summary>Which side of his man he keeps to: -1 left, 1 right.</summary>
    public int Side = 1;
    public int Shots;
    /// <summary>Come in onto the screen (and so kept on it).</summary>
    public bool In;
    /// <summary>How far he means to lob the dynamite.</summary>
    public double Range;

    public Fighter Copy() => (Fighter)MemberwiseClone();
    public bool IsBrother => Kind == Kind.Brother;
    /// <summary>In the fight: a joined brother with a life on, or one of the gang still on his feet or getting up.</summary>
    public bool Active => !Gone && Act != Act.Out && (!IsBrother || Joined) && !(Ko && Act is Act.Down or Act.Fall);
    /// <summary>Can be hit: on his feet, and not just getting up.</summary>
    public bool Open => Active && Safe <= 0 && Act is not (Act.Fly or Act.Down or Act.Rise or Act.Fall or Act.Climb) && !(IsBrother && Wait > 0);
    /// <summary>The giants can't be dazed, so can't be grabbed.</summary>
    public bool Big => Kind is Kind.Goliath or Kind.Magnus;
    public bool Airborne => Act is Act.Jump or Act.JumpKick or Act.Fly;
    public string Name => Kind == Kind.Brother ? Sim.BrotherName(Id) : Kind.ToString().ToUpperInvariant();
}

public enum ItemKind { Bat, Whip, Knife, Drum, Box, Dynamite }

/// <summary>A weapon: on the ground, in someone's hands, or flying.</summary>
public sealed class Item
{
    public int Id;
    public ItemKind Kind;
    public double X, Y, Z, Vx, Vz, Spin;
    /// <summary>Who carries it (-1 nobody), and who threw it.</summary>
    public int Holder = -1, Thrower = -1;
    public bool Flying, Gone;
    /// <summary>Dynamite: lit, the fuse left, and the blast once it goes.</summary>
    public bool Lit;
    public double Fuse, Blast;
    public Item Copy() => (Item)MemberwiseClone();
    public bool Heavy => Kind is ItemKind.Drum or ItemKind.Box;
    /// <summary>Swung, not thrown.</summary>
    public bool Swung => Kind is ItemKind.Bat or ItemKind.Whip;
    public bool Loose => Holder < 0 && !Flying && !Gone && Blast <= 0 && !Lit;
}

/// <summary>A round from the boss's machine gun, flying along his line.</summary>
public sealed class Shot
{
    public double X, Y, Z, Vx, Age;
    public int Owner;
    public bool Gone;
    public Shot Copy() => (Shot)MemberwiseClone();
}

/// <summary>A flash, a puff of dust or a blast, just for the eye. Y is where on the screen.</summary>
public readonly record struct Burst(double X, double Y, double T, int Kind);

/// <summary>
/// Everything in the mission that moves or changes, kept together so it can be copied whole: the brothers, the gang,
/// the weapons, the bullets, the street's scroll, the waves, the clock and the ending.
/// </summary>
public sealed class World
{
    public int Mission = 1;
    public Stage Stage = Stage.Of(1);
    public Fighter[] Brothers = [Sim.NewBrother(0), Sim.NewBrother(1)];
    public List<Fighter> Foes = [];
    public List<Item> Items = [];
    public List<Shot> Shots = [];
    public List<Burst> Bursts = [];
    public int NextId = 2;
    /// <summary>The wave being fought (or next), how many of it are out so far, and how long it's been on.</summary>
    public int Wave, Spawned;
    public bool Locked;
    public double WaveT;
    public double Cam, Clock, TimeLeft;
    public bool Warned, Cleared;
    /// <summary>The garage door rolling up, the statue sinking away.</summary>
    public double Garage, Statue;
    /// <summary>The boss beaten and both brothers standing: they fight each other for her.</summary>
    public bool Duel;
    /// <summary>It's over: she goes to the winner.</summary>
    public bool Ending;
    public int Winner = -1;
    public double EndT, RubyX, RubyY;
    public Dice Dice = new(1);
    public List<Sfx> Heard = [];

    public World Copy() => new()
    {
        Mission = Mission, Stage = Stage, Brothers = Brothers.Select(b => b.Copy()).ToArray(), Foes = Foes.Select(f => f.Copy()).ToList(),
        Items = Items.Select(i => i.Copy()).ToList(), Shots = Shots.Select(s => s.Copy()).ToList(), Bursts = [.. Bursts], NextId = NextId,
        Wave = Wave, Spawned = Spawned, Locked = Locked, WaveT = WaveT, Cam = Cam, Clock = Clock, TimeLeft = TimeLeft, Warned = Warned,
        Cleared = Cleared, Garage = Garage, Statue = Statue, Duel = Duel, Ending = Ending, Winner = Winner, EndT = EndT, RubyX = RubyX,
        RubyY = RubyY, Dice = Dice,
    };

    /// <summary>Anyone by number: the brothers are 0 and 1, the gang count on from 2.</summary>
    public Fighter? Get(int id)
    {
        if (id < 0) return null;
        if (id < 2) return Brothers[id];
        foreach (var f in Foes) if (f.Id == id) return f;
        return null;
    }

    public IEnumerable<Fighter> Everyone => Brothers.Where(b => b.Joined).Concat(Foes);
    /// <summary>The brothers in the fight (joined, with a life on).</summary>
    public IEnumerable<Fighter> Playing => Brothers.Where(b => b.Joined && b.Act != Act.Out);
    /// <summary>What a fighter carries, if anything.</summary>
    public Item? Carried(Fighter f)
    {
        foreach (var i in Items) if (i.Holder == f.Id && !i.Gone) return i;
        return null;
    }

    /// <summary>Whether two fighters are on opposite sides: the gang against the brothers, or brother against brother in the duel.</summary>
    public bool Hostile(Fighter a, Fighter b) => a.Id != b.Id && (a.IsBrother != b.IsBrother || Duel && a.IsBrother && b.IsBrother);
}
