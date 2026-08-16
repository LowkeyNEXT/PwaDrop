import type { Metadata } from "next";

const title = "Frequently Asked Questions — PWADrop";
const description = "Answers about PWADrop compatibility, privacy, licensing, and Windows behavior.";

export const metadata: Metadata = {
  title,
  description,
  openGraph: { title, description, images: [] },
  twitter: { title, description, images: [] },
};

const questions = [
  ["What does PWADrop fix?", "Some modern Windows apps create a file only after a destination accepts a drop. Many destinations do not complete that optional Windows negotiation. PWADrop bridges the handoff at the source so the original drag can continue as a normal file drop."],
  ["Does PWADrop upload my files?", "No. PWADrop has no remote file-processing service. Files are prepared locally. If you drop a file into a cloud service or website, that destination may upload it under its own privacy terms."],
  ["Does it require administrator access?", "No. PWADrop runs as the signed-in user and refuses elevated or cross-user source processes."],
  ["Which apps are supported?", "The current build recognizes supported Chromium browsers and PWAs, New Outlook and New Teams through WebView2, plus explicitly recognized Electron apps such as Slack. It can drop into Google Drive, browser upload zones, File Explorer, and desktop apps that accept standard Windows files. Compatibility can still vary by app version and security policy."],
  ["Why purchase it if the source is available?", "The Store edition provides a trusted Microsoft-signed install, automatic updates, simple removal, and a supported release channel. Source access supports transparency, learning, and community contributions."],
  ["Can I build it myself?", "The repository is available for study, contribution, and uses permitted by its source-available license. Building requires a Windows native and .NET development toolchain. Commercial use requires an appropriate paid license."],
  ["Is it open source?", "PWADrop is source-available under the PolyForm Noncommercial License. Because commercial use is restricted, it is not an OSI-approved open-source license."],
  ["How will paid upgrades work?", "A purchased major version is intended to remain usable perpetually and receive its bug and security fixes. A future major version with substantial new features may be sold separately."],
  ["Does PWADrop collect telemetry?", "No custom telemetry is included today. Microsoft may provide RiddleNEXT with aggregate Store acquisition, usage, and reliability reports under Microsoft’s privacy terms."],
  ["Can organizations deploy it?", "Yes. Business and enterprise licensing and deployment documentation will support per-user and managed-device scenarios, including common Windows management tools."],
];

export default function FAQ() {
  return (
    <main>
      <nav className="nav" aria-label="Main navigation">
        <a className="brand" href="/" aria-label="PWADrop home"><img src="/pwadrop-logo.png" alt="" width="34" height="34" /><span>PWADrop</span></a>
        <div className="nav-links"><a href="/">Home</a><a href="/privacy">Privacy</a><a href="https://github.com/LowkeyNEXT/PwaDrop">Source</a></div>
        <span className="coming-pill">Coming soon</span>
      </nav>

      <article className="faq-page">
        <header className="policy-header">
          <p className="eyebrow"><span /> Frequently asked questions</p>
          <h1>Straight answers.</h1>
          <p>What PWADrop does, what it does not do, and what to expect from the first Windows release.</p>
        </header>
        <div className="faq-list">
          {questions.map(([question, answer]) => <details key={question}><summary>{question}</summary><p>{answer}</p></details>)}
        </div>
      </article>

      <footer className="footer"><a className="brand" href="/"><img src="/pwadrop-logo.png" alt="" width="30" height="30" /><span>PWADrop</span></a><p>Built by RiddleNEXT for Windows.</p><div><a href="/privacy">Privacy</a><a href="/faq">FAQs</a><a href="https://github.com/LowkeyNEXT/PwaDrop">Source</a></div></footer>
    </main>
  );
}
