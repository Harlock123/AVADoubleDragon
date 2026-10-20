using Avalonia;
using Avalonia.Media;
using AVADoubleDragon.Engine;

namespace AVADoubleDragon.Views;

/// <summary>
/// The missions: the far backdrop drifting slowly, the near wall (brick, steel, trees or stone) with the garage, the
/// ladder and the spike recesses in it, the floor band and what's on it (the belt, the pit, the gorge and its bridge),
/// and everyone and everything on the street, drawn back to front.
/// </summary>
public sealed partial class GameCanvas
{
    private const double Top = 64, SpikeTall = 64;

    /// <summary>The whole mission at a camera position: backdrop, wall, floor, then the people and things on it from the back of the street to the front.</summary>
    private void DrawMission(DrawingContext dc, World w, double cam, bool people = true)
    {
        var s = w.Stage;
        using (dc.PushClip(new Rect(0, Top, Field.Width, Field.Height - Top)))
        {
            switch (s.Scenery)
            {
                case Scenery.Street: DrawStreet(dc, w, cam); break;
                case Scenery.Factory: DrawFactory(dc, w, cam); break;
                case Scenery.Forest: DrawForest(dc, w, cam); break;
                default: DrawHideout(dc, w, cam); break;
            }
            if (!people) return;
            // Shadows on the floor, then everything standing on it, back to front.
            foreach (var f in w.Everyone)
                if (f.Active && f.Act is not (Act.Fall or Act.Down or Act.Climb) && !(f.IsBrother && f.Wait > 0) && w.Stage.Floor(f.X, f.Y))
                    Shadow(dc, f.X - cam, f.Y, f.Z, 26 * LookOf(f).Size * LookOf(f).Build);
            foreach (var i in w.Items)
                if (i.Holder < 0 && i.Blast <= 0 && i.Z > 4 && w.Stage.Floor(i.X, i.Y)) Shadow(dc, i.X - cam, i.Y, i.Z, 16);
            var sprites = new List<(double Y, Action Draw)>();
            foreach (var f in w.Everyone)
                if (Visible(f.X, cam)) sprites.Add((f.Y + Layer(f), () => DrawFighter(dc, w, f, cam)));
            foreach (var i in w.Items)
                if (i.Holder < 0 && Visible(i.X, cam)) sprites.Add((i.Y - 0.2, () => DrawItem(dc, i, cam)));
            foreach (var sp in s.Spikes)
                if (Visible(sp.X, cam))
                {
                    double e = Stage.Thrust(sp, w.Clock);
                    sprites.Add((Field.Back + sp.Reach * e, () => DrawSpike(dc, sp, e, cam)));
                }
            if (s.Statue is { } st && Visible(st, cam)) sprites.Add((Field.Back + 70, () => DrawStatue(dc, w, st - cam)));
            if (s.Ruby is { } && Visible(w.RubyX, cam)) sprites.Add((w.RubyY, () => DrawRuby(dc, w, cam)));
            if (s.Bridge is { } br && br.X1 > cam && br.X0 < cam + Field.Width) sprites.Add((br.Y1 + 1, () => BridgeFront(dc, br, cam)));
            foreach (var (_, draw) in sprites.OrderBy(p => p.Y)) draw();
            DrawFlights(dc, w, cam);
        }
    }

    /// <summary>A nudge to the drawing order: a blow (and the weapon swinging it) in front of whoever it lands on, a held man behind his holder.</summary>
    private static double Layer(Fighter f) => f.Act switch
    {
        Act.Punch or Act.Kick or Act.Elbow or Act.JumpKick or Act.Swing or Act.Headbutt or Act.Charge => Sim.DepthReach + 1,
        Act.Down => -0.5,
        Act.Held => -0.6,
        _ => 0,
    };

    private static bool Visible(double x, double cam) => x > cam - 260 && x < cam + Field.Width + 260;

    private static void Sky(DrawingContext dc, uint top, uint low, double bottom)
    {
        int bands = 14;
        double h = (bottom - Top) / bands;
        for (int i = 0; i < bands; i++)
            dc.FillRectangle(Brush(Blend(top, low, i / (bands - 1.0))), new Rect(0, Top + i * h, Field.Width, h + 1));
    }

    /// <summary>The tiles of something repeating along the street every <paramref name="every"/>, at a parallax.</summary>
    private static IEnumerable<(int K, double X)> Along(double cam, double every, double parallax = 1, double pad = 1)
    {
        double scroll = cam * parallax;
        for (int k = (int)Math.Floor(scroll / every - pad); k <= (int)Math.Floor((scroll + Field.Width) / every + pad); k++)
            yield return (k, k * every - scroll);
    }

    // ───────────────────────────── mission 1: the city slums ─────────────────────────────

    private void DrawStreet(DrawingContext dc, World w, double cam)
    {
        Sky(dc, 0xFF1A0E34, 0xFFE0703A, 330);
        dc.FillEllipse(820 - cam * 0.02, 250, 56, 0xFFFFD27A);
        // The far city, and the near tenements.
        foreach (var (k, x) in Along(cam, 90, 0.25))
        {
            double h = 120 + Hash(k, 1, 3) % 140;
            dc.FillRectangle(Brush(0xFF2A1A3A), new Rect(x, 330 - h, 86, h + 20));
            for (int r = 0; r < h / 26; r++)
            for (int c = 0; c < 3; c++)
                if (Hash(k * 5 + c, r, 4) % 4 == 0) dc.FillRectangle(Brush(0xFFF0C060), new Rect(x + 12 + c * 24, 345 - h + r * 26, 9, 11));
        }
        foreach (var (k, x) in Along(cam, 380))
        {
            double top = 110 + Hash(k, 2, 5) % 80;
            uint brick = (Hash(k, 3, 5) % 3) switch { 0 => 0xFF8A3A2A, 1 => 0xFF6A4A3A, _ => 0xFF7A2E34 };
            dc.DrawRectangle(Brush(brick), Pen(Ink, 3), new Rect(x, top, 380, Field.Back - top + 4));
            for (double y = top + 12; y < Field.Back; y += 14)
            {
                dc.DrawLine(Pen(Darker(brick, 0.8), 1.5), new Point(x, y), new Point(x + 380, y));
                for (double bx = x + ((int)((y - top) / 14) % 2) * 15; bx < x + 380; bx += 30) dc.DrawLine(Pen(Darker(brick, 0.8), 1.5), new Point(bx, y), new Point(bx, y + 14));
            }
            dc.FillRectangle(Brush(Darker(brick, 0.7)), new Rect(x, top - 10, 380, 14));
            // Two rows of windows, a fire escape, and the shop front at the bottom.
            for (int r = 0; r < 2; r++)
            for (int c = 0; c < 3; c++)
            {
                double wx = x + 40 + c * 110, wy = top + 30 + r * 90;
                bool lit = Hash(k * 3 + c, r, 6) % 3 == 0;
                dc.DrawRectangle(Brush(lit ? 0xFFF0C878 : 0xFF1A1A2A), Pen(Ink, 3), new Rect(wx, wy, 50, 62));
                dc.DrawLine(Pen(Ink, 2), new Point(wx + 25, wy), new Point(wx + 25, wy + 62));
                dc.DrawLine(Pen(Ink, 2), new Point(wx, wy + 31), new Point(wx + 50, wy + 31));
            }
            if (Hash(k, 4, 5) % 2 == 0)
            {
                double fx = x + 140;
                for (int r = 0; r < 2; r++)
                {
                    double fy = top + 96 + r * 90;
                    dc.DrawLine(Pen(0xFF1A1A20, 4), new Point(fx - 30, fy), new Point(fx + 110, fy));
                    for (double rx = fx - 30; rx <= fx + 110; rx += 14) dc.DrawLine(Pen(0xFF1A1A20, 2), new Point(rx, fy), new Point(rx, fy - 22));
                    dc.DrawLine(Pen(0xFF1A1A20, 3), new Point(fx - 30, fy - 22), new Point(fx + 110, fy - 22));
                    dc.DrawLine(Pen(0xFF1A1A20, 3), new Point(fx + 100, fy), new Point(fx - 20, fy + 90));
                }
            }
            double shop = Field.Back - 92;
            dc.DrawRectangle(Brush(0xFF3A3A44), Pen(Ink, 3), new Rect(x + 30, shop, 320, 92));
            for (double sy = shop + 8; sy < Field.Back; sy += 9) dc.DrawLine(Pen(0xFF54545E, 2), new Point(x + 32, sy), new Point(x + 348, sy));
            string[] signs = ["PAWN", "BAR", "HOTEL", "LIQUOR", "DINER"];
            uint neon = Hash(k, 7, 5) % 2 == 0 ? 0xFFFF4AA0 : 0xFF4AE8FF;
            dc.DrawRectangle(Brush(0xFF14101C), Pen(neon, 3), new Rect(x + 120, shop - 40, 140, 30), 4, 4);
            Text(dc, signs[Hash(k, 8, 5) % signs.Length], new Point(x + 190, shop - 26), 26, neon, center: true, shadow: false);
            // A tag sprayed across the shutter.
            if (Hash(k, 9, 5) % 3 != 0)
                Comic(dc, ((int)(Hash(k, 10, 5) % 4)) switch { 0 => "TALONS", 1 => "KANE", 2 => "AVA", _ => "SLUMS" }, new Point(x + 190, shop + 50), 30,
                    Hash(k, 11, 5) % 2 == 0 ? 0xFF6AE04A : 0xFFFFB020, center: true, lean: 0.3, outline: 3);
        }
        if (w.Stage.Garage is { } g && Visible(g, cam)) Garage(dc, g - cam, w.Garage);
        // The pavement along the wall, the kerb, and the road.
        double walk = Field.Back + 56;
        dc.FillRectangle(Brush(0xFF8A8A90), new Rect(0, Field.Back, Field.Width, walk - Field.Back));
        foreach (var (_, x) in Along(cam, 80)) dc.DrawLine(Pen(0xFF6A6A72, 2), new Point(x, Field.Back), new Point(x - 20, walk));
        dc.DrawLine(Pen(0xFF6A6A72, 2), new Point(0, Field.Back + 26), new Point(Field.Width, Field.Back + 26));
        dc.FillRectangle(Brush(0xFFB0B0B8), new Rect(0, walk - 6, Field.Width, 8));
        dc.FillRectangle(Brush(0xFF34343C), new Rect(0, walk + 2, Field.Width, Field.Height - walk));
        Halftone(dc, 0xFF000000, 0.22, walk + 2, 120, cam);
        foreach (var (k, x) in Along(cam, 160)) dc.FillRectangle(Brush(0xFFE8C040), new Rect(x, 636, 70, 7));
        foreach (var (k, x) in Along(cam, 700))
        {
            dc.DrawEllipse(Brush(0xFF26262C), Pen(Ink, 2), new Point(x + 300, 690), 40, 12);
            dc.DrawLine(Pen(0xFF4A4A52, 2), new Point(x + 270, 690), new Point(x + 330, 690));
            dc.DrawLine(Pen(0xFF26262C, 2), new Point(x + 500, 600), new Point(x + 560, 616));
        }
        dc.DrawLine(Pen(Ink, 3), new Point(0, Field.Back), new Point(Field.Width, Field.Back));
    }

    /// <summary>The gang's garage: its door rolls up as they come out.</summary>
    private static void Garage(DrawingContext dc, double x, double open)
    {
        double top = 150;
        dc.DrawRectangle(Brush(0xFF5A5A62), Pen(Ink, 3), new Rect(x - 140, top, 280, Field.Back - top + 4));
        dc.DrawRectangle(Brush(0xFFE8C040), Pen(Ink, 3), new Rect(x - 90, top + 26, 180, 40), 4, 4);
        Text(dc, "GARAGE", new Point(x, top + 46), 36, Ink, center: true, shadow: false);
        double doorTop = Field.Back - 190, h = 190;
        dc.FillRectangle(Brush(0xFF0C0A10), new Rect(x - 110, doorTop, 220, h));
        double rolled = h * (1 - open);
        dc.DrawRectangle(Brush(0xFF9A9AA4), Pen(Ink, 3), new Rect(x - 110, doorTop, 220, Math.Max(10, rolled)));
        for (double y = doorTop + 14; y < doorTop + rolled; y += 16) dc.DrawLine(Pen(0xFF6A6A74, 3), new Point(x - 108, y), new Point(x + 108, y));
        dc.DrawRectangle(null, Pen(Ink, 4), new Rect(x - 112, doorTop - 2, 224, h + 2));
    }

    // ───────────────────────────── mission 2: the factory ─────────────────────────────

    private void DrawFactory(DrawingContext dc, World w, double cam)
    {
        var s = w.Stage;
        dc.FillRectangle(Brush(0xFF2A2C34), new Rect(0, Top, Field.Width, Field.Back - Top));
        // Steel panels and girders, pipes, the furnace windows' glow.
        foreach (var (k, x) in Along(cam, 120))
        {
            dc.DrawRectangle(null, Pen(0xFF1C1E24, 3), new Rect(x, Top + 150, 120, Field.Back - Top - 150));
            for (double ry = Top + 162; ry < Field.Back; ry += 40) { dc.FillEllipse(x + 8, ry, 2.5, 0xFF44464E); dc.FillEllipse(x + 112, ry, 2.5, 0xFF44464E); }
        }
        foreach (var (k, x) in Along(cam, 560))
        {
            double glow = 0.75 + 0.25 * Math.Sin(_clock * 3 + k);
            dc.DrawRectangle(Brush(Blend(0xFF6A2A0A, 0xFFFFA030, glow)), Pen(Ink, 3), new Rect(x + 300, Top + 190, 140, 90), 6, 6);
            for (int b = 1; b < 4; b++) dc.DrawLine(Pen(Ink, 3), new Point(x + 300 + b * 35, Top + 190), new Point(x + 300 + b * 35, Top + 280));
            dc.FillRectangle(Brush(0xFF3A3C46), new Rect(x + 40, Top, 46, Field.Back - Top));
            dc.DrawRectangle(null, Pen(Ink, 3), new Rect(x + 40, Top, 46, Field.Back - Top));
            for (double gy = Top + 20; gy < Field.Back - 20; gy += 46)
            {
                dc.DrawLine(Pen(0xFF22242C, 3), new Point(x + 42, gy), new Point(x + 84, gy + 40));
                dc.DrawLine(Pen(0xFF22242C, 3), new Point(x + 84, gy), new Point(x + 42, gy + 40));
            }
        }
        Limb(dc, new Point(-10, Top + 40), new Point(Field.Width + 10, Top + 40), 16, 0xFF7A5A3A);
        Limb(dc, new Point(-10, Top + 72), new Point(Field.Width + 10, Top + 72), 10, 0xFF5A7A6A);
        // The catwalk high up, where the ladder goes.
        dc.FillRectangle(Brush(0xFF4A4C56), new Rect(0, Top + 108, Field.Width, 14));
        foreach (var (_, x) in Along(cam, 40)) dc.DrawLine(Pen(0xFF4A4C56, 3), new Point(x, Top + 108), new Point(x, Top + 84));
        dc.DrawLine(Pen(0xFF5A5C66, 4), new Point(0, Top + 84), new Point(Field.Width, Top + 84));
        dc.DrawLine(Pen(Ink, 3), new Point(0, Top + 122), new Point(Field.Width, Top + 122));
        // Hazard stripes along the foot of the wall.
        using (dc.PushClip(new Rect(0, Field.Back - 22, Field.Width, 22)))
            foreach (var (_, x) in Along(cam, 40)) Poly(dc, 0xFFE8C040, null, 0, new Point(x, Field.Back), new Point(x + 20, Field.Back), new Point(x + 42, Field.Back - 22), new Point(x + 22, Field.Back - 22));
        dc.FillRectangle(Brush(0x80000000), new Rect(0, Field.Back - 22, Field.Width, 0));
        if (s.Ladder is { } lx && Visible(lx, cam))
        {
            double x = lx - cam;
            Limb(dc, new Point(x - 20, Top + 108), new Point(x - 20, Field.Back + 4), 6, 0xFF9AA0AA);
            Limb(dc, new Point(x + 20, Top + 108), new Point(x + 20, Field.Back + 4), 6, 0xFF9AA0AA);
            for (double ry = Top + 124; ry < Field.Back; ry += 24) Limb(dc, new Point(x - 20, ry), new Point(x + 20, ry), 4, 0xFF9AA0AA);
            if (w.Wave >= s.Waves.Count && (int)(_clock * 3) % 2 == 0) Comic(dc, "UP!", new Point(x, Top + 160), 40, Yellow, center: true);
        }
        // The floor: steel tread plate.
        dc.FillRectangle(Brush(0xFF55575F), new Rect(0, Field.Back, Field.Width, Field.Height - Field.Back));
        foreach (var (k, x) in Along(cam, 96))
        {
            dc.DrawLine(Pen(0xFF3E4048, 2), new Point(x, Field.Back), new Point(x - 40, Field.Height));
            for (double y = Field.Back + 20; y < Field.Height; y += 30)
                dc.DrawLine(Pen(0xFF6A6C74, 2), new Point(x + 30 - (y - Field.Back) * 0.4, y), new Point(x + 42 - (y - Field.Back) * 0.4, y - 5));
        }
        for (double y = Field.Back + 70; y < Field.Height; y += 70) dc.DrawLine(Pen(0xFF3E4048, 2), new Point(0, y), new Point(Field.Width, y));
        Halftone(dc, 0xFF000000, 0.25, Field.Height - 130, 130, cam);
        if (s.Belt is { } belt) Conveyor(dc, belt, cam);
        foreach (var h in s.Holes) Pit(dc, h, cam);
        dc.DrawLine(Pen(Ink, 3), new Point(0, Field.Back), new Point(Field.Width, Field.Back));
    }

    private void Conveyor(DrawingContext dc, Belt b, double cam)
    {
        double x0 = b.X0 - cam, x1 = b.X1 - cam;
        if (x1 < -40 || x0 > Field.Width + 40) return;
        dc.DrawRectangle(Brush(0xFF1E1E24), Pen(Ink, 3), new Rect(x0, b.Y0, x1 - x0, b.Y1 - b.Y0));
        using (dc.PushClip(new Rect(x0, b.Y0, x1 - x0, b.Y1 - b.Y0)))
        {
            double off = (_clock * b.Speed) % 36;
            for (double x = x0 + off - 36; x < x1 + 36; x += 36)
            {
                dc.DrawLine(Pen(0xFF3A3A44, 6), new Point(x, b.Y0), new Point(x - 16, b.Y1));
                Poly(dc, 0xFFE8C040, null, 0, new Point(x - 4, b.Y0 + 40), new Point(x - 14, b.Y0 + 48), new Point(x - 4, b.Y0 + 56));
            }
        }
        // The steel lip along its front, the rollers at its ends.
        dc.DrawRectangle(Brush(0xFF8A8C96), Pen(Ink, 3), new Rect(x0 - 6, b.Y1, x1 - x0 + 12, 12));
        for (double x = x0 + 20; x < x1 - 10; x += 60) dc.FillEllipse(x, b.Y1 + 6, 3, 0xFF4A4C56);
        dc.DrawEllipse(Brush(0xFF6A6C76), Pen(Ink, 3), new Point(x1, (b.Y0 + b.Y1) / 2), 10, (b.Y1 - b.Y0) / 2);
        Comic(dc, "<<<", new Point((x0 + x1) / 2, b.Y1 + 32), 26, Alpha(Yellow, 0.7), center: true, lean: 0);
    }

    /// <summary>A hole in the factory floor at the end of the belt, striped round its edge.</summary>
    private static void Pit(DrawingContext dc, Hole h, double cam)
    {
        double x0 = h.X0 - cam, x1 = h.X1 - cam;
        if (x1 < -20 || x0 > Field.Width + 20) return;
        double y0 = Math.Max(h.Y0, Field.Back);
        dc.FillRectangle(Brush(0xFF050406), new Rect(x0, y0, x1 - x0, h.Y1 - y0));
        dc.FillRectangle(Brush(0xFF2A0A04), new Rect(x0 + 6, h.Y1 - 18, x1 - x0 - 12, 14));
        using (dc.PushClip(new Rect(x0 - 8, h.Y1, x1 - x0 + 16, 10)))
            for (double x = x0 - 8; x < x1 + 8; x += 16) Poly(dc, 0xFFE8C040, null, 0, new Point(x, h.Y1 + 10), new Point(x + 8, h.Y1 + 10), new Point(x + 16, h.Y1), new Point(x + 8, h.Y1));
        dc.DrawRectangle(null, Pen(Ink, 3), new Rect(x0, y0, x1 - x0, h.Y1 - y0 + 10));
    }

    // ───────────────────────────── mission 3: the forest ─────────────────────────────

    private void DrawForest(DrawingContext dc, World w, double cam)
    {
        var s = w.Stage;
        Sky(dc, 0xFF16304A, 0xFF7AB0A0, 340);
        foreach (var (k, x) in Along(cam, 300, 0.15))
            Poly(dc, 0xFF2A4A4A, null, 0, new Point(x - 60, 340), new Point(x + 120 + Hash(k, 1, 7) % 80, 210 + Hash(k, 2, 7) % 60), new Point(x + 360, 340));
        foreach (var (k, x) in Along(cam, 70, 0.4))
        {
            double h = 120 + Hash(k, 3, 7) % 90;
            Poly(dc, 0xFF1E4030, null, 0, new Point(x - 36, 380), new Point(x, 380 - h), new Point(x + 36, 380));
        }
        dc.FillRectangle(Brush(0xFF1E4030), new Rect(0, 378, Field.Width, Field.Back - 378));
        foreach (var (k, x) in Along(cam, 210))
        {
            double tx = x + Hash(k, 4, 7) % 80;
            if (s.Bridge is { } b0 && tx + cam > b0.X0 - 40 && tx + cam < b0.X1 + 40) continue;
            double wide = 26 + Hash(k, 5, 7) % 18;
            dc.DrawRectangle(Brush(0xFF4A3424), Pen(Ink, 3), new Rect(tx - wide / 2, Top - 10, wide, Field.Back - Top + 14));
            dc.DrawLine(Pen(0xFF33241A, 3), new Point(tx - wide / 4, Top + 40), new Point(tx - wide / 5, Field.Back));
            Poly(dc, 0xFF4A3424, Ink, 3, new Point(tx - wide / 2, Field.Back - 20), new Point(tx - wide, Field.Back + 6), new Point(tx + wide, Field.Back + 6), new Point(tx + wide / 2, Field.Back - 20));
            dc.DrawEllipse(Brush(0xFF2E5A2A), Pen(Ink, 3), new Point(tx, Top + 20), 90, 50);
            dc.DrawEllipse(Brush(0xFF3A6E32), null, new Point(tx - 20, Top + 10), 50, 26);
        }
        // The path: packed earth with grass at its edges.
        dc.FillRectangle(Brush(0xFF8A6A44), new Rect(0, Field.Back, Field.Width, Field.Height - Field.Back));
        dc.FillRectangle(Brush(0xFF4A7A30), new Rect(0, Field.Back, Field.Width, 18));
        dc.FillRectangle(Brush(0xFF4A7A30), new Rect(0, Field.Front + 22, Field.Width, Field.Height - Field.Front - 22));
        foreach (var (k, x) in Along(cam, 50))
        {
            double gx = x + Hash(k, 6, 7) % 40, gy = Field.Back + 30 + Hash(k, 7, 7) % 220;
            Poly(dc, 0xFF5A8A3A, null, 0, new Point(gx - 6, gy), new Point(gx - 2, gy - 12), new Point(gx, gy), new Point(gx + 4, gy - 10), new Point(gx + 6, gy));
            if (Hash(k, 8, 7) % 5 == 0) dc.DrawEllipse(Brush(0xFF9A9A90), Pen(Ink, 2), new Point(gx + 20, gy + 8), 9, 5);
        }
        Halftone(dc, 0xFF000000, 0.18, Field.Height - 120, 120, cam);
        dc.DrawLine(Pen(Ink, 3), new Point(0, Field.Back), new Point(Field.Width, Field.Back));
        if (s.Bridge is { } br) Gorge(dc, br, cam);
    }

    /// <summary>The gorge across the path, the river far below, and the bridge's deck with its broken middle.</summary>
    private void Gorge(DrawingContext dc, (double X0, double X1, double Y0, double Y1, double Gap0, double Gap1) b, double cam)
    {
        double x0 = b.X0 - cam, x1 = b.X1 - cam;
        if (x1 < -60 || x0 > Field.Width + 60) return;
        double top = Field.Back - 30;
        dc.FillRectangle(Brush(0xFF0A0C10), new Rect(x0, top, x1 - x0, Field.Height - top));
        for (int i = 0; i < 6; i++) dc.FillRectangle(Brush(Alpha(0xFF2A3A4A, 0.15 + i * 0.05)), new Rect(x0, top + 90 + i * 40, x1 - x0, 40));
        for (int i = 0; i < 5; i++)
        {
            double gx = x0 + 30 + ((_clock * 40 + i * 57) % (x1 - x0 - 60));
            dc.DrawLine(Pen(0xFF8ACAE8, 2), new Point(gx, 740 + i % 2 * 10), new Point(gx + 18, 740 + i % 2 * 10));
        }
        // The cliff edges, jagged.
        for (int side = 0; side < 2; side++)
        {
            double ex = side == 0 ? x0 : x1, d = side == 0 ? 1 : -1;
            var pts = new List<Point> { new(ex - d * 2, top) };
            for (double y = top; y < Field.Height; y += 24) pts.Add(new Point(ex + d * (6 + Hash((int)y, side, 21) % 14), y + 12));
            pts.Add(new Point(ex - d * 2, Field.Height));
            Poly(dc, 0xFF5A4A3A, Ink, 3, [.. pts]);
        }
        // The deck: planks across the strip, missing in the middle, a few hanging loose.
        double g0 = b.Gap0 - cam, g1 = b.Gap1 - cam;
        void Deck(double from, double to)
        {
            dc.DrawRectangle(Brush(0xFF9A7244), Pen(Ink, 3), new Rect(from, b.Y0, to - from, b.Y1 - b.Y0));
            for (double x = from + 14; x < to; x += 14) dc.DrawLine(Pen(0xFF6A4A2A, 2), new Point(x, b.Y0 + 2), new Point(x, b.Y1 - 2));
            dc.FillRectangle(Brush(0xFF5A3A1E), new Rect(from, b.Y1, to - from, 10));
        }
        Deck(x0 - 4, g0);
        Deck(g1, x1 + 4);
        Limb(dc, new Point(g0 + 4, b.Y1), new Point(g0 + 14, b.Y1 + 60), 8, 0xFF9A7244);
        Limb(dc, new Point(g1 - 6, b.Y1 + 4), new Point(g1 - 20, b.Y1 + 44), 8, 0xFF9A7244);
        // The back rope (the front one is drawn in front of whoever's on the bridge).
        for (double x = x0; x <= x1 + 1; x += (x1 - x0) / 4) Limb(dc, new Point(x, b.Y0), new Point(x, b.Y0 - 40), 4, 0xFF6A4A2A);
        Limb(dc, new Point(x0, b.Y0 - 38), new Point(g0, b.Y0 - 26), 3, 0xFFC8A060);
        Limb(dc, new Point(g1, b.Y0 - 26), new Point(x1, b.Y0 - 38), 3, 0xFFC8A060);
    }

    private static void BridgeFront(DrawingContext dc, (double X0, double X1, double Y0, double Y1, double Gap0, double Gap1) b, double cam)
    {
        double x0 = b.X0 - cam, x1 = b.X1 - cam, g0 = b.Gap0 - cam, g1 = b.Gap1 - cam;
        for (double x = x0; x <= x1 + 1; x += (x1 - x0) / 4)
            if (x < g0 - 4 || x > g1 + 4) Limb(dc, new Point(x, b.Y1 + 4), new Point(x, b.Y1 - 40), 5, 0xFF6A4A2A);
        Limb(dc, new Point(x0, b.Y1 - 38), new Point(g0, b.Y1 - 24), 4, 0xFFC8A060);
        Limb(dc, new Point(g1, b.Y1 - 24), new Point(x1, b.Y1 - 38), 4, 0xFFC8A060);
        Limb(dc, new Point(g0, b.Y1 - 24), new Point(g0 + 10, b.Y1 + 30), 3, 0xFFC8A060);
    }

    // ───────────────────────────── mission 4: the hideout ─────────────────────────────

    private void DrawHideout(DrawingContext dc, World w, double cam)
    {
        var s = w.Stage;
        dc.FillRectangle(Brush(0xFF3A3242), new Rect(0, Top, Field.Width, Field.Back - Top));
        // Stone blocks.
        for (double y = Top; y < Field.Back; y += 34)
        {
            int row = (int)((y - Top) / 34);
            dc.DrawLine(Pen(0xFF241E2C, 3), new Point(0, y), new Point(Field.Width, y));
            foreach (var (_, x) in Along(cam, 90)) dc.DrawLine(Pen(0xFF241E2C, 3), new Point(x + row % 2 * 45, y), new Point(x + row % 2 * 45, y + 34));
        }
        Halftone(dc, 0xFF000000, 0.3, Top, 160, cam);
        foreach (var (k, x) in Along(cam, 400))
        {
            // Pillars, banners with the gang's talon, and torches.
            dc.DrawRectangle(Brush(0xFF4E4458), Pen(Ink, 3), new Rect(x, Top, 60, Field.Back - Top));
            dc.FillRectangle(Brush(0xFF5E5468), new Rect(x + 8, Top, 10, Field.Back - Top));
            double bx = x + 230;
            Poly(dc, 0xFF8A1A24, Ink, 3, new Point(bx - 40, Top + 40), new Point(bx + 40, Top + 40), new Point(bx + 40, Top + 250), new Point(bx, Top + 220), new Point(bx - 40, Top + 250));
            Talon(dc, bx, Top + 130, 30);
            double tx = x + 130, flick = Math.Sin(_clock * 13 + k * 3) * 4;
            dc.DrawRectangle(Brush(0xFF2A2A2A), Pen(Ink, 2), new Rect(tx - 6, 250, 12, 26));
            Poly(dc, 0xFFFF8A20, Ink, 2, new Point(tx - 12, 250), new Point(tx + flick, 214), new Point(tx + 12, 250));
            Poly(dc, 0xFFFFE070, null, 0, new Point(tx - 6, 250), new Point(tx + flick * 0.5, 228), new Point(tx + 6, 250));
            dc.FillEllipse(tx, 236, 60, 0x18FFA040);
        }
        foreach (var sp in s.Spikes)
            if (Visible(sp.X, cam))
            {
                // The recess the spiked block comes out of.
                double x = sp.X - cam;
                dc.DrawRectangle(Brush(0xFF0C0A10), Pen(0xFF6A6A72, 5), new Rect(x - Spike.Wide / 2 - 4, Field.Back - SpikeTall - 8, Spike.Wide + 8, SpikeTall + 10));
            }
        // The boss's hall: red curtains, and the pillar she's tied to.
        if (s.Ruby is { } rx && Visible(rx, cam))
        {
            double x = rx - cam;
            for (int i = 0; i < 6; i++) dc.FillRectangle(Brush(i % 2 == 0 ? 0xFF7A1018 : 0xFF5A0A12), new Rect(x - 460 + i * 40, Top, 40, Field.Back - Top));
            dc.DrawRectangle(Brush(0xFF6A5E78), Pen(Ink, 3), new Rect(x - 18, Top + 40, 36, Field.Back - Top - 14));
        }
        // The floor: flagstones.
        dc.FillRectangle(Brush(0xFF4E4658), new Rect(0, Field.Back, Field.Width, Field.Height - Field.Back));
        for (double y = Field.Back; y < Field.Height; y += 50) dc.DrawLine(Pen(0xFF34303E, 3), new Point(0, y), new Point(Field.Width, y));
        foreach (var (_, x) in Along(cam, 110))
            for (double y = Field.Back; y < Field.Height; y += 50) dc.DrawLine(Pen(0xFF34303E, 3), new Point(x + (y - Field.Back) % 100 * 0.55, y), new Point(x + (y - Field.Back) % 100 * 0.55 - 12, y + 50));
        Halftone(dc, 0xFF000000, 0.3, Field.Height - 140, 140, cam);
        dc.DrawLine(Pen(Ink, 3), new Point(0, Field.Back), new Point(Field.Width, Field.Back));
        foreach (var sp in s.Spikes)
        {
            if (!Visible(sp.X, cam)) continue;
            // The iron runners each spiked block slides out along, and a red warning across its path as it's about to come.
            double x = sp.X - cam;
            foreach (double run in new[] { x - Spike.Wide / 2 + 14, x + Spike.Wide / 2 - 14 })
            {
                dc.DrawLine(Pen(0xFF2A2630, 7), new Point(run, Field.Back), new Point(run, Field.Back + sp.Reach + 6));
                dc.DrawLine(Pen(0xFF8A8A96, 3), new Point(run - 1, Field.Back), new Point(run - 1, Field.Back + sp.Reach + 6));
            }
            if ((int)(_clock * 4) % 2 == 0 && Stage.Thrust(sp, w.Clock + 0.4) > 0.05 && Stage.Thrust(sp, w.Clock) < 0.5)
                dc.FillRectangle(Brush(0x60FF2020), new Rect(x - Spike.Wide / 2, Field.Back, Spike.Wide, sp.Reach));
        }
    }

    /// <summary>The Black Talons' mark: three claws.</summary>
    private static void Talon(DrawingContext dc, double x, double y, double r)
    {
        for (int i = -1; i <= 1; i++)
            Poly(dc, 0xFF14101A, null, 0, new Point(x + i * r * 0.55 - r * 0.18, y - r), new Point(x + i * r * 0.55 + r * 0.22, y - r), new Point(x + i * r * 0.4 + r * 0.05, y + r * 0.9));
        dc.DrawEllipse(null, Pen(0xFFE8C040, 3), new Point(x, y), r * 1.25, r * 1.25);
    }

    /// <summary>A spiked block thrust out of the wall across the floor: its top, its face, and the spikes along its face.</summary>
    private static void DrawSpike(DrawingContext dc, Spike s, double e, double cam)
    {
        if (e <= 0) return;
        double x = s.X - cam, half = Spike.Wide / 2, front = Field.Back + s.Reach * e, h = SpikeTall;
        // Its shadow on the floor, its top running back into the wall, its face, and the spikes on its face.
        dc.FillRectangle(Brush(0x50000000), new Rect(x - half + 8, Field.Back, Spike.Wide + 4, front - Field.Back + 6));
        // The top: dark where it leaves the wall, catching the light towards its front edge, its stones running back.
        double top = front - Field.Back;
        for (int i = 0; i < 6; i++)
            dc.FillRectangle(Brush(Blend(0xFF4A4456, 0xFF9A94A6, i / 5.0)), new Rect(x - half, Field.Back - h + top * i / 6, Spike.Wide, top / 6 + 1));
        for (int i = 1; i < 4; i++) dc.DrawLine(Pen(0xFF5A5466, 2), new Point(x - half + i * Spike.Wide / 4, Field.Back - h), new Point(x - half + i * Spike.Wide / 4, front - h));
        dc.DrawRectangle(null, Pen(Ink, 3), new Rect(x - half, Field.Back - h, Spike.Wide, top));
        dc.DrawLine(Pen(0xFFC8C2D4, 3), new Point(x - half + 3, front - h - 3), new Point(x + half - 3, front - h - 3));
        if (e < 1)
            for (int i = 0; i < 3; i++) dc.DrawLine(Pen(Alpha(0xFFFFFFFF, 0.5), 2), new Point(x - half - 14 - i * 6, front - h - 10 - i * 18), new Point(x - half - 14 - i * 6, front - h - 40 - i * 18));
        dc.DrawRectangle(Brush(0xFF5A5466), Pen(Ink, 3), new Rect(x - half, front - h, Spike.Wide, h));
        for (int i = 0; i < 5; i++)
        {
            double sx = x - half + 12 + i * 24, sy = front - h / 2;
            Poly(dc, 0xFFE0E0E8, Ink, 2.5, new Point(sx - 9, sy - 14), new Point(sx + 9, sy - 14), new Point(sx + 3, sy + 22), new Point(sx, sy + 28));
            Poly(dc, 0xFF9A9AA8, null, 0, new Point(sx + 1, sy - 13), new Point(sx + 8, sy - 13), new Point(sx + 2, sy + 20));
        }
    }

    /// <summary>The statue barring the boss's hall: a stone warrior with a dragon's head, sinking into the floor once the way is open.</summary>
    private void DrawStatue(DrawingContext dc, World w, double x)
    {
        double sink = w.Statue * 330, foot = Field.Back + 70;
        using var _ = dc.PushClip(new Rect(x - 200, 0, 400, foot + 4));
        if (w.Statue > 0 && w.Statue < 1)
            for (int i = 0; i < 4; i++) dc.FillEllipse(x - 90 + i * 60 + Math.Sin(_clock * 20 + i) * 6, foot - 8, 18, 0xA0B0A898);
        using var __ = dc.PushTransform(Matrix.CreateTranslation(x, foot + sink));
        uint stone = 0xFF8A8494, dark = 0xFF5E5A68;
        dc.DrawRectangle(Brush(dark), Pen(Ink, 3), new Rect(-110, -40, 220, 40));
        Poly(dc, stone, Ink, 3, new Point(-60, -40), new Point(-44, -150), new Point(44, -150), new Point(60, -40));
        Limb(dc, new Point(-30, -40), new Point(-30, -130), 26, stone);
        Limb(dc, new Point(30, -40), new Point(30, -130), 26, stone);
        Poly(dc, stone, Ink, 3, new Point(-58, -140), new Point(58, -140), new Point(66, -250), new Point(-66, -250));
        Limb(dc, new Point(-66, -240), new Point(-90, -160), 20, stone);
        Limb(dc, new Point(66, -240), new Point(84, -300), 20, stone);
        Limb(dc, new Point(84, -330), new Point(84, -100), 8, dark);
        Poly(dc, dark, Ink, 3, new Point(84, -360), new Point(96, -330), new Point(72, -330));
        // The dragon head.
        Poly(dc, stone, Ink, 3, new Point(-30, -248), new Point(-36, -300), new Point(-10, -320), new Point(46, -306), new Point(54, -286), new Point(20, -270), new Point(28, -248));
        Poly(dc, dark, Ink, 2.5, new Point(-20, -312), new Point(-40, -350), new Point(-4, -318));
        Poly(dc, dark, Ink, 2.5, new Point(0, -316), new Point(-6, -356), new Point(16, -314));
        dc.FillEllipse(18, -298, 5, w.Statue > 0 ? 0xFFFF4020 : 0xFF2A2830);
        Talon(dc, 0, -196, 22);
    }

    /// <summary>Ruby: tied to the pillar in the boss's hall, and then running to the winner.</summary>
    private void DrawRuby(DrawingContext dc, World w, double cam)
    {
        double x = w.RubyX - cam, y = w.RubyY;
        bool free = w.Ending && w.EndT > 1;
        bool moving = free && !Sim.Together(w);
        var r = Guard with { Ua1 = -0.6, Fa1 = -0.2, Ua2 = -0.5, Fa2 = -0.1 };
        int d = -1;
        if (free)
        {
            var winner = w.Brothers[w.Winner];
            d = winner.X > w.RubyX ? 1 : -1;
            double ph = w.EndT * 11;
            r = moving
                ? Guard with { Th1 = 0.5 * Math.Sin(ph), Sh1 = 0.5 * Math.Sin(ph) - 0.4, Th2 = -0.5 * Math.Sin(ph), Sh2 = -0.5 * Math.Sin(ph) - 0.4, Ua1 = 0.6 * Math.Sin(ph), Fa1 = 1.0, Ua2 = -0.6 * Math.Sin(ph), Fa2 = 0.8 }
                : Guard with { Ua1 = 2.3, Fa1 = 2.9, Ua2 = 2.0, Fa2 = 2.8, Lean = 0.15 };
        }
        Shadow(dc, x, y, 0, 22);
        Figure(dc, x, y, d, RubyLook, r);
        if (!free)
        {
            Limb(dc, new Point(x - 22, y - 92), new Point(x + 22, y - 86), 3, 0xFFC8A060);
            Limb(dc, new Point(x - 20, y - 70), new Point(x + 20, y - 66), 3, 0xFFC8A060);
            if (w.Foes.Any(f => f.Kind == Kind.Magnus) && (int)(_clock * 1.5) % 2 == 0) Balloon(dc, x - 20, y - 200, "HELP!", 30, tailLeft: false);
        }
        else if (!moving)
        {
            Heart(dc, x - d * 28, y - 190 - Math.Sin(_clock * 4) * 6, 22);
            Balloon(dc, x + d * 30, y - 250, $"MY HERO, {w.Brothers[w.Winner].Name}!", 28, tailLeft: d < 0);
        }
    }

    private static void Heart(DrawingContext dc, double x, double y, double r)
    {
        dc.DrawEllipse(Brush(0xFFE83A5A), Pen(Ink, 3), new Point(x - r * 0.5, y), r * 0.6, r * 0.6);
        dc.DrawEllipse(Brush(0xFFE83A5A), Pen(Ink, 3), new Point(x + r * 0.5, y), r * 0.6, r * 0.6);
        Poly(dc, 0xFFE83A5A, Ink, 3, new Point(x - r * 1.05, y + r * 0.15), new Point(x + r * 1.05, y + r * 0.15), new Point(x, y + r * 1.3));
        dc.FillRectangle(Brush(0xFFE83A5A), new Rect(x - r * 0.9, y - r * 0.1, r * 1.8, r * 0.35));
    }
}
