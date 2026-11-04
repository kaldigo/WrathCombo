"""Validate testing artifacts separately from the unchanged stable release format."""
import argparse
import hashlib
import json
from pathlib import Path
import zipfile
from package_testing import overlay


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--tag', required=True)
    parser.add_argument('--stable-feed', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    cfg = json.loads((root / 'release-config.json').read_text())
    name = cfg['internal_name']
    output = root / 'artifacts/testing'
    archive = output / (name + '.zip')
    if not args.tag.startswith('testing-v'):
        raise ValueError('Testing tag required')
    with zipfile.ZipFile(archive) as zipped:
        if zipped.testzip() is not None:
            raise ValueError('Corrupted archive')
        manifest = json.loads(zipped.read(name + '.json'))
        if manifest['InternalName'] != name or manifest['AssemblyVersion'] != args.tag[len('testing-v'):]:
            raise ValueError('Identity/version mismatch')
        required = set(cfg['required_dlls'] + list(cfg['licenses'].values()) + ['images/icon.png', name + '.json'])
        if not required.issubset(zipped.namelist()):
            raise ValueError('Missing runtime dependency or notice')
        if any('configuration' in path.lower() or '..' in Path(path).parts or path.startswith('/') for path in zipped.namelist()):
            raise ValueError('Unexpected private or unsafe file')
        expected = f"https://github.com/{cfg['repository']}/releases/download/{args.tag}/{name}.zip"
        if any(manifest.get(key) != expected for key in ['DownloadLinkInstall', 'DownloadLinkUpdate', 'DownloadLinkTesting']):
            raise ValueError('Package links do not match immutable testing tag')
    stable = json.loads(args.stable_feed.read_text(encoding='utf-8-sig'))
    if json.loads((output / 'pluginmaster.json').read_text()) != overlay(stable, manifest):
        raise ValueError('Testing feed changed stable information')
    if (output / 'SHA256SUMS.txt').read_text().split() != [hashlib.sha256(archive.read_bytes()).hexdigest(), archive.name]:
        raise ValueError('Archive checksum mismatch')
    print('Validated testing candidate:', name, args.tag)


if __name__ == '__main__':
    main()
