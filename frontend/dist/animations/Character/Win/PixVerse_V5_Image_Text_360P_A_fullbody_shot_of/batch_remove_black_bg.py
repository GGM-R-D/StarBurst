#!/usr/bin/env python3
"""
Batch-remove black backgrounds from images in a folder.

Usage:
  python batch_remove_black_bg.py --input ./images --output ./out --threshold 20

Dependencies:
  pip install opencv-python numpy tqdm
"""

import os
import sys
import argparse
from concurrent.futures import ThreadPoolExecutor, as_completed

import cv2
import numpy as np
from tqdm import tqdm

IMAGE_EXTS = {".jpg", ".jpeg", ".png", ".bmp", ".tif", ".tiff", ".webp"}

def remove_black_background_array(bgr: np.ndarray, threshold: int = 20, feather: int = 3, dilate: int = 0) -> np.ndarray:
    """
    Remove black-ish background from a BGR image and return BGRA (with transparency).
    
    Args:
        bgr: Input image in BGR (OpenCV) format.
        threshold: 0-255; pixels with all channels <= threshold are considered black background.
        feather: Odd kernel size for Gaussian blur on the alpha for smoother edges (0 to disable).
        dilate: Number of iterations to dilate the background mask to avoid dark halos.
    """
    if bgr is None or bgr.size == 0:
        raise ValueError("Empty image provided")
        
    # Ensure BGRA
    bgra = cv2.cvtColor(bgr, cv2.COLOR_BGR2BGRA)

    # "Black-ish" rule: background if ALL channels are at or below threshold
    black_mask = (bgr[:,:,0] <= threshold) & (bgr[:,:,1] <= threshold) & (bgr[:,:,2] <= threshold)
    mask = black_mask.astype(np.uint8) * 255

    # Optional refinement: dilate mask to push transparency slightly further into dark zones,
    # which reduces residual dark fringes on object edges
    if dilate > 0:
        kernel = np.ones((3,3), np.uint8)
        mask = cv2.dilate(mask, kernel, iterations=dilate)

    # Alpha: 0 where background (mask==255), 255 elsewhere
    alpha = cv2.bitwise_not(mask)

    # Optional feathering for smoother transitions
    if feather and feather > 0 and feather % 2 == 1:
        alpha = cv2.GaussianBlur(alpha, (feather, feather), 0)

    bgra[:,:,3] = alpha
    return bgra

def process_one(in_path: str, out_dir: str, threshold: int, feather: int, dilate_iters: int, keep_structure: bool) -> str:
    rel_dir = os.path.relpath(os.path.dirname(in_path), start=args.input) if keep_structure else ""
    target_dir = os.path.join(out_dir, rel_dir)
    os.makedirs(target_dir, exist_ok=True)
    stem, _ = os.path.splitext(os.path.basename(in_path))
    out_path = os.path.join(target_dir, f"{stem}.png")  # always write PNG to preserve alpha

    bgr = cv2.imread(in_path, cv2.IMREAD_COLOR)
    if bgr is None:
        return f"SKIP (unreadable): {in_path}"
    try:
        bgra = remove_black_background_array(bgr, threshold=threshold, feather=feather, dilate=dilate_iters)
        ok = cv2.imwrite(out_path, bgra)
        if not ok:
            return f"ERROR (write failed): {out_path}"
        return f"OK: {out_path}"
    except Exception as e:
        return f"ERROR ({in_path}): {e}"

def list_images(root: str):
    for dirpath, _, filenames in os.walk(root):
        for fn in filenames:
            ext = os.path.splitext(fn)[1].lower()
            if ext in IMAGE_EXTS:
                yield os.path.join(dirpath, fn)

def parse_args(argv=None):
    p = argparse.ArgumentParser(description="Batch-remove black backgrounds from images in a folder.")
    p.add_argument("--input", "-i", required=False, default=".", help="Input folder to scan for images (default: current dir)")
    p.add_argument("--output", "-o", required=False, default="./output", help="Output folder (default: ./output)")
    p.add_argument("--threshold", "-t", type=int, default=20, help="Black threshold 0-255; higher = more aggressive (default: 20)")
    p.add_argument("--feather", type=int, default=3, help="Odd kernel size for Gaussian blur on alpha; 0 disables (default: 3)")
    p.add_argument("--dilate", type=int, default=1, help="Dilate iterations on background mask to avoid dark halos (default: 1)")
    p.add_argument("--workers", type=int, default= max(1, os.cpu_count() or 4), help="Parallel workers (default: CPU count)")
    p.add_argument("--keep-structure", action="store_true", help="Mirror input subfolder structure in output")
    return p.parse_args(argv)

def main(argv=None):
    global args
    args = parse_args(argv)

    if not os.path.isdir(args.input):
        print(f"Input folder does not exist: {args.input}", file=sys.stderr)
        return 2

    os.makedirs(args.output, exist_ok=True)

    files = list(list_images(args.input))
    if not files:
        print("No images found in the input folder.")
        return 0

    results = []
    with ThreadPoolExecutor(max_workers=args.workers) as ex:
        futures = {
            ex.submit(process_one, f, args.output, args.threshold, args.feather, args.dilate, args.keep_structure): f
            for f in files
        }
        for fut in tqdm(as_completed(futures), total=len(futures), desc="Processing", unit="img"):
            results.append(fut.result())

    # Summary
    ok = sum(1 for r in results if r.startswith("OK"))
    err = sum(1 for r in results if r.startswith("ERROR"))
    skip = sum(1 for r in results if r.startswith("SKIP"))
    print(f"\nSummary: {ok} OK, {err} errors, {skip} skipped.")
    return 0

if __name__ == "__main__":
    raise SystemExit(main())
