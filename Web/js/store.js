// Desktop/StudyStore.cs karşılığı: etütler bu tarayıcının IndexedDB deposunda saklanır.
// Sunucuya hiçbir şey gönderilmez. Video dosyası saklanmaz; desteklenen tarayıcılarda
// (Chrome/Edge) yalnızca dosyaya erişim tutamacı saklanır, böylece video yeniden bağlanabilir.
import { toRecord, fromRecord } from "./core.js";

const DB_NAME = "VideoZamanEtudu";
const STUDIES = "studies";
const HANDLES = "videoHandles";

function request(req) {
  return new Promise((resolve, reject) => {
    req.onsuccess = () => resolve(req.result);
    req.onerror = () => reject(req.error);
  });
}

export class StudyStore {
  #db = null;
  /** Son readAll çağrısında okunamayıp atlanan kayıt sayısı. */
  lastSkipped = 0;

  async open() {
    if (this.#db) return this.#db;
    if (!globalThis.indexedDB)
      throw Object.assign(new Error("Bu tarayıcı yerel kayıt (IndexedDB) desteklemiyor. Gizli pencere kullanıyorsanız normal pencerede açın."), { key: "store.unsupported" });
    const open = indexedDB.open(DB_NAME, 1);
    open.onupgradeneeded = () => {
      const db = open.result;
      if (!db.objectStoreNames.contains(STUDIES)) db.createObjectStore(STUDIES, { keyPath: "Id" });
      if (!db.objectStoreNames.contains(HANDLES)) db.createObjectStore(HANDLES);
    };
    this.#db = await request(open);
    return this.#db;
  }

  async #store(name, mode) {
    return (await this.open()).transaction(name, mode).objectStore(name);
  }

  async readAll() {
    const records = await request((await this.#store(STUDIES, "readonly")).getAll());
    const studies = [];
    let skipped = 0;
    for (const record of records) {
      // Tek bir bozuk kayıt listeyi engellemesin.
      try { studies.push(fromRecord(record)); } catch { skipped++; }
    }
    this.lastSkipped = skipped;
    return studies.sort((a, b) => (a.Updated < b.Updated ? 1 : a.Updated > b.Updated ? -1 : 0));
  }

  async save(study) {
    await request((await this.#store(STUDIES, "readwrite")).put(toRecord(study)));
  }

  async delete(id) {
    await request((await this.#store(STUDIES, "readwrite")).delete(id));
    await request((await this.#store(HANDLES, "readwrite")).delete(id));
  }

  async saveHandle(id, handle) {
    try { await request((await this.#store(HANDLES, "readwrite")).put(handle, id)); } catch { /* tutamaç saklanamazsa video elle bağlanır */ }
  }

  async readHandle(id) {
    try { return (await request((await this.#store(HANDLES, "readonly")).get(id))) ?? null; } catch { return null; }
  }
}
