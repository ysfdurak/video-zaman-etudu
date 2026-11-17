// Çevrimdışı çalışma (sw.js): önbellek sürümü uygulama sürümüyle aynı olmalı ve
// önbellek listesi Web/ altındaki bütün uygulama dosyalarını kapsamalı.
// Böylece yeni bir js dosyası eklenip listeye yazılmazsa çevrimdışı kırılmadan önce test kalır.
import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync, readdirSync, existsSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";
import { VERSION } from "../js/core.js";

const web = join(dirname(fileURLToPath(import.meta.url)), "..");
const sw = readFileSync(join(web, "sw.js"), "utf8");
const files = JSON.parse(sw.match(/const FILES = (\[[\s\S]*?\]);/)[1]);

test("önbellek sürümü uygulama sürümüyle aynı", () => {
  assert.equal(sw.match(/const CACHE = "vze-([^"]+)"/)[1], VERSION);
});

test("önbellek listesi bütün js dosyalarını ve demoları içerir", () => {
  for (const f of readdirSync(join(web, "js"))) assert.ok(files.includes(`js/${f}`), `listede yok: js/${f}`);
  for (const f of readdirSync(join(web, "demo"))) assert.ok(files.includes(`demo/${f}`), `listede yok: demo/${f}`);
  for (const f of ["index.html", "app.css", "404.html"]) assert.ok(files.includes(f), `listede yok: ${f}`);
});

test("listedeki her dosya gerçekten var", () => {
  for (const f of files) if (f !== "./") assert.ok(existsSync(join(web, f)), `dosya yok: ${f}`);
});

test("sw.js dış adrese istek göndermez", () => {
  assert.doesNotMatch(sw, /https?:\/\//, "sw.js içinde tam adres olmamalı");
  assert.match(sw, /url\.origin !== self\.location\.origin/, "yalnız kendi adresine ait istekler işlenmeli");
});
