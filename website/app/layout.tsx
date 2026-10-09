import type { Metadata } from "next";
import "./globals.css";
import "./marketing.css";

export const metadata: Metadata = {
  icons: {
    icon: "/pwadrop-logo.png",
    shortcut: "/pwadrop-logo.png",
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
