"""B37a1（Q806、Q839、Q840）：从冻结评审提交 a9b93f0 原样取 ink-v2 素材，生成运行时清单。

- 只取 a9b93f0 manifest 引用到的文件，字节原样写入，不解码、不重编码、不预缩（Q840：缓存环节不能先平滑）。
- 同一内容（sha256 相同）只落一份：地牢各层共用的场景图等只保留一个文件名，清单里其余名字指向它。
- 不取 portrait-p0-design.webp（Q839 A：主角只用 manifest 的 actor:p0）。
- 校验：写入后逐个重算 sha256；主角肖像哈希须等于 assets/20261117/b37-assets-and-appearance-q834-q840.json 记录值。

用法（仓库根目录）：python tools/import-b37a1-ink-v2.py
需要本地有 a9b93f0（git fetch origin planning/ink-v2-offline-review）。
"""

import hashlib
import json
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
COMMIT = 'a9b93f0'
SRC_DIR = 'doc/策划案/重墨V2离线评审 v1.0/素材/ink-v2/'
DEST = ROOT / 'web/Survival.Web/wwwroot/art/ink-v2'
RUNTIME = ROOT / 'web/Survival.Web/Art/ink-v2.json'
ACCEPT = ROOT / 'doc/验收/重墨V2 UI重做 v1.0'
SOURCE_COPY = ACCEPT / '素材来源 a9b93f0 manifest.json'
B37_JSON = ROOT / 'doc/策划案/系统策划总册 v1.0/assets/20261117/b37-assets-and-appearance-q834-q840.json'
EXCLUDED = {'portrait-p0-design.webp'}


def git_blob(name: str) -> bytes:
    return subprocess.run(['git', '-C', str(ROOT), 'show', f'{COMMIT}:{SRC_DIR}{name}'],
                          check=True, capture_output=True).stdout


def sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def main() -> int:
    raw = git_blob('manifest.json')
    source = json.loads(raw.decode('utf-8'))
    referenced = set()
    for frame in source['sprites'].values():
        referenced.add(frame['file'])
    referenced.update(source['portraits'].values())
    referenced.update(source['scenes'].values())
    bad = referenced & EXCLUDED
    if bad:
        raise SystemExit(f'manifest unexpectedly references excluded files: {bad}')

    blobs = {name: git_blob(name) for name in sorted(referenced)}
    by_hash: dict[str, list[str]] = {}
    for name, data in blobs.items():
        by_hash.setdefault(sha(data), []).append(name)
    canonical = {}
    files = {}
    for digest, names in sorted(by_hash.items(), key=lambda kv: sorted(kv[1])[0]):
        names.sort()
        keep = names[0]
        for n in names:
            canonical[n] = keep
        files[keep] = {'sha256': digest, 'bytes': len(blobs[keep]), 'sources': names}

    DEST.mkdir(parents=True, exist_ok=True)
    for stale in DEST.iterdir():
        if stale.name not in files:
            stale.unlink()
    for name, info in files.items():
        (DEST / name).write_bytes(blobs[name])
        if sha((DEST / name).read_bytes()) != info['sha256']:
            raise SystemExit(f'sha mismatch after write: {name}')

    b37 = json.loads(B37_JSON.read_text(encoding='utf-8'))
    p0 = b37['protagonist']['portraitSourceAudit']['sha256']
    p0_file = canonical[source['portraits']['actor:p0']]
    if files[p0_file]['sha256'] != p0:
        raise SystemExit(f'actor:p0 portrait sha {files[p0_file]["sha256"]} != B37 record {p0}')

    runtime = {
        'source': f'{COMMIT}:{SRC_DIR}manifest.json',
        'sourceSha256': sha(raw),
        'files': files,
        'sprites': {k: {**v, 'file': canonical[v['file']]} for k, v in sorted(source['sprites'].items())},
        'portraits': {k: canonical[v] for k, v in sorted(source['portraits'].items())},
        'scenes': {k: canonical[v] for k, v in sorted(source['scenes'].items())},
    }
    RUNTIME.parent.mkdir(parents=True, exist_ok=True)
    RUNTIME.write_text(json.dumps(runtime, ensure_ascii=False, indent=1) + '\n', encoding='utf-8')
    ACCEPT.mkdir(parents=True, exist_ok=True)
    SOURCE_COPY.write_bytes(raw)

    total = sum(f['bytes'] for f in files.values())
    print(f'{len(referenced)} referenced names -> {len(files)} unique files, {total / 1048576:.1f} MiB; '
          f'{len(source["sprites"])} sprites, {len(source["portraits"])} portraits, {len(source["scenes"])} scenes')
    return 0


if __name__ == '__main__':
    sys.exit(main())
