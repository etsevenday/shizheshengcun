using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
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

        public static GameState Deserialize(string json)
        {
            var probe = JsonConvert.DeserializeObject<SchemaProbe>(json)
                        ?? throw new SaveVersionException("空");
            if (probe.SchemaVersion != GameState.CurrentSchema)
                throw new SaveVersionException(probe.SchemaVersion ?? "未知");
            var s = JsonConvert.DeserializeObject<GameState>(json, Settings)
                    ?? throw new SaveVersionException("损坏");
            return s;
        }

        private sealed class SchemaProbe
        {
            public string? SchemaVersion { get; set; }
        }
    }
}
