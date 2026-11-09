using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// 可选地牢知识支线（开发计划 v0.9 B23，R71；A11「可选地牢任务与知识」§2—§4、expansion-quests.json，验收 EXQ-A01—A08）：
    /// <list type="bullet">
    /// <item>EXQ05—08 登记后在 h03 罗铮 / h02 许安 / h04 陈芷交谈接取；d05_07/d06_08/d07_07/d08_08 核心记录的领取事务同时写记录事实、日志与知识（claim:…），
    /// 任务标“记录已取得”，回接取岗位交谈 0 分钟完成（岗位不在由代班员接手）；先领记录后接取的接取时追认完成。不另发物资、经验、容量。</item>
    /// <item>这四份记录的领取/核对是固定等待 10 分钟、两段 5 分钟（EXQ-A05）：中途停下保留已提交的段，续做只补剩余，不叠任务工时。</item>
    /// <item>知识按集合并入：已从别处掌握的只记来源（“知识已知，本次用于保存调查记录”），不兑换任何东西。</item>
    /// </list>
    /// </summary>
    public sealed partial class GameEngine
    {
        /// <summary>以这份核心记录为首次固定来源的知识包；不是为 null。</summary>
        public KnowledgePackConfig? PackOfClaim(string resourceId) => Config.ExpansionQuests.Packs.FirstOrDefault(p => p.ClaimResourceId == resourceId);

        /// <summary>两段续做的核心记录（B23 只对可选地牢四份记录，开发假设 B23-3）。</summary>
        public bool StagedRecord(ResourceConfig res) => res.IsRecord && PackOfClaim(res.NodeId) != null;

        /// <summary>两段续做记录本次还要等几分钟（已提交整段之外）。</summary>
        public int StagedLeft(GameState s, ResourceConfig res) =>
            Math.Max(0, res.BaseMinutes - (s.Resources.TryGetValue(res.NodeId, out var rs) ? rs.ClaimMinutesDone : 0));

        /// <summary>领取/核对预演里的两段说明。</summary>
        private IEnumerable<string> StagedClaimNotes(GameState s, ResourceConfig res)
        {
            if (!StagedRecord(res)) yield break;
            var done = res.BaseMinutes - StagedLeft(s, res);
            yield return $"固定等待{res.BaseMinutes}分钟，按两段5分钟提交；中途停下保留已完成的段，续做只补剩余（与这次领取是同一件事，不另加任务时间）";
            if (done > 0) yield return $"已完成{done}分钟，续做剩余{res.BaseMinutes - done}分钟";
        }

        /// <summary>Cancel 进行中时收集两段续做说明（并进取消提示）；平时为 null。</summary>
        private List<string>? _stagedCancelNote;

        /// <summary>取消/中断时保留已提交的整 5 分钟段（最多留到差最后一段）；未提交的不算。</summary>
        private void KeepStagedClaim(GameState s, ActionState a)
        {
            if (a.Type != ActionType.Gather || !_res.TryGetValue(a.TargetId, out var res) || !StagedRecord(res)) return;
            var seg = Math.Max(0, a.ElapsedMinutes - a.ElapsedMinutes % 5);
            if (seg <= 0) return;
            var rs = s.Resources[res.NodeId];
            rs.ClaimMinutesDone = Math.Min(res.BaseMinutes - 5, rs.ClaimMinutesDone + seg);
            var note = $"{res.Name}已保存完成的{rs.ClaimMinutesDone}分钟，下次只需续做剩余{res.BaseMinutes - rs.ClaimMinutesDone}分钟。";
            // B23 试玩修复：玩家点“退出”时这句并进取消提示（浮窗与日志同一条），其余中断仍单独写一条
            if (_stagedCancelNote != null) _stagedCancelNote.Add(note);
            else Emit(s, GameEventKind.Info, note);
        }

        /// <summary>知识包里这次没有新知识时的说明（A11 §3“知识已知，本次用于保存调查记录”）；部分已知写哪些只记来源。不是知识包为 null。</summary>
        private string? PackKnownNote(GameState s, ResourceConfig res, List<string> newKnowledge)
        {
            var p = PackOfClaim(res.NodeId);
            if (p == null) return null;
            // 只看本包的知识（矿道作业手册另有 B16 同授的基础陶工做法，照旧单列）
            if (!p.Knowledge.Any(k => newKnowledge.Contains(KnowledgeName(k)))) return "知识已知，本次用于保存调查记录";
            var known = p.Knowledge.Where(k => KnowsKnowledge(s, k)).Select(KnowledgeName).ToList();
            return known.Count == 0 ? null : $"{string.Join("、", known)}已掌握，只记来源（不兑换材料、经验或别的蓝图）";
        }

        private string KnowledgeName(string id) => Config.Knowledge.FirstOrDefault(k => k.Id == id)?.Name ?? id;

        /// <summary>领取这份记录对应的可选任务说明（预演 preview=true / 完成日志）。</summary>
        private IEnumerable<string> RecordQuestNotes(GameState s, ResourceConfig res, bool preview)
        {
            var key = ClaimKey(res.NodeId);
            // B38b（N02 附录 §3、Q291-2）：预演用“领取后会……”“领取后先保存……”，实际完成才用“已取得”“已保存”
            foreach (var q in LiveQuests.Where(q => q.RecordFacts.Contains(key) && !QuestDone(s, q.Id)))
            {
                var head = $"{KindWord(q)}「{q.Title}」";
                var where = $"{LocName(q.LocationId)}和{NpcNameOf(q.NpcId)}";
                if (QuestAccepted(s, q.Id))
                    yield return preview ? $"{head}：领取后会记为记录已取得，回{where}交谈即可回报（0分钟）。" : $"{head}的记录已取得。回{where}交谈即可回报（0分钟）。";
                else
                {
                    var cond = QuestRequiresBlocked(s, q) == null ? "" : "营区登记后，";
                    yield return preview ? $"{head}：领取后先保存记录，{cond}到{where}交谈接取即可完成（0分钟）。" : $"{head}的记录已保存。{cond}到{where}交谈接取即可完成（0分钟）。";
                }
            }
        }

        // ───────────────────────── 公开抄录（A11 §3 确定替代来源） ─────────────────────────

        /// <summary>城市服务表里的公开抄录（h03“查抄公开资料”）；h02/h04 合成服务借用它的名称与摘要。</summary>
        public const string CopyBaseServiceId = "copy_public_knowledge";
        public const string KnowledgeCopyKind = "knowledge_copy";

        /// <summary>某抄录服务可抄的知识包（配置顺序）。</summary>
        public List<KnowledgePackConfig> CopyPacksOf(string serviceId) => Config.ExpansionQuests.Packs.Where(p => p.CopyServiceId == serviceId).ToList();

        /// <summary>知识包里还没掌握的知识。</summary>
        private List<string> PackUnknown(GameState s, KnowledgePackConfig p) => p.Knowledge.Where(k => !KnowsKnowledge(s, k)).ToList();

        /// <summary>
        /// 抄录预览（0 分钟只读）：固定等待 30 分钟、0 材料；每个知识包一项（名称后标已掌握的），完全已知的包禁用（“已全部掌握”）；
        /// 要营区登记；不写任务调查记录、通关/首通时间或容量。中途停下不授知识、已过时间不退（Q33 通用事务）。
        /// </summary>
        private void KnowledgeCopyPreview(GameState s, CityServiceConfig svc, CityServiceView v)
        {
            var packs = CopyPacksOf(svc.Id);
            // B38b（N02 附录 §2、Q287）：主句逐字；工时与材料独列“{工时}分钟 · 0材料”（标题行 TimeText · CostText）
            v.TimeText = $"{svc.WorldMinutes}分钟";
            v.CostText = "0材料";
            v.EffectText = "只抄所选资料中还没掌握的做法。无需带地牢记录。中途停下，已过时间保留，做法要抄完才学会。";
            v.Choices = packs.Select(p => new CityChoiceView
            {
                Id = p.Id,
                Label = $"{p.CopyLabel}：" + string.Join("、", p.Knowledge.Select(k => KnowledgeName(k) + (KnowsKnowledge(s, k) ? "（已掌握）" : ""))),
                Blocked = PackUnknown(s, p).Count == 0 ? PackAllKnown : null,
            }).ToList();
            if (v.Blocked != null) return;
            if (!s.Progress.Flags.Contains(Config.City.Registration.RegisteredFact)) v.Blocked = "需要：" + FactLabel(Config.City.Registration.RegisteredFact);
            else if (v.Choices.All(c => c.Blocked != null)) v.Blocked = "这里的资料已全部掌握。";
        }

        /// <summary>单包全知（选项禁用与完成重验同句，N02 附录 Q287）。</summary>
        public const string PackAllKnown = "这份资料已全部掌握。";

        /// <summary>抄录完成（段末、死亡已先判过）：重验仍有未掌握的知识，写抄录事实授予；返回失败原因（时间不退），成功为 null。</summary>
        private string? CompleteKnowledgeCopy(GameState s, CityServiceTxn p, out string? done)
        {
            done = null;
            var pack = Config.ExpansionQuests.Packs.FirstOrDefault(x => x.Id == p.ChoiceId);
            if (pack == null) return "资料不存在";
            var unknown = PackUnknown(s, pack);
            if (unknown.Count == 0) return PackAllKnown;
            s.Progress.Flags.Add(pack.CopyFact);
            // B38b（Q283）：一条日志直接写定稿句，名单只取此次新增，去掉外层服务名
            done = $"抄完{pack.CopyLabel}，学会{string.Join("、", unknown.Select(KnowledgeName))}。";
            return null;
        }

        // ───────────────────────── 开发入口 ─────────────────────────

        /// <summary>
        /// 开发入口（?dev=1）B23 栏：register 记下营区登记（授权+登记，C2 与登记默认知识）；board 传送到该任务接取岗位；
        /// record 传送到记录房并把探索度拨到 100%（揭示记录卡，不领取；进危险房间按已开始的远征补提交）；copy 传送到该知识包的抄录岗位；status 一行状态。
        /// </summary>
        public string DebugExq(GameState s, string kind, string questId)
        {
            var q = Quest(questId);
            var p = Config.ExpansionQuests.Packs.FirstOrDefault(x => x.QuestId == questId);
            if (q == null || p == null) return $"开发：没有可选任务 {questId}";
            var res = Config.Resources.First(r => r.NodeId == p.ClaimResourceId);
            switch (kind)
            {
                case "register":
                {
                    var reg = Config.City.Registration;
                    s.Progress.Flags.Add(reg.AuthorizedFact);
                    s.Progress.Flags.Add(reg.RegisteredFact);
                    RefreshGates(s);
                    UpdateFlags(s);
                    return "开发：已记下营区登记（队伍容量 C2 与登记默认做法知识；不发物资）。";
                }
                case "board":
                    return DebugMapTeleport(s, q.LocationId);
                case "copy":
                    return DebugMapTeleport(s, p.CopyLocationId);
                case "record":
                {
                    var tp = DebugMapTeleport(s, res.LocationId);
                    DebugSetProgress(s, res.LocationId, 100);
                    return tp + $"；探索度拨到100%，“{res.Name}”卡已出现（{(CampaignClaimed(s, res.NodeId) ? "本局已领，可核对" : "未领取")}）。";
                }
                default:
                    return DebugExqStatus(s, questId);
            }
        }

        /// <summary>开发入口（?dev=1）：所选可选地牢任务的状态一行（接取、记录、完成、两段已提交分钟、知识）。</summary>
        public string DebugExqStatus(GameState s, string questId)
        {
            var q = Quest(questId);
            var p = Config.ExpansionQuests.Packs.FirstOrDefault(x => x.QuestId == questId);
            if (q == null || p == null) return $"开发：没有可选任务 {questId}";
            var res = Config.Resources.First(r => r.NodeId == p.ClaimResourceId);
            var know = string.Join("、", p.Knowledge.Select(k => $"{KnowledgeName(k)}{(KnowsKnowledge(s, k) ? "✓" : "✗")}"));
            return $"开发：{q.Title}（{q.Id}）· {(QuestDone(s, q.Id) ? "已完成" : QuestAccepted(s, q.Id) ? (HasQuestRecord(s, q) ? "记录已取得" : "进行中") : "未接取")}" +
                   $" · {res.Name}{(CampaignClaimed(s, res.NodeId) ? "已领取" : "未领取")}（已提交{res.BaseMinutes - StagedLeft(s, res)}/{res.BaseMinutes}分钟）· 知识：{know}" +
                   $" · 公开抄录{(s.Progress.Flags.Contains(p.CopyFact) ? "已抄" : "未抄")}";
        }
    }
}
