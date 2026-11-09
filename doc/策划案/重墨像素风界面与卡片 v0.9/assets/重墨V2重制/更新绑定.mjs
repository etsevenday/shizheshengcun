import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';

const root=path.dirname(fileURLToPath(import.meta.url));
const read=p=>JSON.parse(fs.readFileSync(path.resolve(root,p),'utf8'));
const source=read('../全量素材库/全量视觉清单.json');
const plan=read('出图计划.json');
const redo=read('替换清单.json');
const expected=new Set(plan.flatMap(j=>j.cells.map(c=>c.key)));
const font=read('字体来源.json');
const rows=source.assets.map(original=>{
  const row={...original};
  if(row.key==='weapon:fists') {
    row.asset={ref:'skillFamily:unarmed'};
    return row;
  }
  if(!expected.has(row.key)) return row;
  delete row.native;
  delete row.portrait;
  delete row.shape;
  delete row.display;
  const asset=redo.bindings[row.key];
  if(!asset){
    row.asset=null;
    row.rebuildStatus='待重绘或复核';
    row.design=redo.pending?.[row.key]?.reason||'经核对的重绘图将在此键启用。';
    return row;
  }
  row.asset={...asset,file:'../重墨V2重制/'+asset.file};
  row.rebuildStatus='重墨 V2 新绘';
  if(row.category==='recruitTemplate') row.design='按模板名称新绘的职业样例。运行时外观仍须绑定实际保存字段。';
  else if(row.category==='duty') row.design='按岗位名称新绘，使用独立头像裁窗。';
  else row.design='从对象名称出发生成；图位按像素裁切。';
  return row;
});
const payload={
  ...source,schemaVersion:2,scope:'重墨 V2 新绘绑定。未通过复核的对象显示待重绘，不回退到旧造型。',
  artPathsRelativeTo:'../全量素材库/',
  rebuild:{expected:expected.size,approved:Object.keys(redo.bindings).length,fonts:font},
  assets:rows
};
const json=JSON.stringify(payload,null,2);
fs.writeFileSync(path.join(root,'新版全量视觉清单.json'),json+'\n','utf8');
fs.writeFileSync(path.join(root,'新版预览数据.js'),'window.VisualCatalog='+json+';\n','utf8');
console.log('New bindings '+payload.rebuild.approved+'/'+payload.rebuild.expected);
