using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using AkadaemiaAnyder.Modules.Core.Interfaces;
// Type aliases to avoid conflicts with local types
using InterfaceMaterialAvailability = AkadaemiaAnyder.Modules.Core.Interfaces.MaterialAvailability;
using InterfaceMaterialLocation = AkadaemiaAnyder.Modules.Core.Interfaces.MaterialLocation;
using InterfaceCollectionProgress = AkadaemiaAnyder.Modules.Core.Interfaces.CollectionProgress;
using InterfaceCollectionCategory = AkadaemiaAnyder.Modules.Core.Interfaces.CollectionCategory;
using InterfaceCraftingListData = AkadaemiaAnyder.Modules.Core.Interfaces.CraftingListData;
using InterfaceCraftingListItemData = AkadaemiaAnyder.Modules.Core.Interfaces.CraftingListItemData;
using InterfaceCraftingHistory = AkadaemiaAnyder.Modules.Core.Interfaces.CraftingHistory;
using InterfaceCraftingSessionData = AkadaemiaAnyder.Modules.Core.Interfaces.CraftingSessionData;

namespace AkadaemiaAnyder.Data.Repositories
{
    /// <summary>
    /// Adapter class that implements IRepositoryIntegration by delegating to specialized repositories.
    /// Provides a unified interface for Artisan modules to access Akadaemia Anyder's database.
    /// </summary>
    public class RepositoryIntegrationAdapter : IRepositoryIntegration
    {
        private readonly MaterialAvailabilityRepository _materialRepo;
        private readonly CraftingListRepository _craftingListRepo;
        private readonly ICollectionRepository _collectionRepo;
        private readonly IClientState _clientState;
        private readonly IPluginLog _log;

        public RepositoryIntegrationAdapter(
            MaterialAvailabilityRepository materialRepo,
            CraftingListRepository craftingListRepo,
            ICollectionRepository collectionRepo,
            IClientState clientState,
            IPluginLog log)
        {
            _materialRepo = materialRepo;
            _craftingListRepo = craftingListRepo;
            _collectionRepo = collectionRepo;
            _clientState = clientState;
            _log = log;
        }

        private ulong GetCharacterContentId()
        {
            return _clientState.LocalContentId;
        }

        #region Material Availability

        public async Task<InterfaceMaterialAvailability> GetMaterialAvailabilityAsync(uint itemId)
        {
            _log.Debug($"GetMaterialAvailabilityAsync(itemId={itemId})");

            var characterId = GetCharacterContentId();
            var repoResult = await _materialRepo.GetMaterialAvailabilityAsync(itemId, characterId);

            // Map from repository type to interface type
            return new InterfaceMaterialAvailability
            {
                ItemId = repoResult.ItemId,
                ItemName = repoResult.ItemName,
                InInventory = repoResult.InInventory,
                InSaddlebag = repoResult.InSaddlebag,
                InRetainers = repoResult.InRetainers,
                InGlamourDresser = repoResult.InGlamourDresser,
                InArmoryChest = repoResult.InArmoryChest,
                ByLocation = repoResult.ByLocation
            };
        }

        public async Task<List<InterfaceMaterialLocation>> FindMaterialLocationsAsync(uint itemId)
        {
            _log.Debug($"FindMaterialLocationsAsync(itemId={itemId})");

            var characterId = GetCharacterContentId();
            var repoResults = await _materialRepo.FindMaterialLocationsAsync(itemId, characterId);

            // Map from repository type to interface type
            return repoResults.Select(r => new InterfaceMaterialLocation
            {
                Location = r.Location,
                SlotId = r.SlotId,
                Quantity = r.Quantity,
                IsHQ = r.IsHQ
            }).ToList();
        }

        public async Task<Dictionary<uint, InterfaceMaterialAvailability>> GetBulkMaterialAvailabilityAsync(IEnumerable<uint> itemIds)
        {
            _log.Debug($"GetBulkMaterialAvailabilityAsync(count={itemIds.Count()})");

            var characterId = GetCharacterContentId();
            var repoResults = await _materialRepo.GetBulkMaterialAvailabilityAsync(itemIds, characterId);

            // Map from repository type to interface type
            var result = new Dictionary<uint, InterfaceMaterialAvailability>();
            foreach (var kvp in repoResults)
            {
                result[kvp.Key] = new InterfaceMaterialAvailability
                {
                    ItemId = kvp.Value.ItemId,
                    ItemName = kvp.Value.ItemName,
                    InInventory = kvp.Value.InInventory,
                    InSaddlebag = kvp.Value.InSaddlebag,
                    InRetainers = kvp.Value.InRetainers,
                    InGlamourDresser = kvp.Value.InGlamourDresser,
                    InArmoryChest = kvp.Value.InArmoryChest,
                    ByLocation = kvp.Value.ByLocation
                };
            }

            return result;
        }

        #endregion

        #region Collection Bindings

        public async Task<bool> IsItemCollectedAsync(uint itemId, InterfaceCollectionCategory category)
        {
            _log.Debug($"IsItemCollectedAsync(itemId={itemId}, category={category})");

            var characterId = GetCharacterContentId();

            // Query collections table filtered by character, type, and itemId
            var entries = await _collectionRepo.GetAllAsync<Models.CollectionEntry>();

            var entry = entries.FirstOrDefault(e =>
                (ulong)e.CharacterId == characterId &&
                e.ItemId == itemId &&
                MapCategoryToCollectionType(category) == e.Type);

            return entry?.IsUnlocked ?? false;
        }

        public async Task<InterfaceCollectionProgress> GetCollectionProgressAsync(InterfaceCollectionCategory category)
        {
            _log.Debug($"GetCollectionProgressAsync(category={category})");

            var characterId = GetCharacterContentId();
            var collectionType = MapCategoryToCollectionType(category);

            var entries = await _collectionRepo.GetAllAsync<Models.CollectionEntry>();
            var categoryEntries = entries.Where(e =>
                (ulong)e.CharacterId == characterId &&
                e.Type == collectionType).ToList();

            var unlocked = categoryEntries.Count(e => e.IsUnlocked);
            var total = categoryEntries.Count;

            return new InterfaceCollectionProgress
            {
                Category = category,
                Unlocked = unlocked,
                Total = total
            };
        }

        public async Task<Dictionary<InterfaceCollectionCategory, InterfaceCollectionProgress>> GetAllCollectionProgressAsync()
        {
            _log.Debug("GetAllCollectionProgressAsync()");

            var result = new Dictionary<InterfaceCollectionCategory, InterfaceCollectionProgress>();

            // Query all collection categories
            var categories = Enum.GetValues(typeof(InterfaceCollectionCategory)).Cast<InterfaceCollectionCategory>();

            foreach (var category in categories)
            {
                result[category] = await GetCollectionProgressAsync(category);
            }

            return result;
        }

        private Models.CollectionType MapCategoryToCollectionType(InterfaceCollectionCategory category)
        {
            // Map CollectionCategory (from IRepositoryIntegration) to CollectionType (from database models)
            return category switch
            {
                InterfaceCollectionCategory.Recipe => Models.CollectionType.Recipe,
                InterfaceCollectionCategory.Gathering => Models.CollectionType.Gathering,
                InterfaceCollectionCategory.Fishing => Models.CollectionType.Fishing,
                InterfaceCollectionCategory.Mount => Models.CollectionType.Mount,
                InterfaceCollectionCategory.Minion => Models.CollectionType.Minion,
                InterfaceCollectionCategory.TripleTriadCard => Models.CollectionType.TripleTriadCard,
                InterfaceCollectionCategory.OrchestrionRoll => Models.CollectionType.OrchestrionRoll,
                InterfaceCollectionCategory.Emote => Models.CollectionType.Emote,
                InterfaceCollectionCategory.Hairstyle => Models.CollectionType.Hairstyle,
                InterfaceCollectionCategory.Barding => Models.CollectionType.Barding,
                InterfaceCollectionCategory.BlueMageSpell => Models.CollectionType.BlueMageSpell,
                _ => throw new ArgumentOutOfRangeException(nameof(category), category, "Unknown collection category")
            };
        }

        #endregion

        #region Crafting List Persistence

        public async Task SaveCraftingListAsync(InterfaceCraftingListData list)
        {
            _log.Debug($"SaveCraftingListAsync(listId={list.Id}, name={list.Name})");

            // Convert interface DTO to repository DTO
            var repoList = new CraftingListRepository.CraftingListData
            {
                Id = list.Id,
                Name = list.Name,
                CharacterContentId = list.CharacterContentId,
                Created = list.Created,
                LastModified = list.LastModified,
                Items = list.Items.Select(i => new CraftingListRepository.CraftingListItemData
                {
                    RecipeId = i.RecipeId,
                    RecipeName = i.RecipeName,
                    Quantity = i.Quantity,
                    QuantityCrafted = i.QuantityCrafted,
                    CraftType = i.CraftType
                }).ToList()
            };

            await _craftingListRepo.SaveCraftingListAsync(repoList);
        }

        public async Task<InterfaceCraftingListData?> LoadCraftingListAsync(string listId)
        {
            _log.Debug($"LoadCraftingListAsync(listId={listId})");

            var characterId = GetCharacterContentId();
            var repoResult = await _craftingListRepo.LoadCraftingListAsync(listId, characterId);

            if (repoResult == null)
                return null;

            // Convert repository DTO to interface DTO
            return new InterfaceCraftingListData
            {
                Id = repoResult.Id,
                Name = repoResult.Name,
                CharacterContentId = repoResult.CharacterContentId,
                Created = repoResult.Created,
                LastModified = repoResult.LastModified,
                Items = repoResult.Items.Select(i => new InterfaceCraftingListItemData
                {
                    RecipeId = i.RecipeId,
                    RecipeName = i.RecipeName,
                    Quantity = i.Quantity,
                    QuantityCrafted = i.QuantityCrafted,
                    CraftType = i.CraftType
                }).ToList()
            };
        }

        public async Task<List<InterfaceCraftingListData>> LoadAllCraftingListsAsync()
        {
            _log.Debug("LoadAllCraftingListsAsync()");

            var characterId = GetCharacterContentId();
            var repoResults = await _craftingListRepo.LoadAllCraftingListsAsync(characterId);

            // Convert repository DTOs to interface DTOs
            return repoResults.Select(r => new InterfaceCraftingListData
            {
                Id = r.Id,
                Name = r.Name,
                CharacterContentId = r.CharacterContentId,
                Created = r.Created,
                LastModified = r.LastModified,
                Items = r.Items.Select(i => new InterfaceCraftingListItemData
                {
                    RecipeId = i.RecipeId,
                    RecipeName = i.RecipeName,
                    Quantity = i.Quantity,
                    QuantityCrafted = i.QuantityCrafted,
                    CraftType = i.CraftType
                }).ToList()
            }).ToList();
        }

        public async Task DeleteCraftingListAsync(string listId)
        {
            _log.Debug($"DeleteCraftingListAsync(listId={listId})");

            await _craftingListRepo.DeleteCraftingListAsync(listId);
        }

        #endregion

        #region Recipe Tracking

        public async Task<List<uint>> GetCraftedRecipesAsync()
        {
            _log.Debug("GetCraftedRecipesAsync()");

            var characterId = GetCharacterContentId();
            return await _craftingListRepo.GetCraftedRecipesAsync(characterId);
        }

        public async Task RecordCraftedRecipeAsync(uint recipeId, bool wasHQ)
        {
            _log.Debug($"RecordCraftedRecipeAsync(recipeId={recipeId}, wasHQ={wasHQ})");

            var characterId = GetCharacterContentId();
            // Recipe name can be looked up from game data if needed
            await _craftingListRepo.RecordCraftedRecipeAsync(recipeId, $"Recipe {recipeId}", wasHQ, characterId);
        }

        public async Task<InterfaceCraftingHistory> GetCraftingHistoryAsync(uint recipeId)
        {
            _log.Debug($"GetCraftingHistoryAsync(recipeId={recipeId})");

            var characterId = GetCharacterContentId();
            var repoResult = await _craftingListRepo.GetCraftingHistoryAsync(recipeId, characterId);

            if (repoResult == null)
            {
                return new InterfaceCraftingHistory
                {
                    RecipeId = recipeId,
                    RecipeName = $"Recipe {recipeId}",
                    TotalCrafted = 0,
                    HQCount = 0,
                    FirstCrafted = DateTime.UtcNow,
                    LastCrafted = DateTime.UtcNow
                };
            }

            return new InterfaceCraftingHistory
            {
                RecipeId = repoResult.RecipeId,
                RecipeName = repoResult.RecipeName,
                TotalCrafted = repoResult.TotalCrafted,
                HQCount = repoResult.HQCount,
                FirstCrafted = repoResult.FirstCrafted,
                LastCrafted = repoResult.LastCrafted
            };
        }

        #endregion

        #region Session Tracking

        public async Task RecordCraftingSessionAsync(InterfaceCraftingSessionData session)
        {
            _log.Debug($"RecordCraftingSessionAsync(sessionId={session.Id}, duration={session.Duration})");

            // Convert interface DTO to repository DTO
            var repoSession = new CraftingListRepository.CraftingSessionData
            {
                Id = session.Id,
                CharacterContentId = session.CharacterContentId,
                Start = session.Start,
                End = session.End,
                ItemsCrafted = session.ItemsCrafted,
                HQCount = session.HQCount,
                RecipeIds = session.RecipeIds
            };

            await _craftingListRepo.RecordCraftingSessionAsync(repoSession);
        }

        public async Task<List<InterfaceCraftingSessionData>> GetRecentSessionsAsync(int days)
        {
            _log.Debug($"GetRecentSessionsAsync(days={days})");

            var characterId = GetCharacterContentId();
            var repoResults = await _craftingListRepo.GetRecentSessionsAsync(characterId, days);

            // Convert repository DTOs to interface DTOs
            return repoResults.Select(r => new InterfaceCraftingSessionData
            {
                Id = r.Id,
                CharacterContentId = r.CharacterContentId,
                Start = r.Start,
                End = r.End,
                ItemsCrafted = r.ItemsCrafted,
                HQCount = r.HQCount,
                RecipeIds = r.RecipeIds
            }).ToList();
        }

        #endregion
    }
}
