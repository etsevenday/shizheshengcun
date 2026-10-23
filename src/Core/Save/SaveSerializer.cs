using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Save
{
    public sealed class SaveVersionException : Exception
    {
        public string Found { get; }

        public SaveVersionException(string found)
            : base($"存档版本 {found} 与当前版本 {GameState.CurrentSchema} 不兼容")
        {
            Found = found;
        }
    }

    /// <summary>单槽存档的序列化。读档不重掷随机、不重扣材料、不重发里程碑——这些都已在状态里。</summary>
    public static class SaveSerializer
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Formatting = Formatting.None,
            NullValueHandling = NullValueHandling.Include,
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            Converters = { new StringEnumConverter() },
        };

        public static string Serialize(GameState s) => JsonConvert.SerializeObject(s, Settings);

        /// <summary>
        /// 读档。当前版本直接读取；旧版 0.2-proto-1 需提供配置以迁移到当前版本（迁移前由调用方备份原文）；其他版本拒绝。
        /// </summary>
        public static GameState Deserialize(string json, GameConfig? config = null)
        {
            var version = SchemaOf(json);
            if (version == SaveMigrator.LegacySchema && config != null)
            {
                var old = JsonConvert.DeserializeObject<GameState>(json, Settings)
                          ?? throw new SaveVersionException("损坏");
                SaveMigrator.Migrate(old, config);
                return old;
            }
            if (version != GameState.CurrentSchema)
                throw new SaveVersionException(version ?? "未知");
            var s = JsonConvert.DeserializeObject<GameState>(json, Settings)
                    ?? throw new SaveVersionException("损坏");
            return s;
        }

        /// <summary>存档是旧版、读取时会迁移。</summary>
        public static bool NeedsMigration(string json) => SchemaOf(json) == SaveMigrator.LegacySchema;

        private static string? SchemaOf(string json)
        {
            var probe = JsonConvert.DeserializeObject<SchemaProbe>(json)
                        ?? throw new SaveVersionException("空");
            return probe.SchemaVersion;
        }

        private sealed class SchemaProbe
        {
            public string? SchemaVersion { get; set; }
        }
    }
}
