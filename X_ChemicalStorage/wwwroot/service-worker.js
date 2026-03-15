// wwwroot/service-worker.js
self.addEventListener('install', () => {
    console.log('Service Worker Installed');
});

self.addEventListener('fetch', event => {
    event.respondWith(fetch(event.request));
});
