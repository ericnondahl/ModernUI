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
4. Create the GitHub release `v<version>` with `ModernUI.zip` (the asset name the index
   reads) and the demo video attached.
5. The content index's watcher picks new releases up from GitHub by itself once listed.

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
