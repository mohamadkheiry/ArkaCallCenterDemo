#!/opt/miniconda/envs/piper/bin/python
# -*- coding: utf-8 -*-
"""سرویسِ HTTP روی ایزابل: {phone, text, secret} می‌گیرد، با piper (صدای گنجی)
   متن را می‌سازد و به موبایل زنگ می‌زند و پخش می‌کند. برای OTP-با-تماس و
   اطلاع‌رسانیِ اتمامِ تسک استفاده می‌شود."""
import json, os, subprocess, hashlib, time
from http.server import BaseHTTPRequestHandler, HTTPServer

os.environ["PATH"] = "/usr/sbin:/usr/bin:/bin:/sbin:" + os.environ.get("PATH", "")
SECRET   = os.environ.get("VOICE_SECRET", "changeme")
PIPER_PY = "/opt/miniconda/envs/piper/bin/python"
VOICE    = "/opt/arka-jira/voices/fa_IR-ganji-medium.onnx"
AST_SND  = "/var/lib/asterisk/sounds/arka"
LOG      = "/opt/arka-jira/voice_service.log"
os.makedirs(AST_SND, exist_ok=True)

def log(m):
    line = time.strftime("%Y-%m-%d %H:%M:%S ") + m
    try:
        with open(LOG, "a", encoding="utf-8") as f: f.write(line + "\n")
    except Exception: pass

def comma_words(t):
    return " , ".join(w for w in str(t).replace("\n", " ").split(" ") if w.strip())

def tts(text, name, raw=False):
    rawwav = "/tmp/%s_raw.wav" % name
    final = os.path.join(AST_SND, name + ".wav")
    piped = text if raw else comma_words(text)   # raw=True: متن دقیقاً همان‌طور به piper می‌رود
    try:
        subprocess.run([PIPER_PY, "-m", "piper", "--model", VOICE, "--output_file", rawwav],
                       input=piped.encode("utf-8"),
                       stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=120)
        if not os.path.exists(rawwav): return None
        subprocess.run(["sox", rawwav, "-r", "8000", "-c", "1", "-b", "16", final],
                       stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        subprocess.run(["chown", "asterisk:asterisk", final], stderr=subprocess.DEVNULL)
        os.remove(rawwav)
        return "arka/" + name if os.path.exists(final) else None
    except Exception as e:
        log("tts err: %s" % e); return None

def call(phone, ref):
    phone = "".join(c for c in phone if c.isdigit())
    cmd = "channel originate Local/9%s@from-internal application Playback %s" % (phone, ref)
    r = subprocess.run(["asterisk", "-rx", cmd], stdout=subprocess.PIPE,
                       stderr=subprocess.STDOUT, timeout=30)
    log("CALL %s %s -> %s" % (phone, ref, r.stdout.decode('utf-8', 'replace').strip()[:120]))

# ---- حالتِ OTP با فایل‌های صوتیِ ضبط‌شده ----
OTP_SND = "/var/lib/asterisk/sounds/arka/otp"   # intro.wav, repeat.wav, 0.wav..9.wav (8kHz)

def build_otp_wav(code, name):
    """توالی: صدای اولیه + ارقامِ کد + «مجدداً…» + ارقامِ کد → یک WAV واحد."""
    digits = [c for c in str(code) if c.isdigit()]
    if not digits:
        return None
    seq = ["intro"] + digits + ["repeat"] + digits
    parts = [os.path.join(OTP_SND, s + ".wav") for s in seq]
    for p in parts:
        if not os.path.exists(p):
            log("otp part missing: %s" % p); return None
    out = os.path.join(AST_SND, name + ".wav")
    subprocess.run(["sox"] + parts + [out], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    subprocess.run(["chown", "asterisk:asterisk", out], stderr=subprocess.DEVNULL)
    return "arka/" + name if os.path.exists(out) else None

class H(BaseHTTPRequestHandler):
    def _send(self, code, obj):
        self.send_response(code); self.send_header("Content-Type", "application/json")
        self.end_headers(); self.wfile.write(json.dumps(obj, ensure_ascii=False).encode("utf-8"))
    def do_POST(self):
        try:
            n = int(self.headers.get("Content-Length", 0))
            body = json.loads(self.rfile.read(n).decode("utf-8"))
        except Exception:
            return self._send(400, {"error": "bad json"})
        if body.get("secret") != SECRET:
            return self._send(403, {"error": "forbidden"})
        phone = str(body.get("phone", ""))
        if not phone:
            return self._send(400, {"error": "phone required"})
        if self.path.rstrip("/").endswith("call-otp"):
            # حالتِ OTP: از فایل‌های صوتیِ ضبط‌شده استفاده کن (intro + ارقام + repeat + ارقام)
            code = "".join(c for c in str(body.get("code", "")) if c.isdigit())
            if not code:
                return self._send(400, {"error": "code required"})
            name = "otpcall_" + hashlib.md5(("%s|%s|%f" % (phone, code, time.time())).encode()).hexdigest()[:12]
            ref = build_otp_wav(code, name)
        else:
            text = str(body.get("text", ""))
            if not text:
                return self._send(400, {"error": "text required"})
            name = "voice_" + hashlib.md5(("%s|%s|%f" % (phone, text, time.time())).encode()).hexdigest()[:12]
            ref = tts(text, name, raw=bool(body.get("raw", False)))
        if not ref:
            return self._send(500, {"error": "build failed"})
        call(phone, ref)
        return self._send(200, {"ok": True})
    def do_GET(self):
        self._send(200, {"ok": True, "service": "arka-voice"})
    def log_message(self, *a): pass

if __name__ == "__main__":
    log("voice service starting on :8099")
    HTTPServer(("0.0.0.0", 8099), H).serve_forever()
