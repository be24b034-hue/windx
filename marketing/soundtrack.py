"""Original soundtrack for the WindX film (27 s), synthesised from scratch with numpy.

Cue sheet (seconds) matches marketing/reel.html:
  0.0  wind swell while the logo draws         2.9  first hit, "Your screen has a new top."
  5.2  island appears, arpeggios start          9.6  montage: drums in, a whoosh on every cut
 21.1  numbers, riser                           23.7  outro hit, then the chord rings out
Usage: python soundtrack.py out.wav
"""
import sys, wave
import numpy as np

SR = 44100
DUR = 27.0
N = int(SR * DUR)
t = np.arange(N) / SR
mix = np.zeros((N, 2))
rng = np.random.default_rng(7)

def at(sec):
    return int(sec * SR)

def add(sig, start, gain=1.0, pan=0.0):
    i = at(start)
    j = min(N, i + len(sig))
    if j <= i:
        return
    s = sig[: j - i] * gain
    mix[i:j, 0] += s * (1 - max(0, pan))
    mix[i:j, 1] += s * (1 + min(0, pan))

def env(n, a=0.005, r=0.3, curve=3.0):
    x = np.arange(n) / SR
    e = np.minimum(1, x / max(a, 1e-4)) * np.exp(-x / max(r, 1e-4) * curve / 3)
    return e

def lowpass(x, cutoff):
    # simple one-pole, cutoff may be an array (sweeps)
    y = np.zeros_like(x)
    c = np.broadcast_to(np.asarray(cutoff, dtype=float), x.shape)
    a = 1 - np.exp(-2 * np.pi * c / SR)
    acc = 0.0
    for i in range(len(x)):
        acc += a[i] * (x[i] - acc)
        y[i] = acc
    return y

def note(f):
    return 440.0 * 2 ** ((f - 69) / 12)

# ---------------------------------------------------------------- pad: warm detuned saws, chord progression
chords = [  # (start, end, midi notes)  A minor 9 -> F maj7 -> C add9 -> G sus
    (0.0, 5.2, [57, 64, 67, 71, 72]),
    (5.2, 9.6, [53, 60, 64, 69, 72]),
    (9.6, 14.1, [57, 64, 67, 71, 76]),
    (14.1, 18.7, [53, 60, 64, 69, 72]),
    (18.7, 23.7, [55, 62, 67, 69, 74]),
    (23.7, 27.0, [57, 64, 67, 71, 76]),
]
pad = np.zeros(N)
for s, e, notes in chords:
    i, j = at(s), at(e) + at(0.4)
    j = min(j, N)
    tt = np.arange(j - i) / SR
    seg = np.zeros(j - i)
    for m in notes:
        for det in (-0.07, 0.0, 0.07):
            f = note(m) * 2 ** (det / 12)
            seg += ((tt * f) % 1.0 * 2 - 1) * 0.05
    fade = np.minimum(1, tt / 0.35) * np.minimum(1, (len(tt) / SR - tt) / 0.4)
    pad[i:j] += seg * fade
cut = 500 + 2600 * np.clip((t - 2.9) / 6.7, 0, 1) + 1800 * np.clip((t - 9.6) / 1.0, 0, 1) - 1500 * np.clip((t - 24.5) / 2.5, 0, 1)
pad = lowpass(pad, cut)
add(pad, 0, 0.9, -0.15)
add(np.roll(pad, at(0.012)), 0, 0.9, 0.15)

# ---------------------------------------------------------------- wind swell under the logo, and a riser before the outro
noise = rng.standard_normal(N)
swell = lowpass(noise, 300 + 3000 * np.clip(t / 2.9, 0, 1) ** 2) * np.clip(t / 2.9, 0, 1) ** 2 * (t < 3.0)
add(swell, 0, 0.35)
riser_t = np.clip((t - 21.1) / 2.6, 0, 1)
riser = lowpass(noise, 400 + 7000 * riser_t ** 2) * riser_t ** 3 * (t < 23.75)
add(riser, 0, 0.32)

# ---------------------------------------------------------------- hits: sub boom + noise crack
def hit(gain=1.0):
    n = at(1.6)
    tt = np.arange(n) / SR
    f = 30 + 90 * np.exp(-tt * 18)
    sub = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-tt * 2.4)
    crack = lowpass(rng.standard_normal(n), 3500) * np.exp(-tt * 14)
    return (sub * 0.9 + crack * 0.35) * gain

for s, g in ((2.9, 1.0), (9.6, 0.9), (23.7, 1.1)):
    add(hit(g), s)

# ---------------------------------------------------------------- whoosh on every montage cut
def whoosh():
    n = at(0.55)
    tt = np.arange(n) / SR
    shape = np.sin(np.pi * np.clip(tt / 0.55, 0, 1)) ** 2
    return lowpass(rng.standard_normal(n), 800 + 6000 * tt / 0.55) * shape

for s in (11.1, 12.6, 14.1, 15.6, 17.1, 18.7, 20.9):
    add(whoosh(), s - 0.35, 0.45, rng.uniform(-0.4, 0.4))

# ---------------------------------------------------------------- drums from the montage on (120 bpm)
def kick():
    n = at(0.45)
    tt = np.arange(n) / SR
    f = 45 + 120 * np.exp(-tt * 30)
    return np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-tt * 7)

def snap():
    n = at(0.25)
    tt = np.arange(n) / SR
    body = np.sin(2 * np.pi * 190 * tt) * np.exp(-tt * 30)
    return (lowpass(rng.standard_normal(n), 6000) * np.exp(-tt * 22) + body * 0.4)

def hat():
    n = at(0.06)
    tt = np.arange(n) / SR
    x = rng.standard_normal(n)
    return (x - lowpass(x, 6000)) * np.exp(-tt * 60)

beat = 0.5
b = 9.6
while b < 23.6:
    add(kick(), b, 0.85)
    k = round((b - 9.6) / beat)
    if k % 2 == 1:
        add(snap(), b, 0.32)
    add(hat(), b + beat / 2, 0.18, 0.3)
    b += beat

# ---------------------------------------------------------------- bass on the root, eighth-note pulse
roots = [(5.2, 9.6, 41), (9.6, 14.1, 45), (14.1, 18.7, 41), (18.7, 23.7, 43)]
for s, e, m in roots:
    x = s + (0 if s >= 9.6 else 0)
    while x < e - 0.01:
        n = at(0.24)
        tt = np.arange(n) / SR
        f = note(m)
        tone = (np.sin(2 * np.pi * f * tt) + 0.35 * np.sin(4 * np.pi * f * tt)) * env(n, 0.004, 0.2)
        add(tone, x, 0.32 if s >= 9.6 else 0.16)
        x += 0.25

# ---------------------------------------------------------------- arpeggio: bell-like plucks walking the chord
arp_chords = [(5.2, 9.6, [69, 72, 76, 79]), (9.6, 14.1, [69, 72, 76, 81]), (14.1, 18.7, [65, 69, 72, 76]), (18.7, 23.7, [67, 71, 74, 79])]
for s, e, notes in arp_chords:
    x, k = s, 0
    while x < e - 0.01:
        n = at(0.5)
        tt = np.arange(n) / SR
        f = note(notes[[0, 1, 2, 3, 2, 1][k % 6]])
        bell = (np.sin(2 * np.pi * f * tt) + 0.25 * np.sin(2 * np.pi * f * 2.01 * tt) + 0.1 * np.sin(2 * np.pi * f * 3.98 * tt)) * env(n, 0.002, 0.28)
        add(bell, x, 0.12, 0.35 if k % 2 else -0.35)
        x += 0.25
        k += 1

# ---------------------------------------------------------------- final chime on the logo
for i, m in enumerate([81, 84, 88]):
    n = at(3.0)
    tt = np.arange(n) / SR
    f = note(m)
    add((np.sin(2 * np.pi * f * tt) + 0.2 * np.sin(2 * np.pi * f * 3 * tt)) * env(n, 0.003, 1.6), 23.8 + i * 0.12, 0.13, (i - 1) * 0.4)

# ---------------------------------------------------------------- simple room: short stereo echoes
wet = np.zeros_like(mix)
for d, g in ((0.031, 0.25), (0.057, 0.18), (0.083, 0.12), (0.142, 0.08)):
    k = at(d)
    wet[k:, 0] += mix[:-k, 1] * g
    wet[k:, 1] += mix[:-k, 0] * g
mix = mix + wet

# ---------------------------------------------------------------- master: fade, soft clip, normalise
fade = np.clip((DUR - t) / 2.0, 0, 1)[:, None] * np.clip(t / 0.05, 0, 1)[:, None]
mix *= fade
mix = np.tanh(mix * 1.2)
mix /= np.max(np.abs(mix)) + 1e-9
mix *= 0.89
out = (mix * 32767).astype(np.int16)
with wave.open(sys.argv[1], "wb") as w:
    w.setnchannels(2)
    w.setsampwidth(2)
    w.setframerate(SR)
    w.writeframes(out.tobytes())
print("ok", sys.argv[1])
