using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Godot;

/// <summary>
/// Localization core.
/// <para>
/// Translations are authored in <c>res://translations/strings.csv</c>. The first column is the
/// English source string, which doubles as the lookup key, and every following column is a locale
/// code (for example <c>zh_CN</c>).
/// </para>
/// <para>
/// Because the English source text is the key, Godot's auto-translation (enabled by default on
/// Control nodes) translates scene-authored text without any per-node changes.
/// </para>
/// </summary>
public static class L10n
{
    public const string ENGLISH_LOCALE = "en";
    public const string FALLBACK_LOCALE = "en";
    public const string TABLE_PATH = "res://translations/strings.csv";
    public const string TABLE_DIRECTORY = "res://translations";
    public const string TABLE_BASE_NAME = "strings";

    /// <summary>Locales that ship with a translation table.</summary>
    public static readonly string[] TRANSLATED_LOCALES = ["zh_CN"];

    private static readonly Dictionary<string, Translation> tables = new(StringComparer.Ordinal);

    /// <summary>Fonts actually referenced by the UI, patched with a CJK fallback at runtime.</summary>
    private static readonly string[] ui_font_paths =
    [
        "res://fonts/Roboto/Roboto-Medium.ttf",
        "res://fonts/Roboto/Roboto-Bold.ttf",
        "res://fonts/Roboto/Roboto-Italic.ttf",
        "res://fonts/Ubuntu/Ubuntu-Regular.ttf",
        "res://fonts/Ubuntu/Ubuntu-Medium.ttf",
        "res://fonts/Nunito/Nunito-Bold.ttf",
        "res://fonts/Nunito/Nunito-SemiBold.ttf",
        "res://fonts/Comfortaa/Comfortaa-Bold.ttf",
    ];

    private static Font cjkFallbackFont;

    /// <summary>The locale the game is currently running in.</summary>
    public static string Locale { get; private set; } = FALLBACK_LOCALE;

    /// <summary>True once the translation table has been read and registered.</summary>
    public static bool Loaded => tables.Count > 0;

    /// <summary>True when the active locale is Chinese, used to gate the translator credit.</summary>
    public static bool IsChinese => Locale.StartsWith("zh", StringComparison.OrdinalIgnoreCase);

    /// <summary>Translates <paramref name="text"/>, returning it untouched when no entry exists.</summary>
    public static string Tr(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        if (!Loaded)
        {
            return text;
        }

        // Translate() already falls back to the key when the locale has no matching message.
        return TranslationServer.Translate(text);
    }

    /// <summary>
    /// Translates a format string and applies <see cref="string.Format(IFormatProvider, string, object[])"/>.
    /// Keys use numbered placeholders, for example <c>"Initializing maps ({0}/{1})"</c>.
    /// </summary>
    public static string Tr(string text, params object[] args)
    {
        string translated = Tr(text);

        if (args == null || args.Length == 0)
        {
            return translated;
        }

        // Invariant culture keeps numbers identical regardless of the player's system locale.
        try
        {
            return string.Format(CultureInfo.InvariantCulture, translated, args);
        }
        catch (FormatException)
        {
            // A malformed translation must never break the UI: fall back to the English source.
            return string.Format(CultureInfo.InvariantCulture, text, args);
        }
    }

    /// <summary>
    /// Loads the translation tables and registers them with <see cref="TranslationServer"/>.
    /// <para>
    /// Godot's CSV importer writes <c>strings.&lt;locale&gt;.translation</c> next to the table.
    /// Those are resources, so they travel inside an exported .pck, while the raw .csv only
    /// does when the export filter lists it. Preferring the resources keeps exports localized
    /// and the CSV keeps source runs working before the editor has imported anything.
    /// </para>
    /// </summary>
    public static void Load()
    {
        foreach (Translation existing in tables.Values)
        {
            TranslationServer.RemoveTranslation(existing);
        }

        tables.Clear();

        // The CSV is the authored source of truth, so it wins when present. Exported builds
        // cannot read it: a .csv has an importer, so only the imported .translation resource
        // is packed. That resource is the fallback here.
        int entries = loadCsv();
        string source = "csv";

        if (tables.Count == 0)
        {
            entries = loadImportedTranslations();
            source = "imported .translation";
        }

        foreach (Translation table in tables.Values)
        {
            TranslationServer.AddTranslation(table);
        }

        string loaded = entries >= 0 ? $"{entries} entries" : "table";

        Logger.Log($"L10N: loaded {loaded} from {source} for [{string.Join(", ", tables.Keys)}]");

        if (tables.Count == 0)
        {
            GD.PushWarning("L10N: no translation could be loaded, UI will fall back to English");
        }
    }

    /// <summary>Loads the .translation resources produced by Godot's CSV importer.</summary>
    private static int loadImportedTranslations()
    {
        int entries = 0;

        foreach (string locale in TRANSLATED_LOCALES)
        {
            string path = $"{TABLE_DIRECTORY}/{TABLE_BASE_NAME}.{locale}.translation";

            if (!ResourceLoader.Exists(path))
            {
                continue;
            }

            var translation = ResourceLoader.Load<Translation>(path);

            if (translation == null)
            {
                continue;
            }

            translation.Locale = locale;
            tables[locale] = translation;

            // Exported builds ship an OptimizedTranslation, which deliberately drops the
            // source strings and therefore cannot report a message count.
            if (translation is OptimizedTranslation)
            {
                entries = -1;
            }
            else if (entries >= 0)
            {
                entries += translation.GetMessageCount();
            }
        }

        return entries;
    }

    /// <summary>Parses the authored CSV, used when no imported translation is available.</summary>
    private static int loadCsv()
    {
        if (!FileAccess.FileExists(TABLE_PATH))
        {
            // Expected in exported builds, where only the imported resource is packed.
            Logger.Log($"L10N: no CSV at {TABLE_PATH}, falling back to imported translations");
            return 0;
        }

        using FileAccess file = FileAccess.Open(TABLE_PATH, FileAccess.ModeFlags.Read);

        if (file == null)
        {
            GD.PushError($"L10n: could not open {TABLE_PATH} ({FileAccess.GetOpenError()})");
            return 0;
        }

        List<List<string>> rows = parseCsv(file.GetAsText());

        if (rows.Count < 2)
        {
            GD.PushWarning("L10n: translation table is empty");
            return 0;
        }

        List<string> header = rows[0];

        if (header.Count < 2)
        {
            GD.PushWarning("L10n: translation table needs at least a key and one locale column");
            return 0;
        }

        int entries = 0;

        for (int rowIndex = 1; rowIndex < rows.Count; rowIndex++)
        {
            List<string> row = rows[rowIndex];

            if (row.Count == 0)
            {
                continue;
            }

            string key = row[0];

            if (string.IsNullOrEmpty(key))
            {
                continue;
            }

            for (int column = 1; column < header.Count && column < row.Count; column++)
            {
                string locale = header[column].Trim();
                string value = row[column];

                // English is the source language, so it never needs an entry.
                if (locale.Length == 0 || locale == ENGLISH_LOCALE || string.IsNullOrEmpty(value))
                {
                    continue;
                }

                getOrCreateTable(locale).AddMessage(key, value);
                entries++;
            }
        }

        return entries;
    }

    /// <summary>Switches the active locale and applies the matching font setup.</summary>
    public static void SetLocale(string locale)
    {
        if (string.IsNullOrWhiteSpace(locale))
        {
            locale = FALLBACK_LOCALE;
        }

        if (locale == Locale && locale == TranslationServer.GetLocale())
        {
            return;
        }

        Locale = locale;
        TranslationServer.SetLocale(locale);
        applyFonts(locale);

        // Logging a couple of resolved keys is the cheapest proof that the table actually
        // reached TranslationServer, which matters most in exported builds.
        string probeMenu = TranslationServer.Translate("PLAY");
        string probeDifficulty = TranslationServer.Translate("Illogical");

        Logger.Log($"L10N: locale set to {locale} (\"PLAY\" -> \"{probeMenu}\", \"Illogical\" -> \"{probeDifficulty}\")");
    }

    /// <summary>Resolves a user facing language preference to a concrete locale code.</summary>
    public static string ResolvePreference(string preference)
    {
        return preference switch
        {
            "English" => ENGLISH_LOCALE,
            "简体中文" => "zh_CN",
            _ => DetectSystemLocale(),
        };
    }

    /// <summary>Maps the operating system locale onto a locale we ship a table for.</summary>
    public static string DetectSystemLocale()
    {
        string osLocale = OS.GetLocale().Replace('-', '_');

        if (osLocale.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
        {
            // Both zh_TW and zh_HK are covered by the Simplified table, which is far more
            // readable for Traditional readers than falling back to English.
            return "zh_CN";
        }

        return FALLBACK_LOCALE;
    }

    /// <summary>
    /// Latin-only UI fonts have no CJK glyphs, so a CJK font is registered both as a global
    /// fallback and as a per-font fallback for the fonts the UI actually uses.
    /// </summary>
    private static void applyFonts(string locale)
    {
        bool needsCjk = locale.StartsWith("zh", StringComparison.OrdinalIgnoreCase);

        if (!needsCjk)
        {
            return;
        }

        cjkFallbackFont ??= buildCjkFont();

        if (cjkFallbackFont == null)
        {
            GD.PushWarning("L10N: no CJK fallback font could be resolved");
            return;
        }

        foreach (string path in ui_font_paths)
        {
            if (!ResourceLoader.Exists(path))
            {
                continue;
            }

            var font = ResourceLoader.Load<Font>(path);

            if (font == null)
            {
                continue;
            }

            var fallbacks = new Godot.Collections.Array<Font>();

            if (font.Fallbacks != null)
            {
                foreach (Font existing in font.Fallbacks)
                {
                    fallbacks.Add(existing);
                }
            }

            bool alreadyPresent = false;

            foreach (Font existing in fallbacks)
            {
                if (existing == cjkFallbackFont)
                {
                    alreadyPresent = true;
                    break;
                }
            }

            if (!alreadyPresent)
            {
                // Fallbacks are consulted in order, so the CJK font goes first.
                fallbacks.Insert(0, cjkFallbackFont);
            }

            font.Fallbacks = fallbacks;
        }
    }

    private static Font buildCjkFont()
    {
        var font = new SystemFont
        {
            FontNames =
            [
                "Microsoft YaHei UI",
                "Microsoft YaHei",
                "Noto Sans CJK SC",
                "Noto Sans SC",
                "Source Han Sans SC",
                "PingFang SC",
                "WenQuanYi Micro Hei",
                "SimHei",
                "sans-serif",
            ],
            AllowSystemFallback = true,
        };

        return font;
    }

    private static Translation getOrCreateTable(string locale)
    {
        if (tables.TryGetValue(locale, out Translation table))
        {
            return table;
        }

        table = new Translation { Locale = locale };
        tables[locale] = table;

        return table;
    }

    /// <summary>
    /// Minimal RFC-4180 style CSV reader: supports quoted fields, embedded commas, embedded
    /// newlines and doubled quotes. A UTF-8 BOM is stripped from the first cell.
    /// </summary>
    private static List<List<string>> parseCsv(string content)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var cell = new StringBuilder();

        if (content.Length > 0 && content[0] == '\uFEFF')
        {
            content = content[1..];
        }

        bool inQuotes = false;

        for (int i = 0; i < content.Length; i++)
        {
            char c = content[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < content.Length && content[i + 1] == '"')
                    {
                        cell.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    cell.Append(c);
                }

                continue;
            }

            switch (c)
            {
                case '"':
                    inQuotes = true;
                    break;
                case ',':
                    row.Add(cell.ToString());
                    cell.Clear();
                    break;
                case '\r':
                    break;
                case '\n':
                    row.Add(cell.ToString());
                    cell.Clear();
                    rows.Add(row);
                    row = [];
                    break;
                default:
                    cell.Append(c);
                    break;
            }
        }

        if (cell.Length > 0 || row.Count > 0)
        {
            row.Add(cell.ToString());
            rows.Add(row);
        }

        return rows;
    }
}
