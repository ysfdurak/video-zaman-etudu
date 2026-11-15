// Masaüstü sürümün System.Text.Json davranışını izleyen katı JSON okuyucu.
// Neden ayrı: JSON.parse "1000" ile "1000.0"ı ayırt etmez; C# tam sayı alanlarında
// "1000.0" ve "1e3" değerlerini reddeder. İki sürüm aynı paketi aynı şekilde kabul/ret etmeli.

export class JsonError extends Error {}

/** Ayrıştırılmış sayı: ham metni korunur, tür dönüşümü alan türüne göre yapılır. */
export class RawNumber {
  constructor(raw) { this.raw = raw; }
}

/** Ayrıştırılmış nesne: özellik sırası korunur, aynı ad tekrar ederse son değer geçerlidir. */
export class JsonObject {
  constructor() { this.map = new Map(); }
  has(key) { return this.map.has(key); }
  get(key) { return this.map.get(key); }
  entries() { return this.map.entries(); }
}

const MAX_DEPTH = 64;
const NUMBER = /-?(?:0|[1-9]\d*)(?:\.\d+)?(?:[eE][+-]?\d+)?/y;

export function parseStrict(text) {
  let i = 0;
  const fail = () => { throw new JsonError("Etüt paketi eksik veya bozuk."); };
  const ws = () => {
    while (i < text.length) {
      const c = text.charCodeAt(i);
      if (c === 0x20 || c === 0x09 || c === 0x0a || c === 0x0d) i++;
      else break;
    }
  };
  const value = (depth) => {
    if (depth > MAX_DEPTH) fail();
    ws();
    const c = text[i];
    if (c === "{") {
      i++;
      const obj = new JsonObject();
      ws();
      if (text[i] === "}") { i++; return obj; }
      for (;;) {
        ws();
        if (text[i] !== "\"") fail();
        const key = string();
        ws();
        if (text[i] !== ":") fail();
        i++;
        obj.map.set(key, value(depth + 1));
        ws();
        if (text[i] === ",") { i++; continue; }
        if (text[i] === "}") { i++; return obj; }
        fail();
      }
    }
    if (c === "[") {
      i++;
      const arr = [];
      ws();
      if (text[i] === "]") { i++; return arr; }
      for (;;) {
        arr.push(value(depth + 1));
        ws();
        if (text[i] === ",") { i++; continue; }
        if (text[i] === "]") { i++; return arr; }
        fail();
      }
    }
    if (c === "\"") return string();
    if (text.startsWith("true", i)) { i += 4; return true; }
    if (text.startsWith("false", i)) { i += 5; return false; }
    if (text.startsWith("null", i)) { i += 4; return null; }
    NUMBER.lastIndex = i;
    const m = NUMBER.exec(text);
    if (!m) fail();
    i += m[0].length;
    return new RawNumber(m[0]);
  };
  const string = () => {
    i++; // açılış tırnağı
    let out = "";
    for (;;) {
      if (i >= text.length) fail();
      const ch = text[i];
      const code = text.charCodeAt(i);
      if (ch === "\"") { i++; return out; }
      if (code < 0x20) fail();
      if (ch === "\\") {
        const e = text[i + 1];
        i += 2;
        switch (e) {
          case "\"": out += "\""; break;
          case "\\": out += "\\"; break;
          case "/": out += "/"; break;
          case "b": out += "\b"; break;
          case "f": out += "\f"; break;
          case "n": out += "\n"; break;
          case "r": out += "\r"; break;
          case "t": out += "\t"; break;
          case "u": {
            const hex = text.slice(i, i + 4);
            if (!/^[0-9a-fA-F]{4}$/.test(hex)) fail();
            out += String.fromCharCode(parseInt(hex, 16));
            i += 4;
            break;
          }
          default: fail();
        }
        continue;
      }
      out += ch;
      i++;
    }
  };
  const result = value(1);
  ws();
  if (i !== text.length) fail();
  return result;
}

// --- Alan türüne göre okuma (System.Text.Json kurallarıyla) ---

const INTEGER = /^-?(?:0|[1-9]\d*)$/;
const INT32_MIN = -2147483648n, INT32_MAX = 2147483647n;
const INT64_MIN = -9223372036854775808n, INT64_MAX = 9223372036854775807n;

function integer(value, min, max) {
  if (!(value instanceof RawNumber) || !INTEGER.test(value.raw)) throw new JsonError("Etüt paketi eksik veya bozuk.");
  const big = BigInt(value.raw);
  if (big < min || big > max) throw new JsonError("Etüt paketi eksik veya bozuk.");
  return big;
}

/** int alanı. Sayı JavaScript'te tam olarak temsil edilir. */
export const readInt32 = (v) => Number(integer(v, INT32_MIN, INT32_MAX));

/**
 * long alanı. 2^53 üstü değerler BigInt olarak döner; çağıran sınır kontrolünü yapar
 * (zaman damgaları için üst sınır TimeSpan.MaxValue ms, 2^53'ün altında).
 */
export const readInt64 = (v) => {
  const big = integer(v, INT64_MIN, INT64_MAX);
  return big >= -9007199254740991n && big <= 9007199254740991n ? Number(big) : big;
};

export function readDouble(v) {
  if (!(v instanceof RawNumber)) throw new JsonError("Etüt paketi eksik veya bozuk.");
  const n = Number(v.raw);
  if (!Number.isFinite(n)) throw new JsonError("Etüt paketi eksik veya bozuk.");
  return n;
}

export function readBool(v) {
  if (v !== true && v !== false) throw new JsonError("Etüt paketi eksik veya bozuk.");
  return v;
}

/** string alanı: metin veya null. */
export function readString(v) {
  if (v !== null && typeof v !== "string") throw new JsonError("Etüt paketi eksik veya bozuk.");
  return v;
}

/** Nesne alanı: JsonObject veya null. */
export function readObject(v) {
  if (v !== null && !(v instanceof JsonObject)) throw new JsonError("Etüt paketi eksik veya bozuk.");
  return v;
}

/** Dizi alanı: dizi veya null. */
export function readArray(v) {
  if (v !== null && !Array.isArray(v)) throw new JsonError("Etüt paketi eksik veya bozuk.");
  return v;
}
