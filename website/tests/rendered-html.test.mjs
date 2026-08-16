import assert from "node:assert/strict";
import test from "node:test";

async function render(path = "/") {
  const workerUrl = new URL("../dist/server/index.js", import.meta.url);
  workerUrl.searchParams.set("test", `${process.pid}-${Date.now()}`);
  const { default: worker } = await import(workerUrl.href);

  return worker.fetch(
    new Request(`http://localhost${path}`, { headers: { accept: "text/html" } }),
    { ASSETS: { fetch: async () => new Response("Not found", { status: 404 }) } },
    { waitUntil() {}, passThroughOnException() {} },
  );
}

test("server-renders the PWADrop landing page", async () => {
  const response = await render();
  assert.equal(response.status, 200);
  assert.match(response.headers.get("content-type") ?? "", /^text\/html\b/i);

  const html = await response.text();
  assert.match(html, /<title>PWADrop — Drag files from modern apps, drop them anywhere<\/title>/i);
  assert.match(html, /Drag files from modern apps\. Drop them anywhere\./);
  assert.match(html, /No cloud account/);
  assert.match(html, /pwadrop-settings\.png/);
  assert.match(html, /Supported source families/);
  assert.match(html, /Google Drive/);
  assert.match(html, /No destination plug-in/);
  assert.doesNotMatch(html, /codex-preview|react-loading-skeleton/i);
});

test("privacy route has a concise no-collection policy and route-specific metadata", async () => {
  const response = await render("/privacy");
  const html = await response.text();
  assert.equal(response.status, 200);
  assert.match(html, /<title>Privacy Policy — PWADrop<\/title>/i);
  assert.match(html, /Effective August 15, 2026/);
  assert.match(html, /does not collect or send personal information/i);
  assert.match(html, /items you choose to drag/i);
  assert.doesNotMatch(html, /HRESULT|process identity|temporary directory|compatibility-cache/i);
  assert.doesNotMatch(html, /og\.png/);
});

test("FAQ route explains source and commercial licensing", async () => {
  const response = await render("/faq");
  const html = await response.text();
  assert.equal(response.status, 200);
  assert.match(html, /<title>Frequently Asked Questions — PWADrop<\/title>/i);
  assert.match(html, /Why purchase it if the source is available/);
  assert.match(html, /Commercial use requires an appropriate paid license/);
  assert.doesNotMatch(html, /og\.png/);
});
