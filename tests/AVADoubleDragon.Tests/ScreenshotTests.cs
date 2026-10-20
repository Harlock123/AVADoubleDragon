using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using AVADoubleDragon.Engine;
using AVADoubleDragon.Views;

namespace AVADoubleDragon.Tests;

/// <summary>
/// Draws the game in a real (headless) window at the moments worth showing. Set DRAGON_SHOTS to a folder to save
/// them as PNGs: the README's screenshots come from here.
/// </summary>
public sealed class ScreenshotTests
{
    private static (Window W, GameCanvas Canvas, GameEngine G) Open()
    {
        var g = new GameEngine(HighScoreTable.Load(Path.Combine(Path.GetTempPath(), "avadoubledragon-ui-" + Guid.NewGuid().ToString("N")))) { Silent = true };
        var canvas = new GameCanvas(g, new SoundManager(silent: true), startTimer: false);
        var w = new Window { Width = 1024, Height = 768, Content = canvas };
        w.Show();
        Pump();
        return (w, canvas, g);
    }

    private static void Pump()
    {
        for (int i = 0; i < 10; i++) Dispatcher.UIThread.RunJobs();
    }

    private static void Shot(Window w, GameCanvas c, string name)
    {
        c.InvalidateVisual();
        Pump();
        var frame = w.CaptureRenderedFrame();
        Assert.NotNull(frame);
        var dir = Environment.GetEnvironmentVariable("DRAGON_SHOTS");
        if (string.IsNullOrEmpty(dir)) return;
        Directory.CreateDirectory(dir);
        frame.Save(Path.Combine(dir, $"AVADoubleDragon-{name}.png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
    }

    /// <summary>The computer plays (both brothers, if both are in) until the moment comes.</summary>
    private static bool PlayUntil(GameEngine g, GameCanvas c, Func<World, bool> done, int maxSeconds = 300)
    {
        for (int t = 0; t < 60 * maxSeconds && g.State == GameState.Playing; t++)
        {
            if (g.Phase is Phase.Play or Phase.Duel && done(g.World)) { c.Advance(0.016); return true; }
            g.Tick(g.Auto(0), g.Auto(1));
            if (t % 3 == 0) c.Advance(1 / 20.0);
        }
        return false;
    }

    private static void Moment(int mission, int brothers, string name, Func<World, bool> moment, Action<World>? setUp = null, int maxSeconds = 300)
    {
        var (w, c, g) = Open();
        g.Go(mission, brothers);
        foreach (var b in g.World.Brothers) b.Lives = 9;
        setUp?.Invoke(g.World);
        Assert.True(PlayUntil(g, c, moment, maxSeconds), $"never saw the moment for {name}: {g.State} {g.Phase} mission {g.World.Mission} wave {g.World.Wave} cam {g.World.Cam:0} {string.Join(",", g.World.Brothers.Select(b => $"{b.Act}@{b.X:0},{b.Y:0} L{b.Lives}"))}");
        Shot(w, c, name);
    }

    private static int OnScreen(World w) => w.Foes.Count(f => f.Active && !f.Ko && f.X > w.Cam + 40 && f.X < w.Cam + Field.Width - 40);

    [AvaloniaFact]
    public void The_title_and_the_high_scores()
    {
        var (w, c, g) = Open();
        g.Idle(30);
        c.Advance(2.1);
        Shot(w, c, "title");
        g.HighScores.Insert("AVA", 71850, 4);
        g.Idle((int)(60 * (GameEngine.TitleIdleSeconds + GameEngine.AttractSeconds)) + 10);
        for (int i = 0; i < 60 * 600 && g.State != GameState.HighScores; i++) g.Idle(1);
        Assert.Equal(GameState.HighScores, g.State);
        c.Advance(0.3);
        Shot(w, c, "high-scores");
    }

    [AvaloniaFact]
    public void Both_brothers_brawling_in_the_street() =>
        Moment(1, 2, "street-brawl", w =>
            w.Wave == 1 && OnScreen(w) >= 3 && w.Brothers.All(b => b.Act is Act.Punch or Act.Kick or Act.Elbow or Act.JumpKick or Act.Knee or Act.Swing)
            && w.Foes.Any(f => f.Act is Act.Hurt or Act.Fly or Act.Dazed));

    [AvaloniaFact]
    public void A_knee_to_the_face() =>
        Moment(1, 1, "grab", w => w.Brothers[0] is { Act: Act.Knee, T: > 0.1 and < 0.16 } && OnScreen(w) >= 2);

    [AvaloniaFact]
    public void A_bat_swung() =>
        Moment(1, 1, "weapon", w => w.Brothers[0] is { Act: Act.Swing, Struck: true, T: > 0.2 and < 0.26 } && OnScreen(w) >= 2 && w.Foes.Any(f => f.Act == Act.Fly),
            w => w.Items.Add(new Item { Id = w.NextId++, Kind = ItemKind.Bat, Holder = 0 }));

    [AvaloniaFact]
    public void Fighting_on_the_factory_belt() =>
        Moment(2, 2, "conveyor", w =>
        {
            if (w.Stage.Belt is not { } belt) return false;
            bool on(Fighter f) => f.X > belt.X0 && f.X < belt.X1 && f.Y < belt.Y1 && f.Z <= 0;
            return w.Wave == 1 && w.Everyone.Count(f => f.Active && on(f)) >= 2 && OnScreen(w) >= 2
                   && (w.Brothers.Any(b => b.Act is Act.Punch or Act.Kick or Act.Swing or Act.Elbow) || w.Foes.Any(f => f.Act is Act.Hurt or Act.Fly));
        }, w =>
        {
            // Into the second fight, by the belt.
            w.Wave = 1;
            w.Cam = w.Stage.Waves[1].Lock;
            foreach (var b in w.Brothers) { b.X = w.Cam + 420 + b.Id * 260; b.Y = Field.Back + 36 + b.Id * 30; }
            foreach (var (kind, x, y) in new[] { (Kind.Dutch, 560.0, 40.0), (Kind.Rooker, 860.0, 60.0) })
                w.Foes.Add(new Fighter { Id = w.NextId++, Kind = kind, X = w.Cam + x, Y = Field.Back + y, Hp = Sim.Toughness(kind), MaxHp = Sim.Toughness(kind), Act = Act.Stand, In = true });
        });

    [AvaloniaFact]
    public void The_broken_bridge() =>
        Moment(3, 2, "bridge", w =>
        {
            var b = w.Stage.Bridge!.Value;
            return w.Cam > b.X0 - 600 && w.Everyone.Any(f => f.Act == Act.Fall && f.T is > 0.15 and < 0.4 || f.IsBrother && f.Act is Act.Jump && f.X > b.Gap0 - 30 && f.X < b.Gap1 + 30 && f.Z > 40);
        });

    [AvaloniaFact]
    public void The_spiked_walls() =>
        Moment(4, 2, "spikes", w => w.Wave == 1 && w.Stage.Spikes.Any(s => Stage.Thrust(s, w.Clock) > 0.9 && s.X > w.Cam + 100 && s.X < w.Cam + Field.Width - 100) && OnScreen(w) >= 2);

    [AvaloniaFact]
    public void The_boss_and_his_gun() =>
        Moment(4, 2, "boss", w => w.Foes.Any(f => f is { Kind: Kind.Magnus, Act: Act.Shoot }) && w.Shots.Count >= 2 && w.Brothers.All(b => b.Act != Act.Down),
            w =>
            {
                for (int i = 0; i < w.Stage.Waves.Count - 1; i++) w.Wave = i + 1;
                w.Statue = 1;
                w.Cam = w.Stage.Length - Field.Width;
                foreach (var b in w.Brothers) { b.X = w.Cam + 200 + b.Id * 100; b.Y = Field.Middle + b.Id * 60 - 30; }
                w.Items.Clear();
            });

    [AvaloniaFact]
    public void The_brothers_duel()
    {
        var (w, c, g) = Open();
        g.Go(4, 2);
        var world = g.World;
        world.Wave = world.Stage.Waves.Count;
        world.Statue = 1;
        world.Cam = world.Stage.Length - Field.Width;
        world.Items.Clear();
        foreach (var b in world.Brothers) b.X = world.Cam + 300 + b.Id * 200;
        Assert.True(PlayUntil(g, c, x => x.Duel && x.Brothers.Any(b => b.Hp < b.MaxHp) && x.Brothers.All(b => b.Act != Act.Down) && x.Brothers.Any(b => b.Act is Act.Kick or Act.Punch or Act.JumpKick) && x.Brothers.Any(b => b.Act is Act.Hurt or Act.Fly && b.T > 0.1), 120), $"{g.State} {g.Phase} {world.Duel} {world.Ending} {string.Join(",", world.Brothers.Select(b => $"{b.Act} {b.Hp}"))}");
        Assert.Equal(Phase.Duel, g.Phase);
        Shot(w, c, "duel");
    }
}
