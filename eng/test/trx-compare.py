#!/usr/bin/env python3
# Compares two trx files by test method (display names without arguments/generics) and outcome counts.
# Usage: python3 eng/test/trx-compare.py <baseline.trx> <new.trx>
import sys,re,collections
import xml.etree.ElementTree as ET
ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
def load(p):
    c=collections.Counter(); o=collections.Counter()
    for u in ET.parse(p).getroot().findall('.//t:UnitTestResult',ns):
        n=re.sub(r'<.*','',re.sub(r'\(.*','',u.get('testName'))); c[n]+=1; o[u.get('outcome')]+=1
    return c,o
a,oa=load(sys.argv[1]); b,ob=load(sys.argv[2])
diff={k:(a[k],b[k]) for k in set(a)|set(b) if a[k]!=b[k]}
print(sys.argv[2].split('/')[-1], 'methods', len(a), len(b), 'outcomes', dict(oa), dict(ob), 'per-method count differences:', len(diff))
for k,v in sorted(diff.items())[:10]: print('  ',k,v)
