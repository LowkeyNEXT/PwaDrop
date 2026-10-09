/* GitHub Pages exports static HTML without a client router or image optimizer. */
/* eslint-disable @next/next/no-html-link-for-pages, @next/next/no-img-element */
import type { Metadata } from "next";

export const metadata: Metadata = {
  "title": "Privacy Policy — PWADrop",
  "description": "PWADrop does not collect or send personal information to RiddleNEXT.",
  "alternates": {
    "canonical": "https://lowkeynext.github.io/PwaDrop/privacy/"
  },
  "openGraph": {
    "title": "Privacy Policy — PWADrop",
    "description": "PWADrop does not collect or send personal information to RiddleNEXT.",
    "url": "https://lowkeynext.github.io/PwaDrop/privacy/",
    "images": []
  },
  "twitter": {
    "card": "summary",
    "title": "Privacy Policy — PWADrop",
    "description": "PWADrop does not collect or send personal information to RiddleNEXT.",
    "images": []
  }
};

export default function Privacy() {
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
          <a href="/faq/">
            {"FAQs"}
          </a>
          <a href="https://github.com/LowkeyNEXT/PwaDrop">
            {"Source"}
          </a>
        </div>
        <a className="coming-pill store-pill" href="https://apps.microsoft.com/detail/9nlg3zrf0mm3?hl=en-US&gl=US">
          {"Available now"}
        </a>
      </nav>
      <article className="policy">
        <header className="policy-header">
          <p className="eyebrow">
            <span>
            </span>
            {" Privacy policy"}
          </p>
          <h1>
            {"PWADrop collects nothing."}
          </h1>
          <p>
            {"PWADrop does not collect or send personal information to RiddleNEXT."}
          </p>
          <small>
            {"Effective August 15, 2026"}
          </small>
        </header>
        <div className="policy-summary">
          <div>
            <strong>
              {"No telemetry"}
            </strong>
            <span>
              {"No analytics, advertising, or tracking."}
            </span>
          </div>
          <div>
            <strong>
              {"No account"}
            </strong>
            <span>
              {"No PWADrop or RiddleNEXT account is required."}
            </span>
          </div>
          <div>
            <strong>
              {"No data sales"}
            </strong>
            <span>
              {"We do not sell or share personal information."}
            </span>
          </div>
        </div>
        <section>
          <h2>
            {"Using PWADrop"}
          </h2>
          <p>
            {"PWADrop temporarily accesses only the items you choose to drag so it can complete that action on your device. It does not upload those items or store a copy on RiddleNEXT systems."}
          </p>
          <p>
            {"An app or website you choose to drop an item into may handle it under its own privacy policy."}
          </p>
        </section>
        <section>
          <h2>
            {"Information collection"}
          </h2>
          <p>
            {"PWADrop does not collect, transmit, sell, or share personal information. It has no RiddleNEXT-operated telemetry or online service. Because RiddleNEXT receives no personal information through PWADrop, we have no PWADrop personal information about you to access or delete."}
          </p>
        </section>
        <section>
          <h2>
            {"Microsoft Store"}
          </h2>
          <p>
            {"If you obtain PWADrop through the Microsoft Store, Microsoft independently handles purchases, licensing, and Store activity under the "}
            <a href="https://privacy.microsoft.com/privacystatement">
              {"Microsoft Privacy Statement"}
            </a>
            {"."}
          </p>
        </section>
        <section>
          <h2>
            {"Changes and questions"}
          </h2>
          <p>
            {"We will update this policy if PWADrop’s privacy practices change. Questions can be submitted through the "}
            <a href="https://github.com/LowkeyNEXT/PwaDrop/issues">
              {"PWADrop support tracker"}
            </a>
            {". Please do not post sensitive information in a public issue."}
          </p>
        </section>
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
