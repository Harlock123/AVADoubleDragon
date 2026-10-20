using Avalonia.Input;
using AVADoubleDragon.Engine;

namespace AVADoubleDragon.Tests;

public class GameplayTests
{
    private static string TempFolder() => Path.Combine(Path.GetTempPath(), "avadoubledragon-tests-" + Guid.NewGuid().ToString("N"));

    private static GameEngine Engine() => new(HighScoreTable.Load(TempFolder())) { Silent = true };

    /// <summary>A mission with nobody in it: its first fight already won, no weapons lying about, the brothers on their feet.</summary>
    private static World Street(int mission = 1, int brothers = 1)
    {
        var w = new World();
        Sim.Start(w, brothers);
        if (mission != 1) Sim.StartMission(w, mission);
        w.Locked = true;
        w.Spawned = w.Stage.Waves[0].Spawns.Length;
        Sim.Step(w, default);
        w.Items.Clear();
        w.Heard.Clear();
        for (int i = 0; i < brothers; i++) Ready(w.Brothers[i], 400 + i * 300, Field.Middle);
        return w;
    }

    private static Fighter Ready(Fighter b, double x, double y)
    {
        b.X = x;
        b.Y = y;
        b.Z = 0;
        b.Act = Act.Stand;
        b.Safe = 0;
        b.Facing = 1;
        return b;
    }

    /// <summary>One of the gang standing still where he's put (kept there by <see cref="Run"/> while he's on his feet).</summary>
    private static Fighter Put(World w, Kind kind, double x, double y, ItemKind? carry = null)
    {
        var f = new Fighter { Id = w.NextId++, Kind = kind, X = x, Y = y, Hp = Sim.Toughness(kind), MaxHp = Sim.Toughness(kind), Act = Act.Stand, Cool = 99, Think = 99, Target = 0, In = true, Facing = -1 };
        w.Foes.Add(f);
        if (carry is { } k) w.Items.Add(new Item { Id = w.NextId++, Kind = k, Holder = f.Id, X = x, Y = y });
        return f;
    }

    private static Item Give(World w, Fighter f, ItemKind kind)
    {
        var i = new Item { Id = w.NextId++, Kind = kind, Holder = f.Id, X = f.X, Y = f.Y };
        w.Items.Add(i);
        return i;
    }

    /// <summary>Steps on, keeping the pinned ones of the gang where they stand and their fists down.</summary>
    private static void Run(World w, int steps, Controls c = default, Controls c2 = default, params Fighter[] pin)
    {
        var at = pin.Select(f => (f.X, f.Y)).ToArray();
        for (int i = 0; i < steps; i++)
        {
            Sim.Step(w, c, c2);
            for (int k = 0; k < pin.Length; k++)
            {
                if (pin[k].Act is not (Act.Stand or Act.Walk)) continue;
                (pin[k].X, pin[k].Y) = at[k];
                pin[k].Cool = 99;
                pin[k].Act = Act.Stand;
            }
        }
    }

    /// <summary>One press of a button (or more), then on until the move is done.</summary>
    private static void Press(World w, Controls c, params Fighter[] pin)
    {
        Run(w, 1, c, default, pin);
        Run(w, 1, default, default, pin);
        for (int i = 0; i < 90 && w.Brothers[0].Act is not (Act.Stand or Act.Walk or Act.Hold); i++) Run(w, 1, default, default, pin);
    }

    private static readonly Controls PunchKey = new(Punch: true), KickKey = new(Kick: true), JumpKey = new(Jump: true), ElbowKey = new(Elbow: true);

    // ───────────────────────────── walking the street ─────────────────────────────

    [Fact]
    public void A_brother_walks_along_the_street_and_up_and_down_it()
    {
        var w = Street();
        var b = w.Brothers[0];
        Run(w, 60, new Controls(Dx: 1));
        Assert.Equal(400 + Sim.WalkX, b.X, 0.5);
        Assert.Equal(1, b.Facing);
        Run(w, 30, new Controls(Dy: -1));
        Assert.Equal(Field.Middle - Sim.WalkY / 2, b.Y, 0.5);
        Run(w, 600, new Controls(Dy: -1));
        Assert.Equal(Field.Back, b.Y);
        Run(w, 600, new Controls(Dx: -1, Dy: 1));
        Assert.Equal(Field.Front, b.Y);
        Assert.Equal(Field.Margin, b.X);
        Assert.Equal(-1, b.Facing);
    }

    [Fact]
    public void A_jump_goes_up_and_comes_down_where_it_was_aimed()
    {
        var w = Street();
        var b = w.Brothers[0];
        Run(w, 1, new Controls(Dx: 1, Jump: true));
        Assert.Equal(Act.Jump, b.Act);
        double top = 0;
        for (int i = 0; i < 60 && b.Act == Act.Jump; i++) { Run(w, 1, new Controls(Dx: 1)); top = Math.Max(top, b.Z); }
        Assert.Equal(Act.Stand, b.Act);
        Assert.InRange(top, 80, 100);
        Assert.InRange(b.X, 400 + 100, 400 + 130);
    }

    // ───────────────────────────── the depth line ─────────────────────────────

    [Fact]
    public void A_punch_lands_only_on_the_same_line_of_the_street()
    {
        var w = Street();
        var on = Put(w, Kind.Rooker, 450, Field.Middle);
        var off = Put(w, Kind.Rooker, 450, Field.Middle + 30);
        Press(w, PunchKey, on, off);
        Assert.Equal(on.MaxHp - Sim.PunchHarm, on.Hp);
        Assert.Equal(off.MaxHp, off.Hp);
        // Within a step or so of the line still counts.
        var near = Put(w, Kind.Rooker, 450, Field.Middle + Sim.DepthReach - 2);
        on.X = 900;
        Press(w, PunchKey, on, off, near);
        Assert.Equal(near.MaxHp - Sim.PunchHarm, near.Hp);
        Assert.Equal(off.MaxHp, off.Hp);
    }

    [Fact]
    public void A_kick_reaches_further_than_a_punch()
    {
        var w = Street();
        var f = Put(w, Kind.Rooker, 400 + 74, Field.Middle);
        Press(w, PunchKey, f);
        Assert.Equal(f.MaxHp, f.Hp);
        Press(w, KickKey, f);
        Assert.Equal(f.MaxHp - Sim.KickHarm, f.Hp);
    }

    [Fact]
    public void A_punch_doesnt_reach_behind_but_the_back_elbow_does()
    {
        var w = Street();
        var b = w.Brothers[0];
        var f = Put(w, Kind.Rooker, 400 - 44, Field.Middle);
        Press(w, PunchKey, f);
        Assert.Equal(f.MaxHp, f.Hp);
        Press(w, ElbowKey, f);
        Assert.Equal(f.MaxHp - Sim.ElbowHarm, f.Hp);
        Assert.Equal(1, b.Facing);
        // On the keyboard: kick with the way he walks held away from where he faces.
        Press(w, new Controls(Dx: -1, Kick: true), f);
        Assert.Equal(f.MaxHp - 2 * Sim.ElbowHarm, f.Hp);
        Assert.Equal(1, b.Facing);
    }

    [Fact]
    public void A_jump_kick_knocks_him_down_and_a_jump_goes_over_a_punch()
    {
        var w = Street();
        var b = w.Brothers[0];
        var f = Put(w, Kind.Dutch, 470, Field.Middle);
        Run(w, 1, JumpKey, default, f);
        Run(w, 10, default, default, f);
        Run(w, 1, KickKey, default, f);
        for (int i = 0; i < 60 && f.Act != Act.Fly; i++) Run(w, 1, default, default, f);
        Assert.Equal(Act.Fly, f.Act);
        Assert.Equal(f.MaxHp - Sim.JumpKickHarm, f.Hp);
        // Up in the air, a punch passes under him.
        w = Street();
        b = w.Brothers[0];
        f = Put(w, Kind.Dutch, 450, Field.Middle);
        Run(w, 1, JumpKey, default, f);
        Run(w, 10, default, default, f);
        Sim.Begin(w, f, Act.Punch);
        Run(w, 12);
        Assert.Equal(b.MaxHp, b.Hp);
    }

    // ───────────────────────────── dazed, grabbed, kneed and thrown ─────────────────────────────

    /// <summary>Three quick punches, and he's reeling.</summary>
    private static void Daze(World w, Fighter f)
    {
        for (int i = 0; i < 3; i++) Press(w, PunchKey, f);
        Assert.Equal(Act.Dazed, f.Act);
    }

    private static void Grab(World w, Fighter f)
    {
        Daze(w, f);
        for (int i = 0; i < 30 && w.Brothers[0].Act != Act.Hold; i++) Run(w, 1, new Controls(Dx: 1));
        Assert.Equal(Act.Hold, w.Brothers[0].Act);
        Assert.Equal(Act.Held, f.Act);
    }

    [Fact]
    public void Three_quick_blows_daze_him_and_walking_into_him_grabs_him_by_the_hair()
    {
        var w = Street();
        var b = w.Brothers[0];
        var f = Put(w, Kind.Slade, 450, Field.Middle);
        Grab(w, f);
        Assert.Equal(f.Id, b.Partner);
        Assert.Equal(b.Id, f.Partner);
        Assert.Equal(-b.Facing, f.Facing);
        Assert.Equal(b.X + Sim.HeldGap, f.X, 1);
        // Let him be and he wrenches free.
        Run(w, (int)(60 * Sim.HoldTime) + 10);
        Assert.NotEqual(Act.Held, f.Act);
        Assert.Equal(-1, b.Partner);
    }

    [Fact]
    public void A_dazed_man_hit_again_goes_down()
    {
        var w = Street();
        var f = Put(w, Kind.Slade, 450, Field.Middle);
        Daze(w, f);
        Press(w, KickKey, f);
        Assert.Equal(Act.Fly, f.Act);
    }

    [Fact]
    public void Three_knees_to_the_face_and_he_goes_down_and_is_let_go()
    {
        var w = Street();
        var b = w.Brothers[0];
        var f = Put(w, Kind.Slade, 450, Field.Middle);
        Grab(w, f);
        int hp = f.Hp;
        Press(w, KickKey);
        Assert.Equal(Act.Hold, b.Act);
        Assert.Equal(hp - Sim.KneeHarm, f.Hp);
        Press(w, KickKey);
        Assert.Equal(Act.Held, f.Act);
        Run(w, 1, KickKey);
        Run(w, 10);
        Assert.Equal(hp - 3 * Sim.KneeHarm, f.Hp);
        Assert.Equal(Act.Fly, f.Act);
        Assert.Equal(-1, b.Partner);
    }

    [Fact]
    public void The_over_the_shoulder_throw_lands_him_behind_and_flattens_whoever_is_there()
    {
        var w = Street();
        var b = w.Brothers[0];
        var f = Put(w, Kind.Slade, 450, Field.Middle);
        Grab(w, f);
        var behind = Put(w, Kind.Dutch, b.X - 90, Field.Middle);
        int hp = f.Hp;
        Run(w, 1, JumpKey, default, behind);
        for (int i = 0; i < 120 && f.Act != Act.Down; i++) Run(w, 1, default, default, behind);
        Assert.Equal(Act.Down, f.Act);
        Assert.True(f.X < b.X, "over his shoulder, behind him");
        Assert.Equal(hp - Sim.ThrowHarm, f.Hp);
        Assert.Equal(behind.MaxHp - Sim.ThrownHarm, behind.Hp);
        Assert.True(behind.Act is Act.Fly or Act.Down);
    }

    [Fact]
    public void The_hair_pull_slams_him_face_first_in_front()
    {
        var w = Street();
        var b = w.Brothers[0];
        var f = Put(w, Kind.Slade, 450, Field.Middle);
        Grab(w, f);
        int hp = f.Hp;
        Press(w, PunchKey);
        for (int i = 0; i < 60 && f.Act != Act.Down; i++) Run(w, 1);
        Assert.Equal(Act.Down, f.Act);
        Assert.True(f.X > b.X, "in front of him");
        Assert.Equal(hp - Sim.SlamHarm, f.Hp);
    }

    [Fact]
    public void Knocked_down_he_lies_a_moment_gets_up_and_cant_be_hit_straight_away()
    {
        var w = Street();
        var f = Put(w, Kind.Rooker, 450, Field.Middle);
        Sim.Knock(w, f, 1, false);
        for (int i = 0; i < 120 && f.Act != Act.Down; i++) Run(w, 1);
        Assert.Equal(Act.Down, f.Act);
        Assert.False(f.Open);
        Run(w, (int)(60 * Sim.DownTime) + 2);
        Assert.Equal(Act.Rise, f.Act);
        Run(w, (int)(60 * Sim.RiseTime) + 2);
        Assert.True(f.Act is Act.Stand or Act.Walk);
        Assert.True(f.Safe > 0);
        f.X = 450;
        int hp = f.Hp;
        Press(w, PunchKey, f);
        Assert.Equal(hp, f.Hp);
        Run(w, (int)(60 * Sim.SafeAfterRise), default, default, f);
        Press(w, PunchKey, f);
        Assert.Equal(hp - Sim.PunchHarm, f.Hp);
    }

    [Fact]
    public void The_giants_are_never_dazed_cant_be_grabbed_and_take_six_quick_blows_to_floor()
    {
        var w = Street();
        var b = w.Brothers[0];
        var g = Put(w, Kind.Goliath, 450, Field.Middle);
        for (int i = 0; i < 3; i++) Press(w, PunchKey, g);
        Assert.NotEqual(Act.Dazed, g.Act);
        Run(w, 20, new Controls(Dx: 1), default, g);
        Assert.NotEqual(Act.Hold, b.Act);
        b.X = 400;
        for (int i = 0; i < 3 && g.Act != Act.Fly; i++) Press(w, PunchKey, g);
        Assert.Equal(Act.Fly, g.Act);
        Assert.True(g.Big);
    }

    // ───────────────────────────── weapons ─────────────────────────────

    [Fact]
    public void A_bat_is_picked_up_with_a_punch_swung_and_dropped_when_he_is_knocked_down()
    {
        var w = Street();
        var b = w.Brothers[0];
        var bat = new Item { Id = w.NextId++, Kind = ItemKind.Bat, X = 410, Y = Field.Middle };
        w.Items.Add(bat);
        Press(w, PunchKey);
        Assert.Same(bat, w.Carried(b));
        var f = Put(w, Kind.Rooker, 400 + 90, Field.Middle);
        Press(w, PunchKey, f);
        Assert.Equal(f.MaxHp - Sim.BatHarm, f.Hp);
        Assert.True(f.Act is Act.Fly or Act.Down);
        Sim.Knock(w, b, -1, false);
        Assert.Null(w.Carried(b));
        Assert.True(bat.Loose);
    }

    [Fact]
    public void A_whip_reaches_furthest_of_all()
    {
        var w = Street();
        var b = w.Brothers[0];
        var v = Put(w, Kind.Vesper, 400 + 110, Field.Middle, ItemKind.Whip);
        Sim.Begin(w, v, Act.Swing);
        Run(w, 40);
        Assert.True(b.Hp < b.MaxHp);
        Assert.True(Sim.WhipReach > Sim.BatReach && Sim.BatReach > Sim.KickReach);
        // In his hands too.
        w = Street();
        b = w.Brothers[0];
        Give(w, b, ItemKind.Whip);
        var f = Put(w, Kind.Rooker, 400 + 118, Field.Middle);
        Press(w, PunchKey, f);
        Assert.Equal(f.MaxHp - Sim.WhipHarm, f.Hp);
    }

    [Fact]
    public void A_thrown_knife_flies_along_its_line_and_drops_where_it_strikes()
    {
        var w = Street();
        var b = w.Brothers[0];
        var knife = Give(w, b, ItemKind.Knife);
        var off = Put(w, Kind.Dutch, 400 + 150, Field.Middle + 40);
        var on = Put(w, Kind.Dutch, 400 + 320, Field.Middle);
        Press(w, PunchKey, off, on);
        Run(w, 40, default, default, off, on);
        Assert.Equal(off.MaxHp, off.Hp);
        Assert.Equal(on.MaxHp - Sim.KnifeHarm, on.Hp);
        Assert.True(knife.Loose);
        Assert.InRange(knife.X, 400 + 280, 400 + 360);
    }

    [Fact]
    public void A_thrown_drum_bowls_over_everyone_on_its_line_and_a_crate_breaks()
    {
        var w = Street();
        var b = w.Brothers[0];
        Give(w, b, ItemKind.Drum);
        var near = Put(w, Kind.Dutch, 400 + 110, Field.Middle);
        var far = Put(w, Kind.Dutch, 400 + 220, Field.Middle);
        Press(w, PunchKey, near, far);
        Run(w, 40);
        Assert.Equal(near.MaxHp - Sim.DrumHarm, near.Hp);
        Assert.Equal(far.MaxHp - Sim.DrumHarm, far.Hp);
        w = Street();
        b = w.Brothers[0];
        var box = Give(w, b, ItemKind.Box);
        var f = Put(w, Kind.Dutch, 400 + 150, Field.Middle);
        Press(w, KickKey, f);
        Run(w, 40);
        Assert.Equal(f.MaxHp - Sim.BoxHarm, f.Hp);
        Assert.DoesNotContain(box, w.Items);
    }

    [Fact]
    public void Dynamite_is_lobbed_lit_and_goes_off_sparing_no_one_near_it()
    {
        var w = Street(1, 2);
        var b = w.Brothers[0];
        var other = Ready(w.Brothers[1], 400 + Sim.DynamiteRange + 20, Field.Middle + 30);
        var stick = Give(w, b, ItemKind.Dynamite);
        var f = Put(w, Kind.Dutch, 400 + Sim.DynamiteRange - 10, Field.Middle);
        Press(w, PunchKey, f);
        Assert.True(stick.Lit);
        for (int i = 0; i < 180 && stick.Blast <= 0; i++) Run(w, 1, default, default, f);
        Assert.True(stick.Blast > 0);
        Assert.InRange(stick.X, 400 + Sim.DynamiteRange - 40, 400 + Sim.DynamiteRange + 40);
        Assert.Equal(f.MaxHp - Sim.BlastHarm, f.Hp);
        Assert.True(other.Hp < other.MaxHp, "his brother too");
        Assert.Equal(b.MaxHp, b.Hp);
    }

    [Fact]
    public void The_gang_fetch_weapons_and_use_them()
    {
        // A punk goes for a bat lying near.
        var w = Street();
        var b = w.Brothers[0];
        var bat = new Item { Id = w.NextId++, Kind = ItemKind.Bat, X = 800, Y = Field.Middle + 40 };
        w.Items.Add(bat);
        var d = Put(w, Kind.Dutch, 900, Field.Middle + 40);
        d.Cool = 0;
        for (int i = 0; i < 180 && w.Carried(d) == null; i++) Run(w, 1);
        Assert.Same(bat, w.Carried(d));
        // The dock hand hurls a drum down the line.
        w = Street();
        b = w.Brothers[0];
        var r = Put(w, Kind.Rooker, 700, Field.Middle, ItemKind.Drum);
        r.Cool = 0;
        for (int i = 0; i < 180 && b.Act != Act.Fly; i++) Run(w, 1);
        Assert.Equal(Act.Fly, b.Act);
        // The demolition man lobs dynamite.
        w = Street();
        b = w.Brothers[0];
        var fuse = Put(w, Kind.Fuse, 700, Field.Middle);
        fuse.Cool = 0;
        for (int i = 0; i < 120 && !w.Items.Any(x => x is { Kind: ItemKind.Dynamite, Lit: true }); i++) Run(w, 1);
        var stick = w.Items.Single(x => x.Kind == ItemKind.Dynamite);
        Assert.Equal(fuse.Id, stick.Thrower);
        for (int i = 0; i < 120 && stick.Flying; i++) Run(w, 1);
        Assert.InRange(stick.X, b.X - 80, b.X + 80);
    }

    // ───────────────────────────── the Black Talons ─────────────────────────────

    [Fact]
    public void A_punk_walks_up_onto_the_brothers_line_and_strikes()
    {
        var w = Street();
        var b = w.Brothers[0];
        var d = Put(w, Kind.Dutch, 750, Field.Middle + 80);
        d.Cool = 0;
        d.Think = 0;
        for (int i = 0; i < 60 * 6 && b.Hp == b.MaxHp; i++) Run(w, 1);
        Assert.True(b.Hp < b.MaxHp);
        Assert.InRange(d.Y, b.Y - Sim.DepthReach, b.Y + Sim.DepthReach);
        Assert.Equal(500, Sim.Points(Kind.Dutch));
    }

    [Fact]
    public void A_punk_grabs_a_dazed_brother_and_mashing_the_buttons_breaks_free()
    {
        var w = Street();
        var b = w.Brothers[0];
        var d = Put(w, Kind.Dutch, 400 + 48, Field.Middle);
        b.Act = Act.Dazed;
        d.Cool = 0;
        Run(w, 2);
        Assert.Equal(Act.Held, b.Act);
        Assert.Equal(Act.Hold, d.Act);
        // Left alone, he's kneed and thrown.
        for (int i = 0; i < 60 * 3 && b.Act == Act.Held; i++) Run(w, 1);
        Assert.Equal(Act.Fly, b.Act);
        w = Street();
        b = w.Brothers[0];
        d = Put(w, Kind.Dutch, 400 + 48, Field.Middle);
        b.Act = Act.Dazed;
        d.Cool = 0;
        Run(w, 2);
        for (int i = 0; i < 60 && b.Act == Act.Held; i++) Run(w, 1, new Controls(Punch: i % 2 == 0));
        Assert.Equal(Act.Stand, b.Act);
    }

    [Fact]
    public void The_whip_woman_lashes_from_beyond_a_fists_reach()
    {
        var w = Street();
        var b = w.Brothers[0];
        var v = Put(w, Kind.Vesper, 700, Field.Middle, ItemKind.Whip);
        v.Cool = 0;
        v.Think = 0;
        for (int i = 0; i < 60 * 6 && b.Hp == b.MaxHp; i++) Run(w, 1);
        Assert.True(b.Hp < b.MaxHp);
        Assert.True(Math.Abs(v.X - b.X) > Sim.KickReach, "from further than a kick");
    }

    [Fact]
    public void The_bald_brawler_headbutts_and_the_giant_charges_along_the_line()
    {
        var w = Street();
        var b = w.Brothers[0];
        var s = Put(w, Kind.Slade, 400 + 160, Field.Middle);
        s.Facing = -1;
        Sim.Begin(w, s, Act.Headbutt);
        s.Vx = -Sim.HeadbuttSpeed;
        Run(w, 40);
        Assert.True(b.Act is Act.Fly or Act.Down);
        Assert.Equal(b.MaxHp - (int)Math.Round(Sim.HeadbuttHarm * Sim.Toughened), b.Hp);
        // The charge flattens a brother on its line; a step off the line and it thunders past.
        w = Street(1, 2);
        var on = w.Brothers[0];
        var off = Ready(w.Brothers[1], 400, Field.Middle + 40);
        var g = Put(w, Kind.Goliath, 700, Field.Middle);
        Sim.Begin(w, g, Act.Charge);
        g.Vx = -Sim.ChargeSpeed;
        Run(w, 90);
        Assert.True(on.Hp < on.MaxHp);
        Assert.Equal(off.MaxHp, off.Hp);
    }

    [Fact]
    public void The_boss_fires_along_his_line_and_a_step_off_it_is_safe()
    {
        var w = Street(1, 2);
        var on = w.Brothers[0];
        var off = Ready(w.Brothers[1], 300, Field.Middle - 40);
        var m = Put(w, Kind.Magnus, 760, Field.Middle);
        Sim.Begin(w, m, Act.Aim);
        Run(w, (int)(60 * (Sim.AimTime + 3 * Sim.ShotEvery)) + 3);
        Assert.True(m.Act is Act.Stand or Act.Walk);
        Assert.Equal(3, w.Heard.Count(x => x == Sfx.Gun));
        Run(w, 60);
        Assert.True(on.Hp < on.MaxHp);
        Assert.True(on.Act is Act.Fly or Act.Down or Act.Rise);
        Assert.Equal(off.MaxHp, off.Hp);
        Assert.Equal(10000, Sim.Points(Kind.Magnus));
    }

    [Fact]
    public void Knocking_one_of_them_out_scores_his_worth_to_whoever_did_it()
    {
        var w = Street(1, 2);
        var jake = w.Brothers[0];
        var cole = w.Brothers[1];
        var f = Put(w, Kind.Vesper, 450, Field.Middle);
        f.Hp = Sim.PunchHarm;
        Press(w, PunchKey, f);
        Assert.True(f.Ko);
        Assert.Equal(Sim.PunchHarm * Sim.PointsPerHarm + Sim.Points(Kind.Vesper), jake.Score);
        Assert.Equal(0, cole.Score);
        Run(w, 180);
        Assert.DoesNotContain(f, w.Foes);
    }

    // ───────────────────────────── the gorge, the spikes, the belt, the ladder ─────────────────────────────

    [Fact]
    public void Off_the_bridge_into_the_gorge_costs_a_life_but_a_jump_clears_its_broken_middle()
    {
        var w = Street(3);
        var br = w.Stage.Bridge!.Value;
        w.Cam = br.X0 - 500;
        var b = Ready(w.Brothers[0], br.X0 - 40, br.Y1 + 30);
        Run(w, 60, new Controls(Dx: 1));
        Assert.Equal(Act.Fall, b.Act);
        Run(w, (int)(60 * Sim.FallTime) + 2);
        Assert.Equal(Sim.Lives0 - 1, b.Lives);
        Assert.True(b.Wait > 0);
        // Along the bridge and over the gap.
        w = Street(3);
        w.Cam = br.X0 - 500;
        b = Ready(w.Brothers[0], br.Gap0 - 30, (br.Y0 + br.Y1) / 2);
        Run(w, 1, new Controls(Dx: 1, Jump: true));
        Run(w, 60, new Controls(Dx: 1));
        Assert.Equal(Sim.Lives0, b.Lives);
        Assert.NotEqual(Act.Fall, b.Act);
        Assert.True(b.X > br.Gap1);
    }

    [Fact]
    public void Knocked_into_the_gorge_one_of_the_gang_is_gone_for_good_and_scores()
    {
        var w = Street(3);
        var br = w.Stage.Bridge!.Value;
        double mid = (br.Y0 + br.Y1) / 2;
        w.Cam = br.X0 - 500;
        var b = Ready(w.Brothers[0], br.Gap0 - 100, mid);
        var f = Put(w, Kind.Dutch, br.Gap0 - 50, mid);
        Daze(w, f);
        Press(w, KickKey, f);
        for (int i = 0; i < 60 && f.Act != Act.Fall; i++) Run(w, 1);
        Assert.Equal(Act.Fall, f.Act);
        Run(w, 90);
        Assert.DoesNotContain(f, w.Foes);
        Assert.True(b.Score >= Sim.Points(Kind.Dutch));
    }

    [Fact]
    public void The_spiked_walls_skewer_the_back_of_the_floor_and_spare_the_front()
    {
        var w = Street(4, 2);
        var s = w.Stage.Spikes[0];
        var back = Ready(w.Brothers[0], s.X, Field.Back + s.Reach / 3);
        var front = Ready(w.Brothers[1], s.X + 20, Field.Back + s.Reach + 40);
        var f = Put(w, Kind.Dutch, s.X - 30, Field.Back + 20);
        // Just before it thrusts.
        w.Clock = 2.0 - s.Phase;
        Assert.Equal(0.1, Stage.Thrust(s, w.Clock), 3);
        Run(w, 40);
        Assert.Equal(back.MaxHp - (int)Math.Round(Sim.SpikeHarm * Sim.Toughened), back.Hp);
        Assert.True(back.Y > Field.Back + s.Reach / 3, "flung out to the front");
        Assert.Equal(front.MaxHp, front.Hp);
        Assert.True(f.Hp < f.MaxHp, "the gang aren't spared either");
        Assert.Equal(0, Stage.Thrust(s, 1.0 - s.Phase));
    }

    [Fact]
    public void The_factory_belt_carries_whoever_stands_on_it_into_the_pit()
    {
        var w = Street(2);
        var belt = w.Stage.Belt!.Value;
        w.Cam = belt.X0 - 300;
        var b = Ready(w.Brothers[0], belt.X0 + 400, belt.Y0 + 40);
        var drum = new Item { Id = w.NextId++, Kind = ItemKind.Drum, X = belt.X0 + 300, Y = belt.Y0 + 50 };
        w.Items.Add(drum);
        Run(w, 60);
        Assert.Equal(belt.X0 + 400 + belt.Speed, b.X, 1);
        Assert.Equal(belt.X0 + 300 + belt.Speed, drum.X, 1);
        // Off it, he stays put.
        var still = Ready(w.Brothers[0], belt.X0 + 400, belt.Y1 + 30);
        Run(w, 60);
        Assert.Equal(belt.X0 + 400, still.X, 1);
        // Stand on it long enough and it drops him into the pit at its end.
        Ready(b, belt.X0 + 60, belt.Y0 + 40);
        for (int i = 0; i < 120 && b.Act != Act.Fall; i++) Run(w, 1);
        Assert.Equal(Act.Fall, b.Act);
    }

    [Fact]
    public void The_factory_is_left_up_the_ladder_once_its_floor_is_clear()
    {
        var w = Street(2);
        double lx = w.Stage.Ladder!.Value;
        w.Cam = w.Stage.Length - Field.Width;
        var b = Ready(w.Brothers[0], lx, Field.Back + 10);
        w.Wave = 2;
        w.Locked = true;
        Put(w, Kind.Dutch, lx - 400, Field.Front);
        Run(w, 30, new Controls(Dy: -1));
        Assert.NotEqual(Act.Climb, b.Act);
        w.Foes.Clear();
        w.Wave = w.Stage.Waves.Count;
        w.Locked = false;
        Ready(b, lx, Field.Back + 10);
        Run(w, 1, new Controls(Dy: -1));
        Assert.Equal(Act.Climb, b.Act);
        for (int i = 0; i < 60 * 4 && !w.Cleared; i++) Run(w, 1);
        Assert.True(w.Cleared);
    }

    [Fact]
    public void The_first_of_the_gang_come_out_of_the_garage()
    {
        var w = new World();
        Sim.Start(w, 1);
        var heard = new List<Sfx>();
        for (int i = 0; i < 120 && w.Foes.Count == 0; i++) { Sim.Step(w, default); heard.AddRange(w.Heard); w.Heard.Clear(); }
        var f = Assert.Single(w.Foes);
        Assert.InRange(f.X, w.Stage.Garage!.Value - 50, w.Stage.Garage.Value + 50);
        Assert.Equal(Field.Back + 2, f.Y, 1);
        Assert.Contains(Sfx.Garage, heard);
        Assert.True(w.Garage > 0);
    }

    [Fact]
    public void The_statue_bars_the_way_until_the_gang_before_it_are_beaten_then_sinks()
    {
        var w = Street(4);
        double st = w.Stage.Statue!.Value;
        w.Wave = 2;
        w.Locked = true;
        w.Spawned = w.Stage.Waves[2].Spawns.Length;
        w.Cam = w.Stage.Waves[2].Lock;
        var b = Ready(w.Brothers[0], st - 200, Field.Middle);
        var f = Put(w, Kind.Dutch, st - 400, Field.Front);
        Run(w, 120, new Controls(Dx: 1), default, f);
        Assert.True(b.X <= st - 70);
        Assert.Equal(0, w.Statue);
        w.Foes.Remove(f);
        Run(w, 2);
        Assert.Equal(3, w.Wave);
        Assert.False(w.Locked, "the boss's wave waits for the statue");
        Run(w, (int)(60 * 2.3));
        Assert.Equal(1, w.Statue);
        Run(w, 60 * 4, new Controls(Dx: 1));
        Assert.True(b.X > st);
    }

    [Fact]
    public void The_street_stops_for_each_fight_and_goes_on_with_the_clock_set_again_when_its_won()
    {
        var w = Street();
        var b = w.Brothers[0];
        var lockAt = w.Stage.Waves[1].Lock;
        for (int i = 0; i < 60 * 20 && !w.Locked; i++) Run(w, 1, new Controls(Dx: 1));
        Assert.True(w.Locked);
        Assert.Equal(lockAt, w.Cam);
        Run(w, 60 * 5, new Controls(Dx: 1));
        Assert.Equal(lockAt, w.Cam);
        Assert.True(b.X <= lockAt + Field.Width - Field.Margin);
        Assert.NotEmpty(w.Foes);
        w.TimeLeft = 30;
        w.Foes.Clear();
        w.Spawned = w.Stage.Waves[1].Spawns.Length;
        Run(w, 2);
        Assert.Equal(2, w.Wave);
        Assert.False(w.Locked);
        Assert.Equal(w.Stage.Time, w.TimeLeft, 1);
    }

    // ───────────────────────────── two brothers ─────────────────────────────

    [Fact]
    public void Cole_joins_any_time_with_his_own_lives_and_score()
    {
        var g = Engine();
        g.Silent = false;
        var heard = new List<Sfx>();
        g.Sound += heard.Add;
        g.Go(1, 1);
        g.World.Brothers[0].Score = 4000;
        g.Run(300);
        Assert.False(g.World.Brothers[1].Joined);
        g.Tick(default, default, false, true);
        var cole = g.World.Brothers[1];
        Assert.True(cole.Joined);
        Assert.Equal(Sim.Lives0, cole.Lives);
        Assert.Equal(0, cole.Score);
        Assert.True(cole.Safe > 0);
        Assert.Contains(Sfx.Join, heard);
        Assert.Equal(4000, g.World.Brothers[0].Score);
    }

    [Fact]
    public void The_brothers_blows_pass_each_other_by_until_the_duel()
    {
        var w = Street(1, 2);
        var jake = w.Brothers[0];
        var cole = Ready(w.Brothers[1], 450, Field.Middle);
        Press(w, PunchKey);
        Assert.Equal(cole.MaxHp, cole.Hp);
        w.Duel = true;
        Press(w, PunchKey);
        Assert.Equal(cole.MaxHp - Sim.PunchHarm, cole.Hp);
        Assert.Equal(jake.MaxHp, jake.Hp);
    }

    // ───────────────────────────── lives ─────────────────────────────

    [Fact]
    public void Knocked_out_a_brother_loses_a_life_and_comes_back_in_safe()
    {
        var w = Street();
        var b = w.Brothers[0];
        b.Hp = 1;
        var f = Put(w, Kind.Dutch, 450, Field.Middle);
        Sim.Hurt(w, f, b, 10, false, -1, Sfx.Punch);
        Assert.True(b.Ko);
        Run(w, 60 * 3);
        Assert.Equal(Sim.Lives0 - 1, b.Lives);
        Run(w, 60 * 2);
        Assert.Equal(b.MaxHp, b.Hp);
        Assert.False(b.Ko);
        Assert.True(b.Safe > 0);
        Assert.Equal(Act.Stand, b.Act);
    }

    [Fact]
    public void The_game_goes_on_while_either_brother_has_a_life_and_an_out_brother_can_come_back_in()
    {
        var g = Engine();
        g.Go(1, 2);
        var jake = g.World.Brothers[0];
        jake.Lives = 1;
        jake.Score = 7000;
        g.Run(60 * 3);
        jake.Hp = 1;
        Sim.Hurt(g.World, null, jake, 50, true, 1, Sfx.Punch);
        g.Run(60 * 3);
        Assert.Equal(Act.Out, jake.Act);
        Assert.Equal(GameState.Playing, g.State);
        // Back in, score kept.
        g.Tick(default, default, true);
        Assert.Equal(Sim.Lives0, jake.Lives);
        Assert.Equal(1, jake.Continues);
        Assert.Equal(7000, jake.Score);
        // Both out: it's over.
        foreach (var b in g.World.Brothers) { b.Lives = 1; b.Hp = 1; }
        g.Run(60 * 3);
        foreach (var b in g.World.Brothers) Sim.Hurt(g.World, null, b, 50, true, 1, Sfx.Punch);
        g.Run(60 * 4);
        Assert.Equal(GameState.GameOver, g.State);
    }

    [Fact]
    public void Every_thirty_thousand_points_brings_another_life()
    {
        var g = Engine();
        g.Silent = false;
        var heard = new List<Sfx>();
        g.Sound += heard.Add;
        g.Go();
        g.World.Brothers[0].Score = Sim.ExtraLifeEvery;
        g.Tick(default);
        Assert.Equal(Sim.Lives0 + 1, g.World.Brothers[0].Lives);
        Assert.Contains(Sfx.OneUp, heard);
    }

    // ───────────────────────────── the clock ─────────────────────────────

    [Fact]
    public void Out_of_time_a_brother_loses_a_life_and_the_clock_starts_again()
    {
        var w = Street();
        var b = w.Brothers[0];
        w.TimeLeft = Sim.WarnAt + 0.5;
        Run(w, 40);
        Assert.Contains(Sfx.TimeWarning, w.Heard);
        w.TimeLeft = 0.5;
        Run(w, 40);
        Assert.True(b.Ko);
        Assert.InRange(w.TimeLeft, w.Stage.Time - 1, w.Stage.Time);
        Run(w, 60 * 2);
        Assert.Equal(Sim.Lives0 - 1, b.Lives);
    }

    [Fact]
    public void Out_of_a_mission_with_the_time_bonus()
    {
        var w = Street();
        var b = w.Brothers[0];
        w.Wave = w.Stage.Waves.Count;
        w.Locked = false;
        w.Cam = w.Stage.Length - Field.Width;
        Ready(b, w.Stage.Exit - 30, Field.Middle);
        w.TimeLeft = 41.5;
        Run(w, 30, new Controls(Dx: 1));
        Assert.True(w.Cleared);
        Assert.Equal(42 * Sim.TimePoints, b.Score);
        Assert.Contains(Sfx.MissionClear, w.Heard);
    }

    // ───────────────────────────── the end ─────────────────────────────

    /// <summary>The last mission with every one of the gang beaten, the brothers in the boss's hall.</summary>
    private static GameEngine Finale(int brothers)
    {
        var g = Engine();
        g.Silent = false;
        g.Go(4, brothers);
        var w = g.World;
        w.Statue = 1;
        w.Wave = w.Stage.Waves.Count;
        w.Cam = w.Stage.Length - Field.Width;
        foreach (var b in w.Brothers.Where(b => b.Joined)) Ready(b, w.Cam + 300 + b.Id * 200, Field.Middle);
        return g;
    }

    [Fact]
    public void Both_brothers_standing_when_the_boss_falls_they_fight_for_Ruby_and_the_winner_gets_her()
    {
        var g = Finale(2);
        var heard = new List<Sfx>();
        g.Sound += heard.Add;
        g.Tick(default);
        var w = g.World;
        Assert.True(w.Duel);
        Assert.Equal(Phase.Duel, g.Phase);
        Assert.Contains(Sfx.Duel, heard);
        var (jake, cole) = (w.Brothers[0], w.Brothers[1]);
        Assert.Equal(1, jake.Facing);
        Assert.Equal(-1, cole.Facing);
        g.Run(120);
        cole.Hp = Sim.PunchHarm;
        jake.X = cole.X - 50;
        jake.Y = cole.Y;
        jake.Facing = 1;
        cole.Safe = 0;
        g.Tick(new Controls(Punch: true));
        g.Run(20);
        Assert.True(cole.Ko);
        Assert.Equal(Sim.Lives0, cole.Lives);
        g.Run(120);
        Assert.Equal(Phase.Ending, g.Phase);
        Assert.Equal(0, w.Winner);
        Assert.Equal(Sim.RubyPoints, jake.Score - Sim.PunchHarm * 0);
        g.Run(60 * 6);
        Assert.True(Sim.Together(w));
        g.Run((int)(60 * GameEngine.EndingSeconds));
        Assert.True(g.Completed);
        Assert.True(g.State is GameState.GameOver or GameState.EnterInitials);
    }

    [Fact]
    public void One_brother_alone_at_the_end_wins_her_without_a_duel()
    {
        var g = Finale(1);
        g.Tick(default);
        Assert.False(g.World.Duel);
        Assert.Equal(Phase.Ending, g.Phase);
        Assert.Equal(0, g.World.Winner);
        Assert.Equal(Sim.RubyPoints, g.World.Brothers[0].Score);
    }

    [Fact]
    public void The_game_over_takes_each_brothers_initials_best_score_first()
    {
        var g = Engine();
        g.Go(1, 2);
        g.World.Brothers[0].Score = 61000;
        g.World.Brothers[1].Score = 72000;
        foreach (var b in g.World.Brothers) { b.Lives = 1; b.Hp = 1; b.NextLife = 90000; }
        g.Run(60 * 3);
        foreach (var b in g.World.Brothers) Sim.Hurt(g.World, null, b, 50, true, 1, Sfx.Punch);
        for (int i = 0; i < 60 * 10 && g.State != GameState.EnterInitials; i++) g.Tick(default);
        Assert.Equal(GameState.EnterInitials, g.State);
        Assert.Equal(1, g.EntryPlayer);
        g.Initials!.Type('C');
        g.Initials.Type('O');
        g.Initials.Type('L');
        g.Tick(default);
        Assert.Equal(0, g.EntryPlayer);
        g.Initials!.Type('J');
        g.Initials.Type('A');
        g.Initials.Type('K');
        g.Tick(default);
        Assert.Equal(GameState.HighScores, g.State);
        Assert.Equal("COL", g.HighScores.Entries[0].Initials);
        Assert.Equal("JAK", g.HighScores.Entries[1].Initials);
        Assert.Equal(72000, g.HighScores.Entries[0].Score);
    }

    // ───────────────────────────── the game ─────────────────────────────

    [Fact]
    public void The_title_runs_a_demonstration_then_the_high_scores_then_the_title()
    {
        var g = Engine();
        g.Idle((int)(60 * GameEngine.TitleIdleSeconds) + 5);
        Assert.Equal(GameState.Attract, g.State);
        Assert.True(g.World.Brothers[1].Joined, "the first demonstration has both brothers");
        Assert.NotNull(g.Pilots[0]);
        g.Idle((int)(60 * GameEngine.AttractSeconds) + 5);
        Assert.Equal(GameState.HighScores, g.State);
        g.Idle((int)(60 * GameEngine.HighScoreSeconds) + 5);
        Assert.Equal(GameState.Title, g.State);
        g.Idle((int)(60 * GameEngine.TitleIdleSeconds) + 5);
        Assert.Equal(GameState.Attract, g.State);
        Assert.False(g.World.Brothers[1].Joined, "the second has one");
        Assert.Equal(2, g.World.Mission);
    }

    [Fact]
    public void One_starts_a_game_for_Jake_two_for_both_and_a_punch_on_the_title_starts_one()
    {
        var g = Engine();
        g.Tick(default, default, true);
        Assert.Equal(GameState.Playing, g.State);
        Assert.Equal(Phase.Start, g.Phase);
        Assert.True(g.World.Brothers[0].Joined);
        Assert.False(g.World.Brothers[1].Joined);
        g.Run((int)(60 * GameEngine.StartSeconds) + 5);
        Assert.Equal(Phase.Play, g.Phase);
        g = Engine();
        g.Tick(default, default, false, true);
        Assert.True(g.World.Brothers.All(b => b.Joined));
        g = Engine();
        g.Idle(40);
        g.Tick(new Controls(Punch: true));
        Assert.Equal(GameState.Playing, g.State);
    }

    [Fact]
    public void Pausing_stops_the_clock()
    {
        var g = Engine();
        g.Go();
        g.Pause();
        Assert.Equal(GameState.Paused, g.State);
        double t = g.World.TimeLeft;
        g.Run(120, new Controls(Dx: 1));
        Assert.Equal(t, g.World.TimeLeft);
        g.Resume();
        g.Run(60);
        Assert.True(g.World.TimeLeft < t);
    }

    [Fact]
    public void The_keys_for_the_two_brothers_never_overlap_and_up_goes_up_the_screen()
    {
        using var input = new InputHandler();
        Key[] jake = [Key.W, Key.A, Key.S, Key.D, Key.F, Key.G, Key.R, Key.Space, Key.LeftShift];
        Key[] cole = [Key.Up, Key.Left, Key.Down, Key.Right, Key.J, Key.K, Key.I, Key.L, Key.RightShift, Key.NumPad8, Key.NumPad4, Key.NumPad2, Key.NumPad6, Key.NumPad0];
        foreach (var k in jake)
        {
            input.OnKeyDown(k);
            Assert.NotEqual(default, input.Player1);
            Assert.Equal(default, input.Player2);
            input.OnKeyUp(k);
        }
        foreach (var k in cole)
        {
            input.OnKeyDown(k);
            Assert.NotEqual(default, input.Player2);
            Assert.Equal(default, input.Player1);
            input.OnKeyUp(k);
        }
        Assert.Equal(new Controls(Dy: -1), Press(input, Key.W, 0));
        Assert.Equal(new Controls(Dy: 1), Press(input, Key.Down, 1));
        Assert.Equal(new Controls(Punch: true), Press(input, Key.F, 0));
        Assert.Equal(new Controls(Kick: true), Press(input, Key.K, 1));
        Assert.Equal(new Controls(Elbow: true), Press(input, Key.I, 1));
        Assert.Equal(new Controls(Jump: true), Press(input, Key.Space, 0));
        // Through the game: up the keyboard is up the screen, for both.
        var w = Street(1, 2);
        double y0 = w.Brothers[0].Y, y1 = w.Brothers[1].Y;
        input.OnKeyDown(Key.W);
        input.OnKeyDown(Key.Up);
        Run(w, 30, input.Player1, input.Player2);
        Assert.True(w.Brothers[0].Y < y0);
        Assert.True(w.Brothers[1].Y < y1);
        input.OnKeyUp(Key.W);
        input.OnKeyUp(Key.Up);
        input.OnKeyDown(Key.D2);
        Assert.True(input.Start2);
        Assert.False(input.Start1);
    }

    private static Controls Press(InputHandler input, Key key, int player)
    {
        input.OnKeyDown(key);
        var c = input.For(player);
        input.OnKeyUp(key);
        return c;
    }

    [Fact]
    public void A_shift_let_go_as_capslock_is_let_go_all_the_same()
    {
        using var input = new InputHandler();
        input.OnKeyDown(Key.LeftShift, PhysicalKey.ShiftLeft);
        Assert.True(input.Player1.Jump);
        input.OnKeyUp(Key.CapsLock, PhysicalKey.ShiftLeft);
        Assert.False(input.Player1.Jump);
        input.OnKeyDown(Key.RightShift, PhysicalKey.ShiftRight);
        Assert.True(input.Player2.Jump);
        input.OnKeyUp(Key.CapsLock, PhysicalKey.ShiftRight);
        Assert.False(input.Player2.Jump);
    }

    // ───────────────────────────── the missions themselves ─────────────────────────────

    [Fact]
    public void Every_mission_is_laid_out_fairly()
    {
        double jump = Sim.JumpX * 2 * Sim.JumpSpeed / Sim.Gravity;
        for (int m = 1; m <= Stage.Missions; m++)
        {
            var s = Stage.Of(m);
            Assert.Equal(m, s.Mission);
            // Each fight further on than the last, the last at the very end; the gang come in one after another.
            for (int i = 1; i < s.Waves.Count; i++) Assert.True(s.Waves[i].Lock > s.Waves[i - 1].Lock + 500);
            Assert.Equal(s.Length - Field.Width, s.Waves[^1].Lock);
            Assert.All(s.Waves, wave => Assert.True(wave.Spawns.Zip(wave.Spawns.Skip(1)).All(p => p.Second.Delay >= p.First.Delay)));
            // Weapons lie on solid ground; the brothers start on it.
            Assert.All(s.Items, i => Assert.True(s.Floor(i.X, i.Y)));
            Assert.True(s.Floor(160, Field.Middle - 40) && s.Floor(280, Field.Middle + 40));
            Assert.Equal(m == Stage.Missions, s.Final);
            Assert.Equal(m == Stage.Missions, s.Waves[^1].Spawns.Any(p => p.Kind == Kind.Magnus));
        }
        Assert.NotNull(Stage.Of(1).Garage);
        Assert.True(Stage.Of(1).Garage < Field.Width);
        var factory = Stage.Of(2);
        Assert.True(factory.Ladder > factory.Waves[^1].Lock);
        var belt = factory.Belt!.Value;
        Assert.True(belt.Speed < 0);
        Assert.False(factory.Floor(belt.X0 - 10, belt.Y0 + 10), "a pit where the belt ends");
        var bridge = Stage.Of(3).Bridge!.Value;
        Assert.True(bridge.Gap1 - bridge.Gap0 < jump - 30, "the gap can be jumped");
        Assert.False(Stage.Of(3).Floor((bridge.X0 + bridge.X1) / 2, bridge.Y1 + 20));
        Assert.True(Stage.Of(3).Floor(bridge.Gap0 - 10, (bridge.Y0 + bridge.Y1) / 2));
        var hideout = Stage.Of(4);
        Assert.All(hideout.Spikes, s => Assert.InRange(s.Reach, 100, Field.Front - Field.Back + 4));
        Assert.Contains(hideout.Spikes, s => s.Reach < Field.Front - Field.Back - 60);
        Assert.InRange(hideout.Statue!.Value, hideout.Waves[^2].Lock, hideout.Waves[^2].Lock + Field.Width);
    }

    [Fact]
    public void Every_sound_the_game_asks_for_is_there()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "Sounds");
        foreach (var s in Enum.GetValues<Sfx>().Where(s => s != Sfx.MusicStop))
            Assert.True(File.Exists(Path.Combine(folder, s + ".wav")), $"no sound for {s}");
    }

    [Fact]
    public void A_copy_of_the_world_plays_out_exactly_the_same()
    {
        var w = new World();
        Sim.Start(w, 2);
        Pilot[] p = [new(), new()];
        for (int i = 0; i < 600; i++) Sim.Step(w, p[0].Decide(w, 0), p[1].Decide(w, 1));
        var c = w.Copy();
        for (int i = 0; i < 1200; i++)
        {
            var (a, b) = (p[0].Decide(w, 0), p[1].Decide(w, 1));
            Sim.Step(w, a, b);
            Sim.Step(c, a, b);
        }
        Assert.Equal(w.Brothers[0].X, c.Brothers[0].X);
        Assert.Equal(w.Brothers[1].Hp, c.Brothers[1].Hp);
        Assert.Equal(w.Brothers.Sum(b => b.Score), c.Brothers.Sum(b => b.Score));
        Assert.Equal(w.Foes.Count, c.Foes.Count);
        Assert.Equal(w.Items.Count, c.Items.Count);
        Assert.Equal(w.Cam, c.Cam);
    }

    // ───────────────────────────── the computer brothers ─────────────────────────────

    /// <summary>The computer plays (one brother or both) through the missions, as the probe does; the furthest mission reached.</summary>
    private static (World W, int Mission) Autoplay(int brothers, int minutes)
    {
        var w = new World();
        Sim.Start(w, brothers);
        Pilot[] p = [new(), new()];
        for (int t = 0; t < 60 * 60 * minutes && w.Playing.Any() && !(w.Ending && w.EndT > 2); t++)
        {
            Sim.Step(w, p[0].Decide(w, 0), brothers > 1 ? p[1].Decide(w, 1) : default);
            if (w.Cleared) { Sim.StartMission(w, w.Mission + 1); p[0].Reset(); p[1].Reset(); }
        }
        return (w, w.Mission);
    }

    [Fact]
    public void The_computer_brother_fights_his_way_through_the_city_slums()
    {
        var (w, mission) = Autoplay(1, 4);
        Assert.True(mission >= 2);
        Assert.True(w.Brothers[0].Score > 10000);
    }

    [Fact]
    public void Both_computer_brothers_fight_through_all_four_missions_to_the_duel_and_one_wins_Ruby()
    {
        var (w, mission) = Autoplay(2, 12);
        Assert.Equal(Stage.Missions, mission);
        Assert.True(w.Ending);
        Assert.InRange(w.Winner, 0, 1);
        Assert.True(w.Brothers[1 - w.Winner].Ko, "the loser lies beaten");
    }

    [Fact]
    public void Standing_still_mashing_punch_doesnt_get_far()
    {
        var w = new World();
        Sim.Start(w, 1);
        for (int t = 0; t < 60 * 60 * 4 && w.Playing.Any(); t++)
        {
            Sim.Step(w, new Controls(Punch: t % 8 < 4));
            if (w.Cleared) Sim.StartMission(w, w.Mission + 1);
        }
        Assert.False(w.Playing.Any());
        Assert.Equal(1, w.Mission);
    }
}
