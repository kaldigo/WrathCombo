"""Create a testing candidate without changing the source repository's stable feed.

The supplied stable feed must be refreshed when composing a public feed; do not
freeze this candidate's stable entries as an independently maintained install feed.
"""
import argparse
import copy
import json
import re
from pathlib import Path
from package_release import package


def version(value):
    if not isinstance(value, str) or not re.fullmatch(r'\d+\.\d+\.\d+\.\d+', value):
        raise ValueError('Expected a four-part version')
    parts = tuple(map(int, value.split('.')))
    if any(part > 65535 for part in parts):
        raise ValueError('Version component exceeds assembly limits')
    return parts


def overlay(stable, candidate):
    if not isinstance(stable, list):
        raise ValueError('Stable feed must be a plugin list')
    name = candidate['InternalName']
    matches = [entry for entry in stable if entry.get('InternalName') == name]
    if len(matches) != 1:
        raise ValueError('Expected exactly one stable entry with the unchanged identity')
    original = matches[0]
    if original.get('IsTestingExclusive'):
        raise ValueError('Expected a stable entry, not a testing-only identity')
    testing_version = version(candidate['AssemblyVersion'])
    stable_version = version(original['AssemblyVersion'])
    result = copy.deepcopy(stable)
    target = next(entry for entry in result if entry['InternalName'] == name)
    # A subsequent stable release supersedes an older test naturally.
    if testing_version <= stable_version:
        return result
    url = candidate['DownloadLinkTesting']
    if not url.startswith('https://github.com/') or '/releases/download/testing-v' not in url:
        raise ValueError('Testing artifact must use an immutable testing tag')
    target.update(TestingAssemblyVersion=candidate['AssemblyVersion'],
                  TestingDalamudApiLevel=candidate['DalamudApiLevel'], DownloadLinkTesting=url)
    return result


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--tag', required=True)
    parser.add_argument('--stable-feed', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    stable = json.loads(args.stable_feed.read_text(encoding='utf-8-sig'))
    # Validate identity/version before creating any output.
    cfg = json.loads((root / 'release-config.json').read_text())
    built = json.loads((root / cfg['build'] / (cfg['internal_name'] + '.json')).read_text(encoding='utf-8-sig'))
    built['DownloadLinkTesting'] = f"https://github.com/{cfg['repository']}/releases/download/{args.tag}/{cfg['internal_name']}.zip"
    combined = overlay(stable, built)
    package(root, args.tag, 'Opt-in testing build. Native acceptance remains pending.', testing=True)
    output = root / 'artifacts/testing'
    (output / 'pluginmaster.json').write_text(json.dumps(combined, indent=2) + '\n', encoding='utf-8')
    (output / 'testing-release.json').write_text(json.dumps(built, indent=2) + '\n', encoding='utf-8')
    print('Created a candidate only; no release or stable feed was published.')


if __name__ == '__main__':
    main()
