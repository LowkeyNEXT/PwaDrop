/* GitHub Pages exports static HTML without a client router or image optimizer. */
/* eslint-disable @next/next/no-html-link-for-pages, @next/next/no-img-element */
import type { Metadata } from "next";

export const metadata: Metadata = {
  "title": "Frequently Asked Questions — PWADrop",
  "description": "Answers about PWADrop compatibility, privacy, licensing, and Windows behavior.",
  "alternates": {
    "canonical": "https://lowkeynext.github.io/PwaDrop/faq/"
  },
  "openGraph": {
    "title": "Frequently Asked Questions — PWADrop",
    "description": "Answers about PWADrop compatibility, privacy, licensing, and Windows behavior.",
    "url": "https://lowkeynext.github.io/PwaDrop/faq/",
    "images": []
  },
  "twitter": {
    "card": "summary",
    "title": "Frequently Asked Questions — PWADrop",
    "description": "Answers about PWADrop compatibility, privacy, licensing, and Windows behavior.",
    "images": []
  }
};

export default function FAQ() {
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
          <a href="/">
            {"Home"}
          </a>
          <a href="/privacy/">
            {"Privacy"}
          </a>
          <a href="https://github.com/LowkeyNEXT/PwaDrop">
            {"Source"}
          </a>
        </div>
        <a className="coming-pill store-pill" href="https://apps.microsoft.com/detail/9nlg3zrf0mm3?hl=en-US&gl=US">
          {"Available now"}
        </a>
      </nav>
      <article className="faq-page">
        <header className="policy-header">
          <p className="eyebrow">
            <span>
            </span>
            {" Frequently asked questions"}
          </p>
          <h1>
            {"Straight answers."}
          </h1>
          <p>
            {"What PWADrop does, what it does not do, and what to expect from the Windows app."}
          </p>
        </header>
        <div className="faq-list">
          <details>
            <summary>
              {"What does PWADrop fix?"}
            </summary>
            <p>
              {"Some modern Windows apps create a file only after a destination accepts a drop. Many destinations do not complete that optional Windows negotiation. PWADrop bridges the handoff at the source so the original drag can continue as a normal file drop."}
            </p>
          </details>
          <details>
            <summary>
              {"Does PWADrop upload my files?"}
            </summary>
            <p>
              {"No. PWADrop has no remote file-processing service. Files are prepared locally. If you drop a file into a cloud service or website, that destination may upload it under its own privacy terms."}
            </p>
          </details>
          <details>
            <summary>
              {"Does it require administrator access?"}
            </summary>
            <p>
              {"No. PWADrop runs as the signed-in user and refuses elevated or cross-user source processes."}
            </p>
          </details>
          <details>
            <summary>
              {"Which apps are supported?"}
            </summary>
            <p>
              {"The current build recognizes supported Chromium browsers and PWAs, New Outlook and New Teams through WebView2, plus explicitly recognized Electron apps such as Slack. It can drop into Google Drive, browser upload zones, File Explorer, and desktop apps that accept standard Windows files. Compatibility can still vary by app version and security policy."}
            </p>
          </details>
          <details>
            <summary>
              {"Why purchase it if the source is available?"}
            </summary>
            <p>
              {"The Store edition provides a trusted Microsoft-signed install, automatic updates, simple removal, and a supported release channel. Source access supports transparency, learning, and community contributions."}
            </p>
          </details>
          <details>
            <summary>
              {"Can I build it myself?"}
            </summary>
            <p>
              {"The repository is available for study, contribution, and uses permitted by its source-available license. Building requires a Windows native and .NET development toolchain. Commercial use requires an appropriate paid license."}
            </p>
          </details>
          <details>
            <summary>
              {"Is it open source?"}
            </summary>
            <p>
              {"PWADrop is source-available under the PolyForm Noncommercial License. Because commercial use is restricted, it is not an OSI-approved open-source license."}
            </p>
          </details>
          <details>
            <summary>
              {"How do purchases and updates work?"}
            </summary>
            <p>
              {"The Microsoft Store handles purchases and available Store updates. Check the current listing for pricing and purchase terms."}
            </p>
          </details>
          <details>
            <summary>
              {"Does PWADrop collect telemetry?"}
            </summary>
            <p>
              {"No custom telemetry is included today. Microsoft may provide RiddleNEXT with aggregate Store acquisition, usage, and reliability reports under Microsoft’s privacy terms."}
            </p>
          </details>
          <details>
            <summary>
              {"Can organizations deploy it?"}
            </summary>
            <p>
              {"Review the repository’s enterprise deployment documentation and license before an organizational rollout. Compatibility depends on Windows configuration and security policy."}
            </p>
          </details>
        </div>
      </article>
      <footer className="footer">
        <a href="/" className="brand">
          <img src="/pwadrop-logo.png" alt="" width="30" height="30" />
          <span>
            {"PWADrop"}
          </span>
        </a>
        <p>
          {"Built by RiddleNEXT for Windows."}
        </p>
        <div>
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
