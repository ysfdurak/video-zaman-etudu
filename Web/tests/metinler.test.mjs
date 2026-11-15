// Desktop/Metinler.json: masaüstü metinleri tarayıcı sözlüğünden üretilir; güncel ve iki dilde eksiksiz mi?
import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { build, serialize, DESKTOP } from "../../Tools/Metinler/uret.mjs";

test("Desktop/Metinler.json tarayıcı sözlüğünden yeniden üretilince aynı (node Tools/Metinler/uret.mjs)", () => {
  assert.equal(readFileSync(new URL("../../Desktop/Metinler.json", import.meta.url), "utf8"), serialize(build()));
});

test("masaüstü metinleri iki dilde aynı anahtarlara sahip", () => {
  assert.deepEqual(Object.keys(DESKTOP.tr).sort(), Object.keys(DESKTOP.en).sort());
  const data = build();
  assert.deepEqual(Object.keys(data.tr), Object.keys(data.en));
});

test("masaüstü kaynak kodundaki metin anahtarlarının hepsi sözlükte var", () => {
  const data = build();
  const files = ["MainWindow.xaml.cs", "MainWindow.Measure.cs", "MainWindow.Analysis.cs", "PrintReport.cs", "StudyExport.cs", "Ui.cs"];
  const used = new Set();
  for (const f of files) {
    const src = readFileSync(new URL("../../Desktop/" + f, import.meta.url), "utf8");
    for (const m of src.matchAll(/(?:L\.T|L\.Plain|Tx|Head|Btn|Error)\("([a-zA-Z0-9_.]+)"/g)) used.add(m[1]);
    for (const m of src.matchAll(/(?:tipKey: |, )"((?:name|title|al|info)\.[a-zA-Z0-9_.]+)"/g)) used.add(m[1]);
  }
  const missing = [...used].filter((k) => !k.endsWith(".") && !(k in data.tr && k in data.en));
  assert.deepEqual(missing, []);
  assert.ok(used.size > 150, String(used.size));
});
