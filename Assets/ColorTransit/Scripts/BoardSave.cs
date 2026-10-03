using System;
using System.Linq;
namespace ColorTransit
{
    [Serializable] public sealed class BoardSave
    {
        public int level, cursor, moves;
        public int[] queue;
        public Vehicle[] lot, bays;
        public static BoardSave From(TransitModel m) => new BoardSave {
            level = m.Level, cursor = m.Cursor, moves = m.Moves, queue = m.Queue.ToArray(),
            lot = m.Lot.Select(v => v.Copy()).ToArray(), bays = m.Bays.Select(v => v?.Copy() ?? new Vehicle { Id = -1 }).ToArray()
        };
        public TransitModel Restore()
        {
            if (level < 1 || level > 10000 || queue == null || queue.Length > 100 || cursor < 0 || cursor > queue.Length || lot == null || lot.Length > 18 || bays == null || bays.Length != 3) throw new Exception("Invalid checkpoint bounds");
            var m = new TransitModel { Level = level, Cursor = cursor, Moves = moves, Queue = queue.ToList(), Lot = lot.Select(v => v.Copy()).ToList(), Bays = bays.Select(v => v.Id < 0 ? null : v.Copy()).ToArray() };
            var remaining = m.Lot.Concat(m.Bays.Where(v => v != null)).ToArray();
            if (remaining.Any(v => v.Id < 0 || v.Id >= 18 || v.Color < 0 || v.Color > 5 || v.Capacity < 3 || v.Capacity > 5 || v.Boarded < 0 || v.Boarded >= v.Capacity) || remaining.Select(v => v.Id).Distinct().Count() != remaining.Length || m.Lot.Any(v => v.Boarded != 0) || moves < 0 || moves > 18) throw new Exception("Invalid vehicles");
            if (queue.Any(c => c < 0 || c > 5)) throw new Exception("Invalid queue");
            for (int c = 0; c < 6; c++) if (remaining.Where(v => v.Color == c).Sum(v => v.Capacity - v.Boarded) != m.Queue.Skip(cursor).Count(x => x == c)) throw new Exception("Invalid seat totals");
            return m;
        }
    }
}

