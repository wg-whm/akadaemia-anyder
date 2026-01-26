using System.Collections.Generic;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using AkadaemiaAnyder.Data.Models;

namespace AkadaemiaAnyder.Data.Repositories
{
    /// <summary>
    /// Specialized repository for gathering node collection entries.
    /// Provides filtering by gathering class, legendary/ephemeral nodes, and zones.
    /// </summary>
    public class GatheringRepository : CollectionRepository
    {
        private readonly DatabaseContext context;
        private readonly IPluginLog log;

        public GatheringRepository(DatabaseContext databaseContext, IPluginLog pluginLog)
            : base(databaseContext, pluginLog)
        {
            context = databaseContext;
            log = pluginLog;
        }

        /// <summary>
        /// Gets all gathering nodes for a specific gathering class.
        /// </summary>
        public async Task<List<GatheringNodeEntry>> GetByGatheringClassAsync(GatheringClass gatheringClass)
        {
            log.Debug($"GetByGatheringClassAsync(class={gatheringClass})");
            var results = new List<GatheringNodeEntry>();

            if (context.Connection == null)
            {
                log.Warning("Database connection is null");
                return results;
            }

            var sql = @"
                SELECT c.*, g.node_id, g.gathering_class, g.zone, g.folklore_book_id,
                       g.node_level, g.is_legendary, g.is_ephemeral
                FROM collections c
                INNER JOIN gathering_nodes g ON c.id = g.collection_id
                WHERE c.type = 2 AND g.gathering_class = @gathering_class
                ORDER BY g.zone ASC, g.node_level ASC";

            using var command = context.Connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.AddWithValue("@gathering_class", (int)gatheringClass);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var entry = MapGatheringFromReader(reader);
                if (entry != null)
                {
                    results.Add(entry);
                }
            }

            log.Debug($"GetByGatheringClassAsync(class={gatheringClass}) returned {results.Count} nodes");
            return results;
        }

        /// <summary>
        /// Gets all legendary gathering nodes (timed nodes).
        /// </summary>
        public async Task<List<GatheringNodeEntry>> GetLegendaryNodesAsync()
        {
            log.Debug("GetLegendaryNodesAsync()");
            var results = new List<GatheringNodeEntry>();

            if (context.Connection == null)
            {
                log.Warning("Database connection is null");
                return results;
            }

            var sql = @"
                SELECT c.*, g.node_id, g.gathering_class, g.zone, g.folklore_book_id,
                       g.node_level, g.is_legendary, g.is_ephemeral
                FROM collections c
                INNER JOIN gathering_nodes g ON c.id = g.collection_id
                WHERE c.type = 2 AND g.is_legendary = 1
                ORDER BY g.gathering_class ASC, g.zone ASC";

            using var command = context.Connection.CreateCommand();
            command.CommandText = sql;

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var entry = MapGatheringFromReader(reader);
                if (entry != null)
                {
                    results.Add(entry);
                }
            }

            log.Debug($"GetLegendaryNodesAsync() returned {results.Count} nodes");
            return results;
        }

        /// <summary>
        /// Gets all ephemeral gathering nodes (special time-limited nodes).
        /// </summary>
        public async Task<List<GatheringNodeEntry>> GetEphemeralNodesAsync()
        {
            log.Debug("GetEphemeralNodesAsync()");
            var results = new List<GatheringNodeEntry>();

            if (context.Connection == null)
            {
                log.Warning("Database connection is null");
                return results;
            }

            var sql = @"
                SELECT c.*, g.node_id, g.gathering_class, g.zone, g.folklore_book_id,
                       g.node_level, g.is_legendary, g.is_ephemeral
                FROM collections c
                INNER JOIN gathering_nodes g ON c.id = g.collection_id
                WHERE c.type = 2 AND g.is_ephemeral = 1
                ORDER BY g.gathering_class ASC, g.zone ASC";

            using var command = context.Connection.CreateCommand();
            command.CommandText = sql;

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var entry = MapGatheringFromReader(reader);
                if (entry != null)
                {
                    results.Add(entry);
                }
            }

            log.Debug($"GetEphemeralNodesAsync() returned {results.Count} nodes");
            return results;
        }

        /// <summary>
        /// Gets all gathering nodes in a specific zone.
        /// </summary>
        public async Task<List<GatheringNodeEntry>> GetByZoneAsync(string zone)
        {
            log.Debug($"GetByZoneAsync(zone={zone})");
            var results = new List<GatheringNodeEntry>();

            if (context.Connection == null)
            {
                log.Warning("Database connection is null");
                return results;
            }

            var sql = @"
                SELECT c.*, g.node_id, g.gathering_class, g.zone, g.folklore_book_id,
                       g.node_level, g.is_legendary, g.is_ephemeral
                FROM collections c
                INNER JOIN gathering_nodes g ON c.id = g.collection_id
                WHERE c.type = 2 AND g.zone = @zone
                ORDER BY g.gathering_class ASC, g.node_level ASC";

            using var command = context.Connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.AddWithValue("@zone", zone);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var entry = MapGatheringFromReader(reader);
                if (entry != null)
                {
                    results.Add(entry);
                }
            }

            log.Debug($"GetByZoneAsync(zone={zone}) returned {results.Count} nodes");
            return results;
        }

        private GatheringNodeEntry? MapGatheringFromReader(Microsoft.Data.Sqlite.SqliteDataReader reader)
        {
            var entry = new GatheringNodeEntry
            {
                Id = reader.GetInt32(reader.GetOrdinal("id")),
                CharacterId = reader.GetInt32(reader.GetOrdinal("character_id")),
                CharacterName = reader.GetString(reader.GetOrdinal("character_name")),
                WorldName = reader.GetString(reader.GetOrdinal("world_name")),
                Type = (CollectionType)reader.GetInt32(reader.GetOrdinal("type")),
                ItemId = reader.GetInt32(reader.GetOrdinal("item_id")),
                ItemName = reader.GetString(reader.GetOrdinal("item_name")),
                IsUnlocked = reader.GetInt32(reader.GetOrdinal("is_unlocked")) == 1,
                FirstSeenAt = System.DateTime.Parse(reader.GetString(reader.GetOrdinal("first_seen_at"))),
                LastUpdatedAt = System.DateTime.Parse(reader.GetString(reader.GetOrdinal("last_updated_at"))),
                NodeId = reader.GetInt32(reader.GetOrdinal("node_id")),
                GatheringClass = (GatheringClass)reader.GetInt32(reader.GetOrdinal("gathering_class")),
                Zone = reader.GetString(reader.GetOrdinal("zone")),
                NodeLevel = reader.GetInt32(reader.GetOrdinal("node_level")),
                IsLegendary = reader.GetInt32(reader.GetOrdinal("is_legendary")) == 1,
                IsEphemeral = reader.GetInt32(reader.GetOrdinal("is_ephemeral")) == 1
            };

            var unlockedAtOrdinal = reader.GetOrdinal("unlocked_at");
            entry.UnlockedAt = reader.IsDBNull(unlockedAtOrdinal)
                ? null
                : System.DateTime.Parse(reader.GetString(unlockedAtOrdinal));

            var folkloreOrdinal = reader.GetOrdinal("folklore_book_id");
            entry.FolkloreBookId = reader.IsDBNull(folkloreOrdinal)
                ? null
                : reader.GetInt32(folkloreOrdinal);

            return entry;
        }
    }
}
