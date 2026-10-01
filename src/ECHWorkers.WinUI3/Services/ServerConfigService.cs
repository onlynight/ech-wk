using System.IO;
using ECHWorkers.WinUI3.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ECHWorkers.WinUI3.Services;

/// <summary>YAML 序列化 DTO，字段与 gui.py 服务器配置结构一致。</summary>
public class ServerConfigEntry
{
    [YamlMember(Alias = "id")] public string Id { get; set; } = "";
    [YamlMember(Alias = "name")] public string Name { get; set; } = "";
    [YamlMember(Alias = "server")] public string Server { get; set; } = "";
    [YamlMember(Alias = "listen")] public string Listen { get; set; } = "";
    [YamlMember(Alias = "token")] public string Token { get; set; } = "";
    [YamlMember(Alias = "ip")] public string Ip { get; set; } = "";
    [YamlMember(Alias = "dns")] public string Dns { get; set; } = "";
    [YamlMember(Alias = "ech")] public string Ech { get; set; } = "";
    [YamlMember(Alias = "routing_mode")] public string Routing { get; set; } = "";
}

/// <summary>YAML 配置文件根结构。</summary>
public class ConfigFile
{
    [YamlMember(Alias = "servers")]
    public List<ServerConfigEntry> Servers { get; set; } = new();

    [YamlMember(Alias = "current_server_id")]
    public string CurrentServerId { get; set; } = "";
}

/// <summary>
/// YAML 配置文件持久化服务。
/// 路径：%APPDATA%\ECHWorkersClient\config.yml
/// </summary>
public static class ServerConfigService
{
    private static readonly string ConfigDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ECHWorkersClient");

    private static readonly string ConfigPath =
        Path.Combine(ConfigDir, "config.yml");

    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    private static readonly ISerializer Serializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    public static string ConfigFilePath => ConfigPath;

    public static void Load()
    {
        try
        {
            if (!File.Exists(ConfigPath))
            {
                Directory.CreateDirectory(ConfigDir);
                ServerStore.Servers.Clear();
                return;
            }

            var yaml = File.ReadAllText(ConfigPath);
            var config = Deserializer.Deserialize<ConfigFile>(yaml);

            ServerStore.Servers.Clear();
            if (config?.Servers != null)
            {
                foreach (var entry in config.Servers)
                {
                    ServerStore.Servers.Add(EntryToProfile(entry));
                }
            }
            ServerStore.CurrentServerId = config?.CurrentServerId ?? "";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Config] 加载配置失败: {ex.Message}");
            ServerStore.Servers.Clear();
        }
    }

    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);

            var config = new ConfigFile
            {
                Servers = ServerStore.Servers.Select(p => ProfileToEntry(p)).ToList(),
                CurrentServerId = ServerStore.CurrentServerId,
            };

            var yaml = Serializer.Serialize(config);
            var tmpPath = ConfigPath + ".tmp";
            File.WriteAllText(tmpPath, yaml);

            if (File.Exists(ConfigPath)) File.Delete(ConfigPath);
            File.Move(tmpPath, ConfigPath);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Config] 保存配置失败: {ex.Message}");
        }
    }

    private static ServerProfile EntryToProfile(ServerConfigEntry entry) => new()
    {
        Id = entry.Id,
        Name = entry.Name,
        Server = entry.Server,
        Listen = entry.Listen,
        Token = entry.Token,
        Ip = entry.Ip,
        Dns = entry.Dns,
        Ech = entry.Ech,
        Routing = entry.Routing,
    };

    private static ServerConfigEntry ProfileToEntry(ServerProfile p) => new()
    {
        Id = string.IsNullOrEmpty(p.Id) ? Guid.NewGuid().ToString() : p.Id,
        Name = p.Name,
        Server = p.Server,
        Listen = p.Listen,
        Token = p.Token,
        Ip = p.Ip,
        Dns = p.Dns,
        Ech = p.Ech,
        Routing = p.Routing,
    };
}
