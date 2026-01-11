import path from 'node:path';
import { fileURLToPath } from 'node:url';
import sharp from 'sharp';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const projectRoot = path.resolve(__dirname, '..');

const inputPath = path.join(projectRoot, 'public', 'images', 'home-welcome.webp');
const outputPath = path.join(projectRoot, 'public', 'images', 'social-preview.png');

const width = 1200;
const height = 630;

// Simple, welcome-inspired overlay (keeps the preview consistent for every URL).
const overlaySvg = `
<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}" viewBox="0 0 ${width} ${height}">
  <defs>
    <linearGradient id="fade" x1="0" y1="0" x2="0" y2="1">
      <stop offset="0%" stop-color="#000" stop-opacity="0" />
      <stop offset="58%" stop-color="#000" stop-opacity="0" />
      <stop offset="100%" stop-color="#000" stop-opacity="0.55" />
    </linearGradient>
  </defs>

  <rect x="0" y="0" width="${width}" height="${height}" fill="url(#fade)" />

  <text x="78" y="500" fill="#fff" font-family="Georgia, 'Times New Roman', Times, serif" font-size="72" font-weight="600">
    Chelsea &amp; Warre
  </text>
  <text x="82" y="555" fill="rgba(255,255,255,0.92)" font-family="system-ui, -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Arial, sans-serif" font-size="34" font-weight="600">
    6 juni 2026
  </text>
</svg>
`;

await sharp(inputPath)
  .resize(width, height, { fit: 'cover', position: 'centre' })
  .composite([{ input: Buffer.from(overlaySvg), top: 0, left: 0 }])
  .png({ compressionLevel: 9 })
  .toFile(outputPath);

console.log(`Wrote ${outputPath}`);
