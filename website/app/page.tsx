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
          <p className="eyebrow"><span /> Compatibility</p>
          <h2>One bridge for the apps you already use.</h2>
          <p>The current Windows build recognizes common Chromium, WebView2, and Electron source families, then hands the drag to any destination that accepts normal Windows files.</p>
        </div>
        <div className="compat-board">
          <article className="compat-column">
            <div className="compat-column-heading">
              <span className="direction-mark" aria-hidden="true">↗</span>
              <div><p className="card-kicker">Drag from</p><h3>Supported source families</h3></div>
            </div>
            <div className="app-group">
              <strong>Browsers &amp; installed web apps</strong>
              <p>Edge, Chrome, Brave, Chromium, Opera, Vivaldi, and compatible PWAs running through them.</p>
            </div>
            <div className="app-group">
              <strong>WebView2 apps</strong>
              <p>New Outlook and New Teams, including files and messages exposed through their Windows drag objects.</p>
            </div>
            <div className="app-group">
              <strong>Recognized Electron apps</strong>
              <p>Slack, Missive, Superhuman, and other explicitly supported, trusted application roots.</p>
            </div>
          </article>
          <div className="compat-bridge" aria-hidden="true"><span>PWADrop</span><b>→</b></div>
          <article className="compat-column">
            <div className="compat-column-heading">
              <span className="direction-mark destination" aria-hidden="true">↓</span>
              <div><p className="card-kicker">Drop into</p><h3>Standard file destinations</h3></div>
            </div>
            <div className="app-group">
              <strong>Browser upload surfaces</strong>
              <p>Google Drive, cloud storage, ticketing tools, compose windows, and sites that accept ordinary file drops.</p>
            </div>
            <div className="app-group">
              <strong>Windows desktop apps</strong>
              <p>File Explorer, WinForms, WPF, creative tools, and other applications that accept standard file paths.</p>
            </div>
            <div className="app-group">
              <strong>No destination plug-in</strong>
              <p>The target receives a normal Windows file drop, so it does not need to know PWADrop exists.</p>
            </div>
          </article>
        </div>
        <aside className="compat-note">
          <strong>Designed for normal user sessions on Windows 11.</strong>
          <span>Elevated apps, ARM64, security-restricted enterprise environments, and unknown Electron executables may require separate validation. App updates can also change drag behavior.</span>
          <a href="/faq">Read compatibility FAQs <span aria-hidden="true">→</span></a>
        </aside>
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
