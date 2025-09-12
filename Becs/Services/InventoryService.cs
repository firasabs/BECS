using System.Collections.Concurrent;
using Becs.Models;

namespace Becs.Services;

public class InventoryService
{
    private readonly ConcurrentDictionary<string, BloodUnit> _units = new();
    private int _seq = 1;
    private readonly object _lock = new();

    // Frequency score (higher = more common)
    private static readonly Dictionary<string, double> FREQ = new()
    {
        ["O+"] = 0.37, ["A+"] = 0.34, ["B+"] = 0.10, ["AB+"] = 0.04,
        ["O-"] = 0.06, ["A-"] = 0.06, ["B-"] = 0.02, ["AB-"] = 0.01
    };

    private static readonly Dictionary<string, HashSet<string>> ABO_COMPAT = new()
    {
        ["O"]  = new() { "O","A","B","AB" },
        ["A"]  = new() { "A","AB" },
        ["B"]  = new() { "B","AB" },
        ["AB"] = new() { "AB" }
    };

    public InventoryService()
    {
        // seed demo
        AddDonation(new DonationInput { ABO = "O", RhSign = "-", DonationDate = DateTime.Today.AddDays(-8), DonorId="111111111", DonorName="תורם דמו" });
        AddDonation(new DonationInput { ABO = "O", RhSign = "+", DonationDate = DateTime.Today.AddDays(-7), DonorId="222222222", DonorName="תורם דמה" });
        AddDonation(new DonationInput { ABO = "A", RhSign = "+", DonationDate = DateTime.Today.AddDays(-6), DonorId="333333333", DonorName="דוד לוי" });
        AddDonation(new DonationInput { ABO = "A", RhSign = "-", DonationDate = DateTime.Today.AddDays(-6), DonorId="444444444", DonorName="שרה כהן" });
        AddDonation(new DonationInput { ABO = "B", RhSign = "+", DonationDate = DateTime.Today.AddDays(-5), DonorId="555555555", DonorName="יואב בר" });
        AddDonation(new DonationInput { ABO = "AB", RhSign = "+", DonationDate = DateTime.Today.AddDays(-4), DonorId="666666666", DonorName="נעמה ברק" });
        AddDonation(new DonationInput { ABO = "O", RhSign = "-", DonationDate = DateTime.Today.AddDays(-3), DonorId="777777777", DonorName="רון עמית" });
        AddDonation(new DonationInput { ABO = "B", RhSign = "-", DonationDate = DateTime.Today.AddDays(-3), DonorId="888888888", DonorName="נועה נבון" });
    }

    public IEnumerable<BloodUnit> AllUnits() => _units.Values.OrderBy(u => u.DonationDate);

    public BloodUnit AddDonation(DonationInput dto)
    {
        var id = NextId();
        var rh = dto.RhSign == "-" ? Rh.Neg : Rh.Pos;
        var unit = new BloodUnit {
            Id = id,
            Type = new BloodType(dto.ABO.ToUpperInvariant(), rh),
            DonationDate = dto.DonationDate,
            DonorId = dto.DonorId,
            DonorName = dto.DonorName
        };
        _units[id] = unit;
        return unit;
    }

    private string NextId()
    {
        lock (_lock) { return $"U{_seq++.ToString("D6")}"; }
    }

    // --- Compatibility for RBC ---
    private static bool RhCompatible(Rh donor, Rh recip) => donor == Rh.Neg || recip == Rh.Pos;
    private static bool AboCompatible(string donorAbo, string recipAbo) => ABO_COMPAT[donorAbo].Contains(recipAbo);

    public static bool IsCompatible(BloodType donor, BloodType recip)
        => AboCompatible(donor.ABO, recip.ABO) && RhCompatible(donor.Rh, recip.Rh);

    public static string ToSign(Rh rh) => rh == Rh.Neg ? "-" : "+";
    public static string Compose(BloodType t) => $"{t.ABO}{ToSign(t.Rh)}";

    // --- Routine selection: exact first, then compatible by frequency (desc) ---
    public (List<BloodUnit> chosen, List<(string bt, int count)> suggestions) SelectForRoutine(BloodType requested, int qty)
    {
        var avail = _units.Values.Where(u => IsCompatible(u.Type, requested)).ToList();
        if (avail.Count == 0)
            return (new(), SuggestAlternatives(requested));

        var exact = avail.Where(u => u.Type.ABO == requested.ABO && u.Type.Rh == requested.Rh)
                         .OrderBy(u => u.DonationDate) // FEFO surrogate
                         .ToList();
        var chosen = new List<BloodUnit>();
        var remaining = qty;

        foreach (var u in exact)
        {
            if (remaining == 0) break;
            chosen.Add(u); remaining--;
        }

        if (remaining > 0)
        {
            var alts = avail.Where(u => !(u.Type.ABO == requested.ABO && u.Type.Rh == requested.Rh))
                            .OrderByDescending(u => FREQ.GetValueOrDefault(Compose(u.Type), 0.0))
                            .ThenBy(u => u.DonationDate)
                            .ToList();

            foreach (var u in alts)
            {
                if (remaining == 0) break;
                chosen.Add(u); remaining--;
            }
        }

        var suggestions = remaining > 0 ? SuggestAlternatives(requested) : new List<(string, int)>();
        return (chosen, suggestions);
    }

    public List<(string bt, int count)> SuggestAlternatives(BloodType requested, int topK = 6)
    {
        var bar = new Dictionary<string, int>();
        foreach (var u in _units.Values)
        {
            if (IsCompatible(u.Type, requested))
            {
                var key = Compose(u.Type);
                bar[key] = bar.GetValueOrDefault(key, 0) + 1;
            }
        }
        var reqKey = Compose(requested);
        if (bar.ContainsKey(reqKey)) bar.Remove(reqKey);

        return bar.OrderByDescending(kv => FREQ.GetValueOrDefault(kv.Key, 0.0))
                  .ThenByDescending(kv => kv.Value)
                  .Take(topK)
                  .Select(kv => (kv.Key, kv.Value))
                  .ToList();
    }

    // Issue routine (remove selected IDs from stock)
    public List<BloodUnit> IssueByIds(IEnumerable<string> ids, string mode)
    {
        var issued = new List<BloodUnit>();
        foreach (var id in ids)
        {
            if (_units.TryRemove(id, out var unit))
                issued.Add(unit);
        }
        return issued;
    }

    // --- Emergency: issue ALL O- or error if none ---
    public List<BloodUnit> IssueEmergencyONeg()
    {
        var ones = _units.Values.Where(u => u.Type.ABO == "O" && u.Type.Rh == Rh.Neg).ToList();
        if (!ones.Any()) return new();
        return IssueByIds(ones.Select(u => u.Id), "MCI");
    }
}
