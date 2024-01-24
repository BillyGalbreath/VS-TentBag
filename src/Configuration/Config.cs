using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using Vintagestory.API.Config;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TentBag.Configuration;

[SuppressMessage("ReSharper", "ConvertToConstant.Global")]
[SuppressMessage("ReSharper", "FieldCanBeMadeReadOnly.Global")]
public sealed class Config {
    [YamlMember] public int Radius = 3;
    [YamlMember] public int Height = 7;
    [YamlMember] public float BuildEffort = 100F;
    [YamlMember] public bool RequireFloor = false;
    [YamlMember] public bool ReplacePlantsAndRocks = true;
    [YamlMember] public bool PutTentInInventoryOnUse = true;

    private static string ConfigFile => Path.Combine(GamePaths.ModConfig, $"{TentBag.Instance.Mod.Info.ModID}.yml");

    private static FileWatcher? _watcher;
    private static Config? _config;

    public static Config GetConfig() {
        return _config ??= Reload();
    }

    public static Config Reload() {
        TentBag.Instance.Mod.Logger.Event($"Loading config from {ConfigFile}");

        _config = Write(Read());
        
        _watcher ??= new FileWatcher();

        return _config;
    }

    private static Config Read() {
        try {
            return new DeserializerBuilder()
                .IgnoreUnmatchedProperties()
                .WithNamingConvention(NullNamingConvention.Instance)
                .Build().Deserialize<Config>(File.ReadAllText(ConfigFile));
        } catch (Exception) {
            return new Config();
        }
    }

    private static Config Write(Config data) {
        GamePaths.EnsurePathExists(GamePaths.ModConfig);
        File.WriteAllText(ConfigFile,
            new SerializerBuilder()
                .WithQuotingNecessaryStrings()
                .WithNamingConvention(NullNamingConvention.Instance)
                .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
                .Build().Serialize(data)
            , Encoding.UTF8);
        return data;
    }

    public static void Dispose() {
        _watcher?.Dispose();
        _watcher = null;
    }
}
