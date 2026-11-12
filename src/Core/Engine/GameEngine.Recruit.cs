using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>收容所周期招募面板（R08 RC3/RC4 候选卡与名额；B19）。</summary>
    public sealed class RecruitView
    {
        public bool Opened { get; set; }
        /// <summary>没开放或不能办的总原因（不在收容所、战斗中……）；null 为可以办。</summary>
        public string? Blocked { get; set; }
        public string OpenText { get; set; } = "";
        public int? Cycle { get; set; }
        public string CycleText { get; set; } = "";
        /// <summary>“随机/保底名册 n/K（C2：K=min(3,C−1)=1）”。</summary>
        public string QuotaText { get; set; } = "";
        public int SharedUsed { get; set; }
        public int SharedCap { get; set; }
        public string CostText { get; set; } = "";
        public List<RecruitCandidateView> Candidates { get; set; } = new List<RecruitCandidateView>();
        /// <summary>在办登记（跨周期时候选可能已不在本期名单里）。</summary>
        public string? PendingText { get; set; }
        public List<RetireView> Retirables { get; set; } = new List<RetireView>();
        /// <summary>B22g（Q183 selection）：“编入同行”选项的禁用原因（同行已满写真实原因，不挤出他人）；null 为可选。</summary>
        public string? PartyOptionBlocked { get; set; }
        /// <summary>B22g（Q183 selection）：候补目的地下拉（营区广场在前，再列已完工/施工中的自有安全屋，禁用项写原因）。</summary>
        public List<RecruitDestinationView> ReserveDestinations { get; set; } = new List<RecruitDestinationView>();
        /// <summary>B22g（Q183 confirm）：待安置确认（完成重验失败）；没有为 null。</summary>
        public RecruitAwaitingView? Awaiting { get; set; }
    }

    /// <summary>候补目的地一项（B22g）：Key 为 "square" 或 "sh:&lt;地点&gt;"。</summary>
    public sealed class RecruitDestinationView
    {
        public string Key { get; set; } = "";
        public string Label { get; set; } = "";
        public RecruitPlacement Placement { get; set; } = RecruitPlacement.Square;
        public string? Blocked { get; set; }
    }

    /// <summary>待安置确认（B22g，Q183 confirm）：候选、原选择、失败原因；只能改选合法去向或取消。</summary>
    public sealed class RecruitAwaitingView
    {
        public string CandidateName { get; set; } = "";
        public string IdentityText { get; set; } = "";
        public string ChoiceText { get; set; } = "";
        public string FailureText { get; set; } = "";
        /// <summary>现在能不能办（在收容所安全空闲，开发假设 B22g-3）；null 为可以。</summary>
        public string? Blocked { get; set; }
    }

    public sealed class RecruitCandidateView
    {
        public string CandidateId { get; set; } = "";
        public string Name { get; set; } = "";
        public string TemplateName { get; set; } = "";
        public string TemplateUse { get; set; } = "";
        /// <summary>B22g（Q180）：“性别：女 · 外观：中等肤色，黑色齐肩发，雀斑”（旧候选“未说明/未记录”）。</summary>
        public string IdentityText { get; set; } = "";
        public string SkillText { get; set; } = "";
        public string BodyText { get; set; } = "";
        public string CombatText { get; set; } = "";
        /// <summary>二级查看：完整身体、人物档与其余熟练。</summary>
        public string DetailText { get; set; } = "";
        public string StatusText { get; set; } = "";
        public string Status { get; set; } = "";
        public string? Blocked { get; set; }
    }

    public sealed class RetireView
    {
        public string ActorId { get; set; } = "";
        public string Name { get; set; } = "";
        public string KindLabel { get; set; } = "";
        public string WhereText { get; set; } = "";
        /// <summary>确认说明：永久离开、技能不返还、物品去向（件数、借用矛）。</summary>
        public string ConfirmText { get; set; } = "";
        public string? Blocked { get; set; }
    }

    /// <summary>
    /// 收容所周期招募（开发计划 v0.9 B19，需求 R60/R61；R08「2026-10-30 B3 实体收容所、周期招募与战利品owner」RC1—RC7、「v3建筑入口校准」、
    /// 「Q62—Q74」当前优先节；S01 本轮章；A06「B3 随机模板与技能解锁边界」；A11 Q33 shelter_recruitment）：
    /// QMAP01 登记后首次实际安全到达 h06_shelter 记开放时刻；周期 k = floor((worldMinute−开放)/4320)；每期按 本局种子+收容所+k 生成 3 名候选并保存，不重抽；
    /// 跨多期只给当前批、离线不走；登记 0 材料 10 分钟 wait，开始锁候选与名额，完成时主角存活才建人；随机与 RB01 共享 K=min(3,C−1)。
    /// </summary>
    public sealed partial class GameEngine
    {
        private RecruitmentConfig Rc => Config.Party.Recruitment;

        /// <summary>登记等待行动的标记前缀（“recruit:&lt;candidateId&gt;”，10 分钟 wait，完成时重验）。</summary>
        public const string RecruitTargetPrefix = "recruit:";

        public bool IsRecruitService(string serviceId) => serviceId == Rc.ServiceId;

        public bool RecruitOpened(GameState s) => s.Recruit.ShelterOpenedAt != null;

        /// <summary>某世界分钟所在的周期号（没开放为 null）：k = floor((minute − 开放时刻) / 4320)。</summary>
        public int? RecruitCycleAt(GameState s, int minute)
        {
            if (s.Recruit.ShelterOpenedAt is not int start || Rc.RefreshMinutes <= 0) return null;
            return Math.Max(0, (minute - start) / Rc.RefreshMinutes);
        }

        /// <summary>下一期开始的世界分钟（没开放为 null）。</summary>
        public int? NextRecruitRefresh(GameState s) =>
            RecruitCycleAt(s, s.GameMinute) is int k ? s.Recruit.ShelterOpenedAt!.Value + (k + 1) * Rc.RefreshMinutes : (int?)null;

        // ───────────── 共享名额（RC1/RC6：随机 + RB01 共用 K=min(3,C−1)） ─────────────

        /// <summary>随机/保底共享名额 K = min(3, C−1)。</summary>
        public int SharedRosterCap(GameState s) => Math.Max(0, Math.Min(Rc.SharedRosterMax, PartyCapacity(s) - 1));

        /// <summary>占共享名额的存活在册生成角色（随机招入者与 RB01 护送员；留守候补照常占名额，死亡/退役不占）。</summary>
        public List<RosterEntry> SharedRoster(GameState s) => s.Roster.Where(r => r.IsGenerated && r.Present).ToList();

        /// <summary>已占 + 在办登记锁住的名额。</summary>
        public int SharedRosterLocked(GameState s) => SharedRoster(s).Count + (s.Recruit.Pending != null ? 1 : 0);

        /// <summary>全名册存活人数（含主角）。</summary>
        public int RosterAliveWithPlayer(GameState s) => 1 + s.Roster.Count(r => r.Present);

        /// <summary>名额不足的原因（RC4 满额文案、RC7 旧档超额冻结）；null 为还有名额。extra 为本次还要再占的人数。</summary>
        private string? SharedQuotaBlocked(GameState s, bool includePending)
        {
            var k = SharedRosterCap(s);
            var used = includePending ? SharedRosterLocked(s) : SharedRoster(s).Count;
            if (used > k)
                return $"随机/保底名册{used}/{k}（超出现行上限）：冻结新增；可在{LocName(Rc.LocationId)}让一名随机或保底角色退役到合法人数（不会随机删人）";
            if (used >= k) return k == 0 ? $"随机/保底名册0/0（容量C{PartyCapacity(s)}：K=min({Rc.SharedRosterMax},C−1)=0，营区登记后C2才有名额）" : $"随机/保底名册{k}/{k}；留守不释放名额";
            if (RosterAliveWithPlayer(s) + (includePending && s.Recruit.Pending != null ? 1 : 0) >= Rc.RosterAliveCap)
                return $"全名册存活已达上限{Rc.RosterAliveCap}人（含你）";
            return null;
        }

        // ───────────── 开放与周期（RC1/RC7、S01） ─────────────

        /// <summary>现在是不是在收容所的安全访问：实际在收容所、已登记、未终局、没有战斗。</summary>
        private bool SafeAtShelterForRecruit(GameState s) =>
            s.CurrentLocation == Rc.LocationId && s.CurrentZone == ExteriorZone && !s.Progress.Dead && s.Battle == null
            && s.Progress.Flags.Contains(Rc.OpenedByFact);

        /// <summary>
        /// 开放收容所候选（RC1“QMAP01登记后首次实际到达”；旧档同样在首次实到时起算，不补历史周期）：只在安全访问时写一次开放时刻并生成第 0 期。
        /// 返回是否这次开放。
        /// </summary>
        public bool TryOpenShelterRecruitment(GameState s, List<string>? notes = null)
        {
            if (RecruitOpened(s) || !SafeAtShelterForRecruit(s) || Rc.Templates.Count == 0) return false;
            s.Recruit.ShelterOpenedAt = s.GameMinute;
            EnsureRecruitCycle(s);
            var text = $"收容所周期候选开放：每{Clock.Duration(Rc.RefreshMinutes)}换一批{Rc.CandidatesPerCycle}名（本期到{Clock.DayTime(NextRecruitRefresh(s)!.Value)}），在杜穗“事项”的同行者名册里查看与登记";
            if (notes != null) notes.Add(text);
            else Emit(s, GameEventKind.Info, text + "。");
            return true;
        }

        /// <summary>
        /// 当前周期的候选（RC1/RC7）：开放后若存档里的批次不是当前周期，就按 本局种子+收容所+k 生成本期 3 名并保存（跨多期只生成当前这一批，不补历史）；
        /// 在办登记的候选与名额随事务保留（快照在 Pending 里），其余未招者离开。同周期再调用不重抽。返回是否换了批。
        /// </summary>
        public bool EnsureRecruitCycle(GameState s)
        {
            DropStaleRecruitTxn(s);
            if (RecruitCycleAt(s, s.GameMinute) is not int k) return false;
            if (s.Recruit.Cycle == k && s.Recruit.Candidates.Count > 0) return false;
            s.Recruit.Candidates = GenerateCandidates(s, k);
            s.Recruit.Cycle = k;
            return true;
        }

        /// <summary>世界 tick 末（死亡已判过）：周期交界换批（候选是确定性快照，何时生成结果相同）。</summary>
        private void RecruitTick(GameState s)
        {
            if (RecruitOpened(s)) EnsureRecruitCycle(s);
        }

        /// <summary>在办登记的主行动已不在（读档修复、异常中断）：释放候选与名额锁。</summary>
        private void DropStaleRecruitTxn(GameState s)
        {
            var p = s.Recruit.Pending;
            if (p == null || p.AwaitingPlacement || (s.Main != null && s.Main.ActionId == p.ActionId)) return;   // B22g：待安置确认不是在办行动，不释放
            var c = s.Recruit.Candidates.FirstOrDefault(x => x.CandidateId == p.CandidateId);
            if (c != null && c.Status == RecruitCandidate.Registering) c.Status = RecruitCandidate.Available;
            s.Recruit.Pending = null;
        }

        // ───────────── 候选生成（RC2/RC3、Q62、A06） ─────────────

        /// <summary>周期种子：本局随机根（campaignId = 新局种子）+ 收容所 ID + 周期号，FNV-1a 64 后 splitmix64 终混（与鉴定所报价同一稳定哈希）。</summary>
        public static ulong RecruitSeed(GameState s, string shelterId, int cycle) => Mix64(Fnv64($"{CampaignIdOf(s)}|{shelterId}|{cycle}"));

        /// <summary>splitmix64 序列（平台无关、可复现）。</summary>
        private sealed class SeedStream
        {
            private ulong _x;
            public SeedStream(ulong seed) { _x = seed; }
            public ulong Next()
            {
                _x += 0x9E3779B97F4A7C15UL;
                return Mix64(_x);
            }
            /// <summary>[0, n) 等概率整数。</summary>
            public int Below(int n) => n <= 1 ? 0 : (int)(Next() % (ulong)n);
            /// <summary>[min, max] 闭区间整数均匀。</summary>
            public int Between(int min, int max) => min + Below(max - min + 1);
        }

        private static int Weighted(SeedStream rng, Dictionary<string, int> weights)
        {
            var list = weights.Select(kv => (Level: int.Parse(kv.Key), W: kv.Value)).Where(x => x.W > 0).OrderBy(x => x.Level).ToList();
            var total = list.Sum(x => x.W);
            var roll = rng.Below(total);
            foreach (var x in list)
            {
                if (roll < x.W) return x.Level;
                roll -= x.W;
            }
            return list.Last().Level;
        }

        /// <summary>randomRecruit 的 {min,max,step} 离散均匀；缺字段时取 fallback。</summary>
        private static double SteppedUniform(SeedStream rng, JObject? spec, string key, double fallback)
        {
            if (spec?[key] is not JObject o) return spec?[key] is JValue v ? (double)v : fallback;
            var min = (double)o["min"]!;
            var max = (double)o["max"]!;
            var step = (double?)o["step"] ?? 1;
            var n = (int)Math.Round((max - min) / step);
            return Math.Round(min + step * rng.Below(n + 1), 4);
        }

        /// <summary>
        /// 生成第 k 期候选：9 个模板等权无放回抽 3（期内不重复，跨期可重复）；主熟练 Lv2 70%/Lv3 30%，副熟练二选一等权、Lv1/Lv2 各半，其余 Lv1、
        /// 级内经验 0；身体 H14—20、E10—16（上限 20）、N/W 40、S50—75、M40—65、I60—75 整数均匀、V0 未感染；控制抗性/闪避/基础甲/基础速度按
        /// actorBaseDefense.randomRecruit（Q62）。B22g（Q180 选 C）：姓名/性别/外观按独立展示子流 + 全局流水号（与数值独立）；池为空时退回 B19-2 中性名。
        /// </summary>
        private List<RecruitCandidate> GenerateCandidates(GameState s, int k)
        {
            var rng = new SeedStream(RecruitSeed(s, Rc.LocationId, k));
            var order = Enumerable.Range(0, Rc.Templates.Count).ToList();
            var n = Math.Min(Rc.CandidatesPerCycle, order.Count);
            for (var i = 0; i < n; i++)
            {
                var j = i + rng.Below(order.Count - i);
                (order[i], order[j]) = (order[j], order[i]);
            }
            var body = Rc.Body;
            var spec = Config.ActorBaseDefense.RandomRecruit;
            var list = new List<RecruitCandidate>();
            for (var i = 0; i < n; i++)
            {
                var t = Rc.Templates[order[i]];
                var c = new RecruitCandidate
                {
                    CandidateId = $"c{k}_{i + 1}",
                    Cycle = k,
                    TemplateId = t.Id,
                    MainSkill = t.Main,
                    MainLevel = Weighted(rng, Rc.MainLevelWeights),
                    SubSkill = t.Subs[rng.Below(t.Subs.Count)],
                    SubLevel = Weighted(rng, Rc.SubLevelWeights),
                    Health = rng.Between(body.Health[0], body.Health[1]),
                    HealthMax = body.HealthMax,
                    Energy = rng.Between(body.Energy[0], body.Energy[1]),
                    EnergyMax = body.EnergyMax,
                    Satiety = body.Satiety,
                    Hydration = body.Hydration,
                    Wakefulness = rng.Between(body.Wakefulness[0], body.Wakefulness[1]),
                    Morale = rng.Between(body.Morale[0], body.Morale[1]),
                    Immunity = rng.Between(body.Immunity[0], body.Immunity[1]),
                    VirusLoad = body.Virus,
                    GeneratedAt = s.GameMinute,
                };
                c.ControlResist = SteppedUniform(rng, spec, "controlResist", 0);
                c.Evasion = SteppedUniform(rng, spec, "evasion", Config.Scope.BasePlayerDodge);
                c.BaseArmor = SteppedUniform(rng, spec, "baseArmor", 0);
                c.BaseSpeed = SteppedUniform(rng, spec, "baseSpeed", Config.Scope.BasePlayerSpeed);
                var seq = s.Recruit.NextNameSeq++;
                if (Rc.Identity.Enabled) AssignIdentity(s, c, k, seq);
                else c.Name = $"{Rc.NamePrefix}{t.Name}{seq:000}";
                list.Add(c);
            }
            return list;
        }

        // ───────────── 身份（B22g，Q180 选 C：identity 合同） ─────────────

        /// <summary>
        /// 展示随机子流（identity seedRule）：每个字段 = splitmix64(FNV-1a64("本局种子|收容所|周期|候选流水号|字段键")) mod 池长，各池独立等概率有放回；
        /// 不碰模板/身体/人物档的属性随机序列（那条流在 GenerateCandidates 里另起），所以外观池不影响数值。
        /// </summary>
        private int IdentityPick(GameState s, int cycle, int seq, string field, int n) =>
            n <= 1 ? 0 : (int)(Mix64(Fnv64($"{CampaignIdOf(s)}|{Rc.LocationId}|{cycle}|{seq}|{field}")) % (ulong)n);

        /// <summary>一次生成并写进候选快照：姓、名、性别、肤色、发型、发色、面部细节；显示名“姓名·004”（至少三位，超过 999 不截断）。</summary>
        private void AssignIdentity(GameState s, RecruitCandidate c, int cycle, int seq)
        {
            var id = Rc.Identity;
            var surname = id.Surnames[IdentityPick(s, cycle, seq, "surname", id.Surnames.Count)];
            var given = id.GivenNames[IdentityPick(s, cycle, seq, "givenName", id.GivenNames.Count)];
            c.Seq = seq;
            c.Gender = id.Genders[IdentityPick(s, cycle, seq, "gender", id.Genders.Count)].Id;
            c.SkinTone = id.SkinTones[IdentityPick(s, cycle, seq, "skinTone", id.SkinTones.Count)];
            c.HairStyle = id.HairStyles[IdentityPick(s, cycle, seq, "hairStyle", id.HairStyles.Count)];
            c.HairColor = id.HairColors[IdentityPick(s, cycle, seq, "hairColor", id.HairColors.Count)];
            c.FaceDetail = id.FaceDetails[IdentityPick(s, cycle, seq, "faceDetail", id.FaceDetails.Count)];
            c.Name = $"{surname}{given}{id.NameSeparator}{seq.ToString(new string('0', Math.Max(1, id.SeqMinDigits)))}";
        }

        /// <summary>性别显示：池里的名字（女/男/未说明）；旧档没有字段为“未说明”。</summary>
        public string GenderText(string? gender) => Rc.Identity.GenderName(gender) ?? Rc.Identity.LegacyGender;

        /// <summary>外观摘要“中等肤色，黑色齐肩发，雀斑”；旧档没有字段为“未记录”。</summary>
        public string AppearanceText(string? skin, string? hairStyle, string? hairColor, string? face) =>
            skin == null || hairStyle == null || hairColor == null || face == null
                ? Rc.Identity.LegacyAppearance
                : $"{skin}，{hairColor}{hairStyle}，{face}";

        /// <summary>候选卡/名册一行：“性别：女 · 外观：中等肤色，黑色齐肩发，雀斑”。</summary>
        public string IdentityText(RecruitCandidate c) =>
            $"性别：{GenderText(c.Gender)} · 外观：{AppearanceText(c.SkinTone, c.HairStyle, c.HairColor, c.FaceDetail)}";

        /// <summary>名册里随机招入者的身份行；不是随机招入者（固定六人、RB01 护送员）为 null（Q180：既有名称与资料不改）。</summary>
        public string? IdentityText(RosterEntry r) =>
            r.IsRandom ? $"性别：{GenderText(r.Gender)} · 外观：{AppearanceText(r.SkinTone, r.HairStyle, r.HairColor, r.FaceDetail)}" : null;

        public RecruitCandidate? CurrentCandidate(GameState s, string candidateId) =>
            s.Recruit.Candidates.FirstOrDefault(c => c.CandidateId == candidateId);

        // ───────────── 登记事务（RC4/RC7、S01） ─────────────

        /// <summary>收容所服务的安全空闲（R08 v3 校准：只在 h06_shelter 实际办理，不从广场或别处远程）。null 为可以。</summary>
        public string? RecruitServiceBlocked(GameState s, string verb)
        {
            if (s.Progress.Dead) return "本局已结束";
            if (InCombatFlow(s)) return $"战斗中不能{verb}";
            if (s.Main != null || s.Sub != null) return CityBusyText;   // Q317：收容所子盘在事项盘内，行动占用用统一句（原优先级不变）
            if (DialogueLocked(s)) return "先读完对话";
            if (s.CurrentLocation != Rc.LocationId || s.CurrentZone != ExteriorZone) return $"只能在{LocName(Rc.LocationId)}安全空闲时{verb}";
            return null;
        }

        /// <summary>登记某候选的只读预演（开始时同一函数重验；不含去向，去向另由 RecruitPlacementBlocked 判）；null 为可以。</summary>
        public string? RecruitBlocked(GameState s, string candidateId)
        {
            var why = RecruitServiceBlocked(s, "登记");
            if (why != null) return why;
            if (!RecruitOpened(s)) return "收容所候选还没开放（营区登记后第一次实际到达收容所时开放）";
            var c = CurrentCandidate(s, candidateId);
            if (c == null) return "这名候选已不在本期名单里";
            if (c.Status == RecruitCandidate.Recruited) return "本期已招入名册（退役后同一周期也不能再招回）";
            if (s.Recruit.Pending?.AwaitingPlacement == true) return $"{s.Recruit.Pending.Candidate.Name}的登记在等待安置确认：先改选去向或取消";
            if (c.Status == RecruitCandidate.Registering || s.Recruit.Pending != null) return "已有一笔登记在办";
            return SharedQuotaBlocked(s, true);
        }

        // ───────────── 登记后去向（B22g，Q183 shelter.selection/arrival/confirm） ─────────────

        /// <summary>“编入同行”能不能选：同行人数 &lt; C（满员写真实原因，不能挤出他人）。null 为可以。</summary>
        public string? RecruitPartyOptionBlocked(GameState s)
        {
            var cap = PartyCapacity(s);
            return s.Party.Count >= cap ? $"同行已满 {s.Party.Count}/{cap}（含你；容量C{cap}），不能挤出别人：请选“安排候补”" : null;
        }

        /// <summary>
        /// 某间安全屋能不能作候补目的地（selection）：已完工、已实际到访、且从收容所沿当前已知可通路线可达的自有安全屋。null 为可以。
        /// 不设额外床位上限（人数沿名册 K 与全名册存活 10）。
        /// </summary>
        public string? RecruitSafehouseBlocked(GameState s, string locationId)
        {
            if (!_locs.ContainsKey(locationId)) return "地点不存在";
            var name = SafehouseName(s, locationId);
            if (!SafehouseBuilt(s, locationId)) return SafehouseBuilding(s, locationId) ? $"{name}还在施工，未完工" : $"{LocName(locationId)}没有建成的安全屋";
            if (!s.Locations.TryGetValue(locationId, out var ls) || !ls.Visited) return $"还没实际到访过{LocName(locationId)}";
            if (MapPathFrom(s, Rc.LocationId, locationId) == null) return $"从{LocName(Rc.LocationId)}到{LocName(locationId)}没有已知可通的路线";
            return null;
        }

        /// <summary>一种去向现在能不能选（开工与完成重验、待安置改选共用）。null 为可以。</summary>
        public string? RecruitPlacementBlocked(GameState s, RecruitPlacement? placement)
        {
            if (placement == null) return "先选择登记后去向：编入同行，或安排候补（营区广场或安全屋）";
            if (placement.IsParty) return RecruitPartyOptionBlocked(s);
            if (placement.Mode != RecruitPlacement.ReserveMode) return "未知的去向";
            return placement.Safehouse == null ? null : RecruitSafehouseBlocked(s, placement.Safehouse);
        }

        /// <summary>候补目的地下拉：营区广场（总是可选）在前，再列已完工或施工中的安全屋（禁用项写原因）。</summary>
        public List<RecruitDestinationView> RecruitReserveDestinations(GameState s)
        {
            var list = new List<RecruitDestinationView>
            {
                new RecruitDestinationView { Key = PlacementKey(RecruitPlacement.Square), Label = $"{LocName(HubLocation(s))}（公共居所）", Placement = RecruitPlacement.Square },
            };
            foreach (var l in Config.Locations)
            {
                if (!SafehouseBuilt(s, l.Id) && !SafehouseBuilding(s, l.Id)) continue;
                var pl = RecruitPlacement.AtSafehouse(l.Id);
                var path = SafehouseBuilt(s, l.Id) ? MapPathFrom(s, Rc.LocationId, l.Id) : null;
                list.Add(new RecruitDestinationView
                {
                    Key = PlacementKey(pl),
                    Label = $"{SafehouseName(s, l.Id)}屋内" + (path != null ? $"（从{LocName(Rc.LocationId)}约{path.Sum(x => x.Minutes)}基础分钟路程，0分钟送达）" : ""),
                    Placement = pl,
                    Blocked = RecruitSafehouseBlocked(s, l.Id),
                });
            }
            return list;
        }

        /// <summary>去向的界面键："party"、"square" 或 "sh:&lt;地点&gt;"。</summary>
        public static string PlacementKey(RecruitPlacement p) => p.IsParty ? "party" : p.Safehouse == null ? "square" : "sh:" + p.Safehouse;

        /// <summary>界面键还原为去向；不认识为 null。</summary>
        public static RecruitPlacement? PlacementFromKey(string? key) => key switch
        {
            "party" => RecruitPlacement.Party,
            "square" => RecruitPlacement.Square,
            string k when k.StartsWith("sh:") && k.Length > 3 => RecruitPlacement.AtSafehouse(k.Substring(3)),
            _ => null,
        };

        /// <summary>去向文字：“编入同行”“候补·营区广场”“候补·××安全屋屋内”。</summary>
        public string PlacementText(GameState s, RecruitPlacement? p) =>
            p == null ? $"候补·{LocName(HubLocation(s))}（旧档在办登记，默认）"
            : p.IsParty ? "编入同行" : p.Safehouse == null ? $"候补·{LocName(HubLocation(s))}" : $"候补·{SafehouseName(s, p.Safehouse)}屋内";

        /// <summary>
        /// 开始登记（RC4；B22g Q183）：确认前必须明确选去向（编入同行，或候补到营区广场/合格安全屋）；0 材料，主行动 10 分钟 wait（两个 5 分钟段，你与同行按等待消耗）；
        /// 开始时锁 candidateId、1 个名额与这次选择的快照（名额不够或去向不合法不推进时间）。requestId 重放不另开。中途取消只取消登记，已耗时间不退，候选仍是同一人。
        /// </summary>
        public ActionResult StartRecruit(GameState s, string candidateId, RecruitPlacement? placement, string? requestId = null)
        {
            if (SeenRequest(s, requestId)) return ActionResult.Repeated();
            EnsureRecruitCycle(s);
            var why = RecruitBlocked(s, candidateId) ?? RecruitPlacementBlocked(s, placement);
            if (why != null) return ActionResult.Fail(why);
            var c = CurrentCandidate(s, candidateId)!;
            var req = ActionRequest.Wait(Rc.RegistrationMinutes);
            req.TargetId = RecruitTargetPrefix + candidateId;
            req.RequestId = requestId;
            var res = Start(s, req);
            if (!res.Ok) return res;
            s.Main!.Label = $"登记{(placement!.IsParty ? "同行" : "候补")}：{c.Name}（{Clock.Duration(Rc.RegistrationMinutes)}）";
            c.Status = RecruitCandidate.Registering;
            s.Recruit.Pending = new RecruitTxn
            {
                ActionId = s.Main.ActionId, RequestId = requestId, CandidateId = c.CandidateId, Cycle = c.Cycle,
                LocationId = s.CurrentLocation, StartMinute = s.GameMinute, Candidate = c.Clone(), Placement = placement.Clone(),
            };
            return res;
        }

        /// <summary>取消登记：只解除候选与名额锁（候选回到可招、同一人同属性）；已耗时间不退。</summary>
        private void ReleaseRecruit(GameState s, ActionState a)
        {
            var p = s.Recruit.Pending;
            if (p == null || p.ActionId != a.ActionId) return;
            var c = CurrentCandidate(s, p.CandidateId);
            if (c != null && c.Status == RecruitCandidate.Registering) c.Status = RecruitCandidate.Available;
            s.Recruit.Pending = null;
        }

        private string RecruitCancelText(ActionState a)
        {
            var name = a.Label.StartsWith("登记同行：") || a.Label.StartsWith("登记候补：") ? a.Label.Substring(5).Split('（')[0] : "候选";
            return $"已取消登记{name}（实际进行了{Clock.Duration(a.ElapsedMinutes)}，已耗时间不退）；候选仍是同一人、属性不变，名额已释放。";
        }

        /// <summary>
        /// 登记完成（段末结算后，死亡已优先判过）：重验主角存活、仍在办理地点与名额（不含本笔），不过则按原规则失败释放；
        /// 再重验开工锁定的去向（B22g confirm：同行空位 / 该屋完工且从收容所合法可达），失败时登记停在“待安置确认”——不建人、不重收 10 分钟、不释放名额，
        /// 只能改选合法去向或取消。通过才建 actorId、标记候选已招，并 0 分钟送达所选去向（arrival）。旧档在办登记没有选择：默认营区广场候补、不自动入队。
        /// 物资不奖励（七装备槽与五物资卡空）。本笔事务只结算一次（行动只完成一次；requestId 重放在开始时已拦）。
        /// </summary>
        private void CompleteRecruit(GameState s, ActionState a)
        {
            var p = s.Recruit.Pending;
            if (p == null || p.ActionId != a.ActionId)
            {
                s.Recruit.Pending = null;
                Emit(s, GameEventKind.ActionCompleted, "登记没有完成：登记事务已失效。");
                return;
            }
            var cur = CurrentCandidate(s, p.CandidateId);
            var why = s.Progress.Dead ? "本局已结束" : s.CurrentLocation != p.LocationId ? $"不在{LocName(p.LocationId)}" : SharedQuotaBlocked(s, false);
            if (why != null)
            {
                s.Recruit.Pending = null;
                if (cur != null && cur.Status == RecruitCandidate.Registering) cur.Status = RecruitCandidate.Available;
                Emit(s, GameEventKind.ActionCompleted, $"登记没有完成：{why}。");
                return;
            }
            var placement = p.Placement ?? RecruitPlacement.Square;   // 旧档在办登记（shelter.compatibility）
            if (RecruitPlacementBlocked(s, placement) is string fail)
            {
                p.AwaitingPlacement = true;
                p.PlacementFailure = fail;
                Emit(s, GameEventKind.ActionCompleted,
                    // B38b（N02 附录 §4）
                    $"{p.Candidate.Name}的登记手续已办完。原定去向（{PlacementText(s, placement)}）目前无法安排。{Sentence(fail)}"
                    + "名额仍保留，人物尚未加入。可在收容所安全空闲时重选（0分钟），也可取消并释放名额。已用时间不退。");
                return;
            }
            s.Recruit.Pending = null;
            PlaceNewRecruit(s, p, placement, cur);
        }

        /// <summary>建人并 0 分钟送达（arrival）：不模拟路途、不扣费、不推进时间、不触发战斗；只转移这次新招、原本空装备的角色，不带任何物品。</summary>
        private RosterEntry PlaceNewRecruit(GameState s, RecruitTxn p, RecruitPlacement placement, RecruitCandidate? cur)
        {
            var r = CreateRandomRecruit(s, p.Candidate);
            if (cur != null) { cur.Status = RecruitCandidate.Recruited; cur.ActorId = r.ActorId; }
            string where;
            if (placement.IsParty)
            {
                ApplySetActive(s, r.ActorId, true);
                where = $"编入同行（{s.Party.Count}/{PartyCapacity(s)}）";
            }
            else if (placement.Safehouse is string loc)
            {
                r.ReserveSafehouse = loc;
                where = $"已0分钟送到{SafehouseName(s, loc)}屋内作候补（不走路途；要你实地进那间屋才能编入同行）";
            }
            else where = $"入名册作候补（在{LocName(HubLocation(s))}留守，到那里可编入同行）";
            var tpl = Rc.Template(p.Candidate.TemplateId);
            Emit(s, GameEventKind.ActionCompleted,
                $"{r.Name}登记完成，{where}：{tpl?.Name ?? ""}，{SkillName(p.Candidate.MainSkill)}{p.Candidate.MainLevel}级、{SkillName(p.Candidate.SubSkill)}{p.Candidate.SubLevel}级；"
                + $"初始装备与随身为空。随机/保底名册{SharedRoster(s).Count}/{SharedRosterCap(s)}。");
            return r;
        }

        /// <summary>待安置确认能不能处理（开发假设 B22g-3：与登记同一处——收容所安全空闲）；没有待安置为原因。null 为可以。</summary>
        public string? RecruitAwaitingBlocked(GameState s, string verb)
        {
            if (s.Recruit.Pending?.AwaitingPlacement != true) return "没有等待安置确认的登记";
            return RecruitServiceBlocked(s, verb);
        }

        /// <summary>
        /// 改选去向并完成待安置确认（confirm）：只允许合法去向（编入同行需同行空位；候补到营区广场或合格安全屋）；0 分钟、不重收 10 分钟；
        /// 名额重验（本笔已占）通过才建一人。requestId 幂等。失败时状态不变。
        /// </summary>
        public ActionResult ResolveRecruitPlacement(GameState s, RecruitPlacement? placement, string? requestId = null)
        {
            if (SeenRequest(s, requestId)) return ActionResult.Repeated();
            var why = RecruitAwaitingBlocked(s, "确认安置") ?? RecruitPlacementBlocked(s, placement) ?? SharedQuotaBlocked(s, false);
            if (why != null) return ActionResult.Fail(why);
            RememberRequest(s, requestId);
            var p = s.Recruit.Pending!;
            s.Recruit.Pending = null;
            PlaceNewRecruit(s, p, placement!, CurrentCandidate(s, p.CandidateId));
            return ActionResult.Success();
        }

        /// <summary>取消待安置确认（confirm）：不发人不发物，已过时间不退；释放候选（仍在本期名单里则回到可招）与名额。requestId 幂等。</summary>
        public ActionResult CancelRecruitPlacement(GameState s, string? requestId = null)
        {
            if (SeenRequest(s, requestId)) return ActionResult.Repeated();
            var why = RecruitAwaitingBlocked(s, "取消登记");
            if (why != null) return ActionResult.Fail(why);
            RememberRequest(s, requestId);
            var p = s.Recruit.Pending!;
            s.Recruit.Pending = null;
            var c = CurrentCandidate(s, p.CandidateId);
            if (c != null && c.Status == RecruitCandidate.Registering) c.Status = RecruitCandidate.Available;
            Emit(s, GameEventKind.Info, $"已取消{p.Candidate.Name}的登记：没有建人、没有发物，已过的{Clock.Duration(Rc.RegistrationMinutes)}登记时间不退；名额已释放。");
            return ActionResult.Success();
        }

        /// <summary>按候选快照一次初始化角色（RC3“招募提交时才创建玩家角色实体并初始化一次”），之后按 R08 真实状态走。</summary>
        private RosterEntry CreateRandomRecruit(GameState s, RecruitCandidate c)
        {
            var id = $"recruit_{s.Recruit.NextActorSeq++}";
            var skills = new Dictionary<string, int> { [c.MainSkill] = c.MainLevel, [c.SubSkill] = c.SubLevel };
            var a = NewRecruitActor(id, c.Name, skills, Rc.OtherLevel);
            a.Stats.HealthMax = c.HealthMax;
            a.Stats.Health = c.Health;
            a.Stats.EnergyMax = c.EnergyMax;
            a.Stats.Energy = c.Energy;
            a.Stats.Satiety = c.Satiety;
            a.Stats.Hydration = c.Hydration;
            a.Stats.Wakefulness = c.Wakefulness;
            a.Stats.Morale = c.Morale;
            a.Body = new BodyState { Immunity = c.Immunity, Infected = false, VirusLoad = c.VirusLoad };
            s.Actors[id] = a;
            var entry = new RosterEntry
            {
                ActorId = id, Kind = "random", Name = c.Name, JoinedMinute = s.GameMinute, CandidateId = c.CandidateId, Cycle = c.Cycle,
                TemplateId = c.TemplateId, ControlResist = c.ControlResist, Evasion = c.Evasion, BaseArmor = c.BaseArmor, BaseSpeed = c.BaseSpeed,
                Gender = c.Gender, SkinTone = c.SkinTone, HairStyle = c.HairStyle, HairColor = c.HairColor, FaceDetail = c.FaceDetail,
            };
            s.Roster.Add(entry);
            s.FormationVersion++;
            return entry;
        }

        // ───────────── 面板（RC3 候选卡、RC4 满额与退役） ─────────────

        /// <summary>收容所招募面板（0 分钟、只读；候选批次由到达/打开面板/世界 tick 生成，这里不生成）。</summary>
        public RecruitView RecruitPreview(GameState s)
        {
            var v = new RecruitView { Opened = RecruitOpened(s) };
            var k = SharedRosterCap(s);
            v.SharedCap = k;
            v.SharedUsed = SharedRoster(s).Count;
            // B22g 试玩修复 M439：在办登记/待安置确认另锁 1 个名额，计数行写明（只改显示）
            var lockNote = s.Recruit.Pending is RecruitTxn lp ? (lp.AwaitingPlacement ? "（另有1名待安置锁定1个名额）" : "（另有1笔登记在办锁定1个名额）") : "";
            v.QuotaText = $"随机/保底名册 {v.SharedUsed}/{k}{lockNote}（容量C{PartyCapacity(s)}：K=min({Rc.SharedRosterMax},C−1)；留守候补照常占名额，死亡或退役才释放）"
                          + $" · 全名册存活 {RosterAliveWithPlayer(s)}/{Rc.RosterAliveCap}（含你）";
            v.CostText = $"登记：0材料 · {Clock.Duration(Rc.RegistrationMinutes)}等待（两段，你与同行按等待消耗）· 占1个名额";
            v.Blocked = RecruitServiceBlocked(s, "办理");
            if (!v.Opened)
            {
                v.OpenText = s.Progress.Flags.Contains(Rc.OpenedByFact)
                    ? "营区登记后第一次实际安全到达收容所时开放周期候选。"
                    : "需要先完成营区登记（QMAP01），之后第一次实际到达收容所时开放周期候选。";
            }
            else
            {
                var start = s.Recruit.ShelterOpenedAt!.Value;
                v.Cycle = s.Recruit.Cycle;
                var next = NextRecruitRefresh(s)!.Value;
                v.OpenText = $"{Clock.DayTime(start)}开放；每{Clock.Duration(Rc.RefreshMinutes)}换一批{Rc.CandidatesPerCycle}名（只按世界时间，离线不走、不补以前的批次）。";
                v.CycleText = $"第{(s.Recruit.Cycle ?? 0) + 1}批 · 下一批：{Clock.DayTime(next)}（还有{Clock.Duration(next - s.GameMinute)}）；未招者到时离开，已招入的不受影响";
                foreach (var c in s.Recruit.Candidates) v.Candidates.Add(CandidateView(s, c));
            }
            if (s.Recruit.Pending is RecruitTxn p)
            {
                if (p.AwaitingPlacement)
                    v.Awaiting = new RecruitAwaitingView
                    {
                        CandidateName = p.Candidate.Name,
                        IdentityText = IdentityText(p.Candidate),
                        ChoiceText = PlacementText(s, p.Placement),
                        FailureText = p.PlacementFailure ?? "",
                        Blocked = RecruitAwaitingBlocked(s, "确认安置"),
                    };
                v.PendingText = p.AwaitingPlacement
                    ? $"待安置确认：{p.Candidate.Name}（登记已办完、人还没建；仍锁定1个名额，改选去向或取消，不会再收10分钟）"
                    : $"正在登记：{p.Candidate.Name}（{PlacementText(s, p.Placement)}；锁定1个名额；跨批次也保留到这笔结束）";
            }
            // B22g（Q183 selection）：登记后去向二选一（编入同行 / 安排候补到广场或合格安全屋）
            v.PartyOptionBlocked = RecruitPartyOptionBlocked(s);
            v.ReserveDestinations = RecruitReserveDestinations(s);
            foreach (var r in s.Roster.Where(r => r.IsGenerated && r.Present))
                v.Retirables.Add(RetireViewOf(s, r));
            return v;
        }

        private RecruitCandidateView CandidateView(GameState s, RecruitCandidate c)
        {
            var tpl = Rc.Template(c.TemplateId);
            // M381：本批里已招入后又退役的人，卡头写已退役（同周期不能再招回），不再显示“已招入名册”
            var retired = c.Status == RecruitCandidate.Recruited && c.ActorId != null && RosterOf(s, c.ActorId)?.Retired == true;
            var others = SkillsInPlay.Where(sk => sk.Id != c.MainSkill && sk.Id != c.SubSkill).Select(sk => sk.Name);
            return new RecruitCandidateView
            {
                CandidateId = c.CandidateId,
                Name = c.Name,
                TemplateName = tpl?.Name ?? c.TemplateId,
                TemplateUse = tpl?.Use ?? "",
                IdentityText = IdentityText(c),
                Status = retired ? "retired" : c.Status,
                StatusText = retired ? "已退役（本周期不能再招回）" : c.Status switch
                {
                    RecruitCandidate.Recruited => "已招入名册",
                    RecruitCandidate.Registering => "登记中",
                    _ => "周期候选",
                },
                SkillText = $"主：{SkillName(c.MainSkill)}{c.MainLevel}级 · 副：{SkillName(c.SubSkill)}{c.SubLevel}级 · 其余{Rc.OtherLevel}级（经验均为级内0）",
                BodyText = $"健康{Fmt.T(c.Health)}/{Fmt.T(c.HealthMax)} · 体力{Fmt.T(c.Energy)}/{Fmt.T(c.EnergyMax)} · 饱食{Fmt.T(c.Satiety)} 水分{Fmt.T(c.Hydration)} · 清醒{Fmt.T(c.Wakefulness)} 精神{Fmt.T(c.Morale)} 免疫{Fmt.T(c.Immunity)} · 未感染，没有特殊共生机制",
                CombatText = $"基础速度{Fmt.Speed(c.BaseSpeed)} · 控制抗性{Fmt.T(c.ControlResist)} · 闪避{Fmt.T(c.Evasion)} · 基础甲{Fmt.T(c.BaseArmor)} · 初始装备与随身：空",
                DetailText = $"{tpl?.Name}（{tpl?.Use}）。病毒{Fmt.T(c.VirusLoad)}、无出血/腹泻/药物冷却；其余熟练（{string.Join("、", others)}）均{Rc.OtherLevel}级。"
                             + "资料在本批内固定、不随时间变化，招入时按这份资料建人一次；职业图标不附带武器，枪械模板也要你自备枪弹，没武器时用拳。",
                Blocked = c.Status == RecruitCandidate.Available ? RecruitBlocked(s, c.CandidateId) : null,
            };
        }

        // ───────────── 退役（RC4、v3 校准；开发假设 B19-3/B19-4/B19-5） ─────────────

        /// <summary>某人的全部个人物品根实例：随身（五卡）与七个穿戴槽（锅内内容随锅）。</summary>
        private List<ItemInstance> PersonalItems(GameState s, string actorId)
        {
            var carried = ItemInstance.CarriedOwner(actorId);
            return s.Items.Where(i => i.Owner == carried || i.IsWornBy(actorId)).ToList();
        }

        /// <summary>
        /// 退役的只读预演：只适用于存活在册的生成角色（随机招入者、RB01 护送员；固定六人与主角不行）；在收容所安全空闲办理（开发假设 B19-3）；
        /// 本人物品有预留/处理中、或在战斗中都不能退役。null 为可以。
        /// </summary>
        public string? RetireBlocked(GameState s, string actorId)
        {
            if (actorId == GameState.PlayerId) return "你不能退役";
            var r = RosterOf(s, actorId);
            if (r == null) return "名册里没有这名角色";
            if (r.Dead) return $"{r.Name}已经死亡";
            if (r.Retired) return $"{r.Name}已退役";
            if (!r.IsGenerated) return $"{r.Name}是固定角色，不能退役（剧情资格与招募任务保留）";
            var why = RecruitServiceBlocked(s, "办理退役");
            if (why != null) return why;
            if (PersonalItems(s, actorId).Any(i => i.ReservedBy != null || IsLocked(s, i) || i.InFacility))
                return $"{r.Name}的物品正被预留或处理中，先结束相关行动再退役";
            return null;
        }

        private RetireView RetireViewOf(GameState s, RosterEntry r)
        {
            var items = PersonalItems(s, r.ActorId);
            var loans = items.Where(i => i.LoanSlot != null).Select(i => i.LoanSlot!).ToList();
            var kind = r.IsEscort ? "同行补员" : $"周期候选招入·{Rc.Template(r.TemplateId ?? "")?.Name ?? ""}";
            return new RetireView
            {
                ActorId = r.ActorId,
                Name = r.Name,
                KindLabel = kind,
                WhereText = s.Party.Contains(r.ActorId) ? "同行" : $"候补（{ReserveWhereName(s, r)}）",
                // M381：“本周期不能再招回、候选池不补人”只对周期候选招入者写；护送员只写与他相关的句子
                ConfirmText = $"{r.Name}将永久离开名册（已学技能不返还{(r.IsRandom ? "，本周期不能再招回，候选池不会补人" : "")}）；"
                              + (items.Count == 0 ? "身上没有个人物品。" : $"全部个人物品{items.Count}件（穿戴与随身）原样放进{LocName(Rc.LocationId)}认领箱，只能在这里实地取，保质照常、不刷新。")
                              + (loans.Count > 0 ? $"借用矛（{string.Join("、", loans)}）随物品进认领箱，仍归该借用位，不另发。" : "")
                              + "退役后释放1个随机/保底名额。",
                Blocked = RetireBlocked(s, r.ActorId),
            };
        }

        /// <summary>
        /// 退役（RC4）：0 时间（开发假设 B19-4）、原子；本人全部个人物品（穿戴与随身，锅内内容随锅）一次改 owner 到收容所认领箱（remains:&lt;actorId&gt;，类别 claim），
        /// 借用矛保留 loan_slot（归原借用额度、不新发、不销毁）；同行者离队并只清自己的号位；名册记退役（保留历史 actorId，不占名额、不结算身体）。requestId 幂等。
        /// </summary>
        public ActionResult Retire(GameState s, string actorId, string? requestId = null)
        {
            if (SeenRequest(s, requestId)) return ActionResult.Repeated();
            var why = RetireBlocked(s, actorId);
            if (why != null) return ActionResult.Fail(why);
            RememberRequest(s, requestId);
            var r = RosterOf(s, actorId)!;
            var items = PersonalItems(s, actorId);
            var owner = ItemInstance.RemainsOwner(actorId);
            foreach (var it in items)
            {
                it.Owner = owner;
                it.PlacedOrder = null;
            }
            if (items.Count > 0)
                s.Remains[actorId] = new RemainsState
                {
                    Name = r.Name, LocationId = Rc.LocationId, Minute = s.GameMinute, Cause = "退役寄存", PublicReserve = false, Kind = RemainsState.ClaimKind,
                };
            if (s.Party.Contains(actorId)) ApplySetActive(s, actorId, false);
            else s.FormationVersion++;
            if (s.Actors.TryGetValue(actorId, out var a)) a.ReserveMode = null;
            r.Retired = true;
            r.RetiredMinute = s.GameMinute;
            r.RetiredCycle = s.Recruit.Cycle;
            var loans = items.Where(i => i.LoanSlot != null).Select(i => i.LoanSlot!).ToList();
            // B38b（Q284）：同一条日志依次为退役句、寄存物品、寄存借用矛、退役后名册；没有的分支省略
            Emit(s, GameEventKind.Info, $"{r.Name}已退役，永久离开名册。已学技能不返还。"
                + (items.Count > 0 ? $"{items.Count}件个人物品已放进{LocName(Rc.LocationId)}认领箱，只能在这里取。" : "")
                + (loans.Count > 0 ? $"借用矛（{string.Join("、", loans)}）在认领箱里，仍归原借用位，不另发。" : "")
                + $"随机/保底名册{SharedRoster(s).Count}/{SharedRosterCap(s)}。");
            AfterSquadLeave(s, actorId);
            return ActionResult.Success();
        }

        // ───────────── 开发入口（?dev=1） ─────────────

        /// <summary>
        /// 开发入口：go 到收容所（记登记，按实际到达处理）；c2/c4 记容量事实；next 拨到下一批开始；edge 拨到本批结束前 5 分钟；escort 加护送员；
        /// B22g：house 建营外安全屋并回收容所；awaiting 直接造一笔待安置确认。
        /// </summary>
        public string DebugRecruitment(GameState s, string kind)
        {
            switch (kind)
            {
                case "go":
                {
                    var reg = Config.City.Registration;
                    s.Progress.Flags.Add(reg.AuthorizedFact);
                    s.Progress.Flags.Add(reg.RegisteredFact);
                    RefreshGates(s);
                    return DebugMapTeleport(s, Rc.LocationId) + "（开发：已记营区登记）";
                }
                case "c4":
                    foreach (var u in Config.Scope.Capacity.Unlocks)
                        foreach (var f in u.AllOf) s.Progress.Flags.Add(f);
                    return $"开发：容量记到C{PartyCapacity(s)}，随机/保底名额K={SharedRosterCap(s)}";
                case "next":
                case "edge":
                {
                    if (s.Main != null || s.Sub != null || s.Battle != null) return "开发：行动或战斗进行中，不能拨时间";
                    if (NextRecruitRefresh(s) is not int next) return "开发：收容所候选还没开放（先到收容所）";
                    var to = kind == "next" ? next : next - 5;
                    if (to <= s.GameMinute) return "开发：已在本批结束前5分钟以内";
                    s.GameMinute = to;
                    EnsureRecruitCycle(s);
                    return $"开发：时间拨到{Clock.DayTime(to)}（不结算身体）" + (kind == "next" ? $"，第{(s.Recruit.Cycle ?? 0) + 1}批候选已生成" : "；现在开始一笔10分钟登记会跨过换批时刻");
                }
                case "house":
                {
                    // B22g：在营外安全屋地块直接建成安全屋（实际到访、探索 100），再回到收容所，试“候补到安全屋”
                    const string site = "h05_homestead";
                    if (IsBusy(s)) return "开发：行动进行中或有敌踪/战斗，先结束再传送";
                    DebugSafehouseSite(s, site);
                    var built = DebugBuildSafehouse(s);
                    DebugRecruitment(s, "go");
                    return $"{built.TrimEnd('。')}；已回到{LocName(Rc.LocationId)}：候补下拉可选“{SafehouseName(s, site)}屋内”" +
                           (RecruitSafehouseBlocked(s, site) is string why ? $"（现在不可选：{why}）" : "");
                }
                case "awaiting":
                {
                    // B22g：跳过 10 分钟，直接造一笔“完成重验时同行已满”的待安置确认（只为点验改选/取消界面）
                    if (s.Recruit.Pending != null) return "开发：已有一笔登记在办或待安置确认";
                    var c = s.Recruit.Candidates.FirstOrDefault(x => x.Status == RecruitCandidate.Available);
                    if (c == null) return "开发：本批没有可登记的候选";
                    if (SharedQuotaBlocked(s, true) is string quota) return "开发：" + quota;
                    c.Status = RecruitCandidate.Registering;
                    s.Recruit.Pending = new RecruitTxn
                    {
                        ActionId = -1, CandidateId = c.CandidateId, Cycle = c.Cycle, LocationId = Rc.LocationId, StartMinute = s.GameMinute,
                        Candidate = c.Clone(), Placement = RecruitPlacement.Party, AwaitingPlacement = true, PlacementFailure = "开发：模拟完成重验时同行已满",
                    };
                    return $"开发：{c.Name}的登记停在待安置确认（原选编入同行）；在收容所杜穗“事项”里改选去向或取消";
                }
            }
            return "开发：未知操作";
        }
    }
}
