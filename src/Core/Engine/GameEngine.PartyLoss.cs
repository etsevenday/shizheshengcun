using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// 队友死亡与遗物、RB01 同行补员与借用矛（R08 §4、A11 §7/§8；开发计划 v0.9 B8 M185）。
    /// </summary>
    public sealed partial class GameEngine
    {
        // ───────────── 死亡与遗物（R08 §4、A11 §8） ─────────────

        /// <summary>把死者全部穿戴与随身实例一次改 owner 为 remains:&lt;id&gt;（锅内内容随锅）；有物品才建遗物容器。返回迁移件数。</summary>
        private int MoveToRemains(GameState s, ActorState actor, string where, string cause, int? battleId)
        {
            var carried = ItemInstance.CarriedOwner(actor.Id);
            var owner = ItemInstance.RemainsOwner(actor.Id);
            var items = s.Items.Where(i => i.Owner == carried || i.IsWornBy(actor.Id)).ToList();
            foreach (var it in items)
            {
                it.Owner = owner;
                it.PlacedOrder = null;
            }
            if (items.Count == 0) return 0;
            if (!s.Remains.ContainsKey(actor.Id))
                s.Remains[actor.Id] = new RemainsState { Name = ActorLabel(actor), LocationId = where, Minute = s.GameMinute, Cause = cause, BattleId = battleId };
            return items.Count;
        }

        /// <summary>遗物容器里的物品（根物品；锅内内容随锅）。</summary>
        public List<ItemInstance> RemainsItems(GameState s, string deadId) =>
            s.Items.Where(i => i.Owner == ItemInstance.RemainsOwner(deadId)).OrderBy(i => i.InstanceId).ToList();

        /// <summary>当前地点的遗物（有物品的）。</summary>
        public List<KeyValuePair<string, RemainsState>> RemainsHere(GameState s) =>
            s.Remains.Where(kv => kv.Value.LocationId == s.CurrentLocation && RemainsItems(s, kv.Key).Count > 0).OrderBy(kv => kv.Value.Minute).ToList();

        /// <summary>从遗物取出一件（整堆）到你的随身（五卡与 30kg 校验）或当前地面的只读预演；null 为可以。</summary>
        public string? TakeFromRemainsBlocked(GameState s, string deadId, int instanceId, bool toGround)
        {
            if (s.Progress.Dead) return "本局已结束";
            if (!s.Remains.TryGetValue(deadId, out var rem)) return "这里没有这份遗物";
            if (s.Battle != null || s.Encounter != null) return "战斗结束后才能整理遗物";
            if (rem.LocationId != s.CurrentLocation) return $"遗物在{LocName(rem.LocationId)}，要到那里才能取";
            var it = FindItem(s, instanceId);
            if (it == null || it.Owner != ItemInstance.RemainsOwner(deadId)) return "这件物品已不在遗物里";
            if (!toGround && PickupBlockedText(s, it, it.Count) is string why) return why;
            return null;
        }

        /// <summary>从遗物取出（0 时间、原子、requestId 幂等）。容器取空后从地点上移除，不再生成。</summary>
        public ActionResult TakeFromRemains(GameState s, string deadId, int instanceId, bool toGround, string? requestId = null)
        {
            if (SeenRequest(s, requestId)) return ActionResult.Repeated();
            var why = TakeFromRemainsBlocked(s, deadId, instanceId, toGround);
            if (why != null) return ActionResult.Fail(why);
            RememberRequest(s, requestId);
            var it = FindItem(s, instanceId)!;
            var dest = toGround ? s.CurrentLocation : ItemInstance.Carried;
            it.Owner = dest;
            Normalize(s, dest);
            var name = s.Remains[deadId].Name;
            if (RemainsItems(s, deadId).Count == 0) s.Remains.Remove(deadId);
            Emit(s, GameEventKind.Info, $"从{name}的遗物取出{ItemName(it.ItemId)}" + (toGround ? "，放在地面。" : "。"));
            return ActionResult.Success();
        }

        // ───────────── RB01 同行补员与借用矛（A11 §7、R08 §4/RC1） ─────────────

        /// <summary>RB01 登记用的等待行动标记（30 分钟 wait，完成时重验条件）。</summary>
        public const string EscortRegistrationTarget = "rb01";

        /// <summary>同时存活通用护送员上限 min(3, C−1)。</summary>
        public int EscortCap(GameState s) => Math.Max(0, Math.Min(Config.Party.EscortTemplate.MaxAlive, PartyCapacity(s) - 1));

        public List<RosterEntry> AliveEscorts(GameState s) => s.Roster.Where(r => r.IsEscort && !r.Dead).ToList();

        /// <summary>已解锁的借用矛位 loan_1..loan_{C−1}（最多 3）。</summary>
        public List<string> LoanSlotIds(GameState s) => Enumerable.Range(1, EscortCap(s)).Select(k => "loan_" + k).ToList();

        /// <summary>登记条件（不含“正在等待登记”这一行动本身）：营区登记、同行有空位、没有可直接编入的存活候补、护送员未满。</summary>
        private string? EscortConditionBlocked(GameState s)
        {
            if (!s.Progress.Flags.Contains("hub_registered")) return "需要先完成营区登记（QMAP01）";
            var cap = PartyCapacity(s);
            if (s.Party.Count >= cap) return $"同行没有空位（{s.Party.Count}/{cap}）";
            var res = Reserves(s);
            if (res.Count > 0) return $"有可直接编入的存活候补：{string.Join("、", res.Select(r => r.Name))}";
            if (AliveEscorts(s).Count >= EscortCap(s)) return $"存活的通用护送员已达上限 {EscortCap(s)}（C−1）";
            return null;
        }

        public string? EscortRegistrationBlocked(GameState s) => PartyEditBlocked(s)?.Replace("编队", "登记补员") ?? EscortConditionBlocked(s);

        /// <summary>开始 RB01 登记：主行动 30 分钟 wait（全队按 wait 消耗），完成时重验条件并只建一人；中途取消不建人。</summary>
        public ActionResult StartEscortRegistration(GameState s, string? requestId = null)
        {
            if (SeenRequest(s, requestId)) return ActionResult.Repeated();
            var why = EscortRegistrationBlocked(s);
            if (why != null) return ActionResult.Fail(why);
            var req = ActionRequest.Wait(Config.Party.EscortTemplate.RegistrationMinutes);
            req.RequestId = requestId;
            var res = Start(s, req);
            if (!res.Ok) return res;
            s.Main!.TargetId = EscortRegistrationTarget;
            s.Main.Label = $"登记同行补员（{Clock.Duration(Config.Party.EscortTemplate.RegistrationMinutes)}）";
            return res;
        }

        private void CompleteEscortRegistration(GameState s, ActionState a)
        {
            var why = s.Progress.Dead ? "本局已结束" : !AtHub(s) ? "不在营区" : EscortConditionBlocked(s);
            if (why != null)
            {
                Emit(s, GameEventKind.ActionCompleted, $"同行补员登记没有完成：{why}。");
                return;
            }
            var r = CreateEscort(s, true);
            Emit(s, GameEventKind.ActionCompleted, $"{r.Name}登记完成，编入同行（技能均1级、速度{Fmt.T(Config.Party.EscortTemplate.BaseSpeed)}）。{LoanNote(s, r)}");
        }

        /// <summary>建一名通用护送员（RB01）：通用初值、15 门技能均 1、绑定第一个没有存活护送员的借用位；该位已有有效矛则不另发。</summary>
        private RosterEntry CreateEscort(GameState s, bool joinParty)
        {
            var t = Config.Party.EscortTemplate;
            var seq = s.NextEscortSeq++;
            var id = "escort_" + seq;
            var name = $"{t.NamePrefix}#{seq}";
            var used = new HashSet<string>(AliveEscorts(s).Select(e => e.LoanSlot ?? ""));
            s.Actors[id] = NewRecruitActor(id, name, null, t.SkillLevel);
            var slot = LoanSlotIds(s).FirstOrDefault(x => !used.Contains(x)) ?? "loan_1";
            var entry = new RosterEntry { ActorId = id, Kind = "escort", Name = name, JoinedMinute = s.GameMinute, LoanSlot = slot };
            s.Roster.Add(entry);
            if (ValidLoan(s, slot) == null)
            {
                var spear = NewLoanSpear(s, slot, ItemInstance.EquipOwner(id, EquipSlots.Weapon1));
                s.LoanSlots[slot] = spear.InstanceId;
            }
            if (joinParty && s.Party.Count < PartyCapacity(s)) ApplySetActive(s, id, true);
            return entry;
        }

        /// <summary>借用位当前的有效实例（存在且耐久 &gt; 0）；没有为 null。</summary>
        public ItemInstance? ValidLoan(GameState s, string slot) =>
            s.LoanSlots.TryGetValue(slot, out var iid) && FindItem(s, iid) is ItemInstance it && (it.Durability ?? 1) > 0 ? it : null;

        /// <summary>按固定基础模板生成一件借用矛：白色无词条、满耐久取模板最大值（不抽随机耐久，A11 §7）。</summary>
        private ItemInstance NewLoanSpear(GameState s, string slot, string owner)
        {
            var itemId = Config.Party.EscortTemplate.LoanWeapon;
            var cfg = _items[itemId];
            var inst = new ItemInstance
            {
                InstanceId = s.NextInstanceId++,
                ItemId = itemId,
                Count = 1,
                Owner = owner,
                Quality = cfg.IsEquipment ? "white" : null,
                LoanSlot = slot,
                Source = new ItemSource { GrantEventId = "loan:" + slot },
            };
            if (cfg.HasDurability && cfg.DurabilityMaxRange != null && cfg.DurabilityMaxRange.Length > 0)
            {
                inst.MaxDurability = cfg.DurabilityMaxRange.Max();
                inst.Durability = inst.MaxDurability;
            }
            s.Items.Add(inst);
            return inst;
        }

        private string LoanNote(GameState s, RosterEntry r)
        {
            if (r.LoanSlot == null) return "";
            var it = ValidLoan(s, r.LoanSlot);
            if (it == null) return "";
            if (it.IsWornBy(r.ActorId)) return $"武器Ⅰ为借用{ItemName(it.ItemId)}（{r.LoanSlot}）。";
            return $"该借用位的{ItemName(it.ItemId)}已有一件，在{LoanWhere(s, it)}，请去取回，不另发。";
        }

        /// <summary>借用物现在在哪（文案）。</summary>
        public string LoanWhere(GameState s, ItemInstance it)
        {
            if (it.InRemains)
            {
                var dead = it.Owner.Substring(ItemInstance.RemainsPrefix.Length);
                return s.Remains.TryGetValue(dead, out var rem) ? $"{rem.Name}的遗物里（{LocName(rem.LocationId)}）" : "遗物里";
            }
            if (ItemInstance.TryEquip(it.Owner, out var aid, out _)) return $"{ActorDisplayName(s, aid)}身上";
            if (it.IsCarried) return "你的随身";
            if (it.Owner.StartsWith(ItemInstance.Carried + ":")) return $"{ActorDisplayName(s, it.Owner.Substring(ItemInstance.Carried.Length + 1))}的随身";
            var place = PlaceOf(s, it);
            return _locs.ContainsKey(place) ? $"{LocName(place)}地面" : place;
        }

        /// <summary>
        /// 报失借用矛的只读预演（A11 §7、NP-A20）：只在营区安全空闲；被预留/处理中、在存活人物身上拒绝；仍可回收（地面、遗物、容器）只指路；
        /// 实例已不存在或耐久为 0 才可报失。
        /// </summary>
        public string? ReportLostLoanBlocked(GameState s, string slot)
        {
            var why = PartyEditBlocked(s);
            if (why != null) return why.Replace("编队", "报失");
            if (!s.LoanSlots.TryGetValue(slot, out var iid)) return "这个借用位还没有发过借用矛";
            var it = FindItem(s, iid);
            if (it == null) return null;
            if (it.ReservedBy != null || IsLocked(s, it) || it.InFacility) return "这件借用矛正被预留或处理中，不能报失";
            if (ItemInstance.TryEquip(it.Owner, out _, out _) || it.IsCarried || it.Owner.StartsWith(ItemInstance.Carried + ":"))
                return $"借用矛在{LoanWhere(s, it)}，不能报失";
            if ((it.Durability ?? 1) > 0) return $"借用矛还在{LoanWhere(s, it)}，可以去取回，不能报失";
            return null;
        }

        /// <summary>报失：同一事务撤销旧实例（之后任何拾取/拆解/出售入口都不认）并按固定模板补发一件到营区地面；requestId 重放不双发。</summary>
        public ActionResult ReportLostLoan(GameState s, string slot, string? requestId = null)
        {
            if (SeenRequest(s, requestId)) return ActionResult.Repeated();
            var why = ReportLostLoanBlocked(s, slot);
            if (why != null) return ActionResult.Fail(why);
            RememberRequest(s, requestId);
            var old = FindItem(s, s.LoanSlots[slot]);
            if (old != null)
            {
                s.Items.Remove(old);
                foreach (var kv in s.Remains.Where(kv => RemainsItems(s, kv.Key).Count == 0).ToList()) s.Remains.Remove(kv.Key);
            }
            var spear = NewLoanSpear(s, slot, s.CurrentLocation);
            s.LoanSlots[slot] = spear.InstanceId;
            Emit(s, GameEventKind.Info, $"借用矛（{slot}）已报失：旧的作废，新的一件放在{LocName(s.CurrentLocation)}地面。");
            return ActionResult.Success();
        }

        // ───────────── 开发入口（B8-19） ─────────────

        /// <summary>开发入口：加入一名通用护送员（跳过营区登记、地点与 30 分钟；仍守存活上限 C−1，满员进候补）。</summary>
        public string DebugAddEscort(GameState s)
        {
            if (AliveEscorts(s).Count >= EscortCap(s)) return $"存活护送员已达上限 {EscortCap(s)}（C−1）";
            var r = CreateEscort(s, true);
            return $"{r.Name}已加入" + (s.Party.Contains(r.ActorId) ? "并编入同行" : "（同行已满，留作候补）");
        }

        /// <summary>开发入口：改所选队友的状态（health1 / virus79 / food41 / kill / spear0）。</summary>
        public string DebugActor(GameState s, string actorId, string what)
        {
            if (actorId == GameState.PlayerId || !s.Actors.TryGetValue(actorId, out var a) || !(RosterOf(s, actorId) is RosterEntry r) || r.Dead) return "请选一名存活队友";
            switch (what)
            {
                case "health1": a.Stats.Health = 1; return $"{r.Name}健康→1";
                case "virus79": a.Body.Infected = true; a.Body.VirusLoad = 79; return $"{r.Name}病毒→79";
                case "food41": a.Stats.Satiety = 41; a.Stats.Hydration = 41; return $"{r.Name}饱食与水分→41";
                case "kill": CompanionDown(s, a, "开发入口"); return $"{r.Name}已死亡";
                case "spear0":
                    var w = s.Items.FirstOrDefault(i => i.LoanSlot != null && (i.IsWornBy(actorId) || i.Owner == ItemInstance.CarriedOwner(actorId)))
                            ?? s.Items.FirstOrDefault(i => i.IsWornBy(actorId) && i.Durability != null);
                    if (w == null) return "身上没有可打坏的武器";
                    w.Durability = 0;
                    return $"{r.Name}的{ItemName(w.ItemId)}耐久→0";
            }
            return "未知操作";
        }
    }
}
