namespace Becs.Services
{
    public interface IBloodCompatibilityService
    {
        // returns list of compatible donor types for requested recipient type
        // e.g., request "A+" => ["A+","A-","O+","O-"]
        IReadOnlyList<string> GetCompatibleTypes(string abo, string rh);
        IReadOnlyList<string> GetAlternativesOrderedByRarity(string abo, string rh);
    }

    public class BloodCompatibilityService : IBloodCompatibilityService
    {
        private static readonly Dictionary<(string abo, string rh), string[]> compat = new()
        {
            { ("O","+"), new[] { "O+", "O-" } },
            { ("O","-"), new[] { "O-" } },
            { ("A","+"), new[] { "A+", "A-", "O+", "O-" } },
            { ("A","-"), new[] { "A-", "O-" } },
            { ("B","+"), new[] { "B+", "B-", "O+", "O-" } },
            { ("B","-"), new[] { "B-", "O-" } },
            { ("AB","+"), new[] { "AB+","AB-","A+","A-","B+","B-","O+","O-" } },
            { ("AB","-"), new[] { "AB-","A-","B-","O-" } },
        };

        public IReadOnlyList<string> GetCompatibleTypes(string abo, string rh)
        {
            return compat.TryGetValue((abo, rh), out var c) ? c : Array.Empty<string>();
        }

        // toy ordering by "rarity" (you can plug real stats later)
        public IReadOnlyList<string> GetAlternativesOrderedByRarity(string abo, string rh)
        {
            var list = GetCompatibleTypes(abo, rh).ToList();
            // move O- to front as “universal” and rare example
            list = list.OrderBy(t => t == "O-" ? 0 : 1).ToList();
            return list;
        }
    }
}