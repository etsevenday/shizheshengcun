export const uiFrames={
  panel:['面板','iron'], card:['卡片','iron'], selected:['选中','gold'], focus:['键盘焦点','bone'],
  tab:['页签','iron'], tab_active:['激活页签','gold'], button:['普通按钮','iron'], primary:['主操作','rust'],
  danger:['危险确认','hazard'], disabled:['不可用','muted'], tooltip:['悬浮信息','canvas'],
  overlay:['覆盖窗','iron'], menu:['菜单外框','iron'], dialogue:['对白外框','canvas'],
  dialogue_battle:['战斗对白外框','hazard'], history:['历史阅读','canvas'], portrait:['肖像框','iron'],
  slot:['空槽','muted'], equipment:['装备槽','iron'], skill:['招式槽','iron'], legal_drop:['可放入','steel'],
  denied_drop:['不可放入','hazard'], meter:['进度槽','iron'], task:['任务卡','canvas'],
  map_node:['地图节点','steel'], end:['结算','hazard'], banner:['场景标题','gold']
};
const colors={iron:['#30291f','#755c3f','#b89360'],gold:['#4f3720','#bb813f','#f1c17d'],
  bone:['#403529','#a69275','#efe0ba'],rust:['#452419','#91422b','#d68e5e'],
  hazard:['#401d18','#99453b','#d48468'],muted:['#292624','#534a3c','#8a7c62'],
  canvas:['#483724','#907550','#cbb186'],steel:['#273735','#577b75','#92b6a4']};
export function frameSvg(id,pal) {
  const [dark,base,light]=colors[pal];
  const corner='<path d="M1 15V6h4V2h12v4h-7v5H6v7H2z" fill="'+base+'"/>'+
    '<path d="M2 6h4V2h10v2H8v4H4v7H2z" fill="'+light+'"/>'+
    '<path d="M7 7h3v3H7z M12 4h3v2h-3z" fill="#13110e"/>';
  const corners=Array.from({length:4},(_,i)=>'<g transform="rotate('+(i*90)+' 48 48)">'+corner+'</g>').join('');
  const stitch=id.includes('dialogue')||id==='history'
    ? '<path d="M18 9h6m4 0h6m4 0h6m4 0h6m4 0h6m4 0h6m4 0h6" stroke="'+light+'" stroke-opacity=".52"/>'
    : '<path d="M28 3h8m24 90h8M43 6h9" stroke="'+light+'" stroke-opacity=".52"/>';
  return '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 96 96" shape-rendering="crispEdges"><g data-layer="frame">'+
    '<path d="M7 1h82l6 6v82l-6 6H7l-6-6V7z M10 11v74h76V11z" fill-rule="evenodd" fill="'+dark+'"/>'+
    '<path d="M12 4h72M4 12v72m88-72v72M12 92h72" fill="none" stroke="'+base+'" stroke-width="2"/>'+
    '<path d="M14 8h68M8 14v68m80-68v68M14 88h68" fill="none" stroke="'+base+'" stroke-opacity=".6"/>'+
    corners+stitch+'</g></svg>\n';
}

export const textureDesigns={
  iron:['铁灰','6a5946'], paper:['旧纸','714c31'], cloth:['粗布','72604d'],
  danger:['红刻痕','97574b'], shadow:['暗底','7f7460']
};
export function textureSvg(key,hex) {
  const paper=key==='paper';
  const marks=key==='cloth'
    ? 'M1 2h3v1H1z M5 8h3v1H5z M9 2h3v1H9z M13 8h3v1h-3z'
    : 'M3 6h4v1H3z M22 15h6v1h-6z M9 29h2v1H9z M17 23h3v1h-3z M29 2h2v1h-2z';
  const size=key==='cloth'?16:32;
  return '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 '+size+' '+size+'" shape-rendering="crispEdges"><path d="'+marks+
    '" fill="#'+hex+'" opacity="'+(paper?'.30':'.21')+'"/></svg>\n';
}
