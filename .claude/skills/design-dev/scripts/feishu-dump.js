// 飞书 docx 文档 → Markdown 采集器。在内置浏览器的 javascript_tool 里原样执行一次（页面须是已登录的飞书文档），
// 它只定义 window.__fs 并把页面滚到顶部；飞书按可见区域懒渲染，而且只认真实滚轮事件，所以滚动必须用
// computer 的 scroll 动作（见 SKILL.md 第 1 步的批处理写法），每滚一屏调一次 window.__fs.collect()，
// 到底后 window.__fs.final() 返回 {title, markdown, images, files}。块按文档内的绝对纵坐标排序，不依赖渲染先后。
window.__fs = (() => {
  const zws = /[​‌‍﻿‪-‮⁠-⁤⁦-⁩]/g;
  const clean = s => (s || '').replace(zws, '').replace(/ /g, ' ').trim();
  const scroller = () => document.querySelector('.bear-web-x-container') || document.scrollingElement;
  const st = { seen: new Map(), y: new Map(), images: [], files: [] };
  const render = b => {
    const t = b.getAttribute('data-block-type');
    const txt = () => clean(b.innerText).replace(/\n{2,}/g, '\n');
    switch (t) {
      case 'heading1': return '# ' + txt();
      case 'heading2': return '## ' + txt();
      case 'heading3': return '### ' + txt();
      case 'heading4': return '#### ' + txt();
      case 'text': return txt();
      case 'ordered': { const s = txt(); const m = s.match(/^(\d+)\.\s*\n?([\s\S]*)$/); return m ? m[1] + '. ' + m[2].replace(/\n/g, ' ') : s; }
      case 'bullet': return '- ' + txt().replace(/^[•·]\s*/, '').replace(/\n/g, ' ');
      case 'todo': return '- [ ] ' + txt();
      case 'code': return '```\n' + clean(b.innerText) + '\n```';
      case 'quote_container': case 'quote': case 'callout': return txt().split('\n').map(l => '> ' + l).join('\n');
      case 'divider': return '---';
      case 'table': {
        const rows = [...b.querySelectorAll('tr')].map(tr => [...tr.querySelectorAll('td,th')].map(td => clean(td.innerText).replace(/\n+/g, '<br>').replace(/\|/g, '\\|')));
        if (!rows.length) return '';
        const w = rows[0].length;
        const out = ['| ' + rows[0].join(' | ') + ' |', '| ' + Array(w).fill('---').join(' | ') + ' |'];
        for (const r of rows.slice(1)) out.push('| ' + r.join(' | ') + ' |');
        return out.join('\n');
      }
      case 'image': {
        const id = b.getAttribute('data-record-id');
        if (!st.images.some(i => i.record === id)) st.images.push({ record: id });
        const n = st.images.findIndex(i => i.record === id) + 1;
        return `![图片${n}](assets/图${n}.jpg)`;
      }
      case 'file': {
        const name = txt().split('\n')[0];
        const m = b.outerHTML.match(/download\/(?:v2\/cover|all|v2\/image)\/([A-Za-z0-9]{20,})/);
        const rec = b.getAttribute('data-record-id');
        if (!st.files.some(f => f.record === rec)) st.files.push({ name, token: m ? m[1] : null, record: rec, url: m ? `${location.origin}/space/api/box/stream/download/all/${m[1]}?mount_node_token=${rec}&mount_point=docx_file` : null });
        return `[附件：${name}]`;
      }
      case 'back_ref_list': case 'page': return '';
      default: return '<!-- ' + t + ' -->\n' + txt();
    }
  };
  // 每屏调用一次：记录当前渲染出的顶层块。滚出视口的块会变成空壳（表格尤其明显），只保留更完整的那次。
  const collect = () => {
    const sc = scroller(); let n = 0;
    for (const b of document.querySelectorAll('[data-block-id]')) {
      const p = b.parentElement && b.parentElement.closest('[data-block-id]');
      if (!p || p.getAttribute('data-block-type') !== 'page') continue;
      const id = b.getAttribute('data-block-id');
      const md = render(b), old = st.seen.get(id);
      if (old == null || md.length > old.length) st.seen.set(id, md);
      if (!st.y.has(id)) st.y.set(id, sc.scrollTop + b.getBoundingClientRect().top);
      n++;
    }
    return { rendered: n, total: st.seen.size, top: Math.round(sc.scrollTop), bottom: sc.scrollTop + sc.clientHeight >= sc.scrollHeight - 2 };
  };
  const final = () => {
    const ids = [...st.seen.keys()].sort((a, b) => st.y.get(a) - st.y.get(b));
    const title = clean(document.title).replace(/ - 飞书云文档$/, '');
    const markdown = ids.map(id => st.seen.get(id)).filter(s => s).join('\n\n');
    return { title, blocks: ids.length, chars: markdown.length, images: st.images, files: st.files, markdown };
  };
  // 把某张图滚进视口并返回它的视口坐标，供 computer 的 zoom 截图保存。
  const image = n => { const im = st.images[n - 1]; if (!im) return null; const el = document.querySelector(`[data-record-id="${im.record}"] img`); if (!el) return { missing: true }; el.scrollIntoView({ block: 'center' }); const r = el.getBoundingClientRect(); return [Math.round(r.left), Math.round(r.top), Math.round(r.right), Math.round(r.bottom)]; };
  scroller().scrollTop = 0;
  return { collect, final, image };
})(); 'ready'
