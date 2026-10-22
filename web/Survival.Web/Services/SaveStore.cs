using Microsoft.JSInterop;

namespace Survival.Web.Services;

/// <summary>浏览器 localStorage 单槽存档。</summary>
public sealed class SaveStore
{
    public const string Key = "survival.save.v1";
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

    public void Clear() => _js.InvokeVoid("survival.saveRemove", Key);
}
