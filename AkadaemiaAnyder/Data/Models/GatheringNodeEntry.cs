namespace AkadaemiaAnyder.Data.Models
{
    /// <summary>
    /// Gathering node collection entry.
    /// Maps to both 'collections' and 'gathering_nodes' tables via JOIN.
    /// </summary>
    public class GatheringNodeEntry : CollectionEntry
    {
        public int NodeId { get; set; }
        public GatheringClass GatheringClass { get; set; }
        public string Zone { get; set; } = string.Empty;
        public int? FolkloreBookId { get; set; }
        public int NodeLevel { get; set; }
        public bool IsLegendary { get; set; }
        public bool IsEphemeral { get; set; }
    }

    /// <summary>
    /// FFXIV gathering classes.
    /// </summary>
    public enum GatheringClass
    {
        Miner = 16,
        Botanist = 17,
        Fisher = 18
    }
}
