const http = require("http"), fs = require("fs"), path = require("path");
const root = path.resolve(process.argv[2]), port = +process.argv[3] || 5173;
const types = { ".html": "text/html", ".css": "text/css", ".js": "text/javascript", ".png": "image/png", ".jpg": "image/jpeg", ".ico": "image/x-icon", ".mp4": "video/mp4", ".zip": "application/zip", ".svg": "image/svg+xml" };
http.createServer((req, res) => {
  let p = path.normalize(path.join(root, decodeURIComponent(req.url.split("?")[0])));
  if (!p.startsWith(root)) { res.writeHead(403); return res.end(); }
  if (fs.existsSync(p) && fs.statSync(p).isDirectory()) p = path.join(p, "index.html");
  fs.readFile(p, (err, data) => {
    if (err) { res.writeHead(404); return res.end("not found"); }
    res.writeHead(200, { "Content-Type": types[path.extname(p)] || "application/octet-stream" }); res.end(data);
  });
}).listen(port, "127.0.0.1", () => console.log("serving " + root + " on " + port));
