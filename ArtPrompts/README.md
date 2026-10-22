# Hub art: running it yourself

Everything here works without Claude. Run the commands in a terminal (PowerShell or Command Prompt)
from the project folder: `cd "C:\UnityProjects\All Mighty one"`.

## One-time setup

1. Python 3 (already installed on this PC).
2. `pip install playwright pillow numpy` (no Playwright browser download needed: the tool uses your Chrome).
3. Google Chrome installed (Microsoft Edge is also used, in the background, to read the prompts).

## Another PC or another drive

The tools find everything relative to their own folder, so the project can live anywhere (`D:\Work\All Mighty one`,
another user name, ...). Just `cd` into the project folder on that PC and use the same commands.

- Copy the `ArtPrompts` folder along with the project (it is outside `Assets`). `refs` holds the reference screenshots,
  `kept` your chosen images; `out` (candidates) is optional.
- Do the one-time setup on that PC, and sign in to Gemini once in the window that `chrome` opens (each PC has its own
  Gemini window profile).
- Chrome and Edge are found in their normal install folders on any drive. If Chrome is somewhere unusual, set its path
  once: `setx CHROME_PATH "D:\Apps\Chrome\Application\chrome.exe"` (then open a new terminal).

## Make images

1. Open the Gemini window: `python ArtPrompts/gemini_gen.py chrome`
   Sign in to Gemini there if it asks (only the first time), then leave that window open and don't click in it while it works.
2. See what there is: `python ArtPrompts/gemini_gen.py list` ("done" = already kept)
3. Generate:
   - everything not done yet: `python ArtPrompts/gemini_gen.py gen todo -n 1 --keep-going`
   - one image: `python ArtPrompts/gemini_gen.py gen icon_ninja -n 2`
   - a group: `python ArtPrompts/gemini_gen.py gen "Game icons" -n 1 --keep-going`
   Results land in `ArtPrompts/out/<name>/<name>_1.png`, `_2.png`, ... Look at them in any image viewer.
4. Ask for a change to a result, like a follow-up message in Gemini:
   `python ArtPrompts/gemini_gen.py fix icon_ninja "make the zombie bigger" --from 2`
5. Keep the one you like: `python ArtPrompts/gemini_gen.py keep icon_ninja 2` (copies it to `ArtPrompts/kept/`)

Gemini allows roughly 25 images a day; when it says the limit is reached, continue the next day.

## Put the art in the game

1. `python ArtPrompts/cutout.py` (crops, removes the logo background and the Gemini sparkle, writes into `Assets/_Collection/Art`)
2. In Unity: menu **All Mighty One > Hub > Build Hub And Home Buttons**. Then press Play in the Hub scene.

## Without the tool (by hand)

Open `ArtPrompts/prompt_pack.html` in your browser (double-click it). For each card: attach the images named on the
orange chips (from `ArtPrompts/refs/` or `ArtPrompts/kept/`), paste the prompt into gemini.google.com, download the result,
and save it under the card's file name in `ArtPrompts/kept/<group>/` (groups: `Screens`, `Logo & store`, `Game icons`, `Hub`).
Then do "Put the art in the game" above.

## Changing prompts

Prompts live in `ArtPrompts/prompt_pack.html` (the `SCREENS`, `LOGO`, `GAMES`/`ICONS` and `HUB` lists near the bottom).
Edit the text there and save; the tool reads the new prompts on its next run.
