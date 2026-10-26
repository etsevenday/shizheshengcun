// 在 javascript_tool 里执行，读取当前飞书文档里所有附件块（JSON/文本）的内容，不触发浏览器下载。
// 先把页面滚到附件块可见处（feishu-dump.js 的 files 列表给出了 url；没有 url 说明块还没渲染）。
// 返回 [{name, status, type, size, text}]；二进制附件只给 size，不给 text。
await (async () => {
  const zws = /[​‌‍﻿‪-‮⁠-⁤⁦-⁩]/g;
  const out = [];
  for (const b of document.querySelectorAll('[data-block-type="file"]')) {
    const name = (b.innerText || '').replace(zws, '').trim().split('\n')[0];
    const m = b.outerHTML.match(/download\/(?:v2\/cover|all)\/([A-Za-z0-9]{20,})/);
    const rec = b.getAttribute('data-record-id');
    if (!m) { out.push({ name, error: '块里没找到文件 token，先滚动到它再试' }); continue; }
    const url = `${location.origin}/space/api/box/stream/download/all/${m[1]}?mount_node_token=${rec}&mount_point=docx_file`;
    try {
      const r = await fetch(url, { credentials: 'include' });
      const type = r.headers.get('content-type') || '';
      const textLike = /json|text|xml|csv|javascript/i.test(type) || /\.(json|md|txt|csv|yml|yaml)$/i.test(name);
      if (textLike) { const text = await r.text(); out.push({ name, status: r.status, type, size: text.length, text }); }
      else { const bl = await r.blob(); out.push({ name, status: r.status, type, size: bl.size, text: null }); }
    } catch (e) { out.push({ name, error: String(e) }); }
  }
  return out;
})()
