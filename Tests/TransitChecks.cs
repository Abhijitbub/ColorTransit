using System;
using System.Linq;
using ColorTransit;
static class Checks
{
    static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Main(string[] args)
    {
        if (args.Contains("--android-fixture"))
        {
            for (int level = 30; level < 100; level++)
            {
                var board = TransitModel.Generate(level);
                int hint = board.Hint(), index = board.Lot.FindIndex(v => v.Id == hint);
                if (index < 8) continue;
                var fixture = new { version = 1, unlocked = level, awardedThrough = level - 1, coins = 125, theme = 1, music = true, effects = true, haptics = false, board = BoardSave.From(board), undo = Array.Empty<BoardSave>() };
                System.IO.Directory.CreateDirectory("Logs/Signed-Android");
                System.IO.File.WriteAllText("Logs/Signed-Android/later-route-fixture.json", System.Text.Json.JsonSerializer.Serialize(fixture, new System.Text.Json.JsonSerializerOptions { IncludeFields = true }));
                Console.WriteLine($"QA fixture: route {level}, hint vehicle {hint}, yard index {index}");
                return;
            }
            throw new Exception("No suitable scroll fixture found");
        }
        for (int level = 1; level <= 1000; level++)
        {
            var m = TransitModel.Generate(level);
            Assert(m.Lot.Sum(v => v.Capacity) == m.Queue.Count, "Capacity mismatch");
            for (int c = 0; c < 6; c++) Assert(m.Lot.Where(v => v.Color == c).Sum(v => v.Capacity) == m.Queue.Count(x => x == c), "Color mismatch");
            Assert(m.Queue.SequenceEqual(TransitModel.Generate(level).Queue), "Nondeterministic level");
            int steps = 0;
            while (!m.Won && steps++ < 1000)
            {
                while (m.CanBoard) m.BoardOne();
                if (m.Won) break;
                int hint = m.Hint();
                Assert(hint >= 0, "Unsolvable hint path, level " + level);
                var copy = m.Copy();
                Assert(m.Dispatch(hint), "Dispatch rejected");
                var encoded = System.Text.Json.JsonSerializer.Serialize(BoardSave.From(m), new System.Text.Json.JsonSerializerOptions { IncludeFields = true });
                var restored = System.Text.Json.JsonSerializer.Deserialize<BoardSave>(encoded,new System.Text.Json.JsonSerializerOptions { IncludeFields = true }).Restore();
                Assert(restored.Cursor == m.Cursor && restored.Moves == m.Moves && restored.Lot.Count == m.Lot.Count, "Checkpoint round trip failed");
                restored.Settle(); var expected = m.Copy(); expected.Settle();
                Assert(restored.Cursor == expected.Cursor && restored.Won == expected.Won, "Interrupted boarding recovery diverged");
                Assert(copy.Lot.Count == m.Lot.Count + 1, "Snapshot mutated");
                Assert(!m.Dispatch(hint), "Duplicate dispatch accepted");
            }
            Assert(m.Won, "Level did not complete: " + level);
        }
        var jam = new TransitModel(); jam.Queue.Add(1);
        for (int i = 0; i < 3; i++) jam.Lot.Add(new Vehicle { Id = i, Color = 0, Capacity = 1 });
        for (int i = 0; i < 3; i++) Assert(jam.Dispatch(i), "Jam setup failed");
        Assert(jam.Blocked && !jam.CanBoard && !jam.Won, "Deadlock not detected");
        Assert(jam.BoardOne() == -1 && jam.Cursor == 0, "Wrong color boarded");
        var lanes = TransitModel.Generate(7);
        var blocked = lanes.Lot.First(v => !lanes.IsAccessible(v));
        Assert(!lanes.Dispatch(blocked.Id), "Blocked parking lane dispatched");
        var bad = BoardSave.From(TransitModel.Generate(2)); bad.cursor = 100;
        bool rejected = false; try { bad.Restore(); } catch { rejected = true; }
        Assert(rejected, "Corrupt checkpoint accepted");
        Console.WriteLine("PASS: 1,000 levels solved; color/capacity conservation, deterministic generation, snapshots, duplicate dispatch, deadlock, parking blockers, checkpoint serialization, interrupted boarding recovery, and corrupt checkpoint rejection.");
    }
}
