// 32 格透明图标。大轮廓、受光面、细节分别绘制，方便替换材质和颜色。
const inks={
  iron:['#494d4a','#777a6b','#c7bfa0','#e5c88f'],
  steel:['#394a4c','#688084','#b5c7bc','#7fa8a0'],
  rust:['#553126','#965737','#d3a16c','#e6ba79'],
  canvas:['#4c3e2b','#87714e','#c7b78b','#ead09b'],
  olive:['#3b4430','#6a7547','#a7aa70','#d0bd80'],
  water:['#263c43','#497580','#86b4ba','#c7d4bd'],
  medical:['#62504a','#a18c77','#dfcfaa','#b35a4b'],
  flesh:['#522b28','#945246','#d09a78','#edd1a0'],
  amber:['#493522','#937145','#d3a96e','#f0c57d'],
  hazard:['#4a2522','#a44d3f','#d58359','#f0bc78'],
  violet:['#483849','#785d7d','#bda8a6','#e1b68c']
};
const forms={
  bottle:['M13 4h6v4h2v4l4 5v10l-3 2H10l-3-2V17l4-5V8h2z','M12 14l-3 4v7l3 2h4V14z','M12 5h8v3h-8z M20 18h3v7h-3z'],
  canteen:['M13 3h6v5l5 3 3 8-2 8-4 2H10l-5-6 1-8 7-7z','M12 11l-5 7 1 6 4 2 2-12z','M11 18h12v4H11z M14 4h4v3h-4z'],
  vial:['M12 4h8v8l3 4v10l-3 3h-8l-3-3V16l3-4z','M12 16h7v10h-7z','M11 5h10v4H11z M15 16h3v7h-3z'],
  tablet:['M7 7l17-3 4 4v15l-5 5-16 1-4-4V12z','M7 10l17-3v15l-4 3-13 1z','M9 12h4v4H9z M16 11h4v4h-4z M9 19h4v4H9z M16 18h4v4h-4z'],
  sachet:['M5 8l5-4h15l3 4-2 17-4 4H8l-4-4z','M9 8h12l1 14-3 4H9z','M8 7h16v2H8z M10 22h12v2H10z'],
  can:['M6 8l4-4h12l4 4v17l-4 4H10l-4-4z','M8 10h14v15l-3 2H8z','M9 7h14v3H9z M8 15h15v6H8z'],
  berry:['M7 12l6-6 3 4 4-6 6 1-3 8 4 5-4 8-6 2-5-3-7-2-2-6z','M8 14l5-1 3 5-4 5-5-3z M17 16l5-1 3 4-5 5-4-2z','M17 6l5-2 3 2-5 5z M8 15h3v3H8z M19 17h3v3h-3z'],
  root:['M24 5l3 4-6 6-2 8-7 6-7-4-2-7 5-7 9-1 2-5z','M6 18l4-5 8-1-2 8-7 5z','M18 11l4-6h4l-4 5z M9 17h4v2H9z'],
  meat:['M4 19l4-8 11-7 7 1 3 6-2 8-8 9H9l-5-4z','M7 18l5-6 12-5 2 5-5 9-8 4-6-2z','M8 20l5-4 2 2 7-5 2 1-7 8-4-1-4 3z'],
  bowl:['M3 15l4-6 8-2 11 2 3 6-3 9-6 4H11l-6-4z','M6 16h19l-4 9H11z','M7 12h19v4H7z M13 8l2-4h2l-2 4z M20 8l1-4h2l-1 4z'],
  sack:['M9 6h14l-3 5 7 7v7l-5 4H9l-5-4v-7l7-7z','M9 15l-3 7 5 5h6l-2-14z','M10 9h12v3H10z M10 21h8v2h-8z'],
  bread:['M4 17l5-7 9-4 8 3 3 7-4 8-10 4-9-2z','M7 18l5-6 7-3 6 3-3 7-11 6-4-2z','M13 12l2 4 2-1-2-5z M18 10l2 5 2-2-2-4z'],
  strips:['M4 17l17-10 7 2v4L13 24l-7 1-3-3z M5 12l11-7 5 2-2 4-12 7-3-2z','M8 19l16-9 2 2-13 9-6 1z','M10 13l5-3 2 1-5 4z M12 19l7-4 1 1-7 5z'],
  plank:['M3 20L20 5l8 2 1 8-18 14-7-2z M4 14L17 3l7 2-3 4L8 20z','M5 20L21 7l5 1-17 16z','M9 21L24 9l1 2-15 13z M5 14l11-9 2 1L7 16z'],
  rock:['M4 20l4-9 10-6 8 4 3 12-7 7H10z','M8 13l9-5 5 3-5 8-10 1z','M11 12l6-4 3 2-6 5z M21 21h4v2h-4z'],
  scrap:['M3 21l4-8 4 2 3-9 7-2 2 8 6 1 1 10-6 5H9z','M7 17l10-9 4 5-5 11H9z','M17 8h3v3h-3z M8 20h5v2H8z M21 15h5v2h-5z'],
  sheet:['M4 9l5-4h19v16l-7 7H4z','M7 10h16v11l-4 4H7z','M8 8h17v3H8z M7 22h9v2H7z M22 15h2v5h-2z'],
  soap:['M4 14l7-7h13l5 5v9l-7 6H10l-6-5z','M7 15l6-5h11l2 3-5 6H9z','M13 12h7v2h-7z M9 21h11v2H9z'],
  cloth:['M5 10l7-5 14 3 3 6-5 14-14 1-7-7z','M8 11l10-3 4 4-5 12-8 2-3-6z','M8 12l9-2 2 2-8 4z M17 20l6-8 2 1-4 9z'],
  rope:['M8 5h15l6 7v10l-7 6H9l-6-5V12z M11 10l-3 5v6l5 3h7l4-4v-6l-4-4z','M8 8l5-1-7 8 1 7 6 4H9l-4-4V13z','M10 6h4v3h-4z M24 12h3v5h-3z M16 24h5v3h-5z'],
  part:['M12 3h8l1 4 5 2 4 5-3 4v6l-6 2-2 4h-7l-2-4-6-2v-6l-3-4 4-5 5-2z M12 12v7h8v-7z','M7 11l4-2 3 2-4 4 1 7-4-2z','M14 5h4v3h-4z M21 22h4v2h-4z M23 11h3v3h-3z'],
  tubes:['M3 19L18 5l8 1 3 7-2 4-15 13-8-2z M7 20l4 1 1 4-4 2-3-4z','M8 18L21 7l4 3-13 12z','M11 16L23 7l2 2-11 10z'],
  chip:['M8 4h16v4h4v16h-4v4H8v-4H4V8h4z','M8 9h16v14H8z','M12 12h8v8h-8z M4 12h5v2H4z M23 18h5v2h-5z M14 4h2v6h-2z'],
  battery:['M11 3h10v4h4v19l-3 3H10l-3-3V7h4z','M10 9h10v16H10z','M12 5h8v3h-8z M14 11h4v4h4v3h-4v4h-4v-4h-4v-3h4z'],
  lamp:['M4 10l6-4 7 2 3 7 8 4v5l-5 5-8-4-3-5-7-1z','M5 11l5-3 4 3 1 5-5 2-4-2z','M8 10h4v5H8z M17 18l9 3-3 4-7-4z'],
  firekit:['M7 13l6-3 4 4 5-3 5 4v11l-4 3H9l-4-5z','M8 16l6-2 5 4 3 7H9z','M16 4l2-2 3 4-2 5-3-1z M12 21h11v2H12z'],
  patch:['M5 8l6-4 15 2 3 7-3 12-7 4-13-3-3-8z','M8 10l7-3 9 2-3 13-10 4-4-7z','M8 14h3v2H8z M10 20h3v2h-3z M17 23h3v2h-3z M22 11h3v2h-3z'],
  crate:['M4 9l9-4 15 5v14l-9 5-15-4z','M7 13l11 2v11L7 23z','M7 10l6-3 12 4-6 3z M20 16l6-3v2l-6 4z'],
  bag:['M10 5h12l5 6v14l-4 4H8l-4-4V12z','M8 13h10v12H8z','M12 7h8v3h-8z M8 18h9v4H8z M20 15h3v9h-3z'],
  bed:['M3 20l3-7 15-7 8 4v12l-9 6-13-2z','M6 18l14-7 6 3-9 9-11-1z','M8 14l10-6 6 3-8 5z M7 23h12v2H7z'],
  tent:['M3 26L15 5h3l12 21-5 3H6z','M8 25l7-16 2 15z','M16 8l3 1 8 16-7-3z M4 25h5v2H4z'],
  stove:['M6 14h20l2 10-4 4H8l-4-4z M12 4h4v7h-4z M21 4h3v7h-3z','M7 17h13v8H7z','M9 17h12v3H9z M16 22h3v2h-3z'],
  flame:['M6 24l-1-6 4-5 2 3 3-8 4-5 2 9 3-2 4 7v8l-5 4H11z','M10 22l3-5 3 3 3-7 4 6-1 7H12z','M15 24l2-4 4 4-3 3h-3z'],
  pot:['M6 12l5-4h3V5h5v3h3l4 4h4v5h-4l-2 8-4 4H12l-5-4-1-8H2v-5z','M8 15h15l-2 11H12z','M10 11h13v3H10z M10 19h3v5h-3z'],
  mortar:['M5 13l4-4 12-1 4-5 4 3-5 10-2 8-4 4H10l-5-6z','M8 15h15l-3 8-4 3h-6z','M8 12h14v3H8z M24 5h3l-5 7h-2z'],
  glove:['M5 19l4-3V8h4v5h1V5h4v7h1V6h4v7l2-3h4v12l-5 7H11z','M10 16l8-2 5 2v8l-4 3h-6z','M11 20h10v3H11z M18 7h3v5h-3z'],
  vest:['M8 6l5-3 3 4 4-4 5 3 4 8-4 3v11H7V17l-4-3z','M10 11h11v14H10z','M9 13h13v3H9z M12 19h7v4h-7z M12 6h2v4h-2z'],
  coat:['M8 6l6-3 2 4 3-4 5 3 6 18-5 3-3-9v11H10V18l-3 9-5-3z','M11 9h9v17h-9z','M15 9h2v17h-2z M11 17h4v3h-4z M18 13h3v3h-3z'],
  pants:['M7 5h18l2 20-5 4-4-3-2-10-2 10-4 3-5-4z','M9 8h12l-2 7-7 0-2 10-2-1z','M8 7h15v3H8z M20 18h3v7h-3z'],
  badge:['M7 5h18l-2 9 4 5-4 7-7 4-7-4-4-7 4-5z','M9 19l5-6h5l5 6-4 6-4 2-5-3z','M16 15l2 4 4 1-4 2-2 3-2-3-4-2 4-1z'],
  ring:['M9 7l6-4 9 2 4 8-2 10-6 6-10-2-6-7 1-8z M10 13l-1 6 5 5 6-1 3-6-2-6-6-2z','M9 11l4-5 8 1 4 5-3 1-3-4-6 1-2 5z','M16 4h6v5h-6z M9 21l4 4-1 2-5-4z'],
  watch:['M12 3h8v6l5 3v10l-5 4v4h-8v-5l-5-3V12l5-3z','M10 14h12v8H10z','M15 12h2v5l4 1-1 2-5-2z'],
  book:['M4 8l11-4 13 3v19l-12 3-12-4z','M7 10l8-3v18l-8-2z M17 7l8 2v14l-8 2z','M8 12l5-2v2l-5 2z M18 12l5 1v2l-5-1z M18 17l5 1v2l-5-1z'],
  blade:['M3 24l8-9 2 1L24 3l5 1-2 8-10 9 1 2-8 7z','M15 16L25 5l2 1-4 8-7 5z','M5 24l7-7 2 2-7 7z M22 7l3-2 1 1-6 7z'],
  axe:['M6 27L18 13l-2-4 6-5 7 3v8l-5 4-4-2-10 12z','M19 10l4-4 4 2v6l-3 2-4-2z','M22 7l4 1v5l-3 2v-3z M7 26l5-7 2 1-5 7z'],
  spear:['M3 26l15-14-1-3 10-6 3 1-6 11-3-1L7 30z','M20 10l7-5-3 7-3 1z','M5 25l12-11 2 1-12 12z'],
  club:['M4 25l10-12 1-6 7-4 7 4v6l-6 7-6-1-10 11z','M17 8l7-3 3 3-1 5-6 5-3-3z','M21 7l4 1v3h-3z M6 25l9-10 2 2-9 10z'],
  bow:['M7 3l10 2 8 7 3 9-1 8-5-2-3-7-8-8-5-4z M7 7L23 27l-1 1L6 9z','M10 6l7 2 6 6 3 9-1 3-3-8-8-8z','M15 11l3 1 5 6-1 2z M8 4h3v2H8z'],
  crossbow:['M4 7l12 4 12-7 2 4-7 10-4-1-5 12-5-1 2-13-8-4z','M7 9l10 4 10-6-5 8-6-1-4 11-1-2 3-10z','M17 11l5 1-1 4-5-1z'],
  pistol:['M5 7h23v8l-8 2-5-1-3 11H5l-2-5 5-8H4z','M8 9h16v4H9z M8 17h5l-3 8H6z','M8 8h15v2H8z M21 13h5v2h-5z'],
  rifle:['M3 24l4-6 7-5L29 4l2 3-12 10 1 5-5 3-3-3-7 7z','M6 22l9-6 3 2-3 4-7 4z','M15 13L28 6l1 1-11 8z M6 23l5-3 1 2-5 4z'],
  shield:['M6 5l10-3 10 3 2 5-2 13-10 7L6 23 4 10z','M8 8l8-3v21l-7-6-2-8z','M8 9l8-3 8 3v3l-8-2-8 2z M15 13h3v9h-3z'],
  radio:['M7 12l4-4h12l5 5v15H5V14z M20 9l3-7 2 1-2 7z','M8 14h14v11H8z','M10 15h8v4h-8z M10 21h4v2h-4z M21 20h3v4h-3z'],
  flag:['M5 4h3v25H5z M8 5l20-2-4 7 4 6-20 2z','M10 7l14-2-3 5 3 4-14 2z','M11 9l8-1-2 3-6 1z'],
  medkit:['M10 8V4h12v4l6 4v13l-4 4H8l-4-4V12z M13 7v3h6V7z','M7 13h18v12H7z','M14 13h4v4h5v4h-5v4h-4v-4h-5v-4h5z'],
  cartridge:['M5 22l11-14 6-4 4 1 1 4-5 7-11 13z','M7 22l8-10 5 3-9 11z','M15 11l6-5 3 1-4 6z M7 22l5 3-2 3-4-3z'],
  carcass:['M4 21l4-8 8-3 8 3 5 7-3 6-8 2-11-2z','M7 19l6-6 7 1 6 7-6 4-10-2z','M7 20l5-3 2 2 6-2 2 3-5 3-4-2-4 2z'],
  crystal:['M4 20l5-12 8-5 9 5 3 13-7 7H11z','M8 20l4-10 5-4v19l-7-1z','M17 7l5 3 2 7-6 3z M11 14l2-4 2 2-2 4z'],
  compass:['M12 3h8v3l6 4 3 6-3 9-7 4h-7l-7-5-2-8 4-7 5-3z','M8 13l5-5 8 1 4 6-2 8-8 3-6-4z','M13 21l2-10 6-2-2 10z'],
  boots:['M9 4h12l1 12 6 5v6l-6 2H6l-3-5 4-6z','M10 8h8v11l5 4H7l3-5z','M10 10h9v2h-9z M7 24h17v2H7z'],
  hand:['M6 17l4-3V9l5-4h8l5 6v10l-6 8H11z','M10 16l5-7 8-1 3 4-3 8-9 5z','M14 9h3v5h-3z M19 8h3v5h-3z M10 19h7v3h-7z'],
  heart:['M3 10l4-4h6l3 4 3-4h6l4 4v7L16 29 3 17z','M5 11l3-3h4l4 5 4-5h4l3 3v4L16 26 5 16z','M7 12h3l2 3-2 2-3-3z'],
  bolt:['M15 2h11l-7 11h8L10 31l3-13H5z','M16 5h5l-8 11h5l-4 8 9-9h-7z','M17 6h3l-6 8h-3z'],
  eye:['M2 16l7-7 7-3 7 3 7 7-7 7-7 3-7-3z','M6 16l6-5 4-2 5 3 5 4-5 4-5 3-5-2z','M14 12h4l3 4-3 4h-4l-3-4z'],
  lock:['M9 12V7l4-4h6l4 4v5l4 2v12l-3 3H8l-3-3V14z M12 8v5h8V8l-2-2h-4z','M8 17h16v8H8z','M14 17h4v3l-1 1v4h-2v-4l-1-1z'],
  warning:['M14 3h4l12 22-2 4H4l-2-4z','M15 7h2l10 19H5z','M14 12h4v8h-4z M14 22h4v3h-4z'],
  arrow:['M16 3l14 13-14 13v-8H3V11h13z','M18 7l9 9-9 9v-7H6v-4h12z','M18 8l6 6h-6z'],
  cross:['M6 3l10 10L26 3l3 3-10 10 10 10-3 3-10-10L6 29l-3-3 10-10L3 6z','M6 5l11 11-2 2L4 7z M27 24l-2 2-9-9 2-2z','M25 5l2 2-8 8-2-2z'],
  plus:['M12 3h8v9h9v8h-9v9h-8v-9H3v-8h9z','M14 5h4v9h9v4h-9v9h-4v-9H5v-4h9z','M15 6h2v8h-2z M6 15h8v2H6z'],
  clock:['M11 3h10l8 8v10l-8 8H11l-8-8V11z','M12 6h8l6 6v8l-6 6h-8l-6-6v-8z','M15 8h3v7l6 3-2 3-7-4z'],
  moon:['M19 3l-4 8 2 7 6 4 7-1-6 7H12l-8-8-1-8 6-7z','M12 6l-5 6v6l6 7h9l2-2-7-3-4-5z','M10 9l-2 4 2 2 2-5z'],
  compassPin:['M10 4h12l6 6v8L16 30 4 18v-8z M11 12v5h10v-5l-3-3h-4z','M7 12l4-6h7l-7 6v5l5 8-9-8z','M12 12h3v4h-3z M18 21l4-4h3l-5 6z'],
  map:['M3 8l9-4 9 4 8-4v20l-8 4-9-4-9 4z','M6 10l5-2v14l-5 2z M15 8l5 3v13l-5-3z','M23 10l4-2v13l-4 2z M9 12h2v3H9z'],
  dialog:['M3 5h26v18H17l-9 7v-7H3z','M6 8h20v12H15l-4 4v-4H6z','M8 12h3v3H8z M14 12h3v3h-3z M20 12h3v3h-3z'],
  menu:['M3 5h26v6H3z M3 13h26v6H3z M3 21h26v6H3z','M6 6h17v2H6z M6 14h17v2H6z M6 22h17v2H6z','M24 6h3v2h-3z M24 14h3v2h-3z M24 22h3v2h-3z'],
  target:['M12 2h8v6l4 1 1 3h5v8h-5l-1 4-4 1v5h-8v-5l-4-1-1-4H2v-8h5l1-4 4-1z M11 12v8h10v-8z','M14 5h4v5h-4z M5 14h5v4H5z M22 14h5v4h-5z M14 22h4v5h-4z','M14 14h4v4h-4z'],
  wall:['M4 9l6-5h12l6 5v18H4z','M7 12h15v12H7z','M9 11h12v2H9z M13 16h7v9h-7z'],
  foliage:['M14 3h4l7 9h-4l7 9H19v8h-6v-8H4l7-9H7z','M15 6l-5 7h4l-6 7h7z','M17 12h3l5 7h-6z'],
  water:['M16 3l5 8 5 7 1 5-4 5H9l-4-5 1-5 5-7z','M10 17l4-7 3 4-4 9H9z','M10 19h3v4h-3z M16 25h7v2h-7z'],
  sparkle:['M16 2l4 10 10 4-10 4-4 10-4-10-10-4 10-4z','M16 7l2 7 7 2-7 2-2 7-2-7-7-2 7-2z','M16 12l3 4-3 4-3-4z']
};

const itemFamilies={
  bottle:'dirty_water clean_water cooking_oil oil disinfectant saline oral_rehydration emergency_tonic herbal_drink',
  canteen:'ration_canteen water_filter', vial:'antiviral_precursor lab_reagent adhesive antibiotics antiviral_cure herb_extract',
  tablet:'purification_tablet antidiarrheal_powder', sachet:'expedition_bar trail_ration salt filter_media',
  can:'canned_food canned_beans berry_jam', berry:'berry herb dried_berries', root:'raw_root cooked_root boiled_root fried_root',
  meat:'raw_meat cooked_meat boiled_meat', bowl:'root_porridge meat_stew stew_portion', sack:'grain',
  bread:'grain_cake hardtack', strips:'dried_meat', plank:'wood plank', rock:'stone charcoal iron_ore clay', soap:'soap',
  scrap:'scrap steel', sheet:'iron_plate rubber plastic', cloth:'cloth hide leather ballistic_fiber canvas cleaning_wipe medical_gauze',
  rope:'fiber rope thread wire cable_bundle', part:'parts fastener precision_parts weapon_parts spring', tubes:'metal_tube ceramic_shell filter_cartridge',
  chip:'electronics command_terminal', battery:'battery', lamp:'hand_lamp flash_marker', firekit:'fire_starter lighter',
  patch:'repair_patch field_patch field_patch_green field_patch_blue rally_patch brace_pack bandage sterile_bandage compression_bandage',
  crate:'repair_kit gun_service_kit', bag:'ration_pouch', bed:'field_bedroll', tent:'field_tarp', stove:'folding_stove',
  pot:'pot', glove:'knuckle_wraps gather_gloves', vest:'padded_vest leather_armor scrap_armor guard_armor scout_armor heavy_armor medic_armor balanced_armor scavenge_vest',
  coat:'work_shirt forage_jacket craft_coat rest_coat expedition_coat',
  pants:'work_trousers trail_trousers utility_trousers reinforced_trousers recovery_trousers expedition_trousers',
  badge:'guard_badge hunter_token aid_badge resolve_medal swift_pendant filter_token', ring:'steady_ring focus_lens', watch:'tactics_watch',
  book:'repair_manual medical_notes', blade:'stone_knife kitchen_knife machete guard_sword',
  axe:'hatchet fire_axe breach_axe rescue_axe', spear:'wooden_spear metal_spear pike barbed_spear arrow bolt',
  club:'crowbar wooden_club steel_mace shock_baton', bow:'wooden_bow recurve_bow hunter_bow',
  crossbow:'crossbow repeater_crossbow', pistol:'pistol service_pistol',
  rifle:'shotgun rifle marksman_rifle', shield:'wooden_shield scrap_shield riot_shield rescue_shield',
  radio:'field_radio signal_whistle', flag:'signal_flag', medkit:'first_aid_pack field_medkit trauma_bag rescue_medkit triage_kit',
  cartridge:'ammo_9mm shell_12g ammo_rifle', carcass:'carcass_feral_dog carcass_wolf carcass_boar carcass_stag',
  crystal:'active_substance_white_unidentified', compass:'route_compass'
};
const knownItems=new Map(Object.entries(itemFamilies).flatMap(([shape,ids])=>ids.split(' ').map((id,index)=>[id,{shape,index}])));

const glyphs={
  health:'heart', satiety:'bowl', hydration:'water', energy:'bolt', wakefulness:'eye', morale:'flag',
  immunity:'shield', virus:'crystal', bleed:'water', stagger:'sparkle', slow:'boots', fear:'warning', panic:'warning',
  disabled:'lock', warning:'warning', selected:'target', drop_allow:'arrow', drop_deny:'cross',
  progress:'clock', loading:'clock', focus:'target', drag:'hand', transfer:'arrow', close:'cross', add:'plus',
  rest:'moon', map:'map', dialogue:'dialog', log:'book', goal:'flag', help:'book', menu:'menu',
  battle:'blade', aim:'target', equipment:'vest', craft:'part', formation:'flag', growth:'sparkle',
  pause:'clock', continue:'arrow', fresh:'plus', save:'crate', return:'arrow', history:'book',
  storage:'crate', search:'eye', task:'book', known:'compassPin', unknown:'compassPin',
  route:'map', destination:'compassPin', camp:'tent', dungeon:'wall', recruit:'person', service:'badge',
  watch:'watch', expand:'plus', collapse:'cross', recipe:'book', knowledge:'book', foot:'boots',
  weapon:'blade', armor:'vest', utility:'bag', all:'menu', lock:'lock', trap:'warning'
};
glyphs.recruit='hand';
const glyphPal={health:'hazard',hydration:'water',energy:'amber',immunity:'steel',virus:'violet',
  bleed:'hazard', fear:'hazard', panic:'hazard', warning:'hazard', drop_deny:'hazard',
  drop_allow:'water', focus:'amber', selected:'amber', dialogue:'canvas', map:'canvas'};
const kindPal={water:'water',medical:'medical',food:'amber',material:'canvas',gear:'olive',weapon:'iron',
  tool:'iron',ammo:'amber',carcass:'flesh',substance:'violet'};
const variants=['iron','rust','canvas','steel','olive','amber','medical','hazard','water','violet'];
const variantPal={
  dirty_water:'olive',clean_water:'water', bandage:'canvas',sterile_bandage:'medical',
  compression_bandage:'steel',raw_meat:'flesh',cooked_meat:'rust',boiled_meat:'canvas',
  raw_root:'olive',cooked_root:'rust',boiled_root:'amber',fried_root:'hazard',
  field_patch:'canvas',field_patch_green:'olive',field_patch_blue:'steel',
  fox:'rust', wolf:'steel', healing:'medical'
};

const decorations=[
  '', 'M11 20h6v2h-6z', 'M17 16h3v4h-3z', 'M10 17h3v2h-3z M15 20h3v2h-3z',
  'M14 14h4v2h-4z M14 18h4v2h-4z', 'M11 21h2v2h-2z M19 14h2v2h-2z'
];
export function svg(shape,palette='iron',variant=0) {
  const [outline,mid,edge]=forms[shape]??[];
  if (!outline) throw new Error('No icon geometry for '+shape);
  const [dark,base,light,accent]=inks[palette];
  const detail=decorations[variant%decorations.length];
  return '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32" shape-rendering="crispEdges"><g data-layer="ink">'+
    '<path d="'+outline+'" fill="'+dark+'" stroke="#100f0c" stroke-width="2" stroke-linejoin="bevel" fill-rule="evenodd"/>'+
    '</g><g data-layer="material"><path d="'+mid+'" fill="'+base+'"/><path d="'+edge+'" fill="'+light+'"/></g>'+
    (detail?'<g data-layer="wear"><path d="'+detail+'" fill="'+accent+'" opacity=".62"/></g>':'')+'</svg>\n';
}
export function itemSvg(item) {
  const group=knownItems.get(item.id);
  if(!group) throw new Error('Unclassified item '+item.id);
  const pal=variantPal[item.id] || (group.index===0?kindPal[item.kind]:variants[(group.index+variants.indexOf(kindPal[item.kind]))%variants.length]);
  return {shape:group.shape, source:svg(group.shape,pal,group.index)};
}
export function glyphSvg(key,palette) { return svg(glyphs[key]??key,palette??glyphPal[key]??'canvas'); }
export const glyphKeys=Object.keys(glyphs);

const skillShapes={unarmed:'hand',blade:'blade',axe:'axe',spear:'spear',blunt:'club',archery:'bow',
  firearms:'rifle',shield:'shield',command:'flag',medicine:'medkit',crafting:'part',gathering:'foliage',
  scavenging:'eye',cooking:'pot',athletics:'boots'};
export function skillSvg(id,level=0,name='') {
  const shape=skillShapes[id]??'sparkle';
  const tier=[0,1,3,5,7,10].indexOf(level);
  const color=['canvas','iron','olive','steel','rust','amber'][Math.max(tier,0)];
  const silhouette=svg(shape,color,Math.max(tier,0));
  if(!name) return silhouette;
  const effects=[
    [/保护|护身|稳固|掩护|坚守|架势|架矛/,'shield'],[/包扎|护理|创伤|伤势|救护/,'plus'],
    [/震|退|整队|突进|步伐|行进/,'arrow'],[/割伤|致残/,'water'],[/装填/,'cartridge'],
    [/精准|猎标|专长|规划|辨识|检索/,'target'],[/压制|重锤|破甲|破阵/,'warning'],
    [/连|总攻|协同|炊事/,'bolt'],[/装配|加工|制造|运用/,'part']
  ];
  const mark=effects.find(([pattern])=>pattern.test(name))?.[1];
  if(!mark) return silhouette;
  const detail=svg(mark,'amber').replace(/^<svg[^>]+>/,'').replace(/<\/svg>\n$/,'');
  return silhouette.replace('</svg>','<g data-layer="skill-effect" transform="translate(17 17) scale(.46)">'+detail+'</g></svg>');
}

const resourceForms=[
  [/水源|浅滩|泉|溪|蓄水|雨水|净水|井水|集水|沉淀|涵洞|滴水/, 'water','water'],
  [/木材|树枝|枯木|林木|木板|倒木|木料|伐木/, 'plank','rust'],
  [/果|浆|野菜|蔬/, 'berry','flesh'],
  [/块根|作物|农田|谷物|粮/, 'root','olive'],
  [/草|藤|灌|藤蔓|植/, 'foliage','olive'],
  [/石|矿|煤|黏土|陶/, 'rock','rust'],
  [/电|线缆|通讯|终端|仪器|面板/, 'chip','steel'],
  [/机械|机械|零件|废铁|金属|配电|铁屑|残骸/, 'scrap','iron'],
  [/军|武器|枪|警备|警械|守卫|军械/, 'crate','olive'],
  [/药|医|急救|防疫|标本/, 'medkit','medical'],
  [/档案|记录|纸|文件|书|图纸|知识|清单|手册|告示/, 'book','canvas'],
  [/布|服装|衣|织|纤维|帆布/, 'cloth','canvas'],
  [/食|罐|冷柜|冷藏/, 'can','amber'],
  [/锅|炉|灶|燃料/, 'pot','iron'],
  [/骨|巢|兽|尸/, 'carcass','flesh'],
  [/工具|维修|工作|作业/, 'part','iron'],
  [/油|液|试剂|样本/, 'vial','steel'],
  [/箱|柜|包|仓|堆|车|架|货/, 'crate','canvas']
];
export function resourceSvg(name,variant) {
  const [,shape,palette]=resourceForms.find(([pattern])=>pattern.test(name))??[null,'crate','iron'];
  return {shape,source:svg(shape,palette,variant)};
}
export const facilityShape={
  hand:'hand',workbench:'part',forge:'flame',sewing:'cloth',medical:'medkit',armory:'rifle',kiln:'stove',
  water_station:'water',drying_rack:'strips',fire_lit:'flame',fire_out:'stove',pot:'pot',bed:'bed',
  shelter:'tent',safehouse:'wall',camping:'tent',catchment:'water',storage:'crate',charcoal:'rock'
};
