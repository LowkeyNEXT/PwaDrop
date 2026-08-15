import { cp, mkdir, rm, writeFile } from "node:fs/promises";
import { fileURLToPath, pathToFileURL } from "node:url";

const outputRoot = new URL("../github-pages/", import.meta.url);
const clientRoot = new URL("../dist/client/", import.meta.url);
const basePath = "/PwaDrop";
const publicOrigin = "https://lowkeynext.github.io";

await rm(outputRoot, { recursive: true, force: true });
await mkdir(outputRoot, { recursive: true });
await cp(clientRoot, outputRoot, { recursive: true });

const workerUrl = pathToFileURL(fileURLToPath(new URL("../dist/server/index.js", import.meta.url)));
workerUrl.searchParams.set("export", Date.now().toString());
const { default: worker } = await import(workerUrl.href);

const routes = [
  { path: "/", destination: new URL("index.html", outputRoot) },
  { path: "/privacy", destination: new URL("privacy/index.html", outputRoot) },
  { path: "/faq", destination: new URL("faq/index.html", outputRoot) },
];

function makeStatic(html) {
  return html
    .replace(/<script\b[^>]*>[\s\S]*?<\/script>/gi, "")
    .replaceAll('href="/', `href="${basePath}/`)
    .replaceAll('src="/', `src="${basePath}/`)
    .replaceAll('content="http://localhost/og.png"', `content="${publicOrigin}${basePath}/og.png"`)
    .replaceAll('content="http://localhost:3000/og.png"', `content="${publicOrigin}${basePath}/og.png"`);
}

for (const route of routes) {
  const response = await worker.fetch(
    new Request(`http://localhost${route.path}`, { headers: { accept: "text/html" } }),
    { ASSETS: { fetch: async () => new Response("Not found", { status: 404 }) } },
    { waitUntil() {}, passThroughOnException() {} },
  );
  if (!response.ok) throw new Error(`Unable to export ${route.path}: ${response.status}`);
  await mkdir(new URL("./", route.destination), { recursive: true });
  await writeFile(route.destination, makeStatic(await response.text()), "utf8");
}

await writeFile(new URL(".nojekyll", outputRoot), "", "utf8");
await writeFile(
  new URL("robots.txt", outputRoot),
  `User-agent: *\nAllow: /\nSitemap: ${publicOrigin}${basePath}/sitemap.xml\n`,
  "utf8",
);
await writeFile(
  new URL("sitemap.xml", outputRoot),
  `<?xml version="1.0" encoding="UTF-8"?>\n<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9"><url><loc>${publicOrigin}${basePath}/</loc></url><url><loc>${publicOrigin}${basePath}/privacy/</loc></url><url><loc>${publicOrigin}${basePath}/faq/</loc></url></urlset>\n`,
  "utf8",
);

await cp(new URL("index.html", outputRoot), new URL("404.html", outputRoot));
console.log(`Exported ${routes.length} routes to ${fileURLToPath(outputRoot)}`);
