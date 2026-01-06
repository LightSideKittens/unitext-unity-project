const https = require('https');
const fs = require('fs');
const path = require('path');

const PORT = 8080;
const DIR = process.cwd();

const MIME = {
    '.html': 'text/html',
    '.js': 'application/javascript',
    '.wasm': 'application/wasm',
    '.data': 'application/octet-stream',
    '.json': 'application/json',
    '.png': 'image/png',
    '.jpg': 'image/jpeg',
    '.ico': 'image/x-icon'
};

// Генерируем самоподписанный сертификат если нет
let options;
if (fs.existsSync('localhost.pem') && fs.existsSync('localhost-key.pem')) {
    options = {
        cert: fs.readFileSync('localhost.pem'),
        key: fs.readFileSync('localhost-key.pem')
    };
} else {
    console.log('Certificates not found. Run: mkcert -key-file localhost-key.pem -cert-file localhost.pem localhost 127.0.0.1');
    process.exit(1);
}

https.createServer(options, (req, res) => {
    // Remove query string
    let url = req.url.split('?')[0];
    if (url === '/') url = '/index.html';
    let filePath = path.join(DIR, url);

    console.log(`[${req.method}] ${req.url} -> ${filePath}`);

    if (!fs.existsSync(filePath)) {
        console.log(`  404: ${filePath}`);
        res.writeHead(404);
        res.end('Not found');
        return;
    }

    const ext = path.extname(filePath);
    let contentType = MIME[ext.replace('.br', '').replace('.gz', '')] || 'application/octet-stream';

    // Unity WebGL .br и .gz файлы
    // SharedArrayBuffer requires these headers
    const headers = {
        'Content-Type': contentType,
        'Cross-Origin-Opener-Policy': 'same-origin',
        'Cross-Origin-Embedder-Policy': 'require-corp'
    };

    if (filePath.endsWith('.br')) {
        headers['Content-Encoding'] = 'br';
        headers['Content-Type'] = MIME[path.extname(filePath.slice(0, -3))] || 'application/octet-stream';
    } else if (filePath.endsWith('.gz')) {
        headers['Content-Encoding'] = 'gzip';
        headers['Content-Type'] = MIME[path.extname(filePath.slice(0, -3))] || 'application/octet-stream';
    }

    res.writeHead(200, headers);
    fs.createReadStream(filePath).pipe(res);

}).listen(PORT, () => {
    console.log(`https://localhost:${PORT}`);
});
