using Avalonia;
using Avalonia.Media;
using AVADoubleDragon.Engine;

namespace AVADoubleDragon.Views;

/// <summary>
/// The fighters: a jointed figure posed for every move, drawn in flat colour with heavy black outlines, and dressed as
/// a brother, one of the gang, or the girl. The weapons, the bullets and the flashes of a blow.
/// </summary>
public sealed partial class GameCanvas
{
    private enum Hair { Spiky, Cap, Mohawk, Ponytail, Bald, Bandana, Shaved, Beret, Long }

    /// <summary>How a figure is dressed and built.</summary>
    private readonly record struct Look(uint Skin, uint HairColour, Hair Hair, uint Shirt, uint Pants, uint Shoes, uint Belt, bool Bare, double Build, double Size, uint Trim);

    private static readonly Look JakeLook = new(0xFFF0C090, 0xFF5A3418, Hair.Spiky, 0xFF2A62D8, 0xFF1E2A5A, 0xFF3A2A20, 0xFFE8E0D0, true, 1.0, 1.0, 0xFFF4F0E6);
    private static readonly Look ColeLook = new(0xFFE8B888, 0xFFF0C848, Hair.Spiky, 0xFFD8342A, 0xFF3A1C24, 0xFF2A2020, 0xFF1A1A1A, true, 1.0, 1.0, 0xFFF4F0E6);
    private static readonly Look RubyLook = new(0xFFF4C8A0, 0xFFB8401C, Hair.Long, 0xFFF6F2E8, 0xFF2A9A5A, 0xFFB02A3A, 0xFF2A9A5A, true, 0.86, 0.94, 0xFFE83A5A);

    private static Look LookOf(Fighter f) => f.Kind switch
    {
        Kind.Brother => f.Id == 0 ? JakeLook : ColeLook,
        Kind.Dutch => new(0xFFE8B888, 0xFF3A6A3A, Hair.Cap, 0xFF5A6A8A, 0xFF3A4252, 0xFF1A1A1A, 0xFF2A2A2A, false, 1.0, 1.0, 0xFF8A9AB8),
        Kind.Rooker => new(0xFFD8A070, 0xFF9A3AC8, Hair.Mohawk, 0xFF6A4A2A, 0xFF22222A, 0xFF4A2A1A, 0xFF8A6A2A, true, 1.1, 1.04, 0xFFE8C040),
        Kind.Vesper => new(0xFFF0C8A8, 0xFF1A1420, Hair.Ponytail, 0xFFC8203A, 0xFF1A1A24, 0xFF1A1A1A, 0xFFE8C040, true, 0.84, 0.96, 0xFFE8C040),
        Kind.Slade => new(0xFFC89060, 0xFF4A2A1A, Hair.Bald, 0xFFB0B0B8, 0xFF7A6A3A, 0xFF2A2A20, 0xFF3A2A1A, true, 1.15, 1.04, 0xFF6A5A2A),
        Kind.Fuse => new(0xFFE8B888, 0xFFE87A20, Hair.Bandana, 0xFFE8C040, 0xFF5A3A24, 0xFF2A1A10, 0xFF8A2A1A, false, 0.95, 0.98, 0xFFD02A20),
        Kind.Goliath => new(0xFFB87A4A, 0xFF2A1A10, Hair.Shaved, 0xFF2A2A30, 0xFF4A5A4A, 0xFF1A1A1A, 0xFF8A2A1A, true, 1.55, 1.34, 0xFFC8A040),
        _ => new(0xFFE0B088, 0xFF7A1A2A, Hair.Beret, 0xFF5A6A3A, 0xFF4A5A32, 0xFF1A1A14, 0xFF3A2A1A, false, 1.2, 1.12, 0xFFE8C040),
    };

    private static uint Darker(uint c, double k = 0.6) => Blend(0xFF000000, c, k);

    /// <summary>A limb or a thick line with a black outline round it.</summary>
    private static void Limb(DrawingContext dc, Point a, Point b, double width, uint colour)
    {
        dc.DrawLine(Pen(Ink, width + 5), a, b);
        dc.DrawLine(Pen(colour, width), a, b);
    }

    /// <summary>
    /// A pose: the hips' height, the body's lean, the head's tilt, then the near and far legs (thigh and shin) and
    /// arms (upper and fore), each an angle from straight down, positive towards where he faces. Tilt turns the whole
    /// figure about its feet (backwards for a fall), and Lift raises it.
    /// </summary>
    private readonly record struct Rig(double HipY, double Lean, double Head, double Th1, double Sh1, double Th2, double Sh2, double Ua1, double Fa1, double Ua2, double Fa2, double Tilt = 0, double Lift = 0);

    private static readonly Rig Guard = new(-68, 0.06, 0, 0.16, 0.04, -0.14, -0.06, 0.5, 2.35, 0.25, 2.0);

    private static double Ease(double x) => Math.Clamp(x, 0, 1) is var t ? t * t * (3 - 2 * t) : 0;

    /// <summary>A blow's arc: out quickly to full stretch, held, then back.</summary>
    private static double Reach(double t, double at, double length) => t < at ? Ease(t / at) : Ease(1 - (t - at - length * 0.25) / (length * 0.45));

    private Rig PoseOf(World w, Fighter f)
    {
        double t = f.T, ph = f.Walk * 9 + f.Id;
        var carry = w.Carried(f);
        var g = Guard;
        if (carry is { Heavy: true }) g = g with { Ua1 = 3.0, Fa1 = 3.05, Ua2 = 2.9, Fa2 = 3.0 };
        else if (carry != null) g = g with { Ua1 = 0.4, Fa1 = 1.3 };
        if (f.Kind == Kind.Magnus && carry == null) g = g with { Ua1 = 0.9, Fa1 = 1.55, Ua2 = 0.7, Fa2 = 1.5 };
        switch (f.Act)
        {
            case Act.Walk:
            {
                double s = Math.Sin(ph), c = Math.Cos(ph);
                return g with
                {
                    HipY = -67 + Math.Abs(c) * 2, Th1 = 0.5 * s, Sh1 = 0.5 * s - 0.55 * Math.Max(0, -c), Th2 = -0.5 * s, Sh2 = -0.5 * s - 0.55 * Math.Max(0, c),
                    Ua2 = g.Ua2 + 0.25 * s, Lean = 0.1,
                };
            }
            case Act.Punch:
            {
                double k = Reach(t, f.Big ? 0.2 : 0.08, f.Big ? 0.46 : 0.24);
                return g with { Lean = 0.08 + 0.16 * k, Ua1 = 0.5 + (1.5 - 0.5) * k, Fa1 = 2.35 + (1.58 - 2.35) * k, Ua2 = 0.3 - 0.4 * k, Th1 = 0.3, Th2 = -0.3 };
            }
            case Act.Kick:
            {
                double k = Reach(t, 0.12, 0.34);
                return g with { HipY = -68 - 4 * k, Lean = -0.3 * k, Th1 = 0.16 + 1.42 * k, Sh1 = 0.04 + 1.56 * k, Th2 = -0.12, Sh2 = -0.1, Ua1 = 0.3, Fa1 = 2.0, Ua2 = -0.4 * k, Fa2 = 1.2 };
            }
            case Act.Elbow:
            {
                double k = Reach(t, 0.1, 0.34);
                return g with { Lean = 0.12 + 0.12 * k, Head = -0.2 * k, Ua1 = 0.5 - 2.1 * k, Fa1 = 2.35 - 1.5 * k, Ua2 = -1.2 * k, Fa2 = 0.6, Th1 = 0.25, Th2 = -0.3 };
            }
            case Act.Jump:
                return g with { HipY = -70, Th1 = 1.1, Sh1 = -0.35, Th2 = 0.4, Sh2 = -0.9, Ua1 = 2.2, Fa1 = 2.7, Ua2 = 1.6, Fa2 = 2.4 };
            case Act.JumpKick:
                return g with { HipY = -66, Lean = -0.4, Th1 = 1.5, Sh1 = 1.6, Th2 = 0.5, Sh2 = -1.1, Ua1 = -0.6, Fa1 = 0.4, Ua2 = 0.9, Fa2 = 2.4 };
            case Act.Hurt:
                return g with { Lean = -0.35, Head = -0.3, Ua1 = -0.4, Fa1 = 0.4, Ua2 = -0.7, Fa2 = 0.2, Th1 = 0.2, Th2 = -0.35 };
            case Act.Dazed:
            {
                double sway = Math.Sin(t * 7) * 0.08;
                return g with { HipY = -62, Lean = 1.2 + sway, Head = 0.35, Th1 = 0.25, Sh1 = -0.25, Th2 = -0.15, Sh2 = -0.4, Ua1 = 0.15 + sway, Fa1 = 0.3, Ua2 = -0.1, Fa2 = 0.1 };
            }
            case Act.Held:
            {
                // Bent double, head held down at knee height.
                double jolt = w.Get(f.Partner) is { Act: Act.Knee } k && k.T is > 0.1 and < 0.2 ? -0.25 : 0;
                return g with { HipY = -60, Lean = 1.1 + jolt, Head = 0.35 + jolt, Th1 = 0.3, Sh1 = -0.2, Th2 = -0.1, Sh2 = -0.35, Ua1 = 0.4 + Math.Sin(t * 18) * 0.15, Fa1 = 0.2, Ua2 = 0.1, Fa2 = 0.1 };
            }
            case Act.Fly:
            {
                double k = Math.Min(1, t * 2.4);
                return g with { Tilt = -0.25 - 1.2 * k, Lean = -0.2, Head = -0.4, Ua1 = -1.2, Fa1 = -0.6, Ua2 = -1.6, Fa2 = -1.1, Th1 = 0.7, Sh1 = 0.2, Th2 = 0.3, Sh2 = 0.6 };
            }
            case Act.Down:
                return g with { Tilt = -Math.PI / 2, Lift = 0, Lean = 0, Head = 0.1, Ua1 = -2.9, Fa1 = -2.9, Ua2 = -2.6, Fa2 = -2.4, Th1 = 0.1, Sh1 = 0.05, Th2 = -0.05, Sh2 = 0 };
            case Act.Rise:
            {
                double k = Ease(t / Sim.RiseTime);
                return g with { Tilt = -Math.PI / 2 * (1 - k), HipY = -50 - 18 * k, Lean = 0.4 * (1 - k), Th1 = 1.2 * (1 - k), Sh1 = -0.6 * (1 - k), Ua1 = 1.2, Fa1 = 1.0 };
            }
            case Act.Hold:
                return g with { Lean = 0.3, Ua1 = 1.2, Fa1 = 0.45, Ua2 = 0.9, Fa2 = 0.8, Th1 = 0.25, Th2 = -0.2 };
            case Act.Knee:
            {
                double k = Reach(t, 0.12, 0.3);
                return g with { Lean = 0.3 - 0.12 * k, HipY = -68 - 4 * k, Th1 = 0.2 + 1.55 * k, Sh1 = 0.1 - 0.45 * k, Th2 = -0.15, Sh2 = -0.1, Ua1 = 1.2 - 0.25 * k, Fa1 = 0.45, Ua2 = 0.9, Fa2 = 0.8 };
            }
            case Act.Slam:
            {
                double k = Ease(t / 0.26);
                return g with { Lean = 0.3 + 0.7 * k, HipY = -68 + 10 * k, Ua1 = 1.4 - 0.9 * k, Fa1 = 1.4 - 1.1 * k, Ua2 = 1.0 - 0.6 * k, Fa2 = 1.0, Th1 = 0.5, Sh1 = -0.2, Th2 = -0.4 };
            }
            case Act.Throw:
            {
                double k = Ease(t / 0.3);
                return g with { Lean = 0.4 - 0.9 * k, Head = -0.3 * k, Ua1 = 1.2 + 1.9 * k, Fa1 = 1.4 + 1.8 * k, Ua2 = 1.0 + 1.8 * k, Fa2 = 1.2 + 1.8 * k, Th1 = 0.3, Th2 = -0.4 };
            }
            case Act.Swing:
            {
                double at = carry?.Kind == ItemKind.Whip ? 0.22 : 0.16, k = Ease(t / at), back = Ease(t / (at * 0.5));
                double ua = t < at * 0.5 ? 0.4 - 2.8 * back : -2.4 + 3.9 * k;
                return g with { Lean = -0.1 + 0.3 * k, Ua1 = ua, Fa1 = ua + 0.1, Ua2 = -0.3, Fa2 = 0.6, Th1 = 0.35, Th2 = -0.3 };
            }
            case Act.Toss:
            {
                if (carry is { Heavy: true } || t > 0.12 && w.Items.Any(i => i.Thrower == f.Id && i.Heavy && i.Flying && i.Spin < 0.3))
                    return g with { Lean = 0.25, Ua1 = 2.2, Fa1 = 2.0, Ua2 = 2.1, Fa2 = 1.9 };
                double k = Ease(t / 0.12);
                return g with { Lean = -0.15 + 0.35 * k, Ua1 = -2.0 + 3.6 * k, Fa1 = -1.6 + 3.2 * k, Th1 = 0.4, Th2 = -0.35 };
            }
            case Act.Lift:
                return g with { HipY = -48, Lean = 0.7, Th1 = 1.2, Sh1 = -0.3, Th2 = 0.4, Sh2 = -0.9, Ua1 = 0.9, Fa1 = 0.6, Ua2 = 0.8, Fa2 = 0.5 };
            case Act.Headbutt:
            {
                double s = Math.Sin(t * 30);
                return g with { Lean = 1.15, Head = 0.2, HipY = -64, Th1 = 0.6 * s, Sh1 = 0.6 * s - 0.4, Th2 = -0.6 * s, Sh2 = -0.6 * s - 0.4, Ua1 = -0.6, Fa1 = -0.2, Ua2 = -0.8, Fa2 = -0.3 };
            }
            case Act.Charge:
            {
                double s = Math.Sin(t * 22);
                return g with { Lean = 0.5, Th1 = 0.8 * s, Sh1 = 0.8 * s - 0.6, Th2 = -0.8 * s, Sh2 = -0.8 * s - 0.6, Ua1 = 1.4, Fa1 = 1.6, Ua2 = 1.2, Fa2 = 1.4 };
            }
            case Act.Aim or Act.Shoot:
            {
                double kick = f.Act == Act.Shoot && f.T < 0.05 ? -0.08 : 0;
                return g with { Lean = -0.05 + kick, Ua1 = 1.05, Fa1 = 1.57, Ua2 = 0.75, Fa2 = 1.5, Th1 = 0.3, Th2 = -0.3 };
            }
            case Act.Fall:
                return g with { Lean = -0.2, Ua1 = 2.8 + Math.Sin(t * 20) * 0.3, Fa1 = 3.0, Ua2 = 2.6 - Math.Sin(t * 20) * 0.3, Fa2 = 2.9, Th1 = 0.5, Sh1 = 0, Th2 = -0.3, Sh2 = -0.6 };
            case Act.Climb:
            {
                double s = Math.Sin(f.Z * 0.12);
                return g with { Ua1 = 2.9 + 0.2 * s, Fa1 = 3.1, Ua2 = 2.9 - 0.2 * s, Fa2 = 3.1, Th1 = 0.6 + 0.4 * s, Sh1 = -0.2, Th2 = 0.6 - 0.4 * s, Sh2 = -0.2 };
            }
        }
        // Standing: a fighter's bounce.
        double breathe = Math.Sin(_clock * 4 + f.Id) * 1.2;
        return g with { HipY = -68 + breathe * 0.5 };
    }

    /// <summary>A figure, feet at (x, y) on the screen, facing d: posed, dressed, and carrying what it carries.</summary>
    private void Figure(DrawingContext dc, double x, double y, int d, Look look, Rig r, bool flash = false, ItemKind? carry = null, double whip = 0)
    {
        double size = look.Size;
        var m = Matrix.CreateTranslation(0, -r.Lift) * Matrix.CreateRotation(r.Tilt) * Matrix.CreateTranslation(0, Math.Abs(Math.Sin(r.Tilt)) * -14)
                * Matrix.CreateScale(size * d, size) * Matrix.CreateTranslation(x, y);
        using var _ = dc.PushTransform(m);
        double bw = look.Build;
        Point P(Point o, double len, double a) => new(o.X + len * Math.Sin(a), o.Y + len * Math.Cos(a));
        var hip = new Point(0, r.HipY);
        var shoulder = new Point(hip.X + 50 * Math.Sin(r.Lean), hip.Y - 50 * Math.Cos(r.Lean));
        var kneeF = P(hip, 34, r.Th1);
        var footF = P(kneeF, 34, r.Sh1);
        var kneeB = P(hip, 34, r.Th2);
        var footB = P(kneeB, 34, r.Sh2);
        var elbowF = P(shoulder, 28, r.Ua1);
        var handF = P(elbowF, 26, r.Fa1);
        var elbowB = P(shoulder, 28, r.Ua2);
        var handB = P(elbowB, 26, r.Fa2);
        uint shirt = flash ? Blend(look.Shirt, 0xFFFFFFFF, 0.6) : look.Shirt, pants = flash ? Blend(look.Pants, 0xFFFFFFFF, 0.5) : look.Pants, skin = flash ? Blend(look.Skin, 0xFFFFFFFF, 0.5) : look.Skin;
        bool girl = look.Hair is Hair.Ponytail or Hair.Long;
        // The far arm and leg, a shade darker.
        uint farSleeve = Darker(look.Bare ? skin : shirt, 0.72);
        Limb(dc, shoulder, elbowB, 10 * bw, farSleeve);
        Limb(dc, elbowB, handB, 9 * bw, Darker(skin, 0.72));
        dc.FillEllipse(handB.X, handB.Y, 6 * bw, Darker(skin, 0.72));
        Limb(dc, hip, kneeB, 14 * bw, Darker(pants, 0.72));
        Limb(dc, kneeB, footB, 12 * bw, Darker(pants, 0.72));
        Shoe(dc, footB, kneeB, look.Shoes, bw);
        // The body: shoulders, waist, belt; the girl's skirt.
        double sw = 17 * bw, ww = 12 * bw;
        var ax = new Vector(Math.Cos(r.Lean), Math.Sin(r.Lean));
        Poly(dc, shirt, Ink, 3, hip - ax * ww, hip + ax * ww, shoulder + ax * sw, shoulder - ax * sw);
        if (look.Hair == Hair.Shaved)
            dc.DrawLine(Pen(Darker(shirt, 0.7), 3), hip + ax * 2 + (shoulder - hip) * 0.15, shoulder + ax * 2);
        dc.DrawLine(Pen(Ink, 7 * bw), hip - ax * (ww + 1) + (shoulder - hip) * 0.06, hip + ax * (ww + 1) + (shoulder - hip) * 0.06);
        dc.DrawLine(Pen(look.Belt, 4.5 * bw), hip - ax * ww + (shoulder - hip) * 0.06, hip + ax * ww + (shoulder - hip) * 0.06);
        if (look.Hair == Hair.Bandana)
        {
            // Fuse's bandolier of dynamite.
            dc.DrawLine(Pen(0xFF5A3A1A, 5), shoulder - ax * sw * 0.8, hip + ax * ww * 0.8);
            for (int k = 1; k < 5; k++)
            {
                var p = shoulder - ax * sw * 0.8 + (hip + ax * ww * 0.8 - (shoulder - ax * sw * 0.8)) * (k / 5.0);
                dc.DrawRectangle(Brush(0xFFD02A20), Pen(Ink, 1.5), new Rect(p.X - 3, p.Y - 7, 6, 12));
            }
        }
        if (look.Hair == Hair.Beret) dc.DrawLine(Pen(0xFF2A2A1A, 6), shoulder + ax * sw * 0.8, hip - ax * ww * 0.6);
        if (look.Hair == Hair.Spiky)
        {
            // The brothers' sleeveless gi top: the white lapel across the chest.
            dc.DrawLine(Pen(look.Trim, 4), shoulder + ax * sw * 0.7, hip + (shoulder - hip) * 0.35 - ax * ww * 0.3);
        }
        if (girl && look.Hair == Hair.Long)
            Poly(dc, pants, Ink, 3, hip - ax * (ww + 2), hip + ax * (ww + 2), hip + ax * (ww + 12) + new Vector(0, 34), hip - ax * (ww + 12) + new Vector(0, 34));
        // The near leg.
        if (!(girl && look.Hair == Hair.Long))
        {
            Limb(dc, hip, kneeF, 15 * bw, pants);
            Limb(dc, kneeF, footF, 13 * bw, pants);
        }
        else
        {
            Limb(dc, hip + new Vector(0, 30), kneeF, 9, skin);
            Limb(dc, kneeF, footF, 8, skin);
        }
        Shoe(dc, footF, kneeF, look.Shoes, bw);
        // The head.
        double ha = r.Lean + r.Head;
        var neck = new Point(shoulder.X + 4 * Math.Sin(ha), shoulder.Y - 4 * Math.Cos(ha));
        var head = new Point(neck.X + 13 * Math.Sin(ha) + 2 * Math.Cos(ha), neck.Y - 13 * Math.Cos(ha) + 2 * Math.Sin(ha));
        Head(dc, head, ha, look, skin, flash);
        // The near arm, and whatever's in the hand.
        uint sleeve = look.Bare ? skin : shirt;
        Limb(dc, shoulder, elbowF, 11 * bw, sleeve);
        if (look.Bare && !girl) dc.DrawEllipse(Brush(skin), Pen(Ink, 2.5), shoulder, 9 * bw, 9 * bw);
        else if (!look.Bare) dc.DrawEllipse(Brush(shirt), Pen(Ink, 2.5), shoulder, 10 * bw, 10 * bw);
        Limb(dc, elbowF, handF, 10 * bw, skin);
        if (look.Hair is Hair.Shaved or Hair.Mohawk) Limb(dc, P(elbowF, 14, r.Fa1), P(elbowF, 20, r.Fa1), 11 * bw, look.Trim);
        dc.DrawEllipse(Brush(skin), Pen(Ink, 2.5), handF, 7 * bw, 7 * bw);
        if (carry is { } c) Held(dc, c, handF, handB, r.Fa1, whip);
        if (look.Hair == Hair.Beret) Gun(dc, handF, r.Fa1);
    }

    private static void Shoe(DrawingContext dc, Point foot, Point knee, uint colour, double bw)
    {
        double a = Math.Atan2(foot.Y - knee.Y, foot.X - knee.X) - Math.PI / 2;
        var toe = new Point(foot.X + 13 * Math.Cos(a), foot.Y + 13 * Math.Sin(a));
        Limb(dc, foot, toe, 8 * bw, colour);
    }

    private static void Head(DrawingContext dc, Point h, double a, Look look, uint skin, bool flash)
    {
        double r = 14;
        var up = new Vector(Math.Sin(a), -Math.Cos(a));
        var fwd = new Vector(Math.Cos(a), Math.Sin(a));
        Point At(double f, double u) => h + fwd * f + up * u;
        uint hair = flash ? Blend(look.HairColour, 0xFFFFFFFF, 0.4) : look.HairColour;
        // Behind the head: long hair and ponytails.
        if (look.Hair == Hair.Long)
            Poly(dc, hair, Ink, 3, At(-4, 14), At(-18, 8), At(-22, -26), At(-12, -34), At(-2, -20));
        if (look.Hair == Hair.Ponytail)
        {
            Poly(dc, hair, Ink, 3, At(-10, 8), At(-30, 2), At(-40, -20), At(-30, -10), At(-14, 0));
            dc.FillEllipse(At(-12, 6).X, At(-12, 6).Y, 3, look.Trim);
        }
        dc.DrawEllipse(Brush(skin), Pen(Ink, 3), h, r, r + 1);
        // The face: an eye, a brow, a nose and a mouth.
        dc.FillEllipse(At(7, 2).X, At(7, 2).Y, 2, Ink);
        dc.DrawLine(Pen(Ink, 2.5), At(3, 6), At(11, 5));
        Poly(dc, skin, Ink, 2, At(12, 1), At(17, -3), At(12, -4));
        dc.DrawLine(Pen(Darker(skin, 0.6), 2), At(6, -8), At(11, -8));
        switch (look.Hair)
        {
            case Hair.Spiky:
                Poly(dc, hair, Ink, 2.5, At(-14, -2), At(-20, 6), At(-14, 10), At(-18, 18), At(-6, 16), At(-4, 24), At(4, 17), At(10, 22), At(13, 12), At(16, 8), At(4, 8), At(-6, 4), At(-8, -6));
                // The headband, its tails flying behind.
                dc.DrawLine(Pen(look.Trim, 4), At(-13, 6), At(13, 9));
                Limb(dc, At(-13, 6), At(-26, 2), 3, look.Trim);
                break;
            case Hair.Cap:
                Poly(dc, hair, Ink, 2.5, At(-15, 2), At(-14, 12), At(0, 17), At(13, 12), At(14, 4));
                Poly(dc, hair, Ink, 2.5, At(-12, 6), At(-26, 4), At(-24, 9), At(-12, 10));
                break;
            case Hair.Mohawk:
                Poly(dc, hair, Ink, 2.5, At(-12, 8), At(-16, 20), At(-6, 18), At(-4, 26), At(4, 20), At(10, 24), At(10, 12));
                break;
            case Hair.Ponytail or Hair.Long:
                Poly(dc, hair, Ink, 2.5, At(-15, -6), At(-15, 8), At(-4, 16), At(10, 14), At(15, 6), At(4, 6), At(-4, 0), At(-8, -10));
                break;
            case Hair.Bald:
                dc.DrawLine(Pen(Blend(skin, 0xFFFFFFFF, 0.5), 3), At(-4, 12), At(4, 13));
                Poly(dc, hair, Ink, 2, At(14, -8), At(9, -16), At(0, -15), At(-3, -6), At(4, -10), At(10, -10));
                break;
            case Hair.Bandana:
                Poly(dc, hair, Ink, 2.5, At(-15, 2), At(-13, 12), At(0, 16), At(12, 12), At(14, 4));
                Poly(dc, hair, Ink, 2, At(-13, 4), At(-24, 0), At(-22, -6), At(-12, -2));
                break;
            case Hair.Shaved:
                dc.DrawLine(Pen(Blend(skin, 0xFFFFFFFF, 0.45), 3), At(-6, 11), At(2, 13));
                Poly(dc, hair, Ink, 2, At(-6, 12), At(-4, 18), At(4, 18), At(5, 13));
                dc.DrawLine(Pen(Ink, 3), At(4, -10), At(13, -10));
                break;
            case Hair.Beret:
                Poly(dc, hair, Ink, 2.5, At(-16, 4), At(-12, 14), At(4, 18), At(16, 10), At(12, 6));
                dc.DrawRectangle(Brush(Ink), null, new Rect(At(4, 5).X - 1, At(4, 5).Y - 1, 13, 5));
                break;
        }
    }

    /// <summary>The boss's machine gun, along the forearm.</summary>
    private static void Gun(DrawingContext dc, Point hand, double a)
    {
        var dir = new Vector(Math.Sin(a), Math.Cos(a));
        var n = new Vector(-dir.Y, dir.X);
        var muzzle = hand + dir * 46;
        Poly(dc, 0xFF2A2A30, Ink, 2.5, hand - dir * 18 - n * 5, muzzle - n * 4, muzzle + n * 3, hand - dir * 18 + n * 6);
        Poly(dc, 0xFF4A3A2A, Ink, 2, hand - dir * 18 - n * 5, hand - dir * 34 - n * 2, hand - dir * 32 + n * 9, hand - dir * 16 + n * 6);
        Poly(dc, 0xFF2A2A30, Ink, 2, hand + dir * 6 + n * 3, hand + dir * 12 + n * 3, hand + dir * 10 + n * 16, hand + dir * 4 + n * 14);
    }

    /// <summary>What a fighter carries, in hand (or held up overhead).</summary>
    private void Held(DrawingContext dc, ItemKind kind, Point hand, Point other, double a, double whip)
    {
        var dir = new Vector(Math.Sin(a), Math.Cos(a));
        switch (kind)
        {
            case ItemKind.Bat:
                Limb(dc, hand - dir * 8, hand + dir * 62, 6, 0xFFC8904A);
                Limb(dc, hand + dir * 30, hand + dir * 62, 9, 0xFFC8904A);
                break;
            case ItemKind.Knife:
                Poly(dc, 0xFFE0E4EA, Ink, 2, hand + dir * 6 + new Vector(-dir.Y, dir.X) * 3, hand + dir * 30, hand + dir * 6 - new Vector(-dir.Y, dir.X) * 3);
                Limb(dc, hand - dir * 4, hand + dir * 6, 4, 0xFF3A2A1A);
                break;
            case ItemKind.Dynamite:
                Stick(dc, hand.X, hand.Y, a, false);
                break;
            case ItemKind.Whip:
            {
                // Coiled at the hip, or lashing out along the line.
                Limb(dc, hand - dir * 4, hand + dir * 12, 5, 0xFF5A3A1A);
                var tip = hand + dir * 12;
                if (whip > 0)
                {
                    double len = Sim.WhipReach * whip;
                    var g = new StreamGeometry();
                    using (var c = g.Open())
                    {
                        c.BeginFigure(tip, false);
                        c.QuadraticBezierTo(new Point(tip.X + len * 0.5, tip.Y - 30 * (1 - whip)), new Point(tip.X + len, tip.Y + 18 - 30 * (1 - whip)));
                        c.EndFigure(false);
                    }
                    dc.DrawGeometry(null, Pen(Ink, 5), g);
                    dc.DrawGeometry(null, Pen(0xFF6A4A2A, 2.5), g);
                }
                else dc.DrawEllipse(null, Pen(0xFF3A2A1A, 3), new Point(tip.X + 4, tip.Y + 10), 9, 12);
                break;
            }
            default:
            {
                var mid = new Point((hand.X + other.X) / 2, Math.Min(hand.Y, other.Y) - 22);
                if (kind == ItemKind.Drum) Drum(dc, mid.X, mid.Y + 24, 0);
                else Crate(dc, mid.X, mid.Y + 22, 0);
                break;
            }
        }
    }

    // ───────────────────────────── on the street ─────────────────────────────

    /// <summary>A fighter where he stands (or flies, or lies): his shadow is drawn with the floor, the rest here.</summary>
    private void DrawFighter(DrawingContext dc, World w, Fighter f, double cam)
    {
        if (f.Gone || f.Act == Act.Out || f.IsBrother && (!f.Joined || f.Wait > 0)) return;
        // Getting up, or back in: he flickers while nothing can touch him.
        if (f.Safe > 0 && f.Act is Act.Stand or Act.Walk or Act.Jump && (int)(_clock * 16) % 2 == 0) return;
        if (f.Ko && f.Act == Act.Down && !f.IsBrother && f.T > 0.9 && (int)(_clock * 14) % 2 == 0) return;
        var look = LookOf(f);
        var r = PoseOf(w, f);
        double x = f.X - cam, y = f.Y - f.Z;
        var carry = w.Carried(f);
        double whip = f.Act == Act.Swing && carry?.Kind == ItemKind.Whip ? Reach(f.T, 0.22, 0.5) : 0;
        bool flash = f.Act == Act.Hurt && f.T < 0.08 || f.Act == Act.Fly && f.T < 0.06;
        using var o = dc.PushOpacity(f.Act == Act.Fall ? Math.Clamp(1 - f.T / Sim.FallTime, 0, 1) : 1);
        if (f.Act == Act.Fall)
        {
            // Over the edge: down out of sight into the gorge (or the pit).
            using var clip = dc.PushClip(new Rect(x - 120, 0, 240, f.Y + 6));
            Figure(dc, x, y + f.T * f.T * 300, f.Facing, look, r, flash, carry?.Kind, whip);
            return;
        }
        Figure(dc, x, y, f.Facing, look, r, flash, carry?.Kind, whip);
        if (f.Act == Act.Dazed && (int)(_clock * 6) % 2 == 0)
            for (int k = 0; k < 3; k++)
            {
                double a = _clock * 5 + k * 2.1;
                Star(dc, x + 30 * f.Facing + Math.Cos(a) * 18, y - 104 + Math.Sin(a) * 6, 6, 2.5, Yellow, Ink, 5);
            }
        if (f.Act == Act.Aim)
        {
            // The boss's aim: a red line along his line of fire.
            double gx = x + f.Facing * 70 * look.Size;
            dc.DrawLine(Pen(Alpha(0xFFFF2020, 0.35 + 0.3 * Math.Sin(_clock * 30)), 2), new Point(gx, y - 76 * look.Size), new Point(gx + f.Facing * 900, y - 76 * look.Size));
        }
    }

    private static void Shadow(DrawingContext dc, double x, double y, double z, double wide)
    {
        double k = Math.Clamp(1 - z / 300, 0.35, 1);
        dc.DrawEllipse(Brush(0x58000000), null, new Point(x, y + 2), wide * k, 8 * k);
    }

    /// <summary>An oil drum, standing (or tumbling, at an angle).</summary>
    private static void Drum(DrawingContext dc, double x, double y, double turn)
    {
        using var _ = dc.PushTransform(Matrix.CreateRotation(turn) * Matrix.CreateTranslation(x, y - 26));
        dc.DrawRectangle(Brush(0xFF2A62B0), Pen(Ink, 3), new Rect(-18, -26, 36, 52), 4, 4);
        dc.DrawLine(Pen(0xFF1A3A70, 3), new Point(-17, -10), new Point(17, -10));
        dc.DrawLine(Pen(0xFF1A3A70, 3), new Point(-17, 10), new Point(17, 10));
        dc.DrawLine(Pen(0xFF6A9AE0, 3), new Point(-10, -22), new Point(-10, 22));
        dc.DrawEllipse(Brush(0xFF1A3A70), Pen(Ink, 2.5), new Point(0, -26), 18, 5);
    }

    private static void Crate(DrawingContext dc, double x, double y, double turn)
    {
        using var _ = dc.PushTransform(Matrix.CreateRotation(turn) * Matrix.CreateTranslation(x, y - 20));
        dc.DrawRectangle(Brush(0xFFB0783A), Pen(Ink, 3), new Rect(-21, -20, 42, 40));
        dc.DrawRectangle(null, Pen(0xFF6A4220, 3), new Rect(-16, -15, 32, 30));
        dc.DrawLine(Pen(0xFF6A4220, 3), new Point(-16, -15), new Point(16, 15));
    }

    /// <summary>A stick of dynamite, its fuse fizzing if lit.</summary>
    private void Stick(DrawingContext dc, double x, double y, double turn, bool lit)
    {
        using var _ = dc.PushTransform(Matrix.CreateRotation(turn) * Matrix.CreateTranslation(x, y));
        dc.DrawRectangle(Brush(0xFFD02A20), Pen(Ink, 2.5), new Rect(-5, -14, 10, 26), 2, 2);
        dc.DrawLine(Pen(0xFFF0E0B0, 2), new Point(-4, -2), new Point(4, -2));
        dc.DrawLine(Pen(0xFF3A3020, 2), new Point(0, -14), new Point(4, -22));
        if (lit)
        {
            Star(dc, 4, -23, 7 + Math.Sin(_clock * 40) * 2, 3, 0xFFFFE060, 0xFFFF6A10, 6, _clock * 9);
        }
    }

    /// <summary>A weapon lying, flying, or going off.</summary>
    private void DrawItem(DrawingContext dc, Item i, double cam)
    {
        double x = i.X - cam, y = i.Y - i.Z;
        if (i.Blast > 0)
        {
            double k = i.Blast / 0.5;
            Star(dc, x, i.Y - 40, 50 + 70 * k, 26 + 40 * k, Alpha(0xFFFFA020, 1.3 - k), Alpha(Ink, 1.3 - k), 11, k);
            Star(dc, x, i.Y - 40, 30 + 40 * k, 16 + 20 * k, Alpha(0xFFFFF4A0, 1.3 - k), null, 11, k + 0.3);
            if (k < 0.8) Comic(dc, "KA-BOOM!", new Point(x, i.Y - 140), 34, 0xFFFFE040, center: true);
            return;
        }
        double turn = i.Flying ? i.Spin * (i.Kind == ItemKind.Knife ? 0 : 9) * Math.Sign(i.Vx) : 0;
        switch (i.Kind)
        {
            case ItemKind.Bat:
                using (dc.PushTransform(Matrix.CreateRotation(i.Flying ? turn : 0.08) * Matrix.CreateTranslation(x, y - 5)))
                {
                    Limb(dc, new Point(-34, 0), new Point(34, 0), 6, 0xFFC8904A);
                    Limb(dc, new Point(0, 0), new Point(34, 0), 9, 0xFFC8904A);
                }
                break;
            case ItemKind.Whip:
                dc.DrawEllipse(null, Pen(Ink, 5), new Point(x, y - 4), 18, 7);
                dc.DrawEllipse(null, Pen(0xFF6A4A2A, 2.5), new Point(x, y - 4), 18, 7);
                Limb(dc, new Point(x + 14, y - 6), new Point(x + 30, y - 8), 5, 0xFF5A3A1A);
                break;
            case ItemKind.Knife:
            {
                int d = i.Flying ? Math.Sign(i.Vx) : 1;
                if (i.Flying) dc.DrawLine(Pen(0x80FFFFFF, 3), new Point(x - d * 40, y), new Point(x - d * 10, y));
                Poly(dc, 0xFFE0E4EA, Ink, 2, new Point(x - d * 4, y - 4), new Point(x + d * 20, y), new Point(x - d * 4, y + 3));
                Limb(dc, new Point(x - d * 14, y), new Point(x - d * 4, y), 4, 0xFF3A2A1A);
                break;
            }
            case ItemKind.Drum:
                if (i.Flying || i.Vx != 0) Drum(dc, x, y + 26, turn + (i.Vx != 0 && !i.Flying ? Math.PI / 2 : 0));
                else Drum(dc, x, y, 0);
                break;
            case ItemKind.Box:
                Crate(dc, x, y, turn);
                break;
            default:
                Stick(dc, x, y - 6, i.Flying ? turn : Math.PI / 2, i.Lit);
                if (!i.Lit && !i.Flying) { Stick(dc, x - 4, y - 12, Math.PI / 2, false); Stick(dc, x + 2, y - 18, Math.PI / 2 + 0.1, false); }
                break;
        }
    }

    /// <summary>The flashes and puffs, and the boss's bullets.</summary>
    private void DrawFlights(DrawingContext dc, World w, double cam)
    {
        foreach (var s in w.Shots)
        {
            double x = s.X - cam, y = s.Y - s.Z;
            dc.DrawLine(Pen(0x70FFE070, 4), new Point(x - Math.Sign(s.Vx) * 34, y), new Point(x, y));
            dc.DrawLine(Pen(0xFFFFF4C0, 4), new Point(x - Math.Sign(s.Vx) * 12, y), new Point(x, y));
        }
        foreach (var b in w.Bursts)
        {
            double x = b.X - cam, k = b.T / 0.6;
            switch (b.Kind)
            {
                case 0:
                    if (k < 0.3)
                    {
                        Star(dc, x, b.Y, 22 - k * 20, 8, 0xFFFFFFFF, 0xFFFF4040, 7, k * 3);
                        if ((int)(b.X / 9) % 4 == 0 && k < 0.25) Comic(dc, ((int)(b.X / 13) % 3) switch { 0 => "WHAM!", 1 => "POW!", _ => "SMACK!" }, new Point(x, b.Y - 34), 24, Yellow, center: true);
                    }
                    break;
                case 1:
                    Star(dc, x, b.Y - 10, 24 + 50 * k, 12 + 26 * k, Alpha(0xFFFF9A20, 1 - k), Alpha(Ink, 1 - k), 9, k);
                    break;
                case 2:
                    dc.DrawEllipse(Brush(Alpha(0xFFC8B8A0, 0.7 - k)), null, new Point(x - 30 - 30 * k, b.Y - 4), 14 + 20 * k, 6 + 6 * k);
                    dc.DrawEllipse(Brush(Alpha(0xFFC8B8A0, 0.7 - k)), null, new Point(x + 30 + 30 * k, b.Y - 4), 14 + 20 * k, 6 + 6 * k);
                    break;
                default:
                    for (int n = 0; n < 6; n++)
                    {
                        double a = n * 1.05, r = 10 + 60 * k;
                        Limb(dc, new Point(x + Math.Cos(a) * r, b.Y + Math.Sin(a) * r * 0.6 + 80 * k * k), new Point(x + Math.Cos(a) * r + 10, b.Y + Math.Sin(a) * r * 0.6 + 80 * k * k + 4), 4, 0xFFB0783A);
                    }
                    break;
            }
        }
    }

    /// <summary>A comic starburst: a hit, a blast.</summary>
    private static void Star(DrawingContext dc, double x, double y, double outer, double inner, uint fill, uint? edge, int points = 8, double turn = 0)
    {
        var pts = new Point[points * 2];
        for (int i = 0; i < pts.Length; i++)
        {
            double r = i % 2 == 0 ? outer : inner, a = turn + Math.PI * i / points;
            pts[i] = new Point(x + r * Math.Cos(a), y + r * Math.Sin(a));
        }
        Poly(dc, fill, edge, 2.5, pts);
    }
}
