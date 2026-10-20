#!/usr/bin/env python3
"""Synthesises AVADoubleDragon's sound effects and tunes into ../src/AVADoubleDragon/Sounds as 16-bit mono WAV.

Nothing is sampled from any game: every effect is built here from square, saw, triangle, sine and noise voices.
The music is the opening of Beethoven's Fifth Symphony (1808, long in the public domain): its four-note knock on
the title in octaves with timpani, then driving on as a street-rock arrangement with a pumping bass and a drum kit,
which also loops under the fighting. The grunts, the yells and the boss's laugh are spoken by the formant voice in
speech.py. Run it from anywhere:
    python3 tools/make_sounds.py
"""
import math
import os
import random
import struct
import wave

from speech import say


RATE = 22050
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "src", "AVADoubleDragon", "Sounds")
RNG = random.Random(1986)


def write(name, samples, volume=0.7):
    peak = max(1e-9, max(abs(s) for s in samples))
    with wave.open(os.path.join(OUT, name), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(b"".join(struct.pack("<h", int(s / peak * volume * 32767)) for s in samples))


def voice(kind, phase):
    p = phase % 1.0
    if kind == "square":
        return 1.0 if p < 0.5 else -1.0
    if kind == "pulse":
        return 1.0 if p < 0.25 else -1.0
    if kind == "thin":
        return 1.0 if p < 0.125 else -1.0
    if kind == "tri":
        return 4 * p - 1 if p < 0.5 else 3 - 4 * p
    if kind == "saw":
        return 2 * p - 1
    if kind == "sine":
        return math.sin(2 * math.pi * p)
    return RNG.uniform(-1, 1)


def tone(seconds, f0, f1=None, kind="square", attack=0.004, release=1.0, vibrato=0.0, vib_rate=8.0, gain=1.0):
    """A note gliding from f0 to f1 (exponentially), with a short attack and a shaped decay."""
    n = int(RATE * seconds)
    f1 = f1 or f0
    out, phase, held = [], 0.0, 0.0
    for i in range(n):
        t = i / RATE
        f = f0 * (f1 / f0) ** (i / max(1, n - 1))
        if vibrato:
            f *= 1 + vibrato * math.sin(2 * math.pi * vib_rate * t)
        phase += f / RATE
        if kind == "noise":
            # Noise re-sampled at the voice's rate: a lower 'pitch' sounds rougher.
            if int(phase) != int(phase - f / RATE):
                held = RNG.uniform(-1, 1)
            s = held
        else:
            s = voice(kind, phase)
        env = min(1.0, t / attack) * (1 - i / n) ** release
        out.append(s * env * gain)
    return out


def silence(seconds):
    return [0.0] * int(RATE * seconds)


def cat(*parts):
    out = []
    for p in parts:
        out.extend(p)
    return out


def mix(*parts):
    n = max(len(p) for p in parts)
    out = [0.0] * n
    for p in parts:
        for i, v in enumerate(p):
            out[i] += v
    return out


def add(buf, seconds, part):
    """Adds a part into a buffer, a time in."""
    start = int(seconds * RATE)
    for i, v in enumerate(part):
        if start + i < len(buf):
            buf[start + i] += v


def lowpass(samples, k):
    out, y = [], 0.0
    for s in samples:
        y += k * (s - y)
        out.append(y)
    return out


def highpass(samples, k):
    low = lowpass(samples, k)
    return [s - l for s, l in zip(samples, low)]


NOTES = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}


def hz(note):
    n = NOTES[note[0]]
    rest = note[1:]
    while rest and rest[0] in "#b":
        n += 1 if rest[0] == "#" else -1
        rest = rest[1:]
    return 440.0 * 2 ** ((12 * (int(rest) + 1) + n - 69) / 12)


# ───────────────────────────── instruments ─────────────────────────────

def lead(seconds, note, gain=0.5):
    """A rock lead: two saws a hair apart and a square under them, vibrato coming in on a long note."""
    f = hz(note)
    held = max(0.05, seconds * 0.92)
    vib = 0.006 if seconds > 0.3 else 0.0
    s = mix(tone(held, f, kind="saw", attack=0.006, release=0.35, vibrato=vib, vib_rate=6, gain=gain),
            tone(held, f * 1.006, kind="saw", attack=0.006, release=0.35, vibrato=vib, vib_rate=6, gain=gain * 0.7),
            tone(held, f / 2, kind="square", attack=0.006, release=0.4, gain=gain * 0.35))
    return cat(lowpass(s, 0.3), silence(seconds - held))


def strings(seconds, note, gain=0.5):
    """The orchestra's strings in octaves, bowing hard: the opening's knock."""
    f = hz(note)
    held = max(0.05, seconds * 0.95)
    s = mix(tone(held, f, kind="saw", attack=0.012, release=0.5, vibrato=0.004, vib_rate=5.5, gain=gain),
            tone(held, f / 2, kind="saw", attack=0.012, release=0.5, vibrato=0.004, vib_rate=5.2, gain=gain * 0.8),
            tone(held, f * 1.004, kind="saw", attack=0.015, release=0.5, gain=gain * 0.5))
    return cat(lowpass(s, 0.22), silence(seconds - held))


def bass(seconds, note, gain=0.6):
    f = hz(note)
    held = max(0.04, seconds * 0.85)
    s = mix(tone(held, f, kind="saw", attack=0.003, release=1.2, gain=gain), tone(held, f, kind="square", attack=0.003, release=1.5, gain=gain * 0.5))
    return cat(lowpass(s, 0.18), silence(seconds - held))


def brass(seconds, note, gain=0.5):
    f = hz(note)
    held = max(0.05, seconds * 0.9)
    s = mix(tone(held, f, kind="pulse", attack=0.03, release=0.4, vibrato=0.005, vib_rate=5, gain=gain), tone(held, f * 2, kind="saw", attack=0.04, release=0.5, gain=gain * 0.3))
    return cat(lowpass(s, 0.28), silence(seconds - held))


def kick(gain=1.0):
    return mix(tone(0.18, 140, 42, "sine", attack=0.001, release=1.5, gain=gain), tone(0.012, 2000, 800, "noise", attack=0.0005, release=1, gain=gain * 0.4))


def snare(gain=0.6):
    return mix(highpass(tone(0.17, 7000, 5000, "noise", attack=0.001, release=1.8, gain=gain), 0.25), tone(0.1, 220, 160, "tri", attack=0.001, release=1.5, gain=gain * 0.6))


def hat(gain=0.25, length=0.05):
    return highpass(tone(length, 9000, kind="noise", attack=0.0005, release=2.5, gain=gain), 0.6)


def crash(gain=0.4):
    return highpass(tone(1.1, 8000, 6000, "noise", attack=0.001, release=1.4, gain=gain), 0.45)


def timpani(note, gain=1.0, seconds=1.0):
    f = hz(note)
    return mix(tone(seconds, f * 1.05, f, "sine", attack=0.002, release=1.6, gain=gain), lowpass(tone(0.25, 900, 200, "noise", attack=0.001, release=2, gain=gain * 0.5), 0.2))


def play(bpm, notes, instrument, unit=0.5):
    """A line of (note, units); '-' is a rest. A unit is an eighth by default."""
    beat = 60.0 / bpm
    out = []
    for note, units in notes:
        d = units * unit * beat
        out.extend(silence(d) if note == "-" else instrument(d, note))
    return out


def fit(samples, length):
    """Cut or pad to an exact length, so a loop meets itself."""
    return samples[:length] + [0.0] * max(0, length - len(samples))


# ───────────────────────────── the music: Beethoven's Fifth ─────────────────────────────

# The development of the four-note knock, in C minor; then an answer, the horn call and the knock again.
KNOCK = [("-", 1), ("G4", 1), ("G4", 1), ("G4", 1), ("Eb4", 4),
         ("-", 1), ("Ab4", 1), ("Ab4", 1), ("Ab4", 1), ("G4", 4),
         ("-", 1), ("Eb5", 1), ("Eb5", 1), ("Eb5", 1), ("C5", 4),
         ("-", 1), ("G4", 1), ("G4", 1), ("G4", 1), ("D4", 4),
         ("-", 1), ("Ab4", 1), ("Ab4", 1), ("Ab4", 1), ("G4", 4),
         ("-", 1), ("F5", 1), ("F5", 1), ("F5", 1), ("D5", 4),
         ("-", 1), ("G5", 1), ("G5", 1), ("G5", 1), ("Eb5", 2), ("D5", 2),
         ("C5", 2), ("B4", 2), ("C5", 4)]
ANSWER = [("Bb4", 1), ("Bb4", 1), ("Bb4", 1), ("-", 1), ("Eb5", 4),
          ("F5", 4), ("Bb4", 4),
          ("Eb5", 2), ("D5", 2), ("C5", 2), ("Bb4", 2),
          ("Ab4", 2), ("G4", 2), ("F4", 2), ("Eb4", 2),
          ("-", 1), ("G4", 1), ("G4", 1), ("G4", 1), ("Eb4", 4),
          ("-", 1), ("F4", 1), ("F4", 1), ("F4", 1), ("D4", 4),
          ("-", 1), ("G4", 1), ("G4", 1), ("G4", 1), ("C5", 2), ("Eb5", 2),
          ("D5", 4), ("G4", 4)]
ROOTS = ["C", "Ab", "C", "G", "F", "G", "C", "G", "Eb", "Bb", "Eb", "Ab", "C", "G", "C", "G"]


def band(bpm, bars, roots, fill_every=4):
    """The rhythm section: a pumping eighth-note bass on each bar's root, and a rock beat with a crash at each phrase."""
    beat = 60.0 / bpm
    length = int(bars * 4 * beat * RATE)
    drums = [0.0] * length
    low = []
    for b in range(bars):
        t0 = b * 4 * beat
        root = roots[b % len(roots)]
        for e in range(8):
            octave = "3" if e in (3, 7) else "2"
            low.extend(bass(beat / 2, root + octave, 0.55))
            add(drums, t0 + e * beat / 2, hat(0.2 if e % 2 else 0.28))
        for k in (0, 2):
            add(drums, t0 + k * beat, kick(0.9))
        add(drums, t0 + 2.5 * beat, kick(0.6))
        for k in (1, 3):
            add(drums, t0 + k * beat, snare(0.55))
        if b % fill_every == fill_every - 1:
            for k in range(4):
                add(drums, t0 + 3 * beat + k * beat / 4, snare(0.3 + 0.08 * k))
        if b % 8 == 0:
            add(drums, t0, crash(0.35))
    return mix(fit(low, length), drums)


def fight_tune():
    """The fighting tune: the knock and its answer over the band, sixteen bars that loop seamlessly."""
    bpm = 156
    beat = 60.0 / bpm
    length = int(16 * 4 * beat * RATE)
    melody = play(bpm, KNOCK + ANSWER, lambda d, n: lead(d, n, 0.42))
    return mix(fit(melody, length), band(bpm, 16, ROOTS))


def fifth_title():
    """The title: the opening's knock as the orchestra plays it, twice, with the timpani; then the band drives in."""
    slow = 104
    e = 60.0 / slow / 2
    opening = []
    for first, last, held, drum in (("G4", "Eb4", 7, "C3"), ("F4", "D4", 9, "G2")):
        opening.extend(silence(e))
        for _ in range(3):
            opening.extend(strings(e, first, 0.55))
        opening.extend(mix(strings(e * held, last, 0.6), timpani(drum, 0.8, e * held), cat(silence(e * held * 0.6), timpani(drum, 0.5, e * held * 0.4))))
        opening.extend(silence(e * 1.5))
    bpm = 156
    beat = 60.0 / bpm
    count = cat(*[cat(hat(0.4, 0.04), silence(beat - 0.04)) for _ in range(4)])
    body_len = int(16 * 4 * beat * RATE)
    melody = play(bpm, KNOCK + ANSWER, lambda d, n: mix(lead(d, n, 0.4), strings(d, n, 0.18)))
    body = mix(fit(melody, body_len), band(bpm, 16, ROOTS))
    end = mix(lead(1.2, "C5", 0.5), strings(1.2, "C4", 0.4), bass(1.2, "C2", 0.7), kick(1.0), crash(0.5), timpani("C3", 0.8, 1.2))
    return cat(opening, count, body, end, silence(2.0))


# ───────────────────────────── effects ─────────────────────────────

def thump(seconds, f0, f1, gain=1.0):
    return tone(seconds, f0, f1, "sine", attack=0.001, release=2.2, gain=gain)


def snap(seconds, f, gain=1.0):
    return tone(seconds, f, f * 0.4, "noise", attack=0.0005, release=2.5, gain=gain)


def whoosh(seconds, f0, f1, gain=0.6):
    return lowpass(tone(seconds, f0, f1, "noise", attack=seconds * 0.4, release=0.8, gain=gain), 0.3)


def boom(seconds, gain=1.0):
    rumble = lowpass(tone(seconds, 1200, 160, "noise", attack=0.002, release=1.4, gain=gain), 0.12)
    return mix(rumble, tone(seconds * 0.8, 70, 28, "sine", attack=0.002, release=1.4, gain=gain), snap(0.08, 5000, gain * 0.7))


def clank(seconds, f, gain=0.6):
    return mix(tone(seconds, f, kind="sine", attack=0.001, release=2.5, gain=gain), tone(seconds, f * 1.47, kind="sine", attack=0.001, release=3, gain=gain * 0.6),
               tone(seconds * 0.7, f * 2.31, kind="sine", attack=0.001, release=3.5, gain=gain * 0.4))


def voice_cry(text, pitch, fall=0.6, speed=1.0, size=1.0, seed=3):
    return say(text, rate=RATE, pitch=pitch, speed=speed, size=size, seed=seed, bend=lambda t: 1.0 - fall * t)


def main():
    os.makedirs(OUT, exist_ok=True)
    for f in os.listdir(OUT):
        if f.endswith(".wav"):
            os.remove(os.path.join(OUT, f))
    # Fists and feet.
    write("Punch.wav", mix(thump(0.14, 160, 70), snap(0.05, 3200, 0.8)), 0.6)
    write("Kick.wav", mix(thump(0.2, 120, 50), snap(0.06, 2200, 0.7), lowpass(snap(0.12, 900, 0.5), 0.3)), 0.65)
    write("Swish.wav", whoosh(0.13, 500, 2600, 0.6), 0.22)
    write("Knee.wav", mix(thump(0.18, 110, 55), lowpass(snap(0.1, 1200, 0.7), 0.25)), 0.62)
    write("Slam.wav", cat(whoosh(0.1, 300, 1200, 0.4), mix(thump(0.35, 90, 35, 1.1), lowpass(snap(0.2, 1500, 0.8), 0.2))), 0.7)
    write("Throw.wav", cat(whoosh(0.22, 400, 2000, 0.7), mix(thump(0.3, 80, 30), lowpass(snap(0.2, 1200, 0.6), 0.2))), 0.65)
    write("Slap.wav", mix(highpass(snap(0.08, 6000, 1.0), 0.3), thump(0.12, 300, 120, 0.6)), 0.6)
    write("Headbutt.wav", mix(tone(0.25, 420, 140, "tri", attack=0.001, release=1.6), snap(0.04, 4000, 0.6)), 0.55)
    # Weapons.
    crack = cat(whoosh(0.16, 300, 3500, 0.5), highpass(mix(snap(0.035, 9000, 1.2), tone(0.03, 4000, 1500, "square", attack=0.0003, release=2, gain=0.5)), 0.4))
    write("Whip.wav", crack, 0.6)
    write("Bat.wav", mix(clank(0.22, 380, 0.7), thump(0.2, 140, 60), lowpass(snap(0.08, 2400, 0.6), 0.4)), 0.6)
    write("KnifeThrow.wav", mix(whoosh(0.25, 1500, 4000, 0.4), tone(0.3, 3200, 3000, "sine", attack=0.01, release=2, gain=0.3), tone(0.3, 4700, kind="sine", attack=0.01, release=2.5, gain=0.2)), 0.4)
    write("Stab.wav", mix(clank(0.18, 1900, 0.4), thump(0.15, 140, 60), snap(0.05, 3000, 0.5)), 0.55)
    write("Toss.wav", whoosh(0.28, 300, 1400, 0.8), 0.35)
    write("Crash.wav", mix(boom(0.45, 0.5), clank(0.5, 210, 0.5), clank(0.4, 330, 0.3), snap(0.3, 3000, 0.6)), 0.6)
    write("Dynamite.wav", cat(boom(1.3, 1.0)), 0.8)
    write("Pickup.wav", cat(tone(0.05, hz("E5"), kind="pulse", release=0.6), tone(0.09, hz("B5"), kind="pulse", release=1.0)), 0.25)
    write("Grab.wav", mix(lowpass(snap(0.12, 2500, 0.6), 0.5), thump(0.08, 200, 120, 0.4)), 0.4)
    write("BodySlam.wav", mix(thump(0.4, 75, 28, 1.2), lowpass(snap(0.3, 1000, 0.8), 0.15), snap(0.05, 3000, 0.3)), 0.75)
    # Voices: the gang's grunts and yells, the brothers' gasps, the boss's laugh.
    write("Grunt.wav", say("AH1 HH !", rate=RATE, pitch=100, speed=1.1, size=1.25, seed=4), 0.45)
    write("Yell.wav", voice_cry("AA1 AA ,", 170, fall=0.55, speed=0.9, size=1.1, seed=6), 0.5)
    write("Hurt.wav", say("AH1 HH !", rate=RATE, pitch=135, speed=1.5, size=1.0, seed=8), 0.45)
    write("Death.wav", mix(voice_cry("AA1 AA R !", 190, fall=0.6, speed=0.7, seed=9), cat(silence(0.3), play(70, [("G3", 1), ("Eb3", 3)], lambda d, n: strings(d, n, 0.3)))), 0.6)
    write("Fall.wav", mix(tone(1.0, 1400, 300, "sine", attack=0.01, release=0.6, gain=0.4), cat(silence(0.05), voice_cry("AA1 AA AA", 220, fall=0.7, speed=0.6, seed=11)),
                          cat(silence(0.95), lowpass(tone(0.4, 1500, 400, "noise", attack=0.01, release=1.2, gain=0.6), 0.3))), 0.5)
    write("Laugh.wav", say("HH AA1 , HH AA1 , HH AA1 , HH AA1 !", rate=RATE, pitch=82, speed=0.8, size=1.35, seed=12), 0.6)
    write("Jump.wav", whoosh(0.16, 600, 1800, 0.5), 0.2)
    write("Charge.wav", cat(*[mix(thump(0.12, 90, 40, 0.9), snap(0.03, 1500, 0.3)) + silence(0.06) for _ in range(5)]), 0.6)
    write("Gun.wav", mix(snap(0.09, 7000, 1.0), thump(0.1, 200, 80, 0.8), tone(0.06, 1200, 300, "square", attack=0.0005, release=2, gain=0.3)), 0.5)
    write("Cock.wav", cat(clank(0.06, 2300, 0.5), silence(0.08), clank(0.08, 1700, 0.6)), 0.4)
    # The street: the belt, the spikes, the garage door, the statue, the ladder.
    clatter = []
    for k in range(12):
        clatter.extend(mix(clank(0.05, 600 + (k % 3) * 90, 0.3), lowpass(tone(0.1, 200, kind="noise", attack=0.002, release=1, gain=0.25), 0.2)))
    write("Conveyor.wav", mix(clatter, tone(len(clatter) / RATE, 55, kind="saw", attack=0.05, release=0.2, gain=0.15)), 0.35)
    write("SpikeWall.wav", cat(lowpass(tone(0.25, 300, 120, "noise", attack=0.02, release=0.5, gain=0.8), 0.1), mix(clank(0.35, 1300, 0.6), clank(0.3, 1800, 0.4), thump(0.25, 90, 40, 0.8))), 0.6)
    rattle = [s * (0.5 + 0.5 * (1 if (i // 600) % 2 else -0.2)) for i, s in enumerate(lowpass(tone(1.2, 900, 600, "noise", attack=0.02, release=0.3, gain=0.8), 0.3))]
    write("Garage.wav", mix(rattle, tone(1.2, 70, 90, "saw", attack=0.05, release=0.3, gain=0.2)), 0.45)
    write("Statue.wav", mix(lowpass(tone(2.2, 200, 80, "noise", attack=0.2, release=0.4, gain=1.0), 0.06), tone(2.2, 45, 38, "saw", attack=0.3, release=0.5, gain=0.3)), 0.6)
    write("Climb.wav", cat(*[cat(clank(0.07, 900 + k * 60, 0.5), silence(0.18)) for k in range(4)]), 0.35)
    write("Go.wav", cat(tone(0.1, hz("G5"), kind="pulse", release=0.4), silence(0.04), tone(0.22, hz("C6"), kind="pulse", release=1.0)), 0.28)
    write("TimeWarning.wav", cat(*[cat(tone(0.12, 988, kind="square", release=0.2), silence(0.08)) for _ in range(4)]), 0.25)
    write("OneUp.wav", cat(*[tone(0.07, hz(n), kind="pulse", release=0.5) for n in ["C5", "E5", "G5", "C6", "E6"]], tone(0.25, hz("G6"), kind="pulse", release=1)), 0.25)
    write("Join.wav", play(220, [("G4", 1), ("C5", 1), ("E5", 1), ("G5", 3)], lambda d, n: brass(d, n, 0.5)), 0.4)
    # The stings: the knock to start a mission, a major cadence to end it, the duel, the ending, the end.
    write("MissionStart.wav", mix(play(120, [("-", 1), ("G4", 1), ("G4", 1), ("G4", 1), ("Eb4", 6)], lambda d, n: strings(d, n, 0.55)),
                                  play(120, [("-", 4), ("C3", 6)], lambda d, n: timpani(n, 0.8, d)), play(120, [("-", 4), ("C2", 6)], lambda d, n: bass(d, n, 0.5))), 0.5)
    write("MissionClear.wav", mix(play(150, [("G4", 1), ("C5", 1), ("E5", 1), ("G5", 2), ("E5", 1), ("G5", 1), ("C6", 5)], lambda d, n: brass(d, n, 0.5)),
                                  play(150, [("C3", 2), ("G2", 2), ("C3", 2), ("C2", 6)], lambda d, n: bass(d, n, 0.6)), cat(silence(60 / 150 * 3), crash(0.4))), 0.45)
    write("Duel.wav", mix(play(80, [("C3", 1), ("-", 1), ("C3", 1), ("-", 1), ("F#3", 4)], lambda d, n: strings(d, n, 0.6)),
                          play(80, [("Eb4", 1), ("-", 1), ("Eb4", 1), ("-", 1), ("A4", 4)], lambda d, n: brass(d, n, 0.4)),
                          play(80, [("C2", 1), ("-", 1), ("C2", 1), ("-", 1), ("F#2", 4)], lambda d, n: timpani(n, 0.9, d))), 0.55)
    write("Victory.wav", mix(play(132, [("C5", 2), ("E5", 1), ("G5", 1), ("C6", 4), ("B5", 2), ("G5", 2), ("C6", 6)], lambda d, n: brass(d, n, 0.5)),
                             play(132, [("C4", 4), ("G3", 4), ("E4", 4), ("C4", 6)], lambda d, n: strings(d, n, 0.35)),
                             play(132, [("C2", 4), ("G2", 4), ("C2", 4), ("C2", 6)], lambda d, n: bass(d, n, 0.5))), 0.45)
    write("GameOver.wav", mix(play(66, [("-", 1), ("G3", 1), ("G3", 1), ("G3", 1), ("Eb3", 4), ("-", 1), ("F3", 1), ("F3", 1), ("F3", 1), ("C3", 6)], lambda d, n: strings(d, n, 0.55)),
                              play(66, [("-", 4), ("C2", 4), ("-", 4), ("C2", 6)], lambda d, n: timpani(n, 0.7, d))), 0.5)
    # The music.
    write("Music.wav", fight_tune(), 0.42)
    write("TitleTune.wav", fifth_title(), 0.45)


if __name__ == "__main__":
    main()
