"""Run the original APK's ARM64 RF297L code; emulate Dart containers only.

Uses Unicorn, no Android or Bluetooth device. The independent Python reference
is checked against the ARM64 result, rather than being its own golden oracle.
"""
from pathlib import Path
import argparse
import hashlib
import json
import random
import struct
from unicorn import Uc, UC_ARCH_ARM64, UC_MODE_ARM, UC_HOOK_CODE
from unicorn import arm64_const as R
from elftools.elf.elffile import ELFFile

EXPECTED_SHA256 = '1160c2e8446f39a6ad28ae25156a512425c00bb2e2c2835b588dfd6e9897f074'
CODE = (0x313688, 0x314574)
RETURN = 0x600000


class OriginalArm64:
    def __init__(self, library):
        self.uc = Uc(UC_ARCH_ARM64, UC_MODE_ARM)
        self.uc.mem_map(0, 0x800000)
        self.uc.mem_map(0x1000000, 0x200000)
        self.uc.mem_map(0x2000000, 0x100000)
        with library.open('rb') as stream:
            elf = ELFFile(stream)
            for segment in elf.iter_segments():
                if segment['p_type'] == 'PT_LOAD':
                    self.uc.mem_write(segment['p_vaddr'], segment.data())
        self.objects = {}
        self.cursor = 0x1000000
        self.steps = 0
        self.stub_calls = {}
        self.uc.hook_add(UC_HOOK_CODE, self.hook)

    def reg(self, number, value=None):
        key = getattr(R, f'UC_ARM64_REG_X{number}')
        if value is not None:
            self.uc.reg_write(key, value)
        return self.uc.reg_read(key)

    def u32(self, address):
        return struct.unpack('<I', self.uc.mem_read(address, 4))[0]

    def u64(self, address):
        return struct.unpack('<Q', self.uc.mem_read(address, 8))[0]

    def put32(self, address, value):
        self.uc.mem_write(address, struct.pack('<I', value))

    def array(self, values, growable=False):
        values = list(values)
        ptr = self.cursor + 1
        self.cursor += (16 + len(values) * 4 + 15) & ~15
        self.objects[ptr] = ('fixed', len(values))
        self.put32(ptr + 11, len(values) << 1)
        for index, value in enumerate(values):
            self.put32(ptr + 15 + index * 4, value << 1)
        if growable:
            backing = ptr
            ptr = self.cursor + 1
            self.cursor += 32
            self.objects[ptr] = ('growable', len(values))
            self.put32(ptr + 11, len(values) << 1)
            self.put32(ptr + 15, backing)
        return ptr

    def backing(self, ptr):
        return self.u32(ptr + 15) if self.objects[ptr][0] == 'growable' else ptr

    def read(self, ptr):
        return [self.u32(self.backing(ptr) + 15 + index * 4) >> 1
                for index in range(self.objects[ptr][1])]

    def result(self, value, destination=None):
        self.reg(0, value)
        self.uc.reg_write(R.UC_ARM64_REG_PC, destination or self.reg(30))

    def hook(self, uc, address, size, data):
        self.steps += 1
        stack = self.reg(15)
        if address == 0x4a1ba4:  # Dart AllocateArrayStub: length is Smi in x2
            self.stub_calls['AllocateArray'] = self.stub_calls.get('AllocateArray', 0) + 1
            self.result(self.array([0] * (self.reg(2) >> 1)))
        elif address == 0x22f10c:  # _List.sublist(receiver=x1, start=x2)
            self.stub_calls['sublist'] = self.stub_calls.get('sublist', 0) + 1
            self.result(self.array(self.read(self.reg(1))[self.reg(2):], True))
        elif address == 0x1d8fb0:  # List.from(iterable=x2)
            self.stub_calls['List.from'] = self.stub_calls.get('List.from', 0) + 1
            self.result(self.array(self.read(self.reg(2)), True))
        elif address == 0x313e9c:  # dynamic List.length
            self.result(self.objects[self.u64(stack)][1] << 1, 0x313ea8)
        elif address == 0x313ee4:  # dynamic List[index]
            index, ptr = self.u64(stack) >> 1, self.u64(stack + 8)
            self.result(self.read(ptr)[index] << 1, 0x313ef0)
        elif address == 0x31412c:  # dynamic List[index] = value
            value, index, ptr = self.u64(stack), self.u64(stack + 8) >> 1, self.u64(stack + 16)
            self.put32(self.backing(ptr) + 15 + index * 4, value)
            self.result(0, 0x314138)
        elif not CODE[0] <= address < CODE[1]:
            raise RuntimeError(f'Unexpected external execution at {address:#x}')

    def run(self, function, second, third):
        for number in range(31):
            self.reg(number, 0)
        self.reg(1, 0x700001)  # unused RF297L receiver
        self.reg(2, second)
        self.reg(3, third)
        self.reg(15, 0x20f0000)  # Dart stack pointer
        self.reg(26, 0x700000)  # Thread; stack_limit at +0x38 is zero
        self.reg(27, 0x720000)  # Pool; only unused type arguments are read
        self.reg(28, 0)  # compressed-pointer heap base
        self.reg(30, RETURN)
        self.uc.emu_start(function, RETURN, count=1_000_000)
        if self.uc.reg_read(R.UC_ARM64_REG_PC) != RETURN:
            raise RuntimeError('Instruction limit reached')
        return self.reg(0)

    def crc(self, address, data):
        return self.run(0x3141e0, self.array(address, True), self.array(data, True))

    def whiten(self, data, seed):
        ptr = self.array(data, True)
        state = self.array([1] + [(seed >> bit) & 1 for bit in range(5, -1, -1)])
        self.run(0x313e44, ptr, state)
        return self.read(ptr)

    def payload(self, address, data):
        result = self.run(0x313688, self.array(address, True), self.array(data, True))
        return bytes(self.read(result))


def reverse(value, bits):
    return int(f'{value:0{bits}b}'[::-1], 2)


def reference_crc(address, data):
    crc = 0xffff
    for value in list(reversed(address)) + [reverse(value, 8) for value in data]:
        crc ^= value << 8
        for _ in range(8):
            crc = ((crc << 1) ^ (0x1021 if crc & 0x8000 else 0)) & 0xffff
    return reverse(crc, 16) ^ 0xffff


def reference_whiten(data, seed):
    state = [1] + [(seed >> bit) & 1 for bit in range(5, -1, -1)]
    result = []
    for value in data:
        encoded = 0
        for bit in range(8):
            last = state[6]
            encoded |= (((value >> bit) & 1) ^ last) << bit
            state = [last, state[0], state[1], state[2], state[3] ^ last, state[4], state[5]]
        result.append(encoded)
    return result


def reference_payload(address, data):
    arr = [0] * 15 + [reverse(value, 8) for value in [0x71, 0x0f, 0x55] + list(reversed(address))]
    crc = reference_crc(address, data)
    arr += list(data) + [crc & 255, crc >> 8]
    arr[18:] = reference_whiten(arr[18:], 63)
    return bytes(reference_whiten(arr, 37)[15:])


def main():
    parser = argparse.ArgumentParser(description='Verify My LEZEN v1.3.8 ARM64 radio encoding offline.')
    parser.add_argument('libapp', type=Path, help='Path to the original arm64-v8a/libapp.so')
    parser.add_argument('--output-dir', type=Path, default=Path(__file__).resolve().parent,
                        help='Write report.json and protocol-vectors.json here (default: script directory)')
    args = parser.parse_args()
    if not args.libapp.is_file():
        parser.error(f'Input file does not exist: {args.libapp}')
    digest = hashlib.sha256(args.libapp.read_bytes()).hexdigest()
    if digest != EXPECTED_SHA256:
        parser.error(f'Unsupported libapp.so SHA256: {digest}; expected {EXPECTED_SHA256}. '
                     'This verifier uses offsets for the specific My LEZEN v1.3.8 ARM64 binary.')
    emu = OriginalArm64(args.libapp)
    address = [0xcc] * 5
    rng = random.Random(297)
    random_tests = 100
    for test in range(random_tests):
        addr = [rng.randrange(256) for _ in range(5)]
        data = [rng.randrange(256) for _ in range(8)]
        seed = rng.randrange(64)
        if emu.crc(addr, data) != reference_crc(addr, data):
            raise RuntimeError(f'CRC mismatch in random case {test}')
        if emu.whiten(data, seed) != reference_whiten(data, seed):
            raise RuntimeError(f'Whitening mismatch in random case {test}')
        if emu.payload(addr, data) != reference_payload(addr, data):
            raise RuntimeError(f'Payload mismatch in random case {test}')
    vectors = []
    for identifier in ['0000', '1234', 'ABCD', 'FFFF']:
        for command in list(range(0xa0, 0xa6)) + [0xa7, 0xa9] + list(range(0xb0, 0xb5)):
            raw = [0xaa, 0x66, command] + [int(char, 16) for char in identifier]
            raw += [sum(raw) & 255]
            actual = emu.payload(address, raw)
            if actual != reference_payload(address, raw):
                raise RuntimeError(f'Payload mismatch for ID {identifier}, command {command:02X}')
            vectors.append({'id': identifier, 'command': f'{command:02X}',
                            'data': bytes(raw).hex().upper(), 'payload': actual.hex().upper(),
                            'crc': f'{reference_crc(address, raw):04X}'})
    output = {
        'source': 'My LEZEN v1.3.8 arm64 libapp.so',
        'sha256': digest,
        'method': 'Unicorn executes original ARM64 getWhiteningPayload, _checkCrc16 and _whiteningEncode; hooks only Dart list allocation/access, not protocol logic.',
        'randomCasesPerRoutine': random_tests,
        'originalInstructionsExecuted': emu.steps,
        'stubCalls': emu.stub_calls,
        'vectors': vectors,
    }
    args.output_dir.mkdir(parents=True, exist_ok=True)
    (args.output_dir / 'protocol-vectors.json').write_text(json.dumps(vectors, indent=2) + '\n', encoding='utf-8')
    (args.output_dir / 'report.json').write_text(json.dumps(output, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({key: value for key, value in output.items() if key != 'vectors'}, indent=2))
    print(json.dumps(vectors[:3], indent=2))


if __name__ == '__main__':
    main()
