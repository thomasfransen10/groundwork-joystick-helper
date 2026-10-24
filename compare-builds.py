import hashlib
import json
import pathlib
import struct
import sys

import pefile

if len(sys.argv) != 3:
    raise SystemExit('Usage: python compare-builds.py ORIGINAL.exe CI-BUILD.exe')
local_path = pathlib.Path(sys.argv[1])
ci_path = pathlib.Path(sys.argv[2])

def read_image(path):
    data = bytearray(path.read_bytes())
    pe = pefile.PE(data=bytes(data))
    clr = pe.get_offset_from_rva(pe.OPTIONAL_HEADER.DATA_DIRECTORY[14].VirtualAddress)
    metadata_rva = struct.unpack_from('<I', data, clr + 8)[0]
    metadata = pe.get_offset_from_rva(metadata_rva)
    if data[metadata:metadata + 4] != b'BSJB':
        raise ValueError('Not a CLI metadata root')
    version_size = struct.unpack_from('<I', data, metadata + 12)[0]
    pos = (metadata + 16 + version_size + 3) & ~3
    stream_count = struct.unpack_from('<H', data, pos + 2)[0]
    pos += 4
    ranges = [(pe.FILE_HEADER.get_field_absolute_offset('TimeDateStamp'), 4, 'PE compilation timestamp')]
    for _ in range(stream_count):
        offset, size = struct.unpack_from('<II', data, pos)
        pos += 8
        end = data.index(0, pos)
        name = bytes(data[pos:end]).decode('ascii')
        pos = (end + 4) & ~3
        if name == '#GUID':
            if size != 16:
                raise ValueError('Unexpected GUID heap size; investigate instead of normalizing')
            ranges.append((metadata + offset, size, 'single module GUID / MVID heap'))
    if len(ranges) != 2:
        raise ValueError('Expected a single module GUID heap')
    normalized = bytearray(data)
    for start, count, _ in ranges:
        normalized[start:start + count] = bytes(count)
    return data, normalized, ranges

local, local_normalized, local_ranges = read_image(local_path)
ci, ci_normalized, ci_ranges = read_image(ci_path)
differences = [i for i, (a, b) in enumerate(zip(local, ci)) if a != b]
same_normalized = local_normalized == ci_normalized and local_ranges == ci_ranges
report = {
    'local_sha256': hashlib.sha256(local).hexdigest(),
    'ci_sha256': hashlib.sha256(ci).hexdigest(),
    'local_size': len(local),
    'ci_size': len(ci),
    'differing_byte_count': len(differences),
    'normalized_fields': [{'offset': start, 'length': size, 'meaning': meaning} for start, size, meaning in local_ranges],
    'all_other_bytes_identical': same_normalized,
    'normalized_sha256_local': hashlib.sha256(local_normalized).hexdigest(),
    'normalized_sha256_ci': hashlib.sha256(ci_normalized).hexdigest(),
    'limits': 'This comparison does not establish software safety or resolve antivirus detections. No executable was run.'
}
print(json.dumps(report, indent=2))

