using System;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;
using GoogleMobileAds.Ump.Api;
using UnityEngine;

namespace ColorTransit
{
    public sealed class AdService : MonoBehaviour
    {
        [Serializable] public sealed class Configuration
        {
            public bool liveEnabled, testEnabled;
            public string androidAppId = "", rewardedUnitId = "", interstitialUnitId = "";
        }
        public const string TestAppId = "ca-app-pub-3940256099942544~3347511713";
        const string ChoiceKey = "transit.adParticipation"; // 0 undecided, 1 adult opted in, 2 ads off
        public static Configuration ReadConfiguration()
        {
            var file = Resources.Load<TextAsset>("Monetization");
            return file == null ? new Configuration() : JsonUtility.FromJson<Configuration>(file.text);
        }
        Configuration config;
        RewardedAd rewarded;
        InterstitialAd interstitial;
        bool initialized, initializing, rewardLoading, interstitialLoading, dead, test, allowed, gathering;
        int generation;
        float retryAt, loadedRewardAt, loadedInterstitialAt, lastFullScreen;
        string state = "Ads unavailable";
        public bool Showing { get; private set; }
        public event Action<bool> FullScreenChanged;
        public event Action Changed;
        public string State { get => state; private set { state = value; Changed?.Invoke(); } }
        public bool OffersAds => config != null && ((Debug.isDebugBuild && config.testEnabled) || config.liveEnabled);
        public bool NeedsChoice => OffersAds && !test && PlayerPrefs.GetInt(ChoiceKey, 0) == 0;
        public bool Participating => allowed;
        public bool Busy => gathering || Showing;
        bool CanRequest => allowed && !gathering && (test || ConsentInformation.CanRequestAds());
        public bool Ready => CanRequest && initialized && !Showing && rewarded != null && rewarded.CanShowAd();
        public bool PrivacyRequired => !test && ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required;
        void Awake() { config = ReadConfiguration(); test = Debug.isDebugBuild && config.testEnabled; }
        void Start()
        {
            lastFullScreen = Time.realtimeSinceStartup;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!OffersAds) { State = "Ads are disabled in this version"; return; }
            if (test) { allowed = true; Initialize(); return; }
            if (PlayerPrefs.GetInt(ChoiceKey, 0) == 1) { allowed = true; GatherConsent(); }
            else State = NeedsChoice ? "Choose how you want to play" : "Ads are off. All routes remain playable.";
#endif
        }
        public void ConfirmAdult(bool adult)
        {
            if (!adult) { DisableForSession(); return; }
            if (!OffersAds || gathering || Showing) return;
            PlayerPrefs.SetInt(ChoiceKey, 1); PlayerPrefs.Save(); allowed = true; GatherConsent();
        }
        public void DisableForSession()
        {
            if (Showing) return;
            PlayerPrefs.SetInt(ChoiceKey, 2); PlayerPrefs.Save(); allowed = false; gathering = false;
            generation++; rewardLoading = interstitialLoading = false; DestroyLoaded();
            State = "Ads are off. All routes remain playable.";
        }
        void Main(Action action) { MobileAdsEventExecutor.ExecuteInUpdate(() => { if (!dead) action(); }); }
        void GatherConsent()
        {
            if (!allowed || gathering) return;
            gathering = true; generation++; DestroyLoaded(); rewardLoading = interstitialLoading = false;
            int requestGeneration = generation;
            State = "Checking privacy choices...";
            var parameters = new ConsentRequestParameters { TagForUnderAgeOfConsent = false };
#if DEVELOPMENT_BUILD && UNITY_ANDROID && !UNITY_EDITOR
            // Only the isolated QA build accepts debug geography; signed production builds exclude this code.
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var intent = activity.Call<AndroidJavaObject>("getIntent"))
            {
                if (intent.Call<bool>("getBooleanExtra", "transit_reset_consent", false)) ConsentInformation.Reset();
                if (intent.Call<bool>("getBooleanExtra", "transit_eea", false))
                {
                    var hash = intent.Call<string>("getStringExtra", "transit_consent_device");
                    parameters.ConsentDebugSettings = new ConsentDebugSettings {
                        DebugGeography = DebugGeography.EEA,
                        TestDeviceHashedIds = new System.Collections.Generic.List<string> { hash ?? "" }
                    };
                }
            }
#endif
            ConsentInformation.Update(parameters, error => Main(() =>
            {
                if (requestGeneration != generation || !allowed) return;
                if (error != null) { gathering = false; State = "Privacy check unavailable. Keep playing or retry."; Debug.LogWarning("Transit consent update: " + error.Message); return; }
                SetShowing(true);
                ConsentForm.LoadAndShowConsentFormIfRequired(formError => Main(() =>
                {
                    gathering = false; SetShowing(false);
                    if (requestGeneration != generation || !allowed) return;
                    Debug.Log("Transit consent: " + ConsentInformation.ConsentStatus + "; canRequest=" + ConsentInformation.CanRequestAds() + "; privacy=" + PrivacyRequired);
                    if (formError != null) { State = "Privacy form unavailable. Keep playing or retry."; Debug.LogWarning("Transit consent form: " + formError.Message); return; }
                    if (CanRequest) Initialize(); else State = "Ads unavailable. All routes remain playable.";
                }));
            }));
        }
        public void Privacy()
        {
            if (Busy || !PrivacyRequired) return;
            gathering = true; generation++; rewardLoading = interstitialLoading = false;
            SetShowing(true); DestroyLoaded();
            ConsentForm.ShowPrivacyOptionsForm(error => Main(() =>
            {
                gathering = false; SetShowing(false);
                if (error != null) { State = "Privacy form unavailable. Please retry."; return; }
                if (CanRequest) Initialize(); else State = "Ads are off or unavailable. Keep playing.";
            }));
        }
        void Initialize()
        {
            if (!CanRequest || initializing) return;
            if (initialized) { Load(); return; }
            initializing = true; State = "Loading ads...";
            MobileAds.SetRequestConfiguration(new RequestConfiguration { MaxAdContentRating = MaxAdContentRating.T });
            MobileAds.Initialize(result => Main(() =>
            {
                initializing = false; initialized = result != null;
                if (initialized && CanRequest) Load(); else State = "Ads unavailable. Keep playing.";
            }));
        }
        AdRequest Request()
        {
            var request = new AdRequest();
            if (test) request.Extras.Add("npa", "1");
            return request;
        }
        void Update()
        {
            if (!CanRequest || !initialized || Showing || Time.realtimeSinceStartup < retryAt) return;
            if (rewarded != null && Time.realtimeSinceStartup - loadedRewardAt > 3300) { rewarded.Destroy(); rewarded = null; }
            if (interstitial != null && Time.realtimeSinceStartup - loadedInterstitialAt > 3300) { interstitial.Destroy(); interstitial = null; }
            Load();
        }
        void Load()
        {
            if (!CanRequest || !initialized || Showing) return;
            retryAt = Time.realtimeSinceStartup + 30;
            int requestGeneration = generation;
            if (rewarded == null && !rewardLoading)
            {
                rewardLoading = true;
                RewardedAd.Load(test ? "ca-app-pub-3940256099942544/5224354917" : config.rewardedUnitId, Request(), (ad, error) =>
                {
                    if (dead) { ad?.Destroy(); return; }
                    Main(() => {
                        if (requestGeneration != generation || !CanRequest) { ad?.Destroy(); return; }
                        rewardLoading = false;
                        if (error != null) { ad?.Destroy(); State = "No ad available. Keep playing; we'll retry."; Debug.LogWarning("Transit rewarded load: " + error); return; }
                        rewarded = ad; loadedRewardAt = Time.realtimeSinceStartup; State = ad != null ? "Rewarded ad ready: +25 coins" : "No ad available. Keep playing.";
                        Debug.Log("Transit ads: rewarded ready=" + (ad != null));
                    });
                });
            }
            if (interstitial == null && !interstitialLoading)
            {
                interstitialLoading = true;
                InterstitialAd.Load(test ? "ca-app-pub-3940256099942544/1033173712" : config.interstitialUnitId, Request(), (ad, error) =>
                {
                    if (dead) { ad?.Destroy(); return; }
                    Main(() => {
                        if (requestGeneration != generation || !CanRequest) { ad?.Destroy(); return; }
                        interstitialLoading = false;
                        if (error != null) { ad?.Destroy(); Debug.LogWarning("Transit interstitial load: " + error); return; }
                        interstitial = ad; loadedInterstitialAt = Time.realtimeSinceStartup;
                        Debug.Log("Transit ads: interstitial ready=" + (ad != null));
                    });
                });
            }
        }
        public bool Reward(Action earned, Action finished)
        {
            if (!Ready) return false;
            var ad = rewarded; rewarded = null; bool granted = false, closed = false;
            SetShowing(true);
            Action finish = () => { if (closed) return; closed = true; ad.Destroy(); SetShowing(false); finished(); Load(); };
            ad.OnAdFullScreenContentClosed += () => Main(finish);
            ad.OnAdFullScreenContentFailed += error => Main(finish);
            try { ad.Show(reward => Main(() => { if (granted) return; granted = true; earned(); Debug.Log("Transit ads: reward callback accepted"); })); }
            catch (Exception error) { Debug.LogWarning("Transit ad show failed: " + error.Message); finish(); }
            return true;
        }
        public void Next(int completedLevel, Action continueGame)
        {
            if (Showing) return;
            if (!CanRequest || !initialized || completedLevel <= 5 || Time.realtimeSinceStartup - lastFullScreen < 120 || interstitial == null || !interstitial.CanShowAd())
            { continueGame(); return; }
            var ad = interstitial; interstitial = null; bool closed = false; SetShowing(true);
            Action finish = () => { if (closed) return; closed = true; ad.Destroy(); SetShowing(false); continueGame(); Load(); };
            ad.OnAdFullScreenContentClosed += () => Main(finish);
            ad.OnAdFullScreenContentFailed += error => Main(finish);
            try { Debug.Log("Transit ads: interstitial show"); ad.Show(); } catch (Exception error) { Debug.LogWarning("Transit ad show failed: " + error.Message); finish(); }
        }
        void SetShowing(bool value) { Showing = value; if (!value) lastFullScreen = Time.realtimeSinceStartup; FullScreenChanged?.Invoke(value); Changed?.Invoke(); }
        void DestroyLoaded() { rewarded?.Destroy(); rewarded = null; interstitial?.Destroy(); interstitial = null; }
        void OnDestroy() { dead = true; generation++; DestroyLoaded(); }
    }
}
