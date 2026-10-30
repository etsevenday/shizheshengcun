using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Survival.Core.State
{
    /// <summary>
    /// 右栏人物板块（熟练度、词条）的展开/收起偏好（S14「2026-10-30 主界面截图复核」§C，验收 UI-M07—M09）。
    /// 纯界面偏好：按 档案(本地存档槽)×角色ID×板块ID 各存一个布尔值，独立于游戏进度——不进存档、读档/退出战斗不回滚。
    /// 只有玩家显式点击才写入；读到没有记录的组合时返回原默认值（展开），不把默认值写回，界面切换也不写。
    /// 序列化为一个 JSON 对象（键 "档案|角色|板块"），由网页壳存到浏览器本地；存储不可用时调用方只保留内存里的这一份。
    /// </summary>
    public sealed class UiFoldPrefs
    {
        /// <summary>当前唯一的本地存档槽（SaveStore 单槽 survival.save.v1）。</summary>
        public const string DefaultProfile = "slot1";
        /// <summary>主角的角色ID（多角色模型 B3 之前只有这一个角色）。</summary>
        public const string MainActor = "p0";
        public const string Skills = "skills";
        public const string Traits = "traits";
        /// <summary>没有记录时的原默认值：两块都展开（S14 §18.2 原实现）。</summary>
        public const bool DefaultOpen = true;

        private readonly Dictionary<string, bool> _open = new Dictionary<string, bool>(StringComparer.Ordinal);

        public static string KeyOf(string profile, string actorId, string sectionId) =>
            $"{Norm(profile)}|{Norm(actorId)}|{Norm(sectionId)}";

        private static string Norm(string? part) => string.IsNullOrEmpty(part) ? "_" : part!.Replace("|", "/");

        /// <summary>该组合当前是否展开；没有记录时返回 <see cref="DefaultOpen"/>，不写入。</summary>
        public bool IsOpen(string profile, string actorId, string sectionId) =>
            _open.TryGetValue(KeyOf(profile, actorId, sectionId), out var v) ? v : DefaultOpen;

        /// <summary>是否已有玩家明确选择过的记录。</summary>
        public bool HasChoice(string profile, string actorId, string sectionId) => _open.ContainsKey(KeyOf(profile, actorId, sectionId));

        /// <summary>玩家显式设置展开/收起（只在点击时调用）。</summary>
        public void Set(string profile, string actorId, string sectionId, bool open) => _open[KeyOf(profile, actorId, sectionId)] = open;

        /// <summary>玩家点击折叠标题：取反当前显示值并写入，返回新值。</summary>
        public bool Toggle(string profile, string actorId, string sectionId)
        {
            var v = !IsOpen(profile, actorId, sectionId);
            Set(profile, actorId, sectionId, v);
            return v;
        }

        public int Count => _open.Count;

        public string Serialize() => JsonConvert.SerializeObject(_open);

        /// <summary>从浏览器本地读回；内容缺失或损坏时得到空偏好（全部按默认展开），不抛异常。</summary>
        public static UiFoldPrefs Parse(string? json)
        {
            var p = new UiFoldPrefs();
            if (string.IsNullOrWhiteSpace(json)) return p;
            try
            {
                var d = JsonConvert.DeserializeObject<Dictionary<string, bool>>(json!);
                if (d != null)
                    foreach (var kv in d)
                        if (!string.IsNullOrEmpty(kv.Key)) p._open[kv.Key] = kv.Value;
            }
            catch (JsonException)
            {
                // 偏好损坏只影响折叠显示，回到默认值，不阻塞玩法
            }
            return p;
        }
    }
}
