using System.IO;
using BepInEx.Logging;
using UnityEngine;
using Voxels.TowerDefense;

namespace BadNorth.Musketeers
{
    internal static class MusketeerShotAudio
    {
        private const int VoiceCount = 12;
        private const float MainShotGain = 0.99f;
        private const float VolleyGainFalloff = 3.5f;
        private static GameObject emitter;
        private static AudioSource[] voices;
        private static int soundFrame = -1;
        private static int soundsThisFrame;
        private static bool reportedPlayback;

        internal static ManualLogSource Logger { private get; set; }

        internal static void Play(Vector3 position, Camera camera)
        {
            int sfxLevel = Profile.userSettings ? Profile.userSettings.sfxVolume : 10;
            if (sfxLevel <= 0)
            {
                Report("Musketeer gunshot suppressed because the game's SFX volume is zero.");
                return;
            }

            if (soundFrame != Time.frameCount)
            {
                soundFrame = Time.frameCount;
                soundsThisFrame = 0;
            }
            if (soundsThisFrame >= 3) return;
            if (!emitter) CreateVoices();

            foreach (var voice in voices)
            {
                if (voice.isPlaying) continue;

                // Screen position gives stereo separation without attenuating visible troops at long camera depths.
                Vector3 viewport = camera && camera.isActiveAndEnabled
                    ? camera.WorldToViewportPoint(position)
                    : new Vector3(0.5f, 0.5f, 1f);
                float horizontal = viewport.x * 2f - 1f;
                float vertical = viewport.y * 2f - 1f;
                float beyondScreen = Mathf.Max(Mathf.Abs(horizontal), Mathf.Abs(vertical)) - 1f;
                float screenGain = Mathf.Lerp(1f, 0.45f, Mathf.Clamp01(beyondScreen));
                voice.panStereo = Mathf.Clamp(horizontal, -1f, 1f) * 0.45f;
                // Keep the lead shot clear and give overlapping shots room in the mix.
                voice.volume = MainShotGain * Mathf.Clamp01(sfxLevel / 10f) * screenGain / (1f + soundsThisFrame * VolleyGainFalloff);
                voice.Play();
                soundsThisFrame++;
                Report("Musketeer gunshot submitted. SFX volume " + sfxLevel
                    + ", listener paused " + AudioListener.pause + ", listener gain " + AudioListener.volume + ".");
                return;
            }
        }

        private static void CreateVoices()
        {
            // A shot can finish playing after its shooter, projectile and muzzle flash have all been recycled.
            emitter = new GameObject("Musketeer gunshot audio");
            Object.DontDestroyOnLoad(emitter);
            var shotClip = LoadShotClip();
            voices = new AudioSource[VoiceCount];
            for (int i = 0; i < voices.Length; i++)
            {
                var voice = emitter.AddComponent<AudioSource>();
                voice.playOnAwake = false;
                voice.priority = 32;
                voice.clip = shotClip;
                voices[i] = voice;
            }
        }

        private static void Report(string message)
        {
            if (reportedPlayback) return;

            reportedPlayback = true;
            Logger?.LogInfo(message);
        }

        private static AudioClip LoadShotClip()
        {
            using (var wave = typeof(MusketeerShotAudio).Assembly.GetManifestResourceStream("BadNorth.Musketeers.MusketCharleville.wav"))
            using (var reader = new BinaryReader(wave))
            {
                const int headerSize = 44;
                var samples = new float[(wave.Length - headerSize) / 2];
                wave.Position = headerSize;
                for (int i = 0; i < samples.Length; i++) samples[i] = reader.ReadInt16() / 32768f;
                var clip = AudioClip.Create("Musketeer shot - Charleville", samples.Length, 1, 24000, false);
                clip.SetData(samples, 0);
                return clip;
            }
        }
    }
}
