#!/usr/bin/env python3
# Matches every [Fact]/[Theory]/[ConditionalFact]/[ConditionalTheory] method compiled into a test project against
# the tests a trx file executed, and explains the ones that did not run (OuterLoop, ActiveIssue, #if, ...).
# Usage: python3 eng/test/reconcile-tests.py test/<Project>/<Project>.csproj <file.trx>
import re,sys,os,glob,collections
import xml.etree.ElementTree as ET
ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
proj,trx=sys.argv[1],sys.argv[2]
d=os.path.dirname(proj)
# compiled files (approximation: Compile items, resolving known path properties; wildcards expanded)
files=[]
for inc,cond in re.findall(r'<Compile Include="([^"]+)"[^>]*/>()',open(proj).read()):
    inc=inc.replace('$(RepoRoot)','').replace('$(CommonPath)','src/Vendored/Common/').replace('$(CoreLibSharedDir)','src/Vendored/CoreLib/').replace('$(CommonTestPath)','test/Vendored/').replace('\\','/')
    p=inc if inc.startswith(('src/','test/')) else os.path.normpath(os.path.join(d,inc))
    files+=glob.glob(p)
attr_re=re.compile(r'\[(Fact|Theory|ConditionalFact|ConditionalTheory)\b')
meth_re=re.compile(r'(?:public|internal|private|protected)[^;{=()]*?\b(\w+)\s*(?:<[^>()]*>)?\s*\(')
src=collections.defaultdict(list)
for f in files:
    t=open(f).read()
    for m in attr_re.finditer(t):
        mm=meth_re.search(t,m.end())
        if not mm: continue
        between=t[m.start():mm.start()]
        # preceding attribute block (up to 600 chars back to previous '}' or ';')
        start=max(t.rfind('}',0,m.start()),t.rfind(';',0,m.start()))
        attrs=t[start:mm.start()]
        # #if context: find innermost unclosed #if before position
        stack=[]
        for dm in re.finditer(r'^\s*#(if|elif|else|endif)\b(.*)$',t[:m.start()],re.M):
            k=dm.group(1)
            if k=='if': stack.append(dm.group(2).strip())
            elif k in('elif','else') and stack: stack[-1]=k+' '+dm.group(2).strip()
            elif k=='endif' and stack: stack.pop()
        src[mm.group(1)].append((f,attrs,list(stack)))
r=ET.parse(trx).getroot()
ran=set()
for u in r.findall('.//t:UnitTestResult',ns):
    ran.add(re.sub(r'<.*','',re.sub(r'\(.*','',u.get('testName'))).split('.')[-1])
missing=sorted(set(src)-ran)
print(f'{len(src)} test method names in sources, {len(ran)} in trx, {len(missing)} not executed')
reasons=collections.Counter()
for n in missing:
    f,attrs,stack=src[n][0]
    why=[]
    if 'OuterLoop' in attrs: why.append('OuterLoop')
    if 'ActiveIssue' in attrs: why.append('ActiveIssue')
    if stack: why.append('#if '+' / '.join(stack))
    for k in ('SkipOnTargetFramework','SkipOnPlatform','PlatformSpecific','SkipOnCoreClr','SkipOnMono'):
        if k in attrs: why.append(k)
    reasons[', '.join(why) or 'UNEXPLAINED']+=1
    if not why or os.environ.get('V'): print('  ',n,'|',f,'|',', '.join(why) or 'UNEXPLAINED')
for k,v in reasons.most_common(): print(f'{v:5d}  {k}')
