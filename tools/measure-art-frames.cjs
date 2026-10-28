// Read alpha bounds only; keep every original atlas pixel unchanged.
// Usage: node tools/measure-art-frames.cjs [sheet columns rows]
const sharp = require('sharp');
(async () => {
  const file = process.argv[2];
  const cols = Number(process.argv[3] || 4), rows = Number(process.argv[4] || 4);
  const { data, info } = await sharp(file).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
  const frames = [];
  for (let row = 0; row < rows; row++) for (let col = 0; col < cols; col++) {
    const x0 = Math.round(col * info.width / cols), x1 = Math.round((col + 1) * info.width / cols);
    const y0 = Math.round(row * info.height / rows), y1 = Math.round((row + 1) * info.height / rows);
    let left = x1, top = y1, right = x0, bottom = y0;
    for (let y = y0; y < y1; y++) for (let x = x0; x < x1; x++) {
      if (data[(y * info.width + x) * 4 + 3] <= 24) continue;
      left = Math.min(left, x); right = Math.max(right, x);
      top = Math.min(top, y); bottom = Math.max(bottom, y);
    }
    const ux = info.width / cols / 100, uy = info.height / rows / 100;
    // A small guard band includes anti-aliased edges and painted shadows.
    const l = Math.max(0, Math.floor((left - x0) / ux) - 3);
    const t = Math.max(0, Math.floor((top - y0) / uy) - 3);
    const r = Math.min(100, Math.ceil((right - x0 + 1) / ux) + 3);
    const b = Math.min(100, Math.ceil((bottom - y0 + 1) / uy) + 3);
    frames.push([l, t, r - l, b - t]);
  }
  console.log(JSON.stringify({ width: info.width, height: info.height, cols, rows, frames }));
})().catch(e => { console.error(e); process.exitCode = 1; });
