using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CustomCodeSystem;

public sealed class ConfigFileDto
{
    public string Info { get; set; } = string.Empty;

    // Для быстрого доступа по имени секции
    public Dictionary<string, DataSectionDto> DataSections { get; set; }
        = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class DataSectionDto
{
    public string Name { get; set; } = string.Empty;

    public Dictionary<string, string> Values { get; set; }
        = new(StringComparer.OrdinalIgnoreCase);
}

public static class ConfigTxtParser
{
    private static ConfigFileDto? _config;

    public static bool Load(string filePath, out string errorMsg)
    {
        errorMsg = string.Empty;

        var success = Parse(filePath, out var config, out errorMsg);
        if (!success || config == null)
        {
            _config = null;
            return false;
        }

        _config = config;
        return true;
    }

    public static bool Parse(string filePath, out ConfigFileDto? config, out string errorMsg)
    {
        config = null;
        errorMsg = string.Empty;

        try
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                errorMsg = "filePath is empty.";
                return false;
            }

            if (!File.Exists(filePath))
            {
                errorMsg = $"File not found: {filePath}";
                return false;
            }

            var lines = File.ReadAllLines(filePath, Encoding.UTF8);
            return ParseLines(lines, out config, out errorMsg);
        }
        catch (Exception ex)
        {
            errorMsg = $"Parse error: {ex.Message}";
            config = null;
            return false;
        }
    }

    public static bool ParseLines(IEnumerable<string> lines, out ConfigFileDto? config, out string errorMsg)
    {
        config = null;
        errorMsg = string.Empty;

        try
        {
            if (lines == null)
            {
                errorMsg = "lines is null.";
                return false;
            }

            var result = new ConfigFileDto();

            var infoLines = new List<string>();

            string currentRootSection = string.Empty;   // INFO / DATA
            DataSectionDto? currentDataSection = null;

            foreach (var rawLine in lines)
            {
                if (rawLine == null)
                    continue;

                var line = rawLine.Trim();

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                // Комментарии
                if (line.StartsWith(";") || line.StartsWith("#"))
                    continue;

                // [SECTION]
                if (IsSection(line))
                {
                    var sectionName = GetSectionName(line);

                    if (string.Equals(sectionName, "INFO", StringComparison.OrdinalIgnoreCase))
                    {
                        currentRootSection = "INFO";
                        currentDataSection = null;
                        continue;
                    }

                    if (string.Equals(sectionName, "DATA", StringComparison.OrdinalIgnoreCase))
                    {
                        currentRootSection = "DATA";
                        currentDataSection = null;
                        continue;
                    }

                    // Вложенная секция допустима только внутри [DATA]
                    if (string.Equals(currentRootSection, "DATA", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!result.DataSections.TryGetValue(sectionName, out currentDataSection))
                        {
                            currentDataSection = new DataSectionDto
                            {
                                Name = sectionName
                            };

                            result.DataSections[sectionName] = currentDataSection;
                        }

                        continue;
                    }

                    // Неизвестная секция вне [DATA] / [INFO]
                    currentDataSection = null;
                    currentRootSection = string.Empty;
                    continue;
                }

                // Текст внутри [INFO]
                if (string.Equals(currentRootSection, "INFO", StringComparison.OrdinalIgnoreCase))
                {
                    infoLines.Add(line);
                    continue;
                }

                // key=value внутри [DATA] -> [ALA440]
                if (string.Equals(currentRootSection, "DATA", StringComparison.OrdinalIgnoreCase) &&
                    currentDataSection != null)
                {
                    var eqIndex = line.IndexOf('=');
                    if (eqIndex <= 0)
                        continue;

                    var key = line.Substring(0, eqIndex).Trim();
                    var value = line.Substring(eqIndex + 1).Trim();

                    if (string.IsNullOrWhiteSpace(key))
                        continue;

                    currentDataSection.Values[key] = value;
                }
            }

            result.Info = string.Join(Environment.NewLine, infoLines);

            config = result;
            return true;
        }
        catch (Exception ex)
        {
            errorMsg = $"ParseLines error: {ex.Message}";
            config = null;
            return false;
        }
    }

    public static bool TryGetValue(string sectionName, string key, out string value, out string errorMsg)
    {
        value = string.Empty;
        errorMsg = string.Empty;

        try
        {
            if (_config == null)
            {
                errorMsg = "Config is not loaded. Call Load(filePath, out errorMsg) first.";
                return false;
            }

            return TryGetValue(_config, sectionName, key, out value, out errorMsg);
        }
        catch (Exception ex)
        {
            errorMsg = $"TryGetValue error: {ex.Message}";
            value = string.Empty;
            return false;
        }
    }

    public static bool TryGetValue(
        ConfigFileDto? config,
        string sectionName,
        string key,
        out string value,
        out string errorMsg)
    {
        value = string.Empty;
        errorMsg = string.Empty;

        try
        {
            if (config == null)
            {
                errorMsg = "config is null.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(sectionName))
            {
                errorMsg = "sectionName is empty.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(key))
            {
                errorMsg = "key is empty.";
                return false;
            }

            if (!config.DataSections.TryGetValue(sectionName, out var section))
            {
                errorMsg = $"Section '{sectionName}' not found.";
                return false;
            }

            if (!section.Values.TryGetValue(key, out var foundValue))
            {
                errorMsg = $"Key '{key}' not found in section '{sectionName}'.";
                return false;
            }

            value = foundValue;
            return true;
        }
        catch (Exception ex)
        {
            errorMsg = $"TryGetValue error: {ex.Message}";
            value = string.Empty;
            return false;
        }
    }

    public static bool TryGetSection(string sectionName, out DataSectionDto? section, out string errorMsg)
    {
        section = null;
        errorMsg = string.Empty;

        try
        {
            if (_config == null)
            {
                errorMsg = "Config is not loaded. Call Load(filePath, out errorMsg) first.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(sectionName))
            {
                errorMsg = "sectionName is empty.";
                return false;
            }

            if (!_config.DataSections.TryGetValue(sectionName, out section))
            {
                errorMsg = $"Section '{sectionName}' not found.";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            errorMsg = $"TryGetSection error: {ex.Message}";
            section = null;
            return false;
        }
    }

    public static bool TryGetLoadedConfig(out ConfigFileDto? config, out string errorMsg)
    {
        config = null;
        errorMsg = string.Empty;

        try
        {
            if (_config == null)
            {
                errorMsg = "Config is not loaded.";
                return false;
            }

            config = _config;
            return true;
        }
        catch (Exception ex)
        {
            errorMsg = $"TryGetLoadedConfig error: {ex.Message}";
            config = null;
            return false;
        }
    }

    public static void Clear()
    {
        _config = null;
    }

    private static bool IsSection(string line)
    {
        return !string.IsNullOrWhiteSpace(line)
               && line.StartsWith("[")
               && line.EndsWith("]")
               && line.Length >= 3;
    }

    private static string GetSectionName(string line)
    {
        return line.Substring(1, line.Length - 2).Trim();
    }
}