"""
Generate All Mighty One hub art with Gemini, using the prompts in prompt_pack.html (adapted from the Blockhaven tool).

By default it drives the Gemini web app in its own Chrome window (uses your Gemini plan, no API charges):
  python ArtPrompts/gemini_gen.py chrome                      open that window; sign in once, leave it open
Add --api to gen/fix to use the paid Gemini API instead.

  python ArtPrompts/gemini_gen.py models                      list image models your API key can use
  python ArtPrompts/gemini_gen.py list [text]                 list prompts (done = already kept in ArtPrompts/kept)
  python ArtPrompts/gemini_gen.py gen <targets> [-n 2]        make candidates in ArtPrompts/out/<name>/
  python ArtPrompts/gemini_gen.py fix <file> "<change>" [--from 2] [-n 2]
                                                              follow-up edit, like a second message in Gemini
  python ArtPrompts/gemini_gen.py keep <file> <n>             copy candidate n into ArtPrompts/kept (then run cutout.py)

Targets: file names (obstacle_cage or obstacle_cage.png), a group ("Obstacles", "Currency, boosters & UI"),
a phase (screens, assets), or "todo" (everything not kept yet). References come from ArtPrompts/kept, then ArtPrompts/refs
(screenshots of the real hub and games), then the newest candidate.

API key (only for --api): set GEMINI_API_KEY, or put the key alone in ArtPrompts/.gemini_key (git-ignored).
Needs Chrome, Pillow and Playwright (pip install playwright; no browser download needed).
"""
import argparse, base64, io, json, math, os, re, shutil, subprocess, sys, time, traceback, urllib.error, urllib.request
from pathlib import Path
from PIL import Image

HERE = Path(__file__).resolve().parent
PROJECT = HERE.parent
PAGE = HERE / "prompt_pack.html"
OUT = HERE / "out"
SPRITES = HERE / "kept"   # kept raw images (outside Assets: the game only gets the cut-outs)
REFS = HERE / "refs"     # screenshots of the real hub and games, attached as references
API = "https://generativelanguage.googleapis.com/v1beta"
# Newest first; the first one your key can use wins (override with --model).
PREFERRED_MODELS = ["gemini-3.1-flash-image", "gemini-3.1-flash-image-preview", "gemini-3-pro-image-preview", "gemini-2.5-flash-image"]
RATIOS = ["1:1", "2:3", "3:2", "3:4", "4:3", "4:5", "5:4", "9:16", "16:9", "21:9"]
def _installed(*rel):
    """An installed browser on any drive: CHROME_PATH (if set), the Program Files folders, per-user installs, then PATH."""
    roots = [os.environ.get(k) for k in ("ProgramFiles", "ProgramFiles(x86)", "LOCALAPPDATA")]
    found = [os.path.join(r, x) for x in rel for r in roots if r]
    found += [shutil.which(os.path.basename(x)) for x in rel]
    return [f for f in found if f and os.path.exists(f)]

CHROME_EXE = [os.path.join("Google", "Chrome", "Application", "chrome.exe")]
EDGE_EXE = [os.path.join("Microsoft", "Edge", "Application", "msedge.exe")]
BROWSERS = ([os.environ["CHROME_PATH"]] if os.environ.get("CHROME_PATH") else []) + _installed(*EDGE_EXE) + _installed(*CHROME_EXE)
REF_MAX_SIDE = 1280
# Browser mode: a separate Chrome profile (Chrome refuses remote debugging on the default one) that you sign in to once.
# Shared with the Blockhaven tool, so the Gemini sign-in from there carries over.
CHROME = os.environ.get("CHROME_PATH") or next(iter(_installed(*CHROME_EXE)), None)
CHROME_PROFILE = Path(os.environ.get("LOCALAPPDATA", HERE)) / "Blockhaven" / "gemini-chrome"
CDP_PORT = 9333
GEMINI_URL = "https://gemini.google.com/app"


# ---------- prompts ----------

def load_prompts():
    """Runs the page in a headless browser and reads the JSON it exports (cached until the page changes)."""
    cache = OUT / "prompts.json"
    if cache.exists() and cache.stat().st_mtime >= PAGE.stat().st_mtime:
        return json.loads(cache.read_text(encoding="utf-8"))
    browser = next((b for b in BROWSERS if os.path.exists(b)), None)
    if not browser: sys.exit("Need Microsoft Edge or Chrome to read prompt_pack.html.")
    dom = subprocess.run([browser, "--headless=new", "--disable-gpu", "--virtual-time-budget=3000", "--dump-dom", PAGE.as_uri()],
                         capture_output=True, text=True, encoding="utf-8", timeout=120).stdout
    m = re.search(r'<script type="application/json" id="prompt-export">(.*?)</script>', dom, re.S)
    if not m: sys.exit("Couldn't read the prompts from prompt_pack.html (no prompt-export block).")
    prompts = json.loads(m.group(1))
    OUT.mkdir(exist_ok=True)
    cache.write_text(json.dumps(prompts, indent=1), encoding="utf-8")
    return prompts

def folder(p):
    """Where a kept image goes: ArtPrompts/kept/<group>."""
    return SPRITES / p["group"]

def find_sprite(name):
    """A kept image anywhere under ArtPrompts/kept, by file name."""
    name = name.lower()
    for path in SPRITES.rglob("*.png"):
        if path.name.lower() == name: return path
    return None

def stem(file): return Path(file).stem

def latest_candidate(file):
    name = stem(file)
    found = [(int(m.group(1)), f) for f in (OUT / name).glob(f"{name}_*.png") if (m := re.fullmatch(rf"{re.escape(name)}_(\d+)\.png", f.name))]
    return max(found)[1] if found else None

def find_ref(name):
    """A reference image: the kept one in Assets/Sprites, else the newest candidate (so chains like node_open →
    node_locked can run in one go; re-run the second one if you keep a different candidate)."""
    if not name.lower().endswith(".png"): name += ".png"
    ref = REFS / name
    return find_sprite(name) or (ref if ref.exists() else None) or latest_candidate(name)

def select(prompts, targets):
    chosen = []
    for t in targets:
        tl = t.lower().removesuffix(".png")
        if tl == "todo":  # not kept yet and no candidates yet (references are checked when its turn comes)
            hits = [p for p in prompts if not find_sprite(p["file"]) and not latest_candidate(p["file"])]
        else:
            hits = [p for p in prompts if stem(p["file"]).lower() == tl or p["group"].lower() == tl or p["phase"] == tl]
        if not hits: sys.exit(f"No prompt matches '{t}'. Try: python {Path(__file__).name} list")
        chosen += [p for p in hits if p not in chosen]
    return chosen


# ---------- Gemini API ----------

def api_key():
    key = os.environ.get("GEMINI_API_KEY") or os.environ.get("GOOGLE_API_KEY")
    keyfile = HERE / ".gemini_key"
    if not key and keyfile.exists(): key = keyfile.read_text().strip()
    if not key: sys.exit("No API key. Get one at https://aistudio.google.com/apikey, then set GEMINI_API_KEY or save it in ArtPrompts/.gemini_key")
    return key

def call(method, url, body=None):
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(url, data=data, method=method,
                                 headers={"x-goog-api-key": api_key(), "Content-Type": "application/json"})
    for attempt, wait in enumerate([10, 30, 60, None]):
        try:
            with urllib.request.urlopen(req, timeout=300) as r:
                return json.loads(r.read())
        except urllib.error.HTTPError as e:
            detail = e.read().decode(errors="replace")
            if e.code in (429, 500, 502, 503, 504) and wait:
                print(f"  API {e.code}, retrying in {wait}s...")
                time.sleep(wait)
                continue
            try: detail = json.loads(detail)["error"]["message"]
            except Exception: pass
            raise RuntimeError(f"API error {e.code}: {detail}") from None

def image_models():
    models = call("GET", f"{API}/models?pageSize=1000").get("models", [])
    return [m["name"].removeprefix("models/") for m in models
            if "generateContent" in m.get("supportedGenerationMethods", []) and "image" in m["name"] and "imagen" not in m["name"]]

def pick_model(requested):
    if requested: return requested
    available = image_models()
    for m in PREFERRED_MODELS:
        if m in available: return m
    if available: return sorted(available)[-1]
    sys.exit("Your API key has no Gemini image models. Check billing at https://aistudio.google.com")

def nearest_ratio(aspect, ratios=RATIOS):
    w, h = (float(x) for x in aspect.split(":"))
    return min(ratios, key=lambda r: abs(math.log((w / h) / (float(r.split(":")[0]) / float(r.split(":")[1])))))

def encode(path):
    """Reference image as a JPEG part, scaled down so requests stay small."""
    img = Image.open(path).convert("RGB")
    img.thumbnail((REF_MAX_SIDE, REF_MAX_SIDE))
    buf = io.BytesIO()
    img.save(buf, "JPEG", quality=90)
    return {"inline_data": {"mime_type": "image/jpeg", "data": base64.b64encode(buf.getvalue()).decode()}}

def generate(model, prompt, images, aspect, size):
    config = {"responseModalities": ["TEXT", "IMAGE"], "imageConfig": {"aspectRatio": nearest_ratio(aspect)}}
    if size: config["imageConfig"]["imageSize"] = size
    body = {"contents": [{"role": "user", "parts": [encode(i) for i in images] + [{"text": prompt}]}], "generationConfig": config}
    reply = call("POST", f"{API}/models/{model}:generateContent", body)
    texts = []
    for cand in reply.get("candidates", []):
        for part in cand.get("content", {}).get("parts", []):
            data = part.get("inlineData") or part.get("inline_data")
            if data: return Image.open(io.BytesIO(base64.b64decode(data["data"])))
            if part.get("text"): texts.append(part["text"])
    reason = (reply.get("candidates") or [{}])[0].get("finishReason") or reply.get("promptFeedback", {}).get("blockReason")
    raise RuntimeError(f"No image returned ({reason}). {' '.join(texts)[:300]}")

def next_index(folder_path, name):
    taken = [int(m.group(1)) for f in folder_path.glob(f"{name}_*.png") if (m := re.fullmatch(rf"{re.escape(name)}_(\d+)\.png", f.name))]
    return max(taken, default=0) + 1

def make(engine, p, prompt, images, n, pause=0):
    name = stem(p["file"])
    dest = OUT / name
    dest.mkdir(parents=True, exist_ok=True)
    for k in range(n):
        i = next_index(dest, name)
        t = time.time()
        try:
            img = engine(prompt, images, p["aspect"])
        except Exception as e:  # includes browser timeouts: report and let the caller move on
            where = next((f"line {fr.lineno}" for fr in reversed(traceback.extract_tb(e.__traceback__)) if fr.filename == __file__), "")
            print(f"  {name}: FAILED ({where}) {type(e).__name__}: {str(e).splitlines()[0][:300]}")
            return False
        path = dest / f"{name}_{i}.png"
        img.save(path)
        print(f"  {path.relative_to(PROJECT)}  {img.width}x{img.height}  {time.time() - t:.0f}s")
        if pause and k + 1 < n: time.sleep(pause)
    return True

def api_engine(args):
    model = pick_model(args.model)
    print(f"Gemini API, model {model}")
    return lambda prompt, images, aspect: generate(model, prompt, images, aspect, args.size)


# ---------- Gemini web app (browser mode) ----------

class GeminiWeb:
    """Drives gemini.google.com in the Chrome window opened by the `chrome` command, like you would by hand."""
    RATIOS = ["1:1", "9:16", "3:4", "4:3", "16:9"]  # what the app's Aspect ratio menu offers
    TIMEOUT = 300
    MAX_PER_CHAT = 8  # long chats get slow (each image added ~5s) and the page can stop responding

    def __init__(self):
        if not cdp_up():
            sys.exit("The Gemini Chrome window isn't open. Run: python ArtPrompts/gemini_gen.py chrome  (sign in, leave it open)")
        from playwright.sync_api import sync_playwright
        self._pw = sync_playwright().start()
        browser = self._pw.chromium.connect_over_cdp(f"http://127.0.0.1:{CDP_PORT}")
        self._context = browser.contexts[0]
        self.page = self._context.new_page()
        self.in_chat = None  # images made in the current chat; None = start a new chat next time
        print("Gemini web app (your Chrome window)")

    def close(self):
        try: self.page.close()
        finally: self._pw.stop()

    def new_chat(self):
        """The next image starts a fresh chat (the tool keeps one chat per group of prompts)."""
        self.in_chat = None

    def __call__(self, prompt, images, aspect):
        if self.page.is_closed():  # the tab sometimes dies mid-batch (renderer crash); carry on in a new one
            self.page = self._context.new_page()
            self.in_chat = None
        page = self.page
        page.bring_to_front()  # Chrome holds back a tab it isn't showing: menus open empty, clicks time out
        if self.in_chat is None or self.in_chat >= self.MAX_PER_CHAT:
            page.goto(GEMINI_URL)
            page.wait_for_selector("div.ql-editor", timeout=60000)
            if "accounts.google.com" in page.url: raise RuntimeError("Not signed in: sign in to Gemini in the Chrome window.")
            limit = page.get_by_text("Your limit resets at")
            if limit.count(): raise RuntimeError(f"Out of images for now: {limit.first.inner_text().strip()}.")
            self.in_chat = 0
        else:
            prompt = "Next, a new and separate image (don't reuse earlier images in this chat unless they are attached). " + prompt

        if not page.get_by_role("button", name="Deselect Images").count():  # the Create image tool stays on within a chat
            create = page.get_by_role("menuitemcheckbox", name="Create image")
            for attempt in range(4):  # the tools menu sometimes doesn't open on the first click
                page.get_by_role("button", name="Upload & tools").click()
                try: create.wait_for(state="visible", timeout=5000); break
                except Exception:
                    page.keyboard.press("Escape")
                    page.wait_for_timeout(1500)
            else: raise RuntimeError("The Create image tool didn't show up in the tools menu.")
            if create.get_attribute("aria-checked") == "true": page.keyboard.press("Escape")
            else: create.click()
            page.wait_for_timeout(600)

        if images:
            files = [str(self._upload_copy(i)) for i in images]
            page.get_by_role("button", name="Upload & tools").click()
            with page.expect_file_chooser(timeout=15000) as chooser:
                # older layout: "Upload files" menu item; newer one: a "Files" button in the same menu
                page.get_by_role("menuitem", name="Upload files").or_(page.get_by_role("button", name="Files", exact=True)).first.click()
            chooser.value.set_files(files)
            attachments = page.locator("img.gem-attachment-style-img")
            for _ in range(120):
                if attachments.count() >= len(files): break
                page.wait_for_timeout(500)
            else: raise RuntimeError("Reference images didn't finish uploading.")

        # The app's aspect setting is only a hint: with a tall reference attached it may still draw tall,
        # so the prompt says the shape too (the cut-out crops anyway).
        ratio = nearest_ratio(aspect, self.RATIOS)
        try:
            page.locator("button:has-text('Aspect ratio'), button[aria-label^='Aspect ratio']").first.click(timeout=5000)
            page.get_by_role("menuitemradio", name=ratio, exact=True).click(timeout=5000)
            page.wait_for_timeout(400)
        except Exception:
            page.keyboard.press("Escape")
        shape = "landscape" if ratio in ("16:9", "4:3") else "portrait" if ratio in ("9:16", "3:4") else "square"
        prompt = f"{prompt} Output a {shape} {ratio} image."

        page.locator("div.ql-editor").click()
        page.keyboard.insert_text("Create an image. " + prompt)
        send = page.get_by_role("button", name="Send message")
        for _ in range(60):  # stays disabled while uploads finish
            if send.is_enabled(): break
            page.wait_for_timeout(500)
        before = page.locator("model-response").count()
        page.locator("div.ql-editor").press("Enter")  # clicking Send can hang in a long chat; Enter doesn't

        responses = page.locator("model-response")
        start = time.time()
        while time.time() - start < self.TIMEOUT:
            page.wait_for_timeout(2000)
            if responses.count() <= before: continue
            reply = responses.last
            image = reply.locator("generated-image img")
            try:  # the img can be swapped out while it loads; just look again next round
                if image.count() and image.last.evaluate("i => i.complete && i.naturalWidth > 200", timeout=5000): break
            except Exception: continue
            # The rating buttons appear once the answer is finished; finished with no image = refused or out of quota.
            if reply.get_by_role("button", name="Good response").count() and not reply.locator("generated-image").count():
                raise RuntimeError("Gemini answered without an image: " + " ".join(reply.inner_text().split())[:300])
        else: raise RuntimeError(f"No image after {self.TIMEOUT}s.")
        result = responses.last.locator("generated-image img")

        path = OUT / "_download.png"
        for attempt in range(3):  # a download sometimes comes back as something that isn't an image
            result.last.hover()
            with page.expect_download(timeout=120000) as download:
                page.locator("model-response").last.get_by_role("button", name="Download full size image").first.click()
            download.value.save_as(path)
            try:
                img = Image.open(path)
                img.load()
                break
            except Exception:
                if attempt == 2: raise
                page.wait_for_timeout(3000)
        self.in_chat += 1
        return img

    @staticmethod
    def _upload_copy(path):
        """Uploads go faster as a scaled-down JPEG; the generator only needs the look, not every pixel. Rebuilt every
        time (cheap): the cut-out it prefers may have changed since the last copy."""
        out = OUT / "_refs" / (Path(path).stem + ".jpg")
        out.parent.mkdir(parents=True, exist_ok=True)
        img = Image.open(path).convert("RGB")
        img.thumbnail((REF_MAX_SIDE * 2, REF_MAX_SIDE * 2))
        img.save(out, "JPEG", quality=92)
        return out

def open_engine(args):
    if args.api: return api_engine(args), lambda: None
    web = GeminiWeb()
    return web, web.close


# ---------- commands ----------

def cdp_up():
    try:
        with urllib.request.urlopen(f"http://127.0.0.1:{CDP_PORT}/json/version", timeout=2): return True
    except Exception: return False

def cmd_chrome(args):
    """Opens the Gemini Chrome window (sign in once; the profile remembers it)."""
    if cdp_up(): return print(f"Gemini Chrome is already open (port {CDP_PORT}).")
    if not CHROME: sys.exit(r'Google Chrome was not found. Install it, or set CHROME_PATH to chrome.exe, e.g.: setx CHROME_PATH "D:\Chrome\chrome.exe"')
    CHROME_PROFILE.mkdir(parents=True, exist_ok=True)
    cmd = [CHROME, f"--remote-debugging-port={CDP_PORT}", f"--user-data-dir={CHROME_PROFILE}",
           "--no-first-run", "--no-default-browser-check",
           # keep rendering when the window is behind other windows (otherwise the tools menu can open empty)
           "--disable-backgrounding-occluded-windows", "--disable-renderer-backgrounding", "--disable-background-timer-throttling",
           GEMINI_URL]
    # Detached so Chrome outlives the shell that started it (as a child it got killed mid-batch with that shell's job).
    flags = subprocess.DETACHED_PROCESS | subprocess.CREATE_NEW_PROCESS_GROUP if os.name == "nt" else 0
    try: subprocess.Popen(cmd, creationflags=flags | (subprocess.CREATE_BREAKAWAY_FROM_JOB if os.name == "nt" else 0), close_fds=True)
    except OSError: subprocess.Popen(cmd, creationflags=flags, close_fds=True)  # the job may not allow breakaway
    for _ in range(40):
        if cdp_up(): return print(f"Opened Gemini Chrome (profile {CHROME_PROFILE}). Sign in to Gemini there if it asks.")
        time.sleep(0.5)
    sys.exit("Chrome didn't open its debugging port. Close every window of that profile and try again.")

def cmd_models(args):
    for m in image_models(): print(m)

def cmd_list(args, prompts):
    for p in prompts:
        line = f"{p['phase']:8} {p['group'][:24]:24} {p['file']:34} {'done' if find_sprite(p['file']) else '    '}  refs: {', '.join(p['refs']) or '-'}"
        if not args.text or args.text.lower() in line.lower(): print(line)

def cmd_gen(args, prompts):
    chosen = select(prompts, args.targets)
    engine, close = open_engine(args)
    print(f"{len(chosen)} prompt(s) x {args.n}")
    group = prev = None
    failed = []
    try:
        for k, p in enumerate(chosen):
            refs = [find_ref(r) for r in p["refs"]]
            missing = [r for r, path in zip(p["refs"], refs) if path is None]
            if missing:
                print(f"- {p['file']}: skipped, make these first: {', '.join(missing)}")
                continue
            # Fresh chat per prompt unless it builds on the previous one (portrait_x_happy -> portrait_x_worried):
            # earlier images in a chat bleed into the next one (Tobb came out as Brom, Juna as Nell).
            if hasattr(engine, "new_chat") and (prev is None or prev["file"] not in p["refs"]):
                engine.new_chat()
                if p["group"] != group: print(f"== {p['group']}")
            group, prev = p["group"], p
            print(f"- {p['name']} -> {p['file']}" + (f"  (+ {', '.join(p['refs'])})" if refs else ""))
            if not make(engine, p, p["prompt"], refs, args.n, args.pause):
                failed.append(p["file"])
                if not args.keep_going: sys.exit(1)
                engine_new_chat = getattr(engine, "new_chat", None)
                if engine_new_chat: engine_new_chat()  # a stuck chat shouldn't poison the next prompt
                if len(failed) >= 3 and all(f in failed for f in [x["file"] for x in chosen[max(0, k - 2):k + 1]]):
                    sys.exit("Three failures in a row (daily limit or page change?); stopping.")
            if args.pause and k + 1 < len(chosen): time.sleep(args.pause)
    finally:
        close()
        if failed: print("Failed: " + ", ".join(failed))

def cmd_fix(args, prompts):
    p = select(prompts, [args.file])[0]
    name = stem(p["file"])
    base = OUT / name / f"{name}_{args.base}.png" if args.base else find_sprite(p["file"])
    if not base or not base.exists(): sys.exit(f"Nothing to fix: no {'candidate ' + str(args.base) if args.base else p['file'] + ' in ArtPrompts/kept'}.")
    refs = [base] + [r for r in (find_ref(x) for x in p["refs"]) if r]
    prompt = (f"The first attached image is the current version of this game art. Keep everything about it the same "
              f"(art style, layout, framing, colours, background) and change only this: {args.change}")
    engine, close = open_engine(args)
    print(f"Fixing {base.relative_to(PROJECT)}")
    try: make(engine, p, prompt, refs, args.n, args.pause)
    finally: close()

def cmd_keep(args, prompts):
    p = select(prompts, [args.file])[0]
    name = stem(p["file"])
    src = OUT / name / f"{name}_{args.index}.png"
    if not src.exists(): sys.exit(f"No candidate {src.relative_to(PROJECT)}")
    dest = folder(p) / p["file"]
    dest.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(src, dest)
    print(f"Kept {src.name} as {dest.relative_to(PROJECT)}")

def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)
    sub.add_parser("models")
    sub.add_parser("chrome")
    s = sub.add_parser("list"); s.add_argument("text", nargs="?")
    for name in ("gen", "fix"):
        s = sub.add_parser(name)
        if name == "gen":
            s.add_argument("targets", nargs="+")
            s.add_argument("--keep-going", action="store_true", help="don't stop at the first failed prompt")
        else:
            s.add_argument("file"); s.add_argument("change")
            s.add_argument("--from", dest="base", type=int, help="fix candidate N instead of the kept image")
        s.add_argument("-n", type=int, default=2, help="candidates per prompt (default 2)")
        s.add_argument("--pause", type=float, default=10, help="seconds between generations (default 10; be gentle with the web app)")
        s.add_argument("--api", action="store_true", help="use the paid Gemini API instead of the web app")
        s.add_argument("--model", help="--api only: model id (default: newest image model your key has)")
        s.add_argument("--size", help='--api only: image size for models that support it, e.g. "2K"')
    s = sub.add_parser("keep"); s.add_argument("file"); s.add_argument("index", type=int)
    args = ap.parse_args()
    if args.cmd == "models": return cmd_models(args)
    if args.cmd == "chrome": return cmd_chrome(args)
    prompts = load_prompts()
    {"list": cmd_list, "gen": cmd_gen, "fix": cmd_fix, "keep": cmd_keep}[args.cmd](args, prompts)

if __name__ == "__main__":
    main()
