namespace AVADoubleDragon.Engine;

public enum GameState { Title, Attract, Playing, Paused, GameOver, EnterInitials, HighScores }

public enum Phase
{
    /// <summary>The mission's card, a moment before the fighting.</summary>
    Start,
    Play,
    /// <summary>Out of the mission: the time bonus, then the next.</summary>
    Cleared,
    /// <summary>The boss beaten with both brothers standing: they fight each other for her.</summary>
    Duel,
    /// <summary>She goes to the winner; then the game is won.</summary>
    Ending,
}

/// <summary>Sounds the engine asks for; the host decides how they sound.</summary>
public enum Sfx
{
    Punch, Kick, Swish, Knee, Slam, Throw, Slap, Headbutt, Whip, Bat, KnifeThrow, Stab, Toss, Crash, Dynamite, Pickup, Grab,
    BodySlam, Grunt, Yell, Hurt, Death, Fall, Jump, Charge, Gun, Cock, Laugh, Conveyor, SpikeWall, Garage, Statue, Climb, Go,
    TimeWarning, OneUp, Join, MissionStart, MissionClear, Duel, Victory, GameOver,
    /// <summary>The fighting tune, looping while the fight's on.</summary>
    Music, MusicStop,
    TitleTune,
}

/// <summary>
/// The whole game with no UI. Call <see cref="Update"/> with the elapsed time and both brothers' controls; it runs in
/// fixed 60 Hz steps, so it plays the same at any frame rate.
/// </summary>
public sealed class GameEngine
{
    public const double TitleIdleSeconds = 15;
    public const double AttractSeconds = 50;
    public const double HighScoreSeconds = 9;
    public const double StartSeconds = 3, ClearedSeconds = 4.5, EndingSeconds = 10, OverSeconds = 4;

    private double _accumulator;
    private bool _start1Prev, _start2Prev, _tunePending = true, _music;
    private int _demos;
    private readonly Queue<int> _entries = new();

    public GameEngine(HighScoreTable? table = null)
    {
        HighScores = table ?? HighScoreTable.Load();
        NewGame(1);
        State = GameState.Title;
    }

    public event Action<Sfx>? Sound;

    public GameState State { get; private set; } = GameState.Title;
    public Phase Phase { get; private set; } = Phase.Start;
    public double StateTime { get; private set; }
    public double PhaseTime { get; private set; }
    public HighScoreTable HighScores { get; }
    public InitialsEntry? Initials { get; private set; }
    /// <summary>Whose initials are being entered.</summary>
    public int EntryPlayer { get; private set; } = -1;
    public int NewEntryIndex { get; private set; } = -1;
    public bool Silent { get; set; }
    public World World { get; private set; } = new();
    /// <summary>The computer at either brother's controls in the demonstration (or null).</summary>
    public Pilot?[] Pilots { get; } = new Pilot?[2];
    /// <summary>The highest score of the two (shown with the high score).</summary>
    public int TopScore => World.Brothers.Max(b => b.Score);
    public int Reached => World.Mission;
    /// <summary>All four missions done: the game is over because it's won.</summary>
    public bool Completed { get; private set; }
    public string? Message { get; private set; }
    public string? SubMessage { get; private set; }
    public double MessageTime { get; private set; }

    private void Play(Sfx s)
    {
        if (!Silent) Sound?.Invoke(s);
    }

    private void Say(string text, string? sub = null, double seconds = 2.5)
    {
        Message = text;
        SubMessage = sub;
        MessageTime = seconds;
    }

    public void Update(double dt, Controls p1, Controls p2 = default, bool start1 = false, bool start2 = false)
    {
        _accumulator += Math.Min(dt, 0.25);
        while (_accumulator >= Field.Step)
        {
            _accumulator -= Field.Step;
            Tick(p1, p2, start1, start2);
        }
    }

    public void Tick(Controls p1, Controls p2 = default, bool start1 = false, bool start2 = false)
    {
        double dt = Field.Step;
        StateTime += dt;
        bool one = start1 && !_start1Prev, two = start2 && !_start2Prev;
        _start1Prev = start1;
        _start2Prev = start2;
        if (MessageTime > 0) { MessageTime -= dt; if (MessageTime <= 0) Message = SubMessage = null; }
        switch (State)
        {
            case GameState.Title:
                if (_tunePending) { _tunePending = false; Play(Sfx.TitleTune); }
                if (one || two) StartGame(two ? 2 : 1);
                else if (p1.Punch && StateTime > 0.5) StartGame(1);
                else if (StateTime > TitleIdleSeconds) StartAttract();
                break;
            case GameState.Attract:
                Simulate(dt, Pilots[0]?.Decide(World, 0) ?? default, Pilots[1]?.Decide(World, 1) ?? default);
                if (one || two) { StartGame(two ? 2 : 1); break; }
                if (State == GameState.Attract && StateTime > AttractSeconds) ShowHighScores(-1);
                break;
            case GameState.Playing:
                if (one) Join(0);
                if (two) Join(1);
                Simulate(dt, p1, p2);
                break;
            case GameState.GameOver:
                if (StateTime > OverSeconds)
                {
                    _entries.Clear();
                    foreach (var b in World.Brothers.Where(b => b.Joined).OrderByDescending(b => b.Score)) _entries.Enqueue(b.Id);
                    NewEntryIndex = -1;
                    NextEntry();
                }
                break;
            case GameState.EnterInitials:
                if (Initials is { IsDone: true } done)
                {
                    NewEntryIndex = HighScores.Insert(done.Initials, World.Brothers[EntryPlayer].Score, Reached);
                    HighScores.Save();
                    NextEntry();
                }
                break;
            case GameState.HighScores:
                if (one || two) StartGame(two ? 2 : 1);
                else if (StateTime > HighScoreSeconds) ReturnToTitle();
                break;
        }
    }

    /// <summary>The next brother whose score makes the table enters initials; then the table.</summary>
    private void NextEntry()
    {
        while (_entries.Count > 0)
        {
            int i = _entries.Dequeue();
            if (!HighScores.Qualifies(World.Brothers[i].Score)) continue;
            State = GameState.EnterInitials;
            StateTime = 0;
            EntryPlayer = i;
            Initials = new InitialsEntry(HighScores.LastInitials);
            return;
        }
        Initials = null;
        EntryPlayer = -1;
        ShowHighScores(NewEntryIndex);
    }

    private void NewGame(int brothers, int mission = 1)
    {
        World = new World();
        Sim.Start(World, brothers);
        if (mission != 1) Sim.StartMission(World, mission);
        Completed = false;
        BeginMission(mission);
    }

    private void BeginMission(int mission)
    {
        if (mission != World.Mission) Sim.StartMission(World, mission);
        SetPhase(Phase.Start);
        Message = SubMessage = null;
        foreach (var p in Pilots) p?.Reset();
        if (State is GameState.Playing or GameState.Attract) Play(Sfx.MissionStart);
    }

    /// <summary>A game for one brother, or both.</summary>
    public void StartGame(int brothers = 1)
    {
        Pilots[0] = Pilots[1] = null;
        State = GameState.Playing;
        StateTime = 0;
        NewEntryIndex = -1;
        Music(false);
        NewGame(brothers);
    }

    /// <summary>The demonstration: the computer plays both brothers or one, touring the missions in turn.</summary>
    private void StartAttract()
    {
        State = GameState.Attract;
        StateTime = 0;
        int demo = _demos++;
        int brothers = demo % 2 == 0 ? 2 : 1;
        for (int i = 0; i < 2; i++) Pilots[i] = i < brothers ? new Pilot() : null;
        NewGame(brothers, 1 + demo % Stage.Missions);
    }

    /// <summary>A brother steps in (or back in), any time in the fight.</summary>
    public void Join(int i)
    {
        if (Phase is Phase.Duel or Phase.Ending || World.Ending || World.Duel) return;
        if (!Sim.Join(World, i)) return;
        Play(Sfx.Join);
        var b = World.Brothers[i];
        Say(b.Name, b.Continues > 0 ? "IS BACK IN THE FIGHT" : "JOINS THE FIGHT", 2);
    }

    public void ReturnToTitle()
    {
        State = GameState.Title;
        StateTime = 0;
        _tunePending = true;
        Pilots[0] = Pilots[1] = null;
        Music(false);
        Message = SubMessage = null;
        NewGame(1);
    }

    private void ShowHighScores(int place)
    {
        State = GameState.HighScores;
        StateTime = 0;
        NewEntryIndex = place;
        Pilots[0] = Pilots[1] = null;
        Music(false);
    }

    public void Pause()
    {
        if (State != GameState.Playing) return;
        State = GameState.Paused;
        Music(false);
    }

    public void Resume()
    {
        if (State != GameState.Paused) return;
        State = GameState.Playing;
        if (Phase is Phase.Play or Phase.Duel) Music(true);
    }

    private void Music(bool on)
    {
        if (on == _music) return;
        _music = on;
        Play(on ? Sfx.Music : Sfx.MusicStop);
    }

    private void SetPhase(Phase p)
    {
        Phase = p;
        PhaseTime = 0;
    }

    private void Simulate(double dt, Controls p1, Controls p2)
    {
        PhaseTime += dt;
        var w = World;
        switch (Phase)
        {
            case Phase.Start:
                if (PhaseTime > StartSeconds) { SetPhase(Phase.Play); Music(true); }
                break;
            case Phase.Play or Phase.Duel:
                Sim.Step(w, p1, p2);
                if (w.Cleared)
                {
                    SetPhase(Phase.Cleared);
                    Music(false);
                    Say($"MISSION {w.Mission} CLEAR", null, ClearedSeconds);
                }
                else if (w.Ending)
                {
                    SetPhase(Phase.Ending);
                    Music(false);
                }
                else if (w.Duel && Phase != Phase.Duel)
                {
                    SetPhase(Phase.Duel);
                    Say("ONLY ONE BROTHER", "CAN WIN RUBY", 3);
                }
                else if (!w.Playing.Any()) { End(); return; }
                break;
            case Phase.Cleared:
                Sim.Step(w, default, default);
                if (PhaseTime > ClearedSeconds)
                {
                    if (w.Mission < Stage.Missions) { BeginMission(w.Mission + 1); return; }
                    Completed = true;
                    End();
                    return;
                }
                break;
            case Phase.Ending:
                Sim.Step(w, default, default);
                if (PhaseTime > EndingSeconds) { Completed = true; End(); return; }
                break;
        }
        foreach (var s in w.Heard) Play(s);
        w.Heard.Clear();
    }

    private void End()
    {
        if (!Completed) Play(Sfx.GameOver);
        Music(false);
        World.Heard.Clear();
        if (State == GameState.Attract) { ShowHighScores(-1); return; }
        State = GameState.GameOver;
        StateTime = 0;
    }

    // ───────────────────────────── for tests ─────────────────────────────

    public void Run(int steps, Controls p1 = default, Controls p2 = default)
    {
        for (int i = 0; i < steps; i++) Tick(p1, p2);
    }

    public void Idle(int steps) => Run(steps);

    /// <summary>Straight into a mission, past its card, with one brother or both.</summary>
    public void Go(int mission = 1, int brothers = 1)
    {
        State = GameState.Playing;
        StateTime = 0;
        Pilots[0] = Pilots[1] = null;
        NewGame(brothers, mission);
        SetPhase(Phase.Play);
        Message = null;
    }

    /// <summary>Into a mission showing its card, as a new game does.</summary>
    public void Card(int mission = 1, int brothers = 1)
    {
        Go(mission, brothers);
        SetPhase(Phase.Start);
    }

    /// <summary>The computer takes a brother's controls (as in the demonstration).</summary>
    public Controls Auto(int i) => Pilots[i]?.Decide(World, i) ?? (Pilots[i] = new Pilot()).Decide(World, i);
}
