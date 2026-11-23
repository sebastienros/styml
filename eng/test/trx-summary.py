#!/usr/bin/env python3
# Prints a discovered/passed/failed/skipped table for every <Project>.<tfm>.trx in a directory.
# Usage: python3 eng/test/trx-summary.py artifacts/testresults
import sys,glob,os
import xml.etree.ElementTree as ET
ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
rows=[]
for f in sorted(glob.glob(sys.argv[1]+'/*.trx')):
    import re; name=os.path.basename(f)[:-4]; m=re.match(r'(.*)\.(net\d+(?:\.\d+)?)$',name); proj,tfm=m.group(1),m.group(2)
    c=ET.parse(f).getroot().find('.//t:ResultSummary/t:Counters',ns).attrib
    total=int(c['total']); executed=int(c['executed'])
    rows.append((tfm,proj,total,int(c['passed']),int(c['failed']),total-executed))
print('| TFM | Project | Discovered | Passed | Failed | Skipped |')
print('|---|---|---:|---:|---:|---:|')
for r in sorted(rows,key=lambda r:(['net11.0','net10.0','net481'].index(r[0]),r[1])):
    print('| %s | %s | %d | %d | %d | %d |'%r)
