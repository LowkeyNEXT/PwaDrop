export default function Home() {
  return (
    <main>
      <nav className="nav" aria-label="Main navigation">
        <a className="brand" href="/" aria-label="PWADrop home">
          <img src="/pwadrop-logo.png" alt="" width="34" height="34" />
          <span>PWADrop</span>
        </a>
        <div className="nav-links">
          <a href="#how-it-works">How it works</a>
          <a href="#compatibility">Compatibility</a>
          <a href="/privacy">Privacy</a>
        </div>
        <span className="coming-pill">Coming soon</span>
      </nav>

      <section className="hero">
        <div className="hero-copy">
          <p className="eyebrow"><span /> A missing Windows bridge</p>
          <h1>Drag files from modern apps. Drop them anywhere.</h1>
          <p className="hero-lede">
            PWADrop turns delayed file drags from modern Windows apps into the
            ordinary file drops your destination already understands.
          </p>
          <div className="hero-actions">
            <span className="primary-action" aria-disabled="true">Microsoft Store · Coming soon</span>
            <a className="secondary-action" href="#how-it-works">See how it works <span aria-hidden="true">↓</span></a>
          </div>
          <div className="trust-row" aria-label="Product qualities">
            <span>Runs as you</span>
            <span>No cloud account</span>
            <span>No tracking</span>
          </div>
        </div>

        <div className="product-frame" aria-label="PWADrop application preview">
          <div className="frame-glow" />
          <img src="/pwadrop-settings.png" alt="PWADrop settings showing an active drag bridge" />
          <div className="status-card">
            <span className="status-dot" />
            <div><strong>Bridge active</strong><small>Ready when you drag</small></div>
          </div>
        </div>
      </section>

      <section className="signal-strip" aria-label="PWADrop summary">
        <div><strong>One tiny utility</strong><span>Quietly lives in your notification area</span></div>
        <div><strong>One natural gesture</strong><span>Keep using drag and drop the way you expect</span></div>
        <div><strong>Zero uploads</strong><span>Your files stay between your apps and your PC</span></div>
      </section>

      <section className="how" id="how-it-works">
        <div className="section-heading">
          <p className="eyebrow"><span /> How it works</p>
          <h2>The drop target never has to know.</h2>
          <p>PWADrop handles the delayed Windows file handoff at the source, then lets the original drag continue normally.</p>
        </div>
        <ol className="steps">
          <li><span>01</span><h3>You start a drag</h3><p>Drag an email, attachment, or file from a supported modern Windows app.</p></li>
          <li><span>02</span><h3>PWADrop bridges it</h3><p>The selected item is prepared locally using standard Windows drag-and-drop semantics.</p></li>
          <li><span>03</span><h3>Your destination receives it</h3><p>Drop into a browser, desktop app, upload field, or other ordinary file target.</p></li>
        </ol>
      </section>

      <section className="compatibility" id="compatibility">
        <div className="section-heading">
          <p className="eyebrow"><span /> Built for the awkward gap</p>
          <h2>Modern sources. Ordinary destinations.</h2>
          <p>PWADrop is designed for delayed file drags from Chromium-based Windows apps and destinations that already accept normal files.</p>
        </div>
        <div className="compat-grid">
          <article>
            <p className="card-kicker">Drag from</p>
            <h3>Modern Windows apps</h3>
            <p>New Outlook, supported browsers, web apps, and compatible desktop clients that produce files only when a drop occurs.</p>
          </article>
          <article>
            <p className="card-kicker">Drop into</p>
            <h3>The targets you already use</h3>
            <p>Browser upload zones, cloud-drive pages, creative tools, desktop software, and other standard Windows file targets.</p>
          </article>
          <article>
            <p className="card-kicker">Stay local</p>
            <h3>No PWADrop cloud</h3>
            <p>The handoff happens on your PC. PWADrop has no account system, advertising SDK, or remote file-processing service.</p>
          </article>
        </div>
      </section>

      <section className="demo-section" id="demos">
        <div>
          <p className="eyebrow"><span /> Product demos</p>
          <h2>See the bridge in motion.</h2>
          <p>Short compatibility demos and an app-by-app test matrix are being prepared for launch.</p>
        </div>
        <div className="demo-placeholder">
          <span className="play-mark" aria-hidden="true">▶</span>
          <div><strong>Demo library coming soon</strong><small>Outlook · browser uploads · desktop targets</small></div>
        </div>
      </section>

      <section className="faq-callout">
        <p className="eyebrow"><span /> Good questions</p>
        <div>
          <h2>Private by design. Practical by default.</h2>
          <a className="secondary-action" href="/faq">Read the FAQs <span aria-hidden="true">→</span></a>
        </div>
      </section>

      <footer className="footer">
        <a className="brand" href="/" aria-label="PWADrop home">
          <img src="/pwadrop-logo.png" alt="" width="30" height="30" />
          <span>PWADrop</span>
        </a>
        <p>Built by RiddleNEXT for Windows.</p>
        <div><a href="/privacy">Privacy</a><a href="/faq">FAQs</a><a href="https://github.com/LowkeyNEXT/PwaDrop">Source</a></div>
      </footer>
    </main>
  );
}
