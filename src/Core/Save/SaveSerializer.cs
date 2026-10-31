using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
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
        /// 读档。当前版本直接读取；旧版 0.2-proto-1 与 3 需提供配置以迁移到当前版本（迁移前由调用方备份原文）；其他版本拒绝。
        /// 迁移冲突时抛 SaveMigrationConflictException，调用方不得覆盖原档。
        /// </summary>
        public static GameState Deserialize(string json, GameConfig? config = null) => Deserialize(json, config, out _);

        public static GameState Deserialize(string json, GameConfig? config, out List<string> migrationNotes)
        {
            migrationNotes = new List<string>();
            var version = SchemaOf(json);
            if (IsMigratable(version) && config != null)
            {
                var obj = JObject.Parse(json);
                var legacy = PreprocessLegacy(obj);
                var old = obj.ToObject<GameState>(JsonSerializer.Create(Settings))
                          ?? throw new SaveVersionException("损坏");
                old.LegacyEquippedWeaponId = legacy.EquippedWeaponId;
                migrationNotes = SaveMigrator.Migrate(old, config);
                return old;
            }
            if (version != GameState.CurrentSchema)
                throw new SaveVersionException(version ?? "未知");
            var s = JsonConvert.DeserializeObject<GameState>(json, Settings)
                    ?? throw new SaveVersionException("损坏");
            return s;
        }

        private static bool IsMigratable(string? version) =>
            version == SaveMigrator.LegacySchema || version == SaveMigrator.Schema3 || version == SaveMigrator.Schema4 || version == SaveMigrator.Schema5
            || version == SaveMigrator.Schema6 || version == SaveMigrator.Schema7 || version == SaveMigrator.Schema8 || version == SaveMigrator.Schema9 || version == SaveMigrator.Schema10 || version == SaveMigrator.Schema11 || version == SaveMigrator.Schema12 || version == SaveMigrator.Schema13 || version == SaveMigrator.Schema14 || version == SaveMigrator.Schema15 || version == SaveMigrator.Schema16 || version == SaveMigrator.Schema17;

        /// <summary>schema 9 前在顶层的主角个人字段（R08 §5“旧档主角创建 actorId 一次”）。</summary>
        private static readonly string[] LegacyActorFields =
            { "Stats", "Body", "Skills", "DiarrheaUntil", "BandageReadyAt", "AlertLatched", "ZeroLatched" };

        /// <summary>预处理读出的、schema 9 已删除的旧字段（只供迁移）。</summary>
        public sealed class LegacyFields
        {
            public int? EquippedWeaponId { get; set; }
        }

        /// <summary>
        /// 旧档（schema &lt; 9）的 JSON 预处理（开发计划 v0.9 B3 H7）：把顶层主角个人字段原样搬进 Actors.p0、Party=[p0]，再交给原有迁移链；
        /// 取出已删除的 EquippedWeaponId 供 8→9 迁到武器槽 1。已有 Actors 的不再搬（幂等）；缺的字段保持默认值，由各级迁移按原规则补。
        /// </summary>
        public static LegacyFields PreprocessLegacy(JObject obj)
        {
            var legacy = new LegacyFields();
            if (obj["EquippedWeaponId"] is JToken eq && eq.Type == JTokenType.Integer) legacy.EquippedWeaponId = eq.Value<int>();
            obj.Remove("EquippedWeaponId");
            PreprocessBattleFighters(obj);
            if (obj["Actors"] is JObject) return legacy;
            var p0 = new JObject { ["Id"] = GameState.PlayerId };
            foreach (var f in LegacyActorFields)
            {
                if (obj[f] is JToken v) p0[f] = v.DeepClone();
                obj.Remove(f);
            }
            obj["Actors"] = new JObject { [GameState.PlayerId] = p0 };
            obj["Party"] = new JArray(GameState.PlayerId);
            return legacy;
        }

        /// <summary>
        /// schema ≤ 14 进行中战斗的主角战斗字段（PlayerAp、Panic、格挡、冷却、受惊等，BattleState.LegacyFighterFields）原样搬进
        /// Battle.Fighters.p0（开发计划 v0.9 B8 M186）。已有 Fighters 的不再搬（幂等）。
        /// </summary>
        public static void PreprocessBattleFighters(JObject obj)
        {
            if (!(obj["Battle"] is JObject b) || b["Fighters"] is JObject) return;
            var me = new JObject();
            foreach (var (old, now) in BattleState.LegacyFighterFields)
            {
                if (b[old] is JToken v) me[now] = v.DeepClone();
                b.Remove(old);
            }
            b["Fighters"] = new JObject { [GameState.PlayerId] = me };
        }

        /// <summary>存档是旧版、读取时会迁移。</summary>
        public static bool NeedsMigration(string json) => IsMigratable(SchemaOf(json));

        /// <summary>存档的结构版本号（开始界面预告升级用）。</summary>
        public static string? SchemaVersionOf(string json) => SchemaOf(json);

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
