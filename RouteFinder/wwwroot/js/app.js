let map;
let routeLayer;
let userLocation = null;
let lastRouteParams = null;

document.addEventListener('DOMContentLoaded', async() => {
    // Default to Aarhus
    map = L.map('map').setView([56.1572, 10.2107], 7);
     

    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        attribution: '© OpenStreetMap contributors'
    }).addTo(map);
    
    await useMyLocation();
});

async function useMyLocation() {
    if (!navigator.geolocation) {
        alert('Geolocation is not supported by your browser');
        return;
    }

    const btn = document.getElementById('useLocationBtn');
    btn.disabled = true;
    btn.textContent = 'Getting location...';

    navigator.geolocation.getCurrentPosition(
        (position) => {
            userLocation = {
                lat: position.coords.latitude,
                lon: position.coords.longitude
            };
            map.setView([userLocation.lat, userLocation.lon], 13);

            // Add marker
            L.marker([userLocation.lat, userLocation.lon])
                .addTo(map)
                .bindPopup('Your location')
                .openPopup();

            btn.disabled = false;
            btn.textContent = 'Use My Location';
        },
        (error) => {
            alert('Could not get your location');
            btn.disabled = false;
            btn.textContent = 'Use My Location';
        }
    );
}

async function generateRoute() {
    if (!userLocation) {
        alert('Please use your location first');
        return;
    }

    const btn = document.getElementById('generateBtn');
    btn.disabled = true;
    btn.textContent = 'Generating...';

    const distance = parseFloat(document.getElementById('distance').value);
    const profile = document.getElementById('profile').value;

    try {
        const response = await fetch('/api/routes/generate', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                latitude: userLocation.lat,
                longitude: userLocation.lon,
                distance: distance,
                profile: profile
            })
        });

        if (!response.ok) throw new Error('Failed to generate route');

        const route = await response.json();

        if (routeLayer) {
            map.removeLayer(routeLayer);
        }

        // Draw new route
        const latLngs = route.coordinates.map(c => [c.latitude, c.longitude]);
        routeLayer = L.polyline(latLngs, { color: '#2563eb', weight: 4 }).addTo(map);
        map.fitBounds(routeLayer.getBounds());

        document.getElementById('routeInfo').innerHTML = `
            <strong>Route Generated!</strong><br>
            Distance: ${route.distance.toFixed(1)} km<br>
            Duration: ${Math.round(route.duration / 60)} minutes
        `;

        lastRouteParams = {
            latitude: userLocation.lat,
            longitude: userLocation.lon,
            distance: distance,
            profile: profile,
            seed: route.seed
        };

        document.getElementById('downloadBtn').disabled = false;


    } catch (error) {
        alert('Error generating route: ' + error.message);
    } finally {
        btn.disabled = false;
        btn.textContent = 'Generate Route';
    }
}

async function downloadRoute() {
    if (!lastRouteParams) {
        alert('Please generate a route first.');
        return;
    }

    const format = document.getElementById('format').value;

    const btn = document.getElementById('downloadBtn');
    btn.disabled = true;
    btn.textContent = 'Downloading...';

    try {
        const response = await fetch(`/api/routes/download?format=${format}`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(lastRouteParams)
        });

        if (!response.ok) throw new Error('Failed to download route');

        const blob = await response.blob();
        const url = window.URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = `route.${format}`;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        window.URL.revokeObjectURL(url);
    } catch (error) {
        alert('Error downloading route: ' + error.message);
    } finally {
        btn.disabled = false;
        btn.textContent = 'Download Route';
    }
}
