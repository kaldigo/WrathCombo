"""Release boundary checks using isolated build outputs, without live configuration."""
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
import zipfile
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('packager', ROOT / 'tools/package_release.py')
packager = importlib.util.module_from_spec(spec)
spec.loader.exec_module(packager)

class PackageTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        cfg = json.loads((ROOT / 'release-config.json').read_text())
        self.cfg = cfg
        (self.root / 'release-config.json').write_text(json.dumps(cfg))
        self.build = self.root / cfg['build']
        self.build.mkdir(parents=True)
        self.name = cfg['internal_name']
        self.manifest = dict(InternalName=self.name, AssemblyVersion='1.2.3.4', DalamudApiLevel=15)
        self.write_manifest()
        for dll in cfg['required_dlls']:
            (self.build / dll).write_bytes(b'isolated-build-fixture')
        for path in cfg['licenses']:
            target = self.root / path
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_text('fixture notice')
        (self.root / 'assets').mkdir()
        (self.root / 'assets/icon.png').write_bytes((ROOT / 'assets/icon.png').read_bytes())
        (self.root / 'docs').mkdir()
        (self.root / 'docs/publishing.md').write_text('fixture publication notice')

    def write_manifest(self):
        (self.build / (self.name + '.json')).write_text(json.dumps(self.manifest))

    def test_missing_dependency_prevents_release(self):
        (self.build / self.cfg['required_dlls'][-1]).unlink()
        with self.assertRaises(ValueError):
            packager.package(self.root, 'v1.2.3.4')

    def test_wrong_identity_version_or_api_prevents_release(self):
        for key, value in [('InternalName', 'OtherPlugin'), ('AssemblyVersion', '1.2.3.3'), ('DalamudApiLevel', 14)]:
            original = self.manifest[key]
            self.manifest[key] = value
            self.write_manifest()
            with self.assertRaises(ValueError):
                packager.package(self.root, 'v1.2.3.4')
            self.manifest[key] = original

    def test_package_excludes_user_configuration_and_keeps_notices(self):
        (self.build / 'configuration.json').write_text('private fixture')
        archive = packager.package(self.root, 'v1.2.3.4')
        with zipfile.ZipFile(archive) as zipped:
            self.assertNotIn('configuration.json', zipped.namelist())
            self.assertTrue(set(self.cfg['licenses'].values()).issubset(zipped.namelist()))
            manifest = json.loads(zipped.read(self.name + '.json'))
            self.assertEqual(manifest['RepoUrl'], 'https://github.com/' + self.cfg['repository'])
            self.assertIn('/releases/download/v1.2.3.4/', manifest['DownloadLinkInstall'])

    def test_testing_package_preserves_stable_source_feed(self):
        stable = b'unchanged stable feed fixture\n'
        (self.root / 'pluginmaster.json').write_bytes(stable)
        archive = packager.package(self.root, 'testing-v1.2.3.4', testing=True)
        self.assertEqual((self.root / 'pluginmaster.json').read_bytes(), stable)
        self.assertEqual(archive.parent, self.root / 'artifacts/testing')
        with zipfile.ZipFile(archive) as zipped:
            manifest = json.loads(zipped.read(self.name + '.json'))
            self.assertEqual(manifest['InternalName'], self.name)
            self.assertIn('/releases/download/testing-v1.2.3.4/', manifest['DownloadLinkTesting'])

    def test_testing_tag_cannot_enter_stable_packaging(self):
        with self.assertRaises(ValueError):
            packager.package(self.root, 'testing-v1.2.3.4')
        with self.assertRaises(ValueError):
            packager.package(self.root, 'v1.2.3.4', testing=True)

    def test_testing_candidate_command_and_validator(self):
        tools = self.root / 'tools'
        tools.mkdir()
        for script in ['package_release.py', 'package_testing.py', 'check_testing_package.py']:
            (tools / script).write_bytes((ROOT / 'tools' / script).read_bytes())
        stable = self.root / 'stable.json'
        stable.write_text(json.dumps([dict(InternalName=self.name, AssemblyVersion='1.2.3.3',
                                          DownloadLinkInstall='stable-install', DownloadLinkUpdate='stable-update')]))
        (self.root / 'pluginmaster.json').write_text('unchanged stable source')
        arguments = ['--tag', 'testing-v1.2.3.4', '--stable-feed', str(stable)]
        for script in ['package_testing.py', 'check_testing_package.py']:
            result = subprocess.run([sys.executable, str(tools / script), *arguments], capture_output=True, text=True)
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertEqual((self.root / 'pluginmaster.json').read_text(), 'unchanged stable source')
        feed = self.root / 'artifacts/testing/pluginmaster.json'
        content = json.loads(feed.read_text())
        content[0]['DownloadLinkUpdate'] = 'accidental-stable-change'
        feed.write_text(json.dumps(content))
        result = subprocess.run([sys.executable, str(tools / 'check_testing_package.py'), *arguments], capture_output=True, text=True)
        self.assertNotEqual(result.returncode, 0, 'Validator accepted a changed stable update link')
