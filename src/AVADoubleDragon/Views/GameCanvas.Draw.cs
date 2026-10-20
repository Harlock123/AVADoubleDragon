using System.Globalization;
using Avalonia;
using Avalonia.Media;
using AVADoubleDragon.Engine;

namespace AVADoubleDragon.Views;

public sealed partial class GameCanvas
{
    private static readonly Dictionary<uint, IBrush> BrushCache = [];
    private static readonly Dictionary<(uint, double), Pen> PenCache = [];
    private static readonly Dictionary<double, Geometry> DotCache = [];
    private static readonly Typeface Retro = new(new FontFamily("avares://AVADoubleDragon/Assets/Fonts#VT323"));
    private static readonly Typeface Bold = new(new FontFamily("fonts:Inter#Inter"), FontStyle.Normal, FontWeight.Bold);

    public static IBrush Brush(uint argb)
    {
        if (!BrushCache.TryGetValue(argb, out var b))
            BrushCache[argb] = b = new SolidColorBrush(Color.FromUInt32(argb)).ToImmutable();
        return b;
    }

    private static Pen Pen(uint argb, double width = 1)
    {
        if (!PenCache.TryGetValue((argb, width), out var p))
            PenCache[(argb, width)] = p = new Pen(Brush(argb), width, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        return p;
    }

    private const uint Ink = 0xFF0C0A10, White = 0xFFF6F2E8, Yellow = 0xFFFFDC3A, Red = 0xFFE8382A, Dim = 0xFF8A94B8, Cyan = 0xFF6AE8FF, Paper = 0xFFFFF6D8;

    private static void Text(DrawingContext dc, string text, Point at, double size, uint colour, bool center = false, bool right = false, bool shadow = true)
    {
        var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Retro, size, Brush(colour));
        double x = center ? at.X - ft.Width / 2 : right ? at.X - ft.Width : at.X;
        if (shadow) dc.DrawText(new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Retro, size, Brush(0xC0000000)), new Point(x + 2, at.Y - ft.Height / 2 + 2));
        dc.DrawText(ft, new Point(x, at.Y - ft.Height / 2));
    }

    /// <summary>Comic lettering: heavy, leaning, outlined in black with a drop shadow.</summary>
    private static void Comic(DrawingContext dc, string text, Point at, double size, uint fill, bool center = false, double outline = 0, uint shadow = Ink, double lean = 0.2)
    {
        var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Bold, size, Brush(fill));
        if (ft.BuildGeometry(new Point(0, 0)) is not { } g) return;
        double w = ft.Width, h = ft.Height;
        double x = center ? at.X - w / 2 : at.X;
        if (outline <= 0) outline = Math.Max(3, size / 9);
        var m = Matrix.CreateTranslation(0, -h / 2) * Matrix.CreateSkew(-lean, 0) * Matrix.CreateTranslation(x, at.Y);
        using (dc.PushTransform(Matrix.CreateTranslation(outline * 0.8, outline * 0.8) * m)) dc.DrawGeometry(Brush(shadow), Pen(shadow, outline * 2), g);
        using (dc.PushTransform(m))
        {
            dc.DrawGeometry(null, Pen(Ink, outline * 2), g);
            dc.DrawGeometry(Brush(fill), null, g);
        }
    }

    private static void Poly(DrawingContext dc, uint fill, uint? edge, double width, params Point[] pts)
    {
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            c.BeginFigure(pts[0], true);
            foreach (var p in pts.Skip(1)) c.LineTo(p);
            c.EndFigure(true);
        }
        dc.DrawGeometry(fill == 0 ? null : Brush(fill), edge is { } e ? Pen(e, width) : null, g);
    }

    private static uint Hash(int x, int y, int salt)
    {
        uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663) ^ (uint)(salt * 83492791);
        h ^= h >> 13;
        h *= 0x5bd1e995;
        h ^= h >> 15;
        return h;
    }

    private static uint Alpha(uint colour, double a) => (uint)(Math.Clamp(a, 0, 1) * 255) << 24 | (colour & 0x00FFFFFF);

    private static uint Blend(uint a, uint b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        uint Ch(int shift) => (uint)Math.Round(((a >> shift) & 0xFF) * (1 - t) + ((b >> shift) & 0xFF) * t);
        return 0xFF000000 | Ch(16) << 16 | Ch(8) << 8 | Ch(0);
    }

    /// <summary>
    /// Ben-Day dots: a band of dots growing from nothing at the top to full at the bottom, the printed-comic shading.
    /// Built once for each height and moved into place.
    /// </summary>
    private static void Halftone(DrawingContext dc, uint colour, double alpha, double y, double height, double scroll)
    {
        const double gap = 12;
        if (!DotCache.TryGetValue(height, out var g))
        {
            var sg = new StreamGeometry();
            using (var c = sg.Open())
            {
                for (int row = 0; row * gap < height; row++)
                for (int col = -1; col * gap < Field.Width + gap * 2; col++)
                {
                    double r = gap * 0.5 * (row * gap / height);
                    if (r < 0.6) continue;
                    double cx = col * gap + (row % 2) * gap / 2, cy = row * gap;
                    c.BeginFigure(new Point(cx + r, cy), true);
                    c.ArcTo(new Point(cx - r, cy), new Size(r, r), 0, false, SweepDirection.Clockwise);
                    c.ArcTo(new Point(cx + r, cy), new Size(r, r), 0, false, SweepDirection.Clockwise);
                    c.EndFigure(true);
                }
            }
            DotCache[height] = g = sg;
        }
        using var _ = dc.PushTransform(Matrix.CreateTranslation(-(scroll % gap) - gap, y));
        dc.DrawGeometry(Brush(Alpha(colour, alpha)), null, g);
    }

    /// <summary>A speech balloon, its tail pointing down to the speaker.</summary>
    private static void Balloon(DrawingContext dc, double x, double y, string text, double size, bool tailLeft)
    {
        var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Bold, size, Brush(Ink));
        double w = ft.Width + 36, h = ft.Height + 20;
        double tx = tailLeft ? x - w / 2 + 24 : x + w / 2 - 24;
        Poly(dc, 0xFFFFFFFF, Ink, 3, new Point(tx - 10, y + h / 2 - 6), new Point(tx + (tailLeft ? -18 : 18), y + h / 2 + 26), new Point(tx + 10, y + h / 2 - 6));
        dc.DrawEllipse(Brush(0xFFFFFFFF), Pen(Ink, 3), new Point(x, y), w / 2, h / 2);
        dc.FillRectangle(Brush(0xFFFFFFFF), new Rect(tx - 8, y + h / 2 - 9, 16, 6));
        dc.DrawText(ft, new Point(x - ft.Width / 2, y - ft.Height / 2));
    }

    /// <summary>A caption box, the yellow kind a comic's narrator speaks from.</summary>
    private static void Caption(DrawingContext dc, Rect r, string text, double size, uint fill = Yellow)
    {
        dc.FillRectangle(Brush(Ink), r.Translate(new Vector(5, 5)));
        dc.DrawRectangle(Brush(fill), Pen(Ink, 3), r);
        var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Bold, size, Brush(Ink));
        dc.DrawText(ft, new Point(r.Center.X - ft.Width / 2, r.Center.Y - ft.Height / 2));
    }

    /// <summary>Lines bursting out from a point: the background of an action panel.</summary>
    private static void Burst(DrawingContext dc, double x, double y, uint a, uint b, int rays = 28, double turn = 0)
    {
        dc.FillRectangle(Brush(a), new Rect(0, 0, Field.Width, Field.Height));
        for (int i = 0; i < rays; i += 2)
        {
            double a0 = turn + i * Math.PI * 2 / rays, a1 = turn + (i + 1) * Math.PI * 2 / rays;
            Poly(dc, b, null, 0, new Point(x, y), new Point(x + 1600 * Math.Cos(a0), y + 1600 * Math.Sin(a0)), new Point(x + 1600 * Math.Cos(a1), y + 1600 * Math.Sin(a1)));
        }
    }

    private void DrawScreen(DrawingContext dc)
    {
        var g = Engine;
        switch (g.State)
        {
            case GameState.Title: DrawTitle(dc); break;
            case GameState.HighScores: DrawBackdrop(dc); DrawHighScores(dc); break;
            case GameState.EnterInitials: DrawBackdrop(dc); DrawInitials(dc); break;
            case GameState.GameOver: DrawGameOver(dc); break;
            default:
                var w = g.World;
                if (g.Phase == Phase.Start) { DrawCard(dc, w); break; }
                DrawMission(dc, w, w.Cam);
                DrawHud(dc, w);
                DrawWords(dc, w);
                break;
        }
        if (g.State == GameState.Paused)
        {
            dc.FillRectangle(Brush(0xC0000000), new Rect(0, 0, Field.Width, Field.Height));
            Comic(dc, "PAUSED", new Point(512, 320), 90, Yellow, center: true);
            Text(dc, "ANY KEY TO CARRY ON - ESC TO QUIT", new Point(512, 410), 30, White, center: true);
        }
        if (g.State == GameState.Attract && g.Phase != Phase.Start)
            Text(dc, "DEMONSTRATION - PRESS 1 OR 2 TO PLAY", new Point(512, Field.Height - 24), 26, Yellow, center: true);
    }

    // ───────────────────────────── the scores, the health bars, the clock ─────────────────────────────

    private void DrawHud(DrawingContext dc, World w)
    {
        var g = Engine;
        dc.FillRectangle(Brush(Ink), new Rect(0, 0, Field.Width, Top));
        dc.DrawLine(Pen(0xFF3A3448, 2), new Point(0, Top - 1), new Point(Field.Width, Top - 1));
        for (int i = 0; i < 2; i++)
        {
            var b = w.Brothers[i];
            double x = i == 0 ? 16 : Field.Width - 336;
            uint colour = i == 0 ? 0xFF4A8AF0 : 0xFFF04A3A;
            if (!b.Joined || b.Act == Act.Out)
            {
                Text(dc, b.Name, new Point(x, 18), 30, colour);
                if (b.Joined) Text(dc, $"{b.Score:000000}", new Point(x + 90, 18), 30, Dim);
                bool attract = g.State == GameState.Attract;
                if (!attract && (int)(_clock * 2) % 2 == 0) Text(dc, b.Joined ? $"PRESS {i + 1} TO COME BACK" : $"PRESS {i + 1} TO JOIN", new Point(x, 46), 26, Yellow);
                continue;
            }
            Text(dc, b.Name, new Point(x, 18), 30, colour);
            Text(dc, $"{b.Score:000000}", new Point(x + 90, 18), 30, White);
            Text(dc, $"x{b.Lives}", new Point(x + 270, 18), 30, Yellow);
            // The health bar: twelve blocks.
            int blocks = (int)Math.Ceiling(12.0 * Math.Max(0, b.Hp) / b.MaxHp);
            for (int k = 0; k < 12; k++)
            {
                uint fill = k < blocks ? (blocks <= 3 ? Red : blocks <= 6 ? Yellow : 0xFF4AE070) : 0xFF2A2A30;
                dc.DrawRectangle(Brush(fill), Pen(0xFF101014, 2), new Rect(x + k * 26, 38, 23, 16), 2, 2);
            }
        }
        Text(dc, $"MISSION {w.Mission}", new Point(512, 16), 28, Dim, center: true);
        bool hurry = w.TimeLeft <= Sim.WarnAt && !w.Duel && !w.Ending;
        string time = w.Duel || w.Ending ? "--" : $"{Math.Ceiling(Math.Max(0, w.TimeLeft)):00}";
        Text(dc, $"TIME {time}", new Point(512, 44), 32, hurry ? ((int)(_clock * 4) % 2 == 0 ? Red : Yellow) : White, center: true);
        // The giants' strength, while one is about.
        if (w.Foes.FirstOrDefault(f => f.Big && f.Active && !f.Ko) is { } big)
        {
            dc.DrawRectangle(Brush(0xD0000000), Pen(Ink, 2), new Rect(312, Top + 8, 400, 28), 4, 4);
            Text(dc, big.Name, new Point(322, Top + 22), 26, 0xFFF0C030);
            dc.FillRectangle(Brush(0xFF3A1010), new Rect(440, Top + 15, 262, 14));
            dc.FillRectangle(Brush(Red), new Rect(440, Top + 15, 262.0 * Math.Max(0, big.Hp) / big.MaxHp, 14));
        }
    }

    /// <summary>The words across the play: GO, a brother joining, the clear, the duel, the end.</summary>
    private void DrawWords(DrawingContext dc, World w)
    {
        var g = Engine;
        var s = w.Stage;
        bool open = !w.Locked && w.Wave < s.Waves.Count && w.Cam < s.Waves[w.Wave].Lock - 4 || w.Wave >= s.Waves.Count && !s.Final && s.Ladder == null;
        if (g.Phase == Phase.Play && open && !w.Foes.Any(f => f.Active && !f.Ko) && (int)(_clock * 2.5) % 2 == 0)
        {
            Comic(dc, "GO", new Point(880, 170), 56, Yellow, center: true);
            Poly(dc, Yellow, Ink, 4, new Point(920, 150), new Point(970, 172), new Point(920, 194));
        }
        if (g.Message is { } m)
        {
            Caption(dc, new Rect(262, 120, 500, 70), m, 40);
            if (g.SubMessage is { } sub) Text(dc, sub, new Point(512, 214), 34, White, center: true);
        }
        if (g.Phase == Phase.Cleared && g.PhaseTime > 0.8)
        {
            dc.DrawRectangle(Brush(0xE0101018), Pen(Ink, 3), new Rect(312, 236, 400, 60), 6, 6);
            Text(dc, $"TIME BONUS {Sim.TimeBonus(w)}", new Point(512, 266), 34, White, center: true);
        }
        if (g.Phase == Phase.Ending && w.Winner >= 0)
        {
            var b = w.Brothers[w.Winner];
            if (w.EndT > 1.2) Caption(dc, new Rect(212, 110, 600, 70), $"RUBY GOES WITH {b.Name}!", 40);
            if (w.EndT > 3) Text(dc, $"{Sim.RubyPoints} POINTS", new Point(512, 210), 34, Yellow, center: true);
        }
    }

    // ───────────────────────────── the mission's card ─────────────────────────────

    private static readonly string[] Briefs =
    [
        "THE BLACK TALONS HAVE TAKEN RUBY. THEIR TRAIL STARTS AT THE GARAGE.",
        "THROUGH THE TALONS' FACTORY. MIND THE BELT, AND CLIMB OUT BY THE LADDER.",
        "OVER THE GORGE BY THE BROKEN BRIDGE. ONE SLIP AND YOU'RE GONE.",
        "THE HIDEOUT. SPIKED WALLS, A STONE GUARDIAN, AND MAGNUS WITH HIS GUN.",
    ];

    /// <summary>The card before each mission: its number and name over the brothers, back to back.</summary>
    private void DrawCard(DrawingContext dc, World w)
    {
        var s = w.Stage;
        double k = Math.Min(1, Engine.PhaseTime / 0.35);
        uint bg = s.Scenery switch { Scenery.Street => 0xFF4A1A2A, Scenery.Factory => 0xFF2A2C3A, Scenery.Forest => 0xFF1E3A2A, _ => 0xFF2A1A3A };
        Burst(dc, 720, 470, bg, Blend(bg, 0xFFE8C040, 0.25), 32, _clock * 0.05);
        Halftone(dc, Ink, 0.35, Field.Height - 300, 300, 0);
        Dragon(dc, 700, 230, 0.75, 0xFF8A1A24, _clock);
        Figure(dc, 640, 720, 1, JakeLook, Guard, false);
        Figure(dc, 800, 720, -1, ColeLook, Guard, false);
        dc.DrawRectangle(null, Pen(0xFFFFFFFF, 26), new Rect(0, 0, Field.Width, Field.Height));
        dc.DrawRectangle(null, Pen(Ink, 6), new Rect(16, 16, Field.Width - 32, Field.Height - 32));
        double slide = (1 - k) * -600;
        Comic(dc, "MISSION", new Point(70 + slide, 170), 90, White, shadow: Red);
        Comic(dc, w.Mission.ToString(), new Point(460 + slide, 190), 200, Yellow, shadow: Red, outline: 14);
        Caption(dc, new Rect(60, 330, 470, 66), s.Place, 38, Paper);
        string brief = Briefs[(w.Mission - 1) % Briefs.Length];
        int cut = brief.IndexOf(' ', brief.Length / 2);
        Text(dc, brief[..cut], new Point(64, 440), 30, White);
        Text(dc, brief[(cut + 1)..], new Point(64, 472), 30, White);
        Text(dc, $"TIME {s.Time:0} EACH SECTION", new Point(64, 520), 30, Cyan);
        if (Engine.State == GameState.Attract) Text(dc, "DEMONSTRATION", new Point(946, 712), 28, Yellow, right: true);
    }

    /// <summary>A long red dragon, coiling: the brothers' emblem.</summary>
    private static void Dragon(DrawingContext dc, double cx, double cy, double scale, uint colour, double clock)
    {
        var pts = new List<Point>();
        for (int i = 0; i <= 40; i++)
        {
            double t = i / 40.0;
            pts.Add(new Point(cx + (-420 + 800 * t) * scale, cy + (70 * Math.Sin(t * 9 + clock * 1.2) * (0.4 + 0.6 * t) - 40 * t) * scale));
        }
        for (int i = 1; i < pts.Count; i++)
        {
            double t = i / 40.0, wd = (6 + 34 * Math.Sin(Math.PI * Math.Min(1, t * 1.3)) ) * scale;
            dc.DrawLine(Pen(Ink, wd + 7), pts[i - 1], pts[i]);
        }
        for (int i = 1; i < pts.Count; i++)
        {
            double t = i / 40.0, wd = (6 + 34 * Math.Sin(Math.PI * Math.Min(1, t * 1.3))) * scale;
            dc.DrawLine(Pen(colour, wd), pts[i - 1], pts[i]);
            if (i % 3 == 0 && i < 38)
            {
                var a = pts[i - 1];
                var b = pts[i];
                var n = new Vector(b.Y - a.Y, a.X - b.X);
                n /= Math.Max(1, n.Length);
                var top = new Point((a.X + b.X) / 2 + n.X * wd * 0.5, (a.Y + b.Y) / 2 + n.Y * wd * 0.5);
                Poly(dc, 0xFFE8C040, Ink, 2, top - new Vector(6, 0) * scale, top + n * 18 * scale, top + new Vector(6, 0) * scale);
            }
        }
        // The head.
        var h = pts[^1];
        using var _ = dc.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(h.X, h.Y));
        Poly(dc, colour, Ink, 3, new Point(-20, -22), new Point(40, -26), new Point(78, -8), new Point(74, 4), new Point(30, 6), new Point(56, 22), new Point(10, 26), new Point(-24, 18));
        Poly(dc, 0xFFE8C040, Ink, 2.5, new Point(-6, -22), new Point(-30, -58), new Point(10, -24));
        Poly(dc, 0xFFE8C040, Ink, 2.5, new Point(12, -24), new Point(4, -62), new Point(26, -25));
        dc.FillEllipse(30, -12, 6, 0xFFFFE040);
        dc.FillEllipse(31, -12, 2.5, Ink);
        Poly(dc, 0xFFF6F2E8, null, 0, new Point(40, 4), new Point(46, 14), new Point(52, 4));
        Poly(dc, 0xFFFF8A20, null, 0, new Point(78, -2), new Point(120, -18 + Math.Sin(clock * 9) * 5), new Point(112, 2), new Point(130, 12), new Point(80, 4));
    }

    // ───────────────────────────── title, game over and scores ─────────────────────────────

    private void DrawTitle(DrawingContext dc)
    {
        double cx = Field.Width / 2;
        Sky(dc, 0xFF0A0614, 0xFF5A1A2A, 520);
        dc.FillRectangle(Brush(0xFF0A0614), new Rect(0, 0, Field.Width, Top));
        foreach (var (k, x) in Along(_clock * 12, 80, 1))
        {
            double h = 120 + Hash(k, 1, 3) % 200;
            dc.FillRectangle(Brush(0xFF140C20), new Rect(x, 520 - h, 76, h));
            for (int r = 0; r < h / 24; r++)
            for (int c = 0; c < 3; c++)
                if (Hash(k * 5 + c, r, 4) % 5 == 0) dc.FillRectangle(Brush(0xFFC8903A), new Rect(x + 10 + c * 22, 532 - h + r * 24, 8, 10));
        }
        dc.FillRectangle(Brush(0xFF1A1620), new Rect(0, 520, Field.Width, 248));
        Halftone(dc, Ink, 0.5, 520, 248, 0);
        Dragon(dc, cx + 10, 205, 1.0, 0xFFC0202E, _clock);
        Text(dc, "A V A", new Point(cx, 40), 40, Yellow, center: true);
        Comic(dc, "DOUBLE", new Point(cx - 30, 120), 112, 0xFF4A8AF0, center: true, shadow: Ink);
        Comic(dc, "DRAGON", new Point(cx + 30, 236), 120, Red, center: true, shadow: Yellow);
        // The brothers, back to back.
        Figure(dc, cx - 330, 690, -1, JakeLook, Guard with { Ua1 = 0.9, Fa1 = 2.6 }, false);
        Figure(dc, cx - 200, 690, 1, ColeLook, Guard with { Ua1 = 0.9, Fa1 = 2.6 }, false);
        Figure(dc, cx + 300, 690, -1, LookOf(new Fighter { Kind = Kind.Goliath }), Guard with { Lean = 0.15 }, false);
        Figure(dc, cx + 170, 690, -1, LookOf(new Fighter { Kind = Kind.Vesper }), Guard, false, ItemKind.Whip);
        dc.DrawRectangle(Brush(0xE8100C18), Pen(Ink, 3), new Rect(cx - 330, 330, 660, 150), 8, 8);
        Text(dc, "THE BLACK TALONS HAVE TAKEN RUBY FROM THE STREET.", new Point(cx, 354), 27, White, center: true);
        Text(dc, "THE KANE BROTHERS, JAKE AND COLE, GO AFTER HER:", new Point(cx, 382), 27, White, center: true);
        Text(dc, "FOUR MISSIONS OF FISTS, FEET, BATS AND WHIPS TO MAGNUS.", new Point(cx, 410), 27, White, center: true);
        Text(dc, "AND IF BOTH OF THEM GET THERE... ONLY ONE CAN WIN HER.", new Point(cx, 446), 27, Cyan, center: true);
        if ((int)(_clock * 2) % 2 == 0) Comic(dc, "PRESS 1 OR 2", new Point(cx, 524), 40, White, center: true, lean: 0.12);
        dc.FillRectangle(Brush(0xE0000000), new Rect(0, 700, Field.Width, 68));
        Text(dc, "JAKE: WASD  F PUNCH  G KICK  R ELBOW  SPACE JUMP     COLE: ARROWS  J PUNCH  K KICK  I ELBOW  L JUMP", new Point(cx, 716), 20, 0xFFE0D0B0, center: true);
        Text(dc, "MUSIC: BEETHOVEN'S FIFTH SYMPHONY (1808), ARRANGED    P PAUSE   M SOUND", new Point(cx, 746), 20, Dim, center: true);
    }

    /// <summary>The first mission, empty, drifting by behind the score tables.</summary>
    private void DrawBackdrop(DrawingContext dc)
    {
        var w = Engine.World;
        double cam = 300 + _clock * 30 % Math.Max(1, w.Stage.Length - 1600);
        DrawMission(dc, w, cam, people: false);
        dc.FillRectangle(Brush(Ink), new Rect(0, 0, Field.Width, Top));
    }

    private void DrawGameOver(DrawingContext dc)
    {
        var g = Engine;
        var w = g.World;
        double cx = Field.Width / 2;
        if (g.Completed)
        {
            Burst(dc, cx, 520, 0xFF7A1A3A, 0xFFA02A4A, 30, _clock * 0.08);
            Halftone(dc, Ink, 0.3, 468, 300, 0);
            int win = Math.Max(0, w.Winner);
            using (dc.PushTransform(Matrix.CreateScale(2.0, 2.0) * Matrix.CreateTranslation(-cx, -740)))
            {
                Figure(dc, cx - 50, 740, 1, win == 0 ? JakeLook : ColeLook, Guard with { Ua2 = 1.6, Fa2 = 2.0 });
                Figure(dc, cx + 10, 740, -1, RubyLook, Guard with { Ua1 = 2.3, Fa1 = 2.9, Ua2 = 2.0, Fa2 = 2.8, Lean = 0.15 });
            }
            Heart(dc, cx - 40, 300 + Math.Sin(_clock * 4) * 8, 40);
            Comic(dc, "THE END", new Point(cx, 110), 110, Yellow, center: true, shadow: Red);
            Balloon(dc, cx + 250, 290, $"MY HERO, {w.Brothers[win].Name}!", 30, tailLeft: true);
        }
        else
        {
            Burst(dc, cx, 520, 0xFF3A0A14, 0xFF5A1424, 30, _clock * 0.08);
            Halftone(dc, Ink, 0.4, 468, 300, 0);
            using (dc.PushTransform(Matrix.CreateScale(2.2, 2.2) * Matrix.CreateTranslation(-cx * 1.2 + 200, -900)))
                Figure(dc, cx + 200, 740, -1, LookOf(new Fighter { Kind = Kind.Magnus }), Guard with { Ua1 = 0.9, Fa1 = 1.55, Ua2 = 0.7, Fa2 = 1.5, Lean = -0.1 });
            Balloon(dc, cx + 120, 260, "HA HA HA HA!", 40, tailLeft: false);
            Comic(dc, "GAME OVER", new Point(cx, 120), 110, Red, center: true, shadow: Ink);
        }
        dc.DrawRectangle(Brush(0xE0100C18), Pen(Ink, 3), new Rect(60, 420, 420, 170), 8, 8);
        double y = 456;
        foreach (var b in w.Brothers.Where(b => b.Joined))
        {
            Text(dc, $"{b.Name} {b.Score}", new Point(270, y), 44, b.Id == 0 ? 0xFF6AA0FF : 0xFFFF6A5A, center: true);
            y += 46;
        }
        Text(dc, g.Completed ? "ALL FOUR MISSIONS" : $"REACHED MISSION {w.Mission}", new Point(270, 560), 30, Yellow, center: true);
    }

    private void DrawHighScores(DrawingContext dc)
    {
        var g = Engine;
        double cx = Field.Width / 2;
        dc.FillRectangle(Brush(0x90000000), new Rect(0, 0, Field.Width, Field.Height));
        dc.FillRectangle(Brush(Ink), new Rect(cx - 336, 76, 672, 636));
        dc.DrawRectangle(Brush(0xF0141020), Pen(0xFFFFFFFF, 4), new Rect(cx - 340, 70, 672, 636));
        Comic(dc, "TOUGHEST ON THE STREET", new Point(cx, 124), 46, Yellow, center: true, shadow: Red);
        Text(dc, "RANK  NAME    SCORE   MISSION", new Point(cx, 192), 32, Dim, center: true);
        var entries = g.HighScores.Entries;
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            bool mine = i == g.NewEntryIndex && (int)(_clock * 4) % 2 == 0;
            Text(dc, $"{i + 1,3}.  {e.Initials}  {e.Score,8}      {e.Mission}", new Point(cx, 238 + i * 42), 36, mine ? Red : i == 0 ? Yellow : White, center: true);
        }
        Text(dc, "PRESS 1 OR 2 TO PLAY", new Point(cx, 676), 30, White, center: true);
    }

    private void DrawInitials(DrawingContext dc)
    {
        var g = Engine;
        double cx = Field.Width / 2;
        var b = g.World.Brothers[Math.Max(0, g.EntryPlayer)];
        dc.FillRectangle(Brush(0x90000000), new Rect(0, 0, Field.Width, Field.Height));
        dc.DrawRectangle(Brush(0xF0141020), Pen(0xFFFFFFFF, 4), new Rect(cx - 330, 140, 660, 460));
        Comic(dc, "A NEW HIGH SCORE!", new Point(cx, 200), 56, Yellow, center: true, shadow: Red);
        Text(dc, $"{b.Name}  {b.Score}", new Point(cx, 274), 52, b.Id == 0 ? 0xFF6AA0FF : 0xFFFF6A5A, center: true);
        Text(dc, "ENTER YOUR INITIALS", new Point(cx, 350), 34, Dim, center: true);
        if (g.Initials is { } entry)
        {
            string s = entry.Initials;
            for (int i = 0; i < s.Length; i++)
            {
                bool cur = i == entry.Cursor && (int)(_clock * 3) % 2 == 0;
                Text(dc, s[i].ToString(), new Point(cx - 60 + i * 60, 440), 80, cur ? Red : Yellow, center: true);
                dc.FillRectangle(Brush(i == entry.Cursor ? Red : Dim), new Rect(cx - 82 + i * 60, 480, 44, 4));
            }
        }
        Text(dc, "TYPE, OR UP/DOWN AND SPACE; ENTER WHEN DONE", new Point(cx, 560), 26, White, center: true);
    }
}

internal static class DrawingExtensions
{
    public static void FillEllipse(this DrawingContext dc, double x, double y, double r, uint colour) =>
        dc.DrawEllipse(GameCanvas.Brush(colour), null, new Point(x, y), r, r);
    public static void FillRectangle(this DrawingContext dc, IBrush brush, Rect rect, double radius) => dc.FillRectangle(brush, rect, (float)radius);
}
