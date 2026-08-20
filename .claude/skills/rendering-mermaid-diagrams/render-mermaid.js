#!/usr/bin/env node
// Renders a Mermaid diagram to a self-contained SVG with a real light background,
// via the public mermaid.ink service (no local browser/puppeteer needed).
//
// Usage:
//   node render-mermaid.js <input.mmd|input.md> <output.svg> [--bg=#ffffff] [--block=N]
//
// - input.mmd: raw Mermaid source.
// - input.md: the Nth ```mermaid fenced block is extracted (--block, 1-based, default 1).
// - output.svg: written with an injected background rect (see SKILL.md for why).

const fs = require('fs');
const path = require('path');
const { execFileSync } = require('child_process');

function fail(msg) {
  console.error('ERROR:', msg);
  process.exit(1);
}

const args = process.argv.slice(2);
const positional = args.filter(a => !a.startsWith('--'));
const flags = Object.fromEntries(
  args.filter(a => a.startsWith('--')).map(a => {
    const [k, v] = a.slice(2).split('=');
    return [k, v ?? true];
  })
);

const [inputPath, outputPath] = positional;
if (!inputPath || !outputPath) {
  fail('usage: node render-mermaid.js <input.mmd|input.md> <output.svg> [--bg=#ffffff] [--block=N]');
}

const bg = flags.bg || '#ffffff';
const blockIndex = parseInt(flags.block || '1', 10);

let raw = fs.readFileSync(inputPath, 'utf8');
let graph;
if (inputPath.endsWith('.mmd')) {
  graph = raw;
} else {
  const blocks = [...raw.matchAll(/```mermaid\r?\n([\s\S]*?)```/g)].map(m => m[1]);
  if (blocks.length === 0) fail(`no \`\`\`mermaid block found in ${inputPath}`);
  if (!blocks[blockIndex - 1]) fail(`--block=${blockIndex} but only ${blocks.length} mermaid block(s) found`);
  graph = blocks[blockIndex - 1];
}

const b64url = Buffer.from(graph, 'utf8')
  .toString('base64')
  .replace(/\+/g, '-')
  .replace(/\//g, '_')
  .replace(/=+$/, '');

const url = `https://mermaid.ink/svg/${b64url}`;
const tmpOut = outputPath + '.tmp';

execFileSync('curl', ['-sS', '-f', '-o', tmpOut, '--max-time', '30', url]);

let svg = fs.readFileSync(tmpOut, 'utf8');
fs.unlinkSync(tmpOut);

// mermaid.ink returns HTTP 200 even for a render error (a small "Syntax error" SVG),
// so size/shape-sanity-check instead of trusting the status code alone.
if (svg.length < 500 || !svg.includes('<svg')) {
  fail(`response does not look like a valid diagram render (got ${svg.length} bytes) — check Mermaid syntax`);
}
if (/Syntax error/i.test(svg)) {
  fail('mermaid.ink reported a syntax error in the diagram — fix the Mermaid source and retry');
}

// The /svg endpoint ignores bgColor and returns a transparent background, which
// disappears (or clashes) against a dark-theme Markdown viewer. Inject an explicit
// background rect as the first drawn element instead.
const viewBoxMatch = svg.match(/<svg[^>]*viewBox="([^"]+)"/);
if (!viewBoxMatch) fail('could not find viewBox in rendered SVG');
const [x, y, w, h] = viewBoxMatch[1].split(' ');
const openTagEnd = svg.indexOf('>', svg.indexOf('<svg')) + 1;
const bgRect = `<rect x="${x}" y="${y}" width="${w}" height="${h}" fill="${bg}"/>`;
svg = svg.slice(0, openTagEnd) + bgRect + svg.slice(openTagEnd);

fs.writeFileSync(outputPath, svg);
console.log(`OK: wrote ${outputPath} (${svg.length} bytes)`);
