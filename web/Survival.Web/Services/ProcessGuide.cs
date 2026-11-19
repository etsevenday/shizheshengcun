using Survival.Core.Config;
using Survival.Core.Engine;

namespace Survival.Web.Services;

/// <summary>
/// 火堆自动加工四类处理说明（烧水、烤制、水煮、煅烧木炭）的标题、说明与图示步骤。
/// B35a1（S09／S11／S14 2026-11-17 Q932 A）：从手工制作原“自动加工”分类原样迁到火堆／柴炉加工菜单主层的只读“处理说明”折叠区，
/// 标题、步骤与现行规则同源（读配置），不带“查看火堆”按钮，不投料、不开工、不推进时间。
/// </summary>
public static class ProcessGuide
{
    /// <summary>说明条目标题（原手工制作“自动加工”列表同名）。</summary>
    public static string Title(GameEngine e, ProcessConfig p) =>
        p.IsPotBatch ? "烧水" : p.IsSlotBatch ? $"{p.Name}{e.ItemName(p.OutputId)}" : $"{p.Name}{e.ItemName(p.InputId).Replace("生", "")}";

    /// <summary>说明正文（原悬停说明）。</summary>
    public static string Hint(GameEngine e, ProcessConfig p)
    {
        if (p.IsSlotBatch)
            return $"点击火堆或柴炉卡，在加工菜单的空加工位点「煅烧木炭」投料：每批{e.ItemName(p.InputId)}×{p.InputCount}占1个加工位，{p.HeatMinutes}有效受热分钟后原位变成{e.ItemName(p.OutputId)}×{p.OutputCount}。"
                   + $"火堆燃料另计（两批并行不倍扣，点火那5分钟不计热）；把{e.ItemName(p.InputId)}拖到火堆是添加燃料，不是煅烧。不给经验。";
        // BM12（Q996 A，合同 b35a1-q996-q998 heatHelp）：锅食谱（烘制远行硬饼、炖煮谷肉炖食）句首补主食材名（“谷物”“谷物和生肉”，读配置 foodInputs），其余照原锅煮模板
        if (p.IsPotRecipe)
            return $"{PotFoodLead(e, p)}放进有{e.ItemName(p.RequiredLiquid ?? "")}的锅、锅放进燃烧的火堆，每份{p.HeatMinutes}分钟煮成{e.ItemName(p.OutputId)}，完成时扣{p.WaterCostMl}毫升；空锅只存放不加工。";
        return (p.IsPotBatch ? "boil_water" : p.InPot ? "boil_root" : "roast_root") switch
        {
            // B56（02 Q976 B 定稿）：旧「半升或一升都是」换成烧水行短稿「100毫升起，时长不变」，受热分钟不变
            "boil_water" => $"装了{e.ItemName(p.InputId)}的金属锅放进燃烧的火堆，{p.HeatMinutes}分钟原地烧成{e.ItemName(p.OutputId)}；100毫升起，时长不变；不自动补水。",
            "roast_root" => $"{e.ItemName(p.InputId)}直接放进燃烧火堆的空加工位，每份{p.HeatMinutes}分钟烤成{e.ItemName(p.OutputId)}，不需要锅和水。",
            "boil_root" => $"{e.ItemName(p.InputId)}放进有{e.ItemName(p.RequiredLiquid ?? "")}的锅、锅放进燃烧的火堆，每份{p.HeatMinutes}分钟煮成{e.ItemName(p.OutputId)}，完成时扣{p.WaterCostMl}毫升；空锅只存放不加工。",
            _ => "",
        };
    }

    /// <summary>BM12（Q996 A）：锅食谱主食材名，多种用“和”连（“谷物和生肉”）。</summary>
    private static string PotFoodLead(GameEngine e, ProcessConfig p) => string.Join("和", p.FoodInputs.Keys.Select(e.ItemName));

    /// <summary>BM12（Q996 A）：锅食谱第 2 步“2. 把{食材}×{份数}、…拖到锅的食材区”（份数读配置 foodInputs）。</summary>
    private static string PotFoodStep(GameEngine e, ProcessConfig p) =>
        $"2. 把{string.Join("、", p.FoodInputs.Select(kv => $"{e.ItemName(kv.Key)}×{kv.Value}"))}拖到锅的食材区";

    /// <summary>图示步骤：每步一行“图标+名称+说明”。</summary>
    public static List<(string icon, string name, string note)> Steps(GameEngine e, ProcessConfig p)
    {
        var heat = $"受热{p.HeatMinutes}分钟";
        if (p.IsSlotBatch)
            return new()
            {
                (p.InputId, e.ItemName(p.InputId), $"1. 随身或当前地面备好{e.ItemName(p.InputId)}×{p.InputCount}（拖到火堆是加燃料）"),
                ("glyph:add", "空加工位", $"2. 点击火堆或柴炉卡，在加工菜单的空加工位点「煅烧木炭」，确认投料（占1位，不耗时）"),
                ("fire_lit", "燃烧的火堆", $"3. 点火并保持燃料，{p.HeatMinutes}有效受热分钟；燃料另计，熄火保留进度"),
                (p.OutputId, e.ItemName(p.OutputId), $"4. 原位变成{e.ItemName(p.OutputId)}×{p.OutputCount}，取出才进包；不给经验"),
            };
        return (p.IsPotBatch ? "boil_water" : p.InPot ? "boil_root" : "roast_root") switch
        {
            "boil_water" => new()
            {
                (p.InputId, e.ItemName(p.InputId), "1. 把水卡拖到锅的水量条，每份100毫升"),
                ("pot", "金属锅", "2. 把锅拖到火堆的空加工位"),
                ("fire_lit", "燃烧的火堆", $"3. 点火并保持燃料，{heat}"),
                (p.OutputId, e.ItemName(p.OutputId), "4. 留在原锅，可饮用或倒出"),
            },
            "roast_root" => new()
            {
                (p.InputId, e.ItemName(p.InputId), "1. 拖到火堆的空加工位（每份占1位）"),
                ("fire_lit", "燃烧的火堆", $"2. {heat}，熄火保留进度"),
                (p.OutputId, e.ItemName(p.OutputId), "3. 留在加工位，取出后食用"),
            },
            _ => new()
            {
                (p.RequiredLiquid ?? "clean_water", e.ItemName(p.RequiredLiquid ?? "clean_water"), $"1. 锅里装净水，每份食材预留{p.WaterCostMl}毫升"),
                p.IsPotRecipe
                    // BM12（Q996 A）：锅食谱第 2 步写出食材名与份数；图标取第一种主食材，名称列各主食材名
                    ? (p.FoodInputs.Keys.First(), string.Join("、", p.FoodInputs.Keys.Select(e.ItemName)), PotFoodStep(e, p))
                    : (p.InputId, e.ItemName(p.InputId), "2. 拖到锅的食材区（最多2份）"),
                ("pot", "金属锅", "3. 锅放进火堆的空加工位"),
                ("fire_lit", "燃烧的火堆", $"4. {heat}，完成时扣水"),
                (p.OutputId, e.ItemName(p.OutputId), "5. 留在原锅食材位"),
            },
        };
    }
}
