using UnityEngine;
namespace ColorTransit
{
    public sealed class TransitAudio : MonoBehaviour
    {
        AudioSource music, effects;
        bool musicEnabled = true, effectsEnabled = true;
        bool adShowing, focused = true;
        public void SetAdShowing(bool showing) { adShowing = showing; AudioListener.pause = adShowing || !focused; }
        public void Initialize()
        {
            music = gameObject.AddComponent<AudioSource>(); effects = gameObject.AddComponent<AudioSource>();
            music.clip = Resources.Load<AudioClip>("Audio/CityAfterglow"); music.loop = true; music.volume = .36f;
            music.Play(); effects.volume = .65f;
        }
        public void Configure(bool musicOn, bool effectsOn)
        {
            musicEnabled = musicOn; effectsEnabled = effectsOn;
            if (music != null) music.mute = !musicEnabled;
        }
        public void Play(string name)
        {
            if (!effectsEnabled) return;
            var clip = Resources.Load<AudioClip>("Audio/" + name); if (clip) effects.PlayOneShot(clip);
        }
        void OnApplicationPause(bool paused) { if (music) { if (paused) music.Pause(); else music.UnPause(); } }
        void OnApplicationFocus(bool hasFocus) { focused = hasFocus; AudioListener.pause = adShowing || !focused; }
    }
}
