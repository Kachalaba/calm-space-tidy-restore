#!/usr/bin/env python3
"""Deterministically reconcile the physical bytes of Android ZIP archives."""

import argparse
import hashlib
import json
import os
import re
import struct
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Dict, List, Optional, Sequence, Tuple


LOCAL_SIGNATURE = 0x04034B50
CENTRAL_SIGNATURE = 0x02014B50
EOCD_SIGNATURE = b"PK\x05\x06"
DATA_DESCRIPTOR_SIGNATURE = 0x08074B50
APK_SIGNING_MAGIC = b"APK Sig Block 42"
ZIP64_EXTRA_ID = 0x0001
PAIR_DELTA_LIMIT = 1024 * 1024


class AuditError(Exception):
    """Raised when an archive cannot be reconciled without ambiguity."""


@dataclass(frozen=True)
class CentralEntry:
    name: str
    name_bytes: bytes
    flags: int
    method: int
    crc: int
    compressed_size: int
    uncompressed_size: int
    local_offset: int


@dataclass(frozen=True)
class LocalSpan:
    start: int
    end: int
    header_bytes: int
    descriptor_bytes: int
    entry: CentralEntry


def _unpack(fmt: str, data: bytes, offset: int, error: str) -> Tuple[int, ...]:
    size = struct.calcsize(fmt)
    if offset < 0 or offset + size > len(data):
        raise AuditError(error)
    return struct.unpack_from(fmt, data, offset)


def _decode_name(raw: bytes, flags: int) -> str:
    encoding = "utf-8" if flags & 0x0800 else "cp437"
    try:
        return raw.decode(encoding)
    except UnicodeDecodeError as exc:
        raise AuditError("invalid ZIP entry name") from exc


def _validate_extra(extra: bytes, *, allow_zero_alignment_padding: bool = False) -> None:
    position = 0
    while position < len(extra):
        remainder = extra[position:]
        if (
            allow_zero_alignment_padding
            and len(remainder) <= 3
            and remainder == b"\0" * len(remainder)
        ):
            return
        if position + 4 > len(extra):
            raise AuditError("malformed ZIP extra field")
        field_id, size = struct.unpack_from("<HH", extra, position)
        position += 4
        if position + size > len(extra):
            raise AuditError("malformed ZIP extra field")
        if field_id == ZIP64_EXTRA_ID:
            raise AuditError("ZIP64 is unsupported")
        position += size


def _find_eocd(data: bytes) -> Tuple[int, Tuple[int, ...]]:
    lower_bound = max(0, len(data) - (65535 + 22))
    candidates = []
    position = data.find(EOCD_SIGNATURE, lower_bound)
    while position != -1:
        if position + 22 <= len(data):
            fields = struct.unpack_from("<4H2LH", data, position + 4)
            comment_size = fields[6]
            central_size = fields[4]
            central_offset = fields[5]
            if (
                position + 22 + comment_size <= len(data)
                and central_offset + central_size <= position
            ):
                candidates.append((position, fields))
        position = data.find(EOCD_SIGNATURE, position + 1)

    if not candidates:
        raise AuditError("valid EOCD not found")
    if len(candidates) != 1:
        raise AuditError("ambiguous EOCD records")
    return candidates[0]


def _parse_central_directory(
    data: bytes,
    eocd_offset: int,
    fields: Tuple[int, ...],
) -> Tuple[List[CentralEntry], int, int]:
    disk, central_disk, disk_count, total_count, central_size, central_offset, _ = fields
    if (
        disk == 0xFFFF
        or central_disk == 0xFFFF
        or disk_count == 0xFFFF
        or total_count == 0xFFFF
        or central_size == 0xFFFFFFFF
        or central_offset == 0xFFFFFFFF
        or data[max(0, eocd_offset - 20):eocd_offset].startswith(b"PK\x06\x07")
    ):
        raise AuditError("ZIP64 is unsupported")
    if disk != 0 or central_disk != 0 or disk_count != total_count:
        raise AuditError("multi-disk ZIP archives are unsupported")
    central_end = central_offset + central_size
    if central_end > eocd_offset or central_end > len(data):
        raise AuditError("central directory is out of bounds")
    if central_end != eocd_offset:
        raise AuditError("bytes between central directory and EOCD are unsupported")

    entries = []
    position = central_offset
    for _ in range(total_count):
        values = _unpack(
            "<I6H3L5H2L",
            data,
            position,
            "truncated central directory",
        )
        (
            signature,
            _made_by,
            _needed,
            flags,
            method,
            _time,
            _date,
            crc,
            compressed_size,
            uncompressed_size,
            name_size,
            extra_size,
            comment_size,
            disk_start,
            _internal_attributes,
            _external_attributes,
            local_offset,
        ) = values
        if signature != CENTRAL_SIGNATURE:
            raise AuditError("invalid central directory signature")
        if (
            compressed_size == 0xFFFFFFFF
            or uncompressed_size == 0xFFFFFFFF
            or local_offset == 0xFFFFFFFF
            or disk_start == 0xFFFF
        ):
            raise AuditError("ZIP64 is unsupported")
        if disk_start != 0:
            raise AuditError("multi-disk ZIP archives are unsupported")

        variable_start = position + 46
        variable_end = variable_start + name_size + extra_size + comment_size
        if variable_end > central_end:
            raise AuditError("truncated central directory entry")
        name_bytes = data[variable_start:variable_start + name_size]
        extra = data[
            variable_start + name_size:variable_start + name_size + extra_size
        ]
        _validate_extra(extra)
        entries.append(
            CentralEntry(
                name=_decode_name(name_bytes, flags),
                name_bytes=name_bytes,
                flags=flags,
                method=method,
                crc=crc,
                compressed_size=compressed_size,
                uncompressed_size=uncompressed_size,
                local_offset=local_offset,
            )
        )
        position = variable_end

    if position != central_end:
        raise AuditError("central directory size does not match its entries")
    if len({entry.local_offset for entry in entries}) != len(entries):
        raise AuditError("duplicate local entry offsets are ambiguous")
    return entries, central_offset, central_size


def _descriptor_size(data: bytes, offset: int, entry: CentralEntry) -> int:
    if offset + 12 > len(data):
        raise AuditError("malformed data descriptor")
    first = struct.unpack_from("<L", data, offset)[0]
    if first == DATA_DESCRIPTOR_SIGNATURE:
        if entry.crc == DATA_DESCRIPTOR_SIGNATURE:
            raise AuditError("ambiguous data descriptor")
        if offset + 16 > len(data):
            raise AuditError("malformed data descriptor")
        _, crc, compressed_size, uncompressed_size = struct.unpack_from(
            "<4L", data, offset
        )
        size = 16
    else:
        crc, compressed_size, uncompressed_size = struct.unpack_from(
            "<3L", data, offset
        )
        size = 12
    if (
        crc != entry.crc
        or compressed_size != entry.compressed_size
        or uncompressed_size != entry.uncompressed_size
    ):
        raise AuditError("malformed data descriptor")
    return size


def _parse_local_span(data: bytes, entry: CentralEntry) -> LocalSpan:
    values = _unpack(
        "<I5H3L2H",
        data,
        entry.local_offset,
        "truncated local header",
    )
    (
        signature,
        _needed,
        flags,
        method,
        _time,
        _date,
        crc,
        compressed_size,
        uncompressed_size,
        name_size,
        extra_size,
    ) = values
    if signature != LOCAL_SIGNATURE:
        raise AuditError("invalid local header signature")
    if flags != entry.flags or method != entry.method:
        raise AuditError("local and central entry metadata disagree")

    variable_start = entry.local_offset + 30
    header_end = variable_start + name_size + extra_size
    if header_end > len(data):
        raise AuditError("truncated local header")
    name_bytes = data[variable_start:variable_start + name_size]
    extra = data[variable_start + name_size:header_end]
    _validate_extra(extra, allow_zero_alignment_padding=True)
    if name_bytes != entry.name_bytes:
        raise AuditError("local and central entry names disagree")

    if flags & 0x0008:
        if crc not in (0, entry.crc) or compressed_size not in (0, entry.compressed_size):
            raise AuditError("local and central entry sizes disagree")
        if uncompressed_size not in (0, entry.uncompressed_size):
            raise AuditError("local and central entry sizes disagree")
    elif (
        crc != entry.crc
        or compressed_size != entry.compressed_size
        or uncompressed_size != entry.uncompressed_size
    ):
        raise AuditError("local and central entry sizes disagree")

    payload_end = header_end + entry.compressed_size
    if payload_end > len(data):
        raise AuditError("truncated entry payload")
    descriptor_size = (
        _descriptor_size(data, payload_end, entry) if flags & 0x0008 else 0
    )
    return LocalSpan(
        start=entry.local_offset,
        end=payload_end + descriptor_size,
        header_bytes=header_end - entry.local_offset,
        descriptor_bytes=descriptor_size,
        entry=entry,
    )


def _apk_signing_block_start(data: bytes, central_offset: int, archive_type: str) -> int:
    if archive_type != "apk" or central_offset < 24:
        return central_offset
    if data[central_offset - 16:central_offset] != APK_SIGNING_MAGIC:
        return central_offset
    size = struct.unpack_from("<Q", data, central_offset - 24)[0]
    if size < 24 or size > central_offset - 8:
        raise AuditError("malformed APK signing block")
    start = central_offset - size - 8
    if struct.unpack_from("<Q", data, start)[0] != size:
        raise AuditError("malformed APK signing block")

    pairs_end = central_offset - 24
    position = start + 8
    while position < pairs_end:
        if position + 8 > pairs_end:
            raise AuditError("malformed APK signing block")
        pair_size = struct.unpack_from("<Q", data, position)[0]
        if pair_size < 4 or position + 8 + pair_size > pairs_end:
            raise AuditError("malformed APK signing block")
        position += 8 + pair_size
    if position != pairs_end:
        raise AuditError("malformed APK signing block")
    return start


def _parse_free_region(data: bytes, start: int, end: int) -> List[Dict[str, object]]:
    records = []
    position = start
    while position < end:
        try:
            values = _unpack(
                "<I5H3L2H",
                data,
                position,
                "malformed unreferenced local record",
            )
            (
                signature,
                _needed,
                flags,
                _method,
                _time,
                _date,
                _crc,
                compressed_size,
                uncompressed_size,
                name_size,
                extra_size,
            ) = values
            if signature != LOCAL_SIGNATURE or flags & 0x0008:
                raise AuditError("malformed unreferenced local record")
            if compressed_size == 0xFFFFFFFF or uncompressed_size == 0xFFFFFFFF:
                raise AuditError("ZIP64 is unsupported")
            header_end = position + 30 + name_size + extra_size
            record_end = header_end + compressed_size
            if header_end > end or record_end > end:
                raise AuditError("malformed unreferenced local record")
            name_bytes = data[position + 30:position + 30 + name_size]
            extra = data[position + 30 + name_size:header_end]
            _validate_extra(extra, allow_zero_alignment_padding=True)
            name = _decode_name(name_bytes, flags)
        except AuditError as exc:
            if str(exc) == "ZIP64 is unsupported":
                raise
            raise AuditError("malformed unreferenced local record") from exc
        records.append({"name": name, "physical_bytes": record_end - position})
        position = record_end
    return records


def _group_for(name: str) -> str:
    normalized = name.replace("\\", "/").lower()
    without_base = normalized[5:] if normalized.startswith("base/") else normalized
    if without_base.startswith("lib/") and without_base.endswith(".so"):
        return "native"
    if re.search(r"(^|/)classes\d*\.dex$", without_base):
        return "dex"
    if without_base.startswith("assets/bin/data/"):
        return "unity_player_data"
    if without_base.startswith("assets/aa/") or "/addressables/" in without_base:
        return "addressables"
    if (
        without_base in ("androidmanifest.xml", "resources.arsc", "resources.pb")
        or without_base.startswith("res/")
        or without_base.startswith("manifest/")
    ):
        return "android_resources"
    if without_base.startswith("meta-inf/") or normalized.startswith("bundle-metadata/"):
        return "metadata"
    return "other"


def _is_symbol_or_debug_file(name: str) -> bool:
    normalized = name.replace("\\", "/").lower()
    basename = normalized.rsplit("/", 1)[-1]
    return (
        basename.endswith((".pdb", ".mdb", ".debug", ".sym", ".symbols"))
        or "debugsymbols" in normalized
        or "symbols/" in normalized
    )


def _method_name(code: int) -> str:
    return {0: "stored", 8: "deflated"}.get(code, "method_{}".format(code))


def _entry_evidence(entries: Sequence[CentralEntry]) -> Tuple[List[dict], List[dict], List[dict], List[str]]:
    groups: Dict[str, Dict[str, int]] = {}
    methods: Dict[int, Dict[str, int]] = {}
    largest = []
    symbols = []
    for entry in entries:
        group = _group_for(entry.name)
        group_totals = groups.setdefault(
            group,
            {"compressed_bytes": 0, "entry_count": 0, "uncompressed_bytes": 0},
        )
        group_totals["compressed_bytes"] += entry.compressed_size
        group_totals["entry_count"] += 1
        group_totals["uncompressed_bytes"] += entry.uncompressed_size

        method_totals = methods.setdefault(
            entry.method,
            {"compressed_bytes": 0, "entry_count": 0},
        )
        method_totals["compressed_bytes"] += entry.compressed_size
        method_totals["entry_count"] += 1
        largest.append(
            {
                "compressed_bytes": entry.compressed_size,
                "group": group,
                "method": _method_name(entry.method),
                "name": entry.name,
                "uncompressed_bytes": entry.uncompressed_size,
            }
        )
        if _is_symbol_or_debug_file(entry.name):
            symbols.append(entry.name)

    group_list = [dict(totals, name=name) for name, totals in groups.items()]
    group_list.sort(key=lambda item: item["name"])
    method_list = [
        dict(totals, code=code, name=_method_name(code))
        for code, totals in methods.items()
    ]
    method_list.sort(key=lambda item: item["code"])
    largest.sort(key=lambda item: (-item["compressed_bytes"], item["name"]))
    return group_list, method_list, largest[:20], sorted(symbols)


def audit_archive(path: Path, role: str) -> dict:
    try:
        data = path.read_bytes()
        stat_size = path.stat().st_size
    except OSError as exc:
        raise AuditError("unable to read artifact") from exc
    if stat_size != len(data):
        raise AuditError("artifact size changed while reading")
    archive_type = "aab" if role.endswith("aab") else "apk"
    eocd_offset, eocd_fields = _find_eocd(data)
    entries, central_offset, central_size = _parse_central_directory(
        data, eocd_offset, eocd_fields
    )
    signing_start = _apk_signing_block_start(data, central_offset, archive_type)

    spans = sorted((_parse_local_span(data, entry) for entry in entries), key=lambda span: span.start)
    free_records = []
    position = 0
    for span in spans:
        if span.start < position:
            raise AuditError("overlapping local records")
        if span.end > signing_start:
            raise AuditError("local record overlaps signing or central directory")
        if span.start > position:
            free_records.extend(_parse_free_region(data, position, span.start))
        position = span.end
    if position < signing_start:
        free_records.extend(_parse_free_region(data, position, signing_start))
    elif position > signing_start:
        raise AuditError("local record overlaps signing or central directory")

    payload_bytes = sum(entry.compressed_size for entry in entries)
    local_header_bytes = sum(span.header_bytes for span in spans)
    descriptor_bytes = sum(span.descriptor_bytes for span in spans)
    free_bytes = sum(record["physical_bytes"] for record in free_records)
    signing_bytes = central_offset - signing_start
    eocd_and_trailing = len(data) - central_offset - central_size
    reconciled = (
        payload_bytes
        + local_header_bytes
        + descriptor_bytes
        + free_bytes
        + signing_bytes
        + central_size
        + eocd_and_trailing
    )
    if reconciled != stat_size:
        raise AuditError("physical byte reconciliation failed")

    groups, methods, largest, symbols = _entry_evidence(entries)
    if role.startswith("release") and free_bytes:
        policy = {
            "message": "release artifacts must contain zero unreferenced local-record bytes",
            "status": "fail",
        }
    elif role == "debug_apk" and free_bytes:
        policy = {
            "message": "debug artifact contains unreferenced local-record bytes",
            "status": "warning",
        }
    else:
        policy = {"message": "archive physical layout satisfies policy", "status": "pass"}

    return {
        "archive_type": archive_type,
        "compression_methods": methods,
        "entry_count": len(entries),
        "file_size_bytes": stat_size,
        "free_records": sorted(
            free_records, key=lambda record: (record["name"], record["physical_bytes"])
        ),
        "groups": groups,
        "largest_entries": largest,
        "packaged_symbol_debug_files": symbols,
        "physical": {
            "apk_signing_block_bytes": signing_bytes,
            "canonical_payload_bytes": payload_bytes,
            "central_directory_bytes": central_size,
            "data_descriptor_bytes": descriptor_bytes,
            "eocd_and_trailing_bytes": eocd_and_trailing,
            "free_record_bytes": free_bytes,
            "local_header_bytes": local_header_bytes,
            "reconciled_bytes": reconciled,
        },
        "policy": policy,
        "role": role,
        "sha256": hashlib.sha256(data).hexdigest(),
    }


def build_report(role_paths: Sequence[Tuple[str, Path]]) -> dict:
    artifacts = [audit_archive(path, role) for role, path in role_paths]
    artifacts.sort(key=lambda artifact: artifact["role"])
    statuses = [artifact["policy"]["status"] for artifact in artifacts]
    report = {
        "artifacts": artifacts,
        "overall_status": (
            "fail" if "fail" in statuses else "warning" if "warning" in statuses else "pass"
        ),
        "schema_version": 1,
    }

    by_role = {artifact["role"]: artifact for artifact in artifacts}
    if "release_apk" in by_role and "release_aab" in by_role:
        delta = abs(
            by_role["release_apk"]["file_size_bytes"]
            - by_role["release_aab"]["file_size_bytes"]
        )
        pair_status = "pass" if delta <= PAIR_DELTA_LIMIT else "fail"
        report["release_pair"] = {
            "absolute_container_delta_bytes": delta,
            "container_delta_guard_status": pair_status,
            "guard_max_bytes": PAIR_DELTA_LIMIT,
            "installed_size_claim": False,
            "sameSource": "notEvaluated",
        }
        if pair_status == "fail":
            report["overall_status"] = "fail"
    return report


def _arguments(argv: Optional[Sequence[str]]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Audit physical APK/AAB ZIP bytes deterministically."
    )
    parser.add_argument("--debug-apk", type=Path)
    parser.add_argument("--release-apk", type=Path)
    parser.add_argument("--release-aab", type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args(argv)
    if not (args.debug_apk or args.release_apk or args.release_aab):
        parser.error("at least one artifact is required")
    return args


def main(argv: Optional[Sequence[str]] = None) -> int:
    args = _arguments(argv)
    role_paths = [
        (role, path)
        for role, path in (
            ("debug_apk", args.debug_apk),
            ("release_apk", args.release_apk),
            ("release_aab", args.release_aab),
        )
        if path is not None
    ]
    try:
        report = build_report(role_paths)
    except AuditError as exc:
        print("error: {}".format(exc), file=sys.stderr)
        return 2

    args.output.parent.mkdir(parents=True, exist_ok=True)
    encoded = (json.dumps(report, indent=2, sort_keys=True) + "\n").encode("utf-8")
    temporary = args.output.with_name(args.output.name + ".tmp")
    temporary.write_bytes(encoded)
    os.replace(str(temporary), str(args.output))
    return 1 if report["overall_status"] == "fail" else 0


if __name__ == "__main__":
    sys.exit(main())
