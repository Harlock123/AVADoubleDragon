# AGENTS.md

Notes for anyone, human or AI agent, changing this code. `README.md` covers playing the game, its controls and
what the tests check; this file covers how the code is put together and the rules that keep it working.

## What this is

A belt-scrolling beat-'em-up in the style of Technōs Japan's 1987 arcade game Double Dragon, for one or two
players, in C# on .NET 10 and Avalonia 12, drawn through Avalonia's `DrawingContext`. One self-contained executable per platform
(Windows, macOS and Linux, x64 and ARM). Everything in it is original: the fighters and streets are drawn in code,
and the sounds, voices and music are synthesised by `tools/make_sounds.py` (with the formant voice in
`tools/speech.py`).

## Commands

```bash
dotnet build AVADoubleDragon.slnx                 # the SDK version is in global.json (10.0.400 or later)
dotnet test AVADoubleDragon.slnx                  # every test runs headless: no window opens
dotnet run --project src/AVADoubleDragon          # play it; --no-audio for silence
DRAGON_SHOTS=$PWD/Images dotnet test tests/AVADoubleDragon.Tests --filter ScreenshotTests   # refresh the README's pictures
DRAGON_PROBE=1 dotnet test tests/AVADoubleDragon.Tests --filter PilotProbe   # how the computer brothers get on (1, 2 or mash)
python3 tools/make_sounds.py                      # rebuild src/AVADoubleDragon/Sounds (standard-library Python only)
```

`PilotProbe` does nothing unless `DRAGON_PROBE` is set: `1` plays with one brother, `2` with both to the duel,
and `mash` just stands and punches.

`Directory.Build.props` sets `TreatWarningsAsErrors`, nullable reference types and implicit usings for every
project: a warning fails the build, so keep it at zero.

## Layout

- `src/AVADoubleDragon/Engine/`: the game, with no Avalonia drawing in it.
  - `GameEngine.cs` runs the states (title, demonstration, play, high scores), the mission cards and clears, the
    duel, the ending and the second player joining in.
  - `Sim.cs` steps the world: the brothers' moves, blows on the depth line, grabs, throws, weapons, hazards, the
    scroll and the waves. `Sim.Foes.cs` is how each of the gang fights, and the boss's gun.
  - `Stage.cs` has the four missions: their waves, weapons and hazards.
  - `World.cs` has the `Field` measures and fixed step, the seeded `Dice`, and the `World` with its fighters,
    items and shots, kept so it can be copied.
  - `Pilot.cs` is the computer brother, used by the attract mode and the tests.
  - `InputHandler.cs` (two players on one keyboard) and `ControllerManager.cs` (an SDL2 gamepad each) produce
    `Controls`.
  - `HighScoreTable.cs`, `InitialsEntry.cs` and `SoundManager.cs` do what their names say.
- `src/AVADoubleDragon/Views/`: `GameCanvas` (split over partial files: the street, the jointed figures, the
  overlays) draws the frame and runs the 60 Hz timer.
- `src/AVADoubleDragon/Audio/`: the OpenAL Soft audio engine, shared with the author's other AVA games.
- `tests/AVADoubleDragon.Tests/`: xUnit v3 with `Avalonia.Headless.XUnit`: `GameplayTests`, `ScreenshotTests` and
  `PilotProbe`.

## Rules that keep it working

- **Fixed steps.** `GameEngine.Update` advances in fixed 1/60 s steps (`Field.Step`, through `Sim.Step`),
  independent of the frame rate, so tests can play whole missions deterministically. Draw random numbers from
  the world's seeded `Dice` (`w.Dice`), never from `Random.Shared`.
- **Engine first, drawing second.** Gameplay belongs in `Engine/`, where tests can reach it without a window.
  `Views/` only reads the world and draws it.
- **Input goes through `InputHandler`.** Up/down on keys and stick must produce the same `dy` sign the engine
  expects (negative is up the screen, which is further back along the street). Test new controls through
  `InputHandler`, not by setting `Controls` directly.
  - Keys are tracked by `PhysicalKey`, because on some Linux setups Avalonia reports the release of Shift as
    CapsLock. A test in `GameplayTests` covers this.
- **SDL under one lock.** `ControllerManager` starts, polls and stops SDL only inside `lock (Sdl)`. The tests
  create many handlers in parallel, and unlocked SDL calls corrupt its heap.
- **Audio.** OpenAL Soft through Silk.NET. `Directory.Build.targets` swaps in the official Windows `soft_oal.dll`
  from `Audio/native/`, which needs no Visual C++ runtime. In a virtual machine `OpenAlConfig` writes a larger
  mixing period to `ALSOFT_CONF` so the sound doesn't break up.
- **Tests never touch the player's files.** High scores live in the app-data folder (`AVADoubleDragon`); tests
  load the table from a temporary folder (`TempFolder()`) and run silent (`Silent = true`).
- **Line endings.** Keep each file's existing line endings (`app.manifest` is CRLF) so diffs show only real
  changes.

## Making a change

- Add or update tests with every gameplay change. `dotnet test` must pass, with no warnings.
- If the change shows on screen, refresh the screenshots with the command above, and look at them.
- Update `README.md` (features, controls, the test list, the project structure) to match.
- New sounds go into `tools/make_sounds.py` (and spoken words into its `speech.py` phonemes), not as hand-edited
  WAV files.

## CI and releases

`.github/workflows/build.yml` builds and tests every push. It then publishes self-contained single-file builds for
win-x64/x86/arm64, linux-x64/arm64/arm and osx-x64/arm64. Every run keeps the builds as workflow artifacts.
Only pushes to `main` replace the assets of the `latest` pre-release, and a `v*` tag makes a proper release.
