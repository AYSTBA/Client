using Godot;

/// <summary>
/// Autoload that wires <see cref="L10n"/> into the game's lifecycle: it loads the translation
/// table once at startup, resolves the saved language preference and applies it before any
/// scene is instantiated.
/// </summary>
public partial class LocalizationManager : Node
{
    public override void _Ready()
    {
        L10n.Load();

        Apply();
    }

    /// <summary>
    /// Re-resolves the locale from the current settings profile. Safe to call before
    /// <see cref="SettingsManager"/> exists, in which case the system locale is used.
    /// </summary>
    public static void Apply()
    {
        L10n.SetLocale(L10n.ResolvePreference(getSavedPreference()));
    }

    /// <summary>The raw language preference stored in the active profile.</summary>
    private static string getSavedPreference()
    {
        try
        {
            SettingsProfile profile = SettingsManager.Instance?.Settings;

            if (profile?.Language != null)
            {
                return profile.Language.Value;
            }
        }
        catch (System.Exception exception)
        {
            Logger.Error(exception);
        }

        return "System";
    }
}
