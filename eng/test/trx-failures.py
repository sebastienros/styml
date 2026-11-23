#!/usr/bin/env python3
# Prints counters and failures, grouped by test method, of one or more trx files.
# Usage: python3 eng/test/trx-failures.py <file.trx> ...
import sys,re,collections
import xml.etree.ElementTree as ET
ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
for path in sys.argv[1:]:
    r=ET.parse(path).getroot()
    c=r.find('.//t:ResultSummary/t:Counters',ns).attrib
    print(path.split('/')[-1], {k:c[k] for k in ('total','executed','passed','failed','notExecuted')})
    fails=collections.OrderedDict()
    for u in r.findall('.//t:UnitTestResult',ns):
        if u.get('outcome')=='Failed':
            name=u.get('testName'); base=re.sub(r'\(.*','',name)
            msg=u.find('.//t:Message',ns)
            m=(msg.text or '').strip().split('\n')[0][:200] if msg is not None else ''
            fails.setdefault(base,[]).append(m)
    for k,v in fails.items():
        print(f'  {len(v):3d} {k}\n        {v[0]}')
