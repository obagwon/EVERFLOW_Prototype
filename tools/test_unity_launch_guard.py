"""Pure guard tests; do not launch or connect to Unity."""
import unittest
from pathlib import Path
import tempfile
from unittest.mock import patch
import run_mvp_experiment_unity as runner
from run_mvp_experiment_unity import launch_blockers


class LaunchGuardTests(unittest.TestCase):
    def context(self, processes=None, identity='HOME\\vzd12'):
        return {'actualIdentity': identity, 'environmentUser': 'vzd12', 'unityProcesses': processes or []}

    def test_real_token_mismatch_blocks_inherited_profile(self):
        self.assertTrue(launch_blockers(self.context(identity='HOME\\CodexSandboxOffline')))

    def test_same_account_without_editor_has_no_account_blocker(self):
        self.assertEqual(launch_blockers(self.context()), [])

    def test_hub_helper_is_not_an_editor(self):
        self.assertEqual(launch_blockers(self.context([{'pid': 1, 'path': 'C:\\Program Files\\Unity Hub\\resources\\unity.exe'}])), [])

    def test_different_editor_version_still_blocks_second_editor(self):
        self.assertTrue(launch_blockers(self.context([{'pid': 2, 'path': 'C:\\Program Files\\Unity\\Hub\\Editor\\6000.5.3f1\\Editor\\Unity.exe'}])))

    def test_unknown_process_path_fails_closed(self):
        self.assertTrue(launch_blockers(self.context([{'pid': 3, 'path': None}])))

    def test_missing_identity_fails_closed(self):
        self.assertTrue(launch_blockers({}))

    def test_main_account_mismatch_never_calls_popen(self):
        with tempfile.TemporaryDirectory() as directory:
            with patch.object(runner, 'ROOT', Path(directory)), patch.object(runner, 'OUT', Path(directory)), \
                    patch.object(runner, 'inspect_launch_context', return_value=self.context(identity='HOME\\CodexSandboxOffline')), \
                    patch.object(runner.subprocess, 'Popen') as popen, patch('sys.argv', ['runner', 'create']):
                with self.assertRaises(SystemExit):
                    runner.main()
                popen.assert_not_called()

    def test_main_unity_lock_never_calls_popen(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / 'Temp').mkdir()
            lock = root / 'Temp/UnityLockfile'
            lock.write_text('existing Editor lock', encoding='utf-8')
            with patch.object(runner, 'ROOT', root), patch.object(runner, 'OUT', root), \
                    patch.object(runner, 'inspect_launch_context', return_value=self.context()), \
                    patch.object(runner.subprocess, 'Popen') as popen, patch('sys.argv', ['runner', 'create']):
                with self.assertRaises(SystemExit):
                    runner.main()
                popen.assert_not_called()
                self.assertEqual(lock.read_text(), 'existing Editor lock')

    def test_own_launch_reservation_rejects_second_helper(self):
        with tempfile.TemporaryDirectory() as directory:
            with patch.object(runner, 'ROOT', Path(directory)):
                with runner.reserve_batch_launch():
                    with self.assertRaises(SystemExit):
                        with runner.reserve_batch_launch():
                            self.fail('Second reservation was accepted')
                self.assertFalse((Path(directory) / 'Temp/mvp-experiment/batch-launch.lock').exists())

    def test_editor_appearing_at_final_recheck_never_calls_popen(self):
        editor = self.context([{'pid': 4, 'path': 'C:/Unity/Editor/Unity.exe'}])
        with tempfile.TemporaryDirectory() as directory:
            with patch.object(runner, 'ROOT', Path(directory)), patch.object(runner, 'OUT', Path(directory)), \
                    patch.object(runner, 'inspect_launch_context', side_effect=[self.context(), editor]), \
                    patch.object(runner.subprocess, 'Popen') as popen, patch('sys.argv', ['runner', 'create']):
                with self.assertRaises(SystemExit):
                    runner.main()
                popen.assert_not_called()


if __name__ == '__main__':
    unittest.main()
