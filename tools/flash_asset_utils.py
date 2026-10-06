"""Small, static SWF inspection helpers; never execute ActionScript."""
import struct
import zlib


def decode_swf(data):
    if data[:3] not in (b"FWS", b"CWS"):
        data = zlib.decompress(data[7:])
    if data[:3] == b"CWS":
        data = b"FWS" + data[3:8] + zlib.decompress(data[8:])
    if data[:3] != b"FWS":
        raise ValueError("Unsupported SWF signature")
    if len(data) != struct.unpack_from("<I", data, 4)[0]:
        raise ValueError("SWF length mismatch")
    return data


def swf_tags(data):
    """Iterate top-level tags, skipping the RECT, frame rate and frame count."""
    offset = 8 + (5 + 4 * (data[8] >> 3) + 7) // 8 + 4
    while offset < len(data):
        header = struct.unpack_from("<H", data, offset)[0]
        offset += 2
        code, size = header >> 6, header & 63
        if size == 63:
            size = struct.unpack_from("<I", data, offset)[0]
            offset += 4
        if offset + size > len(data):
            raise ValueError("Truncated SWF tag")
        yield code, data[offset:offset + size]
        offset += size


def symbols(data):
    result = {}
    for code, payload in swf_tags(data):
        if code not in (56, 76):
            continue
        offset = 2
        for _ in range(struct.unpack_from("<H", payload)[0]):
            number = struct.unpack_from("<H", payload, offset)[0]
            offset += 2
            end = payload.index(0, offset)
            result[payload[offset:end].decode("utf-8")] = number
            offset = end + 1
    return result


def embedded_data(data):
    return {struct.unpack_from("<H", payload)[0]: payload[6:]
            for code, payload in swf_tags(data) if code == 87}
