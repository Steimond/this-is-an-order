using Order.Contracts;

namespace Order.Content
{
    // Stable IDs are independent of the still-unapproved faction names and setting.
    public static class PrototypeCatalog
    {
        public const string MapId = "prototype-arena";
        public const string DifficultyId = "normal";
        public static string FactionLabel(FactionId faction) => faction == FactionId.Expedition ? "Фракция 1" : "Фракция 2";
    }
}
