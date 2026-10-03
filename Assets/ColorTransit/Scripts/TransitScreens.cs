using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace ColorTransit
{
    public sealed partial class TransitGame
    {
        TransitSave save;
        TransitAudio audioSystem;
        AdService ads;
        string screen = "home";
        bool pauseRequested;
        int awardedNow;
        int mapPage;
        bool adsDirty;
        readonly string[] districts = { "MIDNIGHT TERMINAL", "RIVERSIDE STATION", "SUNSET JUNCTION", "SKYLINE CENTRAL" };
        void Persist()
        {
            if (smokeMode || save == null || model == null) return;
            save.board = BoardSave.From(model);
            save.undo = history.Select(BoardSave.From).ToArray();
            save.Write();
        }
        void RestoreProgress()
        {
            save = smokeMode ? new TransitSave() : TransitSave.Read();
            audioSystem = gameObject.AddComponent<TransitAudio>(); audioSystem.Initialize();
            audioSystem.Configure(save.music, save.effects);
            ads = gameObject.AddComponent<AdService>();
            ads.FullScreenChanged += showing => { audioSystem.SetAdShowing(showing); adsDirty = true; };
            ads.Changed += () => adsDirty = true;
            if (ads.NeedsChoice) screen = "adprivacy";
            model = save.board == null ? TransitModel.Generate(save.unlocked) : save.board.Restore();
            history.Clear();
            for (int i = save.undo.Length - 1; i >= 0; i--) history.Push(save.undo[i].Restore());
            SetTheme();
            if (model.Won) FinishRoute();
        }
        void SetTheme()
        {
            Background = Hex(new[] { "101C2E", "0F292C", "302039", "16283C" }[save.theme]);
            if (Camera.main != null) Camera.main.backgroundColor = Background;
        }
        void Go(string target)
        {
            if (busy) { pauseRequested = true; return; }
            if (target == "routes") mapPage = (save.unlocked - 1) / 12;
            screen = target; Time.timeScale = 1; Draw();
            if (target == "game" && model.CanBoard) { busy = true; StartCoroutine(Board()); }
        }
        void NextRoute()
        {
            if (model.Level >= 10000) { Go("home"); return; }
            int next = model.Level + 1;
            ads.Next(model.Level, () => { screen = "game"; Load(next); });
        }
        void FinishRoute()
        {
            awardedNow = 0;
            if (model.Level > save.awardedThrough)
            {
                awardedNow = 25; save.coins = Mathf.Min(10000000, save.coins + awardedNow);
                save.awardedThrough = model.Level;
                save.unlocked = Mathf.Max(save.unlocked, Mathf.Min(10000, model.Level + 1));
            }
            audioSystem.Play("Win"); Persist();
        }
        void Header(string title, string subtitle)
        {
            Label(root, "JSCA GAMES / COLOR TRANSIT FLOW", 28, 30, 480, 20, 11, Muted, TextAnchor.MiddleLeft);
            Label(root, title, 28, 70, 484, 58, 37, Cream, TextAnchor.MiddleLeft, true);
            Label(root, subtitle, 28, 137, 484, 34, 14, Muted, TextAnchor.MiddleLeft);
        }
        bool DrawScreen()
        {
            if (screen == "game") return false;
            if (screen == "home")
            {
                Header("A city in motion.", "Every color. A way home.");
                var frame = Box(root, "Game artwork", 113, 205, 314, 314, Panel);
                var art = frame.GetComponent<Image>(); art.sprite = iconArt; art.type = Image.Type.Simple; art.color = Color.white;
                Label(root, "COLOR TRANSIT FLOW", 28, 549, 484, 35, 27, Cream, TextAnchor.MiddleCenter, true);
                Label(root, districts[save.theme], 28, 592, 484, 22, 12, Palette[2]);
                Label(root, save.coins + " coins  /  " + (save.unlocked - 1) + " routes complete", 28, 633, 484, 27, 15, Muted);
                Button(root, "CONTINUE ROUTE " + model.Level.ToString("00"), 40, 696, 460,  sixty, Palette[2], () => Go("game"), 17);
                Button(root, "ROUTES", 40, 780, 220, 55, Panel, () => Go("routes"));
                Button(root, "STATION", 280, 780, 220, 55, Panel, () => Go("station"));
                Button(root, "SETTINGS", 150, 863, 240, 49, Panel, () => Go("settings"), 13);
            }
            else if (screen == "settings" || screen == "pause")
            {
                Header(screen == "pause" ? "Take a breather." : "Your journey.", "Your progress is saved automatically.");
                Button(root, "MUSIC  /  " + (save.music ? "ON" : "OFF"), 40, 228, 460, 62, Panel, () => { save.music = !save.music; SettingsChanged(); });
                Button(root, "SOUND EFFECTS  /  " + (save.effects ? "ON" : "OFF"), 40, 312, 460, 62, Panel, () => { save.effects = !save.effects; SettingsChanged(); });
                Button(root, "VIBRATION  /  " + (save.haptics ? "ON" : "OFF"), 40, 396, 460, 62, Panel, () => { save.haptics = !save.haptics; SettingsChanged(); });
                Button(root, "HOW TO PLAY", 40, 497, 220, 54, Panel, () => Go("help"), 13);
                Button(root, "PRIVACY", 280, 497, 220, 54, Panel, () => Go("privacy"), 13);
                if (ads.OffersAds) Button(root, "AD PRIVACY & OPTIONAL REWARDS", 40, 681, 460, 32, Panel, () => Go("adprivacy"), 11);
                Label(root, "Music: City Afterglow\nOriginal composition and sound design for JSCA Games.\nArtwork created for Color Transit Flow.\nVersion " + Application.version, 40, 579, 460, 100, 13, Muted);
                Button(root, "RESUME GAME", 40, 722, 460,  sixty, Palette[2], () => Go("game"));
                Button(root, "MAIN MENU", 40, 806, 460, 55, Panel, () => Go("home"));
            }
            else if (screen == "help")
            {
                Header("A smooth dispatch.", "No timer. No lives. Take your time.");
                Label(root, "1   Look at the first passenger on the left.\n\n2   Tap a matching bus to send it to a bay.\n\n3   Passengers board in queue order.\n     A full bus leaves automatically.\n\n4   Keep a bay free for the next color.\n\n5   From route 7, clear the front buses\n     to unlock vehicles behind them.\n\nUndo and hints are always free.\nLetters identify each color too.", 40, 225, 460, 480, 19, Cream, TextAnchor.UpperLeft);
                Button(root, "LET'S GO", 40, 798, 460, sixty, Palette[2], () => Go("game"));
            }
            else if (screen == "privacy")
            {
                Header("Your privacy.", "JSCA Games / Your choices");
                Label(root, ads.OffersAds ? "Progress and settings stay on your device.\nNo login or purchases are required.\n\nAdults can enable Google AdMob ads.\nGoogle may process identifiers, IP address,\nad interactions and diagnostics for ads,\nmeasurement and fraud prevention.\n\nManage choices in Ad privacy, or keep\nads off. Under-18 players use ads-off play.\n\nSupport: jscagames6@gmail.com\nUninstalling removes local game progress." : "This offline edition has no active ads.\nProgress and settings stay on your device.\nNo login or purchases are required.\n\nUninstalling removes local game progress.\nSupport: jscagames6@gmail.com", 40, 223, 460, 425, 18, Cream, TextAnchor.UpperLeft);
                Button(root, "ONLINE POLICY", 40, 701, 460, 55, Panel, () => Application.OpenURL("https://abhijitbub.github.io/color-transit-flow-privacy.html"), 14);
                Button(root, "BACK", 40, 797, 460, sixty, Palette[2], () => Go("settings"));
            }
            else if (screen == "adprivacy")
            {
                Header("Your ad choices.", "Every route is playable with ads off.");
                Label(root, "Adults can enable optional video rewards\n(+25 coins) and occasional between-route ads.\n\nGoogle may use device/ad data to provide\nads. Privacy choices appear where required.\n\nIf you are under 18 or prefer no ads,\nchoose Keep ads off. You can change\nthis choice later in Settings.\n\n" + ads.State, 40, 208, 460, 325, 17, Cream, TextAnchor.UpperLeft);
                Button(root, "I AM 18+ / ENABLE ADS OR RETRY", 40, 553, 460, 55, Panel, () => ads.ConfirmAdult(true), 12, !ads.Busy);
                if (ads.PrivacyRequired) Button(root, "CHANGE PRIVACY CHOICES", 40, 628, 460, 50, Panel, ads.Privacy, 12, !ads.Busy);
                Button(root, "CONTINUE TO GAME", 40, 718, 460, 55, Palette[2], () => Go("game"), 14, !ads.Showing);
                Button(root, "KEEP ADS OFF / UNDER 18", 40, 797, 460, 55, Panel, () => { ads.DisableForSession(); Go("game"); }, 14, !ads.Showing);
                Button(root, "PRIVACY POLICY", 150, 877, 240, 35, Panel, () => Go("privacy"), 11, !ads.Showing);
            }
            else if (screen == "station")
            {
                Header("Build your station.", "Complete new routes to earn 25 coins.");
                Label(root, districts[save.theme], 28, 236, 484, 40, 23, Palette[2], TextAnchor.MiddleCenter, true);
                for (int i = 0; i < 4; i++)
                {
                    float x = 42 + i * 124; Box(root, "Tower", x, 392 - i * 27,  ninety, 130 + i * 27, i <= save.theme ? Panel : Hex("182333"));
                    for (int row = 0; row < 4; row++) for (int col = 0; col < 3; col++) Box(root, "Window", x + 12 + col * 24, 406 - i * 27 + row * 25, 12, 12, i <= save.theme ? Palette[3] : Muted);
                }
                Label(root, save.coins + " COINS", 28, 580, 484, 32, 23, Cream, TextAnchor.MiddleCenter, true);
                int cost = (save.theme + 1) * 100;
                Button(root, save.theme == 3 ? "STATION COMPLETE" : "UPGRADE / " + cost + " COINS", 40, 689, 460, sixty, Palette[2], () => { if (save.theme < 3 && save.coins >= cost) { save.coins -= cost; save.theme++; SetTheme(); Persist(); audioSystem.Play("Win"); Draw(); } }, 15, save.theme < 3 && save.coins >= cost);
                Button(root, "MAIN MENU", 40, 798, 460, 55, Panel, () => Go("home"));
            }
            else if (screen == "routes")
            {
                Header("Your route map.", "Replay unlocked routes. New routes earn coins.");
                int first = mapPage * 12 + 1;
                for (int i = 0; i < 12; i++)
                {
                    int level = first + i;
                    Button(root, level <= save.unlocked ? level.ToString("00") : "-", 40 + (i % 3) * 160, 233 + (i / 3) * 119, 140,  ninety, level == save.unlocked ? Palette[2] : Panel, () => { screen = "game"; Load(level); }, 23, level <= save.unlocked);
                }
                Button(root, "< PREVIOUS", 40, 731, 160, 45, Panel, () => { mapPage--; Draw(); }, 12, mapPage > 0);
                Label(root, "PAGE " + (mapPage + 1), 200, 731, 140, 45, 12, Muted);
                Button(root, "NEXT >", 340, 731, 160, 45, Panel, () => { mapPage++; Draw(); }, 12, (mapPage + 1) * 12 < save.unlocked);
                Button(root, "MAIN MENU", 40, 815, 460, sixty, Panel, () => Go("home"));
            }
            return true;
        }
        const int sixty = 60, ninety = 90;
        void SettingsChanged() { audioSystem.Configure(save.music, save.effects); Persist(); Draw(); }
        void OnApplicationPause(bool paused) { if (paused) { Persist(); if (screen == "game" && (ads == null || !ads.Showing)) { if (busy) pauseRequested = true; else Go("pause"); } } }
        void OnApplicationQuit() { Persist(); }
    }
}
