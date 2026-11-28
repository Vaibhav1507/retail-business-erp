#!/usr/bin/env python3
from pathlib import Path
import sys, zipfile
EXPECTED={f'S{i:02d}' for i in range(1,12)}
ALLOWED={'.jpg','.jpeg','.png','.webp'}
if len(sys.argv)!=3: raise SystemExit('usage: prepare_dataset.py INPUT.zip OUTPUT_DIR')
archive,out=Path(sys.argv[1]),Path(sys.argv[2]); out.mkdir(parents=True,exist_ok=True)
with zipfile.ZipFile(archive) as z:
    found={}
    for item in z.infolist():
        if item.is_dir(): continue
        name=Path(item.filename).name; p=Path(name); key=p.stem.upper()
        if name!=item.filename or p.suffix.lower() not in ALLOWED or key not in EXPECTED: raise SystemExit(f'Unexpected ZIP entry: {name}')
        if key in found: raise SystemExit(f'Duplicate sample: {key}')
        if item.file_size>12*1024*1024: raise SystemExit(f'Image too large: {name}')
        found[key]=item
    missing=EXPECTED-set(found)
    if missing: raise SystemExit('Missing samples: '+', '.join(sorted(missing)))
    for key,item in found.items(): (out/(key+Path(item.filename).suffix.lower())).write_bytes(z.read(item))
print('Validated exactly 11 sample images.')
