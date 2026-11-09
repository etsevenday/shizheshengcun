// 动态人物仅使用保存外观和模板 ID。未记录字段取同一套中性缺省，性别不推断。
const skin={
  '浅肤色':['#8b634f','#d4ae87','#e6bf99'],'中浅肤色':['#795643','#bd916d','#d9ab84'],
  '中等肤色':['#674735','#a87959','#c69975'],'中深肤色':['#503729','#896347','#ab805c'],
  '深肤色':['#3e2c23','#684c39','#8b694e']
};
const hair={'黑色':'#282521','深棕色':'#4a382e','浅棕色':'#786046','灰白色':'#aaa395'};
const hairShape={
  '短发':'M16 23V13l7-7h17l8 8-3 8-5-4-4 2-4-4-7 6z',
  '齐耳发':'M15 28V15l6-8h20l8 8-2 16-6 3V19l-8-3-10 5-1 10z',
  '齐肩发':'M13 40l2-24 8-10h18l9 9 2 23-8 5-3-10 2-15-11-3-10 8-2 17z',
  '束发':'M11 18l5-10 11-3 17 3 6 7-3 7-9-6-11 3-6 5-5-4-2 11-5-1z',
  '卷发':'M13 19l2-6h4V8h7V5h9v3h6v2h6v6h3v7h-7v-5h-6v3h-6v-5h-7v6h-7z',
  '剃短发':'M16 21v-8l6-6h17l8 7v7l-6-6H22z'
};
const faces={
  '眉尾浅疤':'M37 23h3v1h-3z M39 24h2v2h-2z','面颊浅疤':'M40 31h2v2h-2z M38 33h3v1h-3z',
  '雀斑':'M25 30h1v1h-1z M28 30h1v1h-1z M38 29h1v1h-1z M40 30h1v1h-1z',
  '左眉缺口':'M24 22h2v2h-2z','眼角细纹':'M21 27h2v1h-2z M40 27h2v1h-2z'
};
const outfits={
  front_guard:['#3f4740','#778373','#aaa791','shield'], blade_hand:['#583e2b','#9c6a44','#c89c66','blade'],
  breaker:['#524637','#9c7a50','#c6a575','tool'], spear_walker:['#394231','#6c7d51','#aaa676','strap'],
  salvager:['#3e4646','#788783','#a3b1a2','tool'], hunter:['#4b3b2a','#806744','#b9a078','strap'],
  old_service:['#374148','#647880','#9caa9d','shield'], coordinator:['#4a3936','#846662','#b99b84','badge'],
  camp_nurse:['#544944','#a38e79','#d7c6a0','aid'],
  hub_survivor:['#424332','#788163','#bcb58b','badge'],
  faction_church:['#4a3030','#815450','#c09377','badge'],
  faction_academy:['#3a4648','#687f83','#acbbb0','badge'],
  neutral:['#473f36','#756d59','#aa9a77','strap']
};
const kits={
  shield:'M3 60l12-3 7 4-1 22-9 8-9-7z',
  blade:'M48 64l10-7 3 2-4 8-7 4-3 11-3 1 2-11z',
  tool:'M50 64l4-7 4 1 3 5-3 5-4-2-5 16-4-1 5-14z',
  strap:'M11 46l8-4 25 29-4 7z',
  badge:'M41 49h8v10h-8z',
  aid:'M3 65h15l4 5v13H3z M9 68h4v4h5v4h-5v4H9v-4H6v-4h3z'
};
const poly=(d,fill)=>'<path d="'+d+'" fill="'+fill+'"/>';
function personSvg(fields={},mode='body') {
  const [shadow,base,light]=skin[fields.skinTone]||['#725844','#a98b6d','#ccb391'];
  const [coat,coatMid,coatLight,kit]=outfits[fields.template]||outfits.neutral;
  const view=mode==='portrait'?'8 2 49 47':'0 0 64 104';
  const selectedHair=hair[fields.hairColor]||'#493e34';
  const hairline=hairShape[fields.hairStyle]||'M16 23v-9l7-7h17l9 8-2 9-6-6-9-2-8 5z';
  const feature=faces[fields.faceDetail];
  const marker=Number.isInteger(fields.dutyMark)?fields.dutyMark%16:0;
  return '<svg xmlns="http://www.w3.org/2000/svg" viewBox="'+view+'" shape-rendering="crispEdges">'+
  '<g data-layer="body">'+
    poly('M20 39l7-4h12l9 5 7 11 1 23-7 5-5-6-2 20 5 2 2 7H33l-3-4-3 4H12v-7l7-4V71l-5 5-9-3 2-24z','#13120f')+
    poly('M20 42l8-4 11 1 7 4 7 10-1 19-5 2-5-9-1 23-12 1-10-2 3-23-6 9-7-3 4-19z',coat)+
    poly('M16 48l12-7 10 1 7 6-6 16-9 7-7-2 3-12-7 4-4 8-3-1z',coatMid)+
    poly('M22 77h8l-3 18-8 2z M33 76h8l-2 18 6 4H33z','#302a25')+
    poly('M26 39h10l3 5-8 4-8-3z M10 68h6v6h-6z M48 68h5v6h-5z',base)+
    poly('M19 55h8v4h-8z M33 51h6v4h-6z M30 60h5v3h-5z M15 97h10v2H15z M34 98h10v2H34z',coatLight)+'</g>'+
  '<g data-layer="head">'+
    poly('M14 16l7-9h20l9 9-1 14-5 8-9 4-11-1-7-8z','#15130f')+
    poly('M19 19l7-5 15 2 5 8-4 10-6 5-12-2-4-6z',shadow)+
    poly('M23 21l9-3 10 3 1 7-6 9-10-1-4-6z',base)+
    poly('M29 22h6v3h-6z M31 26h3v5h-3z M35 31h3v2h-3z',light)+
    poly(hairline,selectedHair)+
    poly('M24 25h5v2h-5z M36 24h5v2h-5z M29 34h7v2h-7z','#211a15')+
    (feature?poly(feature,fields.faceDetail==='左眉缺口'?light:shadow):'')+'</g>'+
  '<g data-layer="kit">'+poly(kits[kit],coatLight)+
    (marker?poly('M'+(19+marker%5)+' '+(48+Math.floor(marker/5)*3)+'h3v2h-3z','#ceaf77'):'')+
    '</g></svg>\n';
}
function baobaoSvg(){
  return '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 48 48" shape-rendering="crispEdges"><g data-layer="echo" fill="none" stroke="#677d53" stroke-width="2"><path d="M12 9L6 15v18l6 6M36 9l6 6v18l-6 6M6 7L2 13v22l4 6m36-34 4 6v22l-4 6"/></g><g data-layer="symbol"><path fill="#232820" d="M24 5l5 8 6 1 6 10-5 10-8 2-4 7-5-8-8-2-4-9 5-10 7-1z"/><path fill="#8e9f65" d="M24 11l3 6 7 2 3 5-4 7-7 1-2 5-3-6-7-1-3-6 4-6 6-1z"/><path fill="#c5ca87" d="M17 21h5v3h-5z M26 21h5v3h-5z M21 28h6v2h-6z"/></g></svg>\n';
}
