using System;
using System.Collections.Generic;
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
    public const string EnglishLocale = "en";
    public const string FallbackLocale = "en";
    public const string TablePath = "res://translations/strings.csv";

    /// <summary>Locales that ship with a translation table.</summary>
    public static readonly string[] TranslatedLocales = ["zh_CN"];

    private static readonly Dictionary<string, Translation> tables = new(StringComparer.Ordinal);

    /// <summary>Fonts actually referenced by the UI, patched with a CJK fallback at runtime.</summary>
    private static readonly string[] UiFontPaths =
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
    public static string Locale { get; private set; } = FallbackLocale;

    /// <summary>True once the translation table has been read and registered.</summary>
    public static bool Loaded => tables.Count > 0;

    /// <summary>Raised after <see cref="SetLocale"/> changed the active locale.</summary>
    public static event Action<string> LocaleChanged;

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
    /// Translates a format string and applies <see cref="string.Format(string, object[])"/>.
    /// Keys use numbered placeholders, for example <c>"Initializing maps ({0}/{1})"</c>.
    /// </summary>
    public static string Tr(string text, params object[] args)
    {
        string translated = Tr(text);

        if (args == null || args.Length == 0)
        {
            return translated;
        }

        try
        {
            return string.Format(translated, args);
        }
        catch (FormatException)
        {
            // A malformed translation must never break the UI: fall back to the English source.
            return string.Format(text, args);
        }
    }

    /// <summary>Reads the CSV table and registers every locale it contains.</summary>
    public static void Load()
    {
        foreach (Translation existing in tables.Values)
        {
            TranslationServer.RemoveTranslation(existing);
        }

        tables.Clear();

        if (!FileAccess.FileExists(TablePath))
        {
            GD.PushWarning($"L10n: translation table not found at {TablePath}");
            return;
        }

        using FileAccess file = FileAccess.Open(TablePath, FileAccess.ModeFlags.Read);

        if (file == null)
        {
            GD.PushError($"L10n: could not open {TablePath} ({FileAccess.GetOpenError()})");
            return;
        }

        List<List<string>> rows = ParseCsv(file.GetAsText());

        if (rows.Count < 2)
        {
            GD.PushWarning("L10n: translation table is empty");
            return;
        }

        List<string> header = rows[0];

        if (header.Count < 2)
        {
            GD.PushWarning("L10n: translation table needs at least a key and one locale column");
            return;
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
                if (locale.Length == 0 || locale == EnglishLocale || string.IsNullOrEmpty(value))
                {
                    continue;
                }

                GetOrCreateTable(locale).AddMessage(key, value);
                entries++;
            }
        }

        foreach (Translation table in tables.Values)
        {
            TranslationServer.AddTranslation(table);
        }

        Logger.Log($"L10N: loaded {entries} entries for [{string.Join(", ", tables.Keys)}]");
    }

    /// <summary>Switches the active locale and applies the matching font setup.</summary>
    public static void SetLocale(string locale)
    {
        if (string.IsNullOrWhiteSpace(locale))
        {
            locale = FallbackLocale;
        }

        if (locale == Locale && locale == TranslationServer.GetLocale())
        {
            return;
        }

        Locale = locale;
        TranslationServer.SetLocale(locale);
        ApplyFonts(locale);

        Logger.Log($"L10N: locale set to {locale}");
        LocaleChanged?.Invoke(locale);
    }

    /// <summary>Resolves a user facing language preference to a concrete locale code.</summary>
    public static string ResolvePreference(string preference)
    {
        return preference switch
        {
            "English" => EnglishLocale,
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

        return FallbackLocale;
    }

    /// <summary>
    /// Latin-only UI fonts have no CJK glyphs, so a CJK font is registered both as a global
    /// fallback and as a per-font fallback for the fonts the UI actually uses.
    /// </summary>
    private static void ApplyFonts(string locale)
    {
        bool needsCjk = locale.StartsWith("zh", StringComparison.OrdinalIgnoreCase);

        if (!needsCjk)
        {
            return;
        }

        cjkFallbackFont ??= BuildCjkFont();

        if (cjkFallbackFont == null)
        {
            GD.PushWarning("L10N: no CJK fallback font could be resolved");
            return;
        }

        foreach (string path in UiFontPaths)
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

    private static Font BuildCjkFont()
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

    private static Translation GetOrCreateTable(string locale)
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
    private static List<List<string>> ParseCsv(string content)
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
