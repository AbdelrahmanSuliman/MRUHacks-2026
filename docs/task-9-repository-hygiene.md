# Task 9 — Unity repository hygiene candidate

Evidence and sources checked 2026-10-24. **Decision: PASS WITH CONDITIONS.** The proposed root `.gitignore` passes isolated Git tests, but no Unity project exists here. Confirm its location with the teammate creating it before adopting this policy: put these rules at the Unity project root, whether that is the repository root or one chosen subdirectory. A successful clean-clone Unity import and Quest build are **NOT TESTED**.

## Verified state and source of truth

**VERIFIED:** repository `AbdelrahmanSuliman/MRUHacks-2026`, branch `main`, HEAD `8f84c991d1f82c42672b4d35cd2689be2bc57fb6`, upstream `origin/main` at `5aadfb7e050a991d0430df6e29745b8545018fbe` (`main` ahead 1). A read-only `git ls-remote --heads origin main` returned that same remote SHA. Git is `2.54.0 (Apple Git-157)`. Preflight index and working tree were clean; no merge, rebase, cherry-pick, revert, bisect, or sequencer marker was present. The five tracked files are `README.md`, the Task 10 report, and the three P4/P8 prototype files. Recent changes were Task 10 documentation (`8f84c99`), the P4/P8 PR merge (`5aadfb7`), the P4/P8 feature (`fa0b8c1`), and the original README (`dff6f7c`).

**VERIFIED:** no `.gitignore`, `.gitattributes`, `.lfsconfig`, Unity `Assets`, `Packages`, or `ProjectSettings` existed at preflight. `.git/info/exclude` contains only comments; no `core.excludesfile` is configured. A global `filter.lfs.required=true` exists, but this repository has no LFS attributes and `git lfs` is unavailable. The largest tracked file is 17,472 bytes; no tracked binary assets were found.

| Class | Preserve or exclude when a project arrives |
| --- | --- |
| A — essential source and configuration | Track `Assets/**`, associated file **and folder** `.meta` files, scenes, prefabs, materials, scripts, `Packages/manifest.json`, `Packages/packages-lock.json` when generated, required embedded packages, and `ProjectSettings/**` including `ProjectVersion.txt`. Unity stores asset IDs/import settings in `.meta`; its package lock should be versioned for reproducible resolution. [Unity metadata](https://docs.unity3d.com/6000.0/Documentation/Manual/AssetMetadata.html), [lock files](https://docs.unity3d.com/6000.0/Documentation/Manual/upm-conflicts-auto.html). |
| B — reconstructible outputs | Ignore Unity-root `Library`, `Temp`, `Obj`, `Logs`, `Build`, `Builds`, generated root C# project/solution files, and root APK/AAB outputs. `Library` contains rebuildable import artifacts. [Unity asset database](https://docs.unity3d.com/6000.0/Documentation/Manual/asset-database-contents.html), [GitHub Unity template](https://github.com/github/gitignore/blob/main/Unity.gitignore). |
| C — machine/user state | Ignore Unity-root `UserSettings`, `.vs`, `.gradle`, and `.DS_Store` anywhere. Do not blanket-ignore `.vscode` or project assets without evidence. |
| D — local secrets | Ignore exact `cycling-secrets.json`, `.env`, `.env.local` basenames and signing `*.keystore`/`*.jks` anywhere. The `cycling-secrets.json` name comes from the Task 9 request; **no committed source or configuration references that filename**. The actual Cesium/Google credential location is **NOT TESTED** because the project is absent. |
| E — large binaries | Keep needed source textures, models, audio and video with the project, subject to file-by-file size review. No LFS patterns are proposed now. |

## Candidate rule scope

**PROPOSED:** [root `.gitignore`](../.gitignore) anchors generated folders, IDE outputs and Android artifacts to the directory containing that file. Thus `Library/ArtifactDB` is ignored while `Assets/Library/Important.asset` remains trackable. Specific credential basenames/extensions and `.DS_Store` match at any depth. There is no `Assets/`, `Packages/`, `ProjectSettings/`, generic `.meta`, `*.json`, or recursive `**/Library/` exclusion. If Unity is placed in a subdirectory, put the same policy in that **single project directory** before adoption and repeat the tests; keep build outputs under its `Build`/`Builds` directory or outside the repository. The present root placement is an **ASSUMPTION**, not a confirmed layout.

| Rule group | Why and scope | Excluded example | Must stay trackable / exception |
| --- | --- | --- | --- |
| Unity caches and logs | Rebuildable, root directories only | `Library/ArtifactDB` | `Assets/Library/Important.asset`; a nested project needs its own placement. |
| Local/editor state | Per-developer state; root directories except `.DS_Store` basename | `UserSettings/EditorUserSettings.asset`, `.vs/cache`, `Assets/.DS_Store` | `ProjectSettings/EditorSettings.asset`; add editor-specific rules only when used. |
| Builds and generated IDE files | Root build folders, root APK/AAB and root solution/project files | `Builds/CalgaryCycling.apk`, `Assembly-CSharp.csproj` | `Assets/Scenes/Calgary.unity`; if a root `.csproj` is intentionally authored, review that rule before adopting. |
| Targeted credentials | Exact local basenames or signing-key extensions at any depth | `Assets/Config/cycling-secrets.json`, `release.keystore` | `Assets/Config/.env.example`, `Packages/manifest.json`; identify any other real secret path separately. |

## Independent Git-engine test matrix

**VERIFIED:** Python's standard library copied the candidate into two disposable `git init -q` repositories outside the working tree. Each test invoked both `git check-ignore -q --no-index -- PATH` for the effective result and `git check-ignore -v -n --no-index -- PATH` for the deciding rule; the test environment set `GIT_CONFIG_GLOBAL=/dev/null` and `GIT_CONFIG_NOSYSTEM=1`. Exit `0` from `-q` means ignored; exit `1` means trackable. The presence of verbose output alone was **not** used as evidence, because `-n` also reports nonmatches. [Git ignore semantics](https://git-scm.com/docs/gitignore/), [check-ignore semantics](https://git-scm.com/docs/git-check-ignore).

To repeat a row without touching this repository's index, run the following from the repository root, substituting one path from the matrix for `PATH`. The quiet command returns `1` for a trackable path and `0` for an ignored path; the verbose command shows the deciding rule.

```sh
TASK9_TMP=$(mktemp -d)
GIT_CONFIG_GLOBAL=/dev/null GIT_CONFIG_NOSYSTEM=1 git init -q "$TASK9_TMP"
cp .gitignore "$TASK9_TMP/.gitignore"
GIT_CONFIG_GLOBAL=/dev/null GIT_CONFIG_NOSYSTEM=1 git -C "$TASK9_TMP" check-ignore -q --no-index -- PATH
GIT_CONFIG_GLOBAL=/dev/null GIT_CONFIG_NOSYSTEM=1 git -C "$TASK9_TMP" check-ignore -v -n --no-index -- PATH
```

For the nested case, use a **second** temporary repository with the candidate at `CyclingUnity/.gitignore` and no root ignore file. This is the exact Git engine and option set used in the tests; the listing below gives every expected and observed result.

`TRACK` means expected trackable and observed trackable; `IGNORE` means expected ignored and observed ignored.

```text
TRACK  Assets/Scripts/RideController.cs
TRACK  Assets/Scripts/RideController.cs.meta
TRACK  Assets/Scenes/Calgary.unity
TRACK  Assets/Scenes/Calgary.unity.meta
TRACK  Assets/Prefabs/Bicycle.prefab
TRACK  Assets/Prefabs/Bicycle.prefab.meta
TRACK  Assets/Materials/Road.mat
TRACK  Assets/Materials/Road.mat.meta
TRACK  Packages/manifest.json
TRACK  Packages/packages-lock.json
TRACK  ProjectSettings/ProjectVersion.txt
TRACK  ProjectSettings/EditorSettings.asset
TRACK  prototypes/p4-p8-controls/RideCore.cs
TRACK  docs/task-10-quest2-readiness.md
TRACK  Assets/Cesium/CesiumGeoreference.asset
TRACK  Assets/Library/Important.asset
TRACK  docs/Build/guide.md
TRACK  Assets/Config/.env.example

IGNORE Library/ArtifactDB
IGNORE Temp/UnityLockfile
IGNORE Obj/GeneratedFile
IGNORE Logs/Editor.log
IGNORE UserSettings/EditorUserSettings.asset
IGNORE Builds/CalgaryCycling.apk
IGNORE Build/IntermediateArtifact
IGNORE Assembly-CSharp.csproj
IGNORE CalgaryCycling.sln
IGNORE CalgaryCycling.slnx
IGNORE cycling-secrets.json
IGNORE Assets/Config/cycling-secrets.json
IGNORE .env
IGNORE .env.local
IGNORE Assets/Config/.env.local
IGNORE release.keystore
IGNORE signing.jks
IGNORE CalgaryCycling.apk
IGNORE Assets/.DS_Store

NESTED IGNORE CyclingUnity/Library/ArtifactDB
NESTED TRACK  CyclingUnity/Assets/Scripts/RideController.cs.meta
NESTED TRACK  CyclingUnity/Packages/packages-lock.json
NESTED TRACK  CyclingUnity/ProjectSettings/ProjectVersion.txt
NESTED TRACK  Library/ArtifactDB
NESTED TRACK  prototypes/p4-p8-controls/RideCore.cs
```

**Result:** root positive 18/18, root negative 19/19, nested 6/6, failures **0**. In the actual repository, `git ls-files -z` plus `git check-ignore -q --no-index` found **0/5** currently tracked files affected. This checks ignore policy only; no actual Unity project can be reconstructed yet.

## Credentials, LFS and collaboration

`.gitignore` only reduces accidental addition of untracked files. It does not remove a previously committed secret, stop `git add -f`, or keep a key secret after it is packaged in a mobile APK. Review real Unity scenes, assets, settings and Cesium configuration for embedded credentials when they arrive. Google recommends API/application restrictions and monitoring quotas and billing; the right restrictions depend on the actual API access path, so do not assume Android package restrictions alone cover a direct REST request. [Google Maps security](https://developers.google.com/maps/api-security-best-practices).

**VERIFIED:** no existing file warrants Git LFS. **PROPOSED:** reconsider per asset for large or frequently changing binary textures, models, video or audio, especially near GitHub's 50 MiB warning threshold; ordinary Git pushes are blocked above 100 MiB. Do not put all `Assets` or `.meta` files in LFS. [GitHub file limits](https://docs.github.com/en/repositories/working-with-files/managing-large-files/about-large-files-on-github).

For five contributors: use short-lived branches and PR review; keep each asset with its original `.meta` GUID; agree on one editor/owner at a time for shared scenes and coordinate edits to `Packages` and `ProjectSettings`. Confirm **Visible Meta Files** and **Force Text** in the actual Unity project (Unity 6 documents both as defaults), then include their settings in the committed project. Keep generated folders out of PRs. When teammates push, fetch and inspect incoming paths before integrating them, especially scenes/settings; do not update a dirty tree blindly. After the project lands, a second contributor should clone it cleanly, open the pinned Editor, confirm package resolution and scene references, and build an APK without copying anyone's `Library` or local credentials. [Unity version control settings](https://docs.unity3d.com/6000.0/Documentation/Manual/class-VersionControlSettings.html), [serialization settings](https://docs.unity3d.com/6000.0/Documentation/Manual/class-EditorManager.html).

**Answer:** the candidate proves the required source paths stay visible to Git and tested generated/local files are ignored. Whether another contributor can reconstruct and build the eventual Unity project is **NOT TESTED** until the owner confirms the project root and supplies the actual project for clean-clone verification.
