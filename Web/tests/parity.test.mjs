// Shared/esdeger-ornekler.json: beklenen değerler C# Core'dan üretildi.
// Bu test geçiyorsa tarayıcı ve masaüstü sürümü aynı girdiden aynı sonucu verir.
import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { analyzeSteps, analyzeCycles, studentInv, importPackage, newStudy, PackageError } from "../js/core.js";

const fixture = JSON.parse(readFileSync(new URL("../../Shared/esdeger-ornekler.json", import.meta.url), "utf8"));
const same = (a, e, label) => {
  if (a === null || e === null) return assert.equal(a, e, label);
  assert.ok(Math.abs(a - e) <= 1e-9 * Math.max(1, Math.abs(e)), `${label}: ${a} ≠ ${e}`);
};

for (const c of fixture.Analysis) {
  test(`hesap eşdeğerliği: ${c.Name}`, () => {
    const study = newStudy({ Name: "E", Code: "K", Settings: c.Input.Settings, Observations: c.Input.Observations,
      Frequency: new Map(Object.entries(c.Input.Frequency)) });
    const steps = new Map(analyzeSteps(study).map((r) => [r.Step, r]));
    assert.equal(steps.size, c.Steps.length, "adım sayısı");
    for (const e of c.Steps) {
      const r = steps.get(e.Step);
      assert.ok(r, `adım yok: ${e.Step}`);
      for (const f of ["Mean", "Median", "Min", "Max", "Deviation", "CriticalT", "ErrorPercent", "Base", "Normal", "Standard", "Contribution"])
        same(r[f], e[f], `${e.Step}/${f}`);
      for (const f of ["Count", "Excluded", "Every", "Sufficiency", "Method"]) assert.equal(r[f], e[f], `${e.Step}/${f}`);
      assert.equal(r.Groups.length, e.Groups.length, `${e.Step}/grup sayısı`);
      r.Groups.forEach((g, i) => { assert.equal(g.Count, e.Groups[i].Count); same(g.Seconds, e.Groups[i].Seconds, `${e.Step}/grup ${i}`); });
    }
    const cycles = analyzeCycles(study);
    assert.equal(cycles.length, c.Cycles.length, "çevrim sayısı");
    cycles.forEach((x, i) => {
      assert.equal(x.Cycle, c.Cycles[i].Cycle);
      same(x.Elapsed, c.Cycles[i].Elapsed, "geçen süre");
      same(x.Workload, c.Cycles[i].Workload, "iş yükü");
    });
  });
}

test("t tablosu eşdeğerliği (175 değer)", () => {
  for (const x of fixture.TTable) same(studentInv(x.Df, (1 + x.Confidence / 100) / 2), x.T, `%${x.Confidence} sd ${x.Df}`);
});

for (const p of fixture.Packages) {
  test(`paket ${p.Accept ? "kabul" : "ret"}: ${p.Name}`, () => {
    const bytes = new Uint8Array(Buffer.from(p.Base64, "base64"));
    if (!p.Accept) return assert.throws(() => importPackage(bytes), PackageError);
    const s = importPackage(bytes);
    const e = p.Expected;
    assert.equal(s.Name, e.Name); assert.equal(s.Code, e.Code); assert.equal(s.ExpectedVideoName, e.ExpectedVideoName);
    assert.deepEqual(s.Settings, e.Settings);
    assert.deepEqual(s.Observations, e.Observations);
    assert.deepEqual(Object.fromEntries(s.Frequency), e.Frequency);
  });
}
