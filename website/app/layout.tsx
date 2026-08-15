import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "PWADrop — Drag files from modern apps, drop them anywhere",
  description: "A lightweight Windows utility that makes delayed file drags work with ordinary drop targets.",
  icons: {
    icon: "/pwadrop-logo.png",
    shortcut: "/pwadrop-logo.png",
  },
  openGraph: {
    title: "PWADrop — Drag files from modern apps, drop them anywhere",
    description: "A lightweight Windows utility that makes delayed file drags work with ordinary drop targets.",
    images: [{ url: "/og.png", width: 1728, height: 909, alt: "PWADrop product card" }],
  },
  twitter: {
    card: "summary_large_image",
    title: "PWADrop — Drag files from modern apps, drop them anywhere",
    description: "A lightweight Windows utility that makes delayed file drags work with ordinary drop targets.",
    images: ["/og.png"],
  },
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en">
      <body>{children}</body>
    </html>
  );
}
