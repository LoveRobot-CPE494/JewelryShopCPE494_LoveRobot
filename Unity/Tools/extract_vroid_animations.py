"""Extract named VRoid Studio motions to Unity .anim files on Windows."""

import argparse
from collections import defaultdict
from difflib import get_close_matches
import os
from pathlib import Path
import shutil
import socket
import subprocess
import tempfile
import time
from urllib.error import URLError
from urllib.parse import urlencode
from urllib.request import ProxyHandler, build_opener
import xml.etree.ElementTree as ET


def find_bundle():
    local_app_data = os.environ.get("LOCALAPPDATA")
    if local_app_data:
        versions = Path(local_app_data) / "Programs" / "VRoidStudio"
        bundles = versions.glob("*/VRoidStudio_Data/data.unity3d")
        bundle = max(bundles, key=lambda file: file.stat().st_mtime, default=None)
        if bundle:
            return bundle.resolve()
    raise ValueError("VRoid Studio was not found. Pass --bundle with the path to data.unity3d.")


def run_studio(studio, bundle, temp, mode, output, *options):
    output.mkdir()
    cache = temp / "cache"
    cache.mkdir(exist_ok=True)
    log_path = temp / f"studio-{mode}.log"
    with log_path.open("wb") as log:
        command = [
            studio, bundle, "-m", mode, "-o", output, "--decompress-to-disk",
            "--cache-dir", cache, "--log-level", "warning", *options,
        ]
        result = subprocess.run(command, stdout=log, stderr=subprocess.STDOUT, timeout=600)
    if result.returncode:
        details = log_path.read_text(encoding="utf-8", errors="replace")[-2000:]
        raise ValueError(f"AssetStudioMod {mode} failed ({result.returncode}).\n{details}")


def read_index(index_path):
    clips = defaultdict(list)
    for asset in ET.parse(index_path).getroot().findall("Asset"):
        if asset.findtext("Type") == "AnimationClip":
            name = asset.findtext("Name")
            source = Path(asset.findtext("Source")).name
            clips[name.casefold()].append((name, source, asset.findtext("PathID")))
    return clips


def select_clips(index, names):
    selected = defaultdict(set)
    for requested in names:
        name, marker, path_id = requested.rpartition("#")
        key = (name if marker else requested).casefold()
        candidates = index.get(key, [])
        if marker:
            candidates = [clip for clip in candidates if clip[2] == path_id]
        if not candidates:
            suggestions = [index[match][0][0] for match in get_close_matches(key, index)]
            hint = f" Similar names: {', '.join(suggestions)}." if suggestions else ""
            raise ValueError(f"Clip not found: {requested}.{hint} Use --list to check names.")
        if len(candidates) > 1:
            choices = ", ".join(f"{name}#{path_id} ({source})" for name, source, path_id in candidates)
            raise ValueError(f"Ambiguous clip {requested}. Choose from: {choices}")
        name, source, _ = candidates[0]
        selected[source].add(name)
    return selected


def link_or_copy(source, target):
    try:
        target.hardlink_to(source)
    except OSError:
        shutil.copyfile(source, target)


def export_source(ripper, source, job):
    job.mkdir()
    local_source = job / source.name
    link_or_copy(source, local_source)
    resource = source.with_suffix(source.suffix + ".resS")
    if resource.is_file():
        link_or_copy(resource, job / resource.name)
    output = job / "export"
    output.mkdir()

    with socket.socket() as listener:
        listener.bind(("127.0.0.1", 0))
        port = listener.getsockname()[1]
    url = f"http://127.0.0.1:{port}"
    client = build_opener(ProxyHandler({}))
    log_path = job / "ripper.log"
    with log_path.open("wb") as log:
        process = subprocess.Popen(
            [ripper, "--headless", "--port", str(port), "--log-path", job / "AssetRipper.log"],
            cwd=job,
            stdout=log,
            stderr=subprocess.STDOUT,
            creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0),
        )
        try:
            deadline = time.monotonic() + 20
            while process.poll() is None and time.monotonic() < deadline:
                try:
                    with client.open(url, timeout=1):
                        break
                except (URLError, TimeoutError):
                    time.sleep(0.2)
            else:
                raise ValueError("AssetRipper localhost did not start.")
            for endpoint, path in (("LoadFile", local_source), ("Export/UnityProject", output)):
                payload = urlencode({"Path": str(path)}).encode("ascii")
                with client.open(f"{url}/{endpoint}", data=payload, timeout=600):
                    pass
        except (URLError, TimeoutError, ValueError) as error:
            details = log_path.read_text(encoding="utf-8", errors="replace")[-2000:]
            raise ValueError(f"AssetRipper could not export {source.name}. {error}\n{details}") from error
        finally:
            if process.poll() is None:
                process.terminate()
                process.wait()
    return output / "ExportedProject" / "Assets" / "AnimationClip"


def collect_exports(ripper, unpacked, selected, temp):
    exports = []
    for number, (source, names) in enumerate(selected.items(), 1):
        input_file = unpacked / source
        if not input_file.is_file():
            raise ValueError(f"Source file is missing from the unpacked bundle: {source}")
        print(f"Exporting {len(names)} selected clip(s) from {source}...", flush=True)
        clip_root = export_source(ripper, input_file, temp / f"job-{number}")
        for name in sorted(names):
            matching = [
                clip for clip in clip_root.rglob("*.anim")
                if clip.stem.casefold() == name.casefold() and clip.stat().st_size > 0
            ]
            if len(matching) != 1:
                raise ValueError(f"Expected one .anim for {name} in {source}, found {len(matching)}.")
            exports.append(matching[0])
    return exports


def write_exports(exports, output):
    output.mkdir(parents=True, exist_ok=True)
    names = set()
    for clip in exports:
        target = output / clip.name
        if clip.name.casefold() in names or target.exists():
            raise ValueError(f"Output already exists or is duplicated: {target}. Choose a new --output.")
        names.add(clip.name.casefold())
    for clip in exports:
        target = output / clip.name
        with clip.open("rb") as reader, target.open("xb") as writer:
            shutil.copyfileobj(reader, writer)
        print(target)


def main():
    tools = Path(tempfile.gettempdir()) / "Vroid-AssetStudio-v020"
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("clips", nargs="*", help="Exact clip names or name#PathID")
    parser.add_argument("--list", action="store_true", help="List clips, optionally by substring")
    parser.add_argument("--bundle", type=Path, help="Path to VRoidStudio_Data/data.unity3d")
    parser.add_argument("--studio", type=Path, default=tools / "cli/AssetStudioModCLI.exe")
    parser.add_argument("--ripper", type=Path, default=tools / "ripper/AssetRipper.GUI.Free.exe")
    parser.add_argument("--output", type=Path, default=Path.home() / "Downloads/VRoidAnimations")
    args = parser.parse_args()
    if not args.clips and not args.list:
        parser.error("provide at least one clip name or use --list")

    try:
        bundle = args.bundle.resolve() if args.bundle else find_bundle()
        required = [(bundle, "--bundle"), (args.studio, "--studio")]
        if not args.list:
            required.append((args.ripper, "--ripper"))
        for path, flag in required:
            if not path.is_file():
                raise ValueError(f"File not found: {path}. Set {flag} to the correct file.")

        with tempfile.TemporaryDirectory(prefix="vroid-motions-") as directory:
            temp = Path(directory)
            index_folder = temp / "index"
            run_studio(args.studio.resolve(), bundle, temp, "info", index_folder,
                       "--load-all", "--export-asset-list", "xml")
            index = read_index(index_folder / "assets.xml")
            if args.list:
                filters = [clip.casefold() for clip in args.clips]
                for key, matches in sorted(index.items()):
                    if not filters or any(needle in key for needle in filters):
                        for name, source, path_id in matches:
                            print(f"{name}\t{path_id}\t{source}")
                return

            selected = select_clips(index, args.clips)
            unpacked = temp / "unpacked"
            run_studio(args.studio.resolve(), bundle, temp, "extract", unpacked)
            exports = collect_exports(args.ripper.resolve(), unpacked / f"{bundle.name}_unpacked",
                                      selected, temp)
            write_exports(exports, args.output.resolve())
    except (OSError, ValueError, ET.ParseError, subprocess.SubprocessError) as error:
        parser.exit(1, f"Error: {error}\n")


if __name__ == "__main__":
    main()
