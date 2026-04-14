const fs = require('fs');
const path = require('path');

const dir = path.join(__dirname, 'Assets', 'UniText');

const pkg = JSON.parse(fs.readFileSync(path.join(dir, 'package.json'), 'utf8'));
delete pkg.license;
fs.writeFileSync(path.join(dir, 'package.json'), JSON.stringify(pkg, null, 4) + '\n');

let readme = fs.readFileSync(path.join(dir, 'README.md'), 'utf8');
readme = readme.replace(/## [^\r\n]* License\r?\n[\s\S]*?(?=## [^\r\n]* Third-Party)/, '');
fs.writeFileSync(path.join(dir, 'README.md'), readme);
