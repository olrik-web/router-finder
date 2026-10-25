import { buildRouteFile } from './route-export.js';
import { formatCoordinates, formatDuration, readErrorMessage } from './format.js';

const DEFAULT_VIEW = { center: [56.1572, 10.2107], zoom: 7 };
const START_ZOOM = 14;
const FOOT_PROFILES = new Set(['foot-walking', 'foot-hiking']);

const el = {
    searchForm: document.getElementById('searchForm'),
    placeSearch: document.getElementById('placeSearch'),
    searchButton: document.getElementById('searchButton'),
    useLocationButton: document.getElementById('useLocationButton'),
    routeForm: document.getElementById('routeForm'),
    distance: document.getElementById('distance'),
    profile: document.getElementById('profile'),
    generateButton: document.getElementById('generateButton'),
    status: document.getElementById('status'),
    startText: document.getElementById('startText'),
    routeDetails: document.getElementById('routeDetails'),
    routeProfile: document.getElementById('routeProfile'),
    routeDistance: document.getElementById('routeDistance'),
    routeDuration: document.getElementById('routeDuration'),
};

const state = {
    /** @type {{ lat: number, lng: number } | null} */
    start: null,
    /** @type {{ params: { profile: string }, route: import('./route-export.js').Route } | null} */
    current: null,
    /** @type {AbortController | null} */
    pendingRequest: null,
};

const map = L.map('map').setView(DEFAULT_VIEW.center, DEFAULT_VIEW.zoom);
L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
    maxZoom: 19,
    attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
}).addTo(map);

let startMarker = null;
let routeLayer = null;

map.on('click', (event) => {
    setStart(event.latlng.lat, event.latlng.lng);
    setStatus('info', 'Start point set. Choose a distance and press Generate route.');
});
el.searchForm.addEventListener('submit', searchPlace);
el.useLocationButton.addEventListener('click', useMyLocation);
el.routeForm.addEventListener('submit', generateRoute);
el.distance.addEventListener('input', onSettingsChanged);
el.profile.addEventListener('change', onSettingsChanged);
el.routeDetails.addEventListener('click', (event) => {
    const button = event.target.closest('button[data-format]');
    if (button) {
        downloadRoute(button.dataset.format);
    }
});

/**
 * Moves the start point and throws away any route or request for the old one.
 * @param {number} lat @param {number} lng @param {string} [placeName] shown instead of the coordinates
 */
function setStart(lat, lng, placeName) {
    state.start = { lat, lng };
    clearRoute();

    const popup = document.createElement('span');
    popup.textContent = placeName ?? 'Start point';

    if (startMarker) {
        startMarker.setLatLng([lat, lng]);
    } else {
        startMarker = L.marker([lat, lng], { alt: 'Start point' }).addTo(map);
    }
    startMarker.bindPopup(popup).openPopup();

    map.setView([lat, lng], Math.max(map.getZoom(), START_ZOOM));
    el.startText.textContent = `Start: ${placeName ?? formatCoordinates(lat, lng)}`;
}

/** @param {SubmitEvent} event */
async function searchPlace(event) {
    event.preventDefault();

    const query = el.placeSearch.value.trim();
    if (!query) {
        setStatus('error', 'Type an address or place name to search for.');
        el.placeSearch.focus();
        return;
    }

    setBusy(el.searchButton, true, 'Searching...');
    setStatus('loading', `Searching for ${query}...`);

    try {
        // Nominatim allows one request per second, which a submit button keeps us well under.
        const url = new URL('https://nominatim.openstreetmap.org/search');
        url.search = new URLSearchParams({ q: query, format: 'jsonv2', limit: '1' }).toString();

        const response = await fetch(url);
        if (!response.ok) {
            throw new Error('Search is not available right now. Click the map instead.');
        }

        const [place] = await response.json();
        if (!place) {
            setStatus('error', `Nothing found for ${query}. Try a street name and town, or click the map.`);
            return;
        }

        setStart(Number(place.lat), Number(place.lon), place.display_name);
        setStatus('info', 'Start point set. Choose a distance and press Generate route.');
    } catch (error) {
        setStatus('error', error instanceof TypeError
            ? 'Search is not available right now. Click the map instead.'
            : error.message);
    } finally {
        setBusy(el.searchButton, false);
    }
}

function useMyLocation() {
    if (!navigator.geolocation) {
        setStatus('error', 'This browser cannot share your location. Search for a place or click the map instead.');
        return;
    }

    setBusy(el.useLocationButton, true, 'Finding you...');
    setStatus('loading', 'Waiting for your location...');

    navigator.geolocation.getCurrentPosition(
        ({ coords }) => {
            setBusy(el.useLocationButton, false);
            setStart(coords.latitude, coords.longitude, 'Your location');
            setStatus('info', 'Using your location. Choose a distance and press Generate route.');
        },
        (error) => {
            setBusy(el.useLocationButton, false);
            setStatus('error', geolocationErrorMessage(error));
        },
        { enableHighAccuracy: true, timeout: 10000, maximumAge: 60000 },
    );
}

/** @param {SubmitEvent} event */
async function generateRoute(event) {
    event.preventDefault();

    if (!el.routeForm.reportValidity()) {
        return;
    }

    if (!state.start) {
        setStatus('error', 'Set a start point first: search for a place, click the map or use your location.');
        return;
    }

    clearRoute();

    // Take a copy of everything now. The user can change the start or settings while we wait.
    const params = {
        latitude: state.start.lat,
        longitude: state.start.lng,
        distance: el.distance.valueAsNumber,
        profile: el.profile.value,
    };
    const request = new AbortController();
    state.pendingRequest = request;

    setBusy(el.generateButton, true, 'Generating...');
    setStatus('loading', 'Finding a route. This can take a few seconds.');

    try {
        const response = await fetch('/api/routes/generate', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(params),
            signal: request.signal,
        });

        if (!response.ok) {
            throw new Error(await readErrorMessage(response, 'Could not make a route right now. Try again.'));
        }

        const route = await response.json();
        showRoute(params, route);
        setStatus('success', `Route ready: ${route.distance.toFixed(1)} km.`);
    } catch (error) {
        if (error.name === 'AbortError') {
            return;
        }
        setStatus('error', error instanceof TypeError
            ? 'Could not reach the server. Check your connection and try again.'
            : error.message);
    } finally {
        if (state.pendingRequest === request) {
            state.pendingRequest = null;
            setBusy(el.generateButton, false);
        }
    }
}

/** @param {'gpx' | 'geojson'} format */
function downloadRoute(format) {
    if (!state.current) {
        return;
    }

    const { params, route } = state.current;
    const file = buildRouteFile(format, route, {
        name: `${route.distance.toFixed(1)} km ${profileLabel(params.profile).toLowerCase()} loop`,
        profile: params.profile,
    });

    const url = URL.createObjectURL(new Blob([file.content], { type: file.mimeType }));
    const link = document.createElement('a');
    link.href = url;
    link.download = file.fileName;
    link.click();
    // Some browsers cancel the download if the URL is revoked straight away.
    setTimeout(() => URL.revokeObjectURL(url), 1000);

    setStatus('success', `Saved ${file.fileName}.`);
}

function onSettingsChanged() {
    if (state.current || state.pendingRequest) {
        clearRoute();
        setStatus('info', 'Settings changed. Press Generate route to get a new route.');
    }
}

function showRoute(params, route) {
    state.current = { params, route };

    routeLayer = L.polyline(route.coordinates.map((c) => [c.latitude, c.longitude]), {
        color: getComputedStyle(document.documentElement).getPropertyValue('--route-line').trim(),
        weight: 5,
        attribution: 'Route: <a href="https://openrouteservice.org/">openrouteservice</a>',
    }).addTo(map);
    map.fitBounds(routeLayer.getBounds(), { padding: [24, 24] });

    const pace = FOOT_PROFILES.has(params.profile) ? ' at walking pace' : '';
    el.routeProfile.textContent = profileLabel(params.profile);
    el.routeDistance.textContent = `${route.distance.toFixed(1)} km`;
    el.routeDuration.textContent = `${formatDuration(route.duration)}${pace}`;
    el.routeDetails.hidden = false;
}

/** Removes the drawn route and cancels a route request that is still running. */
function clearRoute() {
    if (state.pendingRequest) {
        state.pendingRequest.abort();
        state.pendingRequest = null;
        setBusy(el.generateButton, false);
    }

    if (routeLayer) {
        routeLayer.remove();
        routeLayer = null;
    }

    state.current = null;
    el.routeDetails.hidden = true;
}

/** @param {'info' | 'success' | 'error' | 'loading'} type @param {string} message */
function setStatus(type, message) {
    el.status.className = `status status-${type}`;
    el.status.textContent = message;
}

/** @param {HTMLButtonElement} button @param {boolean} busy @param {string} [label] */
function setBusy(button, busy, label) {
    button.dataset.label ??= button.textContent;
    button.disabled = busy;
    button.classList.toggle('is-busy', busy);
    button.toggleAttribute('aria-busy', busy);
    button.textContent = busy && label ? label : button.dataset.label;
}

/** @param {string} profile */
function profileLabel(profile) {
    return [...el.profile.options].find((option) => option.value === profile)?.textContent ?? profile;
}

/** @param {GeolocationPositionError} error */
function geolocationErrorMessage(error) {
    switch (error.code) {
        case error.PERMISSION_DENIED:
            return 'Location access was blocked. Search for a place or click the map instead.';
        case error.TIMEOUT:
            return 'Finding your location took too long. Try again, or click the map.';
        default:
            return 'Your location is not available. Search for a place or click the map instead.';
    }
}
