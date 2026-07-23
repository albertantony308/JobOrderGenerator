const http = require('http');
const fs = require('fs');
const path = require('path');
const os = require('os');

const PORT = 9090;
const publishDir = path.join(__dirname, 'ClientApp', 'bin', 'Release', 'net10.0-windows', 'win-x64', 'publish');

function getLocalIp() {
    const interfaces = os.networkInterfaces();
    for (const name of Object.keys(interfaces)) {
        for (const iface of interfaces[name]) {
            if (iface.family === 'IPv4' && !iface.internal) {
                // Return Wi-Fi / LAN IP (192.168.x.x or 10.x.x.x)
                if (iface.address.startsWith('192.168.') || iface.address.startsWith('10.')) {
                    return iface.address;
                }
            }
        }
    }
    return '127.0.0.1';
}

const server = http.createServer((req, res) => {
    console.log(`[${new Date().toLocaleTimeString()}] Download request from ${req.socket.remoteAddress} for ${req.url}`);
    
    // Find setup exe or requested executable in publish directory
    const files = fs.readdirSync(publishDir);
    const setupFileName = files.find(f => f.startsWith('JobOrderGenerator_Setup_') && f.endsWith('.exe')) || 'JobOrderGenerator_Setup_v1.3.0.exe';
    const filePath = path.join(publishDir, setupFileName);

    if (fs.existsSync(filePath)) {
        const stat = fs.statSync(filePath);
        res.writeHead(200, {
            'Content-Type': 'application/octet-stream',
            'Content-Length': stat.size,
            'Content-Disposition': `attachment; filename="${setupFileName}"`,
            'Access-Control-Allow-Origin': '*'
        });
        const stream = fs.createReadStream(filePath);
        stream.pipe(res);
        console.log(`   -> Serving ${setupFileName} (${(stat.size / (1024 * 1024)).toFixed(1)} MB) to client!`);
    } else {
        res.writeHead(404);
        res.end('Setup installer file not found');
    }
});

const localIp = getLocalIp();
server.listen(PORT, '0.0.0.0', () => {
    console.log(`\n=========================================================`);
    console.log(`🚀 LOCAL LAN UPDATE SERVER RUNNING!`);
    console.log(`Paste this URL into your Cloud Admin Update File URL:`);
    console.log(`   http://${localIp}:${PORT}/JobOrderGenerator_Setup_v1.3.0.exe`);
    console.log(`=========================================================\n`);
});
