#!/usr/bin/env node
// 本地 Release 包静态服务器（零依赖）。推送前在本机试玩发布版，不用等 GitHub Pages。
// 先发布：dotnet publish web/Survival.Web -c Release -o publish/local
// 再起服务，试玩用 cdp.mjs open http://localhost:5199/?dev=1
//
//   node tools/playtest/serve.mjs start [目录] [端口]   后台启动（默认 publish/local/wwwroot 5199），等能访问才返回；
//                                                       没写端口且 5199 被别的程序（或另一份包）占着时自动换下一个空闲端口，打印实际地址
//   node tools/playtest/serve.mjs stop [端口]           关闭后台那台（没写端口就关本目录最近一次 start 的那台）
//   node tools/playtest/serve.mjs run [目录] [端口]     前台运行（给 .claude/launch.json 的 preview_start 用）
//
// 所有响应都带 no-store：index.html 里的 ?v=dev 不会让浏览器拿到上一次发布的旧 js/css。
import { createServer } from 'node:http';
import { spawn } from 'node:child_process';
import { existsSync, readFileSync, writeFileSync, rmSync, statSync, createReadStream } from 'node:fs';
import { tmpdir } from 'node:os';
import { createServer as netServer } from 'node:net';
import { createHash } from 'node:crypto';
import { join, resolve, extname, sep } from 'node:path';

const [cmd = 'run', ...args] = process.argv.slice(2);
const isPort = s => /^\d+$/.test(s || '');
const explicitPort = args.some(isPort);
const root = resolve(args.find(a => !isPort(a)) || 'publish/local/wwwroot');
// 本目录最近一次 start 的地址记在这里（run.mjs 不写 --base 时读它）；按目录区分，各 worktree 互不干扰
const currentFile = dir => join(tmpdir(), `survival-serve-root-${createHash('md5').update(dir.toLowerCase()).digest('hex').slice(0, 8)}.json`);
const cur = (() => { try { return JSON.parse(readFileSync(currentFile(root), 'utf8')); } catch { return null; } })();
let port = Number(args.find(isPort) || (cmd === 'stop' && cur?.port) || 5199);
const pidFileOf = p => join(tmpdir(), `survival-serve-${p}.pid`);
const urlOf = p => `http://localhost:${p}/`;
let url = urlOf(port);
const sleep = ms => new Promise(r => setTimeout(r, ms));
// 端口能不能自己监听（被任何程序占着都算不能）
const portFree = p => new Promise(r => { const s = netServer().once('error', () => r(false)).once('listening', () => s.close(() => r(true))).listen(p); });
// 占着端口的是不是本工具服务的同一目录（服务带 X-Survival-Root 头）
async function servesRoot(p) {
  try { const h = (await fetch(urlOf(p), { cache: 'no-store' })).headers.get('x-survival-root'); return h != null && decodeURIComponent(h) === root; } catch { return false; }
}

const TYPES = {
  '.html': 'text/html; charset=utf-8', '.js': 'text/javascript; charset=utf-8', '.mjs': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8', '.json': 'application/json; charset=utf-8', '.wasm': 'application/wasm',
  '.dll': 'application/octet-stream', '.pdb': 'application/octet-stream', '.dat': 'application/octet-stream',
  '.blat': 'application/octet-stream', '.png': 'image/png', '.jpg': 'image/jpeg', '.jpeg': 'image/jpeg',
  '.webp': 'image/webp', '.gif': 'image/gif', '.svg': 'image/svg+xml', '.ico': 'image/x-icon',
  '.woff': 'font/woff', '.woff2': 'font/woff2', '.ttf': 'font/ttf', '.otf': 'font/otf',
  '.mp3': 'audio/mpeg', '.ogg': 'audio/ogg', '.wav': 'audio/wav', '.txt': 'text/plain; charset=utf-8',
};

async function up() {
  try { return (await fetch(url, { cache: 'no-store' })).ok; } catch { return false; }
}

function serve() {
  if (!existsSync(join(root, 'index.html'))) {
    console.error(`${root} 里没有 index.html，先 dotnet publish web/Survival.Web -c Release -o publish/local`);
    process.exit(1);
  }
  createServer((req, res) => {
    let path = decodeURIComponent(new URL(req.url, url).pathname);
    let file = resolve(join(root, path));
    if (file !== root && !file.startsWith(root + sep)) { res.writeHead(403).end(); return; }
    if (existsSync(file) && statSync(file).isDirectory()) file = join(file, 'index.html');
    // 和 Pages 的 404.html 一样：没有扩展名的路由交给 index.html
    if (!existsSync(file) && !extname(path)) file = join(root, 'index.html');
    if (!existsSync(file)) { res.writeHead(404, { 'Cache-Control': 'no-store' }).end('404'); return; }
    res.writeHead(200, { 'Content-Type': TYPES[extname(file).toLowerCase()] || 'application/octet-stream', 'Cache-Control': 'no-store', 'X-Survival-Root': encodeURIComponent(root) });
    createReadStream(file).pipe(res);
  }).listen(port, () => console.log(`本地 Release 包：${url}（目录 ${root}）`));
}

if (cmd === 'run') {
  serve();
} else if (cmd === 'start') {
  // 2026-11-19：5199 常被别的会话占用（B56 两轮都撞上），以前这里只看“能访问”就说已在运行，可能拿别的包去试
  for (let tries = 0; !(await portFree(port)); tries++) {
    if (await servesRoot(port)) { writeFileSync(currentFile(root), JSON.stringify({ url, port })); console.log(`${url} 已在运行（目录 ${root}）`); process.exit(0); }
    if (explicitPort || tries >= 20) { console.error(`端口 ${port} 被别的程序占用（不是本目录的服务）`); process.exit(1); }
    console.log(`端口 ${port} 被别的程序占用，改用 ${port + 1}`);
    port++; url = urlOf(port);
  }
  const child = spawn(process.execPath, [process.argv[1], 'run', root, String(port)], { detached: true, stdio: 'ignore', windowsHide: true });
  child.unref();
  writeFileSync(pidFileOf(port), String(child.pid));
  writeFileSync(currentFile(root), JSON.stringify({ url, port }));
  for (let i = 0; i < 50 && !(await up()); i++) await sleep(200);
  if (!(await up())) { console.error(`启动失败：${url} 没有响应（目录 ${root} 里有 index.html 吗）`); process.exit(1); }
  console.log(`已启动 ${url}（目录 ${root}，pid ${child.pid}）`);
} else if (cmd === 'stop') {
  const pidFile = pidFileOf(port);
  if (!existsSync(pidFile)) { console.log(`没有端口 ${port} 的后台服务记录`); process.exit(0); }
  try { process.kill(Number(readFileSync(pidFile, 'utf8'))); } catch {}
  rmSync(pidFile, { force: true });
  if (cur?.port === port) rmSync(currentFile(root), { force: true });
  console.log(`已关闭端口 ${port}`);
} else {
  console.error('用法：serve.mjs start|stop|run [目录] [端口]');
  process.exit(1);
}
