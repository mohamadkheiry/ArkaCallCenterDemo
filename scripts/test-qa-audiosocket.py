"""Protocol-level smoke test for the Q&A AudioSocket worker."""

import argparse
import math
import socket
import struct
import threading
import time
import wave


def frame(kind: int, payload: bytes = b"") -> bytes:
    return bytes([kind]) + struct.pack("!H", len(payload)) + payload


def uuid_payload(extension: int, caller: str) -> bytes:
    caller_digits = "".join(ch for ch in caller if ch.isdigit())
    guarded = ("1" + caller_digits).rjust(20, "0")
    extension_digits = str(extension).rjust(12, "0")
    return bytes.fromhex(guarded + extension_digits)


def read_exact(stream: socket.socket, length: int) -> bytes | None:
    chunks = bytearray()
    while len(chunks) < length:
        data = stream.recv(length - len(chunks))
        if not data:
            return None
        chunks.extend(data)
    return bytes(chunks)


def pcm16_rms(payload: bytes) -> int:
    samples = [sample[0] for sample in struct.iter_unpack("<h", payload)]
    if not samples:
        return 0
    return int(math.sqrt(sum(sample * sample for sample in samples) / len(samples)))


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--extension", type=int, required=True)
    parser.add_argument("--caller", default="09120000000")
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=19092)
    parser.add_argument("--expect-silence", action="store_true")
    args = parser.parse_args()

    with wave.open(args.input, "rb") as source:
        assert source.getnchannels() == 1, "input must be mono"
        assert source.getsampwidth() == 2, "input must be PCM16"
        assert source.getframerate() == 8000, "input must be 8kHz"
        pcm = source.readframes(source.getnframes())

    stream = socket.create_connection((args.host, args.port), timeout=5)
    stream.settimeout(1)
    stream.sendall(frame(0x01, uuid_payload(args.extension, args.caller)))

    input_done = threading.Event()
    response_done = threading.Event()
    response_audio = bytearray()
    metrics = {"frames": 0, "audible_frames": 0, "max_rms": 0}

    def receive() -> None:
        heard_response = False
        silent_after_response = 0
        try:
            while not response_done.is_set():
                try:
                    header = read_exact(stream, 3)
                except TimeoutError:
                    continue
                except OSError:
                    return
                if header is None:
                    return
                kind, length = header[0], struct.unpack("!H", header[1:])[0]
                payload = read_exact(stream, length)
                if payload is None:
                    return
                if kind != 0x10:
                    if kind in (0x00, 0xFF):
                        return
                    continue
                metrics["frames"] += 1
                if not input_done.is_set():
                    continue
                response_audio.extend(payload)
                rms = pcm16_rms(payload)
                metrics["max_rms"] = max(metrics["max_rms"], rms)
                if rms >= 80:
                    heard_response = True
                    metrics["audible_frames"] += 1
                    silent_after_response = 0
                elif heard_response:
                    silent_after_response += 1
                    if silent_after_response >= 75:
                        response_done.set()
                        return
        finally:
            response_done.set()

    reader = threading.Thread(target=receive, daemon=True)
    reader.start()

    # Leading/trailing silence models the 20 ms SLIN frames sent by Asterisk.
    silence_frame = b"\x00" * 320
    for _ in range(15):
        stream.sendall(frame(0x10, silence_frame))
        time.sleep(0.02)
    for offset in range(0, len(pcm), 320):
        chunk = pcm[offset:offset + 320]
        if len(chunk) < 320:
            chunk += b"\x00" * (320 - len(chunk))
        stream.sendall(frame(0x10, chunk))
        time.sleep(0.02)
    for _ in range(65):
        stream.sendall(frame(0x10, silence_frame))
        time.sleep(0.02)
    input_done.set()

    timeout = 4 if args.expect_silence else 45
    if not response_done.wait(timeout):
        response_done.set()
    try:
        stream.sendall(frame(0x00))
    except OSError:
        pass
    stream.close()
    reader.join(timeout=2)

    with wave.open(args.output, "wb") as output:
        output.setnchannels(1)
        output.setsampwidth(2)
        output.setframerate(8000)
        output.writeframes(bytes(response_audio))

    print(
        f"frames={metrics['frames']} audible_frames={metrics['audible_frames']} "
        f"max_rms={metrics['max_rms']} response_bytes={len(response_audio)}"
    )
    if args.expect_silence and metrics["audible_frames"] != 0:
        raise SystemExit("Unexpected audible response was received for silence")
    if not args.expect_silence and metrics["audible_frames"] == 0:
        raise SystemExit("No audible response was received")


if __name__ == "__main__":
    main()
