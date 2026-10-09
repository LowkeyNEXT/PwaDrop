/* GitHub Pages exports static HTML without a client router or image optimizer. */
/* eslint-disable @next/next/no-html-link-for-pages, @next/next/no-img-element */
import type { Metadata } from "next";

export const metadata: Metadata = {
  "title": "PWADrop — Windows attachment drag bridge",
  "description": "Available on the Microsoft Store. PWADrop bridges delayed attachment drags for destinations that accept ordinary Windows file drops.",
  "alternates": {
    "canonical": "https://lowkeynext.github.io/PwaDrop/"
  },
  "openGraph": {
    "title": "PWADrop — Windows attachment drag bridge",
    "description": "Available on the Microsoft Store. PWADrop bridges delayed attachment drags for destinations that accept ordinary Windows file drops.",
    "url": "https://lowkeynext.github.io/PwaDrop/",
    "images": [
      {
        "url": "https://lowkeynext.github.io/PwaDrop/art/pwadrop-file-handoff.webp?v=13",
        "width": 1536,
        "height": 1024,
        "alt": "Files crossing a blue glass bridge from a browser portal to a desktop folder"
      }
    ],
    "siteName": "PWADrop",
    "type": "website",
    "locale": "en_US"
  },
  "twitter": {
    "card": "summary_large_image",
    "title": "PWADrop — Windows attachment drag bridge",
    "description": "Available on the Microsoft Store. PWADrop bridges delayed attachment drags for destinations that accept ordinary Windows file drops.",
    "images": [
      "https://lowkeynext.github.io/PwaDrop/art/pwadrop-file-handoff.webp?v=13"
    ]
  }
};

export default function Home() {
  return (
    <>
    <a className="skip-link" href="#main-content">
      {"Skip to content"}
    </a>
    <main id="main-content">
      <nav className="nav" aria-label="Main navigation">
        <a href="/" className="brand" aria-label="PWADrop home">
          <img src="/pwadrop-logo.png" alt="" width="34" height="34" />
          <span>
            {"PWADrop"}
          </span>
        </a>
        <div className="nav-links">
          <a href="#how-it-works">
            {"How it works"}
          </a>
          <a href="#compatibility">
            {"Compatibility"}
          </a>
          <a href="/privacy/">
            {"Privacy"}
          </a>
        </div>
        <a className="coming-pill store-pill" href="https://apps.microsoft.com/detail/9nlg3zrf0mm3?hl=en-US&gl=US">
          {"Available now"}
        </a>
      </nav>
      <section className="hero">
        <div className="hero-copy">
          <p className="eyebrow">
            <span>
            </span>
            {" A missing Windows bridge"}
          </p>
          <h1>
            {"Drag attachments. Bridge the handoff."}
          </h1>
          <p className="hero-lede">
            {"PWADrop turns delayed file drags from modern Windows apps into the ordinary file drops for destinations that accept Windows files."}
          </p>
          <div className="hero-actions">
            <a className="primary-action" href="https://apps.microsoft.com/detail/9nlg3zrf0mm3?hl=en-US&gl=US">
              {"Get PWADrop · $2.99"}
            </a>
            <a className="secondary-action" href="#how-it-works">
              {"See how it works "}
              <span aria-hidden="true">
                {"↓"}
              </span>
            </a>
          </div>
          <p className="purchase-note">
            {"US Store price · Windows 11 22H2 or later · x64. Check the Store for current regional pricing."}
          </p>
          <div className="trust-row" aria-label="Product qualities">
            <span>
              {"Runs as you"}
            </span>
            <span>
              {"No cloud account"}
            </span>
            <span>
              {"No tracking"}
            </span>
          </div>
        </div>
        <figure className="marketing-figure pwadrop-marketing">
          <div className="marketing-artwork">
            <img src="/art/pwadrop-file-handoff.webp?v=13" alt="Original 3D artwork of files gliding from a browser-shaped portal along a blue glass bridge into a desktop folder, with a drag cursor beside the leading file" width="1536" height="1024" fetchPriority="high" decoding="async" />
          </div>
          <figcaption>
            <div className="artwork-signature">
              <img src="/pwadrop-logo.png" alt="" width="44" height="44" />
              <div>
                <strong>
                  {"A smoother way from here to there."}
                </strong>
                <span>
                  {"From a supported app to a standard file destination."}
                </span>
              </div>
            </div>
            <p>
              {"Compatibility varies by source and destination."}
            </p>
          </figcaption>
        </figure>
      </section>
      <section className="signal-strip" aria-label="PWADrop summary">
        <div>
          <strong>
            {"One tiny utility"}
          </strong>
          <span>
            {"Quietly lives in your notification area"}
          </span>
        </div>
        <div>
          <strong>
            {"One natural gesture"}
          </strong>
          <span>
            {"Keep using drag and drop the way you expect"}
          </span>
        </div>
        <div>
          <strong>
            {"Zero uploads"}
          </strong>
          <span>
            {"Your files stay between your apps and your PC"}
          </span>
        </div>
      </section>
      <section className="how" id="how-it-works">
        <div className="section-heading">
          <p className="eyebrow">
            <span>
            </span>
            {" How it works"}
          </p>
          <h2>
            {"The drop target never has to know."}
          </h2>
          <p>
            {"PWADrop handles the delayed Windows file handoff at the source, then lets the original drag continue normally."}
          </p>
        </div>
        <ol className="steps">
          <li>
            <span>
              {"01"}
            </span>
            <h3>
              {"You start a drag"}
            </h3>
            <p>
              {"Drag an email, attachment, or file from a supported modern Windows app."}
            </p>
          </li>
          <li>
            <span>
              {"02"}
            </span>
            <h3>
              {"PWADrop bridges it"}
            </h3>
            <p>
              {"The selected item is prepared locally using standard Windows drag-and-drop semantics."}
            </p>
          </li>
          <li>
            <span>
              {"03"}
            </span>
            <h3>
              {"Your destination receives it"}
            </h3>
            <p>
              {"Drop into a browser, desktop app, upload field, or other ordinary file target."}
            </p>
          </li>
        </ol>
      </section>
      <section className="compatibility" id="compatibility">
        <div className="section-heading">
          <p className="eyebrow">
            <span>
            </span>
            {" Compatibility"}
          </p>
          <h2>
            {"One bridge for the apps you already use."}
          </h2>
          <p>
            {"The current Windows build recognizes common Chromium, WebView2, and Electron source families, then hands the drag to destinations that accept normal Windows files. Compatibility varies by app version, item type, and destination."}
          </p>
        </div>
        <div className="compat-board">
          <article className="compat-column">
            <div className="compat-column-heading">
              <div>
                <p className="card-kicker">
                  {"Drag from"}
                </p>
                <h3>
                  {"Supported source families"}
                </h3>
              </div>
            </div>
            <div className="app-group">
              <strong>
                {"Browsers "}
                &amp;
                {" installed web apps"}
              </strong>
              <p>
                {"Edge, Chrome, Brave, Chromium, Opera, Vivaldi, and compatible PWAs running through them."}
              </p>
            </div>
            <div className="app-group">
              <strong>
                {"WebView2 apps"}
              </strong>
              <p>
                {"New Outlook and New Teams, including files and messages exposed through their Windows drag objects."}
              </p>
            </div>
            <div className="app-group">
              <strong>
                {"Recognized Electron apps"}
              </strong>
              <p>
                {"Slack, Missive, Superhuman, and other explicitly supported, trusted application roots."}
              </p>
            </div>
          </article>
          <div className="compat-bridge" aria-hidden="true">
            <img src="/pwadrop-logo.png" alt="" width="48" height="48" loading="lazy" />
          </div>
          <article className="compat-column">
            <div className="compat-column-heading">
              <div>
                <p className="card-kicker">
                  {"Drop into"}
                </p>
                <h3>
                  {"Standard file destinations"}
                </h3>
              </div>
            </div>
            <div className="app-group">
              <strong>
                {"Browser upload surfaces"}
              </strong>
              <p>
                {"Google Drive, cloud storage, ticketing tools, compose windows, and sites that accept ordinary file drops."}
              </p>
            </div>
            <div className="app-group">
              <strong>
                {"Windows desktop apps"}
              </strong>
              <p>
                {"File Explorer, WinForms, WPF, creative tools, and other applications that accept standard file paths."}
              </p>
            </div>
            <div className="app-group">
              <strong>
                {"No destination plug-in"}
              </strong>
              <p>
                {"The target receives a normal Windows file drop, so it does not need to know PWADrop exists."}
              </p>
            </div>
          </article>
        </div>
        <aside className="compat-note">
          <strong>
            {"Windows 11 22H2 or later, x64. Normal user sessions."}
          </strong>
          <span>
            {"Elevated and cross-user source apps are unsupported. ARM64, security-restricted enterprise environments, and unknown Electron executables require separate validation. App updates can also change drag behavior."}
          </span>
          <a href="/faq/">
            {"Read compatibility FAQs "}
            <span aria-hidden="true">
              {"→"}
            </span>
          </a>
        </aside>
      </section>
      <section className="demo-section" id="demos">
        <div>
          <p className="eyebrow">
            <span>
            </span>
            {" Getting started"}
          </p>
          <h2>
            {"Check the handoff. Keep your gesture."}
          </h2>
          <p>
            {"If an attachment drops into File Explorer but fails in another app, PWADrop may bridge that file handoff. Check the compatibility notes for your setup."}
          </p>
        </div>
        <div className="setup-card">
          <h3>
            {"For a useful support report"}
          </h3>
          <p>
            {"Include your Windows version, source app and version, item type, and destination. Keep private file contents out of public reports."}
          </p>
          <a className="secondary-action" href="https://github.com/LowkeyNEXT/PwaDrop/issues">
            {"Open the support tracker "}
            <span aria-hidden="true">
              {"→"}
            </span>
          </a>
        </div>
      </section>
      <section className="faq-callout">
        <p className="eyebrow">
          <span>
          </span>
          {" Good questions"}
        </p>
        <div>
          <h2>
            {"Private by design. Practical by default."}
          </h2>
          <a href="/faq/" className="secondary-action">
            {"Read the FAQs "}
            <span aria-hidden="true">
              {"→"}
            </span>
          </a>
        </div>
      </section>
      <footer className="footer">
        <a href="/" className="brand" aria-label="PWADrop home">
          <img src="/pwadrop-logo.png" alt="" width="30" height="30" />
          <span>
            {"PWADrop"}
          </span>
        </a>
        <p>
          {"Built by RiddleNEXT for Windows."}
        </p>
        <div>
          <a href="https://riddlenext.com/">
            {"RiddleNEXT"}
          </a>
          <a href="/privacy/">
            {"Privacy"}
          </a>
          <a href="/faq/">
            {"FAQs"}
          </a>
          <a href="https://github.com/LowkeyNEXT/PwaDrop">
            {"Source"}
          </a>
        </div>
      </footer>
    </main>
    </>
  );
}
