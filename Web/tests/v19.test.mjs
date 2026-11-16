// v1.9: modüller, etüt durumu ve onay, adımları başka etütten alma.
import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { importPackage, exportPackage, toRecord, fromRecord, cleanModules, hasModule, copySteps, newStudy } from "../js/core.js";

const enc = (s) => new TextEncoder().encode(s);
const demo = () => importPackage(new Uint8Array(readFileSync(new URL("../../demo/sentetik-depo-etudu.vzetud", import.meta.url))));

test("modüller: alan yoksa hepsi açık; boş dizi 'yalın araç yok' demektir ve pakette korunur", () => {
  const st = demo();
  assert.equal(st.Modules, null);
  assert.ok(hasModule(st, "Balance") && hasModule(st, "Cards"));
  assert.equal(JSON.parse(exportPackage(st)).Modules, undefined);
  st.Modules = [];
  assert.ok(!hasModule(st, "Work"));
  const root = JSON.parse(exportPackage(st));
  assert.deepEqual(root.Modules, []);
  assert.deepEqual(importPackage(enc(JSON.stringify(root))).Modules, []);
  assert.deepEqual(cleanModules(["Cards", "Bogus", "Balance"]), ["Balance", "Cards"], "bilinen sırada, bilinmeyen atılır");
  assert.equal(cleanModules("Cards"), null);
});

test("durum ve onay: onaylayan/tarih yalnız onaylı etütte yazılır", () => {
  const st = demo();
  st.Status = "approved"; st.ApprovedBy = " A. Kaya "; st.ApprovedAt = "2026-11-16";
  let root = JSON.parse(exportPackage(st));
  assert.equal(root.Status, "approved"); assert.equal(root.ApprovedBy, "A. Kaya"); assert.equal(root.ApprovedAt, "2026-11-16");
  const back = importPackage(enc(JSON.stringify(root)));
  assert.equal(back.Status, "approved"); assert.equal(back.ApprovedBy, "A. Kaya");
  assert.equal(fromRecord(structuredClone(toRecord(back))).ApprovedAt, "2026-11-16");
  st.Status = "done";
  root = JSON.parse(exportPackage(st));
  assert.equal(root.Status, "done"); assert.equal(root.ApprovedBy, undefined, "tamamlandıda onaylayan yazılmaz");
  root.Status = "bitti"; root.ApprovedAt = "dün";
  const bad = importPackage(enc(JSON.stringify(root)));
  assert.equal(bad.Status, ""); assert.equal(bad.ApprovedAt, "");
});

test("adımları başka etütten alma: tanımlar gelir, kare gelmez, ölçüm yoksa da çalışır", () => {
  const src = demo();
  src.Steps = [{ Name: "Toplama", Classification: "VA", Type: "Walk", Note: "kutuya bırakınca", Waste: "Motion", Idea: "rafı yaklaştır", Station: "1", FrameMs: 4000 }];
  const steps = copySteps(src);
  assert.deepEqual(steps, [{ Name: "Toplama", Classification: "VA", Note: "kutuya bırakınca", Type: "Walk", Station: "1", Waste: "Motion", Idea: "rafı yaklaştır" }]);
  const derived = copySteps(demo());
  assert.deepEqual(derived.map((x) => x.Name), ["Hazırlık", "Toplama", "Tarama", "Paketleme", "Bekleme", "Seyrek kontrol"]);
  assert.deepEqual(copySteps(newStudy()), []);
});
