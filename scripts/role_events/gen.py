# Regenerates the Intelligence and Military event tables and thought defs from spec.py (the one place the wording lives).
# Usage: python scripts/role_events/gen.py   (then run the tests: they check every fact has its thought)
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from spec import INTEL, MIL
from xml.sax.saxutils import escape
ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))

def check(spec):
    for role, d in spec.items():
        assert len(d["P"]) == 5 and len(d["N"]) == 5 and len(d["S"]) == 5, role
        facts = [f for k in "PNS" for f, _, _ in d[k]]
        assert len(set(facts)) == 15 and all(f.isalnum() for f in facts), (role, facts)

def thought_name(prefix, role, k, i): return "WFL_%s_%s_%s%d" % (prefix, role, k, i + 1)

def xml(spec, prefix):
    out = ['<?xml version="1.0" encoding="utf-8"?><Defs>']
    for role, d in spec.items():
        out.append("<!-- %s -->" % role)
        for k in "PNS":
            for i, (fact, label, desc) in enumerate(d[k]):
                name = thought_name(prefix, role, k, i)
                if k == "S":
                    op = 3 if i < 3 else (-3 if i == 3 else -4)
                    out.append('<ThoughtDef><defName>%s</defName><thoughtClass>Thought_MemorySocial</thoughtClass><durationDays>2</durationDays><stackLimit>1</stackLimit><stages><li><label>%s</label><description>%s</description><baseOpinionOffset>%d</baseOpinionOffset></li></stages></ThoughtDef>' % (name, escape(label), escape(desc), op))
                else:
                    mood = (2 + i % 2) if k == "P" else -(2 + i % 2)
                    out.append('<ThoughtDef><defName>%s</defName><durationDays>2</durationDays><stackLimit>1</stackLimit><stages><li><label>%s</label><description>%s</description><baseMoodEffect>%d</baseMoodEffect></li></stages></ThoughtDef>' % (name, escape(label), escape(desc), mood))
    out.append("</Defs>")
    return "\n".join(out) + "\n"

def cs(spec, prefix, cls, evt, header):
    roles = ", ".join('"%s"' % r for r in spec)
    lines = ["using System.Collections.Generic;", "using System.Linq;", "", "namespace FamilyLife.Domain", "{", "    public sealed class %s" % evt, "    {",
             "        public string Id, Role, Fact, Thought; public int Mood, Opinion;", "        public bool Matches(string role, string fact, bool other) => Role == role && Fact == fact && (Opinion == 0 || other);", "    }", "",
             "    /// <summary>" + header + "</summary>", "    public static class %s" % cls, "    {",
             "        public static readonly string[] Roles = { %s };" % roles,
             "        /// <summary>Facts for each role, in trigger order. Positive: routine success, outstanding result, milestone, recognised by others, pride. Negative: failed work, waste or setback, strain, rejected, danger or harm. Social: praised, helped, shared know-how, dispute, betrayal or stolen credit.</summary>",
             "        private static readonly Dictionary<string, string[][]> facts = new Dictionary<string, string[][]>", "        {"]
    for role, d in spec.items():
        rows = ", ".join("new[] { %s }" % ", ".join('"%s"' % f for f, _, _ in d[k]) for k in "PNS")
        lines.append('            { "%s", new[] { %s } },' % (role, rows))
    lines += ["        };", "        public static string Fact(string role, int category, int index) => facts.TryGetValue(role, out var f) && category >= 0 && category < 3 && index >= 0 && index < 5 ? f[category][index] : null;",
              "        public const int Positive = 0, Negative = 1, Social = 2;",
              "        public static string Thought(string role, int category, int index) => \"WFL_%s_\" + role + \"_\" + \"PNS\"[category] + (index + 1);" % prefix,
              "        public static readonly %s[] All = Build().ToArray();" % evt,
              "        private static IEnumerable<%s> Build()" % evt, "        {", "            foreach (var role in Roles)", "                for (int category = 0; category < 3; category++)", "                    for (int i = 0; i < 5; i++)",
              "                        yield return new %s { Id = role + new[] { \"Positive\", \"Negative\", \"Social\" }[category] + i, Role = role, Fact = Fact(role, category, i), Thought = Thought(role, category, i), Mood = category == 0 ? 2 + i %% 2 : category == 1 ? -2 - i %% 2 : 0, Opinion = category == 2 ? (i < 3 ? 3 : i == 3 ? -3 : -4) : 0 };" % evt,
              "        }", "        public static IEnumerable<%s> Eligible(string role, string fact, bool other) => All.Where(e => e.Matches(role, fact, other));" % evt, "    }", "}"]
    return "\n".join(lines) + "\n"

def lang(spec, prefix):
    return ""

check(INTEL); check(MIL)
w = lambda p, t: (os.makedirs(os.path.dirname(p), exist_ok=True), open(p, "w", encoding="utf-8").write(t))
w(ROOT + "/Packages/WilsIntelligence/Defs/IntelligenceThoughts.xml", xml(INTEL, "I"))
w(ROOT + "/Packages/WilsMilitary/Defs/MilitaryThoughts.xml", xml(MIL, "M"))
w(ROOT + "/Source/Intelligence/Domain/IntelligenceEvents.cs", cs(INTEL, "I", "IntelligenceEvents", "IntelligenceEvent", "Every Intelligence role has five positive, five negative and five relationship events; the thought for each is in the package's Defs. Generated from one table so the facts and the thoughts cannot drift apart."))
w(ROOT + "/Source/Military/Domain/MilitaryEvents.cs", cs(MIL, "M", "MilitaryEvents", "MilitaryEvent", "Every Military role has five positive, five negative and five relationship events; the thought for each is in the package's Defs. Generated from one table so the facts and the thoughts cannot drift apart."))
print("ok", sum(len(d["P"]) + len(d["N"]) + len(d["S"]) for d in INTEL.values()), sum(len(d["P"]) + len(d["N"]) + len(d["S"]) for d in MIL.values()))
