using Newtonsoft.Json;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Web.Services;

/// <summary>Presentation-only lookup by stable IDs. Save and simulation values are unchanged.</summary>
public static class ArtAssets
{
    private const string Root = "art/ink-v2/";
    private const string Unknown = "glyph:unknown";
    private static readonly Manifest Assets = Load();

    public static string Scene(string id, string? fallback = null) =>
        Assets.Scenes.TryGetValue(id, out var file) ? Root + file : id switch
        {
            "hardware" => "art/v10/hardware.png",
            "field" or "woods" or "stream" or "village" or "scrapyard" or "clinic" => $"art/v08/{id}.png",
            _ => fallback ?? Root + Assets.Scenes["camp"],
        };

    public static string Portrait(string key) =>
        Root + Assets.Portraits.GetValueOrDefault(key, Assets.Portraits[Unknown]);

    public static string DialoguePortrait(GameConfig config, DialogueSession session)
    {
        var speaker = session.Lines.LastOrDefault(line => line.Side == DialogueSide.Right)?.Speaker ?? session.Counterpart;
        if (speaker is "饱饱") return Portrait("actor:baobao");
        // Resolve against the saved dialogue, never against who is on duty now.
        var eventId = session.EventId.Split('#', 2)[0];
        var posts = config.City.Npcs;
        var post = posts.FirstOrDefault(n => n.DutySpeaker == speaker &&
            (n.States.ContainsValue(eventId) || n.LocationId == session.LocationId));
        if (post != null) return Portrait("duty:" + post.NpcId);

        var original = session.Lines.Where(line => line.Side == DialogueSide.Right)
            .Select(line => config.Story.Dialogues.FirstOrDefault(d => d.Id == line.LineId)?.Speaker)
            .LastOrDefault();
        post = posts.FirstOrDefault(n => n.Name == original && n.DutySpeaker == speaker);
        if (post != null) return Portrait("duty:" + post.NpcId);
        post = posts.FirstOrDefault(n => n.Name == speaker);
        if (post != null) return Portrait("actor:" + post.NpcId);

        var companion = config.Party.NamedCompanions.FirstOrDefault(n => n.Name == speaker);
        if (companion != null) return Portrait("actor:" + companion.ActorId);
        return Portrait(speaker == config.Prologue.Visitor.Name ? "actor:" + config.Prologue.Visitor.NpcId : Unknown);
    }

    internal static bool HasSprite(string key) => Assets.Sprites.ContainsKey(key);

    internal static string Sprite(string key)
    {
        if (!Assets.Sprites.TryGetValue(key, out var sprite))
        {
            key = Unknown;
            sprite = Assets.Sprites[key];
        }
        var (x, y, w, h) = (sprite.Crop[0], sprite.Crop[1], sprite.Crop[2], sprite.Crop[3]);
        return $"<svg class=\"ink-sprite\" data-art=\"{key}\" viewBox=\"0 0 {w} {h}\" aria-hidden=\"true\" focusable=\"false\"><svg width=\"{w}\" height=\"{h}\" viewBox=\"{x} {y} {w} {h}\" style=\"overflow:hidden;width:{w}px;height:{h}px;max-width:none;max-height:none\"><image href=\"{Root}{sprite.File}\" width=\"{sprite.Size[0]}\" height=\"{sprite.Size[1]}\" preserveAspectRatio=\"none\" /></svg></svg>";
    }

    private static Manifest Load()
    {
        using var stream = typeof(ArtAssets).Assembly.GetManifestResourceStream("ink-v2.json")!;
        using var reader = new StreamReader(stream);
        return JsonConvert.DeserializeObject<Manifest>(reader.ReadToEnd())!;
    }

    private sealed record Manifest(Dictionary<string, Frame> Sprites, Dictionary<string, string> Portraits, Dictionary<string, string> Scenes);
    private sealed record Frame(string File, int[] Size, int[] Crop);
}
