using Microsoft.JSInterop;

namespace Survival.Web.Services;

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

    /// <summary>写入存档；失败时抛出带浏览器错误信息的异常。</summary>
    public void Save(string json)
    {
        var err = _js.Invoke<string>("survival.saveSet", Key, json);
        if (!string.IsNullOrEmpty(err)) throw new InvalidOperationException(err);
    }

    /// <summary>覆盖旧版存档前，把原文写到备份键。</summary>
    public void Backup(string json)
    {
        var err = _js.Invoke<string>("survival.saveSet", BackupKey, json);
        if (!string.IsNullOrEmpty(err)) throw new InvalidOperationException(err);
    }

    public void Clear()
    {
        _js.InvokeVoid("survival.saveRemove", Key);
        _js.InvokeVoid("survival.saveRemove", BackupKey);
    }

    public string? GetPref(string key) => _js.Invoke<string?>("survival.prefGet", key);

    public void SetPref(string key, string value) => _js.InvokeVoid("survival.prefSet", key, value);
}
