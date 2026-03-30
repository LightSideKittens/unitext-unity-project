@echo off

where http-server >nul 2>&1 || (
    echo Installing http-server...
    npm install -g http-server
)

where mkcert >nul 2>&1 || (
    echo mkcert not found. Running HTTP only...
    http-server -c-1 --gzip
    exit /b
)

if not exist localhost.pem (
    mkcert -install
    mkcert -key-file localhost-key.pem -cert-file localhost.pem localhost 127.0.0.1
)

echo https://localhost:8080
http-server --ssl --cert localhost.pem --key localhost-key.pem --gzip --brotli -c-1
