using AVADoubleDragon.Engine;

namespace AVADoubleDragon.Tests;

/// <summary>
/// How the computer brothers get on (set DRAGON_PROBE to run it: DRAGON_PROBE=1 plays the game with one brother, =2
/// with both, to the duel; DRAGON_PROBE=mash just stands and punches).
/// </summary>
public sealed class PilotProbe(ITestOutputHelper output)
{
    [Fact]
    public void How_the_computer_brothers_do()
    {
        if (Environment.GetEnvironmentVariable("DRAGON_PROBE") is not { } mode) return;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        int brothers = mode == "2" ? 2 : 1;
        var w = new World();
        Sim.Start(w, brothers);
        Pilot[] pilots = [new(), new()];
        int[] lives = [Sim.Lives0, Sim.Lives0];
        double start = 0, t = 0;
        while (t < 60 * 60 * 20)
        {
            Controls C(int i) => mode == "mash" ? new Controls(Punch: t % 8 < 4) : pilots[i].Decide(w, i);
            Sim.Step(w, C(0), brothers > 1 ? C(1) : default);
            t++;
            foreach (var b in w.Brothers.Where(b => b.Joined))
                if (b.Lives < lives[b.Id] || b.Act == Act.Out && lives[b.Id] > 0)
                {
                    output.WriteLine($"   {b.Name} lost a life in {w.Mission} at x {b.X:0} wave {w.Wave} [{pilots[b.Id].Doing}], {w.TimeLeft:0}s left, foes {string.Join(",", w.Foes.Where(f => f.Active).Select(f => f.Kind))}");
                    lives[b.Id] = b.Act == Act.Out ? 0 : b.Lives;
                }
                else lives[b.Id] = b.Lives;
            if (!w.Playing.Any()) { output.WriteLine($"GAME OVER in mission {w.Mission}, wave {w.Wave}"); break; }
            if (w.Cleared)
            {
                output.WriteLine($"mission {w.Mission}: {(t - start) / 60:0}s, scores {string.Join("/", w.Brothers.Select(b => b.Score))}, lives {string.Join("/", w.Brothers.Select(b => b.Lives))}");
                start = t;
                if (w.Mission == Stage.Missions) break;
                Sim.StartMission(w, w.Mission + 1);
                foreach (var p in pilots) p.Reset();
            }
            if (w.Duel && t % 600 == 0) output.WriteLine($"   duel: {w.Brothers[0].Hp} v {w.Brothers[1].Hp}");
            if (w.Ending && w.EndT > 3) { output.WriteLine($"ENDING: {w.Brothers[w.Winner].Name} wins after {(t - start) / 60:0}s"); break; }
        }
        output.WriteLine($"after {t / 60:0}s; scores {string.Join("/", w.Brothers.Select(b => b.Score))}; {sw.ElapsedMilliseconds} ms");
    }
}
