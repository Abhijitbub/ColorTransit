using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace ColorTransit
{
    [Serializable] public sealed class TransitSave
    {
        public int version = 1, unlocked = 1, awardedThrough, coins, theme;
        public bool music = true, effects = true, haptics;
        public BoardSave board;
        public BoardSave[] undo = new BoardSave[0];
        public static string PathName => Path.Combine(Application.persistentDataPath, "transit-save.json");
        public static TransitSave Read()
        {
            foreach (string file in new[] { PathName, PathName + ".bak" })
            {
                try
                {
                    if (!File.Exists(file)) continue;
                    var save = JsonUtility.FromJson<TransitSave>(File.ReadAllText(file));
                    if (save == null || save.version != 1 || save.unlocked < 1 || save.unlocked > 10000 || save.awardedThrough < 0 || save.awardedThrough > save.unlocked || save.coins < 0 || save.coins > 10000000 || save.theme < 0 || save.theme > 3 || save.undo == null || save.undo.Length > 18) continue;
                    if (save.board != null) save.board.Restore();
                    foreach (var snapshot in save.undo) snapshot.Restore();
                    return save;
                }
                catch (Exception e) { Debug.LogWarning("Save recovery: " + e.GetType().Name); }
            }
            return new TransitSave();
        }
        public void Write()
        {
            try
            {
                Directory.CreateDirectory(Application.persistentDataPath);
                var tmp = PathName + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(this));
                if (File.Exists(PathName)) File.Replace(tmp, PathName, PathName + ".bak");
                else File.Move(tmp, PathName);
            }
            catch (Exception e) { Debug.LogWarning("Save unavailable: " + e.GetType().Name); }
        }
    }
}

