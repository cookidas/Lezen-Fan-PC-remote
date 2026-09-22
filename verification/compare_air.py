"""Offline comparison only: canonical integer BLE whitening, no radio APIs."""
import json
from pathlib import Path

def whitening(data, channel):
    state = channel | 0x40
    result = bytearray(data)
    for index in range(len(result)):
        for bit in range(8):
            feedback = state & 1
            result[index] ^= feedback << bit
            state = (state >> 1) ^ (0x44 if feedback else 0)
    return bytes(result)

def slice_whitening(data, offset, channel=37):
    return whitening(bytes(offset) + data, channel)[offset:]

phone_payload = bytes.fromhex('6DB6435F6E7F37A14F7B5EBE5DF9266572F0')
screenshot_payload = bytes.fromhex('6DB6435F6E7F37A14F7B5EB551F1256336A9')
phone_rf = slice_whitening(phone_payload, 15)
corrected = slice_whitening(phone_rf, 12)
assert corrected.hex().upper() == '18875217F808CF420992F56EC3AA15BDC868'
assert slice_whitening(corrected, 12) == phone_rf
assert slice_whitening(phone_payload, 12) != phone_rf

# Build both real BLE PDU prefixes; address/header arbitrary because whiten state
# depends only on channel/bit position, never the previous input bits.
phone_prefix = bytes.fromhex('401F D632533A320C 020102 15FFF0FF')
pc_prefix = bytes.fromhex('421C D632533A320C 15FFF0FF')
padded_prefix = bytes.fromhex('421F D632533A320C 18FFF0FF FFF0FF')
assert len(phone_prefix) == len(padded_prefix) == 15
assert len(pc_prefix) == 12
phone_air = whitening(phone_prefix + phone_payload, 37)
pc_air = whitening(pc_prefix + corrected, 37)
padded_air = whitening(padded_prefix + phone_payload, 37)
assert phone_air[15:] == pc_air[12:] == padded_air[15:]
assert phone_air[12:] == padded_air[12:]

# Original ARM64 vectors independently check the padded transport layout for all
# known commands. These outputs are fixtures, not fresh claims of hardware tests.
vectors = json.loads((Path(__file__).parent / 'protocol-vectors.json').read_text())
windows_vectors = []
for vector in vectors:
    payload = bytes.fromhex(vector['payload'])
    padded = bytes.fromhex('FFF0FF') + payload
    raw_ad = bytes([len(padded) + 3, 0xFF, 0xF0, 0xFF]) + padded
    assert len(payload) == 18 and len(padded) == 21 and len(raw_ad) == 25
    assert raw_ad[0] == len(raw_ad) - 1
    original_air = whitening(phone_prefix + payload, 37)
    windows_air = whitening(bytes.fromhex('421F D632533A320C') + raw_ad, 37)
    assert windows_air[12:] == original_air[12:]
    windows_vectors.append({
        'id': vector['id'], 'command': vector['command'],
        'original_payload': vector['payload'], 'windows_payload': padded.hex().upper(),
        'windows_raw_ad': raw_ad.hex().upper(),
        'channel37_rf_tail': original_air[15:].hex().upper()
    })
assert len(windows_vectors) == 52
Path(__file__).with_name('windows-padded-vectors.json').write_text(json.dumps(windows_vectors, indent=2))

# Independent decode uses inverse transforms; prefix validates the position.
plain = bytearray(slice_whitening(screenshot_payload, 15))
plain[3:] = whitening(plain[3:], 63)
assert plain[:8] == bytes.fromhex('8EF0AA3333333333')
data = plain[8:16]
assert sum(data[:7]) & 255 == data[7]

result = {
 'screenshot': {'id': ''.join(f'{v:X}' for v in data[3:7]), 'command': f'{data[2]:02X}', 'decoded': data.hex().upper()},
 'phone_6C3C_power_on_host_payload': phone_payload.hex().upper(),
 'corrected_offset12_payload': corrected.hex().upper(),
 'padded_payload': ('FFF0FF' + phone_payload.hex()).upper(),
 'padded_raw_ad_expected': ('18FFF0FFFFF0FF' + phone_payload.hex()).upper(),
 'channel37_air_rf_tail_all_three_equal': phone_rf.hex().upper(),
 'wrong_original_payload_at_offset12_air': slice_whitening(phone_payload, 12).hex().upper(),
 'channel38_corrected_air_rf_tail': slice_whitening(corrected, 12, 38).hex().upper(),
 'channel39_corrected_air_rf_tail': slice_whitening(corrected, 12, 39).hex().upper(),
 'padded_original_vector_count': len(windows_vectors),
 'hardware_observation': 'User confirmed padded 6C3C PowerOn and PowerOff at 2000ms and PowerOn at 200ms. These are user observations, not automated assertions.',
 'unresolved': 'The reason the earlier offset12 transmission received no observed response is not established by this fixture.',
 'assertions': 'PASS: canonical integer LFSR validates offset12; offset15 with3-byte padding also matches; FFF0FF padding matches 3 extra preceding phone bytes for the captured phone PowerOn packet and all52 original ARM64 vectors.'
}
print(json.dumps(result, indent=2))
Path(__file__).with_suffix('.json').write_text(json.dumps(result, indent=2))

