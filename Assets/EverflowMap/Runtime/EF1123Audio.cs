using UnityEngine;

namespace EverflowMap
{
    public enum EF1123Sound
    {
        Select, Launch, Bloom, Freeze, Gust, Mechanism, Star, Step, Jump, Land, Fall, Recovery,
        MenuChoice, Confirm, Door, ChimeLow, ChimeHigh, ChimeThird
    }

    [DisallowMultipleComponent]
    public sealed class EF1123Audio : MonoBehaviour
    {
        const int SampleRate = 22050;
        const string MusicKey = "Everflow.Lkh.Music";
        const string EffectsKey = "Everflow.Lkh.Effects";
        const float Tau = 2 * Mathf.PI;

        public enum Theme { Title, Valley, Ending }
        public Theme startingTheme;
        public AudioClip titleMusic, valleyMusic, endingMusic;
        public AudioClip windAmbience, waterAmbience;

        readonly AudioClip[] music = new AudioClip[3];
        readonly AudioClip[] sounds = new AudioClip[(int)EF1123Sound.ChimeThird + 1];
        readonly System.Collections.Generic.List<AudioClip> generated = new();
        AudioSource musicSource, effectsSource, windSource, waterSource;
        float waterLevel;

        public float MusicVolume { get; private set; }
        public float EffectsVolume { get; private set; }

        void Awake()
        {
            MusicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicKey, .65f));
            EffectsVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(EffectsKey, .75f));
            musicSource = CreateSource("Music", true);
            effectsSource = CreateSource("Effects", false);
            if (startingTheme == Theme.Valley)
            {
                windSource = CreateSource("Valley wind", true);
                waterSource = CreateSource("Nearby water", true);
                windSource.clip = windAmbience != null ? windAmbience : NoiseBed(false);
                waterSource.clip = waterAmbience != null ? waterAmbience : NoiseBed(true);
                windSource.Play();
                waterSource.Play();
            }
            SetTheme(startingTheme);
            ApplyVolumes();
        }

        AudioSource CreateSource(string objectName, bool loop)
        {
            var node = new GameObject(objectName);
            node.transform.SetParent(transform, false);
            var source = node.AddComponent<AudioSource>();
            source.loop = loop;
            source.playOnAwake = false;
            source.spatialBlend = 0;
            source.ignoreListenerPause = true;
            return source;
        }

        public void SetMusicVolume(float value)
        {
            MusicVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(MusicKey, MusicVolume);
            ApplyVolumes();
        }

        public void SetEffectsVolume(float value)
        {
            EffectsVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(EffectsKey, EffectsVolume);
            ApplyVolumes();
        }

        public void SetTheme(Theme theme)
        {
            int index = (int)theme;
            if (music[index] == null)
            {
                AudioClip authored = theme == Theme.Title ? titleMusic : theme == Theme.Valley ? valleyMusic : endingMusic;
                music[index] = authored != null ? authored : ComposeTheme(theme);
            }
            if (musicSource.clip == music[index]) return;
            musicSource.clip = music[index];
            musicSource.Play();
        }

        public void Ambience(float water, bool silent)
        {
            if (windSource == null) return;
            waterLevel = Mathf.MoveTowards(waterLevel, Mathf.Clamp01(water), Time.unscaledDeltaTime * 1.6f);
            windSource.volume = silent ? 0 : EffectsVolume * .11f;
            waterSource.volume = silent ? 0 : EffectsVolume * waterLevel * .23f;
        }

        void ApplyVolumes()
        {
            musicSource.volume = MusicVolume * .59f;
            effectsSource.volume = EffectsVolume;
            if (windSource != null)
            {
                windSource.volume = EffectsVolume * .11f;
                waterSource.volume = EffectsVolume * waterLevel * .23f;
            }
        }

        public void Play(EF1123Sound sound, float gain = 1)
        {
            int index = (int)sound;
            if (sounds[index] == null) sounds[index] = MakeEffect(sound);
            effectsSource.PlayOneShot(sounds[index], gain);
        }

        AudioClip MakeEffect(EF1123Sound sound)
        {
            var settings = sound switch
            {
                EF1123Sound.Select => (.12f, 660f, 160f, .02f),
                EF1123Sound.Launch => (.21f, 420f, 210f, .09f),
                EF1123Sound.Bloom => (.44f, 450f, 240f, .035f),
                EF1123Sound.Freeze => (.49f, 1160f, -270f, .10f),
                EF1123Sound.Gust => (.41f, 190f, 100f, .24f),
                EF1123Sound.Mechanism => (.63f, 93f, -30f, .18f),
                EF1123Sound.Star => (.92f, 523.25f, 0f, .015f),
                EF1123Sound.Step => (.12f, 116f, -37f, .28f),
                EF1123Sound.Jump => (.21f, 185f, 115f, .07f),
                EF1123Sound.Land => (.17f, 92f, -40f, .28f),
                EF1123Sound.Fall => (.32f, 340f, -218f, .16f),
                EF1123Sound.Recovery => (.42f, 392f, 116f, .02f),
                EF1123Sound.MenuChoice => (.11f, 500f, 66f, .02f),
                EF1123Sound.Confirm => (.24f, 580f, 124f, .015f),
                EF1123Sound.ChimeLow => (1.33f, 515f, 0f, 0f),
                EF1123Sound.ChimeHigh => (1.23f, 687f, 0f, 0f),
                EF1123Sound.ChimeThird => (1.24f, 822f, 0f, 0f),
                _ => (1.16f, 261.63f, 0f, .045f)
            };
            var (duration, pitch, sweep, noiseGain) = settings;
            var samples = new float[(int)(duration * SampleRate)];
            var random = new System.Random(1123 + (int)sound);
            float phase = 0, noise = 0;
            bool chime = sound == EF1123Sound.ChimeLow || sound == EF1123Sound.ChimeHigh || sound == EF1123Sound.ChimeThird;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)SampleRate, x = t / duration;
                noise = Mathf.Lerp(noise, 2 * (float)random.NextDouble() - 1, .24f);
                float note = pitch + sweep * x;
                if (sound == EF1123Sound.Star || sound == EF1123Sound.Door)
                    note *= x < .23f ? 1f : x < .47f ? 1.25f : x < .7f ? 1.5f : 2f;
                phase += Tau * note / SampleRate;
                float attack = Mathf.Clamp01(t / .012f);
                float envelope = attack * Mathf.Pow(1 - x, 1.7f);
                samples[i] = chime
                    ? attack * (.27f * Mathf.Sin(phase) * Mathf.Exp(-3.8f * t) +
                        .14f * Mathf.Sin(phase * 2.76f) * Mathf.Exp(-8f * t) +
                        .08f * Mathf.Sin(phase * 5.34f) * Mathf.Exp(-15f * t))
                    : envelope * (.17f * Mathf.Sin(phase) + .04f * Mathf.Sin(phase * 2) + noise * noiseGain);
            }
            return Clip(sound.ToString(), samples);
        }

        AudioClip ComposeTheme(Theme theme)
        {
            float beat = theme == Theme.Valley ? .60f : .70f;
            int[] progression = theme == Theme.Ending ? new[] { 57, 53, 60, 55 } :
                theme == Theme.Title ? new[] { 60, 55, 57, 53 } : new[] { 57, 60, 55, 53 };
            int[] pattern = theme == Theme.Ending ? new[] { 7, 12, 16, 19, 16, 12, 7, 4 } :
                new[] { 12, 7, 16, 7, 12, 19, 16, 7 };
            var samples = new float[Mathf.RoundToInt(SampleRate * beat * 16)];
            for (int bar = 0; bar < progression.Length; bar++)
            {
                int root = progression[bar];
                AddNote(samples, root - 12, beat * bar * 4, beat * 3.9f, .055f, false);
                AddNote(samples, root + 7, beat * bar * 4, beat * 3.8f, .028f, false);
                for (int i = 0; i < pattern.Length; i++)
                {
                    int interval = root == 57 && pattern[i] == 16 ? 15 : pattern[i];
                    AddNote(samples, root + interval, beat * (bar * 4 + i * .5f), beat * .9f, .043f, true);
                }
            }
            return Clip("EVERFLOW " + theme, samples);
        }

        static void AddNote(float[] samples, int midi, float start, float duration, float strength, bool pluck)
        {
            float pitch = 440 * Mathf.Pow(2, (midi - 69) / 12f);
            int offset = Mathf.RoundToInt(start * SampleRate);
            for (int i = 0, length = Mathf.RoundToInt(duration * SampleRate); i < length; i++)
            {
                float t = i / (float)SampleRate;
                float env = Mathf.Clamp01(t / (pluck ? .018f : .23f)) *
                            Mathf.Clamp01((duration - t) / (pluck ? .10f : .25f));
                if (pluck) env *= Mathf.Exp(-6.6f * t);
                float wave = Mathf.Sin(t * pitch * Tau) + .23f * Mathf.Sin(t * pitch * Tau * 2);
                samples[(offset + i) % samples.Length] += wave * env * strength;
            }
        }

        AudioClip NoiseBed(bool water)
        {
            var samples = new float[SampleRate * 7];
            var random = new System.Random(water ? 120 : 124);
            float low = 0, medium = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                float raw = 2 * (float)random.NextDouble() - 1;
                low = Mathf.Lerp(low, raw, .012f);
                medium = Mathf.Lerp(medium, raw, .19f);
                float x = i / (float)samples.Length;
                float seam = Mathf.Clamp01(Mathf.Min(x, 1 - x) * 80);
                float wash = .76f + .24f * Mathf.Sin(Tau * x * 3);
                samples[i] = seam * wash * (water ? medium * .18f + low * .30f : low * .49f + medium * .047f);
            }
            return Clip(water ? "Stream ambience" : "Valley wind", samples);
        }

        AudioClip Clip(string label, float[] samples)
        {
            var clip = AudioClip.Create(label, samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            generated.Add(clip);
            return clip;
        }

        void OnDestroy()
        {
            foreach (var clip in generated) Destroy(clip);
        }
    }
}
