using System.Collections.Generic;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using AkadaemiaAnyder.Data.Models;

namespace AkadaemiaAnyder.Data.Repositories
{
    /// <summary>
    /// Specialized repository for fishing hole collection entries.
    /// Provides filtering by big fish, weather/time requirements, and zones.
    /// </summary>
    public class FishingRepository : CollectionRepository
    {
        private readonly DatabaseContext context;
        private readonly IPluginLog log;

        public FishingRepository(DatabaseContext databaseContext, IPluginLog pluginLog)
            : base(databaseContext, pluginLog)
        {
            context = databaseContext;
            log = pluginLog;
        }

        /// <summary>
        /// Gets all big fish entries (legendary catches).
        /// </summary>
        public async Task<List<FishingHoleEntry>> GetBigFishAsync()
        {
            log.Debug("GetBigFishAsync()");
            var results = new List<FishingHoleEntry>();

            if (context.Connection == null)
            {
                log.Warning("Database connection is null");
                return results;
            }

            var sql = @"
                SELECT c.*, f.fish_id, f.fishing_hole_id, f.zone, f.recommended_bait,
                       f.is_big_fish, f.weather_requirement, f.time_requirement
                FROM collections c
                INNER JOIN fishing_holes f ON c.id = f.collection_id
                WHERE c.type = 3 AND f.is_big_fish = 1
                ORDER BY f.zone ASC, c.item_name ASC";

            using var command = context.Connection.CreateCommand();
            command.CommandText = sql;

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var entry = MapFishingFromReader(reader);
                if (entry != null)
                {
                    results.Add(entry);
                }
            }

            log.Debug($"GetBigFishAsync() returned {results.Count} fish");
            return results;
        }

        /// <summary>
        /// Gets all fishing holes with weather requirements.
        /// </summary>
        public async Task<List<FishingHoleEntry>> GetWeatherRestrictedAsync()
        {
            log.Debug("GetWeatherRestrictedAsync()");
            var results = new List<FishingHoleEntry>();

            if (context.Connection == null)
            {
                log.Warning("Database connection is null");
                return results;
            }

            var sql = @"
                SELECT c.*, f.fish_id, f.fishing_hole_id, f.zone, f.recommended_bait,
                       f.is_big_fish, f.weather_requirement, f.time_requirement
                FROM collections c
                INNER JOIN fishing_holes f ON c.id = f.collection_id
                WHERE c.type = 3 AND f.weather_requirement IS NOT NULL
                ORDER BY f.zone ASC, c.item_name ASC";

            using var command = context.Connection.CreateCommand();
            command.CommandText = sql;

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var entry = MapFishingFromReader(reader);
                if (entry != null)
                {
                    results.Add(entry);
                }
            }

            log.Debug($"GetWeatherRestrictedAsync() returned {results.Count} fish");
            return results;
        }

        /// <summary>
        /// Gets all fishing holes with time requirements.
        /// </summary>
        public async Task<List<FishingHoleEntry>> GetTimeRestrictedAsync()
        {
            log.Debug("GetTimeRestrictedAsync()");
            var results = new List<FishingHoleEntry>();

            if (context.Connection == null)
            {
                log.Warning("Database connection is null");
                return results;
            }

            var sql = @"
                SELECT c.*, f.fish_id, f.fishing_hole_id, f.zone, f.recommended_bait,
                       f.is_big_fish, f.weather_requirement, f.time_requirement
                FROM collections c
                INNER JOIN fishing_holes f ON c.id = f.collection_id
                WHERE c.type = 3 AND f.time_requirement IS NOT NULL
                ORDER BY f.zone ASC, c.item_name ASC";

            using var command = context.Connection.CreateCommand();
            command.CommandText = sql;

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var entry = MapFishingFromReader(reader);
                if (entry != null)
                {
                    results.Add(entry);
                }
            }

            log.Debug($"GetTimeRestrictedAsync() returned {results.Count} fish");
            return results;
        }

        /// <summary>
        /// Gets all fishing holes in a specific zone.
        /// </summary>
        public async Task<List<FishingHoleEntry>> GetByZoneAsync(string zone)
        {
            log.Debug($"GetByZoneAsync(zone={zone})");
            var results = new List<FishingHoleEntry>();

            if (context.Connection == null)
            {
                log.Warning("Database connection is null");
                return results;
            }

            var sql = @"
                SELECT c.*, f.fish_id, f.fishing_hole_id, f.zone, f.recommended_bait,
                       f.is_big_fish, f.weather_requirement, f.time_requirement
                FROM collections c
                INNER JOIN fishing_holes f ON c.id = f.collection_id
                WHERE c.type = 3 AND f.zone = @zone
                ORDER BY c.item_name ASC";

            using var command = context.Connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.AddWithValue("@zone", zone);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var entry = MapFishingFromReader(reader);
                if (entry != null)
                {
                    results.Add(entry);
                }
            }

            log.Debug($"GetByZoneAsync(zone={zone}) returned {results.Count} fish");
            return results;
        }

        /// <summary>
        /// Gets all fishing holes using a specific bait.
        /// </summary>
        public async Task<List<FishingHoleEntry>> GetByBaitAsync(string bait)
        {
            log.Debug($"GetByBaitAsync(bait={bait})");
            var results = new List<FishingHoleEntry>();

            if (context.Connection == null)
            {
                log.Warning("Database connection is null");
                return results;
            }

            var sql = @"
                SELECT c.*, f.fish_id, f.fishing_hole_id, f.zone, f.recommended_bait,
                       f.is_big_fish, f.weather_requirement, f.time_requirement
                FROM collections c
                INNER JOIN fishing_holes f ON c.id = f.collection_id
                WHERE c.type = 3 AND f.recommended_bait = @bait
                ORDER BY f.zone ASC, c.item_name ASC";

            using var command = context.Connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.AddWithValue("@bait", bait);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var entry = MapFishingFromReader(reader);
                if (entry != null)
                {
                    results.Add(entry);
                }
            }

            log.Debug($"GetByBaitAsync(bait={bait}) returned {results.Count} fish");
            return results;
        }

        private FishingHoleEntry? MapFishingFromReader(Microsoft.Data.Sqlite.SqliteDataReader reader)
        {
            var entry = new FishingHoleEntry
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
                FishId = reader.GetInt32(reader.GetOrdinal("fish_id")),
                FishingHoleId = reader.GetInt32(reader.GetOrdinal("fishing_hole_id")),
                Zone = reader.GetString(reader.GetOrdinal("zone")),
                RecommendedBait = reader.GetString(reader.GetOrdinal("recommended_bait")),
                IsBigFish = reader.GetInt32(reader.GetOrdinal("is_big_fish")) == 1
            };

            var unlockedAtOrdinal = reader.GetOrdinal("unlocked_at");
            entry.UnlockedAt = reader.IsDBNull(unlockedAtOrdinal)
                ? null
                : System.DateTime.Parse(reader.GetString(unlockedAtOrdinal));

            var weatherOrdinal = reader.GetOrdinal("weather_requirement");
            entry.WeatherRequirement = reader.IsDBNull(weatherOrdinal)
                ? null
                : reader.GetString(weatherOrdinal);

            var timeOrdinal = reader.GetOrdinal("time_requirement");
            entry.TimeRequirement = reader.IsDBNull(timeOrdinal)
                ? null
                : reader.GetString(timeOrdinal);

            return entry;
        }
    }
}
