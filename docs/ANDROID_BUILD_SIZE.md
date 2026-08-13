# Android archive size audit

`tools/build/android_archive_audit.py` measures the physical APK/AAB container
with Python's standard library only. It does not extract the archive or estimate
an installed size.

## Run the current audit

From the repository root:

```sh
python3 tools/build/android_archive_audit.py \
  --debug-apk Builds/Android/CalmSpace-Demo-debug.apk \
  --release-apk Builds/Android/CalmSpace-Release-verify.apk \
  --release-aab Builds/Android/CalmSpace-Tidy-Restore-release.aab \
  --output Logs/android-archive-audit.json
```

`Logs/` is already ignored. The JSON deliberately excludes timestamps,
durations, absolute paths, session identifiers, and other run-specific fields.
Objects use sorted keys; entry, group, symbol, and free-record lists use stable
ordinal ordering (largest entries use compressed bytes descending, then name
ordinally). Equal inputs therefore produce byte-identical output.

Run the fixture suite with:

```sh
python3 -m unittest -v tools.build.tests.test_android_archive_audit
```

## Physical accounting

For every artifact, the auditor requires this exact equality:

```text
canonical compressed payload
+ referenced local headers
+ data descriptors
+ unreferenced local records
+ APK signing block
+ central directory
+ EOCD, comment, and trailing bytes
= physical file size from stat/read
```

Central-directory records are the canonical entry set. Their local-header
offsets identify referenced records. Every other byte before the signing block
must parse exactly as an unreferenced classic local record; unexplained,
overlapping, or truncated bytes fail closed. APK signing-block framing and both
size words are validated separately. The classic central directory must end
exactly where EOCD begins; EOCD comments and trailing bytes remain accounted
after EOCD. ZIP64 sentinels and ZIP64 extra fields are rejected as unsupported
instead of being interpreted as 32-bit values.

Canonical compressed payload is grouped as:

- `native`: packaged `.so` files under `lib/`;
- `dex`: `classes.dex` and numbered DEX siblings;
- `unity_player_data`: Unity `assets/bin/Data/` content;
- `addressables`: content under `assets/aa/` or an Addressables path;
- `android_resources`: manifests, `res/`, `resources.arsc`, and `resources.pb`;
- `metadata`: `META-INF/` and `BUNDLE-METADATA/`;
- `other`: remaining canonical entries.

The report also includes compression methods, the 20 largest canonical entries,
and packaged `.pdb`, `.mdb`, `.debug`, `.sym`, `.symbols`, or symbol-directory
files.

## Policy

- A release APK or AAB fails when unreferenced local-record bytes are nonzero.
- A debug APK may contain them, but reports a warning.
- A supplied same-source release APK/AAB pair must reconcile independently and
  have an absolute physical container delta of at most 1 MiB (1,048,576 bytes).
- The APK/AAB delta is a container guard only. It is **not** an installed Play
  size claim; Play delivery performs device-specific splitting and processing.
- Malformed, truncated, multi-disk, ambiguous, or ZIP64 archives fail closed and
  do not produce a new report.

## Current checked artifacts

| Role | Physical bytes | Canonical payload | Free records | Signing block | SHA-256 |
| --- | ---: | ---: | ---: | ---: | --- |
| Debug APK | 56,652,376 | 41,534,611 | 15,038,173 | 4,096 | `a0b1722a45d96837d0f1477c3adeb7307d89718038c3cf523a1309834de475e3` |
| Release verification APK | 32,591,255 | 32,534,779 | 0 | 8,192 | `3ba41931708a2f22c27d304712bbc7cf425d01ec7d476d7300743215b53611a2` |
| Release AAB | 31,971,674 | 31,890,370 | 0 | 0 | `7ba336070561b0758fafbe3dbf429f09bfd60a8765643d025fb8068bd5fe3f6d` |

The current debug archive contains exactly **15,038,173 bytes** of debug-only
unreferenced local records. Its canonical payload is **8,999,832 bytes** larger
than the release verification APK, the current Development Player/code delta.
No packaged debug-symbol files were found in any of the three archives. The
release APK and AAB differ by **619,581 physical container bytes**, within the
1 MiB guard.

Unity's `BuildReport.summary.totalSize` describes player-build work output. It
is not the final APK/AAB physical archive size, so it must not replace this
post-build container reconciliation.
