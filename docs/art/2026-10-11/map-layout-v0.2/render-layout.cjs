// Renders the new data-based SVG map drawings; does not edit source attachments.
const fs = require('node:fs');
const path = require('node:path');
const sharp = require(path.join(process.argv[2], 'sharp'));
async function main() {
  const files = fs.readdirSync(__dirname).filter(name => name.endsWith('.svg'));
  for (const file of files) {
    const target = path.join(__dirname, file.replace(/\.svg$/, '.png'));
    if (fs.existsSync(target)) throw new Error('Already exists: ' + target);
    const info = await sharp(path.join(__dirname, file)).png().toFile(target);
    console.log(JSON.stringify({file: path.basename(target), width: info.width, height: info.height}));
  }
}
main().catch(error => { console.error(error.message); process.exitCode = 1; });
