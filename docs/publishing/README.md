# Publishing ModernUI

The steps to publish a release and list it in the KSA content index (what Borea installs
from). See the index's [CONTRIBUTING](https://github.com/KSAModding/content-index/blob/main/CONTRIBUTING.md)
and [POLICY](https://github.com/KSAModding/content-index/blob/main/POLICY.md).

## Each release

1. Bump `VersionPrefix` (and `AssemblyVersion`) in `ModernUI/ModernUI.csproj`.
2. `scripts/release.sh --check` builds `dist/ModernUI.zip` (one top-level `ModernUI/`
   folder; never DevBridge) and prints its contents.
3. Test the zip as a player gets it: unzip into a clean instance's `mods/`, enable only
   ModernUI, start through StarMap.
4. Create the GitHub release `v<version>` with `ModernUI.zip` attached (the asset name
   the index reads). The demo video is shared outside the repository.
5. The content index's watcher picks new releases up from GitHub by itself once listed.
6. A release that changes how the HUD looks: also do [the screenshots and the listing](#when-a-release-changes-how-the-hud-looks).

## When a release changes how the HUD looks

The README and the Borea page show screenshots of the HUD. The Borea page comes from the
listing in the content index, not from the README, and a new release doesn't change it.
A feature release that changes what a player sees needs both updated, or they show the old
HUD. A fix that changes nothing on screen doesn't.

1. **Retake the screenshots** that show what changed, and add one for a new feature. Save
   each under a new name (`flight-v3.jpg`), never over the old file: the listing holds each
   image's sha256, and an overwritten image breaks the Borea page until the listing update
   merges. A 16:9 window gives a 1920x1080 image with nothing overlapping (the HUD lays out
   for a 1080-high screen at any window size):

   ```sh
   KSA_WINDOW=2240x1298 scripts/dev.sh restart      # draws about 2240x1255 under the title bar
   KSA_VEHICLE=Rocket scripts/dev.sh flight         # on the pad; P picks the mode
   scripts/dev.sh call click --data-urlencode button=STAGING   # open a panel (see `call buttons`)
   scripts/dev.sh shot flight
   ffmpeg -i screenshots/flight-<time>.png \
     -vf "crop=trunc(ih*16/9/2)*2:ih,scale=1920:1080:flags=lanczos" -q:v 3 images/flight-v3.jpg
   ```

   On a 2x (Retina) display, a window half that size gives the same pixels, but the
   frame is a little taller than 16:9: crop its height instead, from the top, so the menu
   bar stays whole:

   ```sh
   KSA_WINDOW=1120x649 scripts/dev.sh restart       # 2240x1298 pixels
   ffmpeg -i screenshots/flight-<time>.png \
     -vf "crop=iw:trunc(iw*9/16/2)*2:0:0,scale=1920:1080:flags=lanczos" -q:v 3 images/flight-v3.jpg
   ```

   Look at each image before using it: no panel overlaps another.
2. **README:** point it at the new images.
3. **Listing** (`docs/publishing/ModernUI.toml`): describe the new features, show new
   images with `![alt](ksa-image:<id>)`, and update the records. In a checkout of your fork
   of [content-index](https://github.com/KSAModding/content-index), after
   `pip install -r tools/requirements.txt`, `tools/image_record.py` prints each record:

   ```sh
   python3 tools/image_record.py ~/personal/ModernUI/images/flight-v3.jpg --description Flight \
     --url "https://github.com/ericnondahl/ModernUI/blob/main/images/flight-v3.jpg?raw=true"
   ```

   An image can be at most 2048 pixels on a side and 1 MiB.
4. **Push to main first:** the index's checks fetch the images from those URLs.
5. **Index pull request:** on a branch from the index's `main`, copy `ModernUI.toml` over
   `listings/ModernUI.toml` and run the checks (as in [Listing it the first time](#listing-it-the-first-time),
   including `check_images.py` and `check_release.py` with the network). Open the pull
   request; it merges by itself once the checks pass. Once it has merged, the replaced
   image files can be deleted.
6. **Forums:** reply in the thread with what's new (a draft such as
   `forum-update-0.2.0.md`), and update the first post's title and version.

## Listing it the first time

1. The repository must be public (the index fetches the release and the images).
2. Post the forums thread (`forum-post.md`) and put its URL in `ModernUI.toml`.
3. In a fork of KSAModding/content-index, add `ModernUI.toml` as `listings/ModernUI.toml`
   and run its checks (`tools/check_*.py`, including `check_images.py` and
   `check_release.py` with the network).
4. Open the pull request. The repository is under the author's own account, which proves
   control of the release host, so it merges by itself once the checks pass.

The image records in `ModernUI.toml` hold each image's sha256, size and dimensions: change
an image in `images/` and they must be regenerated.
