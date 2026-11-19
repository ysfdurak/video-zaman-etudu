// İki gözlemci karşılaştırması (dış inceleme, Kasım 2026): aynı videoyu iki kişi ölçer; fark çıkan adımda başlangıç
// ve bitiş tanımı gösterilir. Tarayıcıya özgü; Windows paket biçimi değişmez.
import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { importPackage, observerAgreement, toRecord, fromRecord, cleanSteps, deriveSteps } from "../js/core.js";

const demo = () => importPackage(new Uint8Array(readFileSync(new URL("../../demo/sentetik-depo-etudu.vzetud", import.meta.url))));
function secondObserver(a, lateMs = 700) {
  const b = fromRecord(toRecord(a)), [first, second] = deriveSteps(a).map((s) => s.Name);
  b.Steps = cleanSteps(deriveSteps(a).map((s) => (s.Name === first ? { ...s, Note: "terminal eline alındığında biter" } : s)));
  b.Observations = a.Observations.map((o) => ({ ...o, StartMs: o.StartMs + (o.Step === second ? lateMs : 0), EndMs: o.EndMs + (o.Step === first ? lateMs : 0) }));
  return { b, first, second };
}

test("aynı ölçüm: bütün adımlar tutarlı, sınır farkı 0", () => {
  const a = demo(), c = observerAgreement(a, fromRecord(toRecord(a)));
  assert.equal(c.Checked, 0);
  assert.equal(c.MeanBoundary, 0);
  assert.ok(c.SameVideo);
  assert.ok(c.Rows.every((r) => r.Status === "ok" && r.Unmatched === 0 && r.DiffPercent === 0));
});

test("B ilk adımı 0,7 sn geç bitiriyor: iki adım işaretlenir, tanımlar ve önceki adım gösterilir", () => {
  const a = demo(), { b, first, second } = secondObserver(a);
  const c = observerAgreement(a, b);
  const r1 = c.Rows.find((r) => r.Step === first), r2 = c.Rows.find((r) => r.Step === second);
  assert.equal(r1.Status, "check");
  assert.deepEqual(r1.Reasons.filter((x) => x !== "duration"), ["end"]);
  assert.ok(Math.abs(r1.EndSigned - 0.7) < 1e-9);
  assert.equal(r1.StartDiff, 0);
  assert.equal(r1.Definition.Prev, null, "ilk adım çevrimin başında başlar");
  assert.equal(r1.Definition.EndA, "");
  assert.equal(r1.Definition.EndB, "terminal eline alındığında biter");
  assert.equal(r1.Definition.Same, false);
  assert.equal(r2.Status, "check");
  assert.ok(r2.Reasons.includes("start"));
  assert.ok(Math.abs(r2.StartSigned - 0.7) < 1e-9);
  assert.equal(r2.Definition.Prev, first, "ikinci adım ilk adımın bitişinde başlar");
  assert.equal(r2.Definition.StartB, "terminal eline alındığında biter");
  assert.equal(c.Checked, 2);
  assert.ok(c.Rows.filter((r) => r.Step !== first && r.Step !== second).every((r) => r.Status === "ok"));
});

test("sınır ayarı: 0,8 sn sınırda başlangıç/bitiş farkı kabul edilir, süre farkı yine işaretlenir", () => {
  const a = demo(), { b, first } = secondObserver(a);
  const r = observerAgreement(a, b, { tolMs: 800 }).Rows.find((x) => x.Step === first);
  assert.ok(!r.Reasons.includes("end"));
  assert.ok(r.Reasons.includes("duration"), "2 sn'lik adımda 0,7 sn %35 süre farkı");
});

test("eşleşmeyen ve hariç tutulan ölçümler", () => {
  const a = demo(), b = fromRecord(toRecord(a)), step = a.Observations[0].Step;
  b.Observations = b.Observations.filter((o, i) => i !== 0);       // B bir ölçümü atlamış
  a.Observations.push({ ...a.Observations[1], Excluded: true, Cycle: 99 }); // hariç tutulan sayılmaz
  const r = observerAgreement(a, b).Rows.find((x) => x.Step === step);
  assert.equal(r.Unmatched, 1);
  assert.ok(r.Reasons.includes("unmatched"));
  assert.equal(r.Pairs, r.CountA - 1);
});

test("farklı video adı uyarısı ve adım yalnız birinde", () => {
  const a = demo(), b = fromRecord(toRecord(a));
  b.ExpectedVideoName = "baska.mp4"; b.Video = "";
  b.Observations.push({ StartMs: 50000, EndMs: 51000, Step: "Yeni adım", Classification: "", Excluded: false, Cycle: 1 });
  const c = observerAgreement(a, b);
  assert.equal(c.SameVideo, false);
  const r = c.Rows.find((x) => x.Step === "Yeni adım");
  assert.deepEqual([r.CountA, r.CountB, r.Pairs, r.Status], [0, 1, 0, "check"]);
  assert.equal(r.Definition.EndA, null, "A'da adım yok");
});
