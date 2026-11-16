// Shared/ek-alanlar.json: masaüstü (C#) ile ortak v2 eşdeğerlik dosyası güncel mi?
import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { build, serialize } from "../../Tools/EkAlanlar/uret.mjs";

test("Shared/ek-alanlar.json tarayıcı çekirdeğinden yeniden üretilince aynı (node Tools/EkAlanlar/uret.mjs)", () => {
  assert.equal(readFileSync(new URL("../../Shared/ek-alanlar.json", import.meta.url), "utf8"), serialize(build()));
});
