import path from 'node:path';
import { fileURLToPath } from 'node:url';
import sharp from 'sharp';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const projectRoot = path.resolve(__dirname, '..');

const inputPath = path.join(projectRoot, 'public', 'images', 'home-welcome.webp');
const outputPath = path.join(projectRoot, 'public', 'images', 'social-preview.webp');

const width = 1200;
const height = 630;

// Simple, welcome-inspired overlay (keeps the preview consistent for every URL).
const overlaySvg = `
<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}" viewBox="0 0 ${width} ${height}">
  <defs>
    <linearGradient id="fade" x1="0" y1="0" x2="0" y2="1">
      <stop offset="0%" stop-color="#000" stop-opacity="0" />
      <stop offset="58%" stop-color="#000" stop-opacity="0" />
      <stop offset="100%" stop-color="#000" stop-opacity="0.6" />
    </linearGradient>
  </defs>

  <rect x="0" y="0" width="${width}" height="${height}" fill="url(#fade)" />

  <g transform="translate(78, 470)">
    <!-- Names in serif italic, matching welcome screen -->
    <text x="0" y="0" fill="#fff" font-family="Georgia, 'Times New Roman', Times, serif" font-size="68" font-weight="500" font-style="italic">
      Chelsea
    </text>
    <text x="250" y="0" fill="rgba(255,255,255,0.7)" font-family="Georgia, 'Times New Roman', Times, serif" font-size="68" font-weight="400">
      &amp;
    </text>
    <text x="315" y="0" fill="#fff" font-family="Georgia, 'Times New Roman', Times, serif" font-size="68" font-weight="500" font-style="italic">
      Warre
    </text>
  </g>

  <!-- Tagline in subtle serif -->
  <text x="82" y="540" fill="rgba(255,255,255,0.85)" font-family="Georgia, 'Times New Roman', Times, serif" font-size="28" font-style="italic">
    Come for the love, stay for the party
  </text>

  <!-- Date -->
  <text x="82" y="588" fill="rgba(255,255,255,0.95)" font-family="system-ui, -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Arial, sans-serif" font-size="32" font-weight="600">
    6 juni 2026
  </text>
</svg>
`;

await sharp(inputPath)
  .resize(width, height, { fit: 'cover', position: 'centre' })
  .composite([{ input: Buffer.from(overlaySvg), top: 0, left: 0 }])
  .webp({ quality: 90 })
  .toFile(outputPath);

console.log(`Wrote ${outputPath}`);
