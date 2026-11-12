using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// 安全屋（S09 X2.1—X2.9、S13「安全屋设施恢复与候补照护边界」、A08 永久安全屋白名单 54 地点；开发计划 v0.9 B17，R52—R55）：
    /// 许可只读节点显式 buildAllowed.safehouse（未配置为 false），土地/地牢禁建原因按 Q118/Q134 在住宅入口写；
    /// 开工须已到访、探索 100、营区已登记、当地没有存活敌踪、每地点一间、全队在外部区；状态 not_started→building→built；
    /// 完工即有门（0 分钟进出）、屋内遮蔽、休息精神额外+2/小时与 200kg 固定柜，床/火堆/工坊另建在屋内区。
    /// </summary>
    public sealed partial class GameEngine
    {
        public const string SafehouseNotStarted = "not_started";
        public const string SafehouseBuildingStatus = "building";
        public const string SafehouseBuiltStatus = "built";

        public RecipeConfig SafehouseRecipe => _recipes[Config.Safehouse.BuildRecipe];

        public SafehouseState? SafehouseAt(GameState s, string locationId) =>
            s.Locations.TryGetValue(locationId, out var l) ? l.Safehouse : null;

        public bool SafehouseBuilt(GameState s, string locationId) => SafehouseAt(s, locationId)?.Built == true;

        /// <summary>该地点正在建安全屋：主行动是安全屋基础的建造且在此地开工（暂停也算）；取消即回到未开工。</summary>
        public bool SafehouseBuilding(GameState s, string locationId) =>
            s.Main is ActionState a && a.Type == ActionType.Build && a.TargetId == Config.Safehouse.BuildRecipe && a.SourceLocation == locationId;

        /// <summary>not_started / building / built（S09 X2.4）。</summary>
        public string SafehouseStatus(GameState s, string locationId) =>
            SafehouseBuilt(s, locationId) ? SafehouseBuiltStatus : SafehouseBuilding(s, locationId) ? SafehouseBuildingStatus : SafehouseNotStarted;

        /// <summary>安全屋显示名：玩家改过名用改的，否则“××安全屋”。</summary>
        public string SafehouseName(GameState s, string locationId)
        {
            var sh = SafehouseAt(s, locationId);
            return sh != null && !string.IsNullOrWhiteSpace(sh.Name) ? sh.Name : LocName(locationId) + "安全屋";
        }

        /// <summary>
        /// 地点本身的建屋许可（与人、时间无关）：地牢内/势力/公共/任务保留地写 Q118 原因（Q134：住宅入口按实际禁建原因显示）；
        /// 其余只认节点显式 buildAllowed.safehouse=true（A08 白名单 54 处，未配置一律 false），不凭室内/安全/区域名推定。可建为 null。
        /// </summary>
        public string? SafehousePermissionBlocked(string locationId)
        {
            if (!_locs.TryGetValue(locationId, out var lc)) return "地点不存在";
            if (lc.PrivateBuildForbidden is string forbidden) return forbidden;
            if (lc.WorldKind != "overworld") return "只能在普通主世界地点建安全屋";
            if (!lc.BuildAllowedSafehouse) return "这里不在可建安全屋的地点名单里";
            return null;
        }

        /// <summary>
        /// 在当前地点开工建安全屋的全部不合格原因（S09 X2.1/X2.2，按地点许可→营区登记（首次登记解锁安全屋知识，B17 试玩修复 E2 排第一）→到访→探索→已有/施工→敌踪→所在区；
        /// 界面与开工校验一次列全）。合格为空表。
        /// 只查地点与世界条件；材料、制造等级、行动占用由开工校验另报。
        /// </summary>
        public List<string> SafehouseSiteReasons(GameState s, string locationId)
        {
            var list = new List<string>();
            if (SafehousePermissionBlocked(locationId) is string perm) { list.Add(perm); return list; }
            var ls = s.Locations[locationId];
            if (!s.Progress.Flags.Contains(Config.Safehouse.RegisteredFact)) list.Add("要先在幸存者营区登记（解锁安全屋建造）");
            if (!ls.Visited) list.Add($"要先实际到访{LocName(locationId)}");
            var need = Config.Safehouse.RequiredExplore;
            if (ls.Progress < need) list.Add($"{LocName(locationId)}探索度{ls.Progress}%，要先探索到{need}%");
            if (SafehouseBuilt(s, locationId)) list.Add($"{LocName(locationId)}已有安全屋（每个地点最多一间）");
            else if (SafehouseBuilding(s, locationId)) list.Add($"{LocName(locationId)}的安全屋正在施工");
            var hostiles = LocalHostiles(s, locationId).Count;
            if (hostiles > 0) list.Add($"先清掉{LocName(locationId)}的敌踪（还有{hostiles}个）");
            if (s.CurrentLocation == locationId && s.CurrentZone == SafehouseZone) list.Add("在屋内不能再建安全屋，先出门");
            return list;
        }

        /// <summary>
        /// 建造配方（有 facilityResult 的：遮蔽棚、床垫、工坊、安全屋与屋内床/火）在当前地点与所在区的位置原因；可建为 null。
        /// 开工与完工都调用（Q107“地点/区校验开工与完工都重验”）。
        /// </summary>
        public string? BuildSiteBlocked(GameState s, RecipeConfig rc)
        {
            var loc = s.CurrentLocation;
            var lc = _locs[loc];
            var inside = s.CurrentZone == SafehouseZone;
            var place = s.Ground;
            var fac = FacOf(s, place) ?? s.Locations[loc].Facility;
            var result = rc.FacilityResult!;
            if (result == "safehouse")
            {
                var reasons = SafehouseSiteReasons(s, loc);
                return reasons.Count > 0 ? string.Join("；", reasons) : null;   // B17 试玩修复 E1：一次列全
            }
            if (rc.Group == "safehouse")
            {
                if (!inside || !SafehouseBuilt(s, loc)) return "只能在已建成的安全屋屋内建造（先进入安全屋）";
                if (FacilityFlag(fac, result)) return result == "fireBuilt" ? $"{ZoneName(s, place)}已有火堆（每区一处）" : $"{ZoneName(s, place)}已铺好床垫";
                return null;
            }
            if (IsWildFireRecipe(rc))
            {
                // Q173 按 B（B22e）：普通野外清单外部区、已到访、本区无火源；不扩大屋内/工坊/公共设施的建造权限
                var wild = WildFireSiteReasons(s);
                return wild.Count > 0 ? string.Join("；", wild) : null;
            }
            if (rc.Group == "workshop")
            {
                if (inside ? !SafehouseBuilt(s, loc) : !CanBuildWorkshopsAt(loc)) return WorkshopBuildOnly;
                if (FacilityFlag(fac, result)) return $"{ZoneName(s, place)}已有{rc.Name.Replace("建", "")}（每区每类一座）";
                return null;
            }
            // 遮蔽棚、营地床垫：只在临时营地外部区（屋内已有遮蔽，另铺屋内床垫）
            if (inside || !lc.CanBuildShelter) return BuildOnlyAtCamp(lc);
            if (FacilityFlag(fac, result)) return "已建造";
            return null;
        }

        /// <summary>完工时的位置重验：安全屋只验许可、未重复建与仍在外部区（施工中的敌踪变化不让完工失败）；其余同开工。</summary>
        private string? BuildSiteBlockedAtCompletion(GameState s, RecipeConfig rc)
        {
            if (rc.FacilityResult != "safehouse") return BuildSiteBlocked(s, rc);
            var loc = s.CurrentLocation;
            if (SafehouseBuilt(s, loc)) return $"{LocName(loc)}已经有安全屋（未重复建造）";
            if (s.CurrentZone == SafehouseZone) return "不在外部区";
            return SafehousePermissionBlocked(loc);
        }

        /// <summary>自建工坊（含陶窑、固定净水台）的位置说明（Q107 选 A、Q134：屋内工坊开放后按真实可用范围提示）。</summary>
        public const string WorkshopBuildOnly = "只能在临时营地或已建成的安全屋屋内建造";

        /// <summary>完工时提交建造设施（安全屋基础建屋与首个柜；其他写在当前区设施上）。幂等：已有的不重复建。</summary>
        private void CommitFacility(GameState s, RecipeConfig rc)
        {
            var loc = s.CurrentLocation;
            if (rc.FacilityResult == "safehouse")
            {
                var ls = s.Locations[loc];
                if (ls.Safehouse?.Built == true) return;
                ls.Safehouse = new SafehouseState { Id = ItemInstance.SafehouseIdOf(loc), Built = true, BuiltMinute = s.GameMinute };
                return;
            }
            var fac = FacOf(s, s.Ground) ?? s.Locations[loc].Facility;
            SetFacility(fac, rc.FacilityResult!);
            if (rc.FacilityResult == "fireBuilt")
            {
                fac.FacilityId = FireIdOf(s.Ground);
                // 新建火堆初始熄灭、零燃料、无锅（S09 X2.3；Q173 fixedFire initialLit/initialFuelMinutes/initialPot）
                fac.FireLit = false;
                fac.WentOut = false;
                fac.FuelMinutes = 0;
                SyncFacilities(s);
            }
        }

        /// <summary>建成提示。</summary>
        private string? FacilityDoneText(GameState s, RecipeConfig rc) => rc.FacilityResult switch
        {
            "safehouse" => $"{SafehouseName(s, s.CurrentLocation)}建成：可以“进入安全屋”（0分钟）；屋内遮蔽，休息时精神每小时额外{Fmt.SignedT(Config.FacilityEffects.ShelteredCampRestMoralePerHour)}，" +
                           $"有{Config.Safehouse.StorageCapacityGrams / 1000}公斤固定储物柜；床垫、火堆与工坊要在屋内另建。",
            "fireBuilt" when IsWildFireRecipe(rc) => $"{LocName(s.CurrentLocation)}野外火堆建成：初始熄灭、燃料0、没有锅，点火仍需打火器（燃料上限{Config.Fire.MaxFuelMinutes}分钟、" +
                           $"{Config.Fire.ProcessingSlots}个加工位、最多{Config.Fire.MaxPots}口锅）；火堆不能拾取或拆除，也不挡遭遇。",
            "fireBuilt" => $"{ZoneName(s, s.Ground)}火堆建成：初始熄灭、燃料0、没有锅，点火仍需打火器。",
            "bedBuilt" when rc.Group == "safehouse" => $"{ZoneName(s, s.Ground)}床垫铺好：屋内睡眠体力恢复每小时额外{Fmt.SignedT(Config.FacilityEffects.BedExtraSleepEnergyPerHour)}。",
            _ => null,
        };

        // ───────────────────────── 门：0 分钟进出（S09 X2.5） ─────────────────────────

        /// <summary>现在能不能进/出当前地点的安全屋；能为 null。门不是撤退按钮：战斗、敌踪待选、行动中、未读对白都不行。</summary>
        public string? SafehouseDoorBlocked(GameState s, bool enter)
        {
            if (s.Progress.Dead) return "本局已结束";
            var loc = s.CurrentLocation;
            if (enter)
            {
                if (s.CurrentZone == SafehouseZone) return "已在安全屋屋内";
                if (!SafehouseBuilt(s, loc)) return SafehouseBuilding(s, loc) ? "安全屋还在施工，完工后才能进入" : $"{LocName(loc)}没有建成的安全屋";
            }
            else if (s.CurrentZone != SafehouseZone) return "不在安全屋屋内";
            if (DialogueLocked(s)) return DialogueLockedReason;
            if (s.Battle != null) return "战斗中不能进出安全屋（门不是撤退按钮）";
            if (s.Encounter != null) return "敌踪待处理：先接敌或绕行，不能靠进出门跳过";
            if (s.Main != null || s.Sub != null) return "行动进行中，先完成或取消再进出安全屋";
            return null;
        }

        /// <summary>进入安全屋：0 分钟，全队一起切到屋内区（S09 X2.5）。</summary>
        public ActionResult EnterSafehouse(GameState s)
        {
            if (SafehouseDoorBlocked(s, true) is string why) return ActionResult.Fail(why);
            s.CurrentZone = SafehouseZone;
            RecordSafehouse(s, s.CurrentLocation);
            Emit(s, GameEventKind.Info, $"进入{SafehouseName(s, s.CurrentLocation)}（0分钟）：屋内不会遇到普通遭遇；时间、饮食与伤病照常推进。");
            UpdateFlags(s);
            return ActionResult.Success();
        }

        /// <summary>出门：0 分钟，全队一起回到外部区；外部敌踪、资源照常（S09 X2.5）。</summary>
        public ActionResult ExitSafehouse(GameState s)
        {
            if (SafehouseDoorBlocked(s, false) is string why) return ActionResult.Fail(why);
            RecordSafehouse(s, s.CurrentLocation);
            s.CurrentZone = ExteriorZone;
            var hostiles = LocalHostiles(s, s.CurrentLocation).Count;
            Emit(s, GameEventKind.Info, $"走出{SafehouseName(s, s.CurrentLocation)}，回到{LocName(s.CurrentLocation)}外部（0分钟）" +
                (hostiles > 0 ? $"；外面有{hostiles}个敌踪。" : "。"));
            UpdateFlags(s);
            EnemyApproachBoundary(s);   // B41b：出门回到外部区即与外部敌踪同地同区（空闲边界）
            return ActionResult.Success();
        }

        /// <summary>在屋内时只能做屋内的事：移动、探索、采集/搜集、清理、勘整、绕行要先出门（S09 X2.5）。可以做为 null。</summary>
        public string? InsideSafehouseBlocked(GameState s, ActionType type)
        {
            if (s.CurrentZone != SafehouseZone) return null;
            string? verb = type switch
            {
                ActionType.Travel => "离开",
                ActionType.Explore => "探索",
                ActionType.Gather => "采集或搜集",
                ActionType.Clear => "清理",
                ActionType.Survey => "勘整",
                ActionType.Bypass => "绕行",
                _ => null,
            };
            return verb == null ? null : $"在安全屋屋内：先出门再{verb}";
        }

        /// <summary>更新柜子的“上次记录”（在屋内时随时刷新；远处只读这份快照）。</summary>
        public void RecordSafehouse(GameState s, string locationId)
        {
            var sh = SafehouseAt(s, locationId);
            if (sh == null || !sh.Built) return;
            var owner = ItemInstance.SafehouseStorageOwner(sh.Id);
            sh.LastRecord = s.Items.Where(i => i.Owner == owner).OrderBy(i => i.ItemId).ThenBy(i => i.InstanceId)
                .GroupBy(i => i.ItemId)
                .Select(g => new SafehouseRecordLine { ItemId = g.Key, Name = ItemName(g.Key), Count = g.Sum(i => i.Count), Grams = g.Sum(i => ItemGrams(s, i)) })
                .ToList();
            sh.LastRecordMinute = s.GameMinute;
        }
        // ───────────────────────── 收藏、改名与返程定位（S09 X2.5、X2.9，R55） ─────────────────────────

        /// <summary>收藏/取消收藏“常用安全屋”：0 分钟、只改展示（不改开局点、复活点或地图边）。</summary>
        public ActionResult SetFavoriteSafehouse(GameState s, string? locationId)
        {
            if (locationId != null && !SafehouseBuilt(s, locationId)) return ActionResult.Fail("只能收藏已建成的安全屋");
            s.FavoriteSafehouse = locationId;
            Emit(s, GameEventKind.Info, locationId == null ? "已取消常用安全屋。" : $"已把{SafehouseName(s, locationId)}设为常用安全屋（只是收藏，不改出生/复活点，回去仍要沿路线步行）。");
            return ActionResult.Success();
        }

        /// <summary>给安全屋改名：0 分钟、只改展示；空名恢复默认。</summary>
        public ActionResult RenameSafehouse(GameState s, string locationId, string name)
        {
            var sh = SafehouseAt(s, locationId);
            if (sh == null || !sh.Built) return ActionResult.Fail("只能给已建成的安全屋改名");
            name = (name ?? "").Trim();
            if (name.Length > 12) return ActionResult.Fail("名字最多12个字");
            sh.Name = name;
            Emit(s, GameEventKind.Info, $"安全屋改名为“{SafehouseName(s, locationId)}”。");
            return ActionResult.Success();
        }

        /// <summary>
        /// 回安全屋的已知物理路线（只读）：沿已开通、两端已知的边按基础分钟最短；不自动寻路、不传送、不省时间（S09 X2.5）。
        /// 已在当地为空表；走不到为 null。
        /// </summary>
        public List<MapPathStep>? SafehouseRoute(GameState s, string locationId) =>
            SafehouseBuilt(s, locationId) ? MapPathTo(s, locationId) : null;

        /// <summary>返程定位文字：“××安全屋：已在这里”“路线：A→B（30分钟）→C（20分钟），合计约50基础分钟；要自己沿路线逐段走”或走不到的原因。</summary>
        public string SafehouseRouteText(GameState s, string locationId)
        {
            var name = SafehouseName(s, locationId);
            var path = SafehouseRoute(s, locationId);
            if (path == null) return $"{name}：现在没有已知可走的路线";
            if (path.Count == 0) return $"{name}：就在当前地点" + (s.CurrentZone == SafehouseZone ? "（已在屋内）" : "（点“进入安全屋”，0分钟）");
            var legs = string.Join("", path.Select(p => $"→{LocName(p.To)}（{p.Minutes}分钟）"));
            return $"{name}：路线 {LocName(path[0].From)}{legs}，合计约{path.Sum(p => p.Minutes)}基础分钟（实际按负重、夜间等折算）；要自己沿路线逐段走，不会自动寻路或传送";
        }
        // ───────────────────────── 开发入口（?dev=1，B17） ─────────────────────────

        /// <summary>开发：传送到营外安全屋地块 h05_homestead（按实际到达处理，到访即探索 100），并记下营区登记（只为建屋资格，不发物资）。</summary>
        public string DebugSafehouseSite(GameState s, string locationId = "h05_homestead")
        {
            if (IsBusy(s)) return "开发：行动进行中或有敌踪/战斗，先结束再传送";
            var text = s.CurrentLocation == locationId ? $"开发：已在{LocName(locationId)}" : DebugMapTeleport(s, locationId).TrimEnd('。');
            s.CurrentZone = ExteriorZone;
            if (s.Locations[locationId].Progress < Config.Safehouse.RequiredExplore) DebugSetProgress(s, locationId, Config.Safehouse.RequiredExplore);
            var fresh = s.Progress.Flags.Add(Config.Safehouse.RegisteredFact);
            if (fresh) RefreshGates(s);
            s.WorldEnemies.RemoveAll(w => w.LocationId == locationId && !w.Engaged);
            PruneEnemyCards(s);
            if (s.Skills.TryGetValue(Crafting, out var cr) && cr.Level < 1) cr.Level = 1;
            UpdateFlags(s);
            return text + $"；探索度{s.Locations[locationId].Progress}%、已记营区登记、清掉当地敌踪：{(SafehouseSiteReasons(s, locationId).FirstOrDefault() ?? "可以建安全屋")}。";
        }

        /// <summary>开发：放下一间安全屋＋屋内床垫＋屋内火堆的全部材料（当前地面）。</summary>
        public string DebugSafehouseKit(GameState s)
        {
            var kit = new[] { ("wood", 10), ("fiber", 10), ("cloth", 4), ("stone", 8) };
            foreach (var (id, n) in kit) CreateItems(s, id, n, s.Ground);
            Normalize(s, s.Ground);
            return $"开发：安全屋基础＋床垫＋火堆材料放在{ZoneName(s, s.Ground)}地面（木材10、纤维10、布料4、石块8）";
        }

        /// <summary>开发：直接建成当前地点的安全屋（不扣材料、不花时间、不给经验；地点仍须在许可名单里）。</summary>
        public string DebugBuildSafehouse(GameState s)
        {
            var loc = s.CurrentLocation;
            if (SafehousePermissionBlocked(loc) is string why) return "开发：" + why;
            if (SafehouseBuilt(s, loc)) return $"开发：{LocName(loc)}已有安全屋";
            s.Locations[loc].Safehouse = new SafehouseState { Id = ItemInstance.SafehouseIdOf(loc), Built = true, BuiltMinute = s.GameMinute };
            UpdateFlags(s);
            return $"开发：在{LocName(loc)}直接建成安全屋（不扣材料、不花时间）";
        }

        /// <summary>开发：陶窑与固定净水台的建造与一批加工材料，制造设为 3 级、学会基础陶工做法。</summary>
        public string DebugStationKit(GameState s)
        {
            if (s.Skills.TryGetValue(Crafting, out var cr) && cr.Level < 3) cr.Level = 3;
            s.Progress.Flags.Add("h03_ceramic_notice_read");
            var kit = new[] { ("stone", 8), ("clay", 8), ("iron_plate", 2), ("charcoal", 1), ("ceramic_shell", 4), ("plank", 2), ("cloth", 2), ("dirty_water", 4), ("filter_media", 1) };
            foreach (var (id, n) in kit) CreateItems(s, id, n, s.Ground);
            Normalize(s, s.Ground);
            UpdateFlags(s);
            return $"开发：制造3级、学会基础陶工做法；陶窑与净水台建造及各一批材料放在{ZoneName(s, s.Ground)}地面";
        }

        /// <summary>开发（B22d Q174 试被动批次经验日额）：陶窑一批（黏土2、木炭1）与净水台两批（脏水8、过滤材料2）原料放当前地面。</summary>
        public string DebugStationInputs(GameState s)
        {
            foreach (var (id, n) in new[] { ("clay", 2), ("charcoal", 1), ("dirty_water", 8), ("filter_media", 2) }) CreateItems(s, id, n, s.Ground);
            Normalize(s, s.Ground);
            UpdateFlags(s);
            return $"开发：陶窑一批（黏土2、木炭1）与净水台两批（脏水8、过滤材料2）原料放在{ZoneName(s, s.Ground)}地面";
        }

        /// <summary>开发（B32e Q380 试多批排队与 10 批上限）：陶窑与净水台各 11 批原料放当前地面。</summary>
        public string DebugStationQueueInputs(GameState s)
        {
            foreach (var (id, n) in new[] { ("clay", 22), ("charcoal", 11), ("dirty_water", 44), ("filter_media", 11) }) CreateItems(s, id, n, s.Ground);
            Normalize(s, s.Ground);
            UpdateFlags(s);
            return $"开发：陶窑11批（黏土22、木炭11）与净水台11批（脏水44、过滤材料11）原料放在{ZoneName(s, s.Ground)}地面";
        }

        /// <summary>开发：野营遮雨布、折叠柴炉各一（当前地面）与木材2。</summary>
        public string DebugCampingKit(GameState s)
        {
            CreateItems(s, Config.Safehouse.Camping.TarpItemId, 1, s.Ground);
            CreateItems(s, Config.Safehouse.Camping.StoveItemId, 1, s.Ground);
            CreateItems(s, "wood", 2, s.Ground);
            Normalize(s, s.Ground);
            return $"开发：野营遮雨布、折叠柴炉各1与木材2放在{ZoneName(s, s.Ground)}地面（野营布可在普通野外铺，柴炉只能在临时露营许可点部署）";
        }

        /// <summary>开发（B22e，Q173）：建野外火堆的材料石块4、木材2放当前地面。</summary>
        public string DebugWildFireKit(GameState s)
        {
            var rc = WildFireRecipe;
            foreach (var kv in rc.Inputs) CreateItems(s, kv.Key, kv.Value, s.Ground);
            Normalize(s, s.Ground);
            UpdateFlags(s);
            var why = WildFireSiteReasons(s);
            return $"开发：建野外火堆材料（{string.Join("、", rc.Inputs.Select(kv => $"{ItemName(kv.Key)}{kv.Value}"))}）放在{ZoneName(s, s.Ground)}地面；" +
                   (why.Count == 0 ? "这里可以建野外火堆" : "这里不能建：" + string.Join("；", why));
        }
    }
}
