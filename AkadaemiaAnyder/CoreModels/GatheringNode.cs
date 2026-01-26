namespace AkadaemiaAnyder.Core.Models
{
    public class GatheringNode : CollectionEntry
    {
        public uint NodeId { get; set; }
        public GatheringClass GatheringClass { get; set; }
        public string Zone { get; set; } = string.Empty;
        public uint? FolkloreBookId { get; set; }
        public int NodeLevel { get; set; }
        public bool IsLegendary { get; set; }
        public bool IsEphemeral { get; set; }
    }

    public enum GatheringClass : byte
    {
        Miner = 0,
        Botanist = 1
    }
}
