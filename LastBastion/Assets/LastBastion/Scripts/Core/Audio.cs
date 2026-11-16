using System.Collections.Generic;
using UnityEngine;

namespace LastBastion
{
    /// <summary>Sound effect names. Drop a clip with the same name into Assets/LastBastion/Resources/Audio/ to replace it.</summary>
    public static class Sfx
    {
        public const string Click = "click";
        public const string Place = "place";
        public const string Hammer = "hammer";
        public const string Arrow = "arrow";
        public const string Catapult = "catapult";
        public const string Impact = "impact";
        public const string Zap = "zap";
        public const string Hit = "hit";
        public const string Die = "die";
        public const string Coin = "coin";
        public const string Shard = "shard";
        public const string Horn = "horn";
        public const string Roar = "roar";
        public const string Axe = "axe";
        public const string Eruption = "eruption";
        public const string Rockslide = "rockslide";
        public const string Dragonfire = "dragonfire";
        public const string Slam = "slam";
        public const string Hurt = "hurt";
        public const string HeroDown = "hero_down";
        public const string LevelUp = "levelup";
        public const string Upgrade = "upgrade";
        public const string Sell = "sell";
        public const string LifeLost = "lifelost";
        public const string Interrupt = "interrupt";
        public const string Victory = "victory";
        public const string Defeat = "defeat";
        public const string Warn = "warn";
        public const string Wind = "wind";
        public const string Error = "error";
        public const string Wail = "wail";
        public const string Raise = "raise";
        public const string Bones = "bones";
    }

    public static class Music
    {
        public const string Title = "music_title";
        public const string Battle = "music_battle";
        public const string Boss = "music_boss";
        public const string Marsh = "music_marsh";
    }

    /// <summary>
    /// Plays music and sound effects. Every clip is synthesized in code the first time it is needed,
    /// unless a real clip with the same name exists in Resources/Audio (then that is used instead).
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        static AudioManager inst;
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();
        AudioSource musicA, musicB;
        AudioSource[] pool;
        int next;
        string currentTrack;
        float fade = 1f;

        public static float MusicVolume
        {
            get { return PlayerPrefs.GetFloat("LB_music", 0.55f); }
            set { PlayerPrefs.SetFloat("LB_music", Mathf.Clamp01(value)); Ensure().ApplyVolume(); }
        }

        public static float SfxVolume
        {
            get { return PlayerPrefs.GetFloat("LB_sfx", 0.8f); }
            set { PlayerPrefs.SetFloat("LB_sfx", Mathf.Clamp01(value)); }
        }

        public static bool Muted
        {
            get { return PlayerPrefs.GetInt("LB_mute", 0) == 1; }
            set { PlayerPrefs.SetInt("LB_mute", value ? 1 : 0); Ensure().ApplyVolume(); }
        }

        static AudioManager Ensure()
        {
            if (inst == null)
            {
                var go = new GameObject("Audio");
                DontDestroyOnLoad(go);
                inst = go.AddComponent<AudioManager>();
                inst.Init();
            }
            return inst;
        }

        void Init()
        {
            musicA = gameObject.AddComponent<AudioSource>();
            musicB = gameObject.AddComponent<AudioSource>();
            foreach (var m in new[] { musicA, musicB }) { m.loop = true; m.playOnAwake = false; m.spatialBlend = 0f; m.volume = 0f; }
            pool = new AudioSource[16];
            for (int i = 0; i < pool.Length; i++)
            {
                pool[i] = gameObject.AddComponent<AudioSource>();
                pool[i].playOnAwake = false;
                pool[i].spatialBlend = 0f;
            }
        }

        public static void Play(string name, float volume = 1f, float pitchJitter = 0.06f)
        {
            if (Muted) return;
            var a = Ensure();
            float now = Time.unscaledTime;
            float last;
            if (a.lastPlayed.TryGetValue(name, out last) && now - last < 0.045f) return; // stop spam
            a.lastPlayed[name] = now;
            var clip = a.Clip(name);
            if (clip == null) return;
            var src = a.pool[a.next];
            a.next = (a.next + 1) % a.pool.Length;
            src.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            src.PlayOneShot(clip, volume * SfxVolume);
        }

        public static void PlayMusic(string track)
        {
            var a = Ensure();
            if (a.currentTrack == track) return;
            a.currentTrack = track;
            var clip = a.Clip(track);
            if (clip == null) return;
            // Cross-fade: the idle source takes the new track.
            var tmp = a.musicA; a.musicA = a.musicB; a.musicB = tmp;
            a.musicA.clip = clip;
            a.musicA.volume = 0f;
            a.musicA.Play();
            a.fade = 0f;
        }

        void ApplyVolume() { }

        void Update()
        {
            float target = Muted ? 0f : MusicVolume * 0.6f;
            fade = Mathf.Min(1f, fade + Time.unscaledDeltaTime / 1.2f);
            if (musicA != null) musicA.volume = target * fade;
            if (musicB != null)
            {
                musicB.volume = target * (1f - fade);
                if (fade >= 1f && musicB.isPlaying) musicB.Stop();
            }
        }

        AudioClip Clip(string name)
        {
            AudioClip c;
            if (clips.TryGetValue(name, out c)) return c;
            c = Resources.Load<AudioClip>("Audio/" + name);
            if (c == null) c = Synth.Make(name);
            clips[name] = c;
            return c;
        }
    }

    /// <summary>A tiny software synthesizer for placeholder audio.</summary>
    public static class Synth
    {
        public const int Rate = 22050;
        enum Wave { Sine, Square, Saw, Tri, Noise }

        static float Osc(Wave w, float phase, float duty = 0.5f)
        {
            float p = phase - Mathf.Floor(phase);
            switch (w)
            {
                case Wave.Square: return p < duty ? 0.6f : -0.6f;
                case Wave.Saw: return (p * 2f - 1f) * 0.6f;
                case Wave.Tri: return p < 0.5f ? p * 4f - 1f : 3f - p * 4f;
                case Wave.Noise: return Random.value * 2f - 1f;
                default: return Mathf.Sin(p * 2f * Mathf.PI);
            }
        }

        /// <summary>Add a tone with a pitch sweep and an attack/decay envelope.</summary>
        static void Tone(float[] buf, float start, float dur, float f0, float f1, Wave w, float vol, float attack = 0.005f, float release = -1f, float duty = 0.5f, float vibrato = 0f)
        {
            int s0 = Mathf.Max(0, (int)(start * Rate));
            int n = (int)(dur * Rate);
            if (release < 0f) release = dur;
            double phase = 0;
            for (int i = 0; i < n && s0 + i < buf.Length; i++)
            {
                float t = (float)i / Rate;
                float k = (float)i / n;
                float f = Mathf.Lerp(f0, f1, k);
                if (vibrato > 0f) f *= 1f + vibrato * Mathf.Sin(t * 2f * Mathf.PI * 5.5f);
                phase += f / Rate;
                float env = Mathf.Min(1f, t / Mathf.Max(0.0005f, attack));
                float rel = dur - t;
                if (rel < release) env *= Mathf.Clamp01(rel / release);
                buf[s0 + i] += Osc(w, (float)phase, duty) * vol * env;
            }
        }

        /// <summary>Filtered noise (lowpass amount 0..1, 1 = darkest).</summary>
        static void Noise(float[] buf, float start, float dur, float vol, float lowpass, float attack = 0.002f, float hp = 0f)
        {
            int s0 = Mathf.Max(0, (int)(start * Rate));
            int n = (int)(dur * Rate);
            float y = 0f, prev = 0f;
            float a = 1f - Mathf.Clamp(lowpass, 0f, 0.995f);
            for (int i = 0; i < n && s0 + i < buf.Length; i++)
            {
                float t = (float)i / Rate;
                float x = Random.value * 2f - 1f;
                y += a * (x - y);
                float o = y - hp * prev;
                prev = y;
                float env = Mathf.Min(1f, t / Mathf.Max(0.0005f, attack)) * (1f - (float)i / n);
                buf[s0 + i] += o * vol * env;
            }
        }

        static AudioClip ToClip(string name, float[] buf)
        {
            for (int i = 0; i < buf.Length; i++) buf[i] = (float)System.Math.Tanh(buf[i] * 1.2f);
            var clip = AudioClip.Create(name, buf.Length, 1, Rate, false);
            clip.SetData(buf, 0);
            return clip;
        }

        static float[] Buf(float seconds) => new float[Mathf.Max(1, (int)(seconds * Rate))];

        public static AudioClip Make(string name)
        {
            float[] b;
            switch (name)
            {
                case Sfx.Click: b = Buf(0.06f); Tone(b, 0, 0.05f, 1300, 900, Wave.Square, 0.25f, 0.001f, 0.04f, 0.3f); break;
                case Sfx.Place: b = Buf(0.35f); Tone(b, 0, 0.3f, 180, 55, Wave.Sine, 0.9f); Noise(b, 0, 0.12f, 0.5f, 0.85f); break;
                case Sfx.Hammer:
                    b = Buf(0.3f); Noise(b, 0, 0.04f, 0.6f, 0.2f);
                    Tone(b, 0, 0.25f, 1180, 1160, Wave.Sine, 0.35f); Tone(b, 0, 0.2f, 1760, 1740, Wave.Sine, 0.2f); break;
                case Sfx.Arrow: b = Buf(0.16f); Tone(b, 0, 0.1f, 520, 260, Wave.Saw, 0.25f, 0.001f); Noise(b, 0, 0.14f, 0.25f, 0.4f); break;
                case Sfx.Catapult: b = Buf(0.4f); Tone(b, 0, 0.18f, 130, 90, Wave.Saw, 0.3f); Noise(b, 0.05f, 0.3f, 0.35f, 0.7f, 0.15f); break;
                case Sfx.Impact: b = Buf(0.45f); Tone(b, 0, 0.35f, 100, 38, Wave.Sine, 1f); Noise(b, 0, 0.3f, 0.7f, 0.9f); break;
                case Sfx.Zap: b = Buf(0.22f); Tone(b, 0, 0.18f, 1500, 320, Wave.Square, 0.22f, 0.001f, -1f, 0.25f); Tone(b, 0, 0.12f, 2200, 700, Wave.Sine, 0.2f); break;
                case Sfx.Hit: b = Buf(0.08f); Noise(b, 0, 0.05f, 0.4f, 0.5f); Tone(b, 0, 0.06f, 300, 180, Wave.Square, 0.15f); break;
                case Sfx.Die: b = Buf(0.25f); Tone(b, 0, 0.2f, 380, 110, Wave.Square, 0.2f, 0.002f, -1f, 0.35f); Noise(b, 0, 0.15f, 0.3f, 0.7f); break;
                case Sfx.Coin: b = Buf(0.22f); Tone(b, 0, 0.06f, 1320, 1320, Wave.Square, 0.16f, 0.001f, 0.02f, 0.25f); Tone(b, 0.06f, 0.15f, 1760, 1760, Wave.Square, 0.16f, 0.001f, 0.12f, 0.25f); break;
                case Sfx.Shard:
                    b = Buf(0.6f);
                    Tone(b, 0, 0.5f, 880, 880, Wave.Sine, 0.3f); Tone(b, 0.08f, 0.45f, 1320, 1320, Wave.Sine, 0.25f); Tone(b, 0.16f, 0.4f, 1760, 1760, Wave.Sine, 0.2f); break;
                case Sfx.Horn:
                    b = Buf(1.3f);
                    Tone(b, 0, 1.2f, 196, 196, Wave.Saw, 0.32f, 0.12f, 0.4f, 0.5f, 0.01f); Tone(b, 0, 1.2f, 294, 294, Wave.Saw, 0.22f, 0.15f, 0.4f, 0.5f, 0.01f); break;
                case Sfx.Roar: b = Buf(1.5f); Tone(b, 0, 1.4f, 120, 70, Wave.Saw, 0.5f, 0.1f, 0.6f, 0.5f, 0.04f); Noise(b, 0, 1.4f, 0.5f, 0.92f, 0.1f); break;
                case Sfx.Axe: b = Buf(0.5f); Noise(b, 0, 0.35f, 0.4f, 0.6f, 0.15f); Tone(b, 0.35f, 0.15f, 900, 700, Wave.Square, 0.2f); break;
                case Sfx.Eruption: b = Buf(1.4f); Noise(b, 0, 1.3f, 1f, 0.96f, 0.05f); Tone(b, 0, 1.2f, 60, 35, Wave.Sine, 0.6f, 0.05f); break;
                case Sfx.Rockslide:
                    b = Buf(2f); Noise(b, 0, 1.9f, 0.9f, 0.95f, 0.2f);
                    for (int i = 0; i < 6; i++) Tone(b, 0.2f + i * 0.25f, 0.25f, 110 - i * 8, 45, Wave.Sine, 0.6f);
                    break;
                case Sfx.Dragonfire: b = Buf(1.2f); Noise(b, 0, 1.1f, 0.9f, 0.75f, 0.2f); Tone(b, 0, 1f, 90, 60, Wave.Saw, 0.3f, 0.1f, 0.5f, 0.5f, 0.05f); break;
                case Sfx.Slam: b = Buf(0.6f); Tone(b, 0, 0.5f, 140, 40, Wave.Sine, 1f); Noise(b, 0, 0.25f, 0.6f, 0.8f); Tone(b, 0, 0.3f, 600, 580, Wave.Sine, 0.15f); break;
                case Sfx.Hurt: b = Buf(0.15f); Tone(b, 0, 0.12f, 240, 150, Wave.Square, 0.2f); break;
                case Sfx.HeroDown: b = Buf(0.8f); Tone(b, 0, 0.7f, 330, 90, Wave.Saw, 0.3f, 0.01f, 0.4f); break;
                case Sfx.LevelUp:
                    b = Buf(0.7f);
                    { float[] n = { 523, 659, 784, 1047 }; for (int i = 0; i < 4; i++) Tone(b, i * 0.09f, 0.3f, n[i], n[i], Wave.Square, 0.16f, 0.002f, 0.2f, 0.3f); }
                    break;
                case Sfx.Upgrade:
                    b = Buf(0.4f);
                    { float[] n = { 440, 554, 659 }; for (int i = 0; i < 3; i++) Tone(b, i * 0.07f, 0.2f, n[i], n[i], Wave.Tri, 0.35f, 0.002f, 0.15f); }
                    break;
                case Sfx.Sell: b = Buf(0.25f); Tone(b, 0, 0.08f, 1760, 1760, Wave.Square, 0.15f, 0.001f, 0.05f, 0.25f); Tone(b, 0.08f, 0.14f, 1320, 1320, Wave.Square, 0.15f, 0.001f, 0.1f, 0.25f); break;
                case Sfx.LifeLost: b = Buf(0.45f); Tone(b, 0, 0.15f, 330, 300, Wave.Square, 0.25f); Tone(b, 0.18f, 0.22f, 220, 200, Wave.Square, 0.25f); break;
                case Sfx.Interrupt:
                    b = Buf(0.55f); Noise(b, 0, 0.2f, 0.5f, 0.1f, 0.001f, 0.9f);
                    Tone(b, 0, 0.5f, 2400, 2350, Wave.Sine, 0.25f); Tone(b, 0, 0.45f, 3150, 3100, Wave.Sine, 0.18f); break;
                case Sfx.Victory:
                    b = Buf(2.2f);
                    { float[] n = { 523, 659, 784, 1047, 784, 1047 }; float[] d = { 0.15f, 0.15f, 0.15f, 0.45f, 0.2f, 0.9f }; float t = 0;
                      for (int i = 0; i < n.Length; i++) { Tone(b, t, d[i] + 0.1f, n[i], n[i], Wave.Square, 0.18f, 0.005f, 0.1f, 0.3f); Tone(b, t, d[i] + 0.1f, n[i] / 2, n[i] / 2, Wave.Tri, 0.25f); t += d[i]; } }
                    break;
                case Sfx.Defeat:
                    b = Buf(2f);
                    { float[] n = { 392, 349, 311, 262 }; for (int i = 0; i < 4; i++) Tone(b, i * 0.38f, 0.5f, n[i], n[i] * 0.98f, Wave.Saw, 0.2f, 0.01f, 0.3f); }
                    break;
                case Sfx.Warn: b = Buf(0.35f); Tone(b, 0, 0.14f, 880, 880, Wave.Square, 0.15f, 0.002f, 0.05f, 0.3f); Tone(b, 0.16f, 0.14f, 660, 660, Wave.Square, 0.15f, 0.002f, 0.05f, 0.3f); break;
                case Sfx.Wind: b = Buf(2.5f); Noise(b, 0, 2.4f, 0.5f, 0.97f, 1f); break;
                case Sfx.Error: b = Buf(0.18f); Tone(b, 0, 0.16f, 160, 140, Wave.Square, 0.2f); break;
                case Sfx.Wail:
                    b = Buf(1.3f);
                    Tone(b, 0, 1.2f, 1250, 700, Wave.Sine, 0.3f, 0.06f, 0.5f, 0.5f, 0.035f);
                    Tone(b, 0, 1.2f, 1870, 1050, Wave.Sine, 0.14f, 0.08f, 0.5f, 0.5f, 0.04f);
                    Noise(b, 0, 1.1f, 0.12f, 0.2f, 0.1f, 0.9f);
                    break;
                case Sfx.Raise:
                    b = Buf(0.9f); Noise(b, 0, 0.85f, 0.5f, 0.95f, 0.2f);
                    Tone(b, 0, 0.8f, 110, 220, Wave.Saw, 0.22f, 0.1f, 0.3f);
                    Tone(b, 0.2f, 0.6f, 165, 330, Wave.Tri, 0.2f, 0.05f, 0.3f);
                    break;
                case Sfx.Bones:
                    b = Buf(0.3f);
                    for (int i = 0; i < 5; i++)
                    {
                        Noise(b, i * 0.045f, 0.03f, 0.5f, 0.2f, 0.001f, 0.8f);
                        Tone(b, i * 0.045f, 0.04f, 900 + i * 170, 700 + i * 120, Wave.Square, 0.08f, 0.001f, 0.03f, 0.2f);
                    }
                    break;
                case Music.Marsh: return MakeMusic(name, 3);
                case Music.Title: return MakeMusic(name, 0);
                case Music.Battle: return MakeMusic(name, 1);
                case Music.Boss: return MakeMusic(name, 2);
                default: return null;
            }
            return ToClip(name, b);
        }

        // ------------------------------------------------------------------ Music

        static float Midi(int note) => 440f * Mathf.Pow(2f, (note - 69) / 12f);

        /// <summary>
        /// Procedural loops. style 0 = title (slow, heroic), 1 = battle (driving), 2 = boss (fast, tense).
        /// Each is 16 bars over a 4-chord progression, with bass, arpeggio, lead and drums.
        /// </summary>
        static AudioClip MakeMusic(string name, int style)
        {
            float bpm = style == 0 ? 84f : style == 1 ? 126f : style == 2 ? 148f : 100f;
            bool calm = style == 0 || style == 3;
            float beat = 60f / bpm;
            int bars = 16;
            float bar = beat * 4f;
            var buf = Buf(bar * bars);
            // D minor family. Chords as root midi + minor/major flag.
            int[][] prog = style == 2
                ? new[] { new[] { 50, 0 }, new[] { 51, 1 }, new[] { 50, 0 }, new[] { 49, 2 } }   // Dm, Eb, Dm, C#dim
                : style == 3
                ? new[] { new[] { 45, 0 }, new[] { 41, 1 }, new[] { 50, 0 }, new[] { 52, 1 } }   // Am, F, Dm, E (marsh)
                : new[] { new[] { 50, 0 }, new[] { 46, 1 }, new[] { 53, 1 }, new[] { 48, 1 } };  // Dm, Bb, F, C
            var rng = new System.Random(style * 31 + 7);

            for (int b = 0; b < bars; b++)
            {
                var ch = prog[b % 4];
                int root = ch[0];
                int third = ch[1] == 0 ? 3 : ch[1] == 2 ? 3 : 4;
                int fifth = ch[1] == 2 ? 6 : 7;
                int[] tones = { root, root + third, root + fifth, root + 12 };
                float t0 = b * bar;

                // Bass
                if (calm)
                {
                    Tone(buf, t0, bar * 0.95f, Midi(root - 12), Midi(root - 12), Wave.Tri, 0.32f, 0.05f, 0.4f);
                }
                else
                {
                    int steps = style == 1 ? 8 : 16;
                    for (int s = 0; s < steps; s++)
                    {
                        int n = root - 12 + ((s % 4 == 3) ? 12 : 0);
                        float len = bar / steps;
                        Tone(buf, t0 + s * len, len * 0.9f, Midi(n), Midi(n), Wave.Saw, style == 2 ? 0.16f : 0.2f, 0.004f, len * 0.5f);
                    }
                }

                // Pad / arpeggio
                int arpSteps = calm ? 8 : 16;
                for (int s = 0; s < arpSteps; s++)
                {
                    float len = bar / arpSteps;
                    int n = tones[s % 4] + 12;
                    Tone(buf, t0 + s * len, len * 0.85f, Midi(n), Midi(n), calm ? Wave.Tri : Wave.Square, calm ? 0.1f : 0.045f, 0.003f, len * 0.6f, 0.25f);
                }
                if (calm)
                    for (int k = 0; k < 3; k++) Tone(buf, t0, bar, Midi(tones[k]), Midi(tones[k]), Wave.Sine, 0.06f, 0.4f, 0.5f);

                // Lead melody on the second half of the loop (call and answer)
                if (b >= 4)
                {
                    int notes = calm ? 4 : 8;
                    float len = bar / notes;
                    for (int s = 0; s < notes; s++)
                    {
                        if (rng.NextDouble() < (calm ? 0.15 : 0.25)) continue;
                        int pick = tones[rng.Next(0, 4)] + 12 + (rng.NextDouble() < 0.3 ? 2 : 0);
                        if (s == 0) pick = tones[0] + 12;
                        float dur = len * (rng.NextDouble() < 0.3 ? 1.9f : 0.9f);
                        Tone(buf, t0 + s * len, dur, Midi(pick), Midi(pick), calm ? Wave.Sine : Wave.Square, calm ? 0.13f : 0.07f, 0.01f, dur * 0.5f, 0.4f, style == 3 ? 0.015f : 0.006f);
                    }
                }

                // Drums
                if (style == 3)
                {
                    // Slow funeral toms and a brushed rattle
                    Tone(buf, t0, 0.5f, 110, 55, Wave.Sine, 0.35f, 0.005f);
                    Tone(buf, t0 + beat * 2f, 0.5f, 98, 50, Wave.Sine, 0.3f, 0.005f);
                    Noise(buf, t0 + beat * 3f, 0.25f, 0.1f, 0.3f, 0.02f, 0.6f);
                }
                else if (style != 0)
                {
                    for (int q = 0; q < 4; q++)
                    {
                        float tq = t0 + q * beat;
                        if (q == 0 || q == 2 || style == 2) Tone(buf, tq, 0.18f, 150, 45, Wave.Sine, 0.55f, 0.001f);
                        if (q == 1 || q == 3) Noise(buf, tq, 0.16f, 0.35f, 0.55f);
                        Noise(buf, tq + beat * 0.5f, 0.04f, 0.12f, 0.05f, 0.001f, 0.95f);
                        Noise(buf, tq, 0.03f, 0.08f, 0.05f, 0.001f, 0.95f);
                    }
                }
                else if (b % 2 == 0)
                {
                    Tone(buf, t0, 0.6f, 90, 40, Wave.Sine, 0.3f, 0.01f); // soft timpani
                }
            }

            // Soft limiter and gentle fade at the loop seam
            for (int i = 0; i < buf.Length; i++) buf[i] = (float)System.Math.Tanh(buf[i] * 0.9f);
            int seam = Rate / 50;
            for (int i = 0; i < seam; i++)
            {
                float k = (float)i / seam;
                buf[i] *= k;
                buf[buf.Length - 1 - i] *= k;
            }
            var clip = AudioClip.Create(name, buf.Length, 1, Rate, false);
            clip.SetData(buf, 0);
            return clip;
        }
    }
}
