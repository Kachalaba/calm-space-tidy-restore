import binascii
import json
import struct
import subprocess
import sys
import tempfile
import unittest
import zlib
from pathlib import Path


SCRIPT = Path(__file__).parents[1] / "android_archive_audit.py"
APK_SIGNING_MAGIC = b"APK Sig Block 42"


def _entry_record(name, payload, *, descriptor=False, method=0, local_extra=b""):
    name_bytes = name.encode("utf-8")
    if method == 0:
        compressed = payload
    elif method == 8:
        compressor = zlib.compressobj(level=6, wbits=-15)
        compressed = compressor.compress(payload) + compressor.flush()
    else:
        raise ValueError("fixture only supports stored and deflated entries")

    crc = binascii.crc32(payload) & 0xFFFFFFFF
    flags = 0x0800 | (0x0008 if descriptor else 0)
    local_crc = 0 if descriptor else crc
    local_size = 0 if descriptor else len(compressed)
    local = struct.pack(
        "<I5H3L2H",
        0x04034B50,
        20,
        flags,
        method,
        0,
        0,
        local_crc,
        local_size,
        local_size if descriptor else len(payload),
        len(name_bytes),
        len(local_extra),
    ) + name_bytes + local_extra + compressed
    if descriptor:
        local += struct.pack("<IIII", 0x08074B50, crc, len(compressed), len(payload))
    return {
        "name": name,
        "name_bytes": name_bytes,
        "payload": payload,
        "compressed": compressed,
        "crc": crc,
        "flags": flags,
        "method": method,
        "local": local,
    }


def _central_record(entry, offset, *, extra=b""):
    return struct.pack(
        "<I6H3L5H2L",
        0x02014B50,
        20,
        20,
        entry["flags"],
        entry["method"],
        0,
        0,
        entry["crc"],
        len(entry["compressed"]),
        len(entry["payload"]),
        len(entry["name_bytes"]),
        len(extra),
        0,
        0,
        0,
        0,
        offset,
    ) + entry["name_bytes"] + extra


def _signing_block():
    size_without_first_word = 24
    return (
        struct.pack("<Q", size_without_first_word)
        + struct.pack("<Q", size_without_first_word)
        + APK_SIGNING_MAGIC
    )


def build_archive(
    path,
    entries,
    *,
    orphans=(),
    signing=False,
    before_eocd=b"",
    trailing=b"",
    truncate_descriptor=False,
    zip64_eocd=False,
):
    body = bytearray()
    for orphan in orphans:
        body += orphan["local"]

    offsets = []
    for entry in entries:
        offsets.append(len(body))
        local = entry["local"]
        if truncate_descriptor and entry["flags"] & 0x0008:
            local = local[:-1]
        body += local

    if signing:
        body += _signing_block()

    central_offset = len(body)
    central = b"".join(
        _central_record(entry, offset) for entry, offset in zip(entries, offsets)
    )
    body += central
    body += before_eocd
    count = 0xFFFF if zip64_eocd else len(entries)
    body += struct.pack(
        "<I4H2LH",
        0x06054B50,
        0,
        0,
        count,
        count,
        len(central),
        central_offset,
        0,
    )
    body += trailing
    path.write_bytes(body)
    return bytes(body)


class AndroidArchiveAuditTests(unittest.TestCase):
    def run_audit(self, directory, *arguments):
        output = directory / "audit.json"
        result = subprocess.run(
            [sys.executable, str(SCRIPT), *map(str, arguments), "--output", str(output)],
            capture_output=True,
            text=True,
            check=False,
        )
        report = json.loads(output.read_text()) if output.exists() else None
        return result, report, output

    def test_reconciles_payload_headers_descriptor_free_record_signing_and_tail(self):
        # Catches omitting any physical ZIP region from exact reconciliation.
        with tempfile.TemporaryDirectory() as temporary:
            directory = Path(temporary)
            archive = directory / "fixture.apk"
            canonical = _entry_record("a.txt", b"abc", descriptor=True)
            orphan = _entry_record("old.bin", b"obsolete")
            data = build_archive(
                archive,
                [canonical],
                orphans=[orphan],
                signing=True,
                trailing=b"TAIL",
            )

            result, report, _ = self.run_audit(directory, "--debug-apk", archive)

            self.assertEqual(result.returncode, 0, result.stderr)
            artifact = report["artifacts"][0]
            self.assertEqual(len(data), 208)
            self.assertEqual(
                artifact["physical"],
                {
                    "apk_signing_block_bytes": 32,
                    "canonical_payload_bytes": 3,
                    "central_directory_bytes": 51,
                    "data_descriptor_bytes": 16,
                    "eocd_and_trailing_bytes": 26,
                    "free_record_bytes": 45,
                    "local_header_bytes": 35,
                    "reconciled_bytes": 208,
                },
            )
            self.assertEqual(artifact["file_size_bytes"], 208)
            self.assertEqual(
                artifact["free_records"],
                [{"name": "old.bin", "physical_bytes": 45}],
            )
            self.assertEqual(artifact["policy"]["status"], "warning")

    def test_release_rejects_nonzero_unreferenced_local_records(self):
        # Catches accidentally downgrading release free bytes to a warning.
        with tempfile.TemporaryDirectory() as temporary:
            directory = Path(temporary)
            archive = directory / "release.apk"
            build_archive(
                archive,
                [_entry_record("a.txt", b"new")],
                orphans=[_entry_record("a.txt", b"old")],
            )

            result, report, _ = self.run_audit(directory, "--release-apk", archive)

            self.assertEqual(result.returncode, 1)
            self.assertEqual(report["overall_status"], "fail")
            self.assertEqual(report["artifacts"][0]["policy"]["status"], "fail")

    def test_debug_allows_unreferenced_local_records_as_warning(self):
        # Catches applying release rejection policy to a debug baseline.
        with tempfile.TemporaryDirectory() as temporary:
            directory = Path(temporary)
            archive = directory / "debug.apk"
            build_archive(
                archive,
                [_entry_record("a.txt", b"new")],
                orphans=[_entry_record("a.txt", b"old")],
            )

            result, report, _ = self.run_audit(directory, "--debug-apk", archive)

            self.assertEqual(result.returncode, 0, result.stderr)
            self.assertEqual(report["overall_status"], "warning")
            self.assertEqual(report["artifacts"][0]["policy"]["status"], "warning")

    def test_groups_payload_and_lists_methods_largest_entries_and_symbols(self):
        # Catches a wrong category branch or omission of symbol/method/entry evidence.
        with tempfile.TemporaryDirectory() as temporary:
            directory = Path(temporary)
            archive = directory / "groups.aab"
            entries = [
                _entry_record("base/lib/arm64-v8a/libx.so", b"nn"),
                _entry_record("base/dex/classes.dex", b"ddd"),
                _entry_record("base/assets/bin/Data/game.dat", b"uuuu"),
                _entry_record("base/assets/aa/Android/catalog.bundle", b"aaaaa"),
                _entry_record("base/res/drawable/icon.png", b"rrrrrr"),
                _entry_record("META-INF/MANIFEST.MF", b"mmmmmmm"),
                _entry_record("base/root/misc.txt", b"oooooooo"),
                _entry_record("base/root/game.pdb", b"123456789", method=8),
            ]
            build_archive(archive, entries)

            result, report, _ = self.run_audit(directory, "--release-aab", archive)

            self.assertEqual(result.returncode, 0, result.stderr)
            artifact = report["artifacts"][0]
            self.assertEqual(
                artifact["groups"],
                [
                    {"compressed_bytes": 5, "entry_count": 1, "name": "addressables", "uncompressed_bytes": 5},
                    {"compressed_bytes": 6, "entry_count": 1, "name": "android_resources", "uncompressed_bytes": 6},
                    {"compressed_bytes": 3, "entry_count": 1, "name": "dex", "uncompressed_bytes": 3},
                    {"compressed_bytes": 7, "entry_count": 1, "name": "metadata", "uncompressed_bytes": 7},
                    {"compressed_bytes": 2, "entry_count": 1, "name": "native", "uncompressed_bytes": 2},
                    {"compressed_bytes": 19, "entry_count": 2, "name": "other", "uncompressed_bytes": 17},
                    {"compressed_bytes": 4, "entry_count": 1, "name": "unity_player_data", "uncompressed_bytes": 4},
                ],
            )
            self.assertEqual(
                artifact["compression_methods"],
                [
                    {"code": 0, "compressed_bytes": 35, "entry_count": 7, "name": "stored"},
                    {"code": 8, "compressed_bytes": 11, "entry_count": 1, "name": "deflated"},
                ],
            )
            self.assertEqual(artifact["packaged_symbol_debug_files"], ["base/root/game.pdb"])
            self.assertEqual(artifact["largest_entries"][0]["name"], "base/root/game.pdb")

    def test_output_is_byte_identical_for_equal_archives_at_different_paths(self):
        # Catches leaking absolute paths, timestamps, or traversal order into JSON.
        with tempfile.TemporaryDirectory() as temporary:
            directory = Path(temporary)
            first_dir = directory / "first"
            second_dir = directory / "second"
            first_dir.mkdir()
            second_dir.mkdir()
            first = first_dir / "one.apk"
            second = second_dir / "two.apk"
            archive_bytes = build_archive(first, [_entry_record("z.txt", b"z"), _entry_record("a.txt", b"a")])
            second.write_bytes(archive_bytes)

            first_result, _, first_output = self.run_audit(first_dir, "--debug-apk", first)
            second_result, _, second_output = self.run_audit(second_dir, "--debug-apk", second)

            self.assertEqual(first_result.returncode, 0, first_result.stderr)
            self.assertEqual(second_result.returncode, 0, second_result.stderr)
            self.assertEqual(first_output.read_bytes(), second_output.read_bytes())
            self.assertNotIn(str(directory).encode(), first_output.read_bytes())

    def test_accepts_android_local_zero_alignment_padding(self):
        # Catches rejecting Android zipalign's 1-3 trailing zero extra bytes.
        with tempfile.TemporaryDirectory() as temporary:
            directory = Path(temporary)
            archive = directory / "aligned.apk"
            build_archive(
                archive,
                [_entry_record("a.txt", b"abc", local_extra=b"\0\0\0")],
            )

            result, report, _ = self.run_audit(directory, "--release-apk", archive)

            self.assertEqual(result.returncode, 0, result.stderr)
            self.assertEqual(report["artifacts"][0]["physical"]["local_header_bytes"], 38)

    def test_release_pair_keeps_same_source_not_evaluated_at_guard_boundary(self):
        # Catches treating an inclusive container-delta pass as provenance.
        with tempfile.TemporaryDirectory() as temporary:
            directory = Path(temporary)
            apk = directory / "release.apk"
            aab = directory / "release.aab"
            build_archive(apk, [_entry_record("a", b"")])
            build_archive(aab, [_entry_record("a", b"x" * 1048576)])

            result, report, _ = self.run_audit(
                directory,
                "--release-apk",
                apk,
                "--release-aab",
                aab,
            )

            self.assertEqual(result.returncode, 0, result.stderr)
            self.assertEqual(report["release_pair"]["absolute_container_delta_bytes"], 1048576)
            self.assertEqual(
                report["release_pair"].get("container_delta_guard_status"),
                "pass",
            )
            self.assertEqual(
                report["release_pair"].get("sameSource"),
                "notEvaluated",
            )
            self.assertNotIn("status", report["release_pair"])
            self.assertFalse(report["release_pair"]["installed_size_claim"])

    def test_release_pair_rejects_container_delta_over_one_mibibyte(self):
        # Catches making the pair guard larger than the stated 1 MiB maximum.
        with tempfile.TemporaryDirectory() as temporary:
            directory = Path(temporary)
            apk = directory / "release.apk"
            aab = directory / "release.aab"
            build_archive(apk, [_entry_record("a", b"")])
            build_archive(aab, [_entry_record("a", b"x" * 1048577)])

            result, report, _ = self.run_audit(
                directory,
                "--release-apk",
                apk,
                "--release-aab",
                aab,
            )

            self.assertEqual(result.returncode, 1)
            self.assertEqual(
                report["release_pair"]["container_delta_guard_status"],
                "fail",
            )
            self.assertEqual(report["release_pair"]["sameSource"], "notEvaluated")

    def test_truncated_archive_fails_closed_without_report(self):
        # Catches accepting a partial EOCD or silently reporting partial bytes.
        with tempfile.TemporaryDirectory() as temporary:
            directory = Path(temporary)
            archive = directory / "truncated.apk"
            data = build_archive(archive, [_entry_record("a.txt", b"abc")])
            archive.write_bytes(data[:-5])

            result, report, _ = self.run_audit(directory, "--debug-apk", archive)

            self.assertEqual(result.returncode, 2)
            self.assertIsNone(report)
            self.assertEqual(result.stderr, "error: valid EOCD not found\n")

    def test_bytes_between_central_directory_and_eocd_fail_closed(self):
        # Catches attributing unexplained pre-EOCD bytes as valid EOCD/trailing bytes.
        with tempfile.TemporaryDirectory() as temporary:
            directory = Path(temporary)
            archive = directory / "central-gap.apk"
            build_archive(
                archive,
                [_entry_record("a.txt", b"abc")],
                before_eocd=b"JUNK",
            )

            result, report, _ = self.run_audit(directory, "--release-apk", archive)

            self.assertEqual(result.returncode, 2)
            self.assertIsNone(report)
            self.assertEqual(
                result.stderr,
                "error: bytes between central directory and EOCD are unsupported\n",
            )

    def test_zip64_sentinel_fails_closed_as_unsupported(self):
        # Catches silently truncating ZIP64 counts into classic ZIP fields.
        with tempfile.TemporaryDirectory() as temporary:
            directory = Path(temporary)
            archive = directory / "zip64.apk"
            build_archive(archive, [_entry_record("a.txt", b"abc")], zip64_eocd=True)

            result, report, _ = self.run_audit(directory, "--debug-apk", archive)

            self.assertEqual(result.returncode, 2)
            self.assertIsNone(report)
            self.assertEqual(result.stderr, "error: ZIP64 is unsupported\n")

    def test_truncated_data_descriptor_fails_closed(self):
        # Catches counting a partial descriptor as free bytes or canonical payload.
        with tempfile.TemporaryDirectory() as temporary:
            directory = Path(temporary)
            archive = directory / "descriptor.apk"
            build_archive(
                archive,
                [_entry_record("a.txt", b"abc", descriptor=True)],
                truncate_descriptor=True,
            )

            result, report, _ = self.run_audit(directory, "--debug-apk", archive)

            self.assertEqual(result.returncode, 2)
            self.assertIsNone(report)
            self.assertEqual(result.stderr, "error: malformed data descriptor\n")

    def test_malformed_unreferenced_local_record_fails_closed(self):
        # Catches treating unexplained bytes between canonical records as valid free records.
        with tempfile.TemporaryDirectory() as temporary:
            directory = Path(temporary)
            archive = directory / "free.apk"
            orphan = _entry_record("old.bin", b"obsolete")
            malformed = dict(orphan)
            malformed_local = bytearray(orphan["local"])
            struct.pack_into("<L", malformed_local, 18, 9999)
            malformed["local"] = bytes(malformed_local)
            build_archive(
                archive,
                [_entry_record("new.bin", b"current")],
                orphans=[malformed],
            )

            result, report, _ = self.run_audit(directory, "--debug-apk", archive)

            self.assertEqual(result.returncode, 2)
            self.assertIsNone(report)
            self.assertEqual(result.stderr, "error: malformed unreferenced local record\n")


if __name__ == "__main__":
    unittest.main()
