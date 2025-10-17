let map;
let routeLayer;
let userLocation = null;
let lastRouteParams = null;
let userMarker = null;

document.addEventListener('DOMContentLoaded', async () => {
    // Default to Aarhus
    map = L.map('map').setView([56.1572, 10.2107], 7);

    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        attribution: '© OpenStreetMap contributors'
    }).addTo(map);

    // Enable click-to-select-location
    map.on('click', onMapClick);

    const dropdownBtn = document.getElementById('downloadDropdown');
    const dropdownMenu = document.getElementById('dropdownMenu');

    dropdownBtn.addEventListener('click', () => {
        if (dropdownBtn.disabled) return;
        dropdownMenu.parentElement.classList.toggle('open');
    });

    dropdownMenu.addEventListener('click', async (e) => {
        e.preventDefault();
        const format = e.target.getAttribute('data-format');
        if (format) {
            dropdownMenu.parentElement.classList.remove('open');
            await downloadRoute(format);
        }
    });

    document.addEventListener('click', (e) => {
        if (!e.target.closest('.dropdown')) {
            dropdownMenu.parentElement.classList.remove('open');
        }
    });

    await useMyLocation(); // optional auto-locate on start
});

function onMapClick(e) {
    const { lat, lng } = e.latlng;

    if (userMarker) {
        userMarker.setLatLng([lat, lng]);
        // userMarker.bindPopup('Selected location').openPopup();
    } else {
        userMarker = L.marker([lat, lng])
            .addTo(map)
            // .bindPopup('Selected location')
            .openPopup();
    }

    userLocation = { lat, lon: lng };

    const routeInfo = document.getElementById('routeInfo');
    routeInfo.style.display = 'block';
        routeInfo.innerHTML = `
        📍 Selected location: ${lat.toFixed(5)}, ${lng.toFixed(5)}
    `;
}

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
            const { latitude, longitude } = position.coords;

            userLocation = { lat: latitude, lon: longitude };
            map.setView([latitude, longitude], 13);

            // Add or update marker
            if (userMarker) {
                userMarker.setLatLng([latitude, longitude]);
                userMarker.bindPopup('Your location').openPopup();
            } else {
                userMarker = L.marker([latitude, longitude])
                    .addTo(map)
                    .bindPopup('Your location')
                    .openPopup();
            }

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
        alert('Please set a location by clicking on the map or using your location.');
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

        if (routeLayer) map.removeLayer(routeLayer);

        // Draw route
        const latLngs = route.coordinates.map(c => [c.latitude, c.longitude]);
        routeLayer = L.polyline(latLngs, { color: '#2563eb', weight: 4 }).addTo(map);
        map.fitBounds(routeLayer.getBounds());
        
        

        const routeInfo = document.getElementById('routeInfo');
        routeInfo.style.display = 'block';
        routeInfo.innerHTML = `
            ✅ <strong>Route Generated!</strong><br>
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

    } catch (error) {
        alert('Error generating route: ' + error.message);
    } finally {
        btn.disabled = false;
        btn.textContent = 'Generate Route';
    }
}

async function downloadRoute(format) {
    if (!lastRouteParams) {
        alert('Please generate a route first.');
        return;
    }

    const btn = document.getElementById('downloadDropdown');
    btn.disabled = true;
    btn.textContent = `Downloading ${format.toUpperCase()}...`;

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
        btn.textContent = 'Download Route ▾';
    }
}