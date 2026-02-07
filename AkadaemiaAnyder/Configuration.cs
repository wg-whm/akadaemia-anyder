using Dalamud.Configuration;
using System;

namespace SamplePlugin;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public bool IsConfigWindowMovable { get; set; } = true;
    public bool SomePropertyToBeSavedAndWithADefault { get; set; } = true;

    /// <summary>
    /// Privacy settings controlling PII storage and export anonymization.
    /// Defaults are privacy-first: character names OFF, anonymous exports ON.
    /// </summary>
    public PrivacySettingsConfig PrivacySettings { get; set; } = new();

    /// <summary>
    /// UI state settings for maintaining tab selection and window positions.
    /// </summary>
    public UIStateConfig UIState { get; set; } = new();

    // The below exists just to make saving less cumbersome
    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}

/// <summary>
/// Privacy configuration settings for the plugin.
/// Privacy-first defaults: character names OFF, anonymous exports ON.
/// </summary>
[Serializable]
public class PrivacySettingsConfig
{
    /// <summary>
    /// When false (default), character names are never stored in the database.
    /// All records use only character IDs for privacy protection.
    /// </summary>
    public bool StoreCharacterNames { get; set; } = false;

    /// <summary>
    /// When true (default), exports replace character names with anonymized IDs
    /// like 'Character_001'. This protects privacy when sharing data.
    /// </summary>
    public bool EnableAnonymousExport { get; set; } = true;

    /// <summary>
    /// When true (default), server/datacenter information is excluded from exports.
    /// </summary>
    public bool ExcludeServerFromExport { get; set; } = true;
}

/// <summary>
/// UI state configuration for remembering user preferences.
/// </summary>
[Serializable]
public class UIStateConfig
{
    /// <summary>
    /// Last selected tab index in the main window.
    /// </summary>
    public int LastSelectedTab { get; set; } = 0;

    /// <summary>
    /// Whether the inventory search was expanded.
    /// </summary>
    public bool InventorySearchExpanded { get; set; } = true;

    /// <summary>
    /// Last search query in inventory tab (for convenience).
    /// </summary>
    public string LastInventorySearchQuery { get; set; } = string.Empty;
}
