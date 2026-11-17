using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Survival.Core.State
{
    /// <summary>
    /// B35a1（S14／S08 Q797 B、Q908 B）：制作分类的本机长期记忆。顶栏“手工制作”一份（键 <see cref="TopKey"/>），
    /// 每座真实设施工作区各一份（键为 <see cref="Survival.Core.Engine.CraftWorkspace.Key"/>：世界种子＋地点＋区＋设施，或世界种子＋公共站 Id），互不覆盖。
    /// 只在玩家明确点分类时写入；读取时由调用方按当前合法分类校验，失效回原合法默认（不凭记忆授予资格）。
    /// 纯界面偏好：序列化为一个 JSON 对象，由网页壳存浏览器本地，不进存档；存储不可用时只在内存里保留。
    /// </summary>
    public sealed class CraftTabPrefs
    {
        /// <summary>顶栏“手工制作”自己的那一份。</summary>
        public const string TopKey = "top";

        private readonly Dictionary<string, string> _tabs = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>该上下文上次手选的分类；没有记录为 null。</summary>
        public string? Get(string key) => _tabs.TryGetValue(key, out var v) ? v : null;

        /// <summary>玩家明确选了一个分类：记下（覆盖本上下文旧值，不动别的上下文）。</summary>
        public void Set(string key, string tab) => _tabs[key] = tab;

        /// <summary>按当前合法分类取恢复值：记录仍在 legal 里就用它，否则回 fallback（不写回）。</summary>
        public string Resolve(string key, IEnumerable<string> legal, string fallback)
        {
            var v = Get(key);
            if (v == null) return fallback;
            foreach (var t in legal) if (t == v) return v;
            return fallback;
        }

        public string Serialize() => JsonConvert.SerializeObject(_tabs);

        /// <summary>解析本地存储的原文；空、损坏或不是对象时从空记忆开始（不抛出）。</summary>
        public static CraftTabPrefs Parse(string? raw)
        {
            var p = new CraftTabPrefs();
            if (string.IsNullOrWhiteSpace(raw)) return p;
            try
            {
                var d = JsonConvert.DeserializeObject<Dictionary<string, string>>(raw!);
                if (d != null) foreach (var kv in d) if (kv.Key != null && kv.Value != null) p._tabs[kv.Key] = kv.Value;
            }
            catch (JsonException) { }
            return p;
        }
    }
}
