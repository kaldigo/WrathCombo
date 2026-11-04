import copy
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'tools'))
from package_testing import overlay


class TestingFeedTests(unittest.TestCase):
    def setUp(self):
        self.stable = [dict(InternalName='Accord', AssemblyVersion='1.2.3.4',
                            DownloadLinkInstall='stable-install', DownloadLinkUpdate='stable-update',
                            DownloadLinkTesting='stable-test', Description='Keep this'),
                       dict(InternalName='Other', AssemblyVersion='4.3.2.1')]
        self.candidate = dict(InternalName='Accord', AssemblyVersion='1.2.3.5', DalamudApiLevel=15,
                              DownloadLinkTesting='https://github.com/owner/repo/releases/download/testing-v1.2.3.5/Accord.zip')

    def test_stable_updates_are_preserved(self):
        before = copy.deepcopy(self.stable)
        result = overlay(self.stable, self.candidate)
        self.assertEqual(self.stable, before)
        for key, value in before[0].items():
            if key != 'DownloadLinkTesting':
                self.assertEqual(result[0][key], value)
        self.assertEqual(result[1], before[1])
        self.assertEqual(result[0]['TestingAssemblyVersion'], '1.2.3.5')

    def test_newer_stable_supersedes_testing(self):
        self.stable[0]['AssemblyVersion'] = '1.2.3.6'
        self.assertEqual(overlay(self.stable, self.candidate), self.stable)

    def test_identity_mismatch_and_duplicate_rejected(self):
        with self.assertRaises(ValueError):
            overlay(self.stable[1:], self.candidate)
        with self.assertRaises(ValueError):
            overlay(self.stable + [self.stable[0]], self.candidate)

    def test_mutable_or_stable_tag_rejected(self):
        self.candidate['DownloadLinkTesting'] = 'https://github.com/owner/repo/releases/latest/Accord.zip'
        with self.assertRaises(ValueError):
            overlay(self.stable, self.candidate)

    def test_invalid_versions_rejected(self):
        for value in ['1.2', '1.2.3.65536', 'next', '-1.2.3.4']:
            self.candidate['AssemblyVersion'] = value
            with self.assertRaises(ValueError):
                overlay(self.stable, self.candidate)
