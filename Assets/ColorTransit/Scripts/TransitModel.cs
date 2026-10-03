using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorTransit
{
    [Serializable] public sealed class Vehicle
    {
        public int Id, Color, Capacity, Boarded;
        public Vehicle Copy() => (Vehicle)MemberwiseClone();
    }

    // No Unity dependencies: generation and game rules can be checked outside the editor.
    [Serializable] public sealed class TransitModel
    {
        public int Level, Cursor, Moves;
        public List<int> Queue = new List<int>();
        public List<Vehicle> Lot = new List<Vehicle>();
        public Vehicle[] Bays = new Vehicle[3];
        public bool Won => Cursor == Queue.Count && Lot.Count == 0 && Bays.All(v => v == null);
        public bool Blocked => !Won && Bays.All(v => v != null) && !CanBoard;
        public bool CanBoard => Cursor < Queue.Count && Bays.Any(v => v != null && v.Color == Queue[Cursor]);
        public int FreeBay => Array.FindIndex(Bays, v => v == null);

        public static TransitModel Generate(int level)
        {
            var m = new TransitModel { Level = Math.Max(1, Math.Min(10000, level)) };
            var rng = new Random(731 + m.Level * 107);
            int colors = Math.Min(6, 2 + (m.Level - 1) / 3);
            int count = Math.Min(18, 4 + (m.Level - 1) / 2);
            // Each wave contains at most two colors; following the queue always yields a solution.
            for (int i = 0; i < count; i += 2)
            {
                int a = rng.Next(colors), b = (a + 1 + rng.Next(colors - 1)) % colors;
                var wave = new List<int>();
                for (int j = 0; j < Math.Min(2, count - i); j++)
                {
                    int color = j == 0 ? a : b, capacity = m.Level < 4 ? 3 : rng.Next(3, 6);
                    m.Lot.Add(new Vehicle { Id = i + j, Color = color, Capacity = capacity });
                    for (int k = 0; k < capacity; k++) wave.Add(color);
                }
                if (m.Level > 1)
                    for (int j = wave.Count - 1; j > 0; j--) { int k = rng.Next(j + 1); int t = wave[j]; wave[j] = wave[k]; wave[k] = t; }
                m.Queue.AddRange(wave);
            }
            for (int j = m.Lot.Count - 1; j > 0; j--) { int k = rng.Next(j + 1); var t = m.Lot[j]; m.Lot[j] = m.Lot[k]; m.Lot[k] = t; }
            return m;
        }
        public bool Dispatch(int id)
        {
            int bay = FreeBay; var vehicle = Lot.Find(v => v.Id == id);
            if (bay < 0 || vehicle == null || Won || Blocked || !IsAccessible(vehicle)) return false;
            Lot.Remove(vehicle); Bays[bay] = vehicle; Moves++; return true;
        }
        public int BoardOne()
        {
            if (!CanBoard) return -1;
            int bay = Array.FindIndex(Bays, v => v != null && v.Color == Queue[Cursor]);
            Cursor++; Bays[bay].Boarded++;
            if (Bays[bay].Boarded == Bays[bay].Capacity) Bays[bay] = null;
            return bay;
        }
        public TransitModel Copy() => new TransitModel {
            Level = Level, Cursor = Cursor, Moves = Moves, Queue = new List<int>(Queue),
            Lot = Lot.Select(v => v.Copy()).ToList(), Bays = Bays.Select(v => v?.Copy()).ToArray()
        };
        public bool IsAccessible(Vehicle v) => Level < 7 || !Lot.Any(other => other.Id % 3 == v.Id % 3 && other.Id < v.Id);
        public int Hint()
        {
            if (FreeBay < 0 || Cursor >= Queue.Count) return -1;
            var visited = new HashSet<string>(); int budget = 12000;
            foreach (var v in Lot.Where(IsAccessible).OrderBy(v => v.Color == Queue[Cursor] ? 0 : 1).ThenBy(v => v.Id))
            {
                var next = Copy(); next.Dispatch(v.Id); next.Settle();
                if (Solvable(next, visited, ref budget)) return v.Id;
            }
            return -1;
        }
        public void Settle() { while (CanBoard) BoardOne(); }
        static bool Solvable(TransitModel m, HashSet<string> visited, ref int budget)
        {
            if (m.Won) return true;
            if (m.FreeBay < 0 || --budget < 0) return false;
            string key = m.Cursor + ":" + string.Join(",",m.Lot.Select(v => v.Id).OrderBy(x => x)) + ":" + string.Join(",",m.Bays.Select(v => v == null ? "-" : v.Id + "/" + v.Boarded));
            if (!visited.Add(key)) return false;
            foreach (var v in m.Lot.Where(m.IsAccessible).OrderBy(v => v.Color == m.Queue[m.Cursor] ? 0 : 1).ThenBy(v => v.Id))
            {
                var next = m.Copy(); next.Dispatch(v.Id); next.Settle();
                if (Solvable(next,visited,ref budget)) return true;
            }
            return false;
        }
    }
}
