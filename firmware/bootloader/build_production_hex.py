#!/usr/bin/env python3
"""
生産外注用に euboot bootloader と mlogger_main アプリ + fuses を 1 本の
Intel HEX に merge する。生成物 1 ファイルを IPE/avrdude に渡すだけで
bootloader + app + fuse 設定が完了する。

usage:
    python build_production_hex.py [-o OUTPUT]
"""

import argparse
import sys
from pathlib import Path

THIS_DIR = Path(__file__).resolve().parent
DEFAULT_EUBOOT = THIS_DIR / "hex" / "euboot_LD0_SF2.hex"
DEFAULT_APP    = THIS_DIR.parent / "mlogger_main.X" / "dist" / "default" / "production" / "mlogger_main.X.production.hex"
DEFAULT_OUTPUT = THIS_DIR / "hex" / "mlogger_v4_combined.hex"


def parse_ihex(path: Path) -> dict[int, int]:
    mem: dict[int, int] = {}
    upper = 0
    for raw in path.read_text().splitlines():
        line = raw.strip()
        if not line.startswith(":"):
            continue
        count = int(line[1:3], 16)
        addr  = int(line[3:7], 16)
        rtype = int(line[7:9], 16)
        data  = bytes.fromhex(line[9 : 9 + count * 2])
        if rtype == 0:
            base = (upper << 16) | addr
            for i, b in enumerate(data):
                mem[base + i] = b
        elif rtype == 4:
            upper = int.from_bytes(data, "big")
        elif rtype == 1:
            break
    return mem


def _record(addr_lo: int, rtype: int, data: bytes) -> str:
    body = bytes([len(data), (addr_lo >> 8) & 0xFF, addr_lo & 0xFF, rtype]) + data
    return ":" + body.hex().upper() + f"{(-sum(body)) & 0xFF:02X}"


def write_ihex(mem: dict[int, int], path: Path, record_size: int = 32) -> None:
    if not mem:
        raise ValueError("empty memory map")
    addrs = sorted(mem)
    runs: list[tuple[int, bytearray]] = []
    start = addrs[0]
    buf   = bytearray([mem[start]])
    for a in addrs[1:]:
        if a == start + len(buf):
            buf.append(mem[a])
        else:
            runs.append((start, buf))
            start, buf = a, bytearray([mem[a]])
    runs.append((start, buf))

    lines: list[str] = []
    cur_upper: int | None = None
    for run_start, run_data in runs:
        offset = 0
        while offset < len(run_data):
            addr  = run_start + offset
            upper = addr >> 16
            if upper != cur_upper:
                lines.append(_record(0, 4, upper.to_bytes(2, "big")))
                cur_upper = upper
            remaining_in_seg = 0x10000 - (addr & 0xFFFF)
            chunk_size = min(record_size, len(run_data) - offset, remaining_in_seg)
            chunk = bytes(run_data[offset : offset + chunk_size])
            lines.append(_record(addr & 0xFFFF, 0, chunk))
            offset += chunk_size
    lines.append(":00000001FF")
    path.write_text("\n".join(lines) + "\n")


def summarize(name: str, mem: dict[int, int]) -> None:
    if not mem:
        print(f"  {name}: <empty>")
        return
    addrs = sorted(mem)
    regions: list[tuple[int, int]] = []
    s = addrs[0]; prev = s
    for a in addrs[1:]:
        if a != prev + 1:
            regions.append((s, prev)); s = a
        prev = a
    regions.append((s, prev))
    region_strs = [f"0x{rs:06X}-0x{re:06X} ({re - rs + 1}B)" for rs, re in regions]
    print(f"  {name}: {len(mem)}B  [{', '.join(region_strs)}]")


def main() -> int:
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("--euboot", type=Path, default=DEFAULT_EUBOOT, help=f"bootloader hex (default: {DEFAULT_EUBOOT.relative_to(THIS_DIR.parent.parent)})")
    p.add_argument("--app",    type=Path, default=DEFAULT_APP,    help=f"mlogger_main hex (default: dist の production hex)")
    p.add_argument("-o", "--output", type=Path, default=DEFAULT_OUTPUT, help=f"output hex (default: {DEFAULT_OUTPUT.relative_to(THIS_DIR.parent.parent)})")
    args = p.parse_args()

    for label, path in (("euboot", args.euboot), ("app", args.app)):
        if not path.exists():
            print(f"ERROR: {label} hex not found: {path}", file=sys.stderr)
            return 1

    print("inputs:")
    eb = parse_ihex(args.euboot); summarize(f"euboot ({args.euboot.name})", eb)
    mm = parse_ihex(args.app);    summarize(f"app    ({args.app.name})",    mm)

    overlap = set(eb) & set(mm)
    if overlap:
        print(f"ERROR: {len(overlap)} bytes overlap between euboot and app "
              f"(first at 0x{min(overlap):06X}). app の linker offset 設定 "
              f"(__TEXT_REGION_ORIGIN__=0xA00) を確認", file=sys.stderr)
        return 1

    merged = {**eb, **mm}
    args.output.parent.mkdir(parents=True, exist_ok=True)
    write_ihex(merged, args.output)
    print(f"\noutput: {args.output}")
    summarize("merged", merged)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
