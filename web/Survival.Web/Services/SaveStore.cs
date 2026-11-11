using Microsoft.JSInterop;
using Survival.Core.Save;

namespace Survival.Web.Services;

/// <summary>浏览器存储写入失败：Category 为浏览器异常类别名（如 QuotaExceededError），Message 为浏览器原始错误文本。</summary>
public sealed class SaveWriteException : SaveStorageWriteException
{
    public SaveWriteException(string category, string message) : base(category, message) { }

    /// <summary>解析 survival.saveSet 的失败返回（“类别名\u001f错误文本”；没有分隔符时整段作错误文本）。</summary>
    public static SaveWriteException FromJs(string err)
    {
        var i = err.IndexOf('\u001f');
        return i < 0 ? new SaveWriteException("", err) : new SaveWriteException(err[..i], err[(i + 1)..]);
    }
}

/// <summary>浏览器 localStorage 单槽存档。</summary>
public sealed class SaveStore
{
    public const string Key = "survival.save.v1";
    /// <summary>升级旧存档前的原文备份，只保留一份。</summary>
    public const string BackupKey = "survival.save.v1.bak";
    private readonly IJSInProcessRuntime _js;

    public SaveStore(IJSRuntime js) => _js = (IJSInProcessRuntime)js;

    public string? Load() => _js.Invoke<string?>("survival.saveGet", Key);

    public bool HasSave => !string.IsNullOrEmpty(Load());

    /// <summary>写入存档；失败时抛出带浏览器异常类别与错误信息的 <see cref="SaveWriteException"/>。</summary>
    public void Save(string json)
    {
        var err = _js.Invoke<string>("survival.saveSet", Key, json);
        if (!string.IsNullOrEmpty(err)) throw SaveWriteException.FromJs(err);
    }

    /// <summary>覆盖旧版存档前，把原文写到备份键。</summary>
    public void Backup(string json)
    {
        var err = _js.Invoke<string>("survival.saveSet", BackupKey, json);
        if (!string.IsNullOrEmpty(err)) throw SaveWriteException.FromJs(err);
    }

    public void Clear()
    {
        _js.InvokeVoid("survival.saveRemove", Key);
        _js.InvokeVoid("survival.saveRemove", BackupKey);
    }

    public string? GetPref(string key) => _js.Invoke<string?>("survival.prefGet", key);

    public void SetPref(string key, string value) => _js.InvokeVoid("survival.prefSet", key, value);

    /// <summary>B31a：把阅读字体（pixel/clear）与动画倍率落到页面。</summary>
    public void ApplyPresentation(string font, double motionK) => _js.InvokeVoid("survival.applyPresentation", font, motionK);
}

/// <summary>
/// B40 多存档的浏览器存储（S01 Q335：各槽独立持久化键、原子替换）。读失败与“没有这个键”分开回报（Q403「浏览器暂时无法读取存档」），
/// 写入为单键 setItem（要么旧值、要么新值），失败按 Q282 带浏览器异常类别名抛出。
/// </summary>
public sealed class BrowserSaveStorage : ISaveStorage
{
    private readonly IJSInProcessRuntime _js;

    public BrowserSaveStorage(IJSRuntime js) => _js = (IJSInProcessRuntime)js;

    /// <summary>survival.slotGet 返回：“0”没有、“1”+值、“2”+错误文本。</summary>
    public string? Get(string key)
    {
        var r = _js.Invoke<string?>("survival.slotGet", key);
        if (string.IsNullOrEmpty(r) || r[0] == '0') return null;
        if (r[0] == '1') return r[1..];
        throw new SaveStorageReadException(r[1..]);
    }

    public void Set(string key, string value)
    {
        var err = _js.Invoke<string>("survival.slotSet", key, value);
        if (!string.IsNullOrEmpty(err)) throw SaveWriteException.FromJs(err);
    }

    public void Remove(string key) => _js.InvokeVoid("survival.saveRemove", key);
}
