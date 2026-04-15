let map;
let routeLayer;
let userLocation = null;
let lastRouteParams = null;
let userMarker = null;
let elements;
let dropdownItems = [];

document.addEventListener('DOMContentLoaded', async () => {
    elements = {
        distanceInput: document.getElementById('distance'),
        profileSelect: document.getElementById('profile'),
        generateButton: document.getElementById('generateBtn'),
        useLocationButton: document.getElementById('useLocationBtn'),
        navUseLocationButton: document.getElementById('navUseLocationBtn'),
        downloadButton: document.getElementById('downloadDropdown'),
        dropdown: document.querySelector('.dropdown'),
        dropdownMenu: document.getElementById('dropdownMenu'),
        routeStatusMessage: document.getElementById('routeStatusMessage'),
        routeSelectionText: document.getElementById('routeSelectionText'),
        routeDetails: document.getElementById('routeDetails'),
        routeProfileValue: document.getElementById('routeProfileValue'),
        routeDistanceValue: document.getElementById('routeDistanceValue'),
        routeDurationValue: document.getElementById('routeDurationValue')
    };

    dropdownItems = Array.from(elements.dropdownMenu.querySelectorAll('[role="menuitem"]'));

    initializeButtonLabels();
    updateDownloadAvailability();

    map = L.map('map').setView([56.1572, 10.2107], 7);

    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        attribution: '© OpenStreetMap contributors'
    }).addTo(map);

    map.on('click', onMapClick);

    elements.generateButton.addEventListener('click', generateRoute);
    elements.useLocationButton.addEventListener('click', useMyLocation);
    elements.navUseLocationButton.addEventListener('click', useMyLocation);
    elements.downloadButton.addEventListener('click', toggleDropdown);
    elements.downloadButton.addEventListener('keydown', onDropdownButtonKeyDown);
    elements.dropdownMenu.addEventListener('click', onDropdownMenuClick);
    elements.dropdownMenu.addEventListener('keydown', onDropdownMenuKeyDown);
    elements.distanceInput.addEventListener('input', onRouteInputsChanged);
    elements.profileSelect.addEventListener('change', onRouteInputsChanged);

    document.addEventListener('click', (event) => {
        if (!event.target.closest('.dropdown')) {
            closeDropdown();
        }
    });

    document.addEventListener('keydown', (event) => {
        if (event.key === 'Escape' && isDropdownOpen()) {
            event.preventDefault();
            closeDropdown(true);
        }
    });
});

function onMapClick(e) {
    const { lat, lng } = e.latlng;

    updateUserMarker(lat, lng, 'Selected start location');
    map.setView([lat, lng], Math.max(map.getZoom(), 13));
    updateUserLocation(lat, lng, 'Start location selected. Generate a route when ready.');
}

function initializeButtonLabels() {
    [elements.generateButton, elements.useLocationButton, elements.navUseLocationButton, elements.downloadButton].forEach((button) => {
        button.dataset.defaultLabel = button.textContent.trim();
    });
}

function isDropdownOpen() {
    return elements.dropdown.classList.contains('open');
}

function openDropdown(focusIndex = null) {
    if (elements.downloadButton.disabled) {
        return;
    }

    elements.dropdown.classList.add('open');
    elements.dropdownMenu.hidden = false;
    elements.downloadButton.setAttribute('aria-expanded', 'true');

    if (focusIndex !== null && dropdownItems[focusIndex]) {
        dropdownItems[focusIndex].focus();
    }
}

function closeDropdown(restoreFocus = false) {
    elements.dropdown.classList.remove('open');
    elements.dropdownMenu.hidden = true;
    elements.downloadButton.setAttribute('aria-expanded', 'false');

    if (restoreFocus && !elements.downloadButton.disabled) {
        elements.downloadButton.focus();
    }
}

function toggleDropdown() {
    if (elements.downloadButton.disabled) {
        return;
    }

    if (isDropdownOpen()) {
        closeDropdown();
    } else {
        openDropdown();
    }
}

function onDropdownButtonKeyDown(event) {
    if (elements.downloadButton.disabled) {
        return;
    }

    switch (event.key) {
        case 'Enter':
        case ' ': {
            event.preventDefault();
            if (isDropdownOpen()) {
                closeDropdown();
            } else {
                openDropdown(0);
            }
            break;
        }
        case 'ArrowDown': {
            event.preventDefault();
            openDropdown(0);
            break;
        }
        case 'ArrowUp': {
            event.preventDefault();
            openDropdown(dropdownItems.length - 1);
            break;
        }
    }
}

function onDropdownMenuClick(event) {
    const item = event.target.closest('[role="menuitem"]');
    if (!item) {
        return;
    }

    event.preventDefault();
    closeDropdown();
    downloadRoute(item.dataset.format);
}

function onDropdownMenuKeyDown(event) {
    const currentIndex = dropdownItems.indexOf(document.activeElement);

    switch (event.key) {
        case 'ArrowDown': {
            event.preventDefault();
            focusDropdownItem(currentIndex + 1);
            break;
        }
        case 'ArrowUp': {
            event.preventDefault();
            focusDropdownItem(currentIndex - 1);
            break;
        }
        case 'Home': {
            event.preventDefault();
            focusDropdownItem(0);
            break;
        }
        case 'End': {
            event.preventDefault();
            focusDropdownItem(dropdownItems.length - 1);
            break;
        }
        case 'Escape': {
            event.preventDefault();
            closeDropdown(true);
            break;
        }
        case 'Tab': {
            closeDropdown();
            break;
        }
        case 'Enter':
        case ' ': {
            const item = document.activeElement.closest('[role="menuitem"]');
            if (!item) {
                return;
            }

            event.preventDefault();
            closeDropdown();
            downloadRoute(item.dataset.format);
            break;
        }
    }
}

function focusDropdownItem(index) {
    if (dropdownItems.length === 0) {
        return;
    }

    const normalizedIndex = (index + dropdownItems.length) % dropdownItems.length;
    dropdownItems[normalizedIndex].focus();
}

function onRouteInputsChanged() {
    if (lastRouteParams || routeLayer) {
        invalidateCurrentRoute('Route settings changed. Generate a new route to refresh the map and download.');
    }
}

function updateUserLocation(lat, lng, statusMessage) {
    userLocation = { lat, lon: lng };
    elements.routeSelectionText.textContent = `Start location: ${formatCoordinates(lat, lng)}`;
    invalidateCurrentRoute(statusMessage, false);
}

function updateUserMarker(lat, lng, label) {

    if (userMarker) {
        userMarker.setLatLng([lat, lng]);
        userMarker.bindPopup(label).openPopup();
    } else {
        userMarker = L.marker([lat, lng])
            .addTo(map)
            .bindPopup(label)
            .openPopup();
    }
}

async function useMyLocation() {
    if (!navigator.geolocation) {
        setStatus('error', 'Geolocation is not supported by your browser. Choose a start location on the map instead.');
        return;
    }

    setLocationButtonsBusy(true);
    setStatus('loading', 'Requesting your current location...');

    navigator.geolocation.getCurrentPosition(
        (position) => {
            const { latitude, longitude } = position.coords;

            map.setView([latitude, longitude], 13);
            updateUserMarker(latitude, longitude, 'Your current location');
            updateUserLocation(latitude, longitude, 'Location updated. Generate a route when ready.');
            setLocationButtonsBusy(false);
        },
        (error) => {
            setLocationButtonsBusy(false);
            setStatus('error', getGeolocationErrorMessage(error));
        },
        {
            enableHighAccuracy: true,
            timeout: 10000,
            maximumAge: 0
        }
    );
}

async function generateRoute() {
    if (!userLocation) {
        setStatus('error', 'Choose a start location by clicking on the map or using your current location.');
        return;
    }

    setButtonState(elements.generateButton, true, 'Generating route...');
    clearCurrentRouteDisplay();
    setStatus('loading', 'Generating a circular route...');

    const distance = parseFloat(elements.distanceInput.value);
    const profile = elements.profileSelect.value;

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

        if (!response.ok) {
            throw new Error(await getResponseErrorMessage(response, 'Unable to generate a route right now.'));
        }

        const route = await response.json();

        const latLngs = route.coordinates.map(c => [c.latitude, c.longitude]);
        routeLayer = L.polyline(latLngs, { color: '#2563eb', weight: 4 }).addTo(map);
        map.fitBounds(routeLayer.getBounds());

        lastRouteParams = {
            latitude: userLocation.lat,
            longitude: userLocation.lon,
            distance: distance,
            profile: profile,
            seed: route.seed
        };

        renderRouteDetails(route, profile);
        updateDownloadAvailability();
        setStatus('success', 'Route generated. The download menu is now available.');

    } catch (error) {
        clearCurrentRouteDisplay();
        setStatus('error', error.message);
    } finally {
        setButtonState(elements.generateButton, false);
    }
}

async function downloadRoute(format) {
    if (!lastRouteParams) {
        setStatus('error', 'Generate a route before downloading it.');
        return;
    }

    closeDropdown();
    setDownloadBusy(true, `Downloading ${format.toUpperCase()}...`);
    setStatus('loading', `Preparing your ${format.toUpperCase()} download...`);

    try {
        const response = await fetch(`/api/routes/download?format=${format}`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(lastRouteParams)
        });

        if (!response.ok) {
            throw new Error(await getResponseErrorMessage(response, `Unable to download a ${format.toUpperCase()} route right now.`));
        }

        const blob = await response.blob();
        const url = window.URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = `route.${format}`;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        window.URL.revokeObjectURL(url);
        setStatus('success', `Route downloaded as ${format.toUpperCase()}.`);
    } catch (error) {
        setStatus('error', error.message);
    } finally {
        setDownloadBusy(false);
    }
}

function renderRouteDetails(route, profile) {
    elements.routeProfileValue.textContent = getProfileLabel(profile);
    elements.routeDistanceValue.textContent = `${route.distance.toFixed(1)} km`;
    elements.routeDurationValue.textContent = formatDuration(route.duration);
    elements.routeDetails.hidden = false;
}

function clearCurrentRouteDisplay() {
    if (routeLayer) {
        map.removeLayer(routeLayer);
        routeLayer = null;
    }

    elements.routeDetails.hidden = true;
    elements.routeProfileValue.textContent = 'Not available';
    elements.routeDistanceValue.textContent = 'Not available';
    elements.routeDurationValue.textContent = 'Not available';
    lastRouteParams = null;
    updateDownloadAvailability();
}

function invalidateCurrentRoute(statusMessage, clearStatus = true) {
    clearCurrentRouteDisplay();

    if (statusMessage) {
        setStatus('info', statusMessage);
    } else if (clearStatus) {
        setStatus('info', 'Select a location on the map or use your current location to begin.');
    }
}

function setStatus(type, message) {
    elements.routeStatusMessage.className = `status-message status-${type}`;
    elements.routeStatusMessage.textContent = message;
}

function updateDownloadAvailability() {
    const hasRoute = Boolean(lastRouteParams);
    elements.downloadButton.disabled = !hasRoute;
    elements.downloadButton.setAttribute('aria-disabled', String(!hasRoute));

    if (!hasRoute) {
        closeDropdown();
    }
}

function setButtonState(button, isBusy, label = null) {
    const defaultLabel = button.dataset.defaultLabel;
    button.disabled = isBusy;
    button.classList.toggle('is-busy', isBusy);

    if (isBusy) {
        button.setAttribute('aria-busy', 'true');
        button.textContent = label ?? defaultLabel;
    } else {
        button.removeAttribute('aria-busy');
        button.textContent = defaultLabel;
    }
}

function setLocationButtonsBusy(isBusy) {
    setButtonState(elements.useLocationButton, isBusy, 'Getting location...');
    elements.navUseLocationButton.disabled = isBusy;
    elements.navUseLocationButton.classList.toggle('is-busy', isBusy);

    if (isBusy) {
        elements.navUseLocationButton.setAttribute('aria-busy', 'true');
        elements.navUseLocationButton.textContent = 'Getting location...';
    } else {
        elements.navUseLocationButton.removeAttribute('aria-busy');
        elements.navUseLocationButton.textContent = elements.navUseLocationButton.dataset.defaultLabel;
    }
}

function setDownloadBusy(isBusy, label = null) {
    elements.downloadButton.classList.toggle('is-busy', isBusy);

    if (isBusy) {
        elements.downloadButton.disabled = true;
        elements.downloadButton.setAttribute('aria-busy', 'true');
        elements.downloadButton.textContent = label ?? elements.downloadButton.dataset.defaultLabel;
    } else {
        elements.downloadButton.removeAttribute('aria-busy');
        elements.downloadButton.textContent = elements.downloadButton.dataset.defaultLabel;
        updateDownloadAvailability();
    }
}

async function getResponseErrorMessage(response, fallbackMessage) {
    let detail = '';

    try {
        const payload = await response.json();

        if (payload?.errors) {
            const firstErrorGroup = Object.values(payload.errors)[0];
            if (Array.isArray(firstErrorGroup) && firstErrorGroup.length > 0) {
                detail = firstErrorGroup[0];
            }
        } else {
            detail = payload?.detail || payload?.title || '';
        }

        const retryAfterSeconds = payload?.retryAfterSeconds ?? response.headers.get('Retry-After');
        if (response.status === 429 && retryAfterSeconds && detail) {
            detail = `${detail} Try again in ${retryAfterSeconds} seconds.`;
        }
    } catch {
        const retryAfterSeconds = response.headers.get('Retry-After');
        if (response.status === 429 && retryAfterSeconds) {
            detail = `Too many requests. Try again in ${retryAfterSeconds} seconds.`;
        }
    }

    return detail || fallbackMessage;
}

function getGeolocationErrorMessage(error) {
    switch (error.code) {
        case error.PERMISSION_DENIED:
            return 'Location permission was denied. Choose a start location on the map instead.';
        case error.POSITION_UNAVAILABLE:
            return 'Your location is currently unavailable. Try again or choose a start location on the map.';
        case error.TIMEOUT:
            return 'Getting your location took too long. Try again or choose a start location on the map.';
        default:
            return 'Could not get your location. Choose a start location on the map instead.';
    }
}

function getProfileLabel(profile) {
    const selectedOption = Array.from(elements.profileSelect.options)
        .find((option) => option.value === profile);

    return selectedOption?.textContent?.trim() ?? profile;
}

function formatCoordinates(lat, lng) {
    return `${lat.toFixed(5)}, ${lng.toFixed(5)}`;
}

function formatDuration(durationSeconds) {
    const totalMinutes = Math.max(1, Math.round(durationSeconds / 60));
    const hours = Math.floor(totalMinutes / 60);
    const minutes = totalMinutes % 60;

    if (hours === 0) {
        return `${totalMinutes} min`;
    }

    if (minutes === 0) {
        return `${hours} hr`;
    }

    return `${hours} hr ${minutes} min`;
}