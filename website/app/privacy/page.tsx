import type { Metadata } from "next";

const title = "Privacy Policy — PWADrop";
const description = "How PWADrop processes files and diagnostic information locally on Windows.";

export const metadata: Metadata = {
  title,
  description,
  openGraph: { title, description, images: [] },
  twitter: { title, description, images: [] },
};

export default function Privacy() {
  return (
    <main>
      <nav className="nav" aria-label="Main navigation">
        <a className="brand" href="/" aria-label="PWADrop home"><img src="/pwadrop-logo.png" alt="" width="34" height="34" /><span>PWADrop</span></a>
        <div className="nav-links"><a href="/">Home</a><a href="/faq">FAQs</a><a href="https://github.com/LowkeyNEXT/PwaDrop">Source</a></div>
        <span className="coming-pill">Coming soon</span>
      </nav>

      <article className="policy">
        <header className="policy-header">
          <p className="eyebrow"><span /> Privacy policy</p>
          <h1>Your files are yours.</h1>
          <p>PWADrop processes selected files locally to complete the drag-and-drop action you request. PWADrop does not send your files, filenames, email contents, browsing activity, or diagnostics to RiddleNEXT.</p>
          <small>Effective August 15, 2026</small>
        </header>

        <div className="policy-summary">
          <div><strong>No account</strong><span>PWADrop does not require a PWADrop or RiddleNEXT account.</span></div>
          <div><strong>No tracking</strong><span>The app contains no advertising, analytics, or behavioral-tracking SDK.</span></div>
          <div><strong>No remote file processing</strong><span>File preparation occurs on the user’s Windows device.</span></div>
        </div>

        <section>
          <h2>Information PWADrop accesses</h2>
          <p>PWADrop accesses information only as needed to perform its user-requested function. This can include files, filenames, email messages, attachments, or other content that the user deliberately drags; Windows process identity and signature information used to determine whether a source application is supported and safe to bridge; and local application settings.</p>
          <p>The destination application or website may independently upload or otherwise process a file after the user drops it there. That processing is controlled by the destination and its own privacy terms, not by PWADrop.</p>
        </section>

        <section>
          <h2>Local temporary files</h2>
          <p>Some source applications do not create a real file until a drop occurs. PWADrop may create or retain a temporary local file so the selected item can be delivered to the destination.</p>
          <ul>
            <li>Compatibility-cache files for completed legacy transfers are normally deleted after approximately 15 minutes.</li>
            <li>Abandoned compatibility-cache sessions become eligible for cleanup after 24 hours and are retried when PWADrop next starts.</li>
            <li>Files materialized through the native drag bridge may remain in the Windows temporary directory until Windows restarts, allowing destination applications time to finish reading them.</li>
          </ul>
          <p>Users can inspect and remove the compatibility cache from PWADrop’s Diagnostics page. Temporary content may also be removed using normal Windows storage-cleanup controls when it is no longer in use.</p>
        </section>

        <section>
          <h2>Diagnostics and settings</h2>
          <p>PWADrop stores settings and redacted diagnostic events under the current user’s local application-data folder. Diagnostic events are limited to technical information such as operation type, file count, duration, drop result, and HRESULT-style error codes. They are designed not to contain filenames, file contents, email subjects, URLs, account identifiers, or browser history.</p>
          <p>Diagnostics remain on the device unless the user chooses to share them for support. PWADrop does not automatically upload diagnostic logs.</p>
        </section>

        <section>
          <h2>Collection, sharing, and sale</h2>
          <p>RiddleNEXT does not collect, receive, sell, rent, or share personal information through PWADrop. The app has no PWADrop telemetry endpoint, cloud account, advertising network, or remote file-processing service.</p>
          <p>Microsoft may process Store purchases, licenses, installations, aggregate usage, and Windows diagnostic information under Microsoft’s own privacy terms. RiddleNEXT may receive aggregated Store reports but does not receive the content of files dragged with PWADrop.</p>
        </section>

        <section>
          <h2>Security</h2>
          <p>PWADrop runs at the signed-in user’s normal integrity level and does not request administrator elevation. Its native bridge is limited to recognized, trusted-signed, same-user source processes and rejects elevated, cross-user, unsigned, and unrecognized processes.</p>
        </section>

        <section>
          <h2>Children</h2>
          <p>PWADrop is a general-purpose Windows productivity utility and is not directed to children. RiddleNEXT does not knowingly collect personal information from children through the app.</p>
        </section>

        <section>
          <h2>Your choices</h2>
          <p>Users can pause the bridge, disable launch at sign-in, open or delete local diagnostics and cache files, exit PWADrop, or uninstall the app through Windows Settings. Uninstalling the app does not automatically remove intentionally retained per-user diagnostics or settings; those can be deleted from the local PWADrop application-data folder.</p>
        </section>

        <section>
          <h2>Changes and contact</h2>
          <p>This policy will be updated if PWADrop’s data practices materially change. The effective date above identifies the current version.</p>
          <p>Privacy questions and requests can be submitted through the <a href="https://github.com/LowkeyNEXT/PwaDrop/issues">PWADrop support tracker</a>. Do not include confidential files, email content, credentials, or other sensitive information in a public issue.</p>
        </section>
      </article>

      <footer className="footer"><a className="brand" href="/"><img src="/pwadrop-logo.png" alt="" width="30" height="30" /><span>PWADrop</span></a><p>Built by RiddleNEXT for Windows.</p><div><a href="/privacy">Privacy</a><a href="/faq">FAQs</a><a href="https://github.com/LowkeyNEXT/PwaDrop">Source</a></div></footer>
    </main>
  );
}
