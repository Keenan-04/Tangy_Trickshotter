using UnityEngine;

namespace Trickshot
{
    public enum SfxId
    {
        Shot,
        Bolt,
        Hit,
        Kill,
        Headshot,
        ScopeIn,
        Click
    }

    /// <summary>
    /// Placeholder sound effects, synthesised in code so the project needs no audio files.
    /// Every caller can pass its own AudioClip instead, so real recordings drop straight in.
    /// </summary>
    public static class Sfx
    {
        const int Rate = 44100;
        const float Tau = Mathf.PI * 2f;

        static AudioSource source;
        static AudioSource delayedSource;
        static AudioClip[] clips;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            source = null;
            delayedSource = null;
            clips = null;
        }

        /// <summary>Plays a sound now. Pass a clip of your own to use it instead of the built-in one.</summary>
        public static void Play(SfxId id, float volume = 1f, AudioClip custom = null)
        {
            Ensure();
            source.PlayOneShot(custom != null ? custom : Clip(id), volume);
        }

        /// <summary>Plays a sound after a delay. Only one delayed sound can be waiting at a time.</summary>
        public static void PlayDelayed(SfxId id, float delay, float volume = 1f, AudioClip custom = null)
        {
            Ensure();
            delayedSource.Stop();
            delayedSource.clip = custom != null ? custom : Clip(id);
            delayedSource.volume = volume;
            delayedSource.PlayDelayed(delay);
        }

        public static AudioClip Clip(SfxId id)
        {
            Ensure();
            int i = (int)id;
            if (clips[i] == null) clips[i] = Build(id);
            return clips[i];
        }

        static void Ensure()
        {
            if (source != null) return;

            var go = new GameObject("Sfx");
            Object.DontDestroyOnLoad(go);
            source = NewSource(go);
            delayedSource = NewSource(go);
            clips = new AudioClip[System.Enum.GetValues(typeof(SfxId)).Length];
        }

        static AudioSource NewSource(GameObject go)
        {
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 0f; // plain 2D sound
            return s;
        }

        static AudioClip Build(SfxId id)
        {
            switch (id)
            {
                case SfxId.Shot: return Make("Shot", Shot());
                case SfxId.Bolt: return Make("Bolt", Bolt());
                case SfxId.Hit: return Make("Hit", Hit());
                case SfxId.Kill: return Make("Kill", Tone(0.18f, 660f, 990f, 26f, true));
                case SfxId.Headshot: return Make("Headshot", Tone(0.42f, 1320f, 2640f, 9f, true));
                case SfxId.ScopeIn: return Make("ScopeIn", ScopeIn());
                default: return Make("Click", Tone(0.05f, 880f, 1760f, 60f, false));
            }
        }

        static AudioClip Make(string name, float[] data)
        {
            AudioClip clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // A sharp crack, a low thump that drops in pitch, and a rumbling tail.
        static float[] Shot()
        {
            var d = new float[(int)(Rate * 0.9f)];
            var rng = new System.Random(7);
            float body = 0f, rumble = 0f, phase = 0f;

            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                body += (noise - body) * 0.35f;
                rumble += (noise - rumble) * 0.04f;

                phase += Tau * (55f + 90f * Mathf.Exp(-t * 30f)) / Rate;

                float crack = body * Mathf.Exp(-t * 22f) * 1.2f;
                float thump = Mathf.Sin(phase) * Mathf.Exp(-t * 10f) * 0.9f;
                float tail = rumble * Mathf.Exp(-t * 4.5f) * 1.6f;
                d[i] = (float)System.Math.Tanh((crack + thump + tail) * 1.6f) * 0.9f;
            }
            return d;
        }

        // Three metallic clicks with a quiet slide between them: bolt up, back, forward.
        static float[] Bolt()
        {
            var d = new float[(int)(Rate * 0.45f)];
            var rng = new System.Random(11);
            float[] clicks = { 0f, 0.16f, 0.3f };
            float slide = 0f;

            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                float v = 0f;

                foreach (float start in clicks)
                {
                    float local = t - start;
                    if (local < 0f) continue;
                    v += noise * Mathf.Exp(-local * 90f) * 0.55f;
                    v += Mathf.Sin(Tau * 2600f * local) * Mathf.Exp(-local * 60f) * 0.22f;
                }

                slide += (noise - slide) * 0.12f;
                if (t > 0.03f && t < 0.3f) v += slide * 0.07f;

                d[i] = Mathf.Clamp(v, -1f, 1f);
            }
            return d;
        }

        // The hitmarker tick: a very short, bright snap.
        static float[] Hit()
        {
            var d = new float[(int)(Rate * 0.06f)];
            var rng = new System.Random(3);
            float previous = 0f;

            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                float bright = noise - previous; // crude high-pass
                previous = noise;
                d[i] = Mathf.Clamp(bright * Mathf.Exp(-t * 80f) * 0.45f
                    + Mathf.Sin(Tau * 2100f * t) * Mathf.Exp(-t * 70f) * 0.5f, -1f, 1f);
            }
            return d;
        }

        static float[] ScopeIn()
        {
            var d = new float[(int)(Rate * 0.09f)];
            var rng = new System.Random(5);
            float soft = 0f;

            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                soft += (noise - soft) * 0.2f;
                d[i] = soft * Mathf.Exp(-t * 45f) * 0.5f;
            }
            return d;
        }

        // Two sine tones that ring out, optionally with the hitmarker tick on the front.
        static float[] Tone(float seconds, float low, float high, float decay, bool withTick)
        {
            var d = new float[(int)(Rate * seconds)];
            float[] tick = withTick ? Hit() : null;

            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float v = Mathf.Sin(Tau * low * t) * Mathf.Exp(-t * decay) * 0.45f
                    + Mathf.Sin(Tau * high * t) * Mathf.Exp(-t * decay * 1.3f) * 0.22f;
                if (tick != null && i < tick.Length) v += tick[i];
                d[i] = Mathf.Clamp(v, -1f, 1f);
            }
            return d;
        }
    }
}
