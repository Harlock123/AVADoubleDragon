namespace AVADoubleDragon.Engine;

public enum Scenery { Street, Factory, Forest, Hideout }

/// <summary>One of the gang coming in: from the left (-1) or right (1) edge of the screen, or out of the garage (0); after a delay; maybe armed.</summary>
public readonly record struct Spawn(Kind Kind, int Side, double Delay, ItemKind? Carry = null);

/// <summary>
/// A fight the street stops for: when the screen's left edge reaches <see cref="Lock"/> it stays there, and the gang
/// come in, until every one of them is down.
/// </summary>
public sealed record Wave(double Lock, Spawn[] Spawns);

/// <summary>Where there's no floor: the gorge either side of the bridge and its broken middle, the pit at the belt's end.</summary>
public readonly record struct Hole(double X0, double X1, double Y0, double Y1)
{
    public bool Holds(double x, double y) => x > X0 && x < X1 && y > Y0 && y < Y1;
}

/// <summary>A spiked block in the hideout's wall that thrusts out across the floor, as far as <see cref="Reach"/> from the back.</summary>
public readonly record struct Spike(double X, double Reach, double Phase)
{
    public const double Wide = 120, Period = 3.0;
}

/// <summary>A conveyor belt across the back of the factory floor, carrying whatever stands on it.</summary>
public readonly record struct Belt(double X0, double X1, double Y0, double Y1, double Speed);

/// <summary>A weapon lying in the street when the mission starts.</summary>
public readonly record struct Placed(ItemKind Kind, double X, double Y);

/// <summary>A mission: its street, the waves of the gang along it, and what's in the way.</summary>
public sealed class Stage
{
    public const int Missions = 4;

    public int Mission { get; }
    public string Name { get; }
    public string Place { get; }
    public Scenery Scenery { get; }
    public double Length { get; }
    /// <summary>The clock for each section (it's set again as each wave is beaten).</summary>
    public double Time { get; }
    public IReadOnlyList<Wave> Waves { get; }
    public IReadOnlyList<Placed> Items { get; }
    public IReadOnlyList<Hole> Holes { get; init; } = [];
    public IReadOnlyList<Spike> Spikes { get; init; } = [];
    public Belt? Belt { get; init; }
    /// <summary>The forest's bridge: from X0 to X1 along a strip Y0 to Y1, broken between Gap0 and Gap1.</summary>
    public (double X0, double X1, double Y0, double Y1, double Gap0, double Gap1)? Bridge { get; init; }
    public double? Garage { get; init; }
    public double? Ladder { get; init; }
    /// <summary>The statue barring the way to the boss's hall, until the gang before it are beaten.</summary>
    public double? Statue { get; init; }
    /// <summary>Where the brothers' girl is held, at the end of the last mission.</summary>
    public double? Ruby { get; init; }
    /// <summary>How the mission ends: off the right edge, up the ladder, or with the boss.</summary>
    public double Exit => Length - 70;
    public bool Final => Ruby != null;

    private Stage(int mission, string name, string place, Scenery scenery, double length, double time, Wave[] waves, Placed[] items)
    {
        Mission = mission;
        Name = name;
        Place = place;
        Scenery = scenery;
        Length = length;
        Time = time;
        Waves = waves;
        Items = items;
    }

    /// <summary>Whether there's floor to stand on at a point.</summary>
    public bool Floor(double x, double y)
    {
        foreach (var h in Holes) if (h.Holds(x, y)) return false;
        return true;
    }

    /// <summary>How far a spiked block is thrust out at a moment, 0 (in the wall) to 1 (all the way).</summary>
    public static double Thrust(Spike s, double clock)
    {
        double t = ((clock + s.Phase) % Spike.Period + Spike.Period) % Spike.Period;
        return t switch
        {
            < 1.6 => 0,
            < 2.0 => 0.1,
            < 2.12 => 0.1 + 0.9 * (t - 2.0) / 0.12,
            < 2.6 => 1,
            _ => 1 - (t - 2.6) / 0.4,
        };
    }

    private static Spawn S(Kind k, int side, double delay, ItemKind? carry = null) => new(k, side, delay, carry);

    private const double W = Field.Width, B = Field.Back;

    private static readonly Stage[] All =
    [
        new(1, "MISSION 1", "THE CITY SLUMS", Scenery.Street, 4400, 90,
        [
            new(0, [S(Kind.Dutch, 0, 1.0), S(Kind.Dutch, 0, 2.6), S(Kind.Dutch, 1, 6, ItemKind.Bat)]),
            new(1150, [S(Kind.Rooker, 1, 0), S(Kind.Dutch, -1, 1.5), S(Kind.Vesper, 1, 4, ItemKind.Whip)]),
            new(2250, [S(Kind.Dutch, 1, 0, ItemKind.Knife), S(Kind.Rooker, 1, 1.2), S(Kind.Dutch, -1, 3.5), S(Kind.Vesper, 1, 7, ItemKind.Whip)]),
            new(4400 - W, [S(Kind.Goliath, 1, 0.5), S(Kind.Dutch, -1, 2.5)]),
        ],
        [new(ItemKind.Drum, 1580, B + 24), new(ItemKind.Box, 1660, B + 70), new(ItemKind.Bat, 2700, 610), new(ItemKind.Drum, 3900, B + 30)])
        {
            Garage = 640,
        },
        new(2, "MISSION 2", "THE FACTORY", Scenery.Factory, 4700, 90,
        [
            new(380, [S(Kind.Vesper, 1, 0.5, ItemKind.Whip), S(Kind.Dutch, 1, 1.5), S(Kind.Rooker, -1, 3)]),
            new(1400, [S(Kind.Dutch, 1, 0), S(Kind.Fuse, 1, 1.0), S(Kind.Dutch, -1, 3, ItemKind.Bat)]),
            new(2500, [S(Kind.Slade, 1, 0.5), S(Kind.Vesper, -1, 2, ItemKind.Whip), S(Kind.Rooker, 1, 4)]),
            new(4700 - W, [S(Kind.Goliath, 1, 0.5), S(Kind.Fuse, 1, 2.5), S(Kind.Vesper, -1, 5, ItemKind.Whip)]),
        ],
        [new(ItemKind.Drum, 760, B + 20), new(ItemKind.Drum, 810, B + 46), new(ItemKind.Knife, 1900, 640), new(ItemKind.Box, 3000, B + 60), new(ItemKind.Dynamite, 3300, 650)])
        {
            Belt = new Belt(1560, 2240, B, B + 96, -95),
            Holes = [new Hole(1470, 1560, B - 30, B + 96)],
            Ladder = 4700 - 150,
        },
        new(3, "MISSION 3", "THE FOREST", Scenery.Forest, 4700, 90,
        [
            new(300, [S(Kind.Dutch, 1, 0.5), S(Kind.Slade, 1, 2)]),
            new(1300, [S(Kind.Fuse, 1, 0), S(Kind.Rooker, -1, 1.5), S(Kind.Vesper, 1, 3.5, ItemKind.Whip)]),
            new(2080, [S(Kind.Dutch, 1, 0.3), S(Kind.Slade, 1, 2.5), S(Kind.Dutch, -1, 5, ItemKind.Knife)]),
            new(4700 - W, [S(Kind.Slade, 1, 0.5), S(Kind.Slade, -1, 2), S(Kind.Vesper, 1, 4, ItemKind.Whip)]),
        ],
        [new(ItemKind.Bat, 900, 640), new(ItemKind.Box, 1700, B + 40), new(ItemKind.Dynamite, 3400, 600)])
        {
            Bridge = (2400, 2680, 548, 612, 2505, 2575),
            Holes = [new Hole(2400, 2680, B - 30, 548), new Hole(2400, 2680, 612, Field.Front + 30), new Hole(2505, 2575, 548, 612)],
        },
        new(4, "MISSION 4", "THE HIDEOUT", Scenery.Hideout, 5300, 90,
        [
            new(260, [S(Kind.Dutch, 1, 0.5), S(Kind.Vesper, 1, 2, ItemKind.Whip)]),
            new(1250, [S(Kind.Slade, 1, 0), S(Kind.Fuse, 1, 1.5), S(Kind.Dutch, -1, 3)]),
            new(2900, [S(Kind.Goliath, 1, 0.5), S(Kind.Rooker, 1, 2), S(Kind.Dutch, -1, 4, ItemKind.Bat)]),
            new(5300 - W, [S(Kind.Magnus, 1, 1.2), S(Kind.Dutch, -1, 8), S(Kind.Vesper, 1, 14, ItemKind.Whip)]),
        ],
        [new(ItemKind.Bat, 700, 650), new(ItemKind.Knife, 2000, 600), new(ItemKind.Dynamite, 2600, 640)])
        {
            Spikes = [new Spike(880, 160, 0), new Spike(1700, 250, 1.1), new Spike(2160, 160, 2.0), new Spike(2560, 200, 0.6)],
            Statue = 2900 + W - 150,
            Ruby = 5300 - 110,
        },
    ];

    public static Stage Of(int mission) => All[(mission - 1) % Missions];
}
