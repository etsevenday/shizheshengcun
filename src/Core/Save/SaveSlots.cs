using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Survival.Core.Save
{
    /// <summary>存档槽类型（S01「2026-11-10 Q335 多存档定稿」）。Legacy 只用于旧单槽导入失败时的「旧版存档」卡（Q383）。</summary>
    public enum SaveSlotKind { Auto5, Daily, Manual, Legacy }

    /// <summary>
    /// 浏览器存储的键值接口（网页壳用 localStorage 实现，测试用内存实现）。每个键的写入是原子替换：要么旧值、要么新值。
    /// </summary>
    public interface ISaveStorage
    {
        /// <summary>读取；不存在返回 null。浏览器读不出时抛 <see cref="SaveStorageReadException"/>。</summary>
        string? Get(string key);

        /// <summary>写入；失败抛 <see cref="SaveStorageWriteException"/>（带浏览器异常类别名，Q282 据此识别配额用尽）。</summary>
        void Set(string key, string value);

        void Remove(string key);
    }

    /// <summary>存储写入失败：Category 为浏览器异常类别名（如 QuotaExceededError），Message 为浏览器原始错误文本。</summary>
    public class SaveStorageWriteException : Exception
    {
        public string Category { get; }

        public SaveStorageWriteException(string category, string message) : base(message) => Category = category ?? "";
    }

    /// <summary>存储读取失败（尚未进入解析）。</summary>
    public class SaveStorageReadException : Exception
    {
        public SaveStorageReadException(string message) : base(message) { }
    }

    /// <summary>多存档的键、数量与定稿文字。文字全部来自 S01 Q335、Q381—Q389、Q403—Q411 原句。</summary>
    public static class SaveSlots
    {
        public const string Prefix = "survival.save.v2.";
        public const string Auto5Key = Prefix + "auto5";
        public const string DailyKey = Prefix + "daily";
        public const string ManualPrefix = Prefix + "manual.";
        /// <summary>Q381：最近一次成功进入本局的页面令牌。</summary>
        public const string OwnerKey = Prefix + "owner";
        /// <summary>Q383：已完成导入的旧单槽原文校验和（重复打开不重导）。</summary>
        public const string LegacyImportedKey = Prefix + "legacyImported";
        /// <summary>Q410：导入档尚未消耗的一次性升级提示（值为对应每5分钟槽的 Rev）。</summary>
        public const string UpgradeNoticeKey = Prefix + "upgradeNotice";

        /// <summary>旧单槽主键与备份（B40 前的单槽存档）。</summary>
        public const string LegacyKey = "survival.save.v1";
        public const string LegacyBackupKey = "survival.save.v1.bak";

        /// <summary>Q335：手动存档全浏览器最多 10 个。</summary>
        public const int ManualCap = 10;
        /// <summary>Q335：每5分钟自动存档的现实间隔 300 秒。</summary>
        public const long AutoIntervalMs = 300_000;
        /// <summary>Q335：每日自动存档在局内 06:00。</summary>
        public const int DailyMinuteOfDay = 6 * 60;

        public static string ManualKey(int index) => ManualPrefix + index.ToString(CultureInfo.InvariantCulture);

        public static IEnumerable<string> ManualKeys()
        {
            for (var i = 0; i < ManualCap; i++) yield return ManualKey(i);
        }

        /// <summary>全部多存档槽键（每5分钟、每日06:00、10 个手动）。</summary>
        public static IEnumerable<string> AllSlotKeys()
        {
            yield return Auto5Key;
            yield return DailyKey;
            foreach (var k in ManualKeys()) yield return k;
        }

        public static SaveSlotKind KindOf(string key) =>
            key == Auto5Key ? SaveSlotKind.Auto5
            : key == DailyKey ? SaveSlotKind.Daily
            : key == LegacyKey ? SaveSlotKind.Legacy
            : SaveSlotKind.Manual;

        // ───────────── 定稿文字 ─────────────

        public const string Auto5Label = "自动存档 · 每5分钟";
        public const string DailyLabel = "自动存档 · 每日06:00";
        public const string ManualLabel = "手动存档";
        /// <summary>Q383：旧单槽导入失败的卡类型。</summary>
        public const string LegacyLabel = "旧版存档";

        public static string Label(SaveSlotKind k) => k switch
        {
            SaveSlotKind.Auto5 => Auto5Label,
            SaveSlotKind.Daily => DailyLabel,
            SaveSlotKind.Manual => ManualLabel,
            _ => LegacyLabel,
        };

        /// <summary>Q384 选档页分区标题。</summary>
        public const string AutoSection = "自动存档";
        public const string ManualSection = "手动存档";
        /// <summary>Q335 标题页入口与选档页。</summary>
        public const string ContinueButton = "继续游戏";
        public const string PickerTitle = "存档选择";
        public const string LoadButton = "读取存档";
        public const string BackButton = "返回";
        /// <summary>Q335 空态（没有可读档时）。</summary>
        public const string EmptyText = "还没有可读取的存档。";
        /// <summary>Q335 已结束档附注。</summary>
        public const string EndedText = "本局已结束";
        /// <summary>Q335 旧档无可靠现实保存时间、Q387 已有快照的时刻缺失。</summary>
        public const string TimeNotRecorded = "保存时间未记录";
        /// <summary>Q387 菜单里未写入的槽。</summary>
        public const string NotSavedYet = "尚未保存";
        /// <summary>Q355 / Q387 菜单存档位置句。</summary>
        public const string StorageWhere = "存档保存在当前浏览器。";
        /// <summary>Q387 菜单三行的前缀。</summary>
        public const string MenuAuto5Prefix = "每5分钟：";
        public const string MenuDailyPrefix = "每日06:00：";
        public const string MenuManualPrefix = "手动：";

        /// <summary>
        /// Q387 / Q413 菜单三行的时刻值：未写入「尚未保存」；已有快照缺时刻「保存时间未记录」；
        /// 浏览器读不出这个槽（不知道有没有档）也写「尚未保存」（Q413 选 B；选档页仍按 Q403 显示真实读取失败原因）。
        /// </summary>
        public static string MenuTimeText(SlotTime t, Func<long, string> formatLocal)
        {
            if (!t.Exists) return NotSavedYet;
            return t.SavedAtMs is long ms ? formatLocal(ms) : TimeNotRecorded;
        }

        /// <summary>Q384 / Q403 不可读档原因前缀。</summary>
        public const string UnreadablePrefix = "这份存档无法读取：";
        /// <summary>Q383 / Q403 旧单槽导入失败原因前缀。</summary>
        public const string LegacyFailPrefix = "这份旧存档无法升级：";

        /// <summary>Q335 新游戏确认。</summary>
        public const string NewGameConfirmText = "开始新游戏会覆盖每5分钟自动存档。每日自动存档会在新局下次跨过06:00时更新，手动存档会保留。";
        public const string NewGameConfirmButton = "开始新游戏";
        /// <summary>Q386 局中入口。</summary>
        public const string RestartMenuButton = "重新开始…";

        /// <summary>Q385 / Q405 手动存档子页。</summary>
        public const string ManualPageTitle = "手动存档";
        public const string ManualNewButton = "新建手动存档";
        public const string ManualFullText = "手动存档已满。请选择一份覆盖。";
        public const string OverwriteConfirmTitle = "覆盖这份手动存档？";
        public const string OverwriteConfirmButton = "确认覆盖";
        public const string CancelButton = "取消";
        public const string ManualSavedText = "已保存到手动存档。";

        /// <summary>Q381 写权限。</summary>
        public const string PermissionLostText = "另一个游戏页面正在保存。";
        public const string ReclaimButton = "在此页面继续保存";
        public const string ReclaimRefusedText = "另一个页面已保存了更新的进度。请返回标题重新读取。";

        /// <summary>Q406 底栏成功（{时:分} 由网页壳按浏览器本地格式填）。</summary>
        public const string SavedPrefix = "已保存 ";
        public const string RetryButton = "重试";

        /// <summary>Q388 系统说明「保存与继续」三段（逐字）。</summary>
        public static readonly string[] HelpParagraphs =
        {
            "游戏每隔 5 个现实分钟更新一次「每5分钟」自动存档。游戏内时间经过每天 06:00 时，更新「每日06:00」自动存档。安全空闲时可以从游戏菜单打开「手动存档」，最多保存 10 份。",
            "关闭或刷新页面会丢失尚未保存的进度。想保留当前进度，请在可以保存时选择「保存并返回标题」。保存失败时，可按提示重试。",
            "在标题页选择「继续游戏」，再选一份存档。行动、战斗和对白会恢复到这份存档记录的状态。开始新游戏会覆盖「每5分钟」自动存档，保留手动存档。「每日06:00」自动存档会等新局下次经过 06:00 时更新。",
        };

        /// <summary>Q388 战斗菜单说明句。</summary>
        public const string BattleMenuNote = "要离开战斗，请在指令条选择「撤退」。选择「保存并返回标题」会保留当前战斗进度，下次读取这份存档时可以继续战斗。";
        /// <summary>Q388 撤出中的游戏菜单说明句。</summary>
        public const string ExtractMenuNote = "正在撤出，将一直走到外部入口，途中无法取消。选择「保存并返回标题」后，读取这份存档时会从剩余路程接着走。";

        /// <summary>Q410 导入档首次读取的升级提示。</summary>
        public static string UpgradeNotice(string currentSchema) => $"旧存档已升级到版本{currentSchema}。";

        /// <summary>Q335 卡面游戏内时刻「第{世界日}日 · {时刻}」。</summary>
        public static string DayTimeText(int minute)
        {
            var day = minute / 1440 + 1;
            var m = ((minute % 1440) + 1440) % 1440;
            return $"第{day}日 · {m / 60:00}:{m % 60:00}";
        }

        /// <summary>
        /// 写入失败句（Q282、Q387、Q406）：前标目标槽，配额用尽为定稿整句，其他错误沿现有“保存失败：”加浏览器原文。
        /// </summary>
        public static string FailureText(SaveSlotKind kind, bool quota, string? message) =>
            Label(kind) + "：" + SaveWriteError.PlayerText(quota, message);

        // ───────────── 校验和 ─────────────

        /// <summary>局内快照文本的 FNV-1a 64 位校验和（十六进制），用于完整性校验。</summary>
        public static string Checksum(string text)
        {
            unchecked
            {
                ulong h = 14695981039346656037UL;
                foreach (var ch in text)
                {
                    h ^= (byte)(ch & 0xFF);
                    h *= 1099511628211UL;
                    h ^= (byte)(ch >> 8);
                    h *= 1099511628211UL;
                }
                return h.ToString("x16", CultureInfo.InvariantCulture);
            }
        }
    }

    /// <summary>存档卡摘要（Q335 / Q404 / Q407）：游戏内绝对分钟、当前实际地点名、是否已结束。缺失的字段为 null。</summary>
    public sealed class SaveSlotSummary
    {
        public int? Minute { get; set; }
        public string? Location { get; set; }
        public bool? Ended { get; set; }
    }

    /// <summary>
    /// 一个槽的存储内容：头部字段在前、局内快照（State，完整 GameState JSON）在最后，读列表时只解析头部。
    /// </summary>
    public sealed class SaveSlotEnvelope
    {
        public const string CurrentFormat = "slot1";

        public string Format { get; set; } = CurrentFormat;
        public string Slot { get; set; } = "";
        /// <summary>本次写入的唯一版本号（Q381 / Q385 目标槽版本校验用）。</summary>
        public string Rev { get; set; } = "";
        /// <summary>现实保存时刻（UTC 毫秒）；旧档导入等无可靠时间时为 null（显示「保存时间未记录」）。</summary>
        public long? SavedAtMs { get; set; }
        /// <summary>快照的存档结构版本（与 State 内一致，列表判断版本过新时用）。</summary>
        public string? Schema { get; set; }
        /// <summary>从旧单槽导入时的原版本（Q410 一次性提示判断用）；其他写入为 null。</summary>
        public string? LegacyFrom { get; set; }
        /// <summary>从旧单槽导入时旧原文的校验和（Q383 重复打开不重导）。</summary>
        public string? LegacyChecksum { get; set; }
        public SaveSlotSummary? Summary { get; set; }
        /// <summary>State 文本的校验和。</summary>
        public string Checksum { get; set; } = "";
        public string State { get; set; } = "";

        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Formatting = Formatting.None,
            NullValueHandling = NullValueHandling.Ignore,
        };

        public string ToJson() => JsonConvert.SerializeObject(this, Settings);

        /// <summary>完整解析（含 State）；不是合法 JSON 对象时抛异常。</summary>
        public static SaveSlotEnvelope Parse(string json) =>
            JsonConvert.DeserializeObject<SaveSlotEnvelope>(json, Settings) ?? throw new JsonException("empty");

        /// <summary>只解析头部（读到 State 即停，不读快照正文）；不是合法 JSON 对象时抛异常。返回的 State 为空串。</summary>
        public static SaveSlotEnvelope ParseHeader(string json)
        {
            var head = new JObject();
            using (var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None })
            {
                if (!reader.Read() || reader.TokenType != JsonToken.StartObject) throw new JsonException("not an object");
                while (reader.Read())
                {
                    if (reader.TokenType == JsonToken.EndObject) break;
                    if (reader.TokenType != JsonToken.PropertyName) throw new JsonException("bad token");
                    var name = (string)reader.Value!;
                    if (name == nameof(State)) break;
                    if (!reader.Read()) throw new JsonException("truncated");
                    head[name] = JToken.ReadFrom(reader);
                }
            }
            return head.ToObject<SaveSlotEnvelope>(JsonSerializer.Create(Settings)) ?? throw new JsonException("empty");
        }
    }
}
