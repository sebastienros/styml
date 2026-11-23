# M4p: the string sets of the old-against-new quoting comparison: the strings of
# BlockQuotingSpecPatternTests (short, keywords, timestamps), Psych's timestamp forms, and the lenient and resolver forms.
#   python3 -I bench/quote-dump-strings.py artifacts/quote-dump
import json, itertools, sys
out = sys.argv[1]
A = "018+-._:xboeE~<="
short = []
cur = [""]
for n in range(1, 6):
    cur = [p + c for p in cur for c in A]
    short += cur
def cases(w):
    for mask in range(1 << len(w)):
        yield "".join(ch.upper() if mask & (1 << i) else ch for i, ch in enumerate(w))
kw = set()
for w in ["y","n","yes","no","on","off","true","false","null","inf","nan","infinity","ye","nul","tru","of","yess","nulll"]:
    for c in cases(w):
        for p in ["", ".", "+.", "-.", "+", "-", "~"]:
            kw.add(p + c)
kw |= {"", "~", "~~", "<<", "<<<", "<", "=="}
ts = []
for y in ["2001","200","20011"]:
  for m in ["12","1","123",""]:
    for d in ["14","4","141"]:
      date = f"{y}-{m}-{d}"; ts += [date, "-" + date, f"{y}/{m}/{d}"]
      for sep, h, mi, s, f, z in itertools.product(["T","t"," ","  ","\t"," \t","","x"],["21","1","213"],["59","5"],["43","4","433"],["",".",".10",".1x"],["","Z"," Z","\tZ","z","+5","-05","+05:00","-5:00","+0500"," -5","+5:0","+123","+"]):
        ts.append(date + sep + h + ":" + mi + ":" + s + f + z)
psych = [p + t for t in ts for p in ("-", "+")]
for z in ["+0500","-530","+050","+5:","+05:","-05:0","+05:000","+05000"," +0500","\t-5:","+"," ","Z ","\v+05","\f-05:00"]:
    psych += ["2001-12-14 21:59:43" + z, "-2001-1-4t1:59:43.5" + z, "2001-12-14\r21:59:43" + z]
# The end- and middle-decided lenient forms (condition 2) and the resolver's ruling-out forms (condition 3).
extra = []
for core in ["1", "12", "true", "TrUe", "null", "NuLL", ".nan", "-.iNf", "1e3", "0x1F", "0o17", "1_000", "2001-12-14", "2001-12-14T21:59:43Z", "a", "a *b", "a &b", "a !b", "~"]:
    for f in ["{}", " {}", "{} ", " {} ", "\t{}", "{}\t", "{}\n", "\n{}", "{} # c", "a {}", "{} a", "- {}", "{}:", ": {}", "'{}'", "\"{}\"", "[{}]", "{{{}}}"]:
        extra.append(f.replace("{}", core, 1) if f.count("{}") == 1 else f.format(core))
extra += ["1e3", "1E3", "1e+3", "1.e3", "0x1F", "0X1F", "0o17", "0O17", "0b11", "1_000", "1__0", "_1", "1_", "T", "t", "Z", "z", "1T", "1t", "1Z", "12:30", "1:2:3", "190:20:30", "+12", "-0", "+.5", ".5", "5.", "0.", "00", "007", "1e", "e1", "x1", "0x", "0o", "0b"]
# Non-ASCII forms (M4p, candidate b's resume): at each edge and after the ASCII the one scan reads, with the YAML set's
# characters (NEL, LS, PS, BOM, U+FFFE), and with an escape after them.
for core in ["é", "日本", "\u0085", "\u2028", "\u2029", "\ufeff", "\ufffe", "\u00a0", "😀", "Reykjavík"]:
    for f in ["{}", "a{}", "{}a", "a: {}", "{}: a", "a #{}", "- {}", "{} ", " {}", "{}\"", "{}\\", "\"{}", "a\n{}", "true{}", "1{}"]:
        extra.append(f.replace("{}", core))
for name, data in [("short", short), ("keywords", sorted(kw)), ("timestamps", ts), ("psych", psych), ("extra", extra)]:
    json.dump(data, open(f"{out}/{name}.json", "w"))
    print(name, len(data))
